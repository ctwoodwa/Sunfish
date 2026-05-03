# Workstream #30 — Three-Tier Peer Transport (Mesh VPN) — Stage 06 hand-off

**Workstream:** #30 (Mesh-VPN cross-network transport — adjacent to property-ops cluster)
**Spec:** [ADR 0061](../../docs/adrs/0061-three-tier-peer-transport.md) (Accepted 2026-04-29; A1–A10 amendments landed via PR #299)
**Pipeline variant:** `sunfish-feature-change` (new substrate `Foundation.Transport` + `providers-mesh-headscale` first adapter)
**Estimated effort:** 14–18 hours focused sunfish-PM time
**Decomposition:** 8 phases shipping as ~6 PRs
**Prerequisites:** ADR 0061 amendments landed ✓ (PR #299; `NodeId` → `PeerId` rename, `IDuplexStream` introduced); ADR 0013 provider-neutrality enforcement gate ✓ (PR #196); ADR 0031 Bridge accelerator ✓

**Phase 2.1 scope:** Tier 1 (mDNS) + Tier 2 (Headscale-only, vendor-pluggable) + Tier 3 (Bridge relay reuse). SendGrid/Tailscale/NetBird adapters deferred. Bridge-hosted Headscale (Phase 2.3+) is SPECULATIVE-PENDING-MEASUREMENT per ADR 0061 amendment A4 — NOT in this hand-off.

---

## Scope summary

Build the Phase 2.1 three-tier peer transport substrate end-to-end:

1. **`Foundation.Transport` package** — `IPeerTransport`, `ITransportSelector`, `IMeshVpnAdapter`, `IDuplexStream`, `TransportTier` enum, `PeerEndpoint`, `MeshNodeStatus`, `MeshPeer`, `MeshDeviceRegistration`
2. **`providers-mesh-headscale`** — first vendor adapter (BSD-3 Headscale-Sharp / REST client wrapper)
3. **Tier 1 (mDNS) implementation** — `MdnsPeerTransport` reusing existing peer-discovery code if present
4. **Tier 3 (Bridge relay) implementation** — `BridgeRelayPeerTransport` reusing ADR 0031 ciphertext-only relay
5. **`DefaultTransportSelector`** — per-tier wall-clock budgets (T1: 2s, T2: 5s, T3: 10s connect; per-handshake 2s); tie-break + partial-failure semantics per A2
6. **Audit emission** — 5 new `AuditEventType` constants per ADR 0049
7. **Provider-neutrality enforcement** — `BannedSymbols.txt` for providers-mesh-* per ADR 0013
8. **apps/docs + ledger flip**

**NOT in scope:** Tailscale / NetBird / WireGuard plain adapters (deferred follow-ups); Bridge-hosted Headscale module (SPECULATIVE per A4); iOS-specific transport (W#23 hand-off owns).

---

## Phases

### Phase 1 — `Foundation.Transport` contracts (~2–3h)

Audit-first: confirm `packages/foundation-transport/` doesn't exist (per `feedback_audit_existing_blocks_before_handoff`).

Per ADR 0061 §"Initial contract surface" + A1 (PeerId rename) + A4 (timeout pinning):

```csharp
namespace Sunfish.Foundation.Transport;

public enum TransportTier
{
    LocalNetwork,        // Tier 1: mDNS / link-local
    MeshVpn,             // Tier 2: WireGuard mesh
    ManagedRelay,        // Tier 3: Bridge HTTPS relay
}

public interface IPeerTransport
{
    TransportTier Tier { get; }
    bool IsAvailable { get; }
    Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct);
    Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct);
}

public sealed record PeerEndpoint
{
    public required PeerId Peer { get; init; }
    public required IPEndPoint Endpoint { get; init; }
    public required TransportTier Tier { get; init; }
    public required DateTimeOffset DiscoveredAt { get; init; }
    public DateTimeOffset? LastSeenAt { get; init; }
}

public interface IDuplexStream : IAsyncDisposable
{
    Stream Stream { get; }
    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct);
    Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct);
    Task FlushAsync(CancellationToken ct);
}

public interface ITransportSelector
{
    Task<IPeerTransport> SelectAsync(PeerId peer, CancellationToken ct);
}

public interface IMeshVpnAdapter : IPeerTransport
{
    string AdapterName { get; }
    Task<MeshNodeStatus> GetMeshStatusAsync(CancellationToken ct);
    Task RegisterDeviceAsync(MeshDeviceRegistration registration, CancellationToken ct);
}

public sealed record MeshNodeStatus
{
    public required bool IsConnected { get; init; }
    public required IReadOnlyList<MeshPeer> Peers { get; init; }
    public DateTimeOffset? LastHandshakeAt { get; init; }
}

public sealed record MeshPeer
{
    public required PeerId Peer { get; init; }
    public required IPEndPoint MeshEndpoint { get; init; }
    public required DateTimeOffset LastHandshakeAt { get; init; }
}

public sealed record MeshDeviceRegistration
{
    public required string DeviceId { get; init; }                    // mesh-control-plane-issued (per A1)
    public required PeerId Peer { get; init; }                        // Sunfish federation peer (per A1)
    public required string DeviceName { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
}
```

**Note A1 cited-symbol verification:** before marking AP-21 clean, run:

```bash
git grep -E "(class|record|interface|enum) +PeerId" packages/
git grep -E "(class|record|interface) +IDuplexStream" packages/
```

If `PeerId` exists at `packages/federation-common/PeerId.cs`: import + use. If `IDuplexStream` doesn't exist: this hand-off introduces it (per ADR 0061 amendment A1 surface drift resolution).

**Gate:** package builds; XML doc + nullability + `required` complete; `IPeerTransport` + `IDuplexStream` + `ITransportSelector` + `IMeshVpnAdapter` consumable from downstream.

**PR title:** `feat(foundation-transport): Phase 2.1 contracts (ADR 0061 A1)`

### Phase 2 — `DefaultTransportSelector` per A4 timeout discipline (~1–2h)

Per ADR 0061 amendment A4 (failover semantics):

- T1 (mDNS) connect budget: 2s
- T2 (mesh) connect budget: 5s
- T3 (Bridge relay) connect budget: 10s
- Per-handshake timeout: 2s
- Adapter iteration order: config priority + lexicographic tie-break
- Partial failure: per-peer best tier (some peers Tier-2, some Tier-3 within same sync round)
- Most-recently-handshaked tie-break for multi-mesh-peer matches
- Selection caching: 30s TTL per peer

Implementation:

```csharp
public sealed class DefaultTransportSelector : ITransportSelector
{
    private readonly IReadOnlyList<IPeerTransport> _orderedTransports; // T1, T2..., T3
    private readonly TimeSpan _t1Budget = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _t2Budget = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _t3Budget = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _handshakeBudget = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(30);

    private readonly Dictionary<PeerId, (IPeerTransport Transport, DateTimeOffset CachedAt)> _cache = new();
    private readonly object _cacheLock = new();

    public async Task<IPeerTransport> SelectAsync(PeerId peer, CancellationToken ct)
    {
        // Cache check
        if (TryGetCached(peer, out var cached)) return cached;

        // Try Tier 1
        var t1 = _orderedTransports.FirstOrDefault(t => t.Tier == TransportTier.LocalNetwork);
        if (t1 is not null)
        {
            var endpoint = await TryWithBudget(t1, peer, _t1Budget, ct);
            if (endpoint is not null) { CacheTransport(peer, t1); return t1; }
        }

        // Try Tier 2 (any mesh adapter; iterate by config priority + lexicographic)
        var t2Adapters = _orderedTransports
            .Where(t => t.Tier == TransportTier.MeshVpn)
            .OrderBy(t => (t as IMeshVpnAdapter)?.AdapterName ?? string.Empty)
            .ToList();
        foreach (var t2 in t2Adapters)
        {
            var endpoint = await TryWithBudget(t2, peer, _t2Budget, ct);
            if (endpoint is not null) { CacheTransport(peer, t2); return t2; }
        }

        // Fall through to Tier 3
        var t3 = _orderedTransports.First(t => t.Tier == TransportTier.ManagedRelay);
        CacheTransport(peer, t3);
        return t3;
    }

    private async Task<PeerEndpoint?> TryWithBudget(IPeerTransport t, PeerId peer, TimeSpan budget, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(budget);
        try { return await t.ResolvePeerAsync(peer, cts.Token); }
        catch (OperationCanceledException) { return null; }
        catch (Exception) { return null; } // any failure → fall through
    }

    // Cache TTL helpers omitted for brevity
}
```

**Gate:** unit tests cover: T1-fast-path, T1-fail-then-T2, T1+T2-fail-fall-to-T3, multi-mesh-adapter iteration order, cache hit/miss/expiry, per-peer best-tier (different peers different tiers in same selector instance).

**PR title:** `feat(foundation-transport): DefaultTransportSelector with timeout discipline (ADR 0061 A4)`

### Phase 3 — Tier 1 mDNS implementation (~2–3h)

Implement `MdnsPeerTransport` using existing mDNS code if present (`git grep mdns packages/`); otherwise use `Zeroconf` NuGet (BSD-licensed). Tier 1 emits records advertising the local Sunfish peer's endpoint; resolves by querying for a known service type (e.g., `_sunfish._tcp.local.`).

**Gate:** local-LAN end-to-end test (peer A advertises; peer B resolves; round-trip handshake within T1 budget).

**PR title:** `feat(foundation-transport): MdnsPeerTransport (Tier 1)`

### Phase 4 — `providers-mesh-headscale` adapter (~3–4h)

New package per ADR 0013 provider-neutrality. Use Headscale-Sharp if mature; otherwise `HttpClient` against documented Headscale REST API.

```csharp
namespace Sunfish.Providers.Mesh.Headscale;

public sealed class HeadscaleMeshAdapter : IMeshVpnAdapter
{
    public string AdapterName => "headscale";
    public TransportTier Tier => TransportTier.MeshVpn;
    public bool IsAvailable => /* check Headscale control-plane reachability */;

    // Implements IPeerTransport.ResolvePeerAsync via Headscale node-list query
    // Implements IPeerTransport.ConnectAsync via local WireGuard kernel module
    //   (kernel WireGuard config managed by Headscale client; we just use it)
    // Implements IMeshVpnAdapter.RegisterDeviceAsync via Headscale `nodes register` API
    // Implements IMeshVpnAdapter.GetMeshStatusAsync via `nodes list`
}
```

**Provider-neutrality compliance per ADR 0013:**
- `BannedSymbols.txt` allow-list for Headscale-Sharp / WireGuard kernel types ONLY in this package
- `SUNFISH_PROVNEUT_001` analyzer auto-attaches; verifies no Headscale imports leak into `blocks-*`

**Gate:** Headscale-Sharp / REST client passes adapter unit tests against fixtures (no live Headscale needed; record/replay or stub); provider-neutrality analyzer passes.

**PR title:** `feat(providers-mesh-headscale): Phase 2.1 first adapter (ADR 0061 + 0013)`

### Phase 5 — Tier 3 Bridge relay reuse (~1–2h)

Wrap existing Bridge ciphertext-only relay (ADR 0031) as `BridgeRelayPeerTransport`. Substantively a thin adapter: existing relay code shipped via Bridge; this transport just speaks the relay's HTTPS API.

**Gate:** Tier 3 fallback works when T1 + T2 both unavailable; ciphertext-only posture preserved (no plaintext exposure).

**PR title:** `feat(foundation-transport): BridgeRelayPeerTransport (Tier 3 reuse of ADR 0031)`

### Phase 6 — Audit emission (~1–2h)

Add 5 new `AuditEventType` constants:

```csharp
public static readonly AuditEventType TransportTierSelected = new("TransportTierSelected");
public static readonly AuditEventType MeshDeviceRegistered = new("MeshDeviceRegistered");
public static readonly AuditEventType MeshHandshakeCompleted = new("MeshHandshakeCompleted");
public static readonly AuditEventType MeshTransportFailed = new("MeshTransportFailed");
public static readonly AuditEventType TransportFallbackToRelay = new("TransportFallbackToRelay");
```

Author `TransportAuditPayloadFactory` per established pattern. Wire `DefaultTransportSelector` to emit on each tier-selection; wire `HeadscaleMeshAdapter` to emit on each registration / handshake / failure.

**Gate:** 5 event types ship; factory works; audit emission verified.

**PR title:** `feat(foundation-transport): audit emission — 5 AuditEventType + factory`

### Phase 7 — Cross-package wiring + apps/docs (~1.5h)

- Verify sync daemon (`Sunfish.Kernel.Sync`) consumes `ITransportSelector` for peer-connect flow
- `apps/docs/foundation/transport/overview.md` — three-tier model + adapter selection guide
- `apps/docs/foundation/transport/headscale-setup.md` — operator guide for self-hosted Headscale

**Gate:** end-to-end test: same-LAN sync (Tier 1); cross-network sync (Tier 2); behind-symmetric-NAT sync (Tier 3 fallback). All work in InMemory mode.

**PR title:** `feat(foundation-transport): cross-package wiring + apps/docs`

### Phase 8 — Ledger flip (~0.5h)

Update `icm/_state/active-workstreams.md` row #30 → `built`. Append last-updated entry.

---

## Total decomposition

| Phase | Subject | Hours |
|---|---|---|
| 1 | Foundation.Transport contracts | 2–3 |
| 2 | DefaultTransportSelector (timeouts + tie-break + cache) | 1–2 |
| 3 | Tier 1 mDNS implementation | 2–3 |
| 4 | providers-mesh-headscale first adapter | 3–4 |
| 5 | Tier 3 Bridge relay reuse | 1–2 |
| 6 | Audit emission (5 AuditEventType) | 1–2 |
| 7 | Cross-package wiring + apps/docs | 1.5 |
| 8 | Ledger flip | 0.5 |
| **Total** | | **12–18h** |

---

## Halt conditions

- **`PeerId` doesn't exist at `packages/federation-common/PeerId.cs`** → write `cob-question-*` beacon; XO either points at the correct path or stubs it ahead of full federation work
- **Headscale-Sharp library deprecated / abandoned** → fall back to direct REST client wrapper using documented Headscale API; same interface
- **WireGuard kernel module not available on dev machine** (Linux-only kernel; macOS uses userspace via `wireguard-go`; Windows uses kernel driver) → adapter test mocks the kernel layer; local-LAN integration test only on Linux
- **`Sunfish.Kernel.Sync` doesn't expose `ITransportSelector` injection point** at Phase 7 → halt; XO authors a sync-daemon-extension addendum
- **License posture violation surfaces** (a Headscale dependency pulls in SSPL/BSL) → halt; either swap dependency or escalate to CO

---

## Acceptance criteria

- [ ] `packages/foundation-transport` builds with full XML doc + nullability + `required`
- [ ] `IPeerTransport` + `IDuplexStream` + `ITransportSelector` + `IMeshVpnAdapter` consumable from external packages
- [ ] `DefaultTransportSelector` enforces per-tier timeouts + tie-break + 30s caching
- [ ] `MdnsPeerTransport` works on local LAN (round-trip test)
- [ ] `HeadscaleMeshAdapter` passes adapter unit tests + provider-neutrality analyzer
- [ ] `BridgeRelayPeerTransport` reuses ADR 0031 relay; ciphertext-only posture preserved
- [ ] 5 new `AuditEventType` constants in kernel-audit
- [ ] `TransportAuditPayloadFactory` ships
- [ ] `apps/docs/foundation/transport/` overview + headscale-setup pages exist
- [ ] All tests pass; build clean; provider-neutrality analyzer passes (no Headscale leakage outside providers-mesh-headscale)
- [ ] Ledger row #30 → `built`

---

## References

- [ADR 0061](../../docs/adrs/0061-three-tier-peer-transport.md) — substrate spec + amendments A1–A10
- [Mesh-VPN intake](../../icm/00_intake/output/mesh-vpn-cross-network-transport-intake-2026-04-28.md)
- [ADR 0013](../../docs/adrs/0013-foundation-integrations.md) — provider-neutrality enforcement gate
- [ADR 0031](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) — Bridge as Tier 3 managed-relay
- [ADR 0049](../../docs/adrs/0049-audit-trail-substrate.md) — audit substrate
- [Foundational paper §6.1](../../_shared/product/local-node-architecture-paper.md) — three-tier transport spec this ADR pins
- [W#23 iOS Field-Capture App hand-off](./TBD) — when authored, will consume this transport substrate for iPhone direct-to-Anchor sync
