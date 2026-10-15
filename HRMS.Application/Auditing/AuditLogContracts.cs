namespace HRMS.Application.Auditing;

public sealed record AuditChange(string Field, string? Before, string? After);

public sealed record AuditLogWriteRequest(
    string Action,
    string EntityName,
    string? EntityId,
    IReadOnlyList<AuditChange> Changes,
    string Outcome = "Success",
    string? ActorUserId = null);

public sealed record AuditLogQuery(
    string? Search,
    int Page,
    int PageSize, bool ShowAll = false, string? SortBy = null, bool SortDescending = true);

public sealed record AuditLogEntry(
    long AuditLogId,
    string? ActorUserId,
    string Action,
    string EntityName,
    string? EntityId,
    DateTime OccurredAtUtc,
    string? CorrelationId,
    string ChangesJson,
    string Outcome,
    string? Source);

public sealed record AuditLogPage(
    IReadOnlyList<AuditLogEntry> Items,
    int TotalCount,
    int Page,
    int PageSize);
