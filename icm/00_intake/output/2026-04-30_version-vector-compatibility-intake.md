# Intake — ADR 0028 Amendment A6: Version-Vector Compatibility Contract

**Date:** 2026-04-30
**Requestor:** XO research session (synthesis output of W#33 Mission Space Matrix discovery)
**Request:** Amend ADR 0028 (CRDT Engine Selection) with a new amendment A6 specifying the version-vector compatibility contract for mixed-version Sunfish clusters — kernel × plugin × adapter × schema-epoch × stable/beta channel × self-host/managed instance class.
**Pipeline variant:** `sunfish-api-change` (introduces compatibility-contract API; affects federation-time handshake)
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

Sunfish has no version-vector compatibility contract for mixed-version clusters. The Mission Space Matrix (W#33) **A4 spot-check confirmed this gap**: paper §6.1 (line 180) mentions vector clocks operationally for *gossip mechanics* (anti-entropy reconciliation), but there is no specification of version-vector compatibility for cross-version cluster federation. ADR 0028 (and amendments A1–A4) cover CRDT engine selection and mobile reality but are silent on cross-version cluster behavior. Per discovery §5.8, this is a **genuine gap — net-new** with confirmed predecessor silence.

Paper §15.2 *implicitly* references the gap: *"'Couch device' (offline for 3+ major versions) → capability negotiation rejects with clear error"* — but the rejection logic is exactly the gap.

## Predecessor

**Clean amendment slot:** ADR 0028 — CRDT Engine Selection, with prior amendments A1 (mobile reality / iOS append-only event queue), A2 (test-coverage), A3 (cited-symbol verification fix), A4 (canonicalization retraction). A5 will cover migration semantics (sibling intake); A6 covers version-vector compatibility.

**Why amendment, not new ADR:** version-vector compatibility is intrinsically tied to the CRDT engine + cluster-membership protocol that ADR 0028 already governs. New ADR would over-fragment the contract surface.

## Industry prior-art

Per discovery §5.8:
- **Lamport / Mattern vector clocks** — original formalism for partial-ordering distributed events
- **Paxos epoch numbers** — strict-version-monotonic across cluster; closest formal analog to Sunfish's epoch-coordinated cutover
- **gRPC API versioning** (Google's API design guide) — semantic versioning + explicit deprecation windows + compatibility classes; pragmatic engineering analog
- **HTTP/2 ALPN negotiation** — clients and servers exchange supported-protocol-version lists during connection setup; closest negotiation-protocol analog

## Scope

- **Version-vector type signature** — what tuple expresses "this kernel × plugin × adapter × schema-epoch × channel × instance-class"; JSON shape; normalization rules; comparison semantics
- **Compatibility relation** — given two version vectors V1, V2, when can a node carrying V1 federate with a node carrying V2? Recommend: explicit allowlist with range syntax modeled on gRPC versioning
- **Federation-time handshake** — when peer A meets peer B, version-vector exchange (separate handshake or part of existing gossip protocol)
- **Behavior on incompatibility** — connection fails / degrades to reduced surface / quarantines; specify
- **Long-offline reconnect rejection logic** — paper §15.2's "couch device" scenario; rejection-with-clear-error per the paper's hint; specify the error format and user-visible recovery action

## Dependencies and Constraints

- **Cross-references** ~ADR 0063 (Mission Space Negotiation Protocol) — version-vector compatibility is part of negotiation
- **Sibling intake**: ADR 0028-A5 (cross-form-factor migration); the two amendments are tightly coupled — A5 builds on A6's compatibility relation
- **Effort estimate:** medium-large (~12–18h authoring + council review)
- **Council review posture:** pre-merge canonical (cohort lesson 7-of-7); particular attention to comparison-semantics edge cases (one-sided vs two-sided incompatibility)

## Affected Areas

- foundation: federation-time handshake contract
- accelerators/anchor: per-Anchor version vector + compatibility check at peer-discovery time
- accelerators/bridge: managed-Bridge version surface; cross-instance interop
- federation-capability-sync: capability-token sync may reference version-vector

## Downstream Consumers

- **All Anchor clusters** that span multiple Sunfish versions
- **W#23 iOS Field-Capture** — iOS append-only event queue (ADR 0028-A1) federates with desktop Anchor at the merge boundary; needs version-vector compatibility
- **Bridge accelerator** — managed-Bridge instances may run different versions than self-hosted nodes
- **Long-offline reconnect** scenarios across schema-epoch boundaries

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery. Recommend authoring **first** of the four follow-on items per discovery §7.2 (smallest scope; resolves most concrete A4 spot-check finding; unblocks long-offline-reconnect logic).

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-04-30_mission-space-matrix.md` §5.8 + §6.4 + §7
- Active workstream: W#33 in `icm/_state/active-workstreams.md`
- ADR 0028 + amendments A1–A4
- Sibling intake: `icm/00_intake/output/2026-04-30_cross-form-factor-migration-intake.md` (ADR 0028-A5)
- Mission Space plan: `~/.claude/plans/this-looks-pretty-comprehensive-concurrent-floyd.md`
