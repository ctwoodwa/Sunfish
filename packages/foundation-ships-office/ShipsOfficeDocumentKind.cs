using System.Text.Json.Serialization;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Discriminator for Ship's Office documents per ADR 0083 §1. Phase 1
/// ships four kinds; the <c>DynamicTemplate</c> kind is Phase 5
/// (gated on ADR 0055 reaching <c>Status: Accepted</c> per hand-off
/// halt-condition H4) and is intentionally absent from this enum.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShipsOfficeDocumentKind
{
    /// <summary>A business-case bundle manifest per ADR 0007 (Catalog).</summary>
    BundleManifest,

    /// <summary>A lease document version (W#22 / W#27).</summary>
    LeaseDocument,

    /// <summary>A vendor W9 (W#18); TIN is always redacted in browse view per §Trust impact.</summary>
    VendorW9,

    /// <summary>A signature envelope per ADR 0021 (empty-list stub until Phase 2/Phase 5 wiring).</summary>
    SignatureEnvelope,
}
