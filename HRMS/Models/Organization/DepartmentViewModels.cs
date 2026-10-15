using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Models.Organization;

public sealed class DepartmentListViewModel
{
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    public IReadOnlyList<DepartmentListItemViewModel> Departments { get; set; } =
        Array.Empty<DepartmentListItemViewModel>();
}

public sealed record DepartmentListItemViewModel(
    int DepartmentId,
    string Code,
    string Name,
    string? ManagerName,
    int EmployeeCount,
    bool IsActive);

public sealed class DepartmentFormViewModel
{
    [Required(ErrorMessage = "DepartmentCodeRequired")]
    [Display(Name = "Label.DepartmentCode")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "DepartmentNameRequired")]
    [Display(Name = "Label.DepartmentName")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Label.DepartmentManager")]
    public int? ManagerEmployeeId { get; set; }

    public IReadOnlyList<SelectListItem> Managers { get; set; } =
        Array.Empty<SelectListItem>();
}

public sealed record DepartmentDetailsViewModel(
    int DepartmentId,
    string Code,
    string Name,
    string? ManagerName,
    bool IsActive,
    IReadOnlyList<DepartmentEmployeeViewModel> Employees);

public sealed record DepartmentEmployeeViewModel(
    int EmployeeId,
    string EmployeeNumber,
    string FullName,
    string PositionTitle,
    string EmploymentStatus);
