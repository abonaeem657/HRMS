namespace HRMS.Application.Reporting;

public sealed record EmployeeReportFilter(
    int? DepartmentId = null,
    int? PositionId = null,
    string? Status = null,
    DateOnly? HiredAfter = null,
    DateOnly? HiredBefore = null,
    string? SortBy = null,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 25, bool ShowAll = false);

public sealed record EmployeeReportItem(
    string EmployeeNumber,
    string FullName,
    string WorkEmail,
    string? PhoneNumber,
    string DepartmentName,
    string PositionTitle,
    DateOnly HireDate,
    string EmploymentStatus,
    string EmploymentType,
    string? WorkLocation);

public sealed record AttendanceReportFilter(
    int? DepartmentId = null,
    int? EmployeeId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Page = 1,
    int PageSize = 25, bool ShowAll = false);

public sealed record AttendanceReportItem(
    string EmployeeNumber,
    string FullName,
    string DepartmentName,
    DateOnly WorkDate,
    DateTime? CheckInUtc,
    DateTime? CheckOutUtc,
    decimal TotalHours,
    string Status,
    string? Notes,
    bool IsInvalid = false);

public sealed record LeaveReportFilter(
    int? DepartmentId = null,
    int? EmployeeId = null,
    int? LeaveTypeId = null,
    string? Status = null,
    int? Year = null,
    int Page = 1,
    int PageSize = 25, bool ShowAll = false);

public sealed record LeaveReportItem(
    string EmployeeNumber,
    string FullName,
    string DepartmentName,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalDays,
    string Status,
    DateTime AppliedAtUtc,
    string? ReviewNotes);

public sealed record DepartmentHeadcountItem(
    int DepartmentId,
    string DepartmentName,
    string? ManagerName,
    int TotalEmployees,
    int ActiveEmployees,
    int InactiveEmployees,
    int PositionsCount);

public sealed record LeaveDecisionSummary(int ApprovedCount, int RejectedCount, DateOnly FromDate, DateOnly ToDate);

public sealed record DashboardSummary(
    int TotalActiveEmployees,
    IReadOnlyList<DepartmentHeadcountItem> EmployeesByDepartment,
    int NewHires,
    IReadOnlyDictionary<string, int> EmployeesByStatus,
    int PendingLeaveRequests,
    HRMS.Application.Attendance.AttendanceSummaryDto Attendance,
    IReadOnlyList<RecentHrActivity> RecentActivity);

public sealed record RecentHrActivity(
    long AuditLogId,
    string? ActorUserName,
    string Action,
    string EntityName,
    string? EntityId,
    DateTime OccurredAtUtc);

public sealed record ReportPage<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
