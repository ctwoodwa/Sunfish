namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Opaque stable identifier for a Ship's Office document. The format is
/// internal to <see cref="IShipsOfficeDataProvider"/> implementations —
/// callers MUST NOT parse, construct, or rely on the wire format outside
/// of provider impls. Per ADR 0083 §1.
/// </summary>
/// <param name="Value">Provider-internal identifier string.</param>
public readonly record struct ShipsOfficeDocumentId(string Value);
