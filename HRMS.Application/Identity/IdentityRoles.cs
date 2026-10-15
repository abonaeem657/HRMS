using System.Security.Claims;
using System.Security.Principal;

namespace HRMS.Application.Identity;

public static class IdentityRoles
{
    public const string SystemAdministrator = "SystemAdministrator";
    public const string HRManager = "HRManager";
    public const string HROfficer = "HROfficer";
    public const string DepartmentManager = "DepartmentManager";
    public const string Employee = "Employee";
    public const string Auditor = "Auditor";

    public const string ReadHrData =
        SystemAdministrator + "," + HRManager + "," + HROfficer;

    public const string ManageEmployees =
        SystemAdministrator + "," + HRManager + "," + HROfficer;

    public const string ManageOrganization =
        SystemAdministrator + "," + HRManager;

    public const string ViewAuditLogs =
        SystemAdministrator + "," + HRManager + "," + Auditor;

    public const string ManageAttendance =
        SystemAdministrator + "," + HRManager + "," + HROfficer;

    public const string UseAttendance =
        ManageAttendance + "," + DepartmentManager + "," + Employee;

    public const string ManageLeaves =
        SystemAdministrator + "," + HRManager + "," + HROfficer + "," + DepartmentManager;

    public const string UseLeaves =
        SystemAdministrator + "," + HRManager + "," + HROfficer + "," + DepartmentManager + "," + Employee;

    public const string ManageLeaveTypes = SystemAdministrator + "," + HRManager;

    public const string ViewReports =
        SystemAdministrator + "," + HRManager + "," + HROfficer + "," + DepartmentManager + "," + Auditor;

    public static IReadOnlyList<IdentityRoleOption> All { get; } =
    [
        new(SystemAdministrator, "Role.SystemAdministrator"),
        new(HRManager, "Role.HRManager"),
        new(HROfficer, "Role.HROfficer"),
        new(DepartmentManager, "Role.DepartmentManager"),
        new(Employee, "Role.Employee"),
        new(Auditor, "Role.Auditor")
    ];
}

public sealed record IdentityRoleOption(string Name, string DisplayName);

/// <summary>
/// Extension methods to check comma-separated role strings in Razor views.
/// Use <c>User.IsInRoles(IdentityRoles.ManageOrganization)</c> instead of
/// <c>User.IsInRole(IdentityRoles.ManageOrganization)</c> to correctly handle
/// compound role strings like "SystemAdministrator,HRManager".
/// </summary>
public static class ClaimsPrincipalRoleExtensions
{
    /// <summary>
    /// Returns true if the principal belongs to ANY of the roles in the
    /// comma-separated <paramref name="roles"/> string.
    /// </summary>
    public static bool IsInRoles(this IPrincipal principal, string roles)
    {
        if (string.IsNullOrWhiteSpace(roles))
            return false;

        foreach (var role in roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (principal.IsInRole(role))
                return true;
        }
        return false;
    }
}
