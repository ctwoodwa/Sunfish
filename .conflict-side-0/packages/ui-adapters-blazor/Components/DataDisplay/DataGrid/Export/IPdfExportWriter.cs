namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

internal interface IPdfExportWriter
{
    Task<byte[]> WriteAsync<TItem>(
        IReadOnlyList<ExportColumnDescriptor> columns,
        IReadOnlyList<TItem> items,
        PdfExportOptions options,
        CancellationToken cancellationToken = default);
}
