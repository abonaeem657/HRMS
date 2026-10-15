using HRMS.Application.Common;
using HRMS.Domain.Employees;
using HRMS.Domain.Organization;
using HRMS.Domain.Attendance;
using HRMS.Domain.LeaveManagement;
using HRMS.Application.Identity;
namespace HRMS.Application.Identity;
public interface IIdentityData : IDataSession
{
    IQueryable<Employee> Employees { get; }
    IQueryable<AccountReference> Users { get; }
    IQueryable<AccountRoleReference> UserRoles { get; }
    IQueryable<RoleReference> Roles { get; }
}