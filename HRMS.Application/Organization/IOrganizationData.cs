using HRMS.Application.Common;
using HRMS.Domain.Employees;
using HRMS.Domain.Organization;
using HRMS.Domain.Attendance;
using HRMS.Domain.LeaveManagement;
using HRMS.Application.Identity;
namespace HRMS.Application.Organization;
public interface IOrganizationData : IDataSession
{
    IMutableRecords<Department> Departments { get; }
    IMutableRecords<Position> Positions { get; }
    IQueryable<Employee> Employees { get; }
}