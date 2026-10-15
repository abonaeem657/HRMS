using HRMS.Application.Auditing;
using HRMS.Application.Identity;
using HRMS.Domain.Employees;
using HRMS.Domain.Organization;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Infrastructure.Persistence;

/// <summary>Explicit local organization setup. Never creates attendance, leave requests or passwords.</summary>
public static class OrganizationDataSeed
{
    public static async Task RunAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<HRMSDbContext>();
        var users = services.GetRequiredService<UserManager<HRMSUser>>();
        var audit = services.GetRequiredService<IAuditLogService>();
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Apply migrations before adding organization data.");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var departments = await db.Departments.ToListAsync();
        foreach (var (code, name) in new[] { ("IT", "تقنية المعلومات"), ("HR", "الموارد البشرية"), ("FIN", "الشؤون المالية"), ("OPS", "الشؤون الإدارية") })
        {
            if (departments.Any(d => d.Code.Equals(code, StringComparison.OrdinalIgnoreCase))) continue;
            var department = new Department(code, name, true);
            db.Departments.Add(department);
            departments.Add(department);
        }
        await db.SaveChangesAsync();
        var positions = await db.Positions.ToListAsync();
        foreach (var d in departments.Where(d => d.IsActive))
        {
            var definitions = d.Code.ToUpperInvariant() switch
            {
                "IT" => new[] { ("IT-DEV", "مطور برمجيات"), ("IT-SUP", "مسؤول دعم فني"), ("IT-NET", "مهندس شبكات") },
                "HR" => new[] { ("HR-OFF", "مسؤول موارد بشرية"), ("HR-REC", "مسؤول توظيف"), ("HR-PAY", "مسؤول رواتب") },
                "FIN" => new[] { ("FIN-MGR", "مدير مالي"), ("FIN-ACC", "محاسب"), ("FIN-AUD", "مدقق داخلي"), ("FIN-ANA", "محلل مالي") },
                "OPS" => new[] { ("OPS-MGR", "مدير إداري"), ("OPS-ADM", "مسؤول إداري"), ("OPS-COORD", "منسق عمليات"), ("OPS-SYS", "مسؤول نظم") },
                _ => new[] { (d.Code + "-STAFF", "موظف " + d.Name) }
            };
            foreach (var (code, title) in definitions)
            {
                if (positions.Any(p => p.Code.Equals(code, StringComparison.OrdinalIgnoreCase))) continue;
                var position = new Position(code, title, true, d.DepartmentId);
                db.Positions.Add(position);
                positions.Add(position);
            }
        }
        await db.SaveChangesAsync();
        var employees = await db.Employees.ToListAsync();
        var sequence = 0;
        var allNumbers = (await db.Employees.IgnoreQueryFilters().Select(e => e.EmployeeNumber).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allEmails = (await db.Employees.IgnoreQueryFilters().Select(e => e.WorkEmail).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] firstNames = ["أحمد", "سارة", "عمر", "نور", "خالد", "لينا", "يوسف", "ريم", "محمود", "هبة", "رامي", "دانا"];
        string[] lastNames = ["العمري", "الخطيب", "الحداد", "النجار", "الزعبي", "العلي"];
        async Task<Employee> AddEmployee(Department department, Position position, string? email = null)
        {
            string number;
            do { number = "ORG-" + (++sequence).ToString("D3"); } while (!allNumbers.Add(number));
            var workEmail = !string.IsNullOrWhiteSpace(email) && !allEmails.Contains(email) ? email : number.ToLowerInvariant() + "@example.test";
            while (!allEmails.Add(workEmail)) workEmail = Guid.NewGuid().ToString("N") + "@example.test";
            var employee = new Employee(number, firstNames[(sequence - 1) % firstNames.Length], lastNames[(sequence - 1) % lastNames.Length],
                workEmail, new DateOnly(2026, 1, 1), "Active", department.DepartmentId, position.PositionId, "FullTime", workLocation: "عمّان");
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            employees.Add(employee);
            await audit.RecordAsync(new("Employee.Created", "Employee", employee.EmployeeId.ToString(),
                [new("EmployeeNumber", null, number), new("Source", null, "OrganizationDataSeed")]));
            return employee;
        }

        foreach (var user in await db.Users.OrderBy(u => u.Email).ToListAsync())
        {
            var linked = employees.SingleOrDefault(e => e.IdentityUserId == user.Id);
            if (linked != null) continue;
            // Never reassign a user linked to a deleted employee.
            if (await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.IdentityUserId == user.Id)) continue;
            var userRoles = await users.GetRolesAsync(user);
            var role = IdentityRoles.All.Select(r => r.Name).FirstOrDefault(userRoles.Contains) ?? IdentityRoles.Employee;
            var code = role switch { IdentityRoles.SystemAdministrator => "OPS", IdentityRoles.Auditor => "FIN", IdentityRoles.HRManager or IdentityRoles.HROfficer => "HR", _ => "IT" };
            var department = departments.Single(d => d.Code.ToUpper() == code && d.IsActive);
            var preferredCode = role switch { IdentityRoles.SystemAdministrator => "OPS-SYS", IdentityRoles.Auditor => "FIN-AUD", IdentityRoles.HROfficer => "HR-OFF", _ => null };
            var position = positions.FirstOrDefault(p => p.DepartmentId == department.DepartmentId && p.IsActive && p.Code == preferredCode)
                ?? positions.First(p => p.DepartmentId == department.DepartmentId && p.IsActive);
            var employee = employees.FirstOrDefault(e => e.IdentityUserId == null && e.EmploymentStatus == "Active" &&
                (e.WorkEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase) || e.PositionId == position.PositionId))
                ?? await AddEmployee(department, position, user.Email);
            employee.LinkIdentityUser(user.Id);
            if (role == IdentityRoles.HRManager && !department.ManagerEmployeeId.HasValue)
                department.AssignManager(employee.EmployeeId);
            if (role == IdentityRoles.DepartmentManager)
            {
                var managedDepartment = departments.Single(d => d.DepartmentId == employee.DepartmentId);
                if (managedDepartment.ManagerEmployeeId.HasValue && managedDepartment.ManagerEmployeeId != employee.EmployeeId)
                    throw new InvalidOperationException("A department already has another manager; resolve its assignment in Departments.");
                managedDepartment.AssignManager(employee.EmployeeId);
            }
            var stampResult = await users.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
                throw new InvalidOperationException("Cannot refresh account security: " + string.Join("; ", stampResult.Errors.Select(e => e.Description)));
            await db.SaveChangesAsync();
            await audit.RecordAsync(new("UserAccount.EmployeeLinked", "Employee", employee.EmployeeId.ToString(), [new("IdentityUserId", null, user.Id)]));
        }

        // Ensure every active title has at least one employee, including titles added earlier.
        foreach (var position in positions.Where(p => p.IsActive))
        {
            if (employees.Any(e => e.PositionId == position.PositionId)) continue;
            var department = departments.FirstOrDefault(d => d.IsActive && (!position.DepartmentId.HasValue || d.DepartmentId == position.DepartmentId));
            if (department != null) await AddEmployee(department, position);
        }
        var activeDepartments = departments.Where(d => d.IsActive).ToList();
        var target = Math.Max(6, activeDepartments.Max(d => employees.Count(e => e.DepartmentId == d.DepartmentId)));
        foreach (var department in activeDepartments)
        {
            var options = positions.Where(p => p.IsActive && p.DepartmentId == department.DepartmentId).ToList();
            while (employees.Count(e => e.DepartmentId == department.DepartmentId) < target)
                await AddEmployee(department, options[employees.Count(e => e.DepartmentId == department.DepartmentId) % options.Count]);
            if (!department.ManagerEmployeeId.HasValue)
            {
                var manager = employees.First(e => e.DepartmentId == department.DepartmentId && e.EmploymentStatus == "Active");
                department.AssignManager(manager.EmployeeId);
            }
        }
        await db.SaveChangesAsync();
        await audit.RecordAsync(new("Organization.StarterDataAdded", "Organization", null,
            [new("EmployeesPerActiveDepartment", null, target.ToString())]));
        await transaction.CommitAsync();
        Console.WriteLine($"Organization data saved: {activeDepartments.Count} active departments, {target} employees per department. Existing users linked; no attendance or leave requests created.");
    }
}
