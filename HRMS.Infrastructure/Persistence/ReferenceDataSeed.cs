using HRMS.Application.LeaveManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Infrastructure.Persistence;

/// <summary>Explicit development starter values, editable through leave type management.</summary>
public static class ReferenceDataSeed
{
    public static IReadOnlyList<LeaveTypeInput> LeaveTypes => LeavePolicy.Defaults;

    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<HRMSDbContext>();
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Apply the pending migrations before adding reference data.");
        var leaveService = services.GetRequiredService<ILeaveService>();
        var audit = services.GetRequiredService<HRMS.Application.Auditing.IAuditLogService>();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            foreach (var type in await db.LeaveTypes.Where(t => !LeavePolicy.SupportedNames.Contains(t.Name) && t.IsActive).ToListAsync())
            {
                type.Update(type.Name, type.NameAr, type.DefaultDaysPerYear, type.IsPaid, false);
                await audit.RecordAsync(new("LeaveType.Deactivated", "LeaveType", type.LeaveTypeId.ToString(), [new("IsActive", "true", "false")]));
            }
            // Commit legacy deactivation before the application service's own creation transactions.
            await transaction.CommitAsync();
        }
        var created = 0;
        foreach (var input in LeaveTypes)
        {
            // Preserve existing definitions, including their allowance and active status.
            if (await db.LeaveTypes.AnyAsync(type => type.Name == input.Name || type.NameAr == input.NameAr))
                continue;
            var result = await leaveService.CreateLeaveTypeAsync(input);
            if (!result.Succeeded)
                throw new InvalidOperationException("Cannot create leave type " + input.Name + ": " + string.Join("; ", result.Errors));
            created++;
        }
        return created;
    }
}
