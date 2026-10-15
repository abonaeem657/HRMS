using HRMS.Application.Common.Queries;
using HRMS.Application.Auditing;
using HRMS.Application.Organization;
using HRMS.Domain.Organization;
using HRMS.Application.Common;


namespace HRMS.Application.Organization;

public sealed class PositionService(
    IOrganizationData dbContext,
    IAuditLogService auditLogService) : IPositionService
{
    public Task<int> CountAsync(CancellationToken cancellationToken = default) => dbContext.Positions.CountAsync(cancellationToken);
    public async Task<IReadOnlyList<PositionListItem>> GetListAsync(PositionListQuery query, CancellationToken cancellationToken = default)
    {
        var rows = ListQuery(query);

        return await rows.ToListAsync(cancellationToken);
    }

    public Task<PageResult<PositionListItem>> SearchAsync(PositionListQuery query, PageRequest page, CancellationToken cancellationToken = default) => ListQuery(query).ToPageAsync(page, cancellationToken);

    private IQueryable<PositionListItem> ListQuery(PositionListQuery query)
    {
        var positions = dbContext.Positions.ForRead();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            positions = positions.Where(position =>
                position.Code.Contains(search) || position.Title.Contains(search));
    }

        if (query.DepartmentId is int departmentId)
        {
            positions = positions.Where(position => position.DepartmentId == departmentId);
        }

        if (query.IsActive is bool isActive)
        {
            positions = positions.Where(position => position.IsActive == isActive);
        }

        positions = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("code", false) => positions.OrderBy(p => p.Code).ThenBy(p => p.PositionId),
            ("code", true) => positions.OrderByDescending(p => p.Code).ThenBy(p => p.PositionId),
            ("department", false) => positions.OrderBy(p => p.Department == null ? "" : p.Department.Name).ThenBy(p => p.PositionId),
            ("department", true) => positions.OrderByDescending(p => p.Department == null ? "" : p.Department.Name).ThenBy(p => p.PositionId),
            ("employees", false) => positions.OrderBy(p => p.Employees.Count(e => !e.IsDeleted)).ThenBy(p => p.PositionId),
            ("employees", true) => positions.OrderByDescending(p => p.Employees.Count(e => !e.IsDeleted)).ThenBy(p => p.PositionId),
            ("status", false) => positions.OrderBy(p => p.IsActive).ThenBy(p => p.PositionId),
            ("status", true) => positions.OrderByDescending(p => p.IsActive).ThenBy(p => p.PositionId),
            (_, true) => positions.OrderByDescending(p => p.Title).ThenBy(p => p.PositionId),
            _ => positions.OrderBy(p => p.Title).ThenBy(p => p.PositionId)
        };
        return positions
            .Select(position => new PositionListItem(
                position.PositionId,
                position.Code,
                position.Title,
                position.Department == null ? null : position.Department.Name,
                position.Employees.Count(employee => !employee.IsDeleted),
                position.IsActive,
                position.Grade));
    }
    public async Task<PositionDetails?> GetByIdAsync(
        int positionId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Positions
            .ForRead()
            .Where(position => position.PositionId == positionId)
            .Select(position => new PositionDetails(
                position.PositionId,
                position.Code,
                position.Title,
                position.DepartmentId,
                position.Department == null ? null : position.Department.Name,
                position.Description,
                position.Grade,
                position.IsActive,
                position.Employees.Count(employee => !employee.IsDeleted)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SelectOption>> GetDepartmentsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Departments
            .ForRead()
            .Where(department => includeInactive || department.IsActive)
            .OrderBy(department => department.Name)
            .Select(department => new SelectOption(department.DepartmentId, department.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<OrganizationSaveResult> CreateAsync(
        PositionInput input,
        CancellationToken cancellationToken = default)
    {
        var errors = await ValidateInputAsync(input, null, cancellationToken);
        if (errors.Count > 0)
        {
            return OrganizationSaveResult.Failure(errors);
        }

        var position = new Position(input.Code, input.Title, true, input.DepartmentId,
            input.Description, input.Grade);
        dbContext.Positions.Add(position);
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(new AuditLogWriteRequest(
            "Position.Created",
            "Position",
            position.PositionId.ToString(),
            [
                new("Code", null, position.Code),
                new("Title", null, position.Title),
                new("DepartmentId", null, position.DepartmentId?.ToString()),
                new("Description", null, position.Description),
                new("Grade", null, position.Grade)
            ]), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OrganizationSaveResult.Success(position.PositionId);
    }

    public async Task<OrganizationSaveResult> UpdateAsync(
        int positionId,
        PositionInput input,
        CancellationToken cancellationToken = default)
    {
        var position = await dbContext.Positions
            .SingleOrDefaultAsync(candidate => candidate.PositionId == positionId, cancellationToken);

        if (position is null)
        {
            return OrganizationSaveResult.Failure(new Dictionary<string, string[]>
            {
                [string.Empty] = [HRMS.Application.Common.Messages.Get("Business011")]
            });
        }

        var errors = await ValidateInputAsync(input, positionId, cancellationToken);
        if (errors.Count > 0)
        {
            return OrganizationSaveResult.Failure(errors);
        }

        var changes = new List<AuditChange>();
        Add(nameof(position.Code), position.Code, input.Code.Trim());
        Add(nameof(position.Title), position.Title, input.Title.Trim());
        Add(nameof(position.DepartmentId), position.DepartmentId, input.DepartmentId);
        Add(nameof(position.Description), position.Description, input.Description?.Trim());
        Add(nameof(position.Grade), position.Grade, input.Grade?.Trim());
        position.Update(input.Code, input.Title, input.DepartmentId, input.Description, input.Grade);
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (changes.Count > 0)
        {
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                "Position.Updated",
                "Position",
                position.PositionId.ToString(),
                changes), cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return OrganizationSaveResult.Success(position.PositionId);

        void Add<T>(string field, T before, T after)
        {
            if (!EqualityComparer<T>.Default.Equals(before, after))
            {
                changes.Add(new AuditChange(
                    field,
                    Convert.ToString(before, System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToString(after, System.Globalization.CultureInfo.InvariantCulture)));
    }
        }
    }

    public async Task<bool> SetActiveAsync(
        int positionId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var position = await dbContext.Positions
            .SingleOrDefaultAsync(candidate => candidate.PositionId == positionId, cancellationToken);

        if (position is null)
        {
            return false;
        }

        var wasActive = position.IsActive;
        position.SetActive(isActive);
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (wasActive != isActive)
        {
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                isActive ? "Position.Activated" : "Position.Deactivated",
                "Position",
                position.PositionId.ToString(),
                [new("IsActive", wasActive.ToString(), isActive.ToString())]), cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<Dictionary<string, string[]>> ValidateInputAsync(
        PositionInput input,
        int? positionId,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var departmentId = input.DepartmentId;
        if (string.IsNullOrWhiteSpace(input.Code) || input.Code.Trim().Length > 50)
            errors[nameof(PositionInput.Code)] = [HRMS.Application.Common.Messages.Get("Extra003")];
        else if (await dbContext.Positions.AnyAsync(p => p.Code == input.Code.Trim() && p.PositionId != positionId, cancellationToken))
            errors[nameof(PositionInput.Code)] = [HRMS.Application.Common.Messages.Get("Extra004")];
        if (string.IsNullOrWhiteSpace(input.Title))
            errors[nameof(PositionInput.Title)] = [HRMS.Application.Common.Messages.Get("Extra005")];
        if (input.Description?.Length > 2000 || input.Grade?.Length > 50)
            errors[string.Empty] = [HRMS.Application.Common.Messages.Get("Extra006")];
        if (departmentId is int id)
        {
            var departmentIsActive = await dbContext.Departments
                .AnyAsync(department => department.DepartmentId == id && department.IsActive, cancellationToken);
            if (!departmentIsActive)
            {
                errors[nameof(PositionInput.DepartmentId)] = [HRMS.Application.Common.Messages.Get("Business006")];
            }
        }

        if (positionId is int currentPositionId)
        {
            var hasEmployeesInOtherDepartments = await dbContext.Employees
                .AnyAsync(
                    employee => !employee.IsDeleted &&
                                employee.PositionId == currentPositionId &&
                                employee.DepartmentId != departmentId,
                    cancellationToken);
            if (hasEmployeesInOtherDepartments)
            {
                errors[nameof(PositionInput.DepartmentId)] =
                    [HRMS.Application.Common.Messages.Get("Business055")];
            }
        }

        return errors;
    }
}
