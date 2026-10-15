namespace HRMS.Application.Organization;

public interface IDepartmentService
{
    Task<IReadOnlyList<DepartmentListItem>> GetListAsync(
        DepartmentListQuery query,
        CancellationToken cancellationToken = default);

    Task<DepartmentDetails?> GetByIdAsync(
        int departmentId,
        CancellationToken cancellationToken = default);

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
