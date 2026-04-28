# Intake Note — Mesh VPN / Cross-Network Peer Transport

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build` and a hand-off file appears in `icm/_state/handoffs/`.**
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (turns 1–2 — Tailscale-like cross-network peer connectivity, OSS options for Bridge embedding).
**Pipeline variant:** `sunfish-feature-change` (with new ADR)
**NOT part of property-ops cluster** — this is a kernel-tier transport concern that benefits the whole architecture, not just the property business. Captured here because it surfaced in the same conversation.

---

## Problem Statement

The local-node architecture paper (`_shared/product/local-node-architecture-paper.md` §6.1) specifies a three-tier peer-discovery model:

1. **mDNS** — same network segment, zero-config; covers "all my devices on home Wi-Fi"
2. **Mesh VPN** — peers across networks connect with automatic NAT traversal; "e.g., WireGuard-based"
3. **Managed relay** — fallback for teams where direct peer connectivity isn't viable; also the commercial revenue model (paper §17.2)

Tier 1 is covered by mDNS implementations already in the federation packages. Tier 3 is Bridge's existing role as a SaaS-shaped relay. **Tier 2 has no ADR, no provider adapter, no concrete implementation.** It exists as text in the paper.

This is not blocking for Phase 1 (single-LAN deployments cover personal scale). But two near-term workstreams expose the gap:

- **Phase 2 commercial** (BDFL property business) — 6 tenants, multiple devices, devices that travel; cross-network peer connectivity is the natural shape, even if managed-relay-via-Bridge is the default.
- **iOS field-capture app** (this conversation) — when the BDFL is at home, direct iPad↔Anchor sync is faster than going through Bridge; mesh VPN is the optimization tier that delivers it (Phase 2.3 in cluster phase mapping).

This intake captures the three-tier model as a pinned ADR, names Headscale + WireGuard as the recommended adapter, and reserves a `providers-mesh-*` package family.

## Scope Statement

### In scope (this intake)

1. **New ADR:** "Three-tier peer transport model (mDNS / Mesh VPN / Managed Relay)"
   - Pins paper §6.1's verbal description into a formal architecture decision
   - Names `IPeerDiscovery` (or extends `ISyncTransport`) as the contract surface
   - Lists approved adapters: Headscale, NetBird, plain WireGuard, Tailscale (vendor-bring-your-own; not embedded)
   - Explicitly rejects: NetMaker (SSPL), ZeroTier (BSL) for license incompatibility
   - Notes Nebula and Innernet as future candidates
2. **`providers-mesh-headscale/` package skeleton** — adapter that talks to a Headscale server (self-hosted control plane for WireGuard mesh). Recommended as the canonical mesh-VPN provider for Sunfish.
3. **`providers-mesh-tailscale/` package skeleton** (optional) — adapter for users who already operate a Tailscale account and want to bring their own. Vendor SaaS; not embeddable in Bridge.
4. **`providers-mesh-netbird/` package skeleton** (optional, lower priority) — alternative for users who prefer NetBird's bundled admin UI.
5. **Bridge embedded-Headscale module** (optional Phase 2.3+) — Bridge tenants can flip a feature flag → Headscale process spins up alongside Bridge → tenant's devices register against it → sync flows over WireGuard tunnels rather than Bridge's HTTPS.
6. **Anchor default-install bundling** (Phase 2.3+) — Anchor on macOS/Windows offers to install Tailscale or Headscale client during initial setup; user picks (or skips for mDNS-only home use).

### Out of scope (this intake — handled elsewhere or deferred)

- Specific peer-discovery protocol details (gossip anti-entropy, vector clocks) — paper §6.1; sync-daemon work
- Sync-daemon protocol implementation — separate workstream; paper §6.2
- Bridge as managed-relay (tier 3) — covered by Bridge's existing role; paper §17.2
- iOS-specific transport (Tailscale on iPhone) — touched by `property-ios-field-app-intake-2026-04-28.md`; this intake provides the contract surface that the iOS app consumes

### Explicitly NOT in scope

- Operating Tailscale Inc.'s control plane — closed-source; Sunfish does not embed
- Self-hosting an SSPL or BSL-licensed control plane (NetMaker, ZeroTier) — license incompatibility with Sunfish posture
- Building a custom WireGuard control plane from scratch — Headscale is the canonical OSS choice; reinventing it is unjustified

---

## Affected Sunfish Areas

| Layer | Item | Change |
|---|---|---|
| Foundation / Federation | `federation-common` `ISyncTransport` | May extend with `IPeerDiscovery` interface or add `IMeshTransport` subtype |
| Providers (new family) | `providers-mesh-headscale/` | First exemplar |
| Providers (new family) | `providers-mesh-tailscale/` | Optional second |
| Providers (new family) | `providers-mesh-netbird/` | Optional third |
| Accelerators | Anchor (existing) | Optional install-time mesh-VPN setup wizard (Phase 2.3+) |
| Accelerators | Bridge (existing) | Optional embedded-Headscale module (Phase 2.3+) |
| ADRs | New: "Three-tier peer transport model" | Primary architectural deliverable |
| ADRs | ADR 0013 (provider neutrality) | Validates: providers-mesh-* are vendor adapters; kernel/blocks reference no specific mesh vendor |
| Paper | `_shared/product/local-node-architecture-paper.md` §6.1 | No content change; ADR formalizes existing text |

---

## Acceptance Criteria

- [ ] New ADR drafted and accepted
- [ ] `IPeerDiscovery` (or extended `ISyncTransport`) contract defined in `federation-common`
- [ ] `providers-mesh-headscale/` package skeleton with passing minimal test (registration + peer-list query)
- [ ] Provider-neutrality analyzer (workstream #14) passes on the new providers-mesh-* packages
- [ ] apps/docs entry covering the three-tier model + adapter selection guidance + self-hosting Headscale
- [ ] Phase mapping decision: which sub-phase of Phase 2 (or later) ships embedded-Headscale in Bridge

---

## Open Questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-MV1 | Phase placement: Phase 2.3 (cluster) vs separate Phase 3 vs ship now alongside Phase 1 closure | Stage 02. Recommend Phase 2.3 — managed-relay-via-Bridge covers Phase 2.1 needs; mesh VPN is an optimization. |
| OQ-MV2 | Tailscale free-tier license acceptable for Sunfish distribution? (3 users / 100 devices on personal; commercial restrictions at scale.) | Verify. For BDFL personal use, free tier is fine. For Sunfish-as-distributed-software, Tailscale is bring-your-own (not embedded), so license is end-user's concern. |
| OQ-MV3 | Headscale embedded as in-process Bridge module vs separate sidecar process? | Stage 02. Recommend separate sidecar (Headscale is a Go binary); Bridge orchestrates lifecycle. |
| OQ-MV4 | Tailscale client as the default desktop UX (since users will recognize it) vs custom WireGuard config that hides the Tailscale brand? | Stage 02. Recommend Tailscale client + Headscale server — same client UX, our control plane. |
| OQ-MV5 | Cross-tenant mesh sharing: BDFL's properties are 4 tenant LLCs. Do all 4 join the same mesh, or 4 separate meshes? | Stage 02. Recommend per-tenant mesh; cross-tenant operator joins multiple meshes. |
| OQ-MV6 | Self-hosters who don't want Bridge: how do they get a Headscale instance? Sunfish provides an installer? Documentation only? | Stage 02. Recommend documentation + Docker compose example; installer is later. |

---

## Dependencies

**Blocked by:**
- ADR 0013 enforcement gate (workstream #14, ready-to-build) — provider-neutrality analyzer must pass on new providers-mesh-* packages

**Blocks:**
- iOS field-app Phase 2.3 transport optimization (Tailscale-on-tailnet path)
- Future ADRs / workstreams depending on direct peer-to-peer sync without Bridge

**Cross-cutting open questions consumed:** None from INDEX (this intake is outside the property-ops cluster).

---

## Pipeline Variant Choice

`sunfish-feature-change` with mandatory new ADR. The ADR is the primary deliverable; provider adapter scaffolds are skeleton-level Phase 2.3+ work.

---

## Cross-references

- Paper §6.1, §6.2, §17.2
- ADR 0013 (provider neutrality)
- ADR 0028 (CRDT engine; sync transport adjacent)
- [`property-ios-field-app-intake-2026-04-28.md`](./property-ios-field-app-intake-2026-04-28.md) — consumer in Phase 2.3
- Workstream #14 (provider-neutrality enforcement gate) — prerequisite

---

## Sign-off

Research session — 2026-04-28
