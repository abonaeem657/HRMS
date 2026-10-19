using HRMS.Domain.Employees;
using HRMS.Domain.Organization;
using HRMS.Domain.LeaveManagement;
using HRMS.Application.Identity;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace HRMS.Infrastructure.Persistence;
public static class DevelopmentSeed
{
    // Caller must explicitly select Development and --seed-demo. No automatic production seed.
    public static async Task RunAsync(IServiceProvider services, string password)
    {
        if(string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Set HRMS_DEMO_PASSWORD explicitly; no default credential is shipped.");
        var db=services.GetRequiredService<HRMSDbContext>();
        await db.Database.MigrateAsync();
        if(await db.Users.AnyAsync() || await db.Employees.IgnoreQueryFilters().AnyAsync())
            throw new InvalidOperationException("Demo seed requires an empty development database.");
        var users=services.GetRequiredService<UserManager<HRMSUser>>();
        var roles=services.GetRequiredService<RoleManager<IdentityRole>>();
        await using var transaction=await db.Database.BeginTransactionAsync();
        foreach(var role in IdentityRoles.All)
            if(!await roles.RoleExistsAsync(role.Name)) Check(await roles.CreateAsync(new IdentityRole(role.Name)));
        var department=new Department("DEMO","Demo department / قسم تجريبي",true);
        db.Departments.Add(department); await db.SaveChangesAsync();
        var position=new Position("DEMO","Demo position / وظيفة تجريبية",true,department.DepartmentId);
        db.Positions.Add(position);await db.SaveChangesAsync();
        var index=0;
        foreach(var role in IdentityRoles.All)
        {
            var email=role.Name.ToLowerInvariant()+"@example.test";
            var user=new HRMSUser { UserName=email,Email=email,EmailConfirmed=true,IsActive=true };
            Check(await users.CreateAsync(user,password));Check(await users.AddToRoleAsync(user,role.Name));
            var employee=new Employee("DEMO-"+(++index),role.Name,"Demo",email,new DateOnly(2026,1,1),"Active",department.DepartmentId,position.PositionId,"FullTime",identityUserId:user.Id);
            db.Employees.Add(employee);await db.SaveChangesAsync();
            if(role.Name==IdentityRoles.DepartmentManager) department.AssignManager(employee.EmployeeId);
        }
        foreach (var type in HRMS.Application.LeaveManagement.LeavePolicy.Defaults)
            db.LeaveTypes.Add(new LeaveType(type.Name, type.NameAr, type.DefaultDaysPerYear, type.IsPaid, type.IsActive));
        await db.SaveChangesAsync();await transaction.CommitAsync();
    }
    private static void Check(IdentityResult result)
    {if(!result.Succeeded)throw new InvalidOperationException(string.Join("; ",result.Errors.Select(e=>e.Description)));}
}
