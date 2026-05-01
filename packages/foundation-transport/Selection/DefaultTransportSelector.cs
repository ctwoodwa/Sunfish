using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Transport.Audit;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Transport;

/// <summary>
/// Reference <see cref="ITransportSelector"/> per ADR 0061 §"Tier
/// selection algorithm" + amendments A2 (failover semantics) + A4
/// (timeout pinning). Picks the best <see cref="IPeerTransport"/> for
/// a peer per the failover order (T1 → T2 → T3) with per-tier wall-
/// clock budgets and a short selection-result cache.
/// </summary>
/// <remarks>
/// <para>
/// Caching is per-peer + 30-second TTL: cache key is <see cref="PeerId"/>,
/// cache value is <c>(IPeerTransport, ResolvedAt)</c>. The cache stores
/// the <em>selection result</em>, not mesh-membership state — adapter
/// failures (signalled via the audit pipeline in P5+) are the trigger
/// for explicit invalidation. Until the audit pipeline is wired,
/// callers can drop the cache for a peer via <see cref="Invalidate"/>.
/// </para>
/// <para>
/// Tier-2 mesh adapters iterate in deterministic order: the registration
/// order supplied at construction (operator-set "config priority"),
/// with <see cref="IMeshVpnAdapter.AdapterName"/> lexicographic as the
/// tie-break — "headscale" &lt; "netbird" &lt; "tailscale" &lt;
/// "wireguard-manual".
/// </para>
/// <para>
/// The selector returns the first transport whose
/// <see cref="IPeerTransport.ResolvePeerAsync"/> succeeds within the
/// per-tier budget; <see cref="IPeerTransport.ConnectAsync"/> is the
/// caller's responsibility (per the contract in P1).
/// </para>
/// </remarks>
public sealed class DefaultTransportSelector : ITransportSelector
{
    /// <summary>Default Tier-1 (mDNS) total connect budget per A4.</summary>
    public static readonly TimeSpan DefaultTier1Budget = TimeSpan.FromSeconds(2);

    /// <summary>Default Tier-2 (mesh) per-adapter connect budget per A4.</summary>
    public static readonly TimeSpan DefaultTier2Budget = TimeSpan.FromSeconds(5);

    /// <summary>Default Tier-3 (Bridge relay) connect budget per A4.</summary>
    public static readonly TimeSpan DefaultTier3Budget = TimeSpan.FromSeconds(10);

    /// <summary>Default per-peer selection cache TTL.</summary>
    public static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromSeconds(30);

    private readonly IPeerTransport? _tier1;
    private readonly IReadOnlyList<IPeerTransport> _tier2; // ordered: registration-order, then AdapterName ascending
    private readonly IPeerTransport _tier3;
    private readonly TimeProvider _time;
    private readonly TimeSpan _t1Budget;
    private readonly TimeSpan _t2Budget;
    private readonly TimeSpan _t3Budget;
    private readonly TimeSpan _cacheTtl;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TenantId _tenantId;

    private readonly Dictionary<PeerId, (IPeerTransport Transport, DateTimeOffset CachedAt)> _cache = new();
    private readonly object _cacheLock = new();

    /// <summary>
    /// Builds a selector from the supplied transports. The Tier-3
    /// fallback is required (a Bridge relay must always be available
    /// per ADR 0061 §"Decision"). Multiple Tier-1 transports are not
    /// supported — the first <see cref="TransportTier.LocalNetwork"/>
    /// transport in <paramref name="transports"/> wins; subsequent ones
    /// are ignored.
    /// </summary>
    public DefaultTransportSelector(
        IEnumerable<IPeerTransport> transports,
        TimeProvider? time = null,
        TimeSpan? tier1Budget = null,
        TimeSpan? tier2Budget = null,
        TimeSpan? tier3Budget = null,
        TimeSpan? cacheTtl = null)
        : this(transports, auditTrail: null, signer: null, tenantId: default,
               time, tier1Budget, tier2Budget, tier3Budget, cacheTtl, validateAudit: false)
    {
    }

    /// <summary>
    /// Audit-enabled overload — emits <see cref="AuditEventType.TransportTierSelected"/> on
    /// every Tier-1 / Tier-2 selection, <see cref="AuditEventType.MeshTransportFailed"/>
    /// when a Tier-2 adapter fails to resolve a peer, and
    /// <see cref="AuditEventType.TransportFallbackToRelay"/> on Tier-3 fallback (per ADR 0061
    /// §"Audit emission"). <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/> +
    /// <see cref="TenantId"/> follow the W#32 both-or-neither pattern: all three required
    /// together, validated at construction.
    /// </summary>
    public DefaultTransportSelector(
        IEnumerable<IPeerTransport> transports,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        TenantId tenantId,
        TimeProvider? time = null,
        TimeSpan? tier1Budget = null,
        TimeSpan? tier2Budget = null,
        TimeSpan? tier3Budget = null,
        TimeSpan? cacheTtl = null)
        : this(transports, auditTrail, signer, tenantId,
               time, tier1Budget, tier2Budget, tier3Budget, cacheTtl, validateAudit: true)
    {
    }

    private DefaultTransportSelector(
        IEnumerable<IPeerTransport> transports,
        IAuditTrail? auditTrail,
        IOperationSigner? signer,
        TenantId tenantId,
        TimeProvider? time,
        TimeSpan? tier1Budget,
        TimeSpan? tier2Budget,
        TimeSpan? tier3Budget,
        TimeSpan? cacheTtl,
        bool validateAudit)
    {
        ArgumentNullException.ThrowIfNull(transports);
        if (validateAudit)
        {
            ArgumentNullException.ThrowIfNull(auditTrail);
            ArgumentNullException.ThrowIfNull(signer);
            if (tenantId == default)
            {
                throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
            }
        }
        var ordered = transports.ToList();

        _tier1 = ordered.FirstOrDefault(t => t.Tier == TransportTier.LocalNetwork);
        _tier2 = ordered
            .Where(t => t.Tier == TransportTier.MeshVpn)
            .Select((t, configPriority) => (t, configPriority))
            .OrderBy(x => x.configPriority)
            .ThenBy(x => (x.t as IMeshVpnAdapter)?.AdapterName ?? string.Empty, StringComparer.Ordinal)
            .Select(x => x.t)
            .ToList();
        _tier3 = ordered.FirstOrDefault(t => t.Tier == TransportTier.ManagedRelay)
            ?? throw new ArgumentException(
                "DefaultTransportSelector requires at least one TransportTier.ManagedRelay transport (Tier 3 is the always-tried fallback per ADR 0061).",
                nameof(transports));

        _time = time ?? TimeProvider.System;
        _t1Budget = tier1Budget ?? DefaultTier1Budget;
        _t2Budget = tier2Budget ?? DefaultTier2Budget;
        _t3Budget = tier3Budget ?? DefaultTier3Budget;
        _cacheTtl = cacheTtl ?? DefaultCacheTtl;

        _auditTrail = auditTrail;
        _signer = signer;
        _tenantId = tenantId;
    }

    /// <inheritdoc />
    public async Task<IPeerTransport> SelectAsync(PeerId peer, CancellationToken ct)
    {
        if (TryGetCached(peer, out var cached))
        {
            return cached;
        }

        if (_tier1 is not null && _tier1.IsAvailable)
        {
            var resolved = await TryResolveAsync(_tier1, peer, _t1Budget, ct).ConfigureAwait(false);
            if (resolved is not null)
            {
                CacheTransport(peer, _tier1);
                await EmitAsync(AuditEventType.TransportTierSelected,
                    TransportAuditPayloads.TierSelected(peer, TransportTier.LocalNetwork, adapterName: null), ct).ConfigureAwait(false);
                return _tier1;
            }
        }

        foreach (var t2 in _tier2)
        {
            if (!t2.IsAvailable)
            {
                continue;
            }
            var resolved = await TryResolveAsync(t2, peer, _t2Budget, ct).ConfigureAwait(false);
            var adapterName = (t2 as IMeshVpnAdapter)?.AdapterName;
            if (resolved is not null)
            {
                CacheTransport(peer, t2);
                await EmitAsync(AuditEventType.TransportTierSelected,
                    TransportAuditPayloads.TierSelected(peer, TransportTier.MeshVpn, adapterName), ct).ConfigureAwait(false);
                return t2;
            }
            await EmitAsync(AuditEventType.MeshTransportFailed,
                TransportAuditPayloads.MeshTransportFailed(peer, adapterName ?? string.Empty, "resolve-miss"), ct).ConfigureAwait(false);
        }

        // Tier 3 is always tried as last resort. Resolve attempt within budget;
        // even if ResolvePeerAsync returns null (e.g., Bridge relay can't
        // confirm peer up-front), we still return the Tier-3 transport — the
        // caller's ConnectAsync is the authoritative liveness check per the
        // ADR 0061 §"Decision" Tier-3 contract (always-tried, ciphertext-only
        // last-resort).
        var t3Resolved = await TryResolveAsync(_tier3, peer, _t3Budget, ct).ConfigureAwait(false);
        CacheTransport(peer, _tier3);
        await EmitAsync(AuditEventType.TransportFallbackToRelay,
            TransportAuditPayloads.TransportFallbackToRelay(peer, t3Resolved is null ? "Failed" : "Selected"), ct).ConfigureAwait(false);
        return _tier3;
    }

    /// <summary>
    /// Drops the cached selection for <paramref name="peer"/>. Called by
    /// audit-emission integrations in P5+ on
    /// <c>MeshTransportFailed</c> / <c>MeshHandshakeCompleted</c> events
    /// for the cached peer.
    /// </summary>
    public void Invalidate(PeerId peer)
    {
        lock (_cacheLock)
        {
            _cache.Remove(peer);
        }
    }

    private static async Task<PeerEndpoint?> TryResolveAsync(IPeerTransport transport, PeerId peer, TimeSpan budget, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(budget);
        try
        {
            return await transport.ResolvePeerAsync(peer, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Per-tier budget exhausted — fall through to next tier.
            return null;
        }
        catch (Exception)
        {
            // Any transport error → fall through. The selection algorithm
            // (ADR 0061 §"Tier selection algorithm") treats unreachable /
            // misconfigured transports as fall-through candidates rather
            // than terminal failures.
            return null;
        }
    }

    private bool TryGetCached(PeerId peer, out IPeerTransport transport)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(peer, out var entry))
            {
                if (_time.GetUtcNow() - entry.CachedAt < _cacheTtl)
                {
                    transport = entry.Transport;
                    return true;
                }
                _cache.Remove(peer);
            }
        }
        transport = null!;
        return false;
    }

    private void CacheTransport(PeerId peer, IPeerTransport transport)
    {
        lock (_cacheLock)
        {
            _cache[peer] = (transport, _time.GetUtcNow());
        }
    }

    private async Task EmitAsync(AuditEventType eventType, AuditPayload payload, CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null)
        {
            return;
        }
        var occurredAt = _time.GetUtcNow();
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: _tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }
}
