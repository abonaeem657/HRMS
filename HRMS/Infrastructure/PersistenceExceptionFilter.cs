using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
namespace HRMS.Infrastructure;
public sealed class PersistenceExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var concurrency = context.Exception is DbUpdateConcurrencyException or HRMS.Application.Common.DataConcurrencyException;
        var duplicate = context.Exception is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } or HRMS.Application.Common.DataWriteException { IsUnique: true };
        if (!concurrency && !duplicate) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = 409, Title = "Data conflict",
            Detail = concurrency ? HRMS.Application.Common.Messages.Get("ConcurrentEdit") : HRMS.Application.Common.Messages.Get("DuplicateIdentifier") }) { StatusCode = 409 };
        context.ExceptionHandled = true;
    }
}
