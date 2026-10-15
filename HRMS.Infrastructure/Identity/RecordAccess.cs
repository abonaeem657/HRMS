using System.Security.Claims;
using HRMS.Application.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
namespace HRMS.Infrastructure.Identity;
public sealed class RecordAccess(HRMSDbContext db, IHttpContextAccessor accessor) : IRecordAccess
{
    public string? UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    public async Task<bool> CanAccessEmployeeAsync(int employeeId, bool review, CancellationToken cancellationToken)
    {
        var user = accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true || UserId is null) return false;
        var employee = await db.Employees.AsNoTracking().Where(e => e.EmployeeId == employeeId)
            .Select(e => new { e.IdentityUserId, e.DepartmentId }).SingleOrDefaultAsync(cancellationToken);
        if (employee is null) return false;
        var manages = await db.Departments.AnyAsync(d => d.DepartmentId == employee.DepartmentId && d.IsActive &&
            d.Manager != null && d.Manager.IdentityUserId == UserId && !d.Manager.IsDeleted, cancellationToken);
        return LeaveAccessPolicy.Allows(true, IdentityRoles.ManageEmployees.Split(',').Any(user.IsInRole),
            user.IsInRole(IdentityRoles.DepartmentManager), employee.IdentityUserId == UserId, manages, review);
    }
}
