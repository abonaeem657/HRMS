using HRMS.Application.Common.Queries;
using HRMS.Application.Auditing;
using HRMS.Application.Organization;
using HRMS.Domain.Organization;
using HRMS.Application.Common;


namespace HRMS.Application.Organization;

public sealed class DepartmentService(
    IOrganizationData dbContext,
    IAuditLogService auditLogService) : IDepartmentService
{
    public Task<int> CountAsync(CancellationToken cancellationToken = default) => dbContext.Departments.CountAsync(cancellationToken);
    public async Task<IReadOnlyList<DepartmentListItem>> GetListAsync(DepartmentListQuery query, CancellationToken cancellationToken = default)
    {
        var rows = ListQuery(query);
        if (query.Page is int requestedPage)
            rows = rows.Skip((Math.Clamp(requestedPage, 1, 100000) - 1) * Math.Clamp(query.PageSize, 1, 100)).Take(Math.Clamp(query.PageSize, 1, 100));
        return await rows.ToListAsync(cancellationToken);
    }

    public Task<PageResult<DepartmentListItem>> SearchAsync(DepartmentListQuery query, PageRequest page, CancellationToken cancellationToken = default) => ListQuery(query).ToPageAsync(page, cancellationToken);

    private IQueryable<DepartmentListItem> ListQuery(DepartmentListQuery query)
    {
        var departments = dbContext.Departments.ForRead();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            departments = departments.Where(department =>
                department.Code.Contains(search) || department.Name.Contains(search));
    }

        if (query.IsActive is bool isActive)
        {
            departments = departments.Where(department => department.IsActive == isActive);
        }

        departments = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("code", false) => departments.OrderBy(d => d.Code).ThenBy(d => d.DepartmentId),
            ("code", true) => departments.OrderByDescending(d => d.Code).ThenBy(d => d.DepartmentId),
            ("employees", false) => departments.OrderBy(d => d.Employees.Count(e => !e.IsDeleted)).ThenBy(d => d.DepartmentId),
            ("employees", true) => departments.OrderByDescending(d => d.Employees.Count(e => !e.IsDeleted)).ThenBy(d => d.DepartmentId),
            ("status", false) => departments.OrderBy(d => d.IsActive).ThenBy(d => d.DepartmentId),
            ("status", true) => departments.OrderByDescending(d => d.IsActive).ThenBy(d => d.DepartmentId),
            (_, true) => departments.OrderByDescending(d => d.Name).ThenBy(d => d.DepartmentId),
            _ => departments.OrderBy(d => d.Name).ThenBy(d => d.DepartmentId)
        };
        return departments
            .Select(department => new DepartmentListItem(
                department.DepartmentId,
                department.Code,
                department.Name,
                department.Manager == null
                    ? null
                    : department.Manager.FirstName + " " + department.Manager.LastName,
                department.Employees.Count(employee => !employee.IsDeleted),
                department.IsActive));
    }
    public Task<PageResult<DepartmentEmployeeItem>> GetEmployeesAsync(int departmentId, PageRequest page, CancellationToken cancellationToken = default) =>
        dbContext.Employees.Where(e => !e.IsDeleted && e.DepartmentId == departmentId)
            .OrderBy(e => e.EmployeeNumber).ThenBy(e => e.EmployeeId)
            .Select(e => new DepartmentEmployeeItem(e.EmployeeId, e.EmployeeNumber, e.FirstName + " " + e.LastName, e.Position!.Title, e.EmploymentStatus))
            .ToPageAsync(page, cancellationToken);
    public async Task<DepartmentDetails?> GetByIdAsync(
        int departmentId,
        CancellationToken cancellationToken = default, bool includeEmployees = true)
    {
        return await dbContext.Departments
            .ForRead()
            .Where(department => department.DepartmentId == departmentId)
            .Select(department => new DepartmentDetails(
                department.DepartmentId,
                department.Code,
                department.Name,
                department.ManagerEmployeeId,
                department.Manager == null
                    ? null
                    : department.Manager.FirstName + " " + department.Manager.LastName,
                department.IsActive,
                department.Employees
                    .Where(employee => includeEmployees && !employee.IsDeleted)
                    .OrderBy(employee => employee.EmployeeNumber)
                    .Select(employee => new DepartmentEmployeeItem(
                        employee.EmployeeId,
                        employee.EmployeeNumber,
                        employee.FirstName + " " + employee.LastName,
                        employee.Position == null ? string.Empty : employee.Position.Title,
                        employee.EmploymentStatus))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OrganizationOptions> GetFormOptionsAsync(
        int? excludeManagerEmployeeId = null,
        CancellationToken cancellationToken = default)
    {
        var departments = await dbContext.Departments
            .ForRead()
            .Where(department => department.IsActive)
            .OrderBy(department => department.Name)
            .Select(department => new SelectOption(department.DepartmentId, department.Name))
            .ToListAsync(cancellationToken);

        var managersQuery = dbContext.Employees
            .ForRead()
            .Where(employee => !employee.IsDeleted);

        if (excludeManagerEmployeeId is int employeeId)
        {
            managersQuery = managersQuery.Where(employee => employee.EmployeeId != employeeId);
        }

        var managers = await managersQuery
            .OrderBy(employee => employee.FirstName)
            .ThenBy(employee => employee.LastName)
            .Select(employee => new SelectOption(
                employee.EmployeeId,
                employee.EmployeeNumber + " - " + employee.FirstName + " " + employee.LastName))
            .ToListAsync(cancellationToken);

        return new OrganizationOptions(departments, managers);
    }

    public async Task<OrganizationSaveResult> CreateAsync(
        DepartmentInput input,
        CancellationToken cancellationToken = default)
    {
        var errors = await ValidateManagerAsync(input.ManagerEmployeeId, null, cancellationToken);
        if (errors.Count > 0)
        {
            return OrganizationSaveResult.Failure(errors);
        }

        var department = new Department(input.Code, input.Name, true);
        department.AssignManager(input.ManagerEmployeeId);
        dbContext.Departments.Add(department);
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(new AuditLogWriteRequest(
            "Department.Created",
            "Department",
            department.DepartmentId.ToString(),
            [
                new("Code", null, department.Code),
                new("Name", null, department.Name),
                new("ManagerEmployeeId", null, department.ManagerEmployeeId?.ToString())
            ]), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OrganizationSaveResult.Success(department.DepartmentId);
    }

    public async Task<OrganizationSaveResult> UpdateAsync(
        int departmentId,
        DepartmentInput input,
        CancellationToken cancellationToken = default)
    {
        var department = await dbContext.Departments
            .SingleOrDefaultAsync(candidate => candidate.DepartmentId == departmentId, cancellationToken);

        if (department is null)
        {
            return OrganizationSaveResult.Failure(new Dictionary<string, string[]>
            {
                [string.Empty] = [HRMS.Application.Common.Messages.Get("Business007")]
            });
        }

        var errors = await ValidateManagerAsync(input.ManagerEmployeeId, departmentId, cancellationToken);
        if (errors.Count > 0)
        {
            return OrganizationSaveResult.Failure(errors);
        }

        var changes = new List<AuditChange>();
        Add(nameof(department.Code), department.Code, input.Code.Trim());
        Add(nameof(department.Name), department.Name, input.Name.Trim());
        Add(nameof(department.ManagerEmployeeId), department.ManagerEmployeeId, input.ManagerEmployeeId);
        department.Update(input.Code, input.Name);
        department.AssignManager(input.ManagerEmployeeId);
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (changes.Count > 0)
        {
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                "Department.Updated",
                "Department",
                department.DepartmentId.ToString(),
                changes), cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return OrganizationSaveResult.Success(department.DepartmentId);

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
        int departmentId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var department = await dbContext.Departments
            .SingleOrDefaultAsync(candidate => candidate.DepartmentId == departmentId, cancellationToken);

        if (department is null)
        {
            return false;
        }

        var wasActive = department.IsActive;
        department.SetActive(isActive);
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (wasActive != isActive)
        {
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                isActive ? "Department.Activated" : "Department.Deactivated",
                "Department",
                department.DepartmentId.ToString(),
                [new("IsActive", wasActive.ToString(), isActive.ToString())]), cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<Dictionary<string, string[]>> ValidateManagerAsync(
        int? managerEmployeeId,
        int? departmentId,
        CancellationToken cancellationToken)
    {
        if (managerEmployeeId is not int employeeId)
        {
            return [];
        }

        var managerExists = await dbContext.Employees
            .AnyAsync(employee => employee.EmployeeId == employeeId && !employee.IsDeleted && employee.DepartmentId == departmentId &&
                (employee.EmploymentStatus == "Active" || employee.EmploymentStatus == "نشط" || employee.EmploymentStatus == "على رأس العمل"), cancellationToken);
        return managerExists
            ? []
            : new Dictionary<string, string[]>
            {
                [nameof(DepartmentInput.ManagerEmployeeId)] = [HRMS.Application.Common.Messages.Get("Business012")]
            };
    }
}
