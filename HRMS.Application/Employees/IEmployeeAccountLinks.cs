using HRMS.Application.Common;
using HRMS.Domain.Employees;
using HRMS.Domain.Organization;
using HRMS.Domain.Attendance;
using HRMS.Domain.LeaveManagement;
using HRMS.Application.Identity;
namespace HRMS.Application.Employees;
public interface IEmployeeAccountLinks
{
    Task SetAsync(string userId, int? employeeId, CancellationToken cancellationToken);
}