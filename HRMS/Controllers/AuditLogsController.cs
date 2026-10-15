using HRMS.Models;
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
            new AuditLogQuery(model.Search, model.Page, DisplayPagination.PageSize, DisplayPagination.IsShowAll(Request), model.SortBy, model.SortDescending),
            cancellationToken);
        ViewData["SortDescending"] = model.SortDescending;
        model.Items = result.Items;
        model.TotalCount = result.TotalCount;
        ViewData["Pagination"] = DisplayPagination.Create(Request, result.TotalCount, result.Page);
        model.Page = result.Page;
        model.PageSize = result.PageSize;
        return View(model);
    }
}
