using System.Text.Json.Serialization;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Lifecycle status for a Ship's Office document per ADR 0083 §1.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentStatus
{
    /// <summary>Authored but not yet published.</summary>
    Draft,

    /// <summary>Published; the canonical version is the most recent published version.</summary>
    Published,

    /// <summary>Archived; visible in browse only via the <c>StatusFilter</c>; non-archived views exclude.</summary>
    Archived,

    /// <summary>Awaiting a signature envelope (<see cref="ShipsOfficeDocumentKind.SignatureEnvelope"/>).</summary>
    PendingSignature,
}
