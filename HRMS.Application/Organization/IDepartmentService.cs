namespace HRMS.Application.Organization;

public interface IDepartmentService
{
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<HRMS.Application.Common.PageResult<DepartmentListItem>> SearchAsync(DepartmentListQuery query, HRMS.Application.Common.PageRequest page, CancellationToken cancellationToken = default);
    Task<HRMS.Application.Common.PageResult<DepartmentEmployeeItem>> GetEmployeesAsync(int departmentId, HRMS.Application.Common.PageRequest page, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DepartmentListItem>> GetListAsync(
        DepartmentListQuery query,
        CancellationToken cancellationToken = default);

    Task<DepartmentDetails?> GetByIdAsync(
        int departmentId,
        CancellationToken cancellationToken = default, bool includeEmployees = true);

    Task<OrganizationOptions> GetFormOptionsAsync(
        int? excludeManagerEmployeeId = null,
        CancellationToken cancellationToken = default);

    Task<OrganizationSaveResult> CreateAsync(
        DepartmentInput input,
        CancellationToken cancellationToken = default);

    Task<OrganizationSaveResult> UpdateAsync(
        int departmentId,
        DepartmentInput input,
        CancellationToken cancellationToken = default);

    Task<bool> SetActiveAsync(
        int departmentId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
