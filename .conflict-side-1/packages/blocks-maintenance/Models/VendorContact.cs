using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// One contact at a vendor. Multi-contact-per-vendor + per-property
/// primary override per ADR 0058 §"Initial contract surface". W#18 Phase 2.
/// </summary>
public sealed record VendorContact
{
    /// <summary>Unique identifier.</summary>
    public required VendorContactId Id { get; init; }

    /// <summary>The owning vendor.</summary>
    public required VendorId Vendor { get; init; }

    /// <summary>Contact's name (the human at the vendor).</summary>
    public required string Name { get; init; }

    /// <summary>Role label (e.g., "Owner", "Dispatcher", "Field Tech"); free-text per ADR 0058.</summary>
    public required string RoleLabel { get; init; }

    /// <summary>Optional email address.</summary>
    public string? Email { get; init; }

    /// <summary>Optional SMS-capable phone number (E.164).</summary>
    public string? SmsNumber { get; init; }

    /// <summary>
    /// True iff this contact is the vendor's default primary
    /// (used when no per-property override applies). At most one
    /// contact per vendor MUST have <see langword="true"/> — the
    /// service enforces this invariant on add/update.
    /// </summary>
    public bool IsPrimaryForVendor { get; init; }

    /// <summary>
    /// Per-property primary overrides. Key is a property's
    /// <see cref="EntityId"/>; value is whether this contact is
    /// the primary for that specific property (overrides the
    /// vendor-wide default).
    /// </summary>
    public IReadOnlyDictionary<EntityId, bool> PrimaryForProperty { get; init; }
        = new Dictionary<EntityId, bool>();
}
