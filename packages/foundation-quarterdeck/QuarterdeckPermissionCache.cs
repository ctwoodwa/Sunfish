using System.Collections.Concurrent;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Per-snapshot permission cache for
/// <see cref="DefaultQuarterdeckDataProvider"/> per ADR 0080 §5.2 +
/// the W#46 P1 council Critical M1 amendment (cross-tenant authority
/// bleed). Every cache key includes <see cref="TenantId"/> so
/// permission decisions made for tenant-A never leak into tenant-B's
/// snapshot — even when the same
/// <see cref="Sunfish.Foundation.Crypto.PrincipalId"/> is registered
/// in both tenants with different <see cref="ShipRole"/>s.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifetime:</b> per-snapshot. The cache is constructed inside one
/// <see cref="IQuarterdeckDataProvider.GetSnapshotAsync"/> call and
/// discarded when the snapshot returns. Cross-snapshot caching would
/// risk staleness when a Standing Order applies — re-resolving on
/// every snapshot is the safer default per §2.1.
/// </para>
/// <para>
/// <b>Concurrency:</b>
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>-backed; the
/// snapshot path may parallelize permission resolution per location
/// via <c>Task.WhenAll</c> safely.
/// </para>
/// </remarks>
internal sealed class QuarterdeckPermissionCache
{
    private readonly ConcurrentDictionary<Key, PermissionDecision> _decisions = new();

    private readonly record struct Key(
        TenantId TenantId,
        PrincipalId PrincipalId,
        string ActionName,
        ShipLocation Location);

    /// <summary>Read a cached decision; returns false on miss.</summary>
    public bool TryGet(
        TenantId tenantId,
        PrincipalId principalId,
        ShipAction action,
        ShipLocation location,
        out PermissionDecision? decision)
    {
        var key = new Key(tenantId, principalId, action.Name, location);
        if (_decisions.TryGetValue(key, out var hit))
        {
            decision = hit;
            return true;
        }
        decision = null;
        return false;
    }

    /// <summary>
    /// Store a resolved decision. Idempotent on duplicate keys —
    /// the first stored value wins; subsequent stores leave the
    /// cache unchanged.
    /// </summary>
    public void Set(
        TenantId tenantId,
        PrincipalId principalId,
        ShipAction action,
        ShipLocation location,
        PermissionDecision decision)
    {
        var key = new Key(tenantId, principalId, action.Name, location);
        _decisions.TryAdd(key, decision);
    }
}
