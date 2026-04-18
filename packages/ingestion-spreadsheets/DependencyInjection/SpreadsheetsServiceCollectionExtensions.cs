using Microsoft.Extensions.DependencyInjection;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Core.DependencyInjection;
using Sunfish.Ingestion.Spreadsheets.Importers;

namespace Sunfish.Ingestion.Spreadsheets.DependencyInjection;

/// <summary>
/// DI extensions that register the spreadsheet ingestion pipeline and its importers onto the
/// shared <see cref="IngestionBuilder"/>.
/// </summary>
public static class SpreadsheetsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CsvRowImporter"/>, <see cref="XlsxRowImporter"/>, and the
    /// <see cref="SpreadsheetIngestionPipeline"/> pipeline against the given builder.
    /// </summary>
    public static IngestionBuilder WithSpreadsheets(this IngestionBuilder builder)
    {
        builder.Services.AddSingleton<CsvRowImporter>();
        builder.Services.AddSingleton<XlsxRowImporter>();
        builder.Services.AddSingleton<IIngestionPipeline<SpreadsheetUpload>, SpreadsheetIngestionPipeline>();
        return builder;
    }
}
