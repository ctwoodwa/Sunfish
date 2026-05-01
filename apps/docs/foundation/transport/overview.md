# Foundation.Transport substrate

`Sunfish.Foundation.Transport` is the foundation-tier substrate for the three-tier peer transport stack — the building block behind same-LAN sync, mesh-VPN-routed sync across networks, and last-resort Bridge-relayed sync for peers behind symmetric NATs.

It implements [ADR 0061](../../../docs/adrs/0061-three-tier-peer-transport.md) (Three-Tier Peer Transport).

## What it gives you

| Type | Role |
|---|---|
| `TransportTier` | Three-value discriminator: `LocalNetwork` (Tier 1, mDNS) / `MeshVpn` (Tier 2, WireGuard) / `ManagedRelay` (Tier 3, Bridge HTTPS relay). |
| `IPeerTransport` | Per-tier strategy. Stateless w.r.t. peers; caching is the selector's job. |
| `IDuplexStream` | The connected byte-stream returned by `ConnectAsync`. `IAsyncDisposable`; back-end can be a TCP socket, a WireGuard tunnel, or a WebSocket. |
| `ITransportSelector` | Picks the best `IPeerTransport` for a peer per the failover order (T1 → T2 → T3). |
| `IMeshVpnAdapter` | Tier-2 specialization — `IPeerTransport` + `RegisterDeviceAsync` / `GetMeshStatusAsync` for the mesh control plane. One per `providers-mesh-*` adapter. |
| `PeerEndpoint` / `MeshNodeStatus` / `MeshPeer` / `MeshDeviceRegistration` | Value records on the wire. |
| `MdnsPeerTransport` | Tier-1 reference impl backed by `Makaretu.Dns.Multicast.New`. |
| `BridgeRelayPeerTransport` | Tier-3 reference impl over `ClientWebSocket`. |
| `DefaultTransportSelector` | Reference selector with A4-pinned timeouts + 30-second cache. |

## Three-tier failover order

The selector tries transports in this order; first success wins:

1. **Tier 1 — `LocalNetwork` (mDNS).** Connect budget: 2s. Same-LAN, zero-config.
2. **Tier 2 — `MeshVpn`.** Connect budget: 5s per adapter. Mesh adapters iterate in registration order (operator-set "config priority"); `AdapterName` lexicographic is the tie-break — `headscale` < `netbird` < `tailscale` < `wireguard-manual`. `IsAvailable == false` adapters are skipped without consuming budget.
3. **Tier 3 — `ManagedRelay` (Bridge).** Connect budget: 10s. Always-tried last resort; ciphertext-only per ADR 0031.

Per-handshake timeout: 2s (caps the per-tier budget on inner handshake attempts). All budgets are constructor parameters — defaults match A4.

The selector emits per-peer best-tier independently: in a sync round across N peers, some peers may resolve Tier-1, others Tier-2 (possibly via different adapters), others Tier-3. A Tier-3 fallback for peer P does NOT downgrade peer Q's already-resolved Tier-1 selection.

## Selection cache

Per-peer selection result (which `IPeerTransport` won) caches for 30 seconds (configurable). Cache key is `PeerId`; cache value is `(IPeerTransport, ResolvedAt)`. `Invalidate(PeerId)` drops a peer's entry — wired by audit-event integrations on `MeshTransportFailed` / `MeshHandshakeCompleted` for the cached peer.

## API at a glance

```csharp
// Bootstrap (audit-disabled — test/bootstrap)
services.AddBridgeRelay(new BridgeRelayOptions
{
    RelayUrl = new Uri("wss://relay.bridge.example.com/sync"),
});
services.AddSunfishTransport();

// Bootstrap (audit-enabled — production; both IAuditTrail + IOperationSigner
// must already be registered)
services.AddSunfishTransport(currentTenantId);

// Use the selector
var selector = sp.GetRequiredService<ITransportSelector>();
var transport = await selector.SelectAsync(peerId, ct);
await using var stream = await transport.ConnectAsync(peerId, ct);
// kernel-sync's HandshakeProtocol runs on top of `stream.Stream`.
```

Hosts that want Tier 1 + Tier 2 register the relevant transports independently — `MdnsPeerTransport` for link-local, one or more `IMeshVpnAdapter` (e.g., `HeadscaleMeshAdapter` from `providers-mesh-headscale`) for mesh-VPN. The selector picks them up via `IEnumerable<IPeerTransport>`.

## Audit emission

Five new `AuditEventType` discriminators ship with this substrate (per ADR 0049 + ADR 0061):

| Event type | Emitted by |
|---|---|
| `TransportTierSelected` | `DefaultTransportSelector` on each tier-1 / tier-2 selection. |
| `MeshDeviceRegistered` | `IMeshVpnAdapter.RegisterDeviceAsync` (per-adapter). |
| `MeshHandshakeCompleted` | Reserved for future Tier-2 handshake-bookkeeping; factory shipped now. |
| `MeshTransportFailed` | `DefaultTransportSelector` when a Tier-2 adapter returns null on resolve. |
| `TransportFallbackToRelay` | `DefaultTransportSelector` on T3 fallback. Outcome `"Selected"` if T3 resolved, `"Failed"` otherwise. |

Payload bodies are alphabetized + opaque to the substrate (mirrors the `VersionVectorAuditPayloads` + `TaxonomyAuditPayloadFactory` conventions). Audit emission is opt-in: pass `IAuditTrail` + `IOperationSigner` + `TenantId` to the audit-enabled overloads. Without them, the selector + adapters still work — they just don't emit.

## Cohort discipline — pre-merge council on substrate ADRs

ADR 0061 was authored before the cohort batting-average lesson surfaced (per `feedback_decision_discipline.md`); its A1–A10 amendments came from a normal review cycle rather than a pre-merge council. Future substrate ADRs amending this surface (transport-layer protocol changes, new tier semantics, A11+ on ADR 0061 itself) go through pre-merge council per the canonical pattern.

## Phase 2.1 scope

What ships in this substrate:

- Contracts (`IPeerTransport` / `ITransportSelector` / `IMeshVpnAdapter` / `IDuplexStream`) + value records.
- `DefaultTransportSelector` with A4-pinned timeouts + 30s cache + lexicographic mesh-adapter tie-break.
- `MdnsPeerTransport` (Tier 1) backed by `Makaretu.Dns.Multicast.New`.
- `BridgeRelayPeerTransport` (Tier 3) over `ClientWebSocket`.
- 5 new `AuditEventType` constants + `TransportAuditPayloads` factory.
- `AddSunfishTransport()` + `AddBridgeRelay()` DI extensions.
- `Sunfish.Providers.Mesh.Headscale` first Tier-2 adapter (BSD-3 license posture).

Deferred follow-ups:

- Tailscale / NetBird / WireGuard plain adapters (separate `providers-mesh-*` packages).
- Bridge-hosted Headscale module (SPECULATIVE-PENDING-MEASUREMENT per ADR 0061 amendment A4).
- iOS-specific transport (W#23 hand-off territory; native NetworkExtension shipping is uncosted).
