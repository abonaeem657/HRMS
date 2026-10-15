using HRMS.Application.Auditing;
using HRMS.Application.Identity;
using HRMS.Models.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Controllers;

[Authorize(Roles = IdentityRoles.ViewAuditLogs)]
public sealed class AuditLogsController(IAuditLogService auditLogService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] AuditLogListViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await auditLogService.SearchAsync(
            new AuditLogQuery(model.Search, model.Page, model.PageSize),
            cancellationToken);
        model.Items = result.Items;
        model.TotalCount = result.TotalCount;
        model.Page = result.Page;
        model.PageSize = result.PageSize;
        return View(model);
    }
}
