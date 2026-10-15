using System.Diagnostics;
using System.Security.Claims;
using HRMS.Application.Attendance;
using HRMS.Application.Employees;
using HRMS.Application.Identity;
using HRMS.Application.LeaveManagement;
using HRMS.Application.Organization;
using HRMS.Application.Reporting;
using HRMS.Models;
using HRMS.Models.Home;
using HRMS.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Controllers;

[Authorize]
public sealed class HomeController(
    IReportingService reportingService,
    HRMS.Application.Common.IBusinessClock clock,
    IEmployeeService employeeService,
    IDepartmentService departmentService,
    IPositionService positionService,
    IUserAccountService userAccountService,
    ILeaveService leaveService,
    IAttendanceService attendanceService, IStringLocalizer<SharedResource> localizer) : Controller
{
    [HttpGet]
    public IActionResult Guide() => View();

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var from = fromDate ?? new DateOnly(today.Year, 1, 1);
        var to = toDate ?? today;
        if (from > to || to > today)
        {
            ViewData["DateError"] = localizer["DateRangeInvalid"].Value;
            from = new DateOnly(today.Year, 1, 1);
            to = today;
        }

        var isAdmin = User.IsInRole(IdentityRoles.SystemAdministrator);
        var isHrManager = User.IsInRole(IdentityRoles.HRManager);
        var isHrOfficer = User.IsInRole(IdentityRoles.HROfficer);
        var isDepartmentManager = User.IsInRole(IdentityRoles.DepartmentManager);
        var isEmployee = User.IsInRole(IdentityRoles.Employee);
        var isAuditor = User.IsInRole(IdentityRoles.Auditor);
        var canViewHrData = isAdmin || isHrManager || isHrOfficer;
        var canUseAttendance = User.IsInRoles(IdentityRoles.UseAttendance);
        var canUseLeaves = User.IsInRoles(IdentityRoles.UseLeaves);
        var canManageAttendance = User.IsInRoles(IdentityRoles.ManageAttendance);
        var canManageLeaves = User.IsInRoles(IdentityRoles.ManageLeaves);
        var canManageOrganization = User.IsInRoles(IdentityRoles.ManageOrganization);
        var canManageEmployees = User.IsInRoles(IdentityRoles.ManageEmployees);
        var canViewReports = User.IsInRoles(IdentityRoles.ViewReports);
        var canViewAudit = User.IsInRoles(IdentityRoles.ViewAuditLogs);

        int? employeeId = null;
        int? departmentId = null;
        string? departmentName = null;
        EmployeeDetails? linkedEmployee = null;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            employeeId = await userAccountService.GetLinkedEmployeeIdAsync(userId, cancellationToken);
            if (employeeId.HasValue)
                linkedEmployee = await employeeService.GetByIdAsync(employeeId.Value, cancellationToken);
        }

        var managerScopeValid = false;
        if (isDepartmentManager && linkedEmployee is not null)
        {
            var department = await departmentService.GetByIdAsync(linkedEmployee.DepartmentId, cancellationToken);
            managerScopeValid = department?.ManagerEmployeeId == linkedEmployee.EmployeeId;
            if (managerScopeValid)
            {
                departmentId = linkedEmployee.DepartmentId;
                departmentName = department!.Name;
            }
        }

        var roleKey = IdentityRoles.All.FirstOrDefault(role => User.IsInRole(role.Name))?.DisplayName ?? "Role.Employee";
        var roleName = localizer[roleKey].Value;
        var model = new HomeDashboardViewModel
        {
            DisplayName = linkedEmployee is null
                ? (User.Identity?.Name?.Split('@')[0] ?? "مستخدم النظام")
                : $"{linkedEmployee.FirstName} {linkedEmployee.LastName}",
            Email = User.Identity?.Name ?? string.Empty,
            RoleName = roleName,
            CanViewHrData = canViewHrData,
            CanManageEmployees = canManageEmployees,
            CanManageOrganization = canManageOrganization,
            CanManageUsers = isAdmin,
            CanManageAttendance = canManageAttendance,
            CanUseAttendance = canUseAttendance,
            CanUseLeaves = canUseLeaves,
            CanApproveLeaves = canManageLeaves,
            CanManageLeaveTypes = User.IsInRoles(IdentityRoles.ManageLeaveTypes),
            CanViewReports = canViewReports,
            CanViewAudit = canViewAudit,
            IsDepartmentManager = isDepartmentManager,
            HasLinkedEmployee = linkedEmployee is not null,
            EmployeeId = linkedEmployee?.EmployeeId,
            DepartmentId = departmentId,
            DepartmentName = departmentName,
            Today = today,
            FromDate = from,
            ToDate = to
        };

        if (canViewHrData || isAuditor || (isDepartmentManager && managerScopeValid))
        {
            model = model.WithSummary(await reportingService.GetDashboardAsync(
                from, to, isDepartmentManager ? departmentId : null,
                includeRecentActivity: canViewAudit,
                cancellationToken));
        }

        if (canViewHrData)
        {
            var employeePage = await employeeService.SearchAsync(
                new EmployeeListQuery(null, null, null, "EmployeeNumber", false, 1, 1), cancellationToken);
            var departments = await departmentService.GetListAsync(new DepartmentListQuery(null, null), cancellationToken);
            var positions = await positionService.GetListAsync(new PositionListQuery(null, null, null), cancellationToken);
            model = model.WithCounts(employeePage.TotalCount, departments.Count, positions.Count,
                isAdmin ? (await userAccountService.GetAccountsAsync(cancellationToken)).Count : 0);
        }

        if (linkedEmployee is not null && (isEmployee || isDepartmentManager))
        {
            model = model.WithSelfService(
                await attendanceService.GetTodayRecordAsync(linkedEmployee.EmployeeId, cancellationToken),
                await attendanceService.GetSummaryAsync(new AttendanceFilter(
                    EmployeeId: linkedEmployee.EmployeeId,
                    FromDate: new DateOnly(today.Year, today.Month, 1),
                    ToDate: today), cancellationToken),
                await leaveService.GetLeaveRequestsAsync(new LeaveFilter(
                    EmployeeId: linkedEmployee.EmployeeId, Year: today.Year), cancellationToken));
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult SetLanguage(string culture, string? returnUrl = null)
    {
        if (culture is not ("ar-JO" or "en-US")) culture = "ar-JO";
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax, HttpOnly = true });
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!);
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}

file static class HomeDashboardViewModelExtensions
{
    public static HomeDashboardViewModel WithSummary(this HomeDashboardViewModel model, DashboardSummary summary) =>
        new()
        {
            DisplayName = model.DisplayName, Email = model.Email, RoleName = model.RoleName,
            CanViewHrData = model.CanViewHrData, CanManageEmployees = model.CanManageEmployees,
            CanManageOrganization = model.CanManageOrganization, CanManageUsers = model.CanManageUsers,
            CanManageAttendance = model.CanManageAttendance, CanUseAttendance = model.CanUseAttendance,
            CanUseLeaves = model.CanUseLeaves, CanApproveLeaves = model.CanApproveLeaves,
            CanManageLeaveTypes = model.CanManageLeaveTypes, CanViewReports = model.CanViewReports,
            CanViewAudit = model.CanViewAudit, IsDepartmentManager = model.IsDepartmentManager,
            HasLinkedEmployee = model.HasLinkedEmployee, EmployeeId = model.EmployeeId,
            DepartmentId = model.DepartmentId, DepartmentName = model.DepartmentName,
            Today = model.Today, FromDate = model.FromDate, ToDate = model.ToDate,
            DepartmentCount = model.DepartmentCount, PositionCount = model.PositionCount,
            EmployeeCount = model.EmployeeCount, UserCount = model.UserCount, Summary = summary,
            TodayAttendance = model.TodayAttendance, PersonalAttendance = model.PersonalAttendance,
            MyLeaveRequests = model.MyLeaveRequests
        };

    public static HomeDashboardViewModel WithCounts(this HomeDashboardViewModel model, int employees, int departments, int positions, int users) =>
        new()
        {
            DisplayName = model.DisplayName, Email = model.Email, RoleName = model.RoleName,
            CanViewHrData = model.CanViewHrData, CanManageEmployees = model.CanManageEmployees,
            CanManageOrganization = model.CanManageOrganization, CanManageUsers = model.CanManageUsers,
            CanManageAttendance = model.CanManageAttendance, CanUseAttendance = model.CanUseAttendance,
            CanUseLeaves = model.CanUseLeaves, CanApproveLeaves = model.CanApproveLeaves,
            CanManageLeaveTypes = model.CanManageLeaveTypes, CanViewReports = model.CanViewReports,
            CanViewAudit = model.CanViewAudit, IsDepartmentManager = model.IsDepartmentManager,
            HasLinkedEmployee = model.HasLinkedEmployee, EmployeeId = model.EmployeeId,
            DepartmentId = model.DepartmentId, DepartmentName = model.DepartmentName,
            Today = model.Today, FromDate = model.FromDate, ToDate = model.ToDate,
            DepartmentCount = departments, PositionCount = positions, EmployeeCount = employees, UserCount = users,
            Summary = model.Summary, TodayAttendance = model.TodayAttendance,
            PersonalAttendance = model.PersonalAttendance, MyLeaveRequests = model.MyLeaveRequests
        };

    public static HomeDashboardViewModel WithSelfService(this HomeDashboardViewModel model,
        AttendanceRecordDto? todayAttendance, AttendanceSummaryDto attendance, IReadOnlyList<LeaveRequestDto> leaveRequests) =>
        new()
        {
            DisplayName = model.DisplayName, Email = model.Email, RoleName = model.RoleName,
            CanViewHrData = model.CanViewHrData, CanManageEmployees = model.CanManageEmployees,
            CanManageOrganization = model.CanManageOrganization, CanManageUsers = model.CanManageUsers,
            CanManageAttendance = model.CanManageAttendance, CanUseAttendance = model.CanUseAttendance,
            CanUseLeaves = model.CanUseLeaves, CanApproveLeaves = model.CanApproveLeaves,
            CanManageLeaveTypes = model.CanManageLeaveTypes, CanViewReports = model.CanViewReports,
            CanViewAudit = model.CanViewAudit, IsDepartmentManager = model.IsDepartmentManager,
            HasLinkedEmployee = model.HasLinkedEmployee, EmployeeId = model.EmployeeId,
            DepartmentId = model.DepartmentId, DepartmentName = model.DepartmentName,
            Today = model.Today, FromDate = model.FromDate, ToDate = model.ToDate,
            DepartmentCount = model.DepartmentCount, PositionCount = model.PositionCount,
            EmployeeCount = model.EmployeeCount, UserCount = model.UserCount, Summary = model.Summary,
            TodayAttendance = todayAttendance, PersonalAttendance = attendance, MyLeaveRequests = leaveRequests
        };
}
