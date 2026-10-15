using HRMS.Application.Common;
using HRMS.Application.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace HRMS.Infrastructure.Identity;

public sealed class IdentityStore(UserManager<HRMSUser> users, IIdentityData data) : IIdentityStore
{
    public IQueryable<AccountReference> Users => data.Users;
    private static IdentityStoreResult Result(IdentityResult result) => new(result.Succeeded, result.Errors.Select(e => new IdentityStoreError(e.Code, e.Description)).ToArray());
    private static LoginAccount? Map(HRMSUser? user) => user is null ? null : new() { Id = user.Id, UserName = user.UserName, Email = user.Email, IsActive = user.IsActive };
    private async Task<HRMSUser> Load(LoginAccount account)
    {
        var user = await users.FindByIdAsync(account.Id) ?? throw new InvalidOperationException("Account no longer exists.");
        user.Email = account.Email; user.UserName = account.UserName; user.IsActive = account.IsActive;
        return user;
    }
    public async Task<LoginAccount?> FindByIdAsync(string id) => Map(await users.FindByIdAsync(id));
    public async Task<LoginAccount?> FindByEmailAsync(string email) => Map(await users.FindByEmailAsync(email));
    public async Task<IdentityStoreResult> CreateAsync(LoginAccount account, string password)
    {
        var user = new HRMSUser { Email = account.Email, UserName = account.UserName, IsActive = account.IsActive };
        var result = await users.CreateAsync(user, password); account.Id = user.Id; return Result(result);
    }
    public async Task<IdentityStoreResult> UpdateAsync(LoginAccount a) => Result(await users.UpdateAsync(await Load(a)));
    public async Task<IdentityStoreResult> UpdateSecurityStampAsync(LoginAccount a) => Result(await users.UpdateSecurityStampAsync(await Load(a)));
    public async Task<IdentityStoreResult> AddToRoleAsync(LoginAccount a, string role) => Result(await users.AddToRoleAsync(await Load(a), role));
    public async Task<IdentityStoreResult> RemoveFromRolesAsync(LoginAccount a, IEnumerable<string> roles) => Result(await users.RemoveFromRolesAsync(await Load(a), roles));
    public async Task<IList<string>> GetRolesAsync(LoginAccount a) => await users.GetRolesAsync(await Load(a));
    public async Task<bool> IsInRoleAsync(LoginAccount a, string role) => await users.IsInRoleAsync(await Load(a), role);
    public async Task<string> GeneratePasswordResetTokenAsync(LoginAccount a) => await users.GeneratePasswordResetTokenAsync(await Load(a));
    public async Task<IdentityStoreResult> ResetPasswordAsync(LoginAccount a, string token, string password) => Result(await users.ResetPasswordAsync(await Load(a), token, password));
}
public sealed class RoleStore(RoleManager<IdentityRole> roles) : IRoleStore
{
    public Task<bool> RoleExistsAsync(string role) => roles.RoleExistsAsync(role);
    public async Task<IdentityStoreResult> CreateAsync(string role)
    {
        var result = await roles.CreateAsync(new IdentityRole(role));
        return new(result.Succeeded, result.Errors.Select(e => new IdentityStoreError(e.Code, e.Description)).ToArray());
    }
}
