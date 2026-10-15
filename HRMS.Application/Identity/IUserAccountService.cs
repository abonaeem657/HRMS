namespace HRMS.Application.Identity;

public interface IUserAccountService
{
    Task<bool> AnyAccountsAsync(CancellationToken cancellationToken = default);

    Task<UserAccountOperationResult> CreateFirstAdministratorAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserAccountSummary>> GetAccountsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeAccountOption>> GetAvailableEmployeesAsync(
        string? excludeUserId = null,
        int? includeEmployeeId = null,
        CancellationToken cancellationToken = default);

    Task<UserAccountOperationResult> CreateAsync(
        string email,
        string password,
        string roleName,
        int? employeeId,
        CancellationToken cancellationToken = default);

    Task<UserAccountOperationResult> UpdateAsync(
        string userId,
        string email,
        string roleName,
        int? employeeId,
        CancellationToken cancellationToken = default);

    Task<UserAccountOperationResult> SetActiveAsync(
        string userId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<UserAccountOperationResult> ResetPasswordAsync(
        string userId,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<bool> CanSignInAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<int?> GetLinkedEmployeeIdAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
