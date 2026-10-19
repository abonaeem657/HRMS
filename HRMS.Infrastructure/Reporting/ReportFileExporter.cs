using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml.Linq;
using HRMS.Application.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Reporting;

public sealed class ReportFileExporter(IConfiguration configuration, ILogger<ReportFileExporter> logger) : IReportFileExporter
{
    private const int MaximumRows = 10000;
    // Limit browser processes, independently of the normal per-user request limiter.
    private readonly SemaphoreSlim pdfSlots = new(2, 2);
    private static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public async Task<byte[]> ExportAsync(ReportDocument document, string format, CancellationToken cancellationToken = default)
    {
        if (document.Tables.Count == 0 || document.Tables.Sum(t => t.Rows.Count) > MaximumRows)
            throw new ReportExportException("ExportTooLarge");
        cancellationToken.ThrowIfCancellationRequested();
        return format.ToLowerInvariant() switch
        {
            "xlsx" => Excel(document),
            "pdf" => await PdfAsync(document, cancellationToken),
            _ => throw new ReportExportException("ExportFormatInvalid")
        };
    }

    private static byte[] Excel(ReportDocument document)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            XNamespace content = "http://schemas.openxmlformats.org/package/2006/content-types";
            var types = new XElement(content + "Types",
                new XElement(content + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(content + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(content + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")));
            var sheets = new XElement(Sheet + "sheets");
            var links = new XElement(Relationships + "Relationships");
            for (var index = 0; index < document.Tables.Count; index++)
            {
                var table = document.Tables[index];
                var id = index + 1;
                // Prefix with a stable number so translated titles cannot collide.
                var name = id + " " + new string(table.Title.Where(c => !"[]:*?/\\".Contains(c)).ToArray());
                sheets.Add(new XElement(Sheet + "sheet", new XAttribute("name", name[..Math.Min(31, name.Length)]), new XAttribute("sheetId", id), new XAttribute(OfficeRelationships + "id", "rId" + id)));
                links.Add(new XElement(Relationships + "Relationship", new XAttribute("Id", "rId" + id), new XAttribute("Type", OfficeRelationships.NamespaceName + "/worksheet"), new XAttribute("Target", $"worksheets/sheet{id}.xml")));
                types.Add(new XElement(content + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{id}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
                var data = new XElement(Sheet + "sheetData");
                data.Add(Row(1, [document.Title]));
                data.Add(Row(2, [document.Caption]));
                data.Add(Row(3, table.Columns.Cast<object?>().ToArray()));
                for (var row = 0; row < table.Rows.Count; row++) data.Add(Row(row + 4, table.Rows[row]));
                var worksheet = new XElement(Sheet + "worksheet",
                    new XElement(Sheet + "sheetViews", new XElement(Sheet + "sheetView", new XAttribute("workbookViewId", 0), new XAttribute("rightToLeft", document.RightToLeft ? 1 : 0),
                        new XElement(Sheet + "pane", new XAttribute("ySplit", 3), new XAttribute("topLeftCell", "A4"), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))),
                    new XElement(Sheet + "cols", new XElement(Sheet + "col", new XAttribute("min", 1), new XAttribute("max", Math.Max(1, table.Columns.Count)), new XAttribute("width", 24), new XAttribute("customWidth", 1))), data);
                if (table.Columns.Count > 0) worksheet.Add(new XElement(Sheet + "autoFilter", new XAttribute("ref", $"A3:{Column(table.Columns.Count)}{table.Rows.Count + 3}")));
                Write(zip, $"xl/worksheets/sheet{id}.xml", worksheet);
            }
            Write(zip, "[Content_Types].xml", types);
            Write(zip, "xl/workbook.xml", new XElement(Sheet + "workbook", new XAttribute(XNamespace.Xmlns + "r", OfficeRelationships), sheets));
            Write(zip, "xl/_rels/workbook.xml.rels", links);
            Write(zip, "_rels/.rels", new XElement(Relationships + "Relationships", new XElement(Relationships + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", OfficeRelationships.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))));
        }
        return stream.ToArray();
    }

    private static XElement Row(int number, object?[] values) => new(Sheet + "row", new XAttribute("r", number),
        values.Select((value, index) => value is byte or short or int or long or float or double or decimal
            ? new XElement(Sheet + "c", new XAttribute("r", Column(index + 1) + number), new XElement(Sheet + "v", Convert.ToString(value, CultureInfo.InvariantCulture)))
            : new XElement(Sheet + "c", new XAttribute("r", Column(index + 1) + number), new XAttribute("t", "inlineStr"),
                new XElement(Sheet + "is", new XElement(Sheet + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), Display(value))))));

    private static string Column(int number)
    {
        var result = "";
        while (number > 0) { number--; result = (char)('A' + number % 26) + result; number /= 26; }
        return result;
    }
    private static void Write(ZipArchive zip, string path, XElement root)
    {
        using var entry = zip.CreateEntry(path).Open();
        new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(entry);
    }
    private static string Display(object? value) => value switch
    {
        null => "—",
        DateOnly date => date.ToString("d", CultureInfo.CurrentCulture),
        DateTime time => time.ToString("g", CultureInfo.CurrentCulture),
        IFormattable number => number.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? ""
    };

    private string? Browser()
    {
        var configured = configuration["ReportExport:BrowserPath"];
        if (!string.IsNullOrWhiteSpace(configured)) return File.Exists(configured) ? configured : null;
        return new[] {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            "/usr/bin/google-chrome", "/usr/bin/chromium"
        }.FirstOrDefault(File.Exists);
    }

    private async Task<byte[]> PdfAsync(ReportDocument document, CancellationToken ct)
    {
        var browser = Browser() ?? throw new ReportExportException("PdfBrowserUnavailable");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        var acquired = false;
        var directory = Path.Combine(Path.GetTempPath(), "HRMS-Report-" + Guid.NewGuid().ToString("N"));
        try
        {
            await pdfSlots.WaitAsync(timeout.Token);
            acquired = true;
            Directory.CreateDirectory(directory);
            var input = Path.Combine(directory, "report.html");
            var output = Path.Combine(directory, "report.pdf");
            await File.WriteAllTextAsync(input, Html(document), new UTF8Encoding(false), timeout.Token);
            var info = new ProcessStartInfo(browser) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var argument in new[] { "--headless", "--disable-gpu", "--no-first-run", "--no-default-browser-check", "--disable-background-networking", "--no-pdf-header-footer", "--user-data-dir=" + Path.Combine(directory, "profile"), "--print-to-pdf=" + output, new Uri(input).AbsoluteUri }) info.ArgumentList.Add(argument);
            using var process = Process.Start(info) ?? throw new ReportExportException("PdfExportFailed");
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            try { await process.WaitForExitAsync(timeout.Token); await Task.WhenAll(error, stdout); }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            if (process.ExitCode != 0 || !File.Exists(output)) throw new ReportExportException("PdfExportFailed");
            var bytes = await File.ReadAllBytesAsync(output, timeout.Token);
            if (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-") throw new ReportExportException("PdfExportFailed");
            return bytes;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ReportExportException("PdfExportFailed"); }
        catch (Exception exception) when (exception is not ReportExportException && exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Report PDF rendering failed.");
            throw new ReportExportException("PdfExportFailed");
        }
        finally
        {
            if (acquired) pdfSlots.Release();
            // This directory is created exclusively for this request, outside the served web root.
            try { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
            catch (IOException) { logger.LogWarning("Temporary report cleanup deferred by a file lock."); }
            catch (UnauthorizedAccessException) { logger.LogWarning("Temporary report cleanup could not complete."); }
        }
    }

    private static string Html(ReportDocument document)
    {
        static string E(string? text) => WebUtility.HtmlEncode(text) ?? "";
        var html = new StringBuilder("<!doctype html><html dir=\"").Append(document.RightToLeft ? "rtl" : "ltr")
            .Append("\"><head><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; font-src 'none';\"><title>")
            .Append(E(document.Title)).Append("</title><style>@page{size:A4 landscape;margin:14mm}body{font-family:Tahoma,Arial,sans-serif;font-size:10px;color:#183b36}h1{font-size:22px}h2{font-size:15px}table{width:100%;border-collapse:collapse;margin-bottom:20px}th,td{border:1px solid #b8c9c3;padding:7px;text-align:start;overflow-wrap:anywhere;white-space:pre-wrap}th{background:#e3eee8}thead{display:table-header-group}tr{break-inside:avoid}p{color:#405951}</style></head><body><h1>")
            .Append(E(document.Title)).Append("</h1><p>").Append(E(document.Caption)).Append("</p>");
        foreach (var table in document.Tables)
        {
            html.Append("<h2>").Append(E(table.Title)).Append("</h2><table><thead><tr>");
            foreach (var heading in table.Columns) html.Append("<th>").Append(E(heading)).Append("</th>");
            html.Append("</tr></thead><tbody>");
            foreach (var row in table.Rows) { html.Append("<tr>"); foreach (var value in row) html.Append("<td>").Append(E(Display(value))).Append("</td>"); html.Append("</tr>"); }
            html.Append("</tbody></table>");
        }
        return html.Append("</body></html>").ToString();
    }
}
