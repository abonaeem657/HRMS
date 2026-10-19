namespace HRMS.Application.LeaveManagement;

public sealed record LeaveTypeDto(
    int LeaveTypeId,
    string Name,
    string NameAr,
    int DefaultDaysPerYear,
    bool IsPaid,
    bool IsActive);

public sealed record LeaveRequestDto(
    int LeaveRequestId,
    int EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    int DepartmentId,
    string DepartmentName,
    int LeaveTypeId,
    string LeaveTypeName,
    string LeaveTypeNameAr,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalDays,
    string Reason,
    string Status,
    DateTime AppliedAtUtc,
    string? ReviewedByUserId,
    string? ReviewedByName,
    DateTime? ReviewedAtUtc,
    string? ReviewNotes,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null);

public sealed record CreateLeaveRequestCommand(
    int EmployeeId,
    int LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason);

public sealed record LeaveTypeInput(
    string Name,
    string NameAr,
    int DefaultDaysPerYear,
    bool IsPaid,
    bool IsActive);

public sealed record LeaveBalanceDto(
    int LeaveTypeId,
    string LeaveTypeName,
    string LeaveTypeNameAr,
    int TotalAllowedDays,
    int UsedDays,
    int RemainingDays,
    int PendingDays = 0);

public sealed record LeaveFilter(
    int? EmployeeId = null,
    int? DepartmentId = null,
    int? LeaveTypeId = null,
    string? Status = null,
    int? Year = null,
    int? Page = null,
    int PageSize = 25);

public sealed record LeaveOperationResult(
    bool Succeeded,
    int? LeaveRequestId,
    IReadOnlyList<string> Errors)
{
    public static LeaveOperationResult Success(int leaveRequestId) =>
        new(true, leaveRequestId, Array.Empty<string>());

    public static LeaveOperationResult Failure(params string[] errors) =>
        new(false, null, errors);
}
