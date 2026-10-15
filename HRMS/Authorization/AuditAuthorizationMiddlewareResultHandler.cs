using HRMS.Application.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace HRMS.Authorization;

public sealed class AuditAuthorizationMiddlewareResultHandler(
    ILogger<AuditAuthorizationMiddlewareResultHandler> logger)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
        {
            var auditLogService = context.RequestServices.GetRequiredService<IAuditLogService>();
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                "Authorization.AccessDenied",
                "Endpoint",
                context.Request.Path.Value,
                [],
                "Failure"));

            logger.LogInformation(
                "Authorization denied for user {UserId} on {Path}.",
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                context.Request.Path);
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
