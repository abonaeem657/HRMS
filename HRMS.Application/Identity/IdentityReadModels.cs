namespace HRMS.Application.Identity;
public sealed class AccountReference { public string Id { get; init; } = ""; public string? UserName { get; init; } public string? Email { get; init; } public bool IsActive { get; init; } }
public sealed class AccountRoleReference { public string UserId { get; init; } = ""; public string RoleId { get; init; } = ""; }
public sealed class RoleReference { public string Id { get; init; } = ""; public string? Name { get; init; } }


