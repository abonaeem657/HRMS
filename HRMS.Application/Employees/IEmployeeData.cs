using HRMS.Application.Common;
using HRMS.Domain.Employees;
using HRMS.Domain.Organization;
using HRMS.Domain.Attendance;
using HRMS.Domain.LeaveManagement;
using HRMS.Application.Identity;
namespace HRMS.Application.Employees;
public interface IEmployeeData : IDataSession
{
    IMutableRecords<Employee> Employees { get; }
    IQueryable<Department> Departments { get; }
    IQueryable<Position> Positions { get; }
    void ExpectVersion(Employee employee, byte[] version);
}