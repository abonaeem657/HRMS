namespace HRMS.Domain.Auditing;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    public AuditLog(
        string? actorUserId,
        string action,
        string entityName,
        string? entityId,
        DateTime occurredAtUtc,
        string? correlationId,
        string changesJson,
        string outcome,
        string? source)
    {
        ActorUserId = NormalizeOptional(actorUserId);
        Action = RequireValue(action, nameof(action));
        EntityName = RequireValue(entityName, nameof(entityName));
        EntityId = NormalizeOptional(entityId);
        OccurredAtUtc = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
        CorrelationId = NormalizeOptional(correlationId);
        ChangesJson = changesJson ?? "{}";
        Outcome = RequireValue(outcome, nameof(outcome));
        Source = NormalizeOptional(source);
    }

    public long AuditLogId { get; private set; }

    public string? ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityName { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public string? CorrelationId { get; private set; }

    public string ChangesJson { get; private set; } = "{}";

    public string Outcome { get; private set; } = string.Empty;

    public string? Source { get; private set; }

    private static string RequireValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
