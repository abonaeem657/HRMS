using HRMS.Application.Reporting;
using HRMS.Models.Reporting;
using HRMS.Resources;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Controllers;

public sealed partial class ReportsController
{
    private string[] Headers(params string[] keys) => keys.Select(k => T[k].Value).ToArray();
    private Task<IActionResult> Download(string titleKey, string fileName, CancellationToken ct, params ReportTable[] tables) =>
        this.DownloadAsync(exporter, T, T[titleKey].Value, fileName, clock.ToLocal(DateTime.UtcNow)!.Value, ct, tables);

    private Task<IActionResult> ExportEmployeesAsync(IReadOnlyList<EmployeeReportItem> rows, CancellationToken ct) =>
        Download("EmployeeDirectory", "employees", ct, new ReportTable(T["EmployeeDirectory"].Value,
            Headers("EmployeeNumber", "Name", "Email", "Department", "JobTitle", "HireDate", "Status"),
            rows.Select(r => new object?[] { r.EmployeeNumber, r.FullName, r.WorkEmail, r.DepartmentName, r.PositionTitle, r.HireDate, T.LocalizeValue("Employment.", r.EmploymentStatus) }).ToList()));

    private Task<IActionResult> ExportAttendanceAsync(IReadOnlyList<AttendanceReportItem> rows, CancellationToken ct) =>
        Download("AttendanceReport", "attendance", ct, new ReportTable(T["AttendanceReport"].Value,
            Headers("Date", "EmployeeNumber", "Employee", "Department", "CheckInUtc", "CheckOutUtc", "Hours", "Status", "Notes"),
            rows.Select(r => new object?[] { r.WorkDate, r.EmployeeNumber, r.FullName, r.DepartmentName, clock.ToLocal(r.CheckInUtc), clock.ToLocal(r.CheckOutUtc), r.TotalHours, T["Status." + r.Status].Value, r.Notes }).ToList()));

    private Task<IActionResult> ExportLeavesAsync(IReadOnlyList<LeaveReportItem> rows, CancellationToken ct) =>
        Download("LeaveReport", "leaves", ct, new ReportTable(T["LeaveReport"].Value,
            Headers("EmployeeNumber", "Employee", "Department", "LeaveType", "From", "To", "Days", "Status", "Review"),
            rows.Select(r => new object?[] { r.EmployeeNumber, r.FullName, r.DepartmentName, T.LocalizeValue("LeaveKind.", r.LeaveTypeName), r.StartDate, r.EndDate, r.TotalDays, T["Status." + r.Status].Value, r.ReviewNotes }).ToList()));

    private ReportTable HeadcountTable(IReadOnlyList<DepartmentHeadcountItem> rows) => new(T["HeadcountByDepartment"].Value,
        Headers("Department", "Manager", "Employees", "Active", "Inactive", "JobTitles"),
        rows.Select(r => new object?[] { r.DepartmentName, r.ManagerName, r.TotalEmployees, r.ActiveEmployees, r.InactiveEmployees, r.PositionsCount }).ToList());

    private Task<IActionResult> ExportHeadcountAsync(IReadOnlyList<DepartmentHeadcountItem> rows, CancellationToken ct) =>
        Download("HeadcountByDepartment", "headcount", ct, HeadcountTable(rows));

    private Task<IActionResult> ExportDecisionsAsync(LeaveDecisionSummary summary, CancellationToken ct) =>
        Download("DecisionTitle", "leave-decisions", ct, new ReportTable(T["DecisionTitle"].Value,
            Headers("From", "To", "ApprovedRequests", "RejectedRequests"),
            [new object?[] { summary.FromDate, summary.ToDate, summary.ApprovedCount, summary.RejectedCount }]));

    private Task<IActionResult> ExportDashboardAsync(DashboardViewModel model, CancellationToken ct)
    {
        var s = model.Summary;
        var tables = new List<ReportTable>
        {
            new(T["Dashboard"].Value, Headers("From", "To", "Active", "NewHiresInPeriod", "PendingLeaveRequests", "Hours"),
                [new object?[] { model.FromDate, model.ToDate, s.TotalActiveEmployees, s.NewHires, s.PendingLeaveRequests, s.Attendance.TotalHoursWorked }]),
            HeadcountTable(s.EmployeesByDepartment),
            new(T["Status"].Value, Headers("Status", "Employees"), s.EmployeesByStatus.Select(r => new object?[] { T.LocalizeValue("Employment.", r.Key), r.Value }).ToList()),
            new(T["AttendanceReport"].Value, Headers("Status", "Results"), [
                new object?[] { T["Status.Present"].Value, s.Attendance.PresentCount },
                new object?[] { T["Status.Late"].Value, s.Attendance.LateCount },
                new object?[] { T["Status.Absent"].Value, s.Attendance.AbsentCount },
                new object?[] { T["Status.HalfDay"].Value, s.Attendance.HalfDayCount }])
        };
        if (s.RecentActivity.Count > 0) tables.Add(new(T["RecentActivity"].Value,
            Headers("Date", "User", "Action", "Entity"), s.RecentActivity.Select(r => new object?[] { clock.ToLocal(r.OccurredAtUtc), r.ActorUserName, r.Action, r.EntityName }).ToList()));
        return Download("Dashboard", "dashboard", ct, tables.ToArray());
    }
}
