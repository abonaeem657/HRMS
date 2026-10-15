using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Models.Employees;

public sealed class EmployeeListViewModel
{
    public string? Search { get; set; }

    public int? DepartmentId { get; set; }

    public string? EmploymentStatus { get; set; }

    public string? SortBy { get; set; }

    public bool SortDescending { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 6;

    public int TotalCount { get; set; }

    public IReadOnlyList<EmployeeListRowViewModel> Employees { get; set; } =
        Array.Empty<EmployeeListRowViewModel>();

    public IReadOnlyList<SelectListItem> Departments { get; set; } =
        Array.Empty<SelectListItem>();

    public IReadOnlyList<string> EmploymentStatuses { get; set; } =
        Array.Empty<string>();

    public int PageCount => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record EmployeeListRowViewModel(
    int EmployeeId,
    string EmployeeNumber,
    string FullName,
    string WorkEmail,
    string DepartmentName,
    string PositionTitle,
    string EmploymentStatus,
    DateOnly HireDate);
