namespace HRMS.Application.Employees;

public sealed record EmployeeListQuery(
    string? Search,
    int? DepartmentId,
    string? EmploymentStatus,
    string? SortBy,
    bool SortDescending,
    int Page,
    int PageSize, bool ShowAll = false)
{
    public const string AllStatuses = "__all__";
}

public sealed record EmployeeListItem(
    int EmployeeId,
    string EmployeeNumber,
    string FullName,
    string WorkEmail,
    string DepartmentName,
    string PositionTitle,
    string EmploymentStatus,
    DateOnly HireDate);

public sealed record EmployeeListPage(
    IReadOnlyList<EmployeeListItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record DeletedEmployeeSummary(
    int EmployeeId,
    string EmployeeNumber,
    string FullName,
    string DepartmentName,
    DateTime? DeletedAtUtc,
    string? DeletedByUserId);

public sealed record DeletedEmployeePage(
    IReadOnlyList<DeletedEmployeeSummary> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record EmployeeDetails(
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
    string? ManagerName,
    int DepartmentId,
    int PositionId,
    int? ManagerEmployeeId,
    bool IsDeleted,
    byte[] RowVersion);

public sealed record EmployeeInput(
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string WorkEmail,
    DateOnly HireDate,
    string EmploymentStatus,
    string EmploymentType,
    int DepartmentId,
    int PositionId,
    int? ManagerEmployeeId,
    string? PhoneNumber,
    string? WorkLocation,
    byte[]? RowVersion = null);

public sealed record SelectOption(int Id, string Text);

public sealed record EmployeeFormOptions(
    IReadOnlyList<SelectOption> Departments,
    IReadOnlyList<SelectOption> Positions,
    IReadOnlyList<SelectOption> Managers);

public sealed record EmployeeListFilterOptions(
    IReadOnlyList<SelectOption> Departments,
    IReadOnlyList<string> EmploymentStatuses);

public sealed record EmployeeSaveResult(
    bool Succeeded,
    int? EmployeeId,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public static EmployeeSaveResult Success(int employeeId) =>
        new(true, employeeId, new Dictionary<string, string[]>());

    public static EmployeeSaveResult Failure(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(false, null, errors);
}
