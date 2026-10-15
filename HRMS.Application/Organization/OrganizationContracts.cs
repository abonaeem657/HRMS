namespace HRMS.Application.Organization;

public sealed record DepartmentListQuery(string? Search, bool? IsActive, int? Page = null, int PageSize = 25,
    string? SortBy = null, bool SortDescending = false);

public sealed record DepartmentListItem(
    int DepartmentId,
    string Code,
    string Name,
    string? ManagerName,
    int EmployeeCount,
    bool IsActive);

public sealed record DepartmentEmployeeItem(
    int EmployeeId,
    string EmployeeNumber,
    string FullName,
    string PositionTitle,
    string EmploymentStatus);

public sealed record DepartmentDetails(
    int DepartmentId,
    string Code,
    string Name,
    int? ManagerEmployeeId,
    string? ManagerName,
    bool IsActive,
    IReadOnlyList<DepartmentEmployeeItem> Employees);

public sealed record DepartmentInput(string Code, string Name, int? ManagerEmployeeId);

public sealed record PositionListQuery(string? Search, int? DepartmentId, bool? IsActive,
    string? SortBy = null, bool SortDescending = false);

public sealed record PositionListItem(
    int PositionId,
    string Code,
    string Title,
    string? DepartmentName,
    int EmployeeCount,
    bool IsActive,
    string? Grade);

public sealed record PositionDetails(
    int PositionId,
    string Code,
    string Title,
    int? DepartmentId,
    string? DepartmentName,
    string? Description,
    string? Grade,
    bool IsActive,
    int EmployeeCount);

public sealed record PositionInput(string Code, string Title, int? DepartmentId,
    string? Description = null, string? Grade = null);

public sealed record OrganizationSaveResult(
    bool Succeeded,
    int? Id,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public static OrganizationSaveResult Success(int id) =>
        new(true, id, new Dictionary<string, string[]>());

    public static OrganizationSaveResult Failure(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(false, null, errors);
}

public sealed record OrganizationOptions(
    IReadOnlyList<SelectOption> Departments,
    IReadOnlyList<SelectOption> Managers);

public sealed record SelectOption(int Id, string Text);
