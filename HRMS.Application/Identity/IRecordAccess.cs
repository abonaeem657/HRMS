namespace HRMS.Application.Identity;
public interface IRecordAccess
{
    Task<bool> CanAccessEmployeeAsync(int employeeId, bool review, CancellationToken cancellationToken);
    string? UserId { get; }
}
public static class LeaveAccessPolicy
{
    public static bool Allows(bool authenticated, bool hr, bool manager, bool ownsRecord, bool managesDepartment, bool review)
        => authenticated && (hr || (!review && ownsRecord) || (manager && managesDepartment && !ownsRecord));
}
