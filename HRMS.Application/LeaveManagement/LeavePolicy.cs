namespace HRMS.Application.LeaveManagement;

// Owner-approved training scope: calendar-day leave, without hourly permissions.
public static class LeavePolicy
{
    public static IReadOnlyList<string> SupportedNames { get; } = Array.AsReadOnly(new[] { "Annual", "Sick", "Unpaid" });
    public static IReadOnlyList<LeaveTypeInput> Defaults { get; } = Array.AsReadOnly(new[] {
        new LeaveTypeInput("Annual", "سنوية", 14, true, true),
        new LeaveTypeInput("Sick", "مرضية", 14, true, true),
        new LeaveTypeInput("Unpaid", "بدون راتب", 0, false, true)
    });

    public static LeaveTypeInput? Normalize(LeaveTypeInput input)
    {
        var definition = Defaults.SingleOrDefault(t => string.Equals(t.Name, input.Name?.Trim(), StringComparison.OrdinalIgnoreCase));
        return definition is null ? null : definition with { DefaultDaysPerYear = definition.IsPaid ? input.DefaultDaysPerYear : 0, IsActive = input.IsActive };
    }
}
