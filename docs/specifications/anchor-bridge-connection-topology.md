# Anchor↔Bridge Connection Topology Specification

**Status:** Draft — Phase 1 deliverable G3 (per `icm/05_implementation-plan/output/business-mvp-phase-1-plan-2026-04-26.md`)
**Date:** 2026-04-26
**Source paper:** `C:/Projects/the-inverted-stack/_shared/product/local-node-architecture-paper.md` §6.1 (Gossip Anti-Entropy), §6.2 (Sync Daemon Protocol), §17.2 (Hosted relay as a SaaS node), §20.7 Zone-A / Zone-C
**Audience:** Anchor + Bridge implementers; Phase 1 MVP integration work
**Related ADRs:** [0026 Bridge Posture (superseded)](../adrs/0026-bridge-posture.md), [0029 Federation Reconciliation](../adrs/0029-federation-reconciliation.md), [0031 Bridge as Hybrid Multi-Tenant SaaS](../adrs/0031-bridge-hybrid-multi-tenant-saas.md), [0044 Anchor Windows-only Phase 1](../adrs/0044-anchor-windows-only-phase-1.md)

> This document defines the standard connection topologies that Anchor instances and Bridge service deployments form during the Phase 1 MVP. It does NOT motivate why Sunfish has both a desktop client and a managed relay — see paper §17.2 + §20.7. It does NOT specify the wire protocol — see [`sync-daemon-protocol.md`](sync-daemon-protocol.md). It specifies only how Anchor and Bridge endpoints are configured to find and authenticate each other.

---

## 1. Overview

Sunfish ships two distinct deployable units:

| Unit | Zone | Role | Process model |
|---|---|---|---|
| **Anchor** | A — Local-First Node | Full Sunfish kernel + UI in a desktop app | One MAUI process per device; one device per user typically |
| **Bridge** | C — Hybrid | Managed-relay-and-shell service for off-site sync | One ASP.NET Core process per tenant deployment; multi-tenant within deployment |

Both units use the same kernel-sync gossip protocol on the wire (per [`sync-daemon-protocol.md`](sync-daemon-protocol.md)). They differ only in:

1. **Posture toward authority.** Anchor is the data authority for its team(s); Bridge is a routing relay (paper §17.2: "ciphertext stored at the relay; role keys remain on end-user devices").
2. **Discovery.** Anchor uses tier-1 mDNS for LAN peers; Bridge advertises a stable WAN endpoint that Anchors connect to via configured URL.
3. **Lifecycle.** Anchor's GossipDaemon starts when the user opens the app; Bridge's `RelayServer` runs as a long-lived ASP.NET Core hosted service.

---

## 2. Bridge postures (per ADR 0031 + `BridgeOptions.Mode`)

Bridge has two install-time postures controlled by the `Bridge:Mode` configuration section:

### 2.1 SaaS posture (default)

`Bridge:Mode = SaaS` (default per `BridgeOptions.Mode = BridgeMode.SaaS`).

The full Bridge stack: Aspire orchestration + Postgres + Redis + RabbitMQ + Wolverine + Data API Builder + SignalR + Razor Components shell. Multi-tenant browser-accessible web app per ADR 0031.

**Sync wiring in this posture:** Bridge ALSO accepts inbound sync-daemon connections (the relay layer co-exists with the SaaS layer) but the primary user-facing entry is the browser shell.

**Phase 1 use case:** SMB owner who wants browser-based access to their Anchor's team data from any device (laptop, tablet, phone-web) without installing Anchor everywhere.

### 2.2 Relay posture

`Bridge:Mode = Relay` (opt-in per ADR 0026 / 0031).

Headless. No Razor components, no SignalR hub, no DAB, no Wolverine. Just `kernel-sync` + `kernel-security` + `RelayServer` accepting inbound connections, authenticating peers via `HandshakeProtocol`, fanning out `DELTA_STREAM` and `GOSSIP_PING` frames to co-tenant peers.

**Sync wiring in this posture:** the only purpose. Stateless beyond the current connection set; peers reconnect after restart.

**Phase 1 use case:** SMB with a static-IP server / VPS / edge appliance where they want a 24/7 sync hub but no browser UI.

### 2.3 Both postures support multi-tenant

Per ADR 0031: Bridge is multi-tenant in BOTH postures from day one. Per-tenant data-plane isolation; shared control plane. The paper's ciphertext-at-rest invariant (paper §17.2) means cross-tenant byte leakage yields undecryptable data — fundamentally stronger multi-tenancy than traditional SaaS.

---

## 3. Anchor connection profiles

An Anchor instance can connect to peers via any combination of these discovery profiles. They coexist; the GossipDaemon's round picks from the union of advertisements.

### 3.1 Profile A — LAN-only (mDNS)

Anchor uses `MdnsPeerDiscovery` (per `kernel-sync/Discovery/MdnsPeerDiscovery.cs`). Discovery is automatic on the local subnet via mDNS service-type advertisement. Other Anchor instances on the same LAN are found within the mDNS query interval (default per `PeerDiscoveryOptions`).

**Use case:** small office where all Anchor instances are on the same router. No Bridge needed. Sync survives when the office router loses internet, as long as the router itself is up.

**Limits:** mDNS does not span subnets / VPNs / cellular / hotel networks. Anchor instances on different physical LANs see each other only via Bridge (Profile B).

### 3.2 Profile B — Bridge-mediated WAN (managed-relay)

Anchor uses `ManagedRelayPeerDiscovery` (new per Phase 1 G4) + a configured Bridge relay URL. The relay URL is captured at first-run setup OR via Anchor settings UI. Anchor's GossipDaemon dials the Bridge endpoint via the appropriate transport (UDS / Named Pipe locally, WebSocket over WAN).

**Use case:** Anchor instances on different LANs (owner's home + employee's coffee shop + warehouse phone on cellular); off-site / mobile workers; SMBs with road-warrior employees.

**Limits:** Bridge is a single relay endpoint per Anchor's configuration today (Phase 1). Bridge HA with multiple relay endpoints behind a discovery layer is post-MVP per `BridgeOptions.RelayOptions.AdvertiseHostname` foreshadowing.

### 3.3 Profile C — Both A + B (recommended for Phase 1 deployments)

Anchor attaches BOTH `MdnsPeerDiscovery` and `ManagedRelayPeerDiscovery`. LAN peers are discovered via mDNS and connected to directly (no Bridge hop, lowest latency, works offline). WAN peers are reached via Bridge.

**Use case:** the canonical SMB deployment per plan §8 — owner + bookkeeper + warehouse employees on an office LAN, plus mobile sales rep on cellular, plus accountant who logs in from their own home office.

**This is the Phase 1 reference topology.**

---

## 4. Phase 1 reference topologies

### 4.1 Topology 1 — Single Anchor, no off-site

```
+------------+
|  Anchor 1  |   (one user, one device)
|   (laptop) |
+------------+
```

No Bridge; no peers to sync with. mDNS discovery emits no advertisements (no co-tenant peers on LAN). Backup is local-only via Anchor's backup orchestration (Phase 1 G5). Recovery via paper-key fallback only (no trustees designated yet — recovery workflow optional in this topology).

**Conformance posture:** P1, P3, P5, P7 fully exercised; P2, P4, P6 (sync-relevant aspects) not exercised (no sync occurs).

### 4.2 Topology 2 — Multiple Anchors on LAN, no Bridge

```
+------------+      mDNS
|  Anchor 1  | <----------+
|  (owner    |            |
|   laptop)  |            |
+------------+            |
                          |
+------------+            |
|  Anchor 2  | <----------+
| (bookkeeper|         (LAN sync via UDS / Named Pipe
|   laptop)  |          on each device, peer-to-peer)
+------------+            |
                          |
+------------+            |
|  Anchor 3  | <----------+
| (warehouse |
|   tablet)  |
+------------+
```

All Anchors on the same office LAN. mDNS discovery finds peers automatically. Sync via `UnixSocketSyncDaemonTransport` (POSIX) or `NamedPipeSyncDaemonTransport` (Windows) per the platform.

**Conformance posture:** P1-P7 all exercisable with the constraint that off-site access doesn't exist. Owner's smartphone, accountant's home office — none of these reach the team's data without joining the LAN (e.g., via VPN). For most SMBs that's acceptable; for road-warriors it's not.

### 4.3 Topology 3 — Anchors + Bridge (Phase 1 reference)

```
                        +----------------------+
                        |   Bridge (Zone C)    |
                        |   - SaaS posture     |
                        |   - Relay listener   |  (multi-tenant; this org's
                        |   - Per-tenant DB    |   data isolated to its slot)
                        |   - Hosted on cloud  |
                        +-----------+----------+
                         WAN sync   |
                       (WebSocket / |
                        TLS / Noise)|
        +-----------------+---------+---------+----------------+
        |                 |                   |                |
+------------+     +------------+      +------------+    +------------+
|  Anchor 1  |     |  Anchor 2  |      |  Anchor 3  |    |  Anchor 4  |
|  (owner    |     | (bookkeeper|      | (warehouse |    | (mobile    |
|   laptop)  |     |   laptop)  |      |   tablet)  |    |  sales,    |
+-----+------+     +-----+------+      +-----+------+    | cellular)  |
      |                  |                   |           +-----+------+
      |                  |                   |                 |
      |   mDNS LAN sync  |   mDNS LAN sync   |                 |
      +------------------+-------------------+                 |
        (Anchors 1,2,3 on office LAN — direct sync)            |
                                                                |
        Anchor 4 (mobile, cellular) reaches the team only via Bridge.
```

The Phase 1 reference topology. Anchors 1-3 sync directly via mDNS over the office LAN AND via Bridge (Profile C). Anchor 4 (mobile sales rep on cellular) syncs only via Bridge (Profile B). All four converge on the same team CRDT state via the gossip anti-entropy protocol per paper §6.1.

**Conformance posture:** P1-P7 fully exercisable.

### 4.4 Topology 4 — Multi-tenant Bridge serving multiple SMBs

```
+----------------------+
|   Bridge (Zone C)    |
|   - SaaS posture     |
|   - Relay listener   |
|   - Per-tenant DB    |   (isolation: per-tenant ciphertext
|   - Hosted on cloud  |    only; relay cannot decrypt either)
+-----+----------+-----+
      |          |
   Tenant     Tenant
   ACME       BETA
   (3 Anchors)(2 Anchors)
```

The "Bridge as a service" pattern per plan §8. Bridge operator hosts ONE Bridge deployment serving multiple SMB tenants. Each tenant's data is isolated per-tenant-DB; the relay cannot decrypt either tenant's data.

**Out of scope for Phase 1 acceptance** — but the architecture supports it from day one per ADR 0031. Phase 6 demos may exercise this topology.

---

## 5. Authentication and identity

Per [`sync-daemon-protocol.md`](sync-daemon-protocol.md) §4 (Handshake) + paper §11.3 (Role Attestation vs Key Distribution):

- Each Anchor presents its `NodeIdentity` (Ed25519 device key) during the handshake
- Each Anchor presents its team membership via `attestation_bundle` (opaque CBOR per `kernel-security/Attestation/`)
- Bridge in Relay posture verifies the attestation but does NOT decrypt the team's data — it routes ciphertext between authenticated co-tenant peers
- Bridge in SaaS posture additionally renders a browser shell, but the shell decrypts client-side using role keys derived from the user's password (per paper §17.2 ciphertext-at-rest invariant; per ADR 0031 browser-shell key-bootstrap flow)

---

## 6. Configuration reference

### 6.1 Bridge — appsettings.json

```json
{
  "Bridge": {
    "Mode": "SaaS",
    "Relay": {
      "ListenEndpoint": "tcp://0.0.0.0:9111",
      "MaxConnectedNodes": 500,
      "AdvertiseHostname": "bridge.example.com"
    }
  }
}
```

| Key | Default | Notes |
|---|---|---|
| `Bridge:Mode` | `SaaS` | `SaaS` (full stack) or `Relay` (headless) |
| `Bridge:Relay:ListenEndpoint` | platform default | UDS path on POSIX / Named Pipe on Windows / `tcp://` URL for WAN |
| `Bridge:Relay:MaxConnectedNodes` | 500 | Per-relay-process cap; overflow returns `ErrorCode.RateLimitExceeded` |
| `Bridge:Relay:AdvertiseHostname` | null | Hostname returned to peers via discovery; null disables advertisement |

### 6.2 Anchor — settings (TBD post-G1)

Anchor settings UI captures (per Phase 1 G1 + G4):

- `Sync:Listen` — local listen endpoint convention (default: `<AppDataDirectory>/sync/anchor.sock` POSIX, `\\.\pipe\sunfish-anchor-<install-id>` Windows)
- `Sync:Discovery:Mdns:Enabled` — boolean (default true; tier-1 LAN discovery)
- `Sync:Discovery:Bridge:Url` — optional Bridge relay URL; null disables Profile B
- `Sync:Discovery:Bridge:VerifyHostname` — TLS hostname verification (default true; do not change)

---

## 7. Phase 1 acceptance topology

For Phase 1 acceptance ("Anchor opens, syncs with another Anchor over LAN, syncs with Bridge over WAN, key recovery flow works end-to-end"), the smoke run covers:

1. **Topology 1** smoke: single Anchor launches, opens encrypted local DB, idle gossip daemon
2. **Topology 2** smoke: two Anchors on same machine (different ports / paths), discover each other via mDNS, exchange a CRDT delta, both projections converge
3. **Topology 3** smoke: two Anchors + one Bridge in Aspire orchestration, Anchors configured with Bridge URL, exchange a CRDT delta via Bridge relay, both projections converge

Topology 4 (multi-tenant) is out of Phase 1 acceptance scope per plan §10 phase boundaries — re-verified in Phase 6 demos.

---

## 8. Open questions for future versions

- **Bridge HA with multiple relay endpoints behind a discovery layer** — `BridgeOptions.RelayOptions.AdvertiseHostname` foreshadows this but Phase 1 ships single-endpoint-per-Anchor only
- **NAT-punch peer-to-peer between Anchors on different LANs without Bridge mediation** — out of Phase 1 per G4 risk-register; Bridge as central rendezvous handles this case
- **WireGuard mesh-VPN tier-2 peer discovery** — kernel-sync README §"Status" lists Wave 2.2 tier-2 as deferred; Phase 1 uses Profiles A + B only
- **Discovery via QR code for Bridge URL** — Phase 1 has manual entry; QR-based onboarding may follow

---

## References

- Sync daemon protocol: [`sync-daemon-protocol.md`](sync-daemon-protocol.md)
- Foundational paper §6.1, §6.2, §17.2, §20.7: `C:/Projects/the-inverted-stack/_shared/product/local-node-architecture-paper.md`
- ADR 0026 (Bridge Posture, superseded): `../adrs/0026-bridge-posture.md`
- ADR 0029 (Federation Reconciliation): `../adrs/0029-federation-reconciliation.md`
- ADR 0031 (Bridge Hybrid Multi-Tenant SaaS): `../adrs/0031-bridge-hybrid-multi-tenant-saas.md`
- ADR 0044 (Anchor Windows-only Phase 1): `../adrs/0044-anchor-windows-only-phase-1.md`
- Phase 1 implementation plan: `../../icm/05_implementation-plan/output/business-mvp-phase-1-plan-2026-04-26.md`
- kernel-sync README: `../../packages/kernel-sync/README.md`
- BridgeOptions surface: `../../accelerators/bridge/Sunfish.Bridge/BridgeOptions.cs`
- AppHost orchestration: `../../accelerators/bridge/Sunfish.Bridge.AppHost/Program.cs`
