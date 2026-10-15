namespace HRMS.Application.Reporting;

public sealed record ReportOption(int Id, string Text);

public sealed class ReportFilterOptions
{
    public IReadOnlyList<ReportOption> Departments { get; init; } = [];
    public IReadOnlyList<ReportOption> Positions { get; init; } = [];
    public IReadOnlyList<ReportOption> Employees { get; init; } = [];
    public IReadOnlyList<ReportOption> LeaveTypes { get; init; } = [];
    public IReadOnlyList<string> EmploymentStatuses { get; init; } = [];
}
