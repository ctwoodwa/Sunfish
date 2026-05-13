using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.IdentityAtlas;
using Sunfish.Foundation.UI;
using Sunfish.UICore.Wayfinder;

namespace Sunfish.Bridge.Features.Identity;

/// <summary>
/// Bridge implementation of <see cref="IIdentityAtlasSurface"/> per ADR 0066 §Phase 3.
/// Assembles view-model projections from <see cref="IKeyStore"/>,
/// <see cref="ITrusteeRegistry"/>, and <see cref="ITeamRegistry"/>.
/// Every read is scoped to the caller-supplied TenantId and ActorId — the bridge
/// multi-tenant posture requires both parameters; Bridge does not short-circuit to a
/// local IActiveTeamAccessor.
/// MUST NOT call IFieldDecryptor, IAuditTrail.AppendAsync, or IStandingOrderIssuer
/// (ADR 0066 OQ-4 projection-only constraint).
/// </summary>
public sealed class BridgeIdentityAtlasSurface : IIdentityAtlasSurface
{
    private readonly IKeyStore _keyStore;
    private readonly ITrusteeRegistry _trusteeRegistry;
    private readonly ITeamRegistry _teamRegistry;

    public BridgeIdentityAtlasSurface(
        IKeyStore keyStore,
        ITrusteeRegistry trusteeRegistry,
        ITeamRegistry teamRegistry)
    {
        _keyStore = keyStore;
        _trusteeRegistry = trusteeRegistry;
        _teamRegistry = teamRegistry;
    }

    /// <inheritdoc />
    public async ValueTask<IdentityProfileEditViewModel> GetProfileEditAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default)
    {
        var profile = await _keyStore.GetIdentityProfileAsync(tenant, actor, ct);
        return new IdentityProfileEditViewModel(
            actor,
            profile?.DisplayName ?? string.Empty,
            profile?.ContactEmail ?? string.Empty,
            profile?.PhoneNumber);
    }

    /// <inheritdoc />
    public async ValueTask<KeyRotationViewModel> GetKeyRotationAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default)
    {
        var keyInfo = await _keyStore.GetCurrentKeyInfoAsync(tenant, actor, ct);
        if (keyInfo is null)
        {
            return new KeyRotationViewModel(
                actor,
                CurrentFingerprint: default,
                HistoricalKeyCount: 0,
                RotationInProgress: false,
                RotationWindowExpiry: null);
        }

        return new KeyRotationViewModel(
            actor,
            CurrentFingerprint: KeyFingerprint.FromPublicKey(keyInfo.PublicKey),
            HistoricalKeyCount: keyInfo.HistoricalKeyCount,
            RotationInProgress: keyInfo.RotationInProgress,
            RotationWindowExpiry: keyInfo.RotationWindowExpiry);
    }

    /// <inheritdoc />
    public async ValueTask<RecoveryContactsViewModel> GetRecoveryContactsAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default)
    {
        var policy = await _trusteeRegistry.GetPolicyAsync(tenant, ct);
        var trustees = await _trusteeRegistry.GetTrusteesAsync(tenant, actor, ct);
        var contacts = trustees
            .Select(t => new RecoveryContact(
                t.TrusteeActorId,
                t.DisplayName,
                MapVerificationState(t.VerificationState),
                t.EnrolledAt))
            .ToList();
        return new RecoveryContactsViewModel(actor, contacts, policy.MaxTrustees);
    }

    /// <inheritdoc />
    public ValueTask<HistoricalKeysBrowseViewModel> GetHistoricalKeysAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default)
    {
        // H4 PLACEHOLDER: ADR 0046-A1 HistoricalKeysProjection not yet on main.
        // Returns empty list; follow-up populates from projection when ADR 0046-A1 ships.
        return ValueTask.FromResult(
            new HistoricalKeysBrowseViewModel(actor, Array.Empty<HistoricalKeyEntry>()));
    }

    /// <inheritdoc />
    public async ValueTask<ActiveTeamOverviewViewModel> GetActiveTeamOverviewAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default)
    {
        // BRIDGE-S5: ITeamRegistry.GetMembershipsAsync currently accepts only (ActorId, CancellationToken).
        // The tenant parameter is NOT threaded through — if ActorId namespaces overlap across tenants,
        // this could return memberships from another tenant. Widening ITeamRegistry to
        // GetMembershipsAsync(TenantId, ActorId, CancellationToken) is tracked separately; until
        // that interface change lands, Bridge multi-tenant callers must treat this projection as
        // actor-scoped only. Gate: ITeamRegistry interface widening workstream.
        var memberships = await _teamRegistry.GetMembershipsAsync(actor, ct);
        // Bridge does not have a local active-team concept (no IActiveTeamAccessor);
        // the active team indicator is null in the projection (ADR 0066 §Phase 3 Bridge posture).
        Guid? activeTeamId = null;
        var entries = memberships
            .Select(m => new TeamMembershipEntry(
                m.TeamId,
                m.DisplayName,
                m.RoleDisplayName,
                m.SubkeyFingerprint))
            .ToList();
        return new ActiveTeamOverviewViewModel(actor, entries, activeTeamId);
    }

    // Maps TrusteeVerificationState → SyncState per ADR 0066 OQ-1 council ruling.
    private static SyncState MapVerificationState(TrusteeVerificationState state) =>
        state switch
        {
            TrusteeVerificationState.Pending  => SyncState.Stale,
            TrusteeVerificationState.Verified => SyncState.Healthy,
            TrusteeVerificationState.Revoked  => SyncState.Quarantine,
            _                                  => SyncState.Offline,
        };
}
