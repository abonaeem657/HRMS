using HRMS.Application.Identity;
using HRMS.Application.Reporting;
using HRMS.Application.Employees;
using HRMS.Application.Organization;
using HRMS.Models.Reporting;
using HRMS.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Controllers;

[Authorize(Roles = IdentityRoles.ViewReports)]
public sealed class ReportsController(
    IReportingService reportingService,
    HRMS.Application.Common.IBusinessClock clock,
    IUserAccountService userAccountService,
    IEmployeeService employeeService,
    IDepartmentService departmentService, IStringLocalizer<SharedResource> T) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var model = new DashboardViewModel
        {
            FromDate = fromDate ?? new DateOnly(today.Year, 1, 1),
            ToDate = toDate ?? today
        };
        if (model.ToDate < model.FromDate)
        {
            TempData["ErrorMessage"] = T["ReportDateRangeInvalid"].Value;
            model.FromDate = new DateOnly(today.Year, 1, 1);
            model.ToDate = today;
        }
        var managerScope = User.IsInRole(IdentityRoles.DepartmentManager) &&
                           !User.IsInRole(IdentityRoles.HRManager) &&
                           !User.IsInRole(IdentityRoles.HROfficer) &&
                           !User.IsInRole(IdentityRoles.SystemAdministrator);
        var departmentId = managerScope
            ? await GetActorDepartmentIdAsync(cancellationToken)
            : null;
        if (managerScope && departmentId is null)
        {
            return Forbid();
        }
        model.Summary = await reportingService.GetDashboardAsync(
            model.FromDate,
            model.ToDate,
            departmentId,
            includeRecentActivity: IdentityRoles.ViewAuditLogs.Split(',').Any(User.IsInRole),
            cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Employees(
        EmployeeReportViewModel model,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole(IdentityRoles.DepartmentManager) && !CanViewAll())
        {
            model.DepartmentId = await GetActorDepartmentIdAsync(cancellationToken);
            if (model.DepartmentId is null)
            {
                return Forbid();
            }
        }
        model.Options = await reportingService.GetFilterOptionsAsync(
            CanViewAll() ? null : model.DepartmentId, cancellationToken) ?? new();
        model.Results = await reportingService.GetEmployeeDirectoryReportAsync(
            new EmployeeReportFilter(
                model.DepartmentId,
                model.PositionId,
                model.Status,
                model.HiredAfter,
                model.HiredBefore,
                model.SortBy,
                model.SortDescending,
                model.Page <= 0 ? 1 : model.Page,
                model.PageSize <= 0 ? 25 : model.PageSize),
            cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Attendance(
        AttendanceReportViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.FromDate > model.ToDate)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid date range",
                Detail = HRMS.Application.Common.Messages.Get("Business020")
            });
        }
        if (User.IsInRole(IdentityRoles.DepartmentManager) && !CanViewAll())
        {
            model.DepartmentId = await GetActorDepartmentIdAsync(cancellationToken);
            if (model.DepartmentId is null)
            {
                return Forbid();
            }
        }
        model.Options = await reportingService.GetFilterOptionsAsync(
            CanViewAll() ? null : model.DepartmentId, cancellationToken) ?? new();
        model.Results = await reportingService.GetAttendanceReportAsync(
            new AttendanceReportFilter(
                model.DepartmentId,
                model.EmployeeId,
                model.FromDate,
                model.ToDate,
                model.Page <= 0 ? 1 : model.Page,
                model.PageSize <= 0 ? 25 : model.PageSize),
            cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Leaves(
        LeaveReportViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.Year is < 2000 or > 2100)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid year",
                Detail = HRMS.Application.Common.Messages.Get("Business063")
            });
        }
        if (User.IsInRole(IdentityRoles.DepartmentManager) && !CanViewAll())
        {
            model.DepartmentId = await GetActorDepartmentIdAsync(cancellationToken);
            if (model.DepartmentId is null)
            {
                return Forbid();
            }
        }
        model.Options = await reportingService.GetFilterOptionsAsync(
            CanViewAll() ? null : model.DepartmentId, cancellationToken) ?? new();
        model.Results = await reportingService.GetLeaveReportAsync(
            new LeaveReportFilter(
                model.DepartmentId,
                model.EmployeeId,
                model.LeaveTypeId,
                model.Status,
                model.Year,
                model.Page <= 0 ? 1 : model.Page,
                model.PageSize <= 0 ? 25 : model.PageSize),
            cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> LeaveDecisions(
        DateOnly? fromDate,
        DateOnly? toDate,
        int? departmentId,
        CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var model = new LeaveDecisionReportViewModel
        {
            FromDate = fromDate ?? new DateOnly(today.Year, 1, 1),
            ToDate = toDate ?? today
        };
        var managerScope = User.IsInRole(IdentityRoles.DepartmentManager) && !CanViewAll();
        if (managerScope)
        {
            model.DepartmentId = await GetActorDepartmentIdAsync(cancellationToken);
            if (model.DepartmentId is null)
            {
                return Forbid();
            }
        }
        else
        {
            model.DepartmentId = departmentId;
            var departments = await departmentService.GetListAsync(new DepartmentListQuery(null, null), cancellationToken);
            model.Departments = departments.Select(department => new SelectListItem(
                department.Name,
                department.DepartmentId.ToString(),
                department.DepartmentId == model.DepartmentId)).ToList();
        }

        if (model.ToDate < model.FromDate || model.FromDate.Year < 2000 || model.ToDate.Year > 2100)
        {
            TempData["ErrorMessage"] = T["ReportYearInvalid"].Value;
            model.FromDate = new DateOnly(today.Year, 1, 1);
            model.ToDate = today;
        }
        model.Summary = await reportingService.GetLeaveDecisionSummaryAsync(
            model.FromDate,
            model.ToDate,
            model.DepartmentId,
            cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Headcount(CancellationToken cancellationToken)
    {
        var departmentId = User.IsInRole(IdentityRoles.DepartmentManager) && !CanViewAll()
            ? await GetActorDepartmentIdAsync(cancellationToken)
            : null;
        if (User.IsInRole(IdentityRoles.DepartmentManager) && !CanViewAll() && departmentId is null)
        {
            return Forbid();
        }
        return View(await reportingService.GetDepartmentHeadcountReportAsync(
            departmentId,
            cancellationToken));
    }

    private bool CanViewAll() =>
        User.IsInRole(IdentityRoles.SystemAdministrator) ||
        User.IsInRole(IdentityRoles.HRManager) ||
        User.IsInRole(IdentityRoles.HROfficer) ||
        User.IsInRole(IdentityRoles.Auditor);

    private async Task<int?> GetActorDepartmentIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            return null;
        }
        var employeeId = await userAccountService.GetLinkedEmployeeIdAsync(userId, cancellationToken);
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
}
