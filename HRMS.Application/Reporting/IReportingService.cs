namespace HRMS.Application.Reporting;

public interface IReportingService
{
    Task<ReportFilterOptions> GetFilterOptionsAsync(int? departmentScope = null,
        CancellationToken cancellationToken = default);

    Task<DashboardSummary> GetDashboardAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int? departmentId = null,
        bool includeRecentActivity = true,
        CancellationToken cancellationToken = default, DashboardPaging? paging = null);

    Task<ReportPage<EmployeeReportItem>> GetEmployeeDirectoryReportAsync(
        EmployeeReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<ReportPage<AttendanceReportItem>> GetAttendanceReportAsync(
        AttendanceReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<ReportPage<LeaveReportItem>> GetLeaveReportAsync(
        LeaveReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DepartmentHeadcountItem>> GetDepartmentHeadcountReportAsync(
        int? departmentId = null,
        CancellationToken cancellationToken = default);

    Task<ReportPage<DepartmentHeadcountItem>> GetDepartmentHeadcountPageAsync(int? departmentId, HRMS.Application.Common.PageRequest page, CancellationToken cancellationToken = default, string? sortBy = null, bool sortDescending = false);

    Task<LeaveDecisionSummary> GetLeaveDecisionSummaryAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int? departmentId = null,
        CancellationToken cancellationToken = default);

    byte[] GenerateCsv<T>(IEnumerable<T> data, string[] headers, Func<T, string[]> rowMapper);
}
