using System.Text.Json.Serialization;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Discriminator for Ship's Office documents per ADR 0083 §1. Five
/// kinds — <c>DynamicTemplate</c> (Phase 5) joined the enum once
/// ADR 0055 reached <c>Status: Accepted</c>; consumed via local
/// <see cref="Services.IFormSchemaStore"/> stub per xo-ruling-T02-43Z
/// pending canonical <c>foundation-forms</c> substrate.
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

    /// <summary>
    /// A dynamic form template per ADR 0055 (W#55 Phase 5). Sourced
    /// from <see cref="Services.IFormSchemaStore"/> — local stub until
    /// canonical <c>foundation-forms</c> substrate ships.
    /// </summary>
    DynamicTemplate,
}
