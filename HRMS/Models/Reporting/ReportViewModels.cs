using HRMS.Application.Reporting;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Models.Reporting;

public sealed class DashboardViewModel
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public DashboardSummary Summary { get; set; } =
        new(0, Array.Empty<DepartmentHeadcountItem>(), 0, new Dictionary<string, int>(), 0,
            new HRMS.Application.Attendance.AttendanceSummaryDto(0, 0, 0, 0, 0, 0),
            Array.Empty<RecentHrActivity>());
}

public sealed class EmployeeReportViewModel
{
    public ReportFilterOptions Options { get; set; } = new();
    public int? DepartmentId { get; set; }
    public int? PositionId { get; set; }
    public string? Status { get; set; }
    public DateOnly? HiredAfter { get; set; }
    public DateOnly? HiredBefore { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; } = 6;
    public ReportPage<EmployeeReportItem> Results { get; set; } =
        new(Array.Empty<EmployeeReportItem>(), 0, 1, 25);
}

public sealed class AttendanceReportViewModel
{
    public ReportFilterOptions Options { get; set; } = new();
    public int? DepartmentId { get; set; }
    public int? EmployeeId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; } = 6;
    public ReportPage<AttendanceReportItem> Results { get; set; } =
        new(Array.Empty<AttendanceReportItem>(), 0, 1, 25);
}

public sealed class LeaveReportViewModel
{
    public ReportFilterOptions Options { get; set; } = new();
    public int? DepartmentId { get; set; }
    public int? EmployeeId { get; set; }
    public int? LeaveTypeId { get; set; }
    public string? Status { get; set; }
    public int? Year { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; } = 6;
    public ReportPage<LeaveReportItem> Results { get; set; } =
        new(Array.Empty<LeaveReportItem>(), 0, 1, 25);
}

public sealed class LeaveDecisionReportViewModel
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int? DepartmentId { get; set; }
    public IReadOnlyList<SelectListItem> Departments { get; set; } = Array.Empty<SelectListItem>();
    public LeaveDecisionSummary Summary { get; set; } = new(0, 0, default, default);
}
