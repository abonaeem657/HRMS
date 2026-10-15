using HRMS.Domain.Employees;
using HRMS.Application.Employees;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using HRMS.Controllers;
namespace HRMS.ArchitectureTests;
public class BoundaryTests
{
    [Fact]
    public void BusinessOrchestrationLivesInApplicationAndAcceptsOnlyInnerPorts()
    {
        foreach (var type in new[] { typeof(HRMS.Application.Employees.EmployeeService), typeof(HRMS.Application.Organization.DepartmentService),
            typeof(HRMS.Application.Organization.PositionService), typeof(HRMS.Application.Attendance.AttendanceService),
            typeof(HRMS.Application.LeaveManagement.LeaveService), typeof(HRMS.Application.Identity.UserAccountService) })
        {
            Assert.Equal(typeof(IEmployeeService).Assembly, type.Assembly);
            Assert.DoesNotContain(type.GetConstructors().SelectMany(c => c.GetParameters()), p =>
                p.ParameterType.Assembly.GetName().Name == "HRMS.Infrastructure" || typeof(DbContext).IsAssignableFrom(p.ParameterType));
        }
    }
    [Fact]
    public void ForeignModuleRecordsHaveReadOnlyQueryPorts()
    {
        Assert.Equal(typeof(IQueryable<Employee>), typeof(HRMS.Application.Attendance.IAttendanceData).GetProperty("Employees")!.PropertyType);
        Assert.Equal(typeof(IQueryable<Employee>), typeof(HRMS.Application.LeaveManagement.ILeaveData).GetProperty("Employees")!.PropertyType);
        Assert.Equal(typeof(IQueryable<Employee>), typeof(HRMS.Application.Identity.IIdentityData).GetProperty("Employees")!.PropertyType);
        Assert.Equal(typeof(IQueryable<HRMS.Domain.Organization.Department>), typeof(HRMS.Application.Employees.IEmployeeData).GetProperty("Departments")!.PropertyType);
    }
    [Fact] public void DomainHasNoOuterLayerDependency() => Assert.DoesNotContain(typeof(Employee).Assembly.GetReferencedAssemblies(),a=>a.Name!.StartsWith("Microsoft.EntityFrameworkCore")||a.Name is "HRMS.Infrastructure" or "HRMS.Application" or "HRMS");
    [Fact] public void ApplicationHasNoPersistenceOrPresentationDependency()=>Assert.DoesNotContain(typeof(IEmployeeService).Assembly.GetReferencedAssemblies(),a=>a.Name is "HRMS.Infrastructure" or "HRMS" ||a.Name!.StartsWith("Microsoft.EntityFrameworkCore"));
    [Fact] public void ControllersDoNotDependOnDbContext()
    { foreach(var type in typeof(EmployeesController).Assembly.GetTypes().Where(t=>t.Name.EndsWith("Controller")))foreach(var constructor in type.GetConstructors())Assert.DoesNotContain(constructor.GetParameters(),p=>typeof(DbContext).IsAssignableFrom(p.ParameterType)); }
    [Fact] public void MigrationSnapshotMatchesModel()
    { using var db=new HRMSDbContext(new DbContextOptionsBuilder<HRMSDbContext>().UseSqlServer("Server=localhost;Database=HRMS_ModelCheck;Integrated Security=True;TrustServerCertificate=True").Options);Assert.False(db.Database.HasPendingModelChanges()); }
    [Fact] public void EmployeeAndAttendanceHaveConcurrencyAndSoftDeleteFilters()
    { using var db=new HRMSDbContext(new DbContextOptionsBuilder<HRMSDbContext>().UseSqlServer("Server=localhost;Database=HRMS_ModelCheck;Integrated Security=True;TrustServerCertificate=True").Options);
      foreach(var name in new[]{typeof(Employee),typeof(HRMS.Domain.Attendance.AttendanceRecord)}) {var entity=db.Model.FindEntityType(name)!;Assert.True(entity.FindProperty("RowVersion")!.IsConcurrencyToken);Assert.NotNull(entity.GetQueryFilter());} }
}
