using HRMS.Models;
using System.Globalization;
using System.Security.Claims;
using HRMS.Application.Attendance;
using HRMS.Application.Identity;
using HRMS.Application.Employees;
using HRMS.Application.Organization;
using HRMS.Models.Attendance;
using HRMS.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Controllers;

[Authorize(Roles = IdentityRoles.UseAttendance)]
public sealed class AttendanceController(
    IAttendanceService attendanceService,
    IEmployeeService employeeService,
    IDepartmentService departmentService,
    IUserAccountService userAccountService,
    IConfiguration configuration, IStringLocalizer<SharedResource> T) : Controller
{
    private readonly TimeZoneInfo _businessTimeZone = ResolveTimeZone(
        configuration["Attendance:BusinessTimeZoneId"] ?? "Jordan Standard Time");

    [HttpGet]
    public async Task<IActionResult> Index(
        AttendanceListViewModel model,
        CancellationToken cancellationToken)
    {
        var actorEmployeeId = await GetActorEmployeeIdAsync(cancellationToken);
        ViewBag.CanRecordOwnAttendance = actorEmployeeId.HasValue;
        if (!CanManageAttendance())
        {
            if (actorEmployeeId is null)
            {
                return Forbid();
            }
            model.EmployeeId = actorEmployeeId;
        }

        if (model.FromDate > model.ToDate)
        {
            ModelState.AddModelError(nameof(model.FromDate), T["AttendanceDateOrderInvalid"].Value);
            model.FromDate = null;
            model.ToDate = null;
        }

        var page = await attendanceService.GetAttendanceListAsync(
            new AttendanceFilter(
                model.EmployeeId,
                model.DepartmentId,
                model.FromDate,
                model.ToDate,
                model.Status,
                model.Page,
                DisplayPagination.PageSize, DisplayPagination.IsShowAll(Request)),
            cancellationToken);
        model.Records = page.Items;
        ViewBag.BusinessDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _businessTimeZone));
        model.Page = page.Page;
        model.PageSize = page.PageSize;
        model.TotalCount = page.TotalCount;
        ViewData["Pagination"] = DisplayPagination.Create(Request, page.TotalCount, page.Page);
        model.Summary = await attendanceService.GetSummaryAsync(
            new AttendanceFilter(model.EmployeeId, model.DepartmentId, model.FromDate, model.ToDate, model.Status),
            cancellationToken);

        if (CanManageAttendance())
        {
            var employees = await employeeService.SearchAsync(
                new EmployeeListQuery(null, model.DepartmentId, null, "EmployeeNumber", false, 1, 100),
                cancellationToken);
            model.Employees = employees.Items.Select(employee =>
                new SelectListItem($"{employee.EmployeeNumber} - {employee.FullName}", employee.EmployeeId.ToString()))
                .ToList();
            var departments = await departmentService.GetListAsync(new DepartmentListQuery(null, true), cancellationToken);
            model.Departments = departments.Select(department =>
                new SelectListItem(department.Name, department.DepartmentId.ToString()))
                .ToList();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(CancellationToken cancellationToken)
    {
        var employeeId = await GetActorEmployeeIdAsync(cancellationToken);
        if (employeeId is null)
        {
            return Forbid();
        }

        var result = await attendanceService.CheckInAsync(employeeId.Value, cancellationToken: cancellationToken);
        SetOperationMessage(result, T["AttendanceCheckInSaved"].Value);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOut(CancellationToken cancellationToken)
    {
        var employeeId = await GetActorEmployeeIdAsync(cancellationToken);
        if (employeeId is null)
        {
            return Forbid();
        }

        var result = await attendanceService.CheckOutAsync(employeeId.Value, cancellationToken: cancellationToken);
        SetOperationMessage(result, T["AttendanceCheckOutSaved"].Value);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = IdentityRoles.ManageAttendance)]
    public async Task<IActionResult> Correct(CancellationToken cancellationToken, int? id = null)
    {
        var model = new AttendanceCorrectionViewModel
        {
            WorkDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _businessTimeZone))
        };
        if (id.HasValue)
        {
            var record = await attendanceService.GetByIdAsync(id.Value, cancellationToken);
            if (record is null) return NotFound();
            DateTime? Local(DateTime? utc) => utc.HasValue
                ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), _businessTimeZone) : null;
            var start = Local(record.CheckInUtc);
            var end = Local(record.CheckOutUtc);
            model.AttendanceId = record.AttendanceId;
            model.RowVersion = record.RowVersion;
            model.EmployeeId = record.EmployeeId;
            model.WorkDate = record.WorkDate;
            model.CheckInLocal = start?.ToString("HH:mm", CultureInfo.InvariantCulture);
            model.CheckOutLocal = end?.ToString("HH:mm", CultureInfo.InvariantCulture);
            model.CheckOutNextDay = end.HasValue && DateOnly.FromDateTime(end.Value) > record.WorkDate;
            model.CheckOutDate = end.HasValue ? DateOnly.FromDateTime(end.Value) : record.WorkDate;
            model.Status = record.Status;
            model.Notes = record.Notes;
        }
        await PopulateEmployeesAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.ManageAttendance)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Correct(
        AttendanceCorrectionViewModel model,
        CancellationToken cancellationToken)
    {
        var checkInUtc = ParseLocalDateTime(model.WorkDate, model.CheckInLocal);
        var endDate = model.CheckOutDate ?? (model.CheckOutNextDay ? model.WorkDate?.AddDays(1) : model.WorkDate);
        var checkOutUtc = ParseLocalDateTime(endDate, model.CheckOutLocal);
        if (!string.IsNullOrWhiteSpace(model.CheckInLocal) && checkInUtc is null)
        {
            ModelState.AddModelError(nameof(model.CheckInLocal), T["InvalidLocalCheckIn"].Value);
        }
        if (!string.IsNullOrWhiteSpace(model.CheckOutLocal) && checkOutUtc is null)
        {
            ModelState.AddModelError(nameof(model.CheckOutLocal), T["InvalidLocalCheckOut"].Value);
        }
        if (checkOutUtc.HasValue && !checkInUtc.HasValue)
            ModelState.AddModelError(nameof(model.CheckInLocal), T["CorrectionNeedsCheckIn"].Value);
        if (checkOutUtc < checkInUtc || (checkOutUtc.HasValue && checkInUtc.HasValue && checkOutUtc - checkInUtc > TimeSpan.FromHours(24)))
            ModelState.AddModelError(nameof(model.CheckOutLocal), T["CorrectionTimeOrder"].Value);
        if (checkInUtc > DateTime.UtcNow || checkOutUtc > DateTime.UtcNow)
            ModelState.AddModelError(string.Empty, T["CorrectionFutureTime"].Value);

        if (!ModelState.IsValid || model.WorkDate is null)
        {
            await PopulateEmployeesAsync(model, cancellationToken);
            return View(model);
        }

        var result = await attendanceService.RecordManualEntryAsync(
            model.EmployeeId,
            model.WorkDate.Value,
            checkInUtc,
            checkOutUtc,
            model.Status,
            model.Notes,
            cancellationToken, model.AttendanceId, model.RowVersion);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
            await PopulateEmployeesAsync(model, cancellationToken);
            return View(model);
        }

        TempData["StatusMessage"] = T["AttendanceCorrectionSaved"].Value;
        return RedirectToAction(nameof(Index));
    }

    private async Task<int?> GetActorEmployeeIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null
            ? null
            : await userAccountService.GetLinkedEmployeeIdAsync(userId, cancellationToken);
    }

    private bool CanManageAttendance() =>
        User.IsInRole(IdentityRoles.SystemAdministrator) ||
        User.IsInRole(IdentityRoles.HRManager) ||
        User.IsInRole(IdentityRoles.HROfficer);

    private async Task PopulateEmployeesAsync(
        AttendanceCorrectionViewModel model,
        CancellationToken cancellationToken)
    {
        var employees = await employeeService.SearchAsync(
            new EmployeeListQuery(null, null, EmployeeListQuery.AllStatuses, "EmployeeNumber", false, 1, 100),
            cancellationToken);
        model.Employees = employees.Items
            .Select(employee => new SelectListItem(
                $"{employee.EmployeeNumber} - {employee.FullName}",
                employee.EmployeeId.ToString(),
                employee.EmployeeId == model.EmployeeId))
            .ToList();
    }

    private DateTime? ParseLocalDateTime(DateOnly? workDate, string? localTime)
    {
        if (workDate is null || string.IsNullOrWhiteSpace(localTime) ||
            !TimeOnly.TryParseExact(localTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return null;
        }

        var localDateTime = DateTime.SpecifyKind(workDate.Value.ToDateTime(time), DateTimeKind.Unspecified);
        if (_businessTimeZone.IsInvalidTime(localDateTime))
        {
            return null;
        }

        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, _businessTimeZone);
    }

    private void SetOperationMessage(AttendanceOperationResult result, string successMessage)
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

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new InvalidOperationException(
                $"The configured attendance time zone '{timeZoneId}' is not available.",
                exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new InvalidOperationException(
                $"The configured attendance time zone '{timeZoneId}' is invalid.",
                exception);
        }
    }
}
