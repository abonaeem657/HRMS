using System.ComponentModel.DataAnnotations;

namespace HRMS.Models.Api;

public sealed class EmployeeApiRequest
{
    public byte[]? RowVersion { get; set; }

    [Required, StringLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string WorkEmail { get; set; } = string.Empty;

    [Required]
    public DateOnly? HireDate { get; set; }

    [Required, StringLength(32)]
    public string EmploymentStatus { get; set; } = "Active";

    [Required, StringLength(32)]
    public string EmploymentType { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int DepartmentId { get; set; }

    [Range(1, int.MaxValue)]
    public int PositionId { get; set; }

    public int? ManagerEmployeeId { get; set; }

    [Phone, StringLength(32)]
    public string? PhoneNumber { get; set; }

    [StringLength(100)]
    public string? WorkLocation { get; set; }
}
