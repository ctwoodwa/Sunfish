# ADR 0061 — Three-Tier Peer Transport Model (mDNS / Mesh VPN / Managed Relay)

**Status:** Proposed (2026-04-29; awaiting council review + acceptance)
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
- **Bridge can host Headscale.** Phase 2.3+: Bridge tenants opt-in via feature flag → Bridge spins up Headscale alongside its existing services → tenant devices register → mesh tunnels carry sync. Keeps everything within Bridge's hosting envelope.

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

**Adopt Option A.** Three-tier `IPeerTransport` model with vendor-pluggable mesh-VPN adapters. Headscale is the canonical recommendation; vendor SaaS (Tailscale, NetBird) supported via ADR 0013 adapter pattern. SSPL/BSL adapters explicitly excluded. Bridge-hosted Headscale Phase 2.3+ option.

### Initial contract surface

```csharp
namespace Sunfish.Foundation.Transport;

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
    Task<PeerEndpoint?> ResolvePeerAsync(NodeId peer, CancellationToken ct);
    Task<IDuplexStream> ConnectAsync(NodeId peer, CancellationToken ct);
}

public sealed record PeerEndpoint
{
    public required NodeId Peer { get; init; }
    public required IPEndPoint Endpoint { get; init; }
    public required TransportTier Tier { get; init; }
    public required DateTimeOffset DiscoveredAt { get; init; }
    public DateTimeOffset? LastSeenAt { get; init; }
}

public interface ITransportSelector
{
    /// <summary>Try transports in order: Tier 1 → Tier 2 → Tier 3. Return the first available.</summary>
    Task<IPeerTransport> SelectAsync(NodeId peer, CancellationToken ct);
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
    public required NodeId Peer { get; init; }
    public required IPEndPoint MeshEndpoint { get; init; }    // 100.x.x.x for Tailscale-compat; 10.x.x.x for Headscale-default
    public required DateTimeOffset LastHandshakeAt { get; init; }
}

public sealed record MeshDeviceRegistration
{
    public required NodeId DeviceId { get; init; }
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

### Bridge-hosted Headscale (Phase 2.3+)

Per intake §"In scope" item 5: Bridge tenants flip a feature flag → Headscale spins up alongside Bridge's existing services → tenant's devices register via Bridge-issued auth keys → mesh tunnels carry sync.

Architecture:
- Bridge `accelerators/bridge/MeshControlPlane/` module hosts Headscale
- Tenant's devices get Headscale auth keys via Bridge admin UI (per ADR 0031 multi-tenant data-plane)
- Mesh tunnels: device → Headscale-managed WireGuard → other devices in tenant's mesh
- Headscale storage: per-tenant SQLite (matches Bridge's existing per-tenant data-plane isolation)
- Failover: if Bridge's Headscale is unavailable, fall back to Tier 3 (Bridge HTTPS relay)

This is OPTIONAL — tenants can self-host Headscale on their own infrastructure if they want full control.

### Tier selection algorithm (`ITransportSelector`)

```text
For each peer P that needs sync:
  1. Try Tier 1 (mDNS):
     - Query mDNS for P's NodeId on local network
     - If found + reachable + handshake succeeds → use Tier 1
  2. Try Tier 2 (Mesh VPN):
     - For each registered IMeshVpnAdapter:
       - Query mesh for P's NodeId
       - If found + connected + handshake succeeds → use Tier 2
  3. Fall back to Tier 3 (Bridge relay):
     - Always available as long as Bridge is reachable
     - Use ciphertext-only relay per ADR 0031
```

Selection is per-peer; some peers may be Tier-1-reachable while others are Tier-3-only. Selection caches for `~30s` to avoid mDNS / mesh re-queries on every sync.

### Provider-neutrality enforcement (ADR 0013)

`SUNFISH_PROVNEUT_001` analyzer auto-attaches to providers-mesh-* packages. Headscale-Sharp / Tailscale.Net SDK / etc. types may be imported only inside their respective adapter packages; `blocks-*` cannot reference them.

`BannedSymbols.txt` per provider package per the established pattern.

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

- **Nebula or Innernet matures** to Tailscale/Headscale ergonomic parity → consider adding adapter
- **Headscale switches license** away from BSD-3 → adapter package status revisited
- **Apple deprecates VPN-on-demand** or Tailscale iOS app significantly regresses → iOS sync re-evaluation
- **Bridge tenant exceeds 100+ devices in mesh** — Headscale single-instance scaling needs measurement; might require sharding
- **Tier-2 fallback rate stays <10%** in production — validates that Tier 1 + Tier 2 cover the common case; Tier 3 is the rare fallback as designed

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

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options: pluggable adapter family, defer Tier 2, build custom WireGuard control plane. Option A chosen with explicit rejection rationale for B (defeats paper privacy + latency commitments) and C (NIH antipattern; Headscale is BSD-3 + maintained).
- [x] **FAILED conditions / kill triggers.** 5 named: Nebula/Innernet maturity, Headscale license switch, Apple iOS Tailscale regression, Bridge-Headscale scaling, Tier-2 fallback rate.
- [x] **Rollback strategy.** Greenfield. Rollback = revert ADR + revert adapter packages. Existing Tier 1 + Tier 3 transports unchanged.
- [x] **Confidence level.** **MEDIUM-HIGH.** Substrate composition is well-understood; Headscale + Tailscale are mature; license-posture exclusions are clear-cut.
- [x] **Anti-pattern scan.** None of AP-1, -3, -9, -12, -21 apply.
- [x] **Revisit triggers.** Five named with externally-observable signals.
- [x] **Cold Start Test.** Implementation checklist is 10 specific tasks. Stage 02 contributor reading this ADR + ADR 0013 + ADR 0031 + paper §6.1 should be able to scaffold without asking.
- [x] **Sources cited.** Headscale + Tailscale + NetBird + WireGuard + Nebula + Innernet GitHub repos referenced. NetMaker + ZeroTier explicitly excluded with license citations. Paper §6.1 + §6.2 + §17.2 cited.
