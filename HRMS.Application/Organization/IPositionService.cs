namespace HRMS.Application.Organization;

public interface IPositionService
{
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<HRMS.Application.Common.PageResult<PositionListItem>> SearchAsync(PositionListQuery query, HRMS.Application.Common.PageRequest page, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PositionListItem>> GetListAsync(
        PositionListQuery query,
        CancellationToken cancellationToken = default);

    Task<PositionDetails?> GetByIdAsync(
        int positionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SelectOption>> GetDepartmentsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<OrganizationSaveResult> CreateAsync(
        PositionInput input,
        CancellationToken cancellationToken = default);

    Task<OrganizationSaveResult> UpdateAsync(
        int positionId,
        PositionInput input,
        CancellationToken cancellationToken = default);

    Task<bool> SetActiveAsync(
        int positionId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
