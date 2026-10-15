using HRMS.Application.Attendance;
using HRMS.Application.LeaveManagement;
using HRMS.Application.Reporting;

namespace HRMS.Models.Home;

public sealed class HomeDashboardViewModel
{
    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string RoleName { get; init; } = string.Empty;

    public bool CanViewHrData { get; init; }

    public bool CanManageEmployees { get; init; }

    public bool CanManageOrganization { get; init; }

    public bool CanManageUsers { get; init; }

    public bool CanManageAttendance { get; init; }

    public bool CanUseAttendance { get; init; }

    public bool CanUseLeaves { get; init; }

    public bool CanApproveLeaves { get; init; }

    public bool CanManageLeaveTypes { get; init; }

    public bool CanViewReports { get; init; }

    public bool CanViewAudit { get; init; }

    public bool IsDepartmentManager { get; init; }

    public bool HasLinkedEmployee { get; init; }

    public int? EmployeeId { get; init; }

    public int? DepartmentId { get; init; }

    public string? DepartmentName { get; init; }

    public DateOnly Today { get; init; }

    public DateOnly FromDate { get; init; }

    public DateOnly ToDate { get; init; }

    public int DepartmentCount { get; init; }

    public int PositionCount { get; init; }

    public int EmployeeCount { get; init; }

    public int UserCount { get; init; }

    public DashboardSummary? Summary { get; init; }

    public AttendanceRecordDto? TodayAttendance { get; init; }

    public AttendanceSummaryDto? PersonalAttendance { get; init; }

    public IReadOnlyList<LeaveRequestDto> MyLeaveRequests { get; init; } =
        Array.Empty<LeaveRequestDto>();

    public int MyPendingLeaveCount => MyLeaveRequests.Count(request => request.Status == "Pending");
}
