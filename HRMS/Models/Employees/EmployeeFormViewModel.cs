using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Models.Employees;

public sealed class EmployeeFormViewModel
{
    public byte[]? RowVersion { get; set; }

    [Required(ErrorMessage = "EmployeeNumberRequired")]
    [StringLength(450, ErrorMessage = "EmployeeNumberTooLong")]
    [Display(Name = "Label.EmployeeNumber")]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "FirstNameRequired")]
    [Display(Name = "Label.FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "LastNameRequired")]
    [Display(Name = "Label.LastName")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "WorkEmailRequired")]
    [EmailAddress(ErrorMessage = "ValidEmailRequired")]
    [Display(Name = "Label.WorkEmail")]
    public string WorkEmail { get; set; } = string.Empty;

    [Display(Name = "Label.PhoneNumber")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "HireDateRequired")]
    [DataType(DataType.Date)]
    [Display(Name = "Label.HireDate")]
    public DateOnly? HireDate { get; set; }

    [Required(ErrorMessage = "EmploymentStatusRequired")]
    [Display(Name = "Label.EmploymentStatus")]
    public string EmploymentStatus { get; set; } = "Active";

    [Required(ErrorMessage = "EmploymentTypeRequired")]
    [Display(Name = "Label.EmploymentType")]
    public string EmploymentType { get; set; } = "FullTime";

    [Range(1, int.MaxValue, ErrorMessage = "ValidDepartmentRequired")]
    [Display(Name = "Label.Department")]
    public int DepartmentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "ValidPositionRequired")]
    [Display(Name = "Label.JobTitle")]
    public int PositionId { get; set; }

    [Display(Name = "Label.DirectManager")]
    public int? ManagerEmployeeId { get; set; }

    [Display(Name = "Label.WorkLocation")]
    public string? WorkLocation { get; set; }

    public IReadOnlyList<SelectListItem> Departments { get; set; } =
        Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> Positions { get; set; } =
        Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> Managers { get; set; } =
        Array.Empty<SelectListItem>();
}
