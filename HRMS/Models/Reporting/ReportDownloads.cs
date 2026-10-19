using HRMS.Application.Reporting;
using HRMS.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace HRMS.Models.Reporting;

public static class ReportDownloads
{
    public static bool IsExport(this Controller controller) =>
        controller.Request.Query["export"].ToString().ToLowerInvariant() is "pdf" or "xlsx";

    public static async Task<IActionResult> DownloadAsync(this Controller controller, IReportFileExporter exporter,
        IStringLocalizer<SharedResource> text, string title, string fileName, DateTime localNow,
        CancellationToken ct, params ReportTable[] tables)
    {
        controller.Response.Headers.CacheControl = "no-store, private";
        var format = controller.Request.Query["export"].ToString().ToLowerInvariant();
        try
        {
            var document = new ReportDocument(title, text["ExportGenerated", localNow.ToString("g")].Value,
                System.Globalization.CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft, tables);
            var data = await exporter.ExportAsync(document, format, ct);
            return controller.File(data, format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName + "-" + localNow.ToString("yyyy-MM-dd") + "." + format);
        }
        catch (ReportExportException error)
        {
            return controller.StatusCode(error.Message is "ExportTooLarge" or "ExportFormatInvalid" ? 400 : 503,
                new ProblemDetails { Title = text[error.Message].Value, Status = error.Message is "ExportTooLarge" or "ExportFormatInvalid" ? 400 : 503 });
        }
    }
}
