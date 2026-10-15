namespace HRMS.Application.Identity;

public sealed record UserAccountSummary(
    string UserId,
    string Email,
    string RoleName,
    bool IsActive,
    int? EmployeeId,
    string? EmployeeNumber,
    string? EmployeeName);

public sealed record EmployeeAccountOption(
    int EmployeeId,
    string EmployeeNumber,
    string FullName);

public sealed record UserAccountError(string Key, string Message);

public sealed record UserAccountOperationResult(
    bool Succeeded,
    string? UserId,
    IReadOnlyList<UserAccountError> Errors)
{
    public static UserAccountOperationResult Success(string? userId = null) =>
        new(true, userId, Array.Empty<UserAccountError>());

    public static UserAccountOperationResult Failure(params UserAccountError[] errors) =>
        new(false, null, errors);
}
