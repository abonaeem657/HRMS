using System.IO.Compression;
using System.Xml.Linq;
using HRMS.Application.Reporting;
using HRMS.Infrastructure.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRMS.UnitTests;

public class ReportExportTests
{
    [Fact]
    public async Task ExcelIsAnOpenXmlWorkbookWithArabicLiteralTextNumericCellsAndNoFormulas()
    {
        var exporter = new ReportFileExporter(new ConfigurationBuilder().Build(), NullLogger<ReportFileExporter>.Instance);
        var bytes = await exporter.ExportAsync(new("تقرير الموظفين", "تجربة", true,
            [new("الموظفون", ["الاسم", "الرقم", "الأيام"], [["أحمد <اختبار>", "0002", 14], ["=HYPERLINK(\"https://example.test\")", "+123", 2]])]), "xlsx");
        using var zip = new ZipArchive(new MemoryStream(bytes));
        XDocument Read(string name) { using var s = zip.GetEntry(name)!.Open(); return XDocument.Load(s); }
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = Read("xl/worksheets/sheet1.xml");
        Assert.Contains("أحمد <اختبار>", sheet.Descendants(ns + "t").Select(t => t.Value));
        Assert.Contains("0002", sheet.Descendants(ns + "t").Select(t => t.Value));
        Assert.Empty(sheet.Descendants(ns + "f"));
        Assert.Contains(sheet.Descendants(ns + "c"), c => c.Attribute("r")?.Value == "C4" && c.Element(ns + "v")?.Value == "14");
        Assert.Equal("1", sheet.Descendants(ns + "sheetView").Single().Attribute("rightToLeft")!.Value);
        Assert.Equal("http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument",
            Read("_rels/.rels").Root!.Elements().Single().Attribute("Type")!.Value);
    }

    [Fact]
    public async Task MissingPdfRendererReturnsConfigurationErrorRatherThanFakePdf()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ReportExport:BrowserPath"] = "missing-browser-for-test" }).Build();
        var error = await Assert.ThrowsAsync<ReportExportException>(() => new ReportFileExporter(configuration, NullLogger<ReportFileExporter>.Instance)
            .ExportAsync(new("Report", "", false, [new("Report", ["Field"], [])]), "pdf"));
        Assert.Equal("PdfBrowserUnavailable", error.Message);
    }
}
