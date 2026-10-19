using HRMS.Models;
using HRMS.Application.Auditing;
using HRMS.Application.Identity;
using HRMS.Models.Auditing;
using HRMS.Application.Reporting;
using HRMS.Models.Reporting;
using HRMS.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Controllers;

[Authorize(Roles = IdentityRoles.ViewAuditLogs)]
public sealed class AuditLogsController(IAuditLogService auditLogService, IReportFileExporter exporter,
    IStringLocalizer<SharedResource> T, HRMS.Application.Common.IBusinessClock clock) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] AuditLogListViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await auditLogService.SearchAsync(
            new AuditLogQuery(model.Search, model.Page, DisplayPagination.PageSize, this.IsExport() || DisplayPagination.IsShowAll(Request), model.SortBy, model.SortDescending),
            cancellationToken);
        if (this.IsExport()) return await this.DownloadAsync(exporter, T, T["AuditLog"].Value, "audit",
            clock.ToLocal(DateTime.UtcNow)!.Value, cancellationToken,
            new ReportTable(T["AuditLog"].Value, new[] { "Date", "User", "Action", "Entity", "RecordId", "Outcome", "Changes" }.Select(k => T[k].Value).ToArray(),
                result.Items.Select(r => new object?[] { clock.ToLocal(r.OccurredAtUtc), r.ActorUserId, r.Action, r.EntityName, r.EntityId, r.Outcome, r.ChangesJson }).ToList()));
        ViewData["SortDescending"] = model.SortDescending;
        model.Items = result.Items;
        model.TotalCount = result.TotalCount;
        ViewData["Pagination"] = DisplayPagination.Create(Request, result.TotalCount, result.Page);
        model.Page = result.Page;
        model.PageSize = result.PageSize;
        return View(model);
    }
}
