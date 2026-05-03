namespace Sunfish.Ingestion.Spreadsheets;

/// <summary>
/// A single spreadsheet upload (CSV or XLSX) accompanied by the column mappings that drive
/// per-row entity minting. See Sunfish Platform spec §7.2.
/// </summary>
/// <param name="Content">The spreadsheet content stream (not owned — caller disposes).</param>
/// <param name="Filename">The original filename (used in the session body).</param>
/// <param name="Kind">Whether the content is CSV or XLSX.</param>
/// <param name="SchemaId">The per-row entity schema id (not the session schema id).</param>
/// <param name="Mappings">Ordered list of column mappings applied to every data row.</param>
public sealed record SpreadsheetUpload(
    Stream Content,
    string Filename,
    SpreadsheetKind Kind,
    string SchemaId,
    IReadOnlyList<ColumnMapping> Mappings);

/// <summary>Supported spreadsheet content kinds.</summary>
public enum SpreadsheetKind
{
    /// <summary>Comma-separated values, parsed with CsvHelper.</summary>
    Csv,

    /// <summary>Office Open XML workbook, parsed with ClosedXML.</summary>
    Xlsx,
}
