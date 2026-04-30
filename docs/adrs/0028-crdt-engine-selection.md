# ADR 0028 — CRDT Engine Selection

**Status:** Accepted (2026-04-22; **A1 mobile amendment landed 2026-04-30** — see §"Amendments (post-acceptance)")
**Date:** 2026-04-22 (Accepted) / 2026-04-30 (A1 mobile amendment)
**Resolves:** Paper §9 mandates a CRDT engine with production-grade compaction, GC behavior, and compact binary encoding. Paper §19 names three candidates — Yjs, Loro, and an Automerge-inspired native-.NET implementation. This ADR picks one.

---

## Context

The local-node architecture paper (§2.4, §6.1, §9) treats the CRDT engine as kernel infrastructure: it backs AP-class data (§2.2), feeds the event log that sync exchanges over gossip (§6.1), and carries the majority of document state that domain plugins produce. Paper §9 is explicit that CRDT document growth — tombstones, insert/delete history, list and rich-text internals — is the single most dangerous operational risk in the architecture, and that **"library selection should treat compaction behavior as a first-class evaluation criterion alongside correctness and performance."**

The Sunfish runtime environment is .NET 11 preview. Per paper §5.1 the kernel is a **microkernel monolith** with domain plugins running in-process "to avoid inter-process communication overhead." That rules out architectures that push CRDT execution into a sidecar as the steady-state shape.

A prior evaluation — [`docs/specifications/research-notes/automerge-evaluation.md`](../specifications/research-notes/automerge-evaluation.md) (2026-04-17) — surveyed Automerge, Automerge-Repo, and Keyhive and concluded that Automerge is an excellent **design reference** but not a drop-in .NET dependency. Four mismatches were identified, with the lack of a first-class .NET binding at the top. That evaluation deferred the engine choice to a later ADR. This is that ADR.

---

## Requirements

- **.NET interop** — library must be callable from .NET 11 without a sidecar process in steady state (paper §5.1).
- **Rich-text + list + map CRDT types** — all three are used by domain plugins (paper §2.2 document/note/task data, §9 rich-text and list growth).
- **Compaction / GC** — production-grade, required per paper §9. Not a research prototype.
- **Compact binary encoding** — bandwidth-efficient gossip exchange per paper §6.1.
- **Active maintenance** — the library must stay alive for at least the 18-month implementation roadmap per paper §18.
- **Permissive license** — MIT, Apache 2.0, or similar. CC-BY-SA or strong copyleft is disqualifying for the Sunfish open-source strategy (paper §17.1).

---

## Candidates

### Option A — Yjs + yrs (Rust port) via .NET bindings

- Most mature CRDT library; widely deployed (Figma, Notion, others).
- yrs is the Rust port; .NET binding via P/Invoke or a generated C# wrapper over the C FFI.
- Pro: proven in production at scale; excellent documentation; well-understood tombstone GC; rich-text + list + map first-class.
- Con: .NET binding is an extra layer we maintain; marshaling cost; the binding's maintenance is an ongoing concern.
- License: MIT.

### Option B — Loro

- Newer Rust-native CRDT library. Paper §19 explicitly flags **"compact encoding + shallow snapshots"** as its strength.
- .NET binding via the same P/Invoke approach as yrs.
- Pro: designed with compaction as a primary feature; smaller runtime memory footprint in published benchmarks; rich-text + list + map first-class; shallow-snapshot support maps directly onto paper §9 mitigation 3.
- Con: less battle-tested than Yjs; smaller ecosystem; smaller pool of debugging war-stories to learn from.
- License: MIT.

### Option C — Native .NET implementation inspired by Automerge/Yjs

- Ship a Sunfish-authored CRDT library in C#.
- Pro: no binding layer; full control; aligns with "everything is .NET" ergonomics; matches the path the Automerge evaluation §4 sketched for "adopt the patterns, not the library."
- Con: reinventing the wheel on subtle correctness work; library-level compaction alone is years of engineering; maintenance is ours alone; contradicts paper §19's "production-validated" framing.
- License: ours (MIT per paper §17.1).

### Option D — Automerge via sidecar process

- Keep Automerge as-is (Rust); expose via IPC.
- Pro: use Automerge's correctness guarantees directly.
- Con: sidecar process is operational complexity; directly contradicts paper §5.1 "all running in-process to avoid IPC overhead." The Automerge evaluation §3.1 listed this path as "plausible, painful."
- License: MIT.

---

## Decision (recommended)

**Adopt Option B — Loro.**

Rationale:

- Paper §9 explicitly cites Loro's compact encoding + shallow snapshots as the production-validated answer to CRDT growth. Paper §19's row for "CRDT compaction" names Yjs (internal GC) and Loro (compact encoding + shallow snapshots) side-by-side; Loro's design advantage is specifically the growth-management axis, which paper §9 elevates to "first-class evaluation criterion."
- Loro's design treats compaction as a primary concern rather than an optimization retrofit. That framing matches paper §9's requirements better than Yjs's emergent-GC approach.
- Smaller memory footprint suits the desktop + mobile deployment envelope implied by paper §4.
- .NET binding effort is comparable to yrs; we pay it once either way. Starting with Loro means we do not retrofit later when Yjs's edge-case tombstone behavior becomes the binding work we have to redo.
- License (MIT) is compatible with paper §17.1 open-source strategy.

Fallback: if Loro's .NET binding maturity or .NET 11 compatibility proves intractable, we fall back to **Option A (Yjs/yrs)**. A small evaluation spike validates before committing. Option C and Option D remain rejected for the reasons listed; neither is the fallback.

---

## Decision consequences

### Positive

- Compaction story is "use Loro's compaction" — paper §9's first mitigation strategy is library-provided rather than homegrown.
- Application-level document sharding (paper §9 mitigation 2) becomes a kernel-API concern, not a runtime-CRDT concern.
- Shallow-snapshot support (paper §9 mitigation 3) is a library feature rather than an application-authored capability.
- Small, well-scoped binding package to maintain.
- Community momentum: Loro is gaining adoption in modern local-first apps; ecosystem growth is expected over the 18-month roadmap.

### Negative

- Less battle-tested than Yjs; we may hit issues Yjs users have already debugged. Mitigated by: initial 1-week validation spike, fallback to Option A defined, property-based test harness at paper §15 Level 1 covering convergence/idempotency/commutativity/monotonicity.
- Binding maintenance is a Sunfish-owned responsibility. Mitigated by: the binding is a thin wrapper; we are not forking Loro itself.

---

## Compatibility plan

- Wrap Loro behind a Sunfish CRDT contract (`ICrdtDocument`, `ICrdtText`, `ICrdtMap`, `ICrdtList`) so that a future library swap (e.g., fallback to Yjs) does not ripple through application code.
- The binding package lives in `packages/kernel-crdt/` per Wave 1.2 of the paper-alignment plan.
- The `ICrdtDocument` contract also isolates domain plugins from Loro's wire format — the event log (paper §5.1, §6.1) serializes through kernel-controlled encoding, so an engine swap does not require a schema epoch bump (paper §7.4) by itself.

---

## Implementation checklist

- [ ] 1-week validation spike: build a trivial `ICrdtDocument` wrapper around Loro in .NET; prove P/Invoke works on Windows / macOS / Linux; measure marshaling cost; test rich-text insert/delete + list append + map set.
- [ ] If spike passes: scaffold `packages/kernel-crdt/` with `Loro.NET` or equivalent dependency.
- [ ] If spike fails: re-run with yrs (Option A); update this ADR's recommendation to Option A and move to Accepted.
- [ ] Property-based test harness (paper §15 Level 1) using the `ICrdtDocument` contract — CRDT convergence, idempotency, commutativity, monotonicity.
- [ ] Stress test for CRDT growth under high-churn documents (paper §15.1 explicit requirement) — verify library-level compaction keeps document size bounded.
- [ ] Document application-level sharding pattern as an application concern per paper §9 mitigation 2.
- [ ] Document shallow-snapshot policy per paper §9 mitigation 3 — opt-in per document type, reserved for well-understood cases.

---

## References

- `_shared/product/local-node-architecture-paper.md` §2.4, §5.1, §6.1, §9, §15.1, §17.1, §18, §19
- [`docs/specifications/research-notes/automerge-evaluation.md`](../specifications/research-notes/automerge-evaluation.md) — prior Automerge evaluation (2026-04-17)
- [Loro GitHub](https://github.com/loro-dev/loro)
- [Yjs](https://github.com/yjs/yjs) and [yrs (y-crdt)](https://github.com/y-crdt/y-crdt)
- [ADR 0023](0023-dialog-provider-slot-methods.md), [ADR 0024](0024-button-variant-enum-expansion.md), [ADR 0025](0025-css-class-prefix-policy.md) — ADR format references
- [ADR 0048](0048-anchor-multi-backend-maui.md) — Anchor multi-backend (Win/Mac/iOS/Android via MAUI; Linux/Wasm via Avalonia)
- [ADR 0061](0061-three-tier-peer-transport.md) — Three-tier peer transport (mDNS / Mesh VPN / Managed Relay)

---

## Amendments (post-acceptance)

### A1 (REQUIRED) — Mobile reality check; iOS Phase 2.1 ships append-only events (no CRDT engine), full CRDT-on-mobile deferred

**Date:** 2026-04-30
**Driver:** Workstream #23 iOS Field-Capture App intake (`icm/00_intake/output/property-ios-field-app-intake-2026-04-28.md` §"In scope" item 10) explicitly calls for an ADR 0028 mobile amendment. The original ADR scoped CRDT-engine selection for desktop deployments (Win/Mac/Linux per ADR 0048). It did not address mobile platforms (iOS / Android), and iOS is now a Phase 2.1 deliverable per W#23.

#### A1.1 — Why mobile is different from desktop

ADR 0028 selected **Loro** (Rust core; .NET binding for desktop). The selection rationale (paper §9 compaction, MIT license, growth-management) holds for desktop. For iOS Phase 2.1 (W#23), three constraints invert the calculus:

- **No mature Swift binding for Loro at ADR-acceptance time (2026-04-30).** Loro is Rust-native; Swift consumes Rust via either (a) Swift's `@_cdecl` C-ABI bridge, (b) `swift-bridge`, or (c) UniFFI. As of ADR-acceptance, none of these has shipped a production-grade Loro Swift package. (W#23 Stage 02 must spike + verify; if a viable binding has emerged, that triggers an A2 amendment relaxing this position.)
- **Field-capture workflow is single-actor per device.** The iOS app captures inspections / receipts / mileage / signatures from the field; conflicts with the desktop multi-actor surface arise only when the device syncs back to Anchor. This is a fundamentally different concurrency profile than the multi-actor real-time editing scenario CRDT engines are designed for.
- **Background URLSession + append-only event queue is a simpler model.** iOS's `URLSessionConfiguration.background` provides durability + retry semantics out-of-band — the OS finishes uploads after app suspension, retries on network change. An append-only event log (capture-only, last-write-wins resolution at the Anchor merge boundary) maps cleanly onto that.

#### A1.2 — Phase 2.1 iOS contract: append-only event queue (no CRDT)

**Decision:** iOS Phase 2.1 ships a **capture-only append-only event queue**. NO CRDT engine on the device. Conflicts are resolved at the Anchor merge boundary using existing Anchor CRDT primitives (Loro on Anchor; per ADR 0028 main decision).

**Event-queue contract** (W#23 Stage 06 will spec the full surface; this amendment names the shape):

- **Per-event envelope:** monotonically-increasing `device_local_seq` (uint64) + `captured_at` (ISO 8601 UTC) + `device_id` (Keychain-stored pairing token's actor identifier per ADR 0032) + `event_type` (capture domain enum: `Inspection`, `Receipt`, `Asset`, `Signature`, `Mileage`, `WorkOrderResponse`) + `payload` (domain-specific JSON; canonical-encoded per RFC 8785 for ContentHash binding compatibility per ADR 0054 + ADR 0046-A2)
- **Local store:** GRDB.swift over SQLite (optionally SQLCipher); single-writer-per-device serialization via `device_local_seq`
- **Outbound transport:** `URLSessionConfiguration.background` to Bridge's blob-ingest API (per W#23 OQ3); resumable uploads for blob payloads (photos / PDFs / signatures); event-envelope uploads are JSON POSTs
- **Retry semantics:** exponential backoff per `URLSessionConfiguration` defaults; `device_local_seq` is the dedup key on Anchor side (re-deliveries don't double-count)
- **No on-device merge:** the iOS app never reads back state from Anchor and merges — it only captures and sends. Read-side queries against shared state go through the Bridge HTTPS surface (Phase 2.1 read-only views).

**Conflict resolution at Anchor merge boundary:**

- Each `(device_id, device_local_seq)` is a unique event identity. Anchor's existing Loro store consumes events in `(captured_at, device_id, device_local_seq)` order.
- **Last-write-wins semantics** for fields that two devices captured concurrently — the latest `captured_at` wins; ties broken by `device_id` lexicographic order (deterministic).
- For append-only domains (e.g., signatures, audit records), there are no conflicts — both events land in the trail.
- For mutating domains (e.g., asset condition updates), LWW is the documented Phase 2.1 policy. **Future Phase 3 amendment** introduces a richer conflict-resolution policy if real conflicts surface (e.g., spouse + contractor both editing the same asset's condition within the same minute).

#### A1.3 — When CRDT-on-mobile is reconsidered

A future ADR 0028-A2 amendment (or new ADR) revisits this when ANY of these triggers fire:

- **Loro publishes a production-grade Swift binding** with sustained maintainer activity (proxy: 6+ months of weekly commits + integration test suite).
- **Field-capture workflow gains a multi-actor real-time scenario** (currently none — single-actor per device with intentional sync-back-to-Anchor merge). Examples that would trigger: contractor + owner editing the same inspection finding live; iPad-to-iPad direct sync without Anchor mediation.
- **Append-only LWW produces material data loss** in a real workflow (a forcing function; we don't speculate about hypothetical losses).
- **Anchor side ships rich-CRDT contract** that the iOS app cannot satisfy with append-only events (e.g., field-level concurrency primitives that LWW cannot represent).

The default expectation is that Phase 2.1 + Phase 2.2 do NOT need CRDT on iOS. Phase 3 reassessment is a planned milestone, not an inevitable rework.

#### A1.4 — Compatibility with the main ADR 0028 decision

This amendment does NOT change the desktop decision. Loro remains the selection for Anchor (Win/Mac/Linux per ADR 0048 multi-backend MAUI). The amendment scopes a **mobile carve-out**: iOS Phase 2.1 does not adopt Loro on the device.

The Anchor-side Loro store consumes the iOS event queue at the merge boundary; this is no different from how Anchor would consume any other event source (a third-party integration, a webhook, etc.).

#### A1.5 — Affected packages + downstream impact

- **`accelerators/anchor-mobile-ios/`** (new family per W#23 intake item 9; Stage 02 decides between this path and `apps/field/`): does NOT depend on `Sunfish.Crdt.*` or any Loro binding. Pure SwiftUI + GRDB.swift + URLSession.
- **Bridge blob-ingest API** (cross-cutting OQ3 in property-ops INDEX): the receiving end of the iOS event queue. Phase 2.1 ships this; W#28 Public Listings owns the Bridge route family.
- **Anchor Loro store**: consumes iOS events at merge. No changes to the Loro binding layer required for this amendment; iOS events are JSON-encoded payloads that the consumer service deserializes + applies via Anchor's existing CRDT mutation API.

#### A1.6 — Cited-symbol verification (Decision Discipline Rule 6)

This amendment is at the architectural / paper layer; it does not introduce new `Sunfish.*` source symbols. It references:

- `Sunfish.Foundation.Crypto.Signature` / `SignedOperation<T>` (existing per ADR 0004 + ADR 0049) — for ContentHash binding compatibility
- ADR 0032 (capability delegation) — for Keychain-stored pairing token reference
- ADR 0054 (electronic signatures) — for signature canonicalization compatibility
- ADR 0046-A2 (`EncryptedField`) — for at-rest encryption compatibility on iOS local store

All cited ADRs are Accepted on `origin/main` as of 2026-04-30. No introduced-by-this-amendment symbols.

#### A1.7 — Compatibility plan

- **Existing callers:** none (iOS family doesn't exist yet).
- **Migration:** N/A; this amendment scopes the iOS approach BEFORE any iOS code ships.
- **Forward path:** Phase 2.1 event queue → Phase 3 CRDT-on-mobile (if triggered per A1.3). The event-queue contract (A1.2) is forward-compatible: Phase 3 CRDT-on-mobile can replace the queue OR coexist (queue as the durable transport; CRDT as the in-memory representation).

#### A1.8 — Open questions

- **OQ-A1.1:** Should the iOS event queue support cross-device read consistency (e.g., owner's iPhone sees the iPad's just-captured inspection)? **A1 default:** no — read-side goes through Bridge in Phase 2.1. Phase 2.2 may add iOS-direct-to-Anchor read via Tailscale per ADR 0061.
- **OQ-A1.2:** Should `device_local_seq` be reset on app reinstall, or persisted in Keychain? **A1 default:** persisted in Keychain alongside the pairing token; reinstall preserves the sequence (avoids dedup collisions).
- **OQ-A1.3:** What happens if a device is decommissioned (sold / lost) and a new device is paired with a previously-used `device_id`? **A1 default:** new pairing issues a new `device_id`; old `device_id` is revoked at Anchor (capability revocation per ADR 0032). Sequence-space partitioning is preserved.

#### A1.9 — Pre-acceptance audit

- **AP-1 (unvalidated assumption):** Loro Swift binding maturity claim (A1.1) is verifiable but time-sensitive — W#23 Stage 02 spike must verify. AP applies to the `2026-04-30` snapshot only; future amendments revisit per A1.3 triggers.
- **AP-3 (vague success criteria):** A1.3 trigger conditions are reasonably specific (6+ months of binding maintainer activity; multi-actor field workflow; material data loss). Pass.
- **AP-21 (cited facts):** all cited ADRs verified Accepted on `origin/main`. Pass.

This amendment is `Accepted` upon merge of the PR introducing it. Per the cohort lesson (7-of-7 substrate ADR amendments needed council fixes), this PR's auto-merge is intentionally disabled until a Stage 1.5 council subagent reviews.
