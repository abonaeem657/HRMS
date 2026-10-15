using HRMS.Domain.Organization;
using HRMS.Domain.Attendance;
using HRMS.Domain.LeaveManagement;
using System.ComponentModel.DataAnnotations;

namespace HRMS.Domain.Employees;

public class Employee
{
    private Employee()
    {
    }

    public Employee(
        string employeeNumber,
        string firstName,
        string lastName,
        string workEmail,
        DateOnly hireDate,
        string employmentStatus,
        int departmentId,
        int positionId,
        string employmentType,
        int? managerEmployeeId = null,
        string? phoneNumber = null,
        string? workLocation = null,
        string? identityUserId = null)
    {
        EmployeeNumber = RequireValue(employeeNumber, nameof(employeeNumber));
        FirstName = RequireValue(firstName, nameof(firstName));
        LastName = RequireValue(lastName, nameof(lastName));
        WorkEmail = RequireEmail(workEmail, nameof(workEmail));
        HireDate = RequireHireDate(hireDate);
        EmploymentStatus = RequireValue(employmentStatus, nameof(employmentStatus));
        DepartmentId = departmentId;
        PositionId = positionId;
        EmploymentType = RequireValue(employmentType, nameof(employmentType));
        ManagerEmployeeId = managerEmployeeId;
        PhoneNumber = NormalizeOptional(phoneNumber);
        WorkLocation = NormalizeOptional(workLocation);
        IdentityUserId = NormalizeOptional(identityUserId);
    }

    public int EmployeeId { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public string EmployeeNumber { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string WorkEmail { get; private set; } = string.Empty;

    public string? PhoneNumber { get; private set; }

    public DateOnly HireDate { get; private set; }

    public string EmploymentStatus { get; private set; } = string.Empty;

    public string EmploymentType { get; private set; } = string.Empty;

    public string? WorkLocation { get; private set; }

    public int DepartmentId { get; private set; }

    public Department? Department { get; private set; }

    public int PositionId { get; private set; }

    public Position? Position { get; private set; }

    public int? ManagerEmployeeId { get; private set; }

    public Employee? Manager { get; private set; }

    public ICollection<Employee> DirectReports { get; } = new List<Employee>();

    public ICollection<AttendanceRecord> AttendanceRecords { get; } = new List<AttendanceRecord>();

    public ICollection<LeaveRequest> LeaveRequests { get; } = new List<LeaveRequest>();

    public string? IdentityUserId { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public string? DeletedByUserId { get; private set; }

    public void Update(
        string employeeNumber,
        string firstName,
        string lastName,
        string workEmail,
        DateOnly hireDate,
        string employmentStatus,
        int departmentId,
        int positionId,
        string employmentType,
        int? managerEmployeeId,
        string? phoneNumber,
        string? workLocation,
        string? identityUserId)
    {
        ValidateManager(managerEmployeeId);

        var validatedEmployeeNumber = RequireValue(employeeNumber, nameof(employeeNumber));
        var validatedFirstName = RequireValue(firstName, nameof(firstName));
        var validatedLastName = RequireValue(lastName, nameof(lastName));
        var validatedWorkEmail = RequireEmail(workEmail, nameof(workEmail));
        var validatedHireDate = RequireHireDate(hireDate);
        var validatedEmploymentStatus = RequireValue(employmentStatus, nameof(employmentStatus));
        var validatedEmploymentType = RequireValue(employmentType, nameof(employmentType));

        EmployeeNumber = validatedEmployeeNumber;
        FirstName = validatedFirstName;
        LastName = validatedLastName;
        WorkEmail = validatedWorkEmail;
        HireDate = validatedHireDate;
        EmploymentStatus = validatedEmploymentStatus;
        DepartmentId = departmentId;
        PositionId = positionId;
        EmploymentType = validatedEmploymentType;
        ManagerEmployeeId = managerEmployeeId;
        PhoneNumber = NormalizeOptional(phoneNumber);
        WorkLocation = NormalizeOptional(workLocation);
        IdentityUserId = NormalizeOptional(identityUserId);
    }

    public void AssignManager(int? managerEmployeeId)
    {
        ValidateManager(managerEmployeeId);
        ManagerEmployeeId = managerEmployeeId;
    }

    public void LinkIdentityUser(string? identityUserId)
    {
        IdentityUserId = NormalizeOptional(identityUserId);
    }

    public void SoftDelete(DateTime deletedAtUtc, string? deletedByUserId)
    {
        if (deletedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Deletion time must be UTC.", nameof(deletedAtUtc));
        }

        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
        DeletedByUserId = string.IsNullOrWhiteSpace(deletedByUserId) ? null : deletedByUserId.Trim();
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAtUtc = null;
        DeletedByUserId = null;
    }

    private void ValidateManager(int? managerEmployeeId)
    {
        if (managerEmployeeId == EmployeeId && EmployeeId != 0)
        {
            throw new ArgumentException("An employee cannot be their own manager.", nameof(managerEmployeeId));
        }
    }

    private static string RequireValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string RequireEmail(string value, string parameterName)
    {
        var email = RequireValue(value, parameterName);
        if (!new EmailAddressAttribute().IsValid(email))
        {
            throw new ArgumentException("A valid work email is required.", parameterName);
        }

        return email;
    }

    private static DateOnly RequireHireDate(DateOnly hireDate)
    {
        if (hireDate == default)
        {
            throw new ArgumentOutOfRangeException(nameof(hireDate), "A hire date is required.");
        }

        return hireDate;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
