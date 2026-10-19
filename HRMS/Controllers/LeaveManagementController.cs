using HRMS.Models;
using System.Security.Claims;
using HRMS.Application.Employees;
using HRMS.Application.Identity;
using HRMS.Application.LeaveManagement;
using HRMS.Application.Organization;
using HRMS.Models.LeaveManagement;
using HRMS.Application.Reporting;
using HRMS.Models.Reporting;
using HRMS.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Controllers;

[Authorize(Roles = IdentityRoles.UseLeaves)]
public sealed class LeaveManagementController(
    ILeaveService leaveService,
    IUserAccountService userAccountService,
    IEmployeeService employeeService,
    HRMS.Application.Common.IBusinessClock businessClock,
    IDepartmentService departmentService, IStringLocalizer<SharedResource> T, IReportFileExporter exporter) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? status, int? year, CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 2100)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = HRMS.Application.Common.Messages.Get("InvalidYearTitle"),
                Detail = HRMS.Application.Common.Messages.Get("Business063")
            });
        }

        var employeeId = await GetEmployeeIdAsync(cancellationToken);
        var isHr = CanManageAllRequests();
        if (!isHr && employeeId is null)
        {
            return Forbid();
        }
        var isDepartmentManager = User.IsInRole(IdentityRoles.DepartmentManager) && !isHr;
        var managerDepartmentId = isDepartmentManager
            ? await GetActorDepartmentIdAsync(employeeId, cancellationToken)
            : null;
        if (isDepartmentManager && managerDepartmentId is null)
        {
            return Forbid();
        }
        var filter = new LeaveFilter(
            EmployeeId: isHr || isDepartmentManager ? null : employeeId,
            DepartmentId: managerDepartmentId,
            Status: status,
            Year: year);
        var requests = await leaveService.SearchRequestsAsync(filter, DisplayPagination.Request(Request), cancellationToken);
        ViewData["Pagination"] = DisplayPagination.Create(Request, requests.TotalCount, requests.Page);
        var model = new LeaveListViewModel
        {
            Status = status,
            Year = year,
            Requests = requests.Items
        };
        ViewBag.CurrentEmployeeId = employeeId;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> MyReport(string? status, int? year, CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 2100) return BadRequest();
        var employeeId = await GetEmployeeIdAsync(cancellationToken);
        if (employeeId is null) return Forbid();
        var reportYear = year ?? businessClock.Today.Year;
        var requests = await leaveService.SearchRequestsAsync(new LeaveFilter(EmployeeId: employeeId.Value, Status: status, Year: reportYear), this.IsExport() ? new(ShowAll: true) : DisplayPagination.Request(Request), cancellationToken);
        var balances = await leaveService.SearchBalancesAsync(employeeId.Value, reportYear, this.IsExport() ? new(ShowAll: true) : DisplayPagination.Request(Request, "Balance"), cancellationToken);
        if (this.IsExport())
        {
            var ar = System.Globalization.CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
            return await this.DownloadAsync(exporter, T, T["MyLeaveReport"].Value + " " + reportYear, "my-leaves",
                businessClock.ToLocal(DateTime.UtcNow)!.Value, cancellationToken,
                new ReportTable(T["LeaveBalancesTitle", reportYear].Value,
                    new[] { "LeaveType", "Allowed", "Used", "PendingDays", "Remaining" }.Select(k => T[k].Value).ToArray(),
                    balances.Items.Select(b => new object?[] { ar ? b.LeaveTypeNameAr : b.LeaveTypeName, b.TotalAllowedDays, b.UsedDays, b.PendingDays, b.RemainingDays }).ToList()),
                new ReportTable(T["MyLeaveRequests"].Value,
                    new[] { "LeaveType", "From", "To", "Days", "Status", "Reason", "Review" }.Select(k => T[k].Value).ToArray(),
                    requests.Items.Select(r => new object?[] { ar ? r.LeaveTypeNameAr : r.LeaveTypeName, r.StartDate, r.EndDate, r.TotalDays, T["Status." + r.Status].Value, r.Reason, r.ReviewNotes }).ToList()));
        }
        ViewData["Pagination"] = DisplayPagination.Create(Request, requests.TotalCount, requests.Page);
        ViewData["BalancePagination"] = DisplayPagination.Create(Request, balances.TotalCount, balances.Page, "Balance");
        return View(new LeaveListViewModel
        {
            Year = reportYear, Status = status, Requests = requests.Items, Balances = balances.Items
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new LeaveRequestFormViewModel
        {
            StartDate = businessClock.Today
        };
        await PopulateLeaveTypesAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        LeaveRequestFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.StartDate is not null && model.EndDate is not null && model.EndDate < model.StartDate)
        {
            ModelState.AddModelError(nameof(model.EndDate), HRMS.Application.Common.Messages.Get("Business021"));
        }

        var employeeId = await GetEmployeeIdAsync(cancellationToken);
        if (!ModelState.IsValid || employeeId is null)
        {
            if (employeeId is null)
            {
                ModelState.AddModelError(string.Empty, T["EmployeeNotLinked"].Value);
            }
            await PopulateLeaveTypesAsync(model, cancellationToken);
            return View(model);
        }

        var result = await leaveService.SubmitLeaveRequestAsync(
            new CreateLeaveRequestCommand(
                employeeId.Value,
                model.LeaveTypeId,
                model.StartDate!.Value,
                model.EndDate!.Value,
                model.Reason),
            cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            await PopulateLeaveTypesAsync(model, cancellationToken);
            return View(model);
        }

        TempData["StatusMessage"] = T["LeaveSubmitted"].Value;
        return RedirectToAction(nameof(MyReport));
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.ManageLeaves)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(
        LeaveDecisionViewModel model,
        CancellationToken cancellationToken)
    {
        if (!await CanReviewRequestAsync(model.LeaveRequestId, cancellationToken))
        {
            return Forbid();
        }
        var result = await leaveService.ApproveLeaveRequestAsync(
            model.LeaveRequestId,
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            model.Notes,
            cancellationToken);
        SetResultMessage(result, T["LeaveApproved"].Value);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.ManageLeaves)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(
        LeaveDecisionViewModel model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Notes))
        {
            TempData["ErrorMessage"] = T["LeaveRejectReasonRequired"].Value;
            return RedirectToAction(nameof(Index));
        }
        if (!await CanReviewRequestAsync(model.LeaveRequestId, cancellationToken))
        {
            return Forbid();
        }
        var result = await leaveService.RejectLeaveRequestAsync(
            model.LeaveRequestId,
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            model.Notes,
            cancellationToken);
        SetResultMessage(result, T["LeaveRejected"].Value);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var employeeId = await GetEmployeeIdAsync(cancellationToken);
        var request = await leaveService.GetLeaveRequestByIdAsync(id, cancellationToken);
        if (employeeId is null || request?.EmployeeId != employeeId)
        {
            return Forbid();
        }

        var result = await leaveService.CancelLeaveRequestAsync(
            id,
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            cancellationToken);
        SetResultMessage(result, T["LeaveCancelled"].Value);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = IdentityRoles.ManageLeaveTypes)]
    public async Task<IActionResult> Types(CancellationToken cancellationToken)
    {
        var types = await leaveService.SearchTypesAsync(false, DisplayPagination.Request(Request), cancellationToken);
        ViewData["Pagination"] = DisplayPagination.Create(Request, types.TotalCount, types.Page);
        var model = new LeaveTypeFormViewModel
        {
            Types = types.Items
        };
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.ManageLeaveTypes)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveType(
        LeaveTypeFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var types = await leaveService.SearchTypesAsync(false, DisplayPagination.Request(Request), cancellationToken);
            model.Types = types.Items;
            ViewData["Pagination"] = DisplayPagination.Create(Request, types.TotalCount, types.Page);
            return View("Types", model);
        }

        var input = new LeaveTypeInput(
            model.Name,
            model.NameAr,
            model.DefaultDaysPerYear,
            model.IsPaid,
            model.IsActive);
        var result = model.LeaveTypeId is int id
            ? await leaveService.UpdateLeaveTypeAsync(id, input, cancellationToken)
            : await leaveService.CreateLeaveTypeAsync(input, cancellationToken);
        SetResultMessage(result, T["LeaveTypeSaved"].Value);
        return RedirectToAction(nameof(Types));
    }

    private async Task PopulateLeaveTypesAsync(
        LeaveRequestFormViewModel model,
        CancellationToken cancellationToken)
    {
        var types = await leaveService.GetLeaveTypesAsync(cancellationToken: cancellationToken);
        model.LeaveTypes = types
            .Select(type => new SelectListItem($"{type.NameAr} ({type.Name})", type.LeaveTypeId.ToString()))
            .ToList();
    }

    private async Task<int?> GetEmployeeIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null
            ? null
            : await userAccountService.GetLinkedEmployeeIdAsync(userId, cancellationToken);
    }

    private async Task<int?> GetActorDepartmentIdAsync(int? employeeId, CancellationToken cancellationToken)
    {
        if (employeeId is null)
        {
            return null;
        }
        var employee = await employeeService.GetByIdAsync(employeeId.Value, cancellationToken);
        if (employee is null)
        {
            return null;
        }
        var department = await departmentService.GetByIdAsync(employee.DepartmentId, cancellationToken);
        return department?.ManagerEmployeeId == employeeId.Value ? employee.DepartmentId : null;
    }

    private bool CanManageAllRequests() =>
        User.IsInRole(IdentityRoles.SystemAdministrator) ||
        User.IsInRole(IdentityRoles.HRManager) ||
        User.IsInRole(IdentityRoles.HROfficer);

    private async Task<bool> CanReviewRequestAsync(int id, CancellationToken cancellationToken)
    {
        var request = await leaveService.GetLeaveRequestByIdAsync(id, cancellationToken);
        if (request is null)
        {
            return false;
        }
        if (CanManageAllRequests())
        {
            return true;
        }
        if (User.IsInRole(IdentityRoles.DepartmentManager))
        {
            var employeeId = await GetEmployeeIdAsync(cancellationToken);
            var managerDepartmentId = await GetActorDepartmentIdAsync(employeeId, cancellationToken);
            return managerDepartmentId.HasValue && request.DepartmentId == managerDepartmentId;
        }
        return false;
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }

    private void SetResultMessage(LeaveOperationResult result, string successMessage)
    {
        if (result.Succeeded)
        {
            TempData["StatusMessage"] = successMessage;
        }
        else
        {
            TempData["ErrorMessage"] = string.Join(" ", result.Errors);
        }
    }
}
