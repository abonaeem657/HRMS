using HRMS.Application.Auditing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace HRMS.Authorization;
public sealed class AuditForbidFilter(IAuditLogService audit) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ForbidResult && context.HttpContext.User.Identity?.IsAuthenticated == true)
            await audit.RecordAsync(new AuditLogWriteRequest("Authorization.RecordDenied", "Endpoint",
                context.HttpContext.Request.Path.Value, [], "Failure"), context.HttpContext.RequestAborted);
        await next();
    }
}
