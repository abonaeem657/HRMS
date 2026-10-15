using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Models.Organization;

public sealed class PositionListViewModel
{
    public string? Search { get; set; }

    public int? DepartmentId { get; set; }

    public bool? IsActive { get; set; }

    public IReadOnlyList<SelectListItem> Departments { get; set; } =
        Array.Empty<SelectListItem>();

    public IReadOnlyList<PositionListItemViewModel> Positions { get; set; } =
        Array.Empty<PositionListItemViewModel>();
}

public sealed record PositionListItemViewModel(
    int PositionId,
    string Code,
    string Title,
    string? DepartmentName,
    int EmployeeCount,
    bool IsActive,
    string? Grade);

public sealed class PositionFormViewModel
{
    [Required(ErrorMessage = "PositionCodeRequired")]
    [StringLength(50, ErrorMessage = "ValidationMaxLength")]
    [Display(Name = "Label.PositionCode")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "PositionTitleRequired")]
    [StringLength(200, ErrorMessage = "ValidationMaxLength")]
    [Display(Name = "Label.PositionTitle")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Label.OptionalDepartment")]
    public int? DepartmentId { get; set; }

    [StringLength(2000, ErrorMessage = "ValidationMaxLength")]
    [Display(Name = "Label.Description")]
    public string? Description { get; set; }

    [StringLength(50, ErrorMessage = "ValidationMaxLength")]
    [Display(Name = "Label.Grade")]
    public string? Grade { get; set; }

    public IReadOnlyList<SelectListItem> Departments { get; set; } =
        Array.Empty<SelectListItem>();
}

public sealed record PositionDetailsViewModel(
    int PositionId,
    string Code,
    string Title,
    string? DepartmentName,
    string? Description,
    string? Grade,
    bool IsActive,
    int EmployeeCount);
