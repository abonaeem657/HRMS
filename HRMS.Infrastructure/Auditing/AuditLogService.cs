using System.Security.Claims;
using System.Text.Json;
using HRMS.Application.Auditing;
using HRMS.Domain.Auditing;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Auditing;

public sealed class AuditLogService(
    HRMSDbContext dbContext,
    IHttpContextAccessor httpContextAccessor) : IAuditLogService
{
    private const int MaximumPageSize = 100;
    private static readonly string[] SensitiveFieldMarkers =
    [
        "password",
        "token",
        "secret",
        "credential",
        "hash",
        "securitystamp"
    ];

    public void Append(AuditLogWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Changes.Any(change =>
                SensitiveFieldMarkers.Any(marker =>
                    change.Field.Contains(marker, StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException("Sensitive fields cannot be written to the audit log.");
        }

        if (request.Changes.Any(change => change.Field.Contains("phone", StringComparison.OrdinalIgnoreCase)
            && (change.Before is not null || change.After is not null)))
            throw new InvalidOperationException("Personal contact values cannot be written to audit logs.");

        var httpContext = httpContextAccessor.HttpContext;
        var actorUserId = request.ActorUserId ??
            httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var changesJson = JsonSerializer.Serialize(request.Changes);

        dbContext.AuditLogs.Add(new AuditLog(
            actorUserId,
            request.Action,
            request.EntityName,
            Limit(request.EntityId, 256),
            DateTime.UtcNow,
            Limit(httpContext?.TraceIdentifier, 128),
            changesJson,
            request.Outcome,
            Limit(httpContext?.Request.Path.Value, 256)));
    }

    public async Task RecordAsync(
        AuditLogWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        Append(request);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuditLogPage> SearchAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, MaximumPageSize);
        var requestedPage = Math.Max(1, query.Page);
        var logs = dbContext.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            logs = logs.Where(log =>
                log.Action.Contains(search) ||
                log.EntityName.Contains(search) ||
                (log.EntityId != null && log.EntityId.Contains(search)) ||
                (log.ActorUserId != null && log.ActorUserId.Contains(search)));
        }

        var totalCount = await logs.CountAsync(cancellationToken);
        if (query.ShowAll) pageSize = Math.Max(1, totalCount);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var page = Math.Min(requestedPage, totalPages);
        var ordered = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("action", false) => logs.OrderBy(log => log.Action),
            ("action", true) => logs.OrderByDescending(log => log.Action),
            ("entity", false) => logs.OrderBy(log => log.EntityName),
            ("entity", true) => logs.OrderByDescending(log => log.EntityName),
            (_, false) => logs.OrderBy(log => log.OccurredAtUtc),
            _ => logs.OrderByDescending(log => log.OccurredAtUtc)
        };
        var items = await ordered.ThenByDescending(log => log.AuditLogId)            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new AuditLogEntry(
                log.AuditLogId,
                log.ActorUserId,
                log.Action,
                log.EntityName,
                log.EntityId,
                log.OccurredAtUtc,
                log.CorrelationId,
                log.ChangesJson,
                log.Outcome,
                log.Source))
            .ToListAsync(cancellationToken);

        return new AuditLogPage(items, totalCount, page, pageSize);
    }

    private static string? Limit(string? value, int maximumLength) =>
        value is { Length: > 0 } && value.Length > maximumLength
            ? value[..maximumLength]
            : value;
}
