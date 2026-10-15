using System.ComponentModel.DataAnnotations;

namespace HRMS.Models.Api;

public sealed class EmployeeApiRequest
{
    public byte[]? RowVersion { get; set; }

    [Required(ErrorMessage = "ValidationRequired"), StringLength(50, ErrorMessage = "ValidationMaxLength")]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "ValidationRequired"), StringLength(100, ErrorMessage = "ValidationMaxLength")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "ValidationRequired"), StringLength(100, ErrorMessage = "ValidationMaxLength")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "ValidationRequired"), EmailAddress(ErrorMessage = "ValidationEmail"), StringLength(256, ErrorMessage = "ValidationMaxLength")]
    public string WorkEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "ValidationRequired")]
    public DateOnly? HireDate { get; set; }

    [Required(ErrorMessage = "ValidationRequired"), StringLength(32, ErrorMessage = "ValidationMaxLength")]
    public string EmploymentStatus { get; set; } = "Active";

    [Required(ErrorMessage = "ValidationRequired"), StringLength(32, ErrorMessage = "ValidationMaxLength")]
    public string EmploymentType { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "ValidationRange")]
    public int DepartmentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "ValidationRange")]
    public int PositionId { get; set; }

    public int? ManagerEmployeeId { get; set; }

    [Phone(ErrorMessage = "ValidationPhone"), StringLength(32, ErrorMessage = "ValidationMaxLength")]
    public string? PhoneNumber { get; set; }

    [StringLength(100, ErrorMessage = "ValidationMaxLength")]
    public string? WorkLocation { get; set; }
}
