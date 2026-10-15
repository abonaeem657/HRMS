using HRMS.Application.Employees;
using System.Data;
using HRMS.Application.Auditing;
using HRMS.Application.Identity;
using HRMS.Domain.Employees;
using HRMS.Application.Common;
using HRMS.Application.Common.Queries;



namespace HRMS.Application.Identity;

public sealed class UserAccountService(
    IIdentityData dbContext,
    IIdentityStore userManager,
    IRoleStore roleManager,
    IEmployeeAccountLinks employeeLinks,
    IAuditLogService auditLogService) : IUserAccountService
{
    public Task<bool> AnyAccountsAsync(CancellationToken cancellationToken = default) =>
        userManager.Users.AnyAsync(cancellationToken);

    public async Task<UserAccountOperationResult> CreateFirstAdministratorAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (await userManager.Users.AnyAsync(cancellationToken))
        {
            return Failure(string.Empty, HRMS.Application.Common.Messages.Get("Business027"));
        }

        foreach (var role in IdentityRoles.All)
        {
            if (await roleManager.RoleExistsAsync(role.Name))
            {
                continue;
            }

            var createRoleResult = await roleManager.CreateAsync(role.Name);
            if (!createRoleResult.Succeeded)
            {
                return IdentityFailure(createRoleResult);
            }
        }

        var user = new LoginAccount
        {
            Email = email.Trim(),
            UserName = email.Trim(),
            IsActive = true
        };
        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return IdentityFailure(createResult);
        }

        var roleResult = await userManager.AddToRoleAsync(user, IdentityRoles.SystemAdministrator);
        if (!roleResult.Succeeded)
        {
            return IdentityFailure(roleResult);
        }

        await auditLogService.RecordAsync(new AuditLogWriteRequest(
            "UserAccount.InitialAdministratorCreated",
            "UserAccount",
            user.Id,
            [new("RoleName", null, IdentityRoles.SystemAdministrator)],
            ActorUserId: user.Id), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return UserAccountOperationResult.Success(user.Id);
    }

    public async Task<IReadOnlyList<UserAccountSummary>> GetAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await userManager.Users
            .ForRead()
            .OrderBy(user => user.Email)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.IsActive,
                Employee = dbContext.Employees
                    .Where(employee => employee.IdentityUserId == user.Id)
                    .Select(employee => new
                    {
                        employee.EmployeeId,
                        employee.EmployeeNumber,
                        Name = employee.FirstName + " " + employee.LastName
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var summaries = new List<UserAccountSummary>(rows.Count);
        foreach (var row in rows)
        {
            var user = await userManager.FindByIdAsync(row.Id);
            var roles = user is null
                ? Array.Empty<string>()
                : (await userManager.GetRolesAsync(user)).ToArray();
            summaries.Add(new UserAccountSummary(
                row.Id,
                row.Email ?? string.Empty,
                roles.FirstOrDefault() ?? string.Empty,
                row.IsActive,
                row.Employee?.EmployeeId,
                row.Employee?.EmployeeNumber,
                row.Employee?.Name));
        }

        return summaries;
    }

    public async Task<IReadOnlyList<EmployeeAccountOption>> GetAvailableEmployeesAsync(
        string? excludeUserId = null,
        int? includeEmployeeId = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Employees
            .ForRead()
            .Where(employee =>
                !employee.IsDeleted &&
                (employee.IdentityUserId == null ||
                 employee.IdentityUserId == excludeUserId ||
                 employee.EmployeeId == includeEmployeeId))
            .OrderBy(employee => employee.EmployeeNumber)
            .Select(employee => new EmployeeAccountOption(
                employee.EmployeeId,
                employee.EmployeeNumber,
                employee.FirstName + " " + employee.LastName))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserAccountOperationResult> CreateAsync(
        string email,
        string password,
        string roleName,
        int? employeeId,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAccountInputAsync(email, roleName, employeeId, null, cancellationToken);
        if (validation.Count > 0)
        {
            return UserAccountOperationResult.Failure(validation.ToArray());
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var user = new LoginAccount
        {
            Email = email.Trim(),
            UserName = email.Trim(),
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return IdentityFailure(createResult);
        }

        var roleResult = await userManager.AddToRoleAsync(user, roleName);
        if (!roleResult.Succeeded)
        {
            return IdentityFailure(roleResult);
        }

        await employeeLinks.SetAsync(user.Id, employeeId, cancellationToken);

        await auditLogService.RecordAsync(new AuditLogWriteRequest(
            "UserAccount.Created",
            "UserAccount",
            user.Id,
            [
                new("RoleName", null, roleName),
                new("IsActive", null, "true"),
                new("EmployeeId", null, employeeId?.ToString())
            ]), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return UserAccountOperationResult.Success(user.Id);
    }

    public async Task<UserAccountOperationResult> UpdateAsync(
        string userId,
        string email,
        string roleName,
        int? employeeId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Failure(string.Empty, HRMS.Application.Common.Messages.Get("Business057"));
        }

        var validation = await ValidateAccountInputAsync(
            email,
            roleName,
            employeeId,
            userId,
            cancellationToken);
        if (validation.Count > 0)
        {
            return UserAccountOperationResult.Failure(validation.ToArray());
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var identityChanged =
            !string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase) ||
            currentRoles.Count != 1 ||
            !currentRoles.Contains(roleName);
        var roleChanged = currentRoles.Count != 1 || !currentRoles.Contains(roleName);
        var emailChanged = !string.Equals(
            user.Email,
            email.Trim(),
            StringComparison.OrdinalIgnoreCase);
        var losesLastAdmin = user.IsActive &&
            currentRoles.Contains(IdentityRoles.SystemAdministrator) &&
            roleName != IdentityRoles.SystemAdministrator &&
            await IsLastActiveAdministratorAsync(user.Id, cancellationToken);
        if (losesLastAdmin)
        {
            return Failure(nameof(roleName), HRMS.Application.Common.Messages.Get("Business064"));
        }

        var currentEmployee = await dbContext.Employees
            .SingleOrDefaultAsync(employee => employee.IdentityUserId == user.Id, cancellationToken);
        Employee? selectedEmployee = employeeId is int selectedId
            ? await dbContext.Employees.SingleAsync(
                employee => employee.EmployeeId == selectedId,
                cancellationToken)
            : null;

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);

        user.Email = email.Trim();
        user.UserName = email.Trim();
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return IdentityFailure(updateResult);
        }

        if (!currentRoles.Contains(roleName))
        {
            var addRoleResult = await userManager.AddToRoleAsync(user, roleName);
            if (!addRoleResult.Succeeded)
            {
                return IdentityFailure(addRoleResult);
            }
        }

        var rolesToRemove = currentRoles.Where(role => role != roleName).ToArray();
        if (rolesToRemove.Length > 0)
        {
            var removeRolesResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeRolesResult.Succeeded)
            {
                return IdentityFailure(removeRolesResult);
            }
        }

        if (currentEmployee?.EmployeeId != selectedEmployee?.EmployeeId)
        {
            await employeeLinks.SetAsync(user.Id, selectedEmployee?.EmployeeId, cancellationToken);
        }

        if (identityChanged)
        {
            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                return IdentityFailure(stampResult);
            }
        }

        var changes = new List<AuditChange>();
        if (emailChanged)
        {
            changes.Add(new AuditChange(nameof(user.Email), null, null));
        }

        if (roleChanged)
        {
            changes.Add(new AuditChange("RoleName", currentRoles.FirstOrDefault(), roleName));
        }

        if (currentEmployee?.EmployeeId != selectedEmployee?.EmployeeId)
        {
            changes.Add(new AuditChange(
                "EmployeeId",
                currentEmployee?.EmployeeId.ToString(),
                selectedEmployee?.EmployeeId.ToString()));
        }

        if (changes.Count > 0)
        {
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                roleChanged ? "UserAccount.RoleChanged" : "UserAccount.Updated",
                "UserAccount",
                user.Id,
                changes), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return UserAccountOperationResult.Success(user.Id);
    }

    public async Task<UserAccountOperationResult> SetActiveAsync(
        string userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Failure(string.Empty, HRMS.Application.Common.Messages.Get("Business057"));
        }

        if (!isActive && user.IsActive &&
            await userManager.IsInRoleAsync(user, IdentityRoles.SystemAdministrator) &&
            await IsLastActiveAdministratorAsync(user.Id, cancellationToken))
        {
            return Failure(string.Empty, HRMS.Application.Common.Messages.Get("Business054"));
        }

        if (user.IsActive == isActive)
        {
            return UserAccountOperationResult.Success(user.Id);
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var wasActive = user.IsActive;
        user.IsActive = isActive;
        var result = await userManager.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
        {
            return IdentityFailure(result);
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return IdentityFailure(updateResult);
        }

        await auditLogService.RecordAsync(new AuditLogWriteRequest(
            isActive ? "UserAccount.Activated" : "UserAccount.Deactivated",
            "UserAccount",
            user.Id,
            [new("IsActive", wasActive.ToString(), isActive.ToString())]), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserAccountOperationResult.Success(user.Id);
    }

    public async Task<UserAccountOperationResult> ResetPasswordAsync(
        string userId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Failure(string.Empty, HRMS.Application.Common.Messages.Get("Business057"));
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return IdentityFailure(result);
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            return IdentityFailure(stampResult);
        }

        await auditLogService.RecordAsync(new AuditLogWriteRequest(
            "UserAccount.PasswordReset",
            "UserAccount",
            user.Id,
            []), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserAccountOperationResult.Success(user.Id);
    }

    public async Task<bool> CanSignInAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            return false;
        }

        return !await dbContext.Employees.IncludingDeleted()
            .AnyAsync(employee =>
                employee.IdentityUserId == userId &&
                (employee.IsDeleted || (employee.EmploymentStatus != "Active" && employee.EmploymentStatus != "نشط" && employee.EmploymentStatus != "على رأس العمل")),
                cancellationToken);
    }

    public Task<int?> GetLinkedEmployeeIdAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        dbContext.Employees
                .ForRead()
                .Where(employee => employee.IdentityUserId == userId && !employee.IsDeleted)
                .Select(employee => (int?)employee.EmployeeId)
                .SingleOrDefaultAsync(cancellationToken);

    private async Task<List<UserAccountError>> ValidateAccountInputAsync(
        string email,
        string roleName,
        int? employeeId,
        string? userId,
        CancellationToken cancellationToken)
    {
        var errors = new List<UserAccountError>();
        var existingUser = await userManager.FindByEmailAsync(email.Trim());
        if (existingUser is not null && existingUser.Id != userId)
        {
            errors.Add(new UserAccountError("Email", HRMS.Application.Common.Messages.Get("Business004")));
        }

        if (!IdentityRoles.All.Any(role => role.Name == roleName) ||
            !await roleManager.RoleExistsAsync(roleName))
        {
            errors.Add(new UserAccountError("RoleName", HRMS.Application.Common.Messages.Get("Business003")));
        }

        if (employeeId is int id)
        {
            var available = await dbContext.Employees
                .AnyAsync(employee =>
                    employee.EmployeeId == id &&
                    !employee.IsDeleted &&
                    (employee.IdentityUserId == null || employee.IdentityUserId == userId),
                    cancellationToken);
            if (!available)
            {
                errors.Add(new UserAccountError("EmployeeId", HRMS.Application.Common.Messages.Get("Business015")));
            }
        }

        return errors;
    }

    private async Task<bool> IsLastActiveAdministratorAsync(
        string excludedUserId,
        CancellationToken cancellationToken)
    {
        var activeAdminIds = await (
            from user in dbContext.Users
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where user.IsActive &&
                  user.Id != excludedUserId &&
                  role.Name == IdentityRoles.SystemAdministrator
            select user.Id)
            .Take(1)
            .ToListAsync(cancellationToken);
        return activeAdminIds.Count == 0;
    }

    private static UserAccountOperationResult IdentityFailure(IdentityStoreResult result)
    {
        var errors = result.Errors.Select(error => new UserAccountError(
            error.Code.Contains("Password", StringComparison.OrdinalIgnoreCase)
                ? "Password"
                : error.Code.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
                  error.Code.Contains("UserName", StringComparison.OrdinalIgnoreCase)
                    ? "Email"
                    : string.Empty,
            TranslateIdentityError(error.Code, error.Description))).ToArray();
        return UserAccountOperationResult.Failure(errors);
    }

    private static string TranslateIdentityError(string code, string description) =>
        code switch
        {
            "PasswordTooShort" => HRMS.Application.Common.Messages.Get("Business046"),
            "PasswordRequiresDigit" => HRMS.Application.Common.Messages.Get("Business049"),
            "PasswordRequiresLower" => HRMS.Application.Common.Messages.Get("Business047"),
            "PasswordRequiresUpper" => HRMS.Application.Common.Messages.Get("Business048"),
            "PasswordRequiresNonAlphanumeric" => HRMS.Application.Common.Messages.Get("Business050"),
            "DuplicateEmail" or "DuplicateUserName" => HRMS.Application.Common.Messages.Get("Business004"),
            _ => description
        };

    private static UserAccountOperationResult Failure(string key, string message) =>
        UserAccountOperationResult.Failure(new UserAccountError(key, message));
}
