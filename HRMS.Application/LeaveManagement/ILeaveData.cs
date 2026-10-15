using HRMS.Application.Common;
using HRMS.Domain.Employees;
using HRMS.Domain.Organization;
using HRMS.Domain.Attendance;
using HRMS.Domain.LeaveManagement;
using HRMS.Application.Identity;
namespace HRMS.Application.LeaveManagement;
public interface ILeaveData : IDataSession
{
    IMutableRecords<LeaveRequest> LeaveRequests { get; }
    IMutableRecords<LeaveType> LeaveTypes { get; }
    IQueryable<Employee> Employees { get; }
    IQueryable<AccountReference> Users { get; }
}