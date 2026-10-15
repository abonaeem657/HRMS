using HRMS.Infrastructure.Persistence;
using HRMS.Application.Employees;
using HRMS.Infrastructure.Auditing;
using HRMS.Domain.Employees;
using HRMS.Domain.Organization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
namespace HRMS.IntegrationTests;
public sealed class SqlFactAttribute : FactAttribute
{ public SqlFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HRMS_TEST_SQLSERVER")))Skip="SQL Server unavailable: set HRMS_TEST_SQLSERVER. This is an unverified acceptance gate.";} }
public partial class SqlWorkflowTests
{
    [SqlFact]
    public async Task ReportDropdownsRespectDepartmentScopeAndIncludeInactiveEmployees() => await WithDatabase(async db =>
    {
        var (departmentId, positionId) = await Organization(db);
        var otherDepartment = new Department("OTHER", "Other", true);
        db.Departments.Add(otherDepartment);
        await db.SaveChangesAsync();
        var otherPosition = new Position("OTHER", "Other", true, otherDepartment.DepartmentId);
        db.Positions.Add(otherPosition);
        await db.SaveChangesAsync();
        var visible = new Employee("R1", "Visible", "Employee", "r1@example.test", new(2026, 1, 1), "Inactive", departmentId, positionId, "FullTime");
        var hidden = new Employee("R2", "Other", "Employee", "r2@example.test", new(2026, 1, 1), "Active", otherDepartment.DepartmentId, otherPosition.PositionId, "FullTime");
        db.Employees.AddRange(visible, hidden);
        await db.SaveChangesAsync();
        var service = new HRMS.Infrastructure.Reporting.ReportingService(db, null!, null!);
        var scoped = await service.GetFilterOptionsAsync(departmentId);
        Assert.Equal(departmentId, Assert.Single(scoped.Departments).Id);
        Assert.Equal(positionId, Assert.Single(scoped.Positions).Id);
        Assert.Equal(visible.EmployeeId, Assert.Single(scoped.Employees).Id);
        Assert.Equal("Inactive", Assert.Single(scoped.EmploymentStatuses));
        var all = await service.GetFilterOptionsAsync();
        Assert.Equal(2, all.Employees.Count);
        await Service(db).SoftDeleteAsync(visible.EmployeeId, null);
        Assert.Empty((await service.GetFilterOptionsAsync(departmentId)).Employees);
    });

    private static async Task WithDatabase(Func<HRMSDbContext,Task> test)
    {
        var cs=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("HRMS_TEST_SQLSERVER")) {InitialCatalog="HRMS_Test_"+Guid.NewGuid().ToString("N")};
        await using var db=new HRMSDbContext(new DbContextOptionsBuilder<HRMSDbContext>().UseSqlServer(cs.ConnectionString).Options);
        try{await db.Database.MigrateAsync();await test(db);}finally{await db.Database.EnsureDeletedAsync();}
    }
    private static async Task<(int,int)> Organization(HRMSDbContext db)
    {var d=new Department("TEST","Test",true);db.Departments.Add(d);await db.SaveChangesAsync();var p=new Position("TEST","Test",true,d.DepartmentId);db.Positions.Add(p);await db.SaveChangesAsync();return(d.DepartmentId,p.PositionId);}
    private static EmployeeService Service(HRMSDbContext db)=>new(new ModuleData(db),new AuditLogService(db,new HttpContextAccessor()),NullLogger<EmployeeService>.Instance);
    [SqlFact] public async Task MigrationsCreateDatabase()=>await WithDatabase(async db=>Assert.Empty(await db.Database.GetPendingMigrationsAsync()));
    [SqlFact] public async Task EmployeeWorkflowPersistsAuditsAndRestores()=>await WithDatabase(async db=>
    {
        var(d,p)=await Organization(db);var service=Service(db);var input=new EmployeeInput("E1","Ahmad","Test","ahmad@example.test",new(2026,1,1),"Active","FullTime",d,p,null,null,null);
        var created=await service.CreateAsync(input);Assert.True(created.Succeeded);var id=created.EmployeeId!.Value;
        var details=(await service.GetByIdAsync(id))!;Assert.True((await service.UpdateAsync(id,input with{FirstName="Updated",RowVersion=details.RowVersion})).Succeeded);
        await Assert.ThrowsAsync<HRMS.Application.Common.DataConcurrencyException>(()=>service.UpdateAsync(id,input with{FirstName="Stale",RowVersion=details.RowVersion}));
        Assert.True(await service.SoftDeleteAsync(id,null));Assert.Null(await service.GetByIdAsync(id));Assert.Empty(await db.Employees.AsNoTracking().ToListAsync());
        Assert.False((await service.CreateAsync(input)).Succeeded);
        Assert.True(await service.RestoreAsync(id,null));Assert.NotNull(await service.GetByIdAsync(id));
        Assert.Contains(await db.AuditLogs.ToListAsync(),a=>a.Action=="Employee.Restored");
    });
    [SqlFact] public async Task PositionCodeUniqueInSql()=>await WithDatabase(async db=>{await Organization(db);db.Positions.Add(new Position("TEST","Duplicate",true));await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());});
    [SqlFact] public async Task InvalidEmployeeReferencesRejected()=>await WithDatabase(async db=>{var input=new EmployeeInput("E1","A","B","a@example.test",new(2026,1,1),"Active","FullTime",999,999,null,null,null);var result=await Service(db).CreateAsync(input);Assert.False(result.Succeeded);Assert.Contains("DepartmentId",result.Errors.Keys);Assert.Contains("PositionId",result.Errors.Keys);});
    [SqlFact] public async Task ActiveListExcludesInactive()=>await WithDatabase(async db=>{var(d,p)=await Organization(db);db.Employees.Add(new Employee("E2","A","B","a@example.test",new(2026,1,1),"Inactive",d,p,"FullTime"));await db.SaveChangesAsync();var service=Service(db);var active=await service.SearchAsync(new(null,null,null,null,false,1,25));Assert.Empty(active.Items);var all=await service.SearchAsync(new(null,null,EmployeeListQuery.AllStatuses,null,false,1,25));Assert.Single(all.Items);var inactive=await service.SearchAsync(new(null,null,"Inactive",null,false,1,25));Assert.Single(inactive.Items);});
}
