using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Anchor-specific <see cref="IPermissionResolver"/> stub that grants every
/// action to every principal as <see cref="ShipRole.Captain"/>. Correct for
/// Anchor's single-operator local-first posture — the local operator IS the
/// owner of their node and may perform all Engine Room operations.
/// </summary>
/// <remarks>
/// This resolver carries no audit-trail dependency. Anchor is a personal node,
/// not a multi-tenant server; production authority resolution (role
/// assignments, rate-limit, audit emission) is handled by the full
/// <see cref="DefaultPermissionResolver"/> registered in Bridge. W#51 Phase 4
/// will replace this stub with real Anchor ship-role wiring when the
/// Quarterdeck Blazor UI ships.
/// </remarks>
internal sealed class AnchorGrantAllPermissionResolver : IPermissionResolver
{
    /// <inheritdoc />
    public IReadOnlyList<ShipAction> AuditLoudActions { get; } = [];

    /// <inheritdoc />
    public ValueTask<PermissionDecision> ResolveAsync(
        TenantId tenantId,
        Principal subject,
        ShipLocation location,
        DeckDepth deck,
        ShipAction action,
        Resource? resource,
        CancellationToken ct = default)
        => ValueTask.FromResult<PermissionDecision>(
            new PermissionDecision.Granted(ShipRole.Captain, DateTimeOffset.UtcNow, Proof: null));
}
