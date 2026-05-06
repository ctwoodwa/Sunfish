using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// Identity Atlas read-side surface per ADR 0066 §2. Five view-model
/// projections render the per-actor identity Atlas (profile / key
/// rotation / recovery contacts / historical keys / active team
/// overview); concrete implementations land in W#54 (Phase 3 deferred
/// per the hand-off — W#54 ships the identity Atlas implementations).
/// </summary>
/// <remarks>
/// <para>
/// <b>Side-effect-free contract:</b> implementations MUST be projection-
/// only — no mutations, no audit emission, no Standing Order issuance.
/// The Identity Atlas surface composes the existing
/// <c>foundation-recovery</c> read-side state for UI rendering; writes
/// continue to flow through the Standing Order issuer + capability
/// graph + recovery coordinator.
/// </para>
/// <para>
/// <b>Folder note:</b> the file lives in
/// <c>packages/ui-core/Wayfinder/Identity/</c> for organisation but the
/// namespace stays <c>Sunfish.UICore.Wayfinder</c> per OQ-2 council
/// decision (flat namespace; sub-folders are organisation-only).
/// </para>
/// </remarks>
public interface IIdentityAtlasSurface
{
    /// <summary>Returns the identity-profile edit view-model for the actor.</summary>
    ValueTask<IdentityProfileEditViewModel> GetProfileEditAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default);

    /// <summary>Returns the key-rotation view-model for the actor.</summary>
    ValueTask<KeyRotationViewModel> GetKeyRotationAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default);

    /// <summary>Returns the recovery-contacts view-model for the actor.</summary>
    ValueTask<RecoveryContactsViewModel> GetRecoveryContactsAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default);

    /// <summary>Returns the historical-keys browse view-model for the actor.</summary>
    ValueTask<HistoricalKeysBrowseViewModel> GetHistoricalKeysAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default);

    /// <summary>Returns the active-team overview view-model for the actor.</summary>
    ValueTask<ActiveTeamOverviewViewModel> GetActiveTeamOverviewAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default);
}
