using HRMS.Application.Auditing;
using HRMS.Application.Identity;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRMS;

/// <summary>Explicit local maintenance commands. Never runs during a normal web startup.</summary>
internal static class DevelopmentAdminCommands
{
    public static async Task RunAsync(
        string command,
        IServiceProvider services,
        IConfiguration configuration)
    {
        var email = configuration["LocalAdmin:Email"]?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException(
                "Set LocalAdmin:Email in Development user secrets before running an administrator command.");

        var db = services.GetRequiredService<HRMSDbContext>();
        var connection = db.Database.GetDbConnection();
        Console.WriteLine($"Environment: Development; database: {connection.DataSource}/{connection.Database}");
        try
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
            if (pending.Length > 0)
                throw new InvalidOperationException(
                    "The configured database has pending migrations. Apply the reviewed migrations to this same database first: " +
                    string.Join(", ", pending));
        }
        catch (Exception error) when (error is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Cannot inspect the configured HRMS database. Check the Development environment, LocalDB availability, and ConnectionStrings:HRMS.",
                error);
        }

        var users = services.GetRequiredService<UserManager<HRMSUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var audit = services.GetRequiredService<IAuditLogService>();
        var accounts = services.GetRequiredService<IUserAccountService>();
        var user = await users.FindByEmailAsync(email);

        if (command == "--admin-status")
        {
            await PrintStatusAsync(user, users, roles, accounts, db);
            return;
        }

        if (command == "--admin-check-password")
        {
            var password = RequirePassword(configuration);
            Console.WriteLine(user is null
                ? "Account does not exist."
                : await users.CheckPasswordAsync(user, password)
                    ? "Configured password matches the stored Identity hash."
                    : "Configured password does not match the stored Identity hash.");
            await PrintStatusAsync(user, users, roles, accounts, db);
            return;
        }

        if (command == "--admin-create")
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await EnsureRoleAsync(roles);
            if (user is null)
            {
                var password = RequirePassword(configuration);
                user = new HRMSUser { Email = email, UserName = email, IsActive = true };
                Check(await users.CreateAsync(user, password), "Create administrator");
                Check(await users.AddToRoleAsync(user, IdentityRoles.SystemAdministrator), "Assign administrator role");
                await audit.RecordAsync(new AuditLogWriteRequest(
                    "UserAccount.LocalAdministratorCreated", "UserAccount", user.Id,
                    [new("RoleName", null, IdentityRoles.SystemAdministrator)]));
                Console.WriteLine("Administrator account created.");
            }
            else
            {
                if (!await users.IsInRoleAsync(user, IdentityRoles.SystemAdministrator))
                {
                    Check(await users.AddToRoleAsync(user, IdentityRoles.SystemAdministrator),
                        "Assign administrator role");
                    await audit.RecordAsync(new AuditLogWriteRequest(
                        "UserAccount.LocalAdministratorRoleAssigned", "UserAccount", user.Id,
                        [new("RoleName", null, IdentityRoles.SystemAdministrator)]));
                }
                Console.WriteLine("Account already exists; its password and active status were not changed.");
            }
            await transaction.CommitAsync();
            await PrintStatusAsync(user, users, roles, accounts, db);
            return;
        }

        if (user is null)
            throw new InvalidOperationException(
                "The configured account does not exist. Run --admin-create first.");

        if (command == "--admin-reset-password")
        {
            var password = RequirePassword(configuration);
            await using var transaction = await db.Database.BeginTransactionAsync();
            var token = await users.GeneratePasswordResetTokenAsync(user);
            Check(await users.ResetPasswordAsync(user, token, password), "Reset administrator password");
            await audit.RecordAsync(new AuditLogWriteRequest(
                "UserAccount.LocalAdministratorPasswordReset", "UserAccount", user.Id, []));
            await transaction.CommitAsync();
            Console.WriteLine("Password reset completed. Any account lockout or inactivity remains in effect.");
        }
        else if (command == "--admin-unlock")
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            Check(await users.SetLockoutEndDateAsync(user, null), "Clear lockout");
            Check(await users.ResetAccessFailedCountAsync(user), "Clear failed attempt count");
            await audit.RecordAsync(new AuditLogWriteRequest(
                "UserAccount.LocalAdministratorUnlocked", "UserAccount", user.Id, []));
            await transaction.CommitAsync();
            Console.WriteLine("Account lockout cleared.");
        }
        else if (command == "--admin-activate")
        {
            var blockedEmployee = await db.Employees.IgnoreQueryFilters().AnyAsync(employee =>
                employee.IdentityUserId == user.Id &&
                (employee.IsDeleted ||
                 (employee.EmploymentStatus != "Active" &&
                  employee.EmploymentStatus != "نشط" &&
                  employee.EmploymentStatus != "على رأس العمل")));
            if (blockedEmployee)
                throw new InvalidOperationException(
                    "The linked employee is deleted or inactive. Restore that employee through the authorized HR workflow before activating this account.");

            if (!user.IsActive)
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                user.IsActive = true;
                Check(await users.UpdateAsync(user), "Activate administrator");
                await audit.RecordAsync(new AuditLogWriteRequest(
                    "UserAccount.LocalAdministratorActivated", "UserAccount", user.Id,
                    [new("IsActive", "false", "true")]));
                await transaction.CommitAsync();
            }
            Console.WriteLine("Account is active.");
        }

        await PrintStatusAsync(user, users, roles, accounts, db);
    }

    private static async Task PrintStatusAsync(
        HRMSUser? user,
        UserManager<HRMSUser> users,
        RoleManager<IdentityRole> roles,
        IUserAccountService accounts,
        HRMSDbContext db)
    {
        Console.WriteLine($"SystemAdministrator role exists: {await roles.RoleExistsAsync(IdentityRoles.SystemAdministrator)}");
        if (user is null)
        {
            Console.WriteLine("Configured account does not exist.");
            return;
        }

        Console.WriteLine($"Account exists: true; active: {user.IsActive}; has password: {await users.HasPasswordAsync(user)}");
        Console.WriteLine($"Administrator role assigned: {await users.IsInRoleAsync(user, IdentityRoles.SystemAdministrator)}");
        Console.WriteLine($"Account and linked employee permit sign-in: {await accounts.CanSignInAsync(user.Id)}");
        Console.WriteLine(
            $"Lockout enabled: {await users.GetLockoutEnabledAsync(user)}; locked out: {await users.IsLockedOutAsync(user)}; " +
            $"lockout ends (UTC): {(await users.GetLockoutEndDateAsync(user))?.UtcDateTime.ToString("O") ?? "none"}; " +
            $"failed attempts: {await users.GetAccessFailedCountAsync(user)}");
        var linkedEmployees = await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(employee => employee.IdentityUserId == user.Id)
            .Select(employee => new { employee.IsDeleted, employee.EmploymentStatus })
            .ToListAsync();
        Console.WriteLine(linkedEmployees.Count == 0
            ? "Linked employee: none."
            : string.Join("; ", linkedEmployees.Select(employee =>
                $"Linked employee deleted: {employee.IsDeleted}; employment status: {employee.EmploymentStatus}")));
        Console.WriteLine("A deleted or inactive linked employee prevents sign-in even with the correct password.");
    }

    private static string RequirePassword(IConfiguration configuration)
    {
        var password = configuration["LocalAdmin:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Set LocalAdmin:Password in Development user secrets before this command.");
        return password;
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roles)
    {
        foreach (var role in IdentityRoles.All)
        {
            if (!await roles.RoleExistsAsync(role.Name))
                Check(await roles.CreateAsync(new IdentityRole(role.Name)),
                    $"Create application role {role.Name}");
        }
    }

    private static void Check(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"{operation} failed: " +
                string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}")));
    }
}
