using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.PropertyAssets.Models;

/// <summary>
/// Append-only lifecycle event for an <see cref="Asset"/>. Provides the
/// audit-grade history needed by inspections, work orders, depreciation
/// schedules, and tax reporting. Events are immutable once appended.
/// </summary>
/// <remarks>
/// Per <see cref="IMustHaveTenant"/>; tenant scoping is mandatory.
/// <para>
/// <b>Audit-trail integration deferred.</b> The hand-off names
/// <c>IAuditTrail</c> emission as a Phase 4 deliverable, but full integration
/// requires a signing key + <c>SignedOperation&lt;AuditPayload&gt;</c>
/// envelope construction that would not be exercised by the in-memory
/// first-slice tests. The kernel-audit substrate (see <c>Sunfish.Kernel.Audit</c>)
/// is the eventual emission target; first-slice carries the domain event in
/// the in-memory event store only (see <c>Sunfish.Blocks.PropertyAssets.Services.IAssetLifecycleEventStore</c>).
/// See PR description for the research-session OQ (#2) flag and the
/// follow-up hand-off scope.
/// </para>
/// </remarks>
public sealed record AssetLifecycleEvent : IMustHaveTenant
{
    /// <summary>Stable identifier for this event.</summary>
    public required Guid EventId { get; init; }

    /// <summary>FK to the asset this event describes.</summary>
    public required AssetId Asset { get; init; }

    /// <summary>
    /// FK snapshot of the property the asset belonged to at event-emission
    /// time. Lets <c>IAssetLifecycleEventStore.GetForPropertyAsync</c> resolve
    /// without a repository dependency. Slight deviation from the hand-off
    /// shape (added by sunfish-PM) — see PR description for rationale.
    /// </summary>
    public required PropertyId Property { get; init; }

    /// <summary>Owning tenant. Required (default-rejected by persistence adapters).</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Discriminator for the event kind.</summary>
    public required AssetLifecycleEventType EventType { get; init; }

    /// <summary>Wall-clock time at which the event occurred.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Opaque reference to the principal who recorded the event (operator,
    /// vendor, inspector). First-slice ships this as a string; will migrate
    /// to a typed <c>IdentityRef</c> when the identity-substrate hand-off lands.
    /// TODO: IdentityRef — gated on identity-substrate hand-off.
    /// </summary>
    public required string RecordedBy { get; init; }

    /// <summary>Free-text notes captured with the event.</summary>
    public string? Notes { get; init; }

    /// <summary>Event-type-specific payload (e.g. service vendor + cost; warranty claim id).</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
