using HRMS.Application.Common.Queries;
using HRMS.Application.Employees;
using HRMS.Application.Auditing;
using HRMS.Domain.Employees;
using HRMS.Application.Common;


using Microsoft.Extensions.Logging;

namespace HRMS.Application.Employees;

public sealed class EmployeeService(
    IEmployeeData dbContext,
    IAuditLogService auditLogService,
    ILogger<EmployeeService> logger) : IEmployeeService
{
    private const int MaximumPageSize = 100;
    public async Task<EmployeeDetails?> GetOwnProfileAsync(string identityUserId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityUserId);
        var id = await dbContext.Employees.ForRead().Where(e => e.IdentityUserId == identityUserId && !e.IsDeleted)
            .Select(e => (int?)e.EmployeeId).SingleOrDefaultAsync(cancellationToken);
        return id.HasValue ? await GetByIdAsync(id.Value, cancellationToken) : null;
    }

    public async Task<EmployeeListFilterOptions> GetListFilterOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var departments = await dbContext.Departments.ForRead()
            .OrderBy(department => department.Name)
            .Select(department => new SelectOption(department.DepartmentId, department.Name))
            .ToListAsync(cancellationToken);
        var statuses = await dbContext.Employees.ForRead()
            .Where(employee => !employee.IsDeleted)
            .Select(employee => employee.EmploymentStatus)
            .Distinct()
            .OrderBy(status => status)
            .ToListAsync(cancellationToken);
        return new EmployeeListFilterOptions(departments, statuses);
    }

    public async Task<EmployeeListPage> SearchAsync(
        EmployeeListQuery query,
        CancellationToken cancellationToken = default)
    {
        var employees = dbContext.Employees
            .ForRead()
            .Where(employee => !employee.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            employees = employees.Where(employee =>
                employee.EmployeeNumber.Contains(search) ||
                employee.FirstName.Contains(search) ||
                employee.LastName.Contains(search) ||
                (employee.FirstName + " " + employee.LastName).Contains(search) ||
                employee.WorkEmail.Contains(search) ||
                employee.Department!.Name.Contains(search) ||
                employee.EmploymentStatus.Contains(search));
        }

        if (query.DepartmentId is int departmentId)
        {
            employees = employees.Where(employee => employee.DepartmentId == departmentId);
        }

        if (!string.IsNullOrWhiteSpace(query.EmploymentStatus) &&
            query.EmploymentStatus != EmployeeListQuery.AllStatuses)
        {
            employees = employees.Where(employee =>
                employee.EmploymentStatus == query.EmploymentStatus);
        }

        if (string.IsNullOrWhiteSpace(query.EmploymentStatus))
        {
            employees = employees.Where(employee => employee.EmploymentStatus == "Active" ||
                employee.EmploymentStatus == "نشط" || employee.EmploymentStatus == "على رأس العمل");
        }

        employees = ApplySorting(employees, query.SortBy, query.SortDescending);

        var pageSize = Math.Clamp(query.PageSize, 1, MaximumPageSize);
        var totalCount = await employees.CountAsync(cancellationToken);
        if (query.ShowAll) pageSize = Math.Max(1, totalCount);
        var pageCount = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var page = Math.Clamp(query.Page, 1, pageCount);
        var items = await employees
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(employee => new EmployeeListItem(
                employee.EmployeeId,
                employee.EmployeeNumber,
                employee.FirstName + " " + employee.LastName,
                employee.WorkEmail,
                employee.Department!.Name,
                employee.Position!.Title,
                employee.EmploymentStatus,
                employee.HireDate))
            .ToListAsync(cancellationToken);

        return new EmployeeListPage(items, totalCount, page, pageSize);
    }

    public async Task<EmployeeDetails?> GetByIdAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Employees
            .ForRead()
            .Where(employee => employee.EmployeeId == employeeId && !employee.IsDeleted)
            .Select(employee => new EmployeeDetails(
                employee.EmployeeId,
                employee.EmployeeNumber,
                employee.FirstName,
                employee.LastName,
                employee.WorkEmail,
                employee.PhoneNumber,
                employee.HireDate,
                employee.EmploymentStatus,
                employee.EmploymentType,
                employee.WorkLocation,
                employee.Department!.Name,
                employee.Position!.Title,
                employee.Manager == null
                    ? null
                    : employee.Manager.FirstName + " " + employee.Manager.LastName,
                employee.DepartmentId,
                employee.PositionId,
                employee.ManagerEmployeeId,
                employee.IsDeleted,
                employee.RowVersion))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<EmployeeFormOptions> GetFormOptionsAsync(
        int? excludeEmployeeId = null,
        int? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        var departments = await dbContext.Departments
            .ForRead()
            .Where(department => department.IsActive)
            .OrderBy(department => department.Name)
            .Select(department => new SelectOption(department.DepartmentId, department.Name))
            .ToListAsync(cancellationToken);

        var positionsQuery = dbContext.Positions
            .ForRead()
            .Where(position => position.IsActive);

        if (departmentId is int selectedDepartmentId)
        {
            positionsQuery = positionsQuery.Where(position =>
                position.DepartmentId == null || position.DepartmentId == selectedDepartmentId);
        }

        var positions = await positionsQuery
            .OrderBy(position => position.Title)
            .Select(position => new SelectOption(position.PositionId, position.Title))
            .ToListAsync(cancellationToken);

        var managerQuery = dbContext.Employees
            .ForRead()
            .Where(employee => !employee.IsDeleted && (employee.EmploymentStatus == "Active" || employee.EmploymentStatus == "نشط" || employee.EmploymentStatus == "على رأس العمل"));

        if (excludeEmployeeId is int employeeId)
        {
            managerQuery = managerQuery.Where(employee => employee.EmployeeId != employeeId);
        }

        var managers = await managerQuery
            .OrderBy(employee => employee.FirstName)
            .ThenBy(employee => employee.LastName)
            .Select(employee => new SelectOption(
                employee.EmployeeId,
                employee.EmployeeNumber + " - " + employee.FirstName + " " + employee.LastName))
            .ToListAsync(cancellationToken);

        return new EmployeeFormOptions(departments, positions, managers);
    }

    public async Task<EmployeeSaveResult> CreateAsync(
        EmployeeInput input,
        CancellationToken cancellationToken = default)
    {
        var errors = await ValidateReferencesAsync(input, null, cancellationToken);
        if (errors.Count > 0)
        {
            return EmployeeSaveResult.Failure(errors);
        }

        var employee = new Employee(
            input.EmployeeNumber,
            input.FirstName,
            input.LastName,
            input.WorkEmail,
            input.HireDate,
            input.EmploymentStatus,
            input.DepartmentId,
            input.PositionId,
            input.EmploymentType,
            input.ManagerEmployeeId,
            input.PhoneNumber,
            input.WorkLocation);

        dbContext.Employees.Add(employee);
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                "Employee.Created",
                "Employee",
                employee.EmployeeId.ToString(),
                [
                    new("EmployeeNumber", null, null),
                    new("DepartmentId", null, input.DepartmentId.ToString()),
                    new("PositionId", null, input.PositionId.ToString()),
                    new("EmploymentStatus", null, input.EmploymentStatus),
                    new("EmploymentType", null, input.EmploymentType)
                ]), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return EmployeeSaveResult.Success(employee.EmployeeId);
        }
        catch (DataWriteException exception) when (IsEmployeeNumberConflict(exception))
        {
            return EmployeeSaveResult.Failure(new Dictionary<string, string[]>
            {
                [nameof(EmployeeInput.EmployeeNumber)] = [HRMS.Application.Common.Messages.Get("Business040")]
            });
        }
        catch (DataWriteException exception)
        {
            logger.LogError(exception, "Failed to create employee {EmployeeNumber}.", input.EmployeeNumber);
            throw;
        }
    }

    public async Task<EmployeeSaveResult> UpdateAsync(
        int employeeId,
        EmployeeInput input,
        CancellationToken cancellationToken = default)
    {
        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(
                candidate => candidate.EmployeeId == employeeId && !candidate.IsDeleted,
                cancellationToken);

        if (employee is null)
        {
            return EmployeeSaveResult.Failure(new Dictionary<string, string[]>
            {
                [string.Empty] = [HRMS.Application.Common.Messages.Get("Business014")]
            });
        }

        var errors = await ValidateReferencesAsync(input, employeeId, cancellationToken);
        if (errors.Count > 0)
        {
            return EmployeeSaveResult.Failure(errors);
        }

        if (input.RowVersion is null || !input.RowVersion.SequenceEqual(employee.RowVersion))
            throw new DataConcurrencyException("The employee changed. Reload the form before saving.");
        dbContext.ExpectVersion(employee, input.RowVersion);
        var changes = GetEmployeeChanges(employee, input);
        employee.Update(
            input.EmployeeNumber,
            input.FirstName,
            input.LastName,
            input.WorkEmail,
            input.HireDate,
            input.EmploymentStatus,
            input.DepartmentId,
            input.PositionId,
            input.EmploymentType,
            input.ManagerEmployeeId,
            input.PhoneNumber,
            input.WorkLocation,
            employee.IdentityUserId);

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (changes.Count > 0)
            {
                await auditLogService.RecordAsync(new AuditLogWriteRequest(
                    "Employee.Updated",
                    "Employee",
                    employee.EmployeeId.ToString(),
                    changes), cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return EmployeeSaveResult.Success(employee.EmployeeId);
        }
        catch (DataWriteException exception) when (IsEmployeeNumberConflict(exception))
        {
            return EmployeeSaveResult.Failure(new Dictionary<string, string[]>
            {
                [nameof(EmployeeInput.EmployeeNumber)] = [HRMS.Application.Common.Messages.Get("Business040")]
            });
        }
        catch (DataWriteException exception)
        {
            logger.LogError(exception, "Failed to update employee {EmployeeId}.", employeeId);
            throw;
        }
    }

    public async Task<bool> SoftDeleteAsync(
        int employeeId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(
                candidate => candidate.EmployeeId == employeeId && !candidate.IsDeleted,
                cancellationToken);

        if (employee is null)
        {
            return false;
        }

        var deletedAtUtc = DateTime.UtcNow;
        employee.SoftDelete(deletedAtUtc, actorUserId);
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(new AuditLogWriteRequest(
            "Employee.SoftDeleted",
            "Employee",
            employee.EmployeeId.ToString(),
            [
                new("IsDeleted", "false", "true"),
                new("DeletedAtUtc", null, deletedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                new("DeletedByUserId", null, actorUserId)
            ],
            ActorUserId: actorUserId), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<DeletedEmployeePage> GetDeletedAsync(
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default, bool showAll = false)
    {
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var query = dbContext.Employees
            .IncludingDeleted()
            .ForRead()
            .Where(employee => employee.IsDeleted);
        var totalCount = await query.CountAsync(cancellationToken);
        if (showAll) pageSize = Math.Max(1, totalCount);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var items = await query
            .OrderByDescending(employee => employee.DeletedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(employee => new DeletedEmployeeSummary(
                employee.EmployeeId,
                employee.EmployeeNumber,
                employee.FirstName + " " + employee.LastName,
                employee.Department!.Name,
                employee.DeletedAtUtc,
                employee.DeletedByUserId))
            .ToListAsync(cancellationToken);
        return new DeletedEmployeePage(items, totalCount, page, pageSize);
    }

    public async Task<bool> RestoreAsync(
        int employeeId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var employee = await dbContext.Employees
            .IncludingDeleted()
            .SingleOrDefaultAsync(candidate => candidate.EmployeeId == employeeId && candidate.IsDeleted, cancellationToken);
        if (employee is null)
        {
            return false;
        }

        var priorDeletedAt = employee.DeletedAtUtc;
        var priorDeletedBy = employee.DeletedByUserId;
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        employee.Restore();
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(new AuditLogWriteRequest(
            "Employee.Restored",
            "Employee",
            employee.EmployeeId.ToString(),
            [
                new("IsDeleted", "true", "false"),
                new("DeletedAtUtc", priorDeletedAt?.ToString("O", System.Globalization.CultureInfo.InvariantCulture), null),
                new("DeletedByUserId", priorDeletedBy, null)
            ],
            ActorUserId: actorUserId), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static List<AuditChange> GetEmployeeChanges(Employee employee, EmployeeInput input)
    {
        var changes = new List<AuditChange>();
        AddSensitiveChange(nameof(employee.EmployeeNumber), employee.EmployeeNumber, input.EmployeeNumber);
        AddSensitiveChange(nameof(employee.FirstName), employee.FirstName, input.FirstName);
        AddSensitiveChange(nameof(employee.LastName), employee.LastName, input.LastName);
        AddSensitiveChange(nameof(employee.WorkEmail), employee.WorkEmail, input.WorkEmail);
        AddSensitiveChange(nameof(employee.PhoneNumber), employee.PhoneNumber, input.PhoneNumber);
        Add(nameof(employee.HireDate), employee.HireDate, input.HireDate);
        Add(nameof(employee.EmploymentStatus), employee.EmploymentStatus, input.EmploymentStatus);
        Add(nameof(employee.EmploymentType), employee.EmploymentType, input.EmploymentType);
        Add(nameof(employee.DepartmentId), employee.DepartmentId, input.DepartmentId);
        Add(nameof(employee.PositionId), employee.PositionId, input.PositionId);
        Add(nameof(employee.ManagerEmployeeId), employee.ManagerEmployeeId, input.ManagerEmployeeId);
        AddSensitiveChange(nameof(employee.WorkLocation), employee.WorkLocation, input.WorkLocation);
        return changes;

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

        void AddSensitiveChange(string field, string? before, string? after)
        {
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                changes.Add(new AuditChange(field, null, null));
            }
        }
    }

    private async Task<Dictionary<string, string[]>> ValidateReferencesAsync(
        EmployeeInput input,
        int? currentEmployeeId,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (input.HireDate == default)
            errors[nameof(EmployeeInput.HireDate)] = [HRMS.Application.Common.Messages.Get("HireDateRequired")];
        if (string.IsNullOrWhiteSpace(input.EmployeeNumber) || input.EmployeeNumber.Trim().Length > 450)
            errors[nameof(EmployeeInput.EmployeeNumber)] = [HRMS.Application.Common.Messages.Get("EmployeeNumberRequired")];
        if (string.IsNullOrWhiteSpace(input.FirstName))
            errors[nameof(EmployeeInput.FirstName)] = [HRMS.Application.Common.Messages.Get("FirstNameRequired")];
        if (string.IsNullOrWhiteSpace(input.LastName))
            errors[nameof(EmployeeInput.LastName)] = [HRMS.Application.Common.Messages.Get("LastNameRequired")];
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(input.WorkEmail) || string.IsNullOrWhiteSpace(input.WorkEmail))
            errors[nameof(EmployeeInput.WorkEmail)] = [HRMS.Application.Common.Messages.Get("ValidEmailRequired")];
        if (string.IsNullOrWhiteSpace(input.EmploymentStatus))
            errors[nameof(EmployeeInput.EmploymentStatus)] = [HRMS.Application.Common.Messages.Get("EmploymentStatusRequired")];
        if (string.IsNullOrWhiteSpace(input.EmploymentType))
            errors[nameof(EmployeeInput.EmploymentType)] = [HRMS.Application.Common.Messages.Get("EmploymentTypeRequired")];

        var departmentIsActive = await dbContext.Departments
            .AnyAsync(
                department => department.DepartmentId == input.DepartmentId && department.IsActive,
                cancellationToken);
        if (!departmentIsActive)
        {
            errors[nameof(EmployeeInput.DepartmentId)] = [HRMS.Application.Common.Messages.Get("Business006")];
        }

        var position = await dbContext.Positions
            .ForRead()
            .Where(candidate => candidate.PositionId == input.PositionId && candidate.IsActive)
            .Select(candidate => new { candidate.DepartmentId })
            .SingleOrDefaultAsync(cancellationToken);

        if (position is null)
        {
            errors[nameof(EmployeeInput.PositionId)] = [HRMS.Application.Common.Messages.Get("Business009")];
        }
        else if (position.DepartmentId is int positionDepartmentId &&
                 positionDepartmentId != input.DepartmentId)
        {
            errors[nameof(EmployeeInput.PositionId)] = [HRMS.Application.Common.Messages.Get("Business010")];
        }

        if (input.ManagerEmployeeId is int managerEmployeeId)
        {
            if (managerEmployeeId == currentEmployeeId)
            {
                errors[nameof(EmployeeInput.ManagerEmployeeId)] = [HRMS.Application.Common.Messages.Get("Business052")];
            }
            else
            {
                var managerExists = await dbContext.Employees
                    .AnyAsync(
                        candidate => candidate.EmployeeId == managerEmployeeId && !candidate.IsDeleted &&
                            (candidate.EmploymentStatus == "Active" || candidate.EmploymentStatus == "نشط" || candidate.EmploymentStatus == "على رأس العمل"),
                        cancellationToken);
                if (!managerExists)
                {
                    errors[nameof(EmployeeInput.ManagerEmployeeId)] = [HRMS.Application.Common.Messages.Get("Business008")];
                }
            }
        }

        if (currentEmployeeId is int managingEmployee && await dbContext.Departments.AnyAsync(
            department => department.ManagerEmployeeId == managingEmployee && department.DepartmentId != input.DepartmentId, cancellationToken))
            errors[nameof(EmployeeInput.DepartmentId)] = [HRMS.Application.Common.Messages.Get("Extra002")];

        var numberAlreadyExists = await dbContext.Employees.IncludingDeleted()
            .AnyAsync(
                candidate => candidate.EmployeeNumber == input.EmployeeNumber &&
                             candidate.EmployeeId != currentEmployeeId,
                cancellationToken);
        if (numberAlreadyExists)
        {
            errors[nameof(EmployeeInput.EmployeeNumber)] = [HRMS.Application.Common.Messages.Get("Business040")];
        }

        return errors;
    }

    private static IQueryable<Employee> ApplySorting(
        IQueryable<Employee> employees,
        string? sortBy,
        bool descending)
    {
        return (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("name", false) => employees.OrderBy(employee => employee.FirstName).ThenBy(employee => employee.LastName).ThenBy(employee => employee.EmployeeNumber),
            ("name", true) => employees.OrderByDescending(employee => employee.FirstName).ThenByDescending(employee => employee.LastName).ThenBy(employee => employee.EmployeeNumber),
            ("email", false) => employees.OrderBy(employee => employee.WorkEmail).ThenBy(employee => employee.EmployeeNumber),
            ("email", true) => employees.OrderByDescending(employee => employee.WorkEmail).ThenBy(employee => employee.EmployeeNumber),
            ("department", false) => employees.OrderBy(employee => employee.Department!.Name).ThenBy(employee => employee.EmployeeNumber),
            ("department", true) => employees.OrderByDescending(employee => employee.Department!.Name).ThenBy(employee => employee.EmployeeNumber),
            ("hiredate", false) => employees.OrderBy(employee => employee.HireDate).ThenBy(employee => employee.EmployeeNumber),
            ("hiredate", true) => employees.OrderByDescending(employee => employee.HireDate).ThenBy(employee => employee.EmployeeNumber),
            ("status", false) => employees.OrderBy(employee => employee.EmploymentStatus).ThenBy(employee => employee.EmployeeNumber),
            ("status", true) => employees.OrderByDescending(employee => employee.EmploymentStatus).ThenBy(employee => employee.EmployeeNumber),
            ("number", true) => employees.OrderByDescending(employee => employee.EmployeeNumber),
            _ => employees.OrderBy(employee => employee.EmployeeNumber)
        };
    }

    private static bool IsEmployeeNumberConflict(DataWriteException exception) => exception.IsUnique && exception.ConflictCode == "EmployeeNumber";
}
