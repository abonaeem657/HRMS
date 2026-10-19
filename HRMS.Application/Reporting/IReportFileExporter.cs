namespace HRMS.Application.Reporting;

public sealed record ReportTable(string Title, IReadOnlyList<string> Columns, IReadOnlyList<object?[]> Rows);
public sealed record ReportDocument(string Title, string Caption, bool RightToLeft, IReadOnlyList<ReportTable> Tables);
public interface IReportFileExporter
{
    Task<byte[]> ExportAsync(ReportDocument document, string format, CancellationToken cancellationToken = default);
}
public sealed class ReportExportException(string message) : Exception(message);
