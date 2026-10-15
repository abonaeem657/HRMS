using HRMS.Application.LeaveManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Infrastructure.Persistence;

/// <summary>Explicit development starter values, editable through leave type management.</summary>
public static class ReferenceDataSeed
{
    public static IReadOnlyList<LeaveTypeInput> LeaveTypes { get; } =
    [
        new("Annual", "سنوية", 14, true, true),
        new("Sick", "مرضية", 14, true, true),
        new("Emergency", "طارئة", 3, true, true),
        new("Bereavement", "وفاة", 3, true, true),
        new("Study", "دراسة", 5, false, true),
        new("Unpaid", "بدون راتب", 0, false, true)
    ];

    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<HRMSDbContext>();
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Apply the pending migrations before adding reference data.");
        var leaveService = services.GetRequiredService<ILeaveService>();
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
