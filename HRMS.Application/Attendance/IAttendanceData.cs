using HRMS.Application.Common;
using HRMS.Domain.Employees;
using HRMS.Domain.Organization;
using HRMS.Domain.Attendance;
using HRMS.Domain.LeaveManagement;
using HRMS.Application.Identity;
namespace HRMS.Application.Attendance;
public interface IAttendanceData : IDataSession
{
    IMutableRecords<AttendanceRecord> AttendanceRecords { get; }
    IQueryable<Employee> Employees { get; }
    void ExpectVersion(AttendanceRecord record, byte[] version);
}
