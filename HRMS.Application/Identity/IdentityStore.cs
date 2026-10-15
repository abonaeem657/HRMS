using HRMS.Application.Common;
namespace HRMS.Application.Identity;

public sealed class LoginAccount
{
    public string Id { get; set; } = "";
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
}
public sealed record IdentityStoreError(string Code, string Description);
public sealed record IdentityStoreResult(bool Succeeded, IReadOnlyList<IdentityStoreError> Errors);
public interface IIdentityStore
{
    IQueryable<AccountReference> Users { get; }
    Task<LoginAccount?> FindByIdAsync(string id);
    Task<LoginAccount?> FindByEmailAsync(string email);
    Task<IdentityStoreResult> CreateAsync(LoginAccount account, string password);
    Task<IdentityStoreResult> UpdateAsync(LoginAccount account);
    Task<IdentityStoreResult> UpdateSecurityStampAsync(LoginAccount account);
    Task<IdentityStoreResult> AddToRoleAsync(LoginAccount account, string role);
    Task<IdentityStoreResult> RemoveFromRolesAsync(LoginAccount account, IEnumerable<string> roles);
    Task<IList<string>> GetRolesAsync(LoginAccount account);
    Task<bool> IsInRoleAsync(LoginAccount account, string role);
    Task<string> GeneratePasswordResetTokenAsync(LoginAccount account);
    Task<IdentityStoreResult> ResetPasswordAsync(LoginAccount account, string token, string password);
}
public interface IRoleStore
{
    Task<bool> RoleExistsAsync(string role);
    Task<IdentityStoreResult> CreateAsync(string role);
}
