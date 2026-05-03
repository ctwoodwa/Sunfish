using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;

namespace Sunfish.Blocks.PublicListings.Models;

/// <summary>
/// A property-rental listing surfaced to public browsers (per ADR 0059).
/// Entity is per-tenant scoped + persisted via
/// <c>PublicListingsEntityModule</c> per ADR 0015. Phase 2 of W#28 ships
/// the <c>IListingRenderer</c> that projects this entity per
/// <see cref="RedactionTier"/>.
/// </summary>
/// <remarks>
/// <c>PropertyUnitId? Unit</c> is omitted in W#28 Phase 1 because
/// <c>blocks-properties</c> hasn't shipped <c>PropertyUnit</c> yet. The
/// field is present as <c>UnitRef: string?</c> placeholder; future phase
/// promotes to typed FK.
/// </remarks>
public sealed record PublicListing
{
    /// <summary>Stable identifier.</summary>
    public required PublicListingId Id { get; init; }

    /// <summary>Owning tenant (per <c>IMustHaveTenant</c>).</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>Reference to the listed property.</summary>
    public required PropertyId Property { get; init; }

    /// <summary>Optional reference to a specific unit within the property; null for whole-property listings (single-family). Stored as a string placeholder until <c>blocks-properties</c> ships <c>PropertyUnitId</c>.</summary>
    public string? UnitRef { get; init; }

    /// <summary>Lifecycle status.</summary>
    public required PublicListingStatus Status { get; init; }

    /// <summary>Headline rendered at top of the listing card; e.g., "Charming 2-bedroom in West End".</summary>
    public required string Headline { get; init; }

    /// <summary>Markdown-formatted body of the listing.</summary>
    public required string Description { get; init; }

    /// <summary>Photos attached to the listing, in display order.</summary>
    public IReadOnlyList<ListingPhotoRef> Photos { get; init; } = Array.Empty<ListingPhotoRef>();

    /// <summary>Asking rent per ADR 0051; null while in Draft.</summary>
    public Money? AskingRent { get; init; }

    /// <summary>Date the unit becomes available; null if rolling.</summary>
    public DateTimeOffset? AvailableFrom { get; init; }

    /// <summary>Showing-availability mode + slots.</summary>
    public required ShowingAvailability ShowingAvailability { get; init; }

    /// <summary>Per-listing redaction policy (Phase 2 renderer enforces).</summary>
    public required RedactionPolicy Redaction { get; init; }

    /// <summary>URL-safe slug; tenant-scoped uniqueness; e.g., "123-main-st-2".</summary>
    public required string Slug { get; init; }

    /// <summary>Wall-clock time the listing was first persisted.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Wall-clock time the listing transitioned to <see cref="PublicListingStatus.Published"/>.</summary>
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Wall-clock time the listing transitioned to <see cref="PublicListingStatus.Unlisted"/>.</summary>
    public DateTimeOffset? UnlistedAt { get; init; }
}
