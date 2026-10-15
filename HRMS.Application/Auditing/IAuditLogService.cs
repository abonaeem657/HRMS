namespace HRMS.Application.Auditing;

public interface IAuditLogService
{
    void Append(AuditLogWriteRequest request);

    Task RecordAsync(
        AuditLogWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<AuditLogPage> SearchAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);
}
