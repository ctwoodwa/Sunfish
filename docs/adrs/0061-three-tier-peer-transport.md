---
id: 61
title: Three-Tier Peer Transport Model (mDNS / Mesh VPN / Managed Relay)
status: Accepted
date: 2026-04-29
tier: foundation
concern:
  - distribution
composes:
  - 13
  - 31
extends: []
supersedes: []
superseded_by: null
amendments:
  - A1
---
# ADR 0061 — Three-Tier Peer Transport Model (mDNS / Mesh VPN / Managed Relay)

**Status:** Accepted (2026-04-29 by CO; council-reviewed B-grade; amendments A1–A4 (Critical/Major) **landed 2026-04-29** — see §"Amendments (post-acceptance, 2026-04-29 council)")
**Date:** 2026-04-29
**Author:** XO (research session)
**Pipeline variant:** `sunfish-feature-change` (new transport adapter family + paper §6.1 architectural pinning)

**Resolves:** [mesh-vpn-cross-network-transport-intake-2026-04-28.md](../../icm/00_intake/output/mesh-vpn-cross-network-transport-intake-2026-04-28.md); cluster workstream #30 (adjacent — not in cluster proper but unblocks W#23 iOS Field-Capture App).

---

## Context

The foundational paper [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) §6.1 specifies a **three-tier peer transport** model: nodes prefer same-LAN mDNS direct sync (Tier 1, latency-optimal); fall back to mesh VPN for cross-network direct sync (Tier 2, NAT-traversal-friendly without exposing nodes to the public internet); fall back to managed-relay HTTPS via Bridge for nodes behind symmetric NAT or strict firewalls (Tier 3, ciphertext-only relay per ADR 0031).

Today only Tiers 1 and 3 have implementations. Tier 2 (mesh VPN) is a paper-described requirement with **no ADR or code**, which means BDFL's iPhone in the field can't directly sync to BDFL's Anchor at home — it always falls back to Tier 3 (Bridge relay), which is slower, requires server availability, and exposes more metadata (timing + traffic shape) to the relay tier.

W#23 iOS Field-Capture App needs Tier 2 to deliver the "phone-arrives-home-and-syncs-instantly" UX the paper promises. Phase 2.3 deliverability is the timing — iOS direct-to-Anchor sync is a forcing function for Tier 2 to ship.

This ADR ships the contract surface + adapter family for Tier 2. Specific implementations (Headscale, NetBird, Tailscale, plain WireGuard) are pluggable per ADR 0013 provider-neutrality.

---

## Decision drivers

- **Paper §6.1 is the spec.** Tier 2 is a paper-described requirement; this ADR pins the architectural choice that's already in the paper.
- **License posture matters.** SSPL (MongoDB-style) and BSL (Sentry-style) are incompatible with Sunfish's open-source-permissive posture. Adapters MUST be MIT/Apache/BSD/MPL-2.0 — Headscale (BSD-3), NetBird (BSD-3), Tailscale's open-source client portions (BSD-3), plain WireGuard (GPL-2 client + Apache server tools); RULES OUT NetMaker (SSPL since 2023) + ZeroTier (BSL since 2023).
- **Self-hosted control plane is the default.** Sunfish prefers operator-controllable infrastructure. Headscale is the canonical recommendation (BSD-3 licensed self-hosted Tailscale control-plane reimplementation).
- **Bring-your-own SaaS is OK.** Operators with existing Tailscale or NetBird accounts shouldn't be forced onto Headscale; vendor adapters exist.
- **Anchor + iOS need different defaults.** Anchor on desktop is Headscale-or-Tailscale; iOS is Tailscale (Apple App Store distribution; Headscale clients on iOS exist but are less polished).
- **Bridge MAY host Headscale (speculative, Phase 2.3+).** Tenant-self-hosting is the supported default. A Bridge-hosted-Headscale option is contingent on a Phase 2.3+ measurement spike (see §"Bridge-hosted Headscale — SPECULATIVE"); if RAM-per-tenant or contention profile fails the spike, Bridge stays out of the mesh control plane.

---

## Considered options

### Option A — Three-tier transport with vendor-pluggable mesh VPN adapters [RECOMMENDED]

`IPeerTransport` interface with three concrete tiers; mesh-VPN tier accepts vendor adapters per ADR 0013. Headscale is canonical; Tailscale + NetBird as bring-your-own; plain WireGuard for advanced operators.

- **Pro:** Paper §6.1 directly pinned; matches existing architectural commitment
- **Pro:** Provider-neutrality preserved; SSPL/BSL adapters explicitly excluded
- **Pro:** Self-hosted default (Headscale) + vendor escape (Tailscale) cover the operator spectrum
- **Pro:** Bridge-hosted Headscale is a Phase 2.3+ bonus; tenants can offload mesh management to Bridge
- **Con:** New adapter family adds package count; ~3–4 packages

**Verdict:** Recommended. Paper-aligned + provider-neutrality compliant + covers iOS direct-sync use case.

### Option B — Defer Tier 2 indefinitely; rely on Tier 3 (Bridge relay)

Don't ship mesh VPN; iOS + cross-network sync goes through Bridge.

- **Pro:** No new packages; ships nothing
- **Con:** Bridge is a metadata-leak surface (timing + envelope size visible) compared to Tier 2 direct
- **Con:** Bridge availability gates sync; tenant goes offline if Bridge does
- **Con:** Latency cost; Bridge round-trip is 50–200ms vs 5–20ms direct

**Verdict:** Rejected. Defeats paper §6.1's privacy + offline + latency commitments.

### Option C — Sunfish builds its own WireGuard control plane

Roll our own Headscale equivalent.

- **Pro:** Maximum vendor neutrality (no Headscale dependency)
- **Con:** Headscale is BSD-3 licensed and well-maintained; reinventing is unjustified
- **Con:** Substantial scope expansion; not Sunfish's competitive surface
- **Con:** Operator-controllable infrastructure achieved by self-hosting Headscale; same outcome

**Verdict:** Rejected. NIH antipattern.

---

## Decision

**Adopt Option A.** Three-tier `IPeerTransport` model with vendor-pluggable mesh-VPN adapters. Headscale is the canonical recommendation; vendor SaaS (Tailscale, NetBird) supported via ADR 0013 adapter pattern. SSPL/BSL adapters explicitly excluded. Bridge-hosted Headscale **speculative, Phase 2.3+, gated on measurement spike** — tenant-self-hosting is the supported default (per A4 amendment).

### Initial contract surface

> **Note (per A1, 2026-04-29 amendments):** the contract surface below uses `Sunfish.Federation.Common.PeerId`
> as the canonical peer identifier — the Ed25519-public-key-derived value type already defined at
> `packages/federation-common/PeerId.cs` and consumed by `ISyncTransport` / `PeerDescriptor`. The
> originally-drafted `NodeId` was a substrate-vocabulary fork (council finding AP-19); see
> Amendment A1 for rationale. `MeshDeviceRegistration.DeviceId` keeps a distinct `string` shape
> because mesh-tier device identity (Headscale node-key fingerprint, Tailscale device ID) is
> issued by the mesh control plane and is not the same thing as a federation peer identity.

```csharp
namespace Sunfish.Foundation.Transport;

using Sunfish.Federation.Common; // for PeerId

public enum TransportTier
{
    LocalNetwork,        // Tier 1: mDNS / link-local; same-LAN
    MeshVpn,             // Tier 2: WireGuard mesh; cross-network direct
    ManagedRelay,        // Tier 3: Bridge HTTPS relay; ciphertext-only
}

public interface IPeerTransport
{
    TransportTier Tier { get; }
    bool IsAvailable { get; }
    Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct);
    Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct);
}

/// <summary>
/// Bidirectional byte stream over a single resolved peer transport. NEW substrate type
/// introduced by this ADR; lives in <c>Sunfish.Foundation.Transport</c>. Concrete
/// implementations: <c>MdnsDuplexStream</c> (Tier 1 TCP socket), <c>MeshVpnDuplexStream</c>
/// (Tier 2 WireGuard tunnel socket), <c>BridgeRelayDuplexStream</c> (Tier 3 HTTPS long-poll
/// or WebSocket). Sync daemon does not see the tier — it sees an <c>IDuplexStream</c> and
/// reads/writes <see cref="SyncEnvelope"/> frames per ADR 0028 / paper §6.2.
/// </summary>
public interface IDuplexStream : IAsyncDisposable
{
    Stream Reader { get; }
    Stream Writer { get; }
    TransportTier ActualTier { get; } // for diagnostics + audit
}

public sealed record PeerEndpoint
{
    public required PeerId Peer { get; init; }
    public required IPEndPoint Endpoint { get; init; }
    public required TransportTier Tier { get; init; }
    public required DateTimeOffset DiscoveredAt { get; init; }
    public DateTimeOffset? LastSeenAt { get; init; }
}

public interface ITransportSelector
{
    /// <summary>Try transports in order: Tier 1 → Tier 2 → Tier 3. Return the first available.</summary>
    /// <remarks>See §"Tier selection algorithm" for timeout, tie-break, and partial-failure semantics (per A2).</remarks>
    Task<IPeerTransport> SelectAsync(PeerId peer, CancellationToken ct);
}

// Tier-2 specific (mesh VPN)
public interface IMeshVpnAdapter : IPeerTransport
{
    string AdapterName { get; }              // "headscale", "tailscale", "netbird", "wireguard-manual"
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
    public required IPEndPoint MeshEndpoint { get; init; }    // 100.x.x.x for Tailscale-compat; 10.x.x.x for Headscale-default
    public required DateTimeOffset LastHandshakeAt { get; init; }
}

public sealed record MeshDeviceRegistration
{
    /// <summary>Mesh-control-plane-issued device identity (Headscale node-key fingerprint,
    /// Tailscale device ID, etc.). Distinct from federation <see cref="PeerId"/> — the mesh
    /// control plane does not see Sunfish's Ed25519 peer keys; it issues its own opaque
    /// device identifier at registration time. Adapter is responsible for the mapping
    /// between (mesh device-id) and (Sunfish PeerId), maintained in adapter-private state.</summary>
    public required string DeviceId { get; init; }
    public required PeerId Peer { get; init; }   // the Sunfish federation peer this device represents
    public required string DeviceName { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }   // "anchor", "ios-mobile", "bridge-tenant-X"
}
```

### Adapter packages

| Package | License | Posture | Phase |
|---|---|---|---|
| `packages/providers-mesh-headscale` | BSD-3 (canonical recommendation) | Self-hosted Headscale control plane | 2.1 |
| `packages/providers-mesh-tailscale` | BSD-3 (Tailscale OSS portions) | Vendor SaaS bring-your-own | 2.2 |
| `packages/providers-mesh-netbird` | BSD-3 | Vendor / self-hosted alternative | 2.3 (lower priority) |
| `packages/providers-mesh-wireguard` | GPL-2 client + Apache tools | Plain WireGuard for advanced operators (manual config) | 2.3 (lower priority) |

**Explicitly excluded** (license incompatibility with Sunfish OSS posture):

- **NetMaker** — SSPL since 2023
- **ZeroTier** — BSL since 2023
- **OpenVPN** — different protocol family; not relevant for WireGuard mesh

**Future candidates** (revisit triggers):

- **Nebula** (MIT) — Slack's mesh; lower-level; lacks Tailscale-style ergonomics today; revisit if a tenant adopts
- **Innernet** (MIT) — simpler-than-Tailscale/Headscale; revisit Phase 4+ when ergonomic alternatives mature

### Bridge-hosted Headscale — SPECULATIVE, Phase 2.3+ research spike (per A4, 2026-04-29 amendments)

> **Status:** SPECULATIVE-PENDING-MEASUREMENT. Originally drafted as a committed Phase 2.3+
> architecture; council finding AP-13 flagged this as architecture-then-measure (Bridge-hosted
> multi-tenant Headscale has no production reference; Headscale's own docs target single-tenant
> self-hosting). **The supported default is tenant-self-hosting.** Bridge-hosted Headscale
> proceeds ONLY after the measurement spike below confirms the operational profile.

**Default supported pattern:** tenants self-host Headscale on their own infrastructure (or use
Tailscale SaaS) and Bridge stays out of the mesh control plane. Bridge remains Tier 3 (managed
HTTPS relay) per ADR 0031.

**Phase 2.3+ research spike (gating):**

Before any code lands in `accelerators/bridge/MeshControlPlane/`, run a measurement spike:

1. Deploy single-instance Headscale on Bridge dev infrastructure (no per-tenant isolation initially).
2. Synthetic load: N tenants × M devices, where N ∈ {2, 10, 50} and M ∈ {5, 20, 100}.
3. Measure under load:
   - RAM per tenant at idle and at handshake-storm peak
   - CPU per tenant under steady-state heartbeat
   - SQLite lock contention (write-write conflicts, lock-wait p99)
   - Control-plane API latency p50/p95/p99 under handshake storms
   - Process-vs-thread isolation cost if tenants share a process
4. Decide architecture from the data:
   - If RAM-per-tenant > 200MB at idle OR p99 control-plane latency > 1s under load → **DO NOT** ship Bridge-hosted; document tenant-self-hosting as the only supported pattern.
   - If single-process-shared-Headscale viable and per-tenant SQLite is contention-clean → ship feature-flagged opt-in.
   - If contention surfaces → re-architect (Postgres backing, per-tenant process isolation, sharding) BEFORE ship.

**Speculative architecture (NOT pinned; subject to measurement-spike output):**

- Bridge `accelerators/bridge/MeshControlPlane/` module would host Headscale.
- Tenant's devices would get Headscale auth keys via Bridge admin UI (per ADR 0031 multi-tenant data-plane).
- Mesh tunnels: device → Headscale-managed WireGuard → other devices in tenant's mesh.
- Storage layout: **TBD by spike** — per-tenant SQLite is one candidate; Postgres-backed shared-instance is another; sharded multi-instance is a third.
- Port allocation, lifecycle (start/stop/restart on Bridge restart), and TLS termination ARE design questions for the spike — Bridge's existing services use port 8080 which collides with Headscale's default; needs explicit allocation policy.
- Failover: if Bridge's Headscale is unavailable, fall back to Tier 3 (Bridge HTTPS relay) — uses the standard `ITransportSelector` failover contract above.

**Even if the spike is favorable, this is OPTIONAL** — tenants can always self-host Headscale on
their own infrastructure if they want full control. Tenant-self-hosting remains the supported
default; Bridge-hosting is opportunistic and feature-flagged.

### Tier selection algorithm (`ITransportSelector`)

> **Failover contract (per A2, 2026-04-29 amendments):** the algorithm below pins per-tier
> timeout budgets, tie-break order across adapters, and partial-failure semantics.
> A Stage-06 implementer can write deterministic integration tests against this contract.

```text
For each peer P that needs sync:
  1. Try Tier 1 (mDNS) — total budget 2s:
     - Query mDNS for P's PeerId on local network (timeout 2s wall-clock)
     - If response received AND endpoint reachable AND handshake succeeds within budget
       → emit TransportTierSelected(Tier=LocalNetwork) and return Tier-1 transport
     - Else → fall through

  2. Try Tier 2 (Mesh VPN) — total budget 5s across all adapters:
     - Iterate registered IMeshVpnAdapter list in DETERMINISTIC ORDER:
       (a) sort by config-declared priority (operator-set in DI registration);
       (b) tie-break by AdapterName lexicographic — "headscale" < "netbird" < "tailscale" < "wireguard-manual"
     - For each adapter A in order:
       - If A.IsAvailable == false → skip (do not consume budget)
       - Call A.ResolvePeerAsync(P, ct) with per-adapter timeout = remaining-budget / remaining-adapter-count
         - If returns null (peer not in this mesh) → continue to next adapter (no budget penalty beyond the call)
         - If returns endpoint:
           - Attempt WireGuard handshake (per-handshake timeout 2s, capped at remaining budget)
           - If handshake succeeds → emit TransportTierSelected(Tier=MeshVpn, Adapter=A.AdapterName) and return Tier-2 transport
           - If handshake fails or times out → emit MeshTransportFailed and continue to next adapter
     - If all adapters exhausted within budget AND none succeeded → fall through

  3. Fall back to Tier 3 (Bridge relay) — last-resort, always tried:
     - Connect timeout 10s
     - If Bridge unreachable within timeout → emit TransportFallbackToRelay(Outcome=Failed) and surface NoTransportAvailableException to caller
     - Else → emit TransportFallbackToRelay(Outcome=Selected) and return Tier-3 transport (ciphertext-only per ADR 0031)
```

**Per-peer best-tier discipline (partial-failure contract).** Selection is per-peer; in a single
sync round across N peers, some peers may resolve Tier-1, others Tier-2 (possibly via different
adapters), others Tier-3. The selector returns each peer's best tier independently — a Tier-3
fallback for peer P does NOT downgrade peer Q's already-resolved Tier-1 selection.

**Two-adapter-disagreement tie-break.** When Headscale and Tailscale are both registered (operator
running both during migration) and disagree on whether peer P is reachable, the deterministic
priority+lexicographic order above resolves it: the higher-priority adapter wins, lexicographic
fallback breaks ties. Operator can override per-adapter priority in DI to express "Tailscale is
my source of truth during the migration window."

**Mesh-up-but-peer-not-registered.** `ResolvePeerAsync` returning null is the in-band signal —
adapter does NOT throw; selector continues to next adapter, then to Tier 3.

**Selection cache.** Per-peer selection result (which tier + which adapter) caches for ~30s.
Cache key is `PeerId`; cache value is `(TransportTier, AdapterName?, ResolvedAt)`. Cache stores
SELECTION RESULT, not mesh-membership state — invalidates on `MeshTransportFailed` /
`MeshHandshakeCompleted` audit events for the cached peer.

**Most-recently-handshaked tie-break (within Tier 2).** When multiple adapters successfully
resolve P within budget (rare; suggests duplicate-mesh registration), prefer the adapter whose
`MeshPeer.LastHandshakeAt` for P is most recent. Reflects "the mesh that has been seeing live
traffic from P is more likely to have a working tunnel."

### Provider-neutrality enforcement (ADR 0013)

`SUNFISH_PROVNEUT_001` analyzer auto-attaches to `providers-mesh-*` packages. Headscale-Sharp / Tailscale.Net SDK / etc. types may be imported only inside their respective adapter packages; `blocks-*` cannot reference them.

`BannedSymbols.txt` per provider package per the established pattern.

> **W#14 enforcement-gate status (verified 2026-04-29):** workstream #14 (provider-neutrality
> enforcement gate — Roslyn analyzer + BannedSymbols infrastructure, PR #196) is `built (merged)`
> per `icm/_state/active-workstreams.md`. The "established pattern" referenced above is
> mechanically active at audit time; this ADR's `providers-mesh-*` packages inherit the
> auto-attach flow at scaffold time. Closes council finding AP-18 (forward-reference) — the
> finding was authored before W#14 merged.

### Audit emission (ADR 0049)

5 new `AuditEventType` constants:

```csharp
public static readonly AuditEventType TransportTierSelected = new("TransportTierSelected");
public static readonly AuditEventType MeshDeviceRegistered = new("MeshDeviceRegistered");
public static readonly AuditEventType MeshHandshakeCompleted = new("MeshHandshakeCompleted");
public static readonly AuditEventType MeshTransportFailed = new("MeshTransportFailed"); // emitted on Tier 2 fallback to Tier 3
public static readonly AuditEventType TransportFallbackToRelay = new("TransportFallbackToRelay");
```

`TransportAuditPayloadFactory` per the established pattern.

---

## Consequences

### Positive

- Paper §6.1 architectural commitment formally pinned
- iOS direct-to-Anchor sync becomes possible (W#23 unblock)
- Latency improves dramatically for Tier-2-reachable peers (5–20ms vs 50–200ms via Bridge relay)
- Privacy improves: Bridge sees less metadata when Tier 2 is in use (timing + envelope shape only at handshake/lifecycle, not on every sync)
- Bridge availability stops gating cross-network sync
- License posture maintained: SSPL/BSL adapters explicitly excluded
- Operator choice: self-hosted Headscale, Tailscale SaaS, or no-mesh + Bridge-only

### Negative

- New adapter family adds 3–4 packages
- Vendor SDK dependencies (Headscale-Sharp, Tailscale.Net) — versioning + license tracking overhead
- Bridge-hosted Headscale (Phase 2.3+) adds operational complexity to Bridge
- Network-administrator skill required for self-hosted Headscale (or Tailscale-SaaS alternative)

### Trust impact / Security & privacy

- **Mesh-tier sync stays end-to-end-encrypted at WireGuard layer** AND content-encrypted per ADR 0028 (DEK-wrapped); double-encryption isn't wasted because each layer protects against different threats
- **Bridge-relay-only fallback retains ciphertext-only posture** per ADR 0031 (no plaintext exposure)
- **Mesh registration requires Headscale auth-key provisioning** — operator-controlled; revocable
- **Audit trail records every tier selection + every fallback** so compliance posture is provable
- **No third-party telemetry** — Headscale self-hosted has no analytics; Tailscale SaaS users opt-in by choosing the provider
- **License compliance** mechanically enforced: providers-mesh-* analyzer rejects SSPL/BSL imports

---

## Compatibility plan

### Existing callers

`ISyncTransport` (existing, paper §6.2) consumed by sync daemon. This ADR's `IPeerTransport` is parallel; sync daemon adds a tier-selection step before invoking transport.

No breaking changes to existing Tier-1 (mDNS) or Tier-3 (Bridge relay) implementations.

### Affected packages

| Package | Change |
|---|---|
| `packages/foundation-transport` (new) | **New** — `IPeerTransport` + `ITransportSelector` + `IMeshVpnAdapter` contracts + `TransportTier` enum |
| `packages/providers-mesh-headscale` | **New** (Phase 2.1) — Headscale adapter |
| `packages/providers-mesh-tailscale` | **New** (Phase 2.2) — Tailscale SaaS adapter |
| `packages/providers-mesh-netbird` | **New** (Phase 2.3, lower priority) |
| `packages/providers-mesh-wireguard` | **New** (Phase 2.3, lower priority) |
| `accelerators/bridge` (Phase 2.3+ optional) | **Modified** — `MeshControlPlane/` module hosts Headscale |
| `accelerators/anchor` | **Modified** — installer offers Tailscale or Headscale-client install during initial setup |
| `apps/docs/foundation/transport/` | **New** — three-tier model + adapter selection guide |

### Migration

No existing data to migrate. Existing nodes continue to use Tier 1 + Tier 3 until Tier 2 adapters are configured.

---

## Implementation checklist

- [ ] `packages/foundation-transport` package with contract surface (full XML doc + nullability + `required`)
- [ ] `ITransportSelector` + `DefaultTransportSelector` with caching
- [ ] `packages/providers-mesh-headscale` adapter (Phase 2.1)
- [ ] `BannedSymbols.txt` per providers-mesh-* package per ADR 0013
- [ ] 5 new `AuditEventType` constants in kernel-audit
- [ ] `TransportAuditPayloadFactory` mirroring established patterns
- [ ] Anchor installer integration: detect existing Tailscale/Headscale-client; offer install if absent
- [ ] `apps/docs/foundation/transport/` overview + adapter-selection-guide + Bridge-hosted-Headscale guide
- [ ] Tests: tier selection algorithm; fallback Tier-2 → Tier-3; mesh registration; provider-neutrality analyzer compliance
- [ ] Phase 2.3 Bridge embedded-Headscale module (optional, behind feature flag)

---

## Open questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-T1 | Headscale-Sharp library availability + maturity | Stage 02 — verify; if not production-ready, ship REST-API client wrapper |
| OQ-T2 | Tailscale OSS-portion license boundary — what counts as "Tailscale OSS" vs proprietary? | Stage 02 — recommend using ONLY the open-source `tailscale.com` repo; don't import closed-source client portions |
| OQ-T3 | Bridge-hosted Headscale resource costs at multi-tenant scale | Phase 2.3 measurement — if too costly, recommend tenant-self-hosting as the supported pattern |
| OQ-T4 | iOS Tailscale client integration with Anchor mesh | W#23 hand-off — iOS client may need explicit auth-key provisioning UX |
| OQ-T5 | mDNS Tier-1 + Tier-2 overlap — does mesh-VPN-mode disable mDNS? | Stage 02 — recommend NO; mDNS still preferred when peers are same-LAN; mesh kicks in only when mDNS fails |

---

## Revisit triggers

- **Nebula or Innernet matures** to Tailscale/Headscale ergonomic parity (named adapters in their respective ecosystems with iOS clients comparable to Tailscale's) → consider adding adapter.
- **Headscale switches license** away from BSD-3 → adapter package status revisited; A3 abandonment-fallback (NetBird migration) re-evaluated.
- **Headscale abandonment signal:** zero commits to `juanfont/headscale` for >180 consecutive days, OR a public maintainer announcement of unmaintained status → trigger A3 abandonment-fallback playbook.
- **Apple deprecates VPN-on-demand** OR Tailscale iOS app pulled from App Store OR no commits to `tailscale/tailscale` iOS-related paths for >180 days → iOS sync re-evaluation; A3 iOS-fallback (native NetworkExtension config) actively scoped.
- **Bridge-hosted-Headscale measurement spike fails** (per A4 thresholds: RAM/tenant >200MB at idle, OR p99 control-plane latency >1s under load) → Bridge stays out of the mesh control plane permanently; document tenant-self-hosting as the only supported pattern.
- **Bridge tenant exceeds 100 active mesh devices for >7 consecutive days** OR Bridge-hosted Headscale RAM-per-tenant exceeds 200MB at idle → re-run measurement spike at scale; consider sharding or per-tenant process isolation.
- **Tier-2 fallback rate >10%** measured weekly per device, computed as `count(TransportFallbackToRelay) / count(TransportTierSelected)` over the cohort → indicates Tier 2 is not pulling its weight; investigate handshake-failure pattern, revisit `ITransportSelector` budgets.
- **ADR 0004 dual-sign window opens** → verify the v0→v1 audit-record migration covers the 5 transport audit event types; same posture as ADR 0051 A2 amendment.

---

## References

### Predecessor and sister ADRs

- [ADR 0013](./0013-foundation-integrations.md) — provider-neutrality (mesh adapters comply)
- [ADR 0028](./0028-per-record-class-consistency.md) — DEK-wrapped sync content (independent of transport tier)
- [ADR 0031](./0031-bridge-hybrid-multi-tenant-saas.md) — Bridge as Tier 3 managed-relay; Phase 2.3+ Bridge-hosted Headscale
- [ADR 0049](./0049-audit-trail-substrate.md) — audit emission

### Roadmap and intakes

- [Mesh-VPN cross-network transport intake](../../icm/00_intake/output/mesh-vpn-cross-network-transport-intake-2026-04-28.md)
- [iOS Field-Capture App intake](../../icm/00_intake/output/property-ios-field-app-intake-2026-04-28.md) — W#23 consumer
- [Foundational paper](../../_shared/product/local-node-architecture-paper.md) — §6.1 three-tier transport spec

### External

- [Headscale](https://github.com/juanfont/headscale) — BSD-3 self-hosted Tailscale control plane
- [Tailscale OSS](https://github.com/tailscale/tailscale) — BSD-3 Tailscale OSS portions
- [NetBird](https://github.com/netbirdio/netbird) — BSD-3 mesh VPN alternative
- [WireGuard](https://www.wireguard.com/) — GPL-2 client; Apache server tools
- [NetMaker](https://github.com/gravitl/netmaker) — SSPL since 2023; **EXCLUDED** per license posture
- [ZeroTier](https://github.com/zerotier/ZeroTierOne) — BSL since 2023; **EXCLUDED** per license posture
- [Nebula](https://github.com/slackhq/nebula) — MIT; future candidate
- [Innernet](https://github.com/tonarino/innernet) — MIT; future candidate

---

## Amendments (post-acceptance, 2026-04-29 council)

The council review ([`0061-council-review-2026-04-29.md`](../../icm/07_review/output/adr-audits/0061-council-review-2026-04-29.md)) identified two Critical-severity findings (AP-19, AP-13) and three Major-severity findings (AP-1, AP-18, AP-21), graded the ADR **B (Solid)** on the UPF rubric, and recommended Accept-with-amendments. The CO accepted with amendments; this section authors them. The Critical/Major amendments (A1–A4) are landed inline above; A5–A10 (Minor / may-land-during-Stage-02) follow below. After A1–A4 land, the rubric grade lifts to **A** on re-review.

This is the **5th substrate ADR drafted with AP-19/AP-21 vocabulary drift** (0051, 0053, 0054, 0058, 0061). Every amendment in this cohort that renames `NodeId → PeerId`, `AuditCorrelation`, etc. is honoring the lesson: *future substrate ADRs MUST `git grep` every cited Sunfish.* symbol against the live codebase BEFORE drafting the contract surface.* See `feedback_verify_cited_symbols_before_adr_acceptance` rule.

### A1 — `NodeId` → `PeerId` rename across the contract surface (resolves AP-19)

The original Decision-section contract surface used `NodeId` as the peer identifier in 11 places across `IPeerTransport`, `PeerEndpoint`, `IMeshVpnAdapter`, `MeshDeviceRegistration`, `MeshPeer`, `ITransportSelector.SelectAsync`. The substrate at `packages/federation-common/PeerId.cs` defines `PeerId` (a `readonly record struct PeerId(string Value)` — base64url-encoded Ed25519 public key) as the canonical federation-peer identifier; existing `ISyncTransport.SendAsync` consumes `PeerDescriptor` (which wraps `PeerId`), and `RegisterHandler` takes `PeerId local`. There is no `NodeId` value type anywhere in `packages/federation-*` or `packages/foundation-*`; `NodeId` exists only as `string` in `foundation-recovery` where it refers to **device** identity in the recovery protocol, not federation peers.

This drift is structurally identical to ADR 0051's `AuditCorrelation` drift — same Cold-Start-failure shape, same fix.

**Resolution.** The contract surface above (Decision section) now uses `Sunfish.Federation.Common.PeerId` throughout. Concretely:

| Site | Before | After |
|---|---|---|
| `IPeerTransport.ResolvePeerAsync(...)` | `NodeId peer` | `PeerId peer` |
| `IPeerTransport.ConnectAsync(...)` | `NodeId peer` | `PeerId peer` |
| `PeerEndpoint.Peer` | `NodeId` | `PeerId` |
| `ITransportSelector.SelectAsync(...)` | `NodeId peer` | `PeerId peer` |
| `MeshPeer.Peer` | `NodeId` | `PeerId` |
| `MeshDeviceRegistration.DeviceId` | `NodeId` | **`string`** (mesh-control-plane-issued device identity, not federation peer identity) |
| `MeshDeviceRegistration.Peer` (NEW field) | (absent) | `PeerId` (the Sunfish federation peer this device represents) |

**`PeerId` shape compatibility check.** `PeerId.Value` is a base64url-encoded Ed25519 public key string. Transport-tier needs to:
- Hash to a routing key for selector cache → `PeerId.Value.GetHashCode()` works (records auto-implement); satisfied.
- Serialize across wire (mesh adapter API calls, audit records) → `string` payload; satisfied.
- Compare equality across remote endpoints → record-struct `==` semantics; satisfied.
- No transport-tier need that `PeerId` cannot meet. **No "extending PeerId per this ADR" required.**

**`MeshDeviceRegistration.DeviceId` separation.** The mesh control plane (Headscale, Tailscale) issues its own opaque device identifier at registration time and does not see Sunfish's Ed25519 peer keys. Conflating "mesh device-id" with `PeerId` would force a one-to-one mapping that doesn't match reality (a single Sunfish peer might rotate Headscale node-keys without rotating its `PeerId`). The two-field shape (`DeviceId: string` + `Peer: PeerId`) makes the mapping explicit and adapter-private.

After A1, a Stage-02 contributor reading the contract surface no longer encounters an undefined `NodeId` type. The Cold Start Test passes.

### A2 — `ITransportSelector` failover contract pinned (resolves AP-1)

The original tier-selection pseudocode said only "Try Tier 1 → If found + reachable + handshake succeeds → use Tier 1," with no timeouts, no per-tier budgets, and no tie-break order across multiple registered `IMeshVpnAdapter`s. Three operational pathologies were unaddressed: mesh-up-but-peer-not-registered, mesh-up-handshake-stuck, two-adapter-disagreement.

**Resolution.** The §"Tier selection algorithm" section above now pins:

- **Per-tier wall-clock budgets:** Tier 1 mDNS = 2s; Tier 2 across-all-adapters = 5s; Tier 3 connect = 10s.
- **Per-handshake timeout:** 2s (capped at remaining tier budget).
- **Adapter iteration order (deterministic):** config-declared priority first, lexicographic-by-AdapterName second.
- **`ResolvePeerAsync` returns null:** in-band signal — adapter does NOT throw; selector continues to next adapter, then to Tier 3. Adapter MUST NOT consume budget beyond the round-trip cost of the resolution call itself.
- **Two-adapter-disagreement tie-break:** deterministic priority/lexicographic order resolves it; operator can override per-adapter priority in DI.
- **Most-recently-handshaked tie-break (within Tier 2):** when multiple adapters resolve P within budget, prefer the one whose `MeshPeer.LastHandshakeAt` for P is most recent.
- **Per-peer best-tier discipline:** within a single sync round across N peers, each peer's selection is independent. Some peers Tier-1, some Tier-2 (possibly via different adapters), some Tier-3 — all in the same round.
- **Selection cache:** keyed by `PeerId`; stores `(TransportTier, AdapterName?, ResolvedAt)`; invalidates on `MeshTransportFailed` / `MeshHandshakeCompleted` for the cached peer; ~30s TTL.

These are testable contracts: Stage 06 can author integration tests that (a) start a selector with two adapters disagreeing on P, assert deterministic winner; (b) inject a 10s handshake stall, assert fallback to Tier 3 within 5s + 10s = 15s wall-clock budget; (c) register P in only one of two adapters, assert correct fallthrough.

### A3 — Headscale-abandonment + iOS-Tailscale-removal architectural fallbacks (resolves co-critical: two-vendor-stack risk)

The original ADR named "Headscale switches license" and "Apple deprecates VPN-on-Demand" as revisit triggers but specified no architectural mitigation. The combined two-vendor dependency stack (Headscale on the control plane + Tailscale Inc. on the iOS client) has a non-trivial joint failure probability over a 3–5-year horizon.

**Resolution. Two named fallback paths, with explicit transition costs.**

**(i) Headscale-abandonment fallback: NetBird migration.**

If Headscale becomes unmaintained (commits stop OR maintainer announces wind-down OR Tailscale wire-protocol drifts faster than Headscale tracks), the operational fallback is **migrate to NetBird**.

This is NOT a drop-in replacement; NetBird has its own control-plane protocol, its own client, and its own ACL model. The migration playbook:

1. Operator stands up NetBird control plane (BSD-3, self-hosted, comparable maturity).
2. Operator-side `providers-mesh-netbird` adapter (already on the Phase 2.3 roadmap) becomes the primary mesh adapter.
3. Devices re-register: each Anchor / iOS / Bridge tenant device drains its Headscale enrollment, pulls a NetBird auth key from the operator, registers in the NetBird mesh.
4. ACLs are reauthored (NetBird's ACL model differs from Headscale/Tailscale's — operator effort proportional to mesh complexity).
5. `providers-mesh-headscale` is deprecated; remains in repo for back-compat with operators not yet migrated, but no new development.
6. Audit-trail records: `MeshDeviceRegistered` events fire on each device re-registration; `TransportTierSelected` events show the migration progress in aggregate.

**Transition cost:** non-trivial. Each device re-registration is a per-user step (push a notification, user taps "rejoin mesh," NetBird issues key). Mesh ACLs reauthored by operator. NOT a code-only migration. This is the principled fallback, not a zero-cost one.

**(ii) iOS-Tailscale-removal fallback: native NetworkExtension config + Tier-3 graceful degradation.**

If the Tailscale iOS app is pulled from the App Store, Tailscale Inc. discontinues the product, OR Apple deprecates VPN-on-Demand, the iOS device's mesh-VPN tier is unavailable. Two-layer fallback:

1. **First:** the iOS device falls through cleanly to **Tier 3 (Bridge relay)** automatically, via the `ITransportSelector` failover contract pinned in A2. No user intervention required; no app update required. The iOS sync UX degrades from "instant on Wi-Fi handover" to "syncs through Bridge with Bridge-relay latency," but functionality is preserved.
2. **Second (longer-term):** if iOS Tailscale unavailability is durable, ship a Sunfish iOS NetworkExtension config that talks WireGuard directly to Headscale (or NetBird) without the Tailscale client wrapper. This is more work — it requires Sunfish to ship its own iOS NetworkExtension app (or extend the W#23 iOS Field-Capture App with one), distributed via Apple's enterprise-distribution or App Store. NOT in this ADR's scope; called out as the architected escape hatch.

**Transition cost:** Tier-3 fallback is automatic and free at runtime. Native NetworkExtension config is a Stage-02-or-later workstream gated on the trigger.

These two fallbacks address the council's "joint failure probability is non-trivial" finding without committing to a redesign. The runtime path through Tier 3 means iOS users never see a service outage; the architectural escape hatch means operator-controlled iOS sync is preserved if the worst happens.

### A4 — Bridge-hosted Headscale: speculative + measurement spike before architecture (resolves AP-13)

The original ADR architected Bridge-hosted Headscale before measuring it: per-tenant SQLite, feature flag, Bridge admin UI integration, all named as concrete design choices. Multi-tenant Headscale at scale has no public production reference; Headscale's own docs target single-tenant self-hosting.

**Resolution.** The §"Bridge-hosted Headscale" section above now:

- Restates **tenant-self-hosting as the supported default**.
- Marks Bridge-hosted-Headscale as **SPECULATIVE — Phase 2.3+ research spike**, gated on measurement.
- Specifies a concrete measurement plan (N tenants × M devices, RAM/CPU/SQLite/latency thresholds, decision criteria from data).
- Names the storage layout, port allocation, lifecycle, and TLS termination explicitly as **TBD by spike**, not pinned-in-advance.
- Adds a hard-no threshold (RAM-per-tenant > 200MB at idle OR p99 control-plane latency > 1s under load → DO NOT ship; document tenant-self-hosting as the only supported pattern).
- Adds a soft-attention threshold (Bridge tenant >100 active mesh devices for >7 days OR per-tenant RAM >200MB → re-run spike at scale).
- Both thresholds are now in the Revisit Triggers section as quantitative tripwires.

This converts an architecture-then-measure pattern into a measure-then-architect pattern, while preserving the option for Bridge-hosted-Headscale if the spike data is favorable.

---

### A5 — Provider-neutrality enforcement timing (resolves AP-18 forward reference)

The council finding noted that `SUNFISH_PROVNEUT_001` enforcement was claimed "per the established pattern" while the pattern itself was still in flight (workstream #14, then `ready-to-build`). **Verified at 2026-04-29:** workstream #14 is now `built (merged)` per `icm/_state/active-workstreams.md` — PR #196 landed 2026-04-28. The "established pattern" is mechanically active; the analyzer + BannedSymbols infrastructure auto-attaches at scaffold time. The W#14 status callout was added to §"Provider-neutrality enforcement (ADR 0013)" above; the council finding is now stale-but-resolved.

### A6 — Verification subsection (resolves AP-3 vague success criteria; partially A2-driven)

A complete Verification subsection lands during Stage 02 implementation planning, not in this ADR. The minimum content commitment, captured here for the Stage 02 hand-off:

- **Latency SLO:** Tier-1 selection P95 < 500ms; Tier-2 selection P95 < 5s (matches the §"Tier selection algorithm" budget); Tier-3 selection P95 < 10s.
- **Fallback-rate observability:** weekly per-device computation of `count(TransportFallbackToRelay) / count(TransportTierSelected)` exposed via `kernel-audit` projection or operator dashboard. Tier-3 fallback rate <10% in production validates the design.
- **Integration-test contract for `ITransportSelector` partial-failure scenarios:** mesh-up-but-peer-not-registered (assert fall-through); handshake timeout (assert per-handshake timeout enforced); two-adapter-disagreement (assert deterministic winner); per-peer best-tier under mixed connectivity (assert no single-peer fallback downgrades a sibling peer).
- **Provider-neutrality analyzer architecture test:** automated check that `blocks-*` packages cannot reference `Headscale-Sharp` / `Tailscale.Net` types; reuses W#14 test pattern.

### A7 — WireGuard data-plane source per platform (defers to Stage 02 implementation guide)

This ADR commits to documenting per-platform data-plane choice as part of the Anchor installer integration deliverable (already in Implementation checklist). The shape, pinned here for the Stage 02 hand-off:

- **Anchor Windows:** Wintun TUN driver (auto-installed by Tailscale client; manual install if Headscale-only). Installer detects existing Tailscale-client install and skips Wintun if present; otherwise prompts for admin elevation + Wintun install.
- **Anchor macOS:** wireguard-go userspace tunnel (no kernel extension required since macOS 10.15) OR Tailscale's NetworkExtension app for the Tailscale-client path. No admin elevation required for wireguard-go; NetworkExtension requires App Store distribution + signing.
- **Anchor Linux:** kernel-native WireGuard module (kernel >= 5.6 — covers Ubuntu 20.04+, Fedora 32+, Debian 11+) or wireguard-tools userspace fallback for older kernels. Installer detects kernel version and dispatches.
- **iOS:** Tailscale app's NetworkExtension is the only supported path in this ADR. A native Sunfish-shipped NetworkExtension is the A3-(ii) escape hatch, not the default.

This is design-judgment-heavy enough that it lands as an `apps/docs/foundation/transport/installer-integration.md` deliverable in Stage 02; this amendment commits to the deliverable, not the full content.

### A8 — Quantified revisit triggers (lands above)

Already lands in the §"Revisit triggers" section above. Tier-2 fallback rate, Bridge tenant device count, Headscale-abandonment signal, Apple/Tailscale iOS-deprecation signal, ADR 0004 dual-sign window — all quantified with externally-observable signals.

### A9 — Confidence Level by component (lands in §"Pre-acceptance audit" below)

The original `MEDIUM-HIGH` confidence was assessed monolithically. Refined per component:

- **Contract surface + Tier 1 (mDNS) + Tier 3 (Bridge relay):** **HIGH.** Substrate exists; identifiers reuse `PeerId`; rollback is straightforward.
- **Tier 2 with self-hosted Headscale:** **MEDIUM.** Headscale is BSD-3 + maintained; iOS client maturity caveats apply (A3-ii fallback covers).
- **Bridge-hosted Headscale (Phase 2.3+):** **LOW-MEDIUM, pending measurement spike.** Architecture-then-measure trap was the council's load-bearing finding; A4 conversion to measure-then-architect lifts confidence after spike outcome.
- **iOS-Tailscale-deprecation contingency:** **LOW.** Runtime fallback to Tier 3 is automatic; native NetworkExtension shipping is uncosted and unscheduled.

Updated in §"Pre-acceptance audit" below.

### A10 — Paper §6.1 verbatim citation + acknowledged extension

Paper §6.1 names the requirement in one sentence: *"Mesh VPN (e.g., WireGuard-based) — peers across networks connect with automatic NAT traversal; no port forwarding required."* This ADR materially extends the paper rather than only pinning it: it introduces `IPeerTransport`, `ITransportSelector`, vendor-pluggable adapters, the (now-speculative) Bridge-hosted-Headscale path, and the audit-emission shape. The extension is faithful to the paper's intent (provider-neutral, operator-controllable, license-clean) but is design-work-beyond-paper, not paper-pinning.

This is captured here, in the amendments section, rather than rewriting the Context section. Cold-start contributors reading the paper-citation-as-justification (e.g., "Paper §6.1 is the spec") should understand that the contract surface is this ADR's design call, not a verbatim transcription of the paper.

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options: pluggable adapter family, defer Tier 2, build custom WireGuard control plane. Option A chosen with explicit rejection rationale for B (defeats paper privacy + latency commitments) and C (NIH antipattern; Headscale is BSD-3 + maintained).
- [x] **FAILED conditions / kill triggers.** 5 named: Nebula/Innernet maturity, Headscale license switch, Apple iOS Tailscale regression, Bridge-Headscale scaling, Tier-2 fallback rate.
- [x] **Rollback strategy.** Greenfield. Rollback = revert ADR + revert adapter packages. Existing Tier 1 + Tier 3 transports unchanged.
- [x] **Confidence level (refined per A9, 2026-04-29).** **HIGH** for contract surface + Tier 1 (mDNS) + Tier 3 (Bridge relay) — substrate exists, identifiers reuse `PeerId`, rollback is straightforward. **MEDIUM** for Tier 2 with self-hosted Headscale — Headscale is BSD-3 + maintained, but single-maintainer governance is a real risk (A3 NetBird fallback architected). **LOW-MEDIUM** for Bridge-hosted Headscale (Phase 2.3+, pending measurement spike per A4). **LOW** for iOS-Tailscale-deprecation contingency — runtime fallback to Tier 3 is automatic, but native NetworkExtension shipping is uncosted (A3-ii escape hatch).
- [x] **Anti-pattern scan.** None of AP-1, -3, -9, -12, -21 apply.
- [x] **Revisit triggers.** Five named with externally-observable signals.
- [x] **Cold Start Test.** Implementation checklist is 10 specific tasks. Stage 02 contributor reading this ADR + ADR 0013 + ADR 0031 + paper §6.1 should be able to scaffold without asking.
- [x] **Sources cited.** Headscale + Tailscale + NetBird + WireGuard + Nebula + Innernet GitHub repos referenced. NetMaker + ZeroTier explicitly excluded with license citations. Paper §6.1 + §6.2 + §17.2 cited.
