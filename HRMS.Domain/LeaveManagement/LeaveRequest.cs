using HRMS.Domain.Employees;

namespace HRMS.Domain.LeaveManagement;

public class LeaveRequest
{
    private LeaveRequest()
    {
    }

    public LeaveRequest(
        int employeeId,
        int leaveTypeId,
        DateOnly startDate,
        DateOnly endDate,
        string reason,
        TimeOnly? startTime = null,
        TimeOnly? endTime = null)
    {
        if (employeeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(employeeId), "Employee ID must be greater than zero.");
        }

        if (leaveTypeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(leaveTypeId), "Leave type ID must be greater than zero.");
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("Leave end date cannot be earlier than start date.", nameof(endDate));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (startDate == endDate && startTime.HasValue && endTime.HasValue && endTime <= startTime)
            throw new ArgumentException("The end time must be after the start time.");
        if (startDate == default || endDate == default)
        {
            throw new ArgumentException("Leave dates are required.");
        }

        EmployeeId = employeeId;
        LeaveTypeId = leaveTypeId;
        StartDate = startDate;
        EndDate = endDate;
        StartTime = startTime;
        EndTime = endTime;
        TotalDays = (endDate.DayNumber - startDate.DayNumber) + 1;
        Reason = reason.Trim();
        Status = "Pending";
        AppliedAtUtc = DateTime.UtcNow;
    }

    public int LeaveRequestId { get; private set; }

    public int EmployeeId { get; private set; }

    public Employee? Employee { get; private set; }

    public int LeaveTypeId { get; private set; }

    public LeaveType? LeaveType { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public TimeOnly? StartTime { get; private set; }

    public TimeOnly? EndTime { get; private set; }

    public int TotalDays { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string Status { get; private set; } = "Pending";

    public DateTime AppliedAtUtc { get; private set; }

    public string? ReviewedByUserId { get; private set; }

    public DateTime? ReviewedAtUtc { get; private set; }

    public string? ReviewNotes { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void Approve(string reviewerUserId, string? notes = null)
    {
        EnsurePending();
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerUserId);

        Status = "Approved";
        ReviewedByUserId = reviewerUserId.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void Reject(string reviewerUserId, string reason)
    {
        EnsurePending();
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        Status = "Rejected";
        ReviewedByUserId = reviewerUserId.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewNotes = reason.Trim();
    }

    public void Cancel(string cancelledByUserId)
    {
        EnsurePending();
        ArgumentException.ThrowIfNullOrWhiteSpace(cancelledByUserId);

        Status = "Cancelled";
        ReviewedByUserId = cancelledByUserId.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewNotes = "ألغي من قبل صاحب الطلب / Cancelled by user";
    }

    public void EnsureCanReview()
    {
        EnsurePending();
    }

    private void EnsurePending()
    {
        if (Status != "Pending")
        {
            throw new InvalidOperationException($"Cannot alter leave request with current status '{Status}'. Only Pending requests can be updated.");
        }
    }
}
