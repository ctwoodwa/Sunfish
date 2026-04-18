namespace Sunfish.Ingestion.Spreadsheets;

/// <summary>
/// Declarative mapping from a source spreadsheet column header to a target entity field with an
/// explicit type coercion. See Sunfish Platform spec §7.2.
/// </summary>
/// <param name="SourceHeader">The column header name as it appears in the spreadsheet.</param>
/// <param name="TargetField">The field name to use in the ingested entity body.</param>
/// <param name="TypeCoercion">The coercion to apply to the raw cell value.</param>
public sealed record ColumnMapping(string SourceHeader, string TargetField, CoercionKind TypeCoercion);

/// <summary>
/// Supported type coercion kinds for <see cref="ColumnMapping"/>. Each kind corresponds to a
/// branch in <see cref="Coercion.TypeCoercer.TryCoerce"/>.
/// </summary>
public enum CoercionKind
{
    /// <summary>Pass the raw string through unchanged.</summary>
    String,

    /// <summary>Parse as a 32-bit integer using invariant culture.</summary>
    Integer,

    /// <summary>Parse as a decimal using invariant culture.</summary>
    Decimal,

    /// <summary>Parse as a UTC <see cref="System.DateTime"/>.</summary>
    DateTimeUtc,

    /// <summary>Parse as a boolean accepting common textual variants.</summary>
    Boolean,

    /// <summary>Normalize as a trimmed string; actual enum resolution is the consumer's job.</summary>
    EnumIgnoreCase,
}
