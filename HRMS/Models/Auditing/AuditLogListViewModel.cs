using HRMS.Application.Auditing;

namespace HRMS.Models.Auditing;

public sealed class AuditLogListViewModel
{
    public string? Search { get; set; }

    public IReadOnlyList<AuditLogEntry> Items { get; set; } =
        Array.Empty<AuditLogEntry>();

    public int TotalCount { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
