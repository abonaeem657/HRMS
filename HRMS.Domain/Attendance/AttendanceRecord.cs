using HRMS.Domain.Employees;

namespace HRMS.Domain.Attendance;

public class AttendanceRecord
{
    public const string PresentStatus = "Present";
    public const string LateStatus = "Late";
    public const string AbsentStatus = "Absent";
    public const string HalfDayStatus = "HalfDay";
    private static readonly HashSet<string> ValidStatuses =
        [PresentStatus, LateStatus, AbsentStatus, HalfDayStatus];

    private AttendanceRecord()
    {
    }

    public AttendanceRecord(
        int employeeId,
        DateOnly workDate,
        DateTime? checkInUtc,
        DateTime? checkOutUtc,
        string status,
        string? notes = null)
    {
        if (employeeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(employeeId), "Employee ID must be greater than zero.");
        }

        if (workDate == default)
        {
            throw new ArgumentOutOfRangeException(nameof(workDate), "Work date is required.");
        }

        EmployeeId = employeeId;
        WorkDate = workDate;
        Notes = NormalizeOptional(notes);
        SetAttendanceTimes(checkInUtc, checkOutUtc, status);
    }

    public int AttendanceId { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public int EmployeeId { get; private set; }

    public Employee? Employee { get; private set; }

    public DateOnly WorkDate { get; private set; }

    public DateTime? CheckInUtc { get; private set; }

    public DateTime? CheckOutUtc { get; private set; }

    public decimal TotalHours { get; private set; }

    public string Status { get; private set; } = "Present";

    public string? Notes { get; private set; }

    public static AttendanceRecord CreateCheckIn(
        int employeeId,
        DateOnly workDate,
        DateTime checkInUtc,
        string? notes = null,
        bool isLate = false)
    {
        ValidateAttendanceTime(checkInUtc);

        if (employeeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(employeeId));
        }

        if (workDate == default)
        {
            throw new ArgumentOutOfRangeException(nameof(workDate));
        }

        var record = new AttendanceRecord
        {
            EmployeeId = employeeId,
            WorkDate = workDate,
            CheckInUtc = checkInUtc,
            Status = isLate ? LateStatus : PresentStatus,
            Notes = NormalizeOptional(notes),
            TotalHours = 0
        };

        return record;
    }

    public void RecordCheckOut(DateTime checkOutUtc, string? notes = null)
    {
        ValidateAttendanceTime(checkOutUtc);
        if (!CheckInUtc.HasValue)
        {
            throw new InvalidOperationException("Cannot record check-out without prior check-in.");
        }

        if (checkOutUtc < CheckInUtc.Value)
        {
            throw new ArgumentException("Check-out time cannot be earlier than check-in time.", nameof(checkOutUtc));
        }

        if (CheckOutUtc.HasValue) throw new InvalidOperationException("Check-out is already recorded.");
        if (checkOutUtc - CheckInUtc.Value > TimeSpan.FromHours(24))
            throw new ArgumentException("An attendance interval cannot exceed 24 hours.", nameof(checkOutUtc));
        CheckOutUtc = checkOutUtc;
        TotalHours = Math.Round((decimal)(checkOutUtc - CheckInUtc.Value).TotalHours, 2);

        if (!string.IsNullOrWhiteSpace(notes))
        {
            Notes = string.IsNullOrWhiteSpace(Notes) ? notes.Trim() : $"{Notes} | {notes.Trim()}";
        }
    }

    public void UpdateManual(
        DateTime? checkInUtc,
        DateTime? checkOutUtc,
        string status,
        string? notes)
    {
        SetAttendanceTimes(checkInUtc, checkOutUtc, status);
        Notes = NormalizeOptional(notes);
    }

    private void SetAttendanceTimes(DateTime? checkInUtc, DateTime? checkOutUtc, string status)
    {
        if (checkOutUtc.HasValue && !checkInUtc.HasValue)
            throw new ArgumentException("Check-out requires check-in.", nameof(checkOutUtc));
        if (checkInUtc.HasValue && checkOutUtc.HasValue && checkOutUtc.Value - checkInUtc.Value > TimeSpan.FromHours(24))
            throw new ArgumentException("An attendance interval cannot exceed 24 hours.", nameof(checkOutUtc));

        if (!ValidStatuses.Contains(status))
        {
            throw new ArgumentException("Attendance status is not supported.", nameof(status));
        }

        if (checkInUtc.HasValue)
        {
            ValidateAttendanceTime(checkInUtc.Value);
        }
        if (checkOutUtc.HasValue)
        {
            ValidateAttendanceTime(checkOutUtc.Value);
        }

        if (checkInUtc.HasValue && checkOutUtc.HasValue)
        {
            if (checkOutUtc.Value < checkInUtc.Value)
            {
                throw new ArgumentException("Check-out time cannot be earlier than check-in time.", nameof(checkOutUtc));
            }

            TotalHours = Math.Round((decimal)(checkOutUtc.Value - checkInUtc.Value).TotalHours, 2);
        }
        else
        {
            TotalHours = 0;
        }

        CheckInUtc = checkInUtc;
        CheckOutUtc = checkOutUtc;
        Status = string.IsNullOrWhiteSpace(status) ? "Present" : status.Trim();
    }

    private static void ValidateAttendanceTime(DateTime attendanceTimeUtc)
    {
        if (attendanceTimeUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Attendance timestamps must be UTC.", nameof(attendanceTimeUtc));
        }

        if (attendanceTimeUtc > DateTime.UtcNow)
        {
            throw new ArgumentException("Attendance timestamps cannot be in the future.", nameof(attendanceTimeUtc));
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
