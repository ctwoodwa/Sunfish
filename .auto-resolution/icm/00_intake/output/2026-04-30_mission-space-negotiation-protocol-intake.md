# Intake — Mission Space Negotiation Protocol (runtime layer)

**Date:** 2026-04-30
**Requestor:** XO research session (synthesis output of W#33 Mission Space Matrix discovery)
**Request:** New ADR ~0063 specifying the runtime protocol by which a Sunfish deployment discovers its current capability profile (Mission Envelope), communicates it to the user, re-evaluates it when conditions change, and degrades gracefully when a previously-available feature becomes unavailable.
**Pipeline variant:** `sunfish-feature-change` (introduces new cross-cutting protocol)
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

Sunfish has no specification of how a deployment probes for, caches, communicates, and re-evaluates its capability profile at runtime. Currently each feature implementer re-derives "how do I check if this is available, when do I re-check, how do I tell the user if it changes" from scratch. The Mission Space Matrix (W#33) identifies this as a **genuine gap** — §5.6 Lifecycle/negotiation, with recommendation: "New ADR ~0063 — Mission Space Negotiation Protocol." This is the **most load-bearing** of the four follow-on intakes per discovery §6.2: every other dimensional gate ultimately surfaces through this protocol's UX channels.

## Predecessor

**No clean predecessor.** Adjacent: paper §13.2 (AP/CP visibility table — staleness thresholds and UX treatments for *some* dimensions); ADR 0036 (5 sync states with ARIA roles); ADR 0041 (rich-vs-MVP UI degradation primitive). The negotiation *protocol* — when probing happens, what's probed, what state is cached, what triggers re-evaluation, how user is notified — is unaddressed in any current artifact.

## Industry prior-art

Per discovery §5.6: SIP/SDP capability negotiation (RFC 5939); TLS cipher-suite negotiation; OpenGL/Vulkan extension query + DirectX feature levels (FL_9_1 / 10_0 / 11_0 / 12_0); HTTP content negotiation (Accept-* headers); WebRTC codec negotiation. DirectX Feature Levels are the closest engineering analog to Sunfish's discrete capability tiers.

## Scope

- **Probe mechanics** — install-time vs startup-time vs continuous; what's probed at each cadence
- **Manifest format** — how a deployment serializes its current Mission Envelope (for diagnostics, logging, telemetry, user-visible "what your device can do" UI)
- **Re-evaluation triggers** — hot-plug events; version upgrades; network-topology changes; jurisdiction crossings (mobile); commercial-tier changes (Bridge subscription start/end)
- **Cache vs live-probe** — what gets cached; for how long; what invalidates
- **Graceful-degradation taxonomy** — formalize hide / disable-with-explanation / disable-with-upsell / read-only / hard-fail per feature-gate failure mode (pairs with ~ADR 0062)
- **User-communication policy** — when to surface capability changes; channel selection (status bar per paper §13.2; modal; toast; deferred to next session)
- **Per-feature force-enable** — power-user override surface for "force-enable an unsupported feature with warning"
- **Telemetry shape** — capability-cohort analytics for product roadmap

## Dependencies and Constraints

- **Soft dependency**: outputs feed ~ADR 0062 (Mission Space Requirements) — the install-time UX layer renders this protocol's output. Recommended sequencing per discovery §7.2: ~0063 first, ~0062 second.
- **Soft dependency**: outputs feed ~ADR 0064 (regulatory) — runtime jurisdiction probe is part of the negotiation protocol's probe mechanics.
- **Effort estimate:** large (~16–24h authoring + council review).
- **Council review posture:** pre-merge canonical (cohort lesson 7-of-7).

## Affected Areas

- foundation: cross-cutting probe + manifest contract
- ui-core: graceful-degradation primitive contract
- ui-adapters-blazor / ui-adapters-react: per-adapter implementation
- accelerators/anchor + accelerators/bridge: per-zone probe behavior

## Downstream Consumers

- All ten Mission Space Matrix dimensions (this protocol is the connective tissue)
- W#23 iOS Field-Capture: capture-only mobile profile is itself a Mission Envelope
- Phase 2 commercial MVP: tier transitions surface through this protocol

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery. Highest *engineering* priority of the new ADRs per discovery §6.2.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-04-30_mission-space-matrix.md` §5.6 + §6.2 + §7
- Active workstream: W#33 in `icm/_state/active-workstreams.md`
- Mission Space plan: `~/.claude/plans/this-looks-pretty-comprehensive-concurrent-floyd.md`
