namespace HRMS.Application.Attendance;

public sealed record AttendanceRecordDto(
    int AttendanceId,
    int EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    int DepartmentId,
    string DepartmentName,
    DateOnly WorkDate,
    DateTime? CheckInUtc,
    DateTime? CheckOutUtc,
    decimal TotalHours,
    string Status,
    string? Notes,
    bool IsInvalid = false,
    byte[]? RowVersion = null);

public sealed record AttendanceFilter(
    int? EmployeeId = null,
    int? DepartmentId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 25, bool ShowAll = false);

public sealed record AttendancePage(
    IReadOnlyList<AttendanceRecordDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AttendanceSummaryDto(
    int TotalRecords,
    int PresentCount,
    int LateCount,
    int AbsentCount,
    int HalfDayCount,
    decimal TotalHoursWorked);

public sealed record AttendanceOperationResult(
    bool Succeeded,
    int? AttendanceId,
    IReadOnlyList<string> Errors)
{
    public static AttendanceOperationResult Success(int attendanceId) =>
        new(true, attendanceId, Array.Empty<string>());

    public static AttendanceOperationResult Failure(params string[] errors) =>
        new(false, null, errors);
}
