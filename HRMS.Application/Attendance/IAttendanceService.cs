namespace HRMS.Application.Attendance;

public interface IAttendanceService
{
    Task<AttendanceRecordDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<AttendancePage> GetAttendanceListAsync(
        AttendanceFilter filter,
        CancellationToken cancellationToken = default);

    Task<AttendanceRecordDto?> GetTodayRecordAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<AttendanceOperationResult> CheckInAsync(
        int employeeId,
        DateTime? timeUtc = null,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<AttendanceOperationResult> CheckOutAsync(
        int employeeId,
        DateTime? timeUtc = null,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<AttendanceOperationResult> RecordManualEntryAsync(
        int employeeId,
        DateOnly workDate,
        DateTime? checkInUtc,
        DateTime? checkOutUtc,
        string status,
        string? notes,
        CancellationToken cancellationToken = default,
        int? attendanceId = null,
        byte[]? rowVersion = null);

    Task<AttendanceSummaryDto> GetSummaryAsync(
        AttendanceFilter filter,
        CancellationToken cancellationToken = default);
}
