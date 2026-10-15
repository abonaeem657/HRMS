namespace HRMS.Application.LeaveManagement;

public interface ILeaveService
{
    Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(
        bool onlyActive = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveRequestDto>> GetLeaveRequestsAsync(
        LeaveFilter filter,
        CancellationToken cancellationToken = default);

    Task<LeaveRequestDto?> GetLeaveRequestByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<LeaveOperationResult> SubmitLeaveRequestAsync(
        CreateLeaveRequestCommand command,
        CancellationToken cancellationToken = default);

    Task<LeaveOperationResult> ApproveLeaveRequestAsync(
        int id,
        string reviewerUserId,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<LeaveOperationResult> RejectLeaveRequestAsync(
        int id,
        string reviewerUserId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<LeaveOperationResult> CancelLeaveRequestAsync(
        int id,
        string cancelledByUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveBalanceDto>> GetEmployeeLeaveBalancesAsync(
        int employeeId,
        int year,
        CancellationToken cancellationToken = default);

    Task<LeaveOperationResult> CreateLeaveTypeAsync(
        LeaveTypeInput input,
        CancellationToken cancellationToken = default);

    Task<LeaveOperationResult> UpdateLeaveTypeAsync(
        int id,
        LeaveTypeInput input,
        CancellationToken cancellationToken = default);
}
