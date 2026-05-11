# Intake Note — Crew Comms (W#45)

**Date:** 2026-05-04
**Requestor:** CO (Chris Wood)
**Request:** Real-time peer-to-peer crew communication for Anchor — text, then audio, then video — as a default-installed extension with a pluggable provider model.

---

## Problem Statement

Anchor installations currently have no way to communicate with each other in real time. Crew Comms is an **alternative MVP path** — a self-contained, compelling feature that doesn't require the full property-management block suite to demonstrate Anchor's value. Two Anchor nodes on the same LAN should be able to exchange text messages out of the box; subsequent phases extend to cross-network text and audio. This also proves the cross-Anchor transport infrastructure needed for all future peer connectivity features. The feature ships as a default-installed extension with a clean provider interface so third-party implementations (Zoom, Teams) can replace it later — but that pluggability is a design constraint on the contracts layer, not a deliverable for this workstream.

---

## Affected Areas

- `packages/foundation-channels/` (**new**) — contract layer: `IChannelProvider`, `IChannelSession`, `IPeerPresence`, `ChannelCapability` flags enum
- `packages/blocks-crew-comms/` (**new**) — reference implementation; default-installed in Anchor; registers against `IChannelProvider`
- `packages/foundation-transport/` (existing, in-flight) — consumed by the reference impl; mDNS for LAN, Bridge relay for cross-network
- `accelerators/anchor/` — wires `blocks-crew-comms` by default in `MauiProgram.cs`
- `compat-zoom/`, `compat-teams/` (future) — vendor adapters; out of scope for this workstream
- `apps/docs/` — documentation block page

---

## Selected Pipeline Variant

- [x] sunfish-feature-change

Fast-track: no existing API being changed; two new packages + Anchor wiring. Stage 01 Discovery → Stage 02 Architecture (new `foundation-channels` contracts) → Stage 03 Package Design → Stage 05 Impl Plan → Stage 06 Build.

---

## Scope Boundary

**In scope (this workstream):**
- `foundation-channels` contract layer — provider interface + session lifecycle + presence protocol
- `blocks-crew-comms` reference implementation — native Sunfish transport-backed channels
- Phase 1 delivery target: LAN text chat (mDNS + TCP, two Anchor nodes, same network)
- Phase 2 delivery target: cross-network text (Bridge relay tier)
- Phase 3 delivery target: audio (Opus; phone-call quality)
- Phase 4 delivery target: video (H.264/VP8; deferred to follow-on workstream)
- Anchor wiring (default-installed, opt-out capable via DI)
- Provider-pattern surface that compat adapters can implement

**Out of scope:**
- `blocks-messaging` (async durable email/SMS threads — separate concern, no collision)
- compat-zoom / compat-teams — not explored or built in this workstream; `IChannelProvider` must be *designable* to support them but no compat work happens here
- Bridge-side relay infrastructure changes (W#28 boundary; relay consumes `foundation-transport` as-is)
- Video (Phase 4) — deferred to follow-on workstream after Phase 3 audio lands
- Enterprise/external communication — this feature is intra-tenant crew only

---

## Dependencies and Constraints

| Dependency | Status | Notes |
|---|---|---|
| `foundation-transport` | In-flight (staged, not yet merged) | `MdnsPeerTransport` + `BridgeRelayPeerTransport` + `TcpDuplexStream` + `DefaultTransportSelector` are the LAN + relay implementations this workstream consumes |
| W#23 pairing token | Built (PR #478) | `IPairingService` + `HmacPairingService` provide device identity; needed for cross-network node identity binding |
| W#30 (`foundation-transport`) | In-flight | Must land before Stage 06 Build can begin |

**Sequencing constraint:** Stage 06 build for Phase 1 (LAN text) can begin as soon as `foundation-transport` merges. Phases 2-3 follow sequentially.

---

## Key Design Questions (for Stage 01 Discovery)

1. **Presence model** — push heartbeats vs. pull-on-demand? Push is simpler for small LAN deployments; pull scales to larger tenant rosters.
2. **Signaling transport** — reuse `foundation-transport` for session signaling (invite/accept/reject/busy) or a separate channel? Reusing is architecturally clean but couples data and control planes.
3. **Message persistence model** — AP record class (local-first, sync when connected) per the architecture paper. Chat history stored locally on both nodes; no message loss on disconnect.
4. **E2E encryption** — session key derived at pairing time; relay sees ciphertext only. Confirm key derivation approach (HKDF from pairing HMAC secret vs. separate DH exchange).
5. **Audio codec** — Opus assumed (open, excellent voice at 16-64 kbps, used by WebRTC/Discord/Teams). Confirm before designing audio block.
6. **Provider interface surface** — `IChannelProvider` must be scoped so a future compat adapter *could* replace the native impl (transport details must not leak upward), but the interface is sized for the native impl's needs only — no speculative surface for Zoom/Teams features.

---

## Next Steps

Proceed to **Stage 01 Discovery**. Deliverables:
- Dependency mapping: `foundation-transport` contract surface consumed by `foundation-channels`
- Presence protocol options analysis
- Signaling protocol sketch (state machine: idle → invited → connecting → connected → terminated)
- E2E encryption key derivation decision
- `IChannelProvider` surface draft (sufficient to evaluate compat-adapter replaceability)
- Audio codec confirmation (Opus)

Status: `design-in-flight` — sunfish-PM must not implement until `ready-to-build`.
