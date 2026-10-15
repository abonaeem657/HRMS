namespace HRMS.Application.Employees;

public interface IEmployeeService
{
    Task<EmployeeDetails?> GetOwnProfileAsync(string identityUserId, CancellationToken cancellationToken = default);
    Task<EmployeeListPage> SearchAsync(
        EmployeeListQuery query,
        CancellationToken cancellationToken = default);

    Task<EmployeeListFilterOptions> GetListFilterOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<EmployeeDetails?> GetByIdAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<EmployeeFormOptions> GetFormOptionsAsync(
        int? excludeEmployeeId = null,
        int? departmentId = null,
        CancellationToken cancellationToken = default);

    Task<EmployeeSaveResult> CreateAsync(
        EmployeeInput input,
        CancellationToken cancellationToken = default);

    Task<EmployeeSaveResult> UpdateAsync(
        int employeeId,
        EmployeeInput input,
        CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteAsync(
        int employeeId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<DeletedEmployeePage> GetDeletedAsync(
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default, bool showAll = false);

    Task<bool> RestoreAsync(
        int employeeId,
        string? actorUserId,
        CancellationToken cancellationToken = default);
}
