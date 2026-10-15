namespace HRMS.Models.Employees;

public sealed record EmployeeDetailsViewModel(
    int EmployeeId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string WorkEmail,
    string? PhoneNumber,
    DateOnly HireDate,
    string EmploymentStatus,
    string EmploymentType,
    string? WorkLocation,
    string DepartmentName,
    string PositionTitle,
    string? ManagerName);
