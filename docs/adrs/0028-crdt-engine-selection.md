# ADR 0028 — CRDT Engine Selection

**Status:** Accepted (2026-04-22; **A1 + A2 + A3 + A4 mobile amendments landed 2026-04-30** — see §"Amendments (post-acceptance)")
**Date:** 2026-04-22 (Accepted) / 2026-04-30 (A1 mobile amendment / A2 council-fix amendments / A3 retraction of A2.4 false-vapourware / A4 retraction of A2.10 false-positive `JsonCanonical` claim)
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
- [`mesh-vpn-cross-network-transport-intake-2026-04-28.md`](../../icm/00_intake/output/mesh-vpn-cross-network-transport-intake-2026-04-28.md) — three-tier peer transport intake (`design-in-flight`; A2.4 corrected the prior premature "ADR 0061" reference)

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

### A2 (REQUIRED, mechanical) — A1 council-review fixes

**Driver:** Stage 1.5 council review of A1 (`icm/07_review/output/adr-audits/0028-A1-council-review-2026-04-30.md`, dated 2026-04-30; PR #345) ran pre-merge per the cohort discipline — auto-merge on PR #342 was intentionally disabled to allow council to review A1 before it lands. Council found 1 Critical + 4 Major + 2 Minor + 1 Encouraged. All 5 required + 3 encouraged are mechanical (per Decision Discipline Rule 3); A2 applies them and the cohort batting average updates to **8-of-8 substrate amendments needing post-acceptance amendments after council review**.

#### A2.1 — F2 Critical fix: per-event-type conflict-policy table (replaces A1.2's blanket LWW)

The blanket "LWW for mutating domains" framing in A1.2 conflates event types with fundamentally different concurrency requirements. Council found this is the **same gap ADR 0054's council caught on concurrent revocation** (council A4: "earliest-by-Lamport-timestamp"). LWW on wall-clock with no causality silently rolls back forward-only status transitions when stale devices reconnect with older state.

Replace A1.2's "Conflict resolution at Anchor merge boundary" section's LWW paragraph with this per-event-type policy table:

| `event_type` | Conflict policy | Rationale |
|---|---|---|
| `Inspection` | Append-only at photo / finding leaf; **forward-only `Inspection.Status`** (draft → submitted → signed-off; backward transitions REJECTED at merge + emitted as `InspectionStatusRollbackAttempted` audit-event for human review) | Status has required ordering; LWW would silently undo signed-off-by-owner with stale-iPad's still-draft |
| `Receipt` | Append-only | Immutable post-capture; LWW not invoked |
| `Asset` | LWW per field for mutable attributes (e.g., `Asset.LastSeenLocation`); **forward-only `Asset.WorkOrderStatus`** (acknowledged → in-progress → blocked → complete; no rollback; rollback attempts → `AssetWorkOrderStatusRollbackAttempted` audit) | Status has required ordering; same forensic risk as `Inspection.Status` |
| `Signature` | Append-only (per ADR 0054) | Revocations are separate append events; no field-level conflict |
| `Mileage` | Append-only | Immutable post-capture |
| `WorkOrderResponse` | Append-only at response leaf; **forward-only `WorkOrderResponse.Status`** (acknowledged → in-progress → blocked → complete; same forward-only-flag-guard as `Asset.WorkOrderStatus`) | Status has required ordering |

**Forward-only-status guard mechanics:** the Anchor merge service receives the iOS event, looks up the current `(tenant, entity_id, status_field_name)` value, compares against the event's proposed new status using a domain-specific `IsForwardTransition(current, proposed)` predicate, and rejects backward transitions. Rejected events emit a domain-specific `*StatusRollbackAttempted` audit-record (NOT silently dropped) with `(device_id, device_local_seq, captured_at, current_status_at_merge, proposed_status, rejected_at)` payload. Human operator reviews the audit trail; legitimate stale-state corrections require explicit Anchor-side override (out-of-band; not via iOS).

This is the **load-bearing substrate fix.** Without it, A1 ships a forensic foot-gun where status transitions look correct (deterministic, audit-trail-preserved) but only surface as disputes weeks later.

#### A2.2 — F1 Major fix: explicit `URLSessionConfiguration.background` settings

A1.1 asserted `URLSessionConfiguration.background` provides "durability + retry semantics out-of-band" — true under specific settings A1 didn't name. Add new sub-section A1.2.1 immediately after A1.2's event-queue contract:

##### A1.2.1 — `URLSessionConfiguration.background` settings (per A2.2)

Phase 2.1 reference impl pins these settings explicitly; Stage 06 build verifies via integration test:

```swift
let config = URLSessionConfiguration.background(withIdentifier: "dev.sunfish.field.upload")
config.discretionary = false                 // foreground-captured events upload immediately;
                                              // do NOT defer until Wi-Fi + plugged-in
config.sessionSendsLaunchEvents = true       // OS may relaunch app to deliver completion
config.allowsCellularAccess = true            // explicit; not the default for `.background`
config.waitsForConnectivity = true            // queue uploads when offline; resume on connectivity
config.timeoutIntervalForResource = 7 * 24 * 3600  // 7-day window for resumable uploads
```

**Blob uploads:** ALWAYS use file-based `uploadTask(with:fromFile:)`. Memory-based upload bodies do NOT survive `NSURLErrorBackgroundSessionWasDisconnected` (-997; fires on device reboot mid-upload or force-quit) — only file-based tasks are OS-resumable.

**Empty-body POSTs:** event-envelope-only POSTs (no blob payload) have been silently dropped in some iOS versions; Stage 06 ships explicit integration tests on the W#23 OQ-I3 iOS 16 baseline. If empty-body fails, fall back to a 1-byte sentinel body OR a file-based upload of a small JSON sidecar.

**Retry semantics:** OS-managed exponential backoff per `URLSessionConfiguration` defaults; on `NSURLErrorBackgroundSessionWasDisconnected`, restart the upload via the file-based task path (uploads-from-file are resumable; uploads-from-memory restart from byte 0).

#### A2.3 — F3 Major fix: cited-symbol corrections in A1.6

A1.6's cited-symbol audit passed internally but failed externally on 2 of 4 citations. Per Decision Discipline Rule 6 + cohort precedent (A2 missed `ITenantKeyProvider`; A4 missed `IRecoveryClock` method-vs-property), A2 corrects:

**A1.6 corrected entries:**

- ~~"`Sunfish.Foundation.Crypto.Signature` / `SignedOperation<T>`"~~ — **unchanged** (verified existing per ADRs 0003/0004/0049)
- ~~"ADR 0032 (capability delegation) — for Keychain-stored pairing token reference"~~ → **replaced with:** "`device_id` is derived from the install's root Ed25519 public key per ADR 0032 (workspace switching with single root keypair stored once in OS keystore, line 114). Pairing-token surface for multi-device flows TBD pending Phase 2.2 multi-device ADR; A1 does NOT define a pairing-token shape."
- ~~"ADR 0054 (electronic signatures) — for signature canonicalization compatibility"~~ → **replaced with:** "Canonical-encoded payload per `Sunfish.Foundation.Canonicalization.JsonCanonical` (per ADR 0054 Amendment A1; RFC 8785 / JCS). The Phase 2.1 event envelope's `payload` field is JSON-canonical-encoded so that ContentHash binding round-trips through Anchor merge."
- ~~"ADR 0046-A2 (`EncryptedField`) — for at-rest encryption compatibility on iOS local store"~~ → **replaced with:** "At-rest encryption via SQLCipher whole-database encryption (per W#23 intake §"In scope" item 2). Phase 2.1 does NOT use per-field encryption (`EncryptedField` per ADR 0046-A2 is the desktop foundation-recovery substrate; not consumed by iOS Phase 2.1). If per-field encryption needs surface for iOS in a future phase, a dedicated ADR amendment will be authored."

#### A2.4 — F4 Major fix: drop forward-references to ADR 0061 (vapourware)

A1's "Affected packages" + the OQ-A1.1 default both forward-reference ADR 0061 (Three-Tier Peer Transport). **ADR 0061 does not exist on `origin/main`** — it's still an intake (`icm/00_intake/output/mesh-vpn-cross-network-transport-intake-2026-04-28.md`, status `design-in-flight`).

**Replacement everywhere ADR 0061 was cited:**

> *"Phase 2.2 cross-network-direct sync (e.g., iOS app talks to Anchor over mesh VPN) is GATED ON [`mesh-vpn-cross-network-transport-intake-2026-04-28.md`](../../icm/00_intake/output/mesh-vpn-cross-network-transport-intake-2026-04-28.md) being promoted to an Accepted ADR. Phase 2.1 ships managed-relay-only (Bridge as primary transport per paper §17.2 tier 3); upgrade-to-mesh is post-Phase-2.1 and out of scope for this amendment."*

A2 also strikes the "[ADR 0061]" reference in the §"References" footnote that A1 introduced (added on the assumption ADR 0061 was already on `origin/main`).

#### A2.5 — F5 Major fix: pick Phase 3 forward-compat Option α explicitly

A1.7's "Phase 3 CRDT-on-mobile can replace the queue OR coexist" is hand-waved. Coexistence is the worst case (dual representations with merge logic — exactly what ADR 0028 main was chosen to avoid). Pick **Option α: one-way migration**.

Replace A1.7's "Forward path" with:

> **Forward path (per A2.5):** Phase 2.1 event queue → Phase 3 CRDT-on-mobile via **one-way migration** (NO coexistence period). When Phase 3 ships:
>
> 1. Queue-shutdown precedes CRDT-startup.
> 2. Pending queue events are flushed to Anchor via the Phase 2.1 path; the device waits for ACK on every queued event before disabling the queue.
> 3. Once queue is empty + ACK'd, CRDT-startup begins. The CRDT replays the queue's history *into Lamport-timestamped operations at the merge boundary, with the Lamport stamp set to the device's Phase-3-startup-time clock*. Pre-Phase-3 events carry no genuine causality beyond per-device monotonic ordering — this is acceptable for the field-capture domain (status transitions are forward-only per A2.1; non-status events are append-only).
> 4. After CRDT-startup, the queue is deprecated. New captures go directly to CRDT operations.
>
> No dual-write window. No "queue as durable transport, CRDT as in-memory representation" coexistence. The asymmetry is intentional: pre-Phase-3 events are forensically *less precise* than Phase 3+ events, and that's fine — Phase 3 is the upgrade path, not a backfill of pre-existing causality.

#### A2.6 — F6 Encouraged fix: paper §6.1, §15, §19 reconciliation

Add a new sub-section A1.4.1 immediately after A1.4's "Compatibility with the main ADR 0028 decision":

##### A1.4.1 — Reconciliation with paper §6.1, §15, §19 (per A2.6)

- **Paper §6.1 (three peer-transport tiers):** iOS Phase 2.1 uses **tier 3 only** (Managed Relay via Bridge per paper §17.2). Tier 2 (mesh VPN; mDNS-or-Tailscale) is Phase 2.2 gated on the mesh-VPN intake promotion to ADR. Tier 1 (LAN-direct when iPad + Anchor on same Wi-Fi) is post-Phase-2.2.
- **Paper §15 (testing levels):** the iOS event queue ships with Level-1 property tests covering: `(device_id, device_local_seq)` uniqueness under retry storm; deterministic LWW + forward-only-status-guard semantics under reordered delivery; deterministic tiebreak by `device_id` lexicographic order; rejected-rollback emission shape (per A2.1's `*StatusRollbackAttempted` audit). Property tests live in W#23 Stage 06's test suite.
- **Paper §19 (CRDT compaction first-class):** the iOS event queue **also has a growth problem** — offline-iPad accumulation can reach hundreds of events × tens of MB blobs after a week offline. Compaction policy in A2.7 below addresses this.

#### A2.7 — F6/A6 Encouraged fix: queue-growth + compaction policy

Add a new sub-section A1.5.1 immediately after A1.5's "Affected packages":

##### A1.5.1 — Phase 2.1 queue-growth + compaction policy (per A2.7)

- **Hard cap:** 5000 events OR 500 MB blob storage on the device — whichever first triggers. User-visible warning at 80% (4000 events / 400 MB); user-visible block at 100% (no further captures until existing queue drains).
- **Event TTL:** 30 days unsubmitted = warning shown to operator on next app launch. 90 days = forced foregrounded re-auth + sync flow before further captures.
- **Local SQLite VACUUM:** runs on the next app launch after a successful Anchor-ACK of a batch ≥ 100 events (covers the case where high-throughput field days finish + the device is plugged in overnight).
- **Blob cleanup:** after Anchor confirms ingest of a `(device_id, device_local_seq)`, the corresponding local blob (photos / PDFs / signatures) is moved to a `Garbage/` directory + deleted on the next VACUUM cycle (preserves the ~24h grace window for retroactive verification).

Operator UX: queue-status row on the iOS app's home screen showing `<events queued>` + `<MB blob storage used>` + `<last successful sync>`. Tap to force-sync.

#### A2.8 — F7 Encouraged fix: Keychain semantics for `device_local_seq` persistence

A1's OQ-A1.2 said `device_local_seq` is "persisted in Keychain" without specifying `kSecAttrAccessible` policy or iCloud-Keychain-sync stance. Updated:

**Replace OQ-A1.2 default with:**

> **A1 default (per A2.8):** `device_local_seq` is persisted in the iOS Keychain alongside the pairing-token-derived install identity, with:
>
> - `kSecAttrAccessible = kSecAttrAccessibleAfterFirstUnlock` — survives passcode retention and reboot but requires post-reboot unlock
> - `kSecAttrSynchronizable = false` — **explicitly NOT synced via iCloud Keychain.** Each iPad / iPhone has its own `device_id` + sequence space; synchronizing would break "single-actor per device" framing
> - On app reinstall: Keychain entries persist by default (iOS does NOT erase Keychain on app uninstall as of iOS 16+; earlier behavior differs and is irrelevant to W#23 OQ-I3 baseline). Reinstall preserves `device_local_seq`; new app launch reads existing values.
>
> If a future use case requires cross-device sequence-space sharing (e.g., owner's iPhone + iPad share an actor identity), that triggers a Phase 2.2+ multi-device ADR.

#### A2.9 — F8 Encouraged fix: operator device-key read-back acknowledgment

A1.2 said "the iOS app never reads back state from Anchor — it only captures and sends." For most domains this holds. For `Signature` (ADR 0054 two-identity model), the iPad must hold the **operator's current device-key** locally to sign at capture time. This is a state read (not a CRDT merge) but A1 didn't acknowledge.

**Add to A1.2's "No on-device merge" paragraph:**

> **Exception (per A2.9):** the iPad reads its current operator device-key from Bridge at pairing time and refreshes periodically (per ADR 0046-A1 historical-keys projection). This is a state read, not a CRDT merge. Signatures captured during an offline window are signed under the iPad's then-cached operator key; verification resolves at the Anchor merge boundary via the historical-keys projection (every signature carries the device-key version that signed it; merge looks up the historical key for that version + verifies). No on-device merge logic needed.

#### A2.10 — Cited-symbol re-verification (Decision Discipline Rule 6)

Per the cohort lesson, A2 re-runs the cited-symbol audit. After A2.3's corrections:

| Symbol / reference | Status |
|---|---|
| `Sunfish.Foundation.Crypto.Signature` / `SignedOperation<T>` | ✓ verified existing on `origin/main` |
| `Sunfish.Foundation.Canonicalization.JsonCanonical` | ✓ verified existing per ADR 0054 A1 (named explicitly post-A2.3) |
| ADR 0032 (workspace switching; Ed25519 root keypair in OS keystore) | ✓ verified Accepted on `origin/main` |
| ADR 0046-A1 (historical-keys projection) | ✓ verified existing on `origin/main` (was the surviving citation; A2.3 dropped the invalid 0046-A2 reference) |
| `mesh-vpn-cross-network-transport-intake-2026-04-28.md` | ✓ verified existing as `design-in-flight` intake (NOT yet ADR; A2.4 dropped premature ADR 0061 citation) |
| Paper §6.1, §15, §19 | ✓ verified existing in `_shared/product/local-node-architecture-paper.md` |
| GRDB.swift, SQLCipher, AesGcm, URLSessionConfiguration, Keychain APIs | ✓ external; all Apple-platform APIs at iOS 16 baseline |

No introduced-by-A2 new `Sunfish.*` symbols (A2 is mechanical fix-ups + scope tightening; no new types).

#### A2.11 — Cohort batting average (updated)

**8-of-8 substrate ADR amendments** have now needed post-acceptance amendments after council review (A1 here being the 8th). Pattern is locked-in: every substrate amendment that surfaces non-trivial design content needs a council pass before merge. Cost of A1's pre-merge council: zero held-state, zero W#23 build pause; mechanical amendments applied in same PR via this A2 commit. Cost of skipping council pre-merge (A2-of-0046 case study): ~24h held-state + extra round-trip.

Pre-merge council remains canonical; XO MUST disable auto-merge on substrate ADR amendments + dispatch council before flipping any downstream ledger row.

### A3 (REQUIRED, mechanical retraction) — A2.4 false-vapourware claim retraction

**Driver:** A2.4 claimed "ADR 0061 does not exist on `origin/main` — it's still an intake" and replaced ADR 0061 citations with intake references "gated on intake being promoted to an Accepted ADR." This claim was **false** at the time A2 shipped. ADR 0061 (Three-Tier Peer Transport — mDNS / Mesh VPN / Managed Relay) is in fact Accepted on `origin/main` (PR #278 + amendments A1–A4 in PR #299, dated 2026-04-29). The A1 council subagent reported it as missing; A2 propagated the error without independent verification. Retroactively corrected here.

**Lesson codified:** council subagents are not infallible at cited-symbol verification. The cohort batting average ("9-of-9 substrate amendments needed council fixes") describes the *forward* failure mode (XO drafts → council finds gaps); A3 introduces a new failure mode entry: **council can also miss / falsely report missing**. XO must spot-check council citation claims before applying mechanical fixes derived from them, especially negative-existence claims ("X does not exist") which are easy to mis-verify if the council ran a stale workspace snapshot.

#### A3.1 — Retraction of A2.4

**Replace A2.4's substantive content with:**

> **A2.4 (RETRACTED by A3):** A2.4 claimed ADR 0061 was vapourware. This was incorrect; ADR 0061 (`docs/adrs/0061-three-tier-peer-transport.md`) is Accepted on `origin/main` (PR #278 + amendments A1–A4 in PR #299, 2026-04-29). The A1 council subagent's negative-existence claim was a false negative. A2.4's "Replace ADR 0061 citations with intake references" applied to A1's references — those are now retracted; ADR 0061 is the canonical citation for three-tier peer transport going forward.

**Restore the original ADR 0061 references** removed by A2.4:

- A1.4's "Affected packages" now reads: *"Phase 2.2 cross-network-direct sync (e.g., iOS app talks to Anchor over mesh VPN) consumes [ADR 0061](0061-three-tier-peer-transport.md)'s three-tier transport (Tier 1 mDNS / Tier 2 Mesh VPN / Tier 3 Managed Relay). Phase 2.1 ships managed-relay-only (Bridge as primary transport per paper §17.2 tier 3, equivalent to ADR 0061's Tier 3 — `BridgeRelayPeerTransport` per ADR 0061 A4); upgrade-to-mesh is Phase 2.2 per ADR 0061's three-tier model."*
- The §"References" footnote that A2.4 struck (ADR 0061 link) is **restored** — see this ADR's Reference list.

#### A3.2 — Re-verification

Per Decision Discipline Rule 6 + the new "council can also miss" lesson, A3 explicitly re-verifies:

```bash
$ git ls-tree origin/main docs/adrs/ | grep 0061
100644 blob 902de1f3da0c9e1621cfbd6d5a0f98a233934c14	docs/adrs/0061-three-tier-peer-transport.md
$ git show origin/main:docs/adrs/0061-three-tier-peer-transport.md | head -3
# ADR 0061 — Three-Tier Peer Transport Model (mDNS / Mesh VPN / Managed Relay)
**Status:** Accepted (2026-04-29 by CO; council-reviewed B-grade; amendments A1–A4 (Critical/Major) **landed 2026-04-29** — see §"Amendments (post-acceptance, 2026-04-29 council)")
```

ADR 0061 is fully specified at 605 lines with `Initial contract surface`, `Tier selection algorithm`, audit emission, and amendments A1–A4. It is the canonical citation for three-tier peer transport.

#### A3.3 — Memory note (council-can-miss data point)

A new operating-discipline data point lands in `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/` documenting this incident. Going forward: when a council subagent's mechanical-fix recommendation depends on a negative-existence claim about an ADR or symbol, XO MUST spot-check the actual `origin/main` state before applying. Single bash command (`git ls-tree origin/main docs/adrs/ | grep <number>`) is sufficient.

#### A3.4 — Cohort batting average note

A3 doesn't increment the substrate-amendment council batting average (still 9-of-9, since A3 isn't a council finding — it's an XO-initiated retraction of a previously-applied council recommendation). New separate metric: **council false-negative rate** = 1-of-9 across the cohort so far. Track for pattern; if it grows beyond ~2-of-N, may warrant XO running a separate "verify council's cited-symbol claims" pass on each council output.

### A4 (REQUIRED, mechanical retraction) — A2.10 false-positive claim retraction (`Sunfish.Foundation.Canonicalization.JsonCanonical`)

**Driver:** W#23 hand-off council review (`icm/07_review/output/adr-audits/W23-handoff-council-review-2026-04-30.md`, PR #356) found that A2.10's verified-symbols table line — *"`Sunfish.Foundation.Canonicalization.JsonCanonical` ✓ verified existing per ADR 0054 A1 (named explicitly post-A2.3)"* — is **incorrect**. The cited symbol does not exist on `origin/main` and is not delivered by ADR 0054 A1's implementation checklist (which is unchecked). This is a **second false-claim** in the cohort, the positive-existence sibling to A3's negative-existence retraction of A2.4 (false-negative on ADR 0061).

**Lesson reinforced:** the council-can-miss spot-check discipline (`feedback_council_can_miss_spot_check_negative_existence`) extends to **positive-existence claims** as well. XO must spot-check both directions: when council claims "X does not exist" AND when council recommends a citation by saying "X is verified existing." Both are easy to mis-verify if the council ran a stale workspace snapshot OR confused a documented-but-unshipped symbol (ADR-promised, implementation-unchecked) with a shipped symbol.

**Filed under cohort discipline:** the false-claim count (combining both directions) is now **2-of-9** as of 2026-04-30. This meets the ">~2-of-N" threshold A3.4 named for warranting a separate "verify council's cited-symbol claims" pass on each council output. Going forward, XO adds a standing rung-6 fallback task: spot-check the cited-symbol table in any substrate ADR amendment that ships under auto-merge — both negative-existence AND positive-existence claims.

#### A4.1 — Retraction of A2.10's `JsonCanonical` row

**A2.10's verified-symbols table is amended:** the row `"Sunfish.Foundation.Canonicalization.JsonCanonical | ✓ verified existing per ADR 0054 A1 (named explicitly post-A2.3)"` is **retracted**. Replacement classification: **VAPOURWARE per A4 (2026-04-30)**. ADR 0054 A1 promised the symbol + a new `packages/foundation-canonicalization/` package; implementation checklist is unchecked on `origin/main`. The existing canonicalizers (`Sunfish.Foundation.Crypto.CanonicalJson`, `Sunfish.Foundation.Assets.Common.JsonCanonicalizer`, `Sunfish.Kernel.Signatures.Canonicalization.JsonCanonicalCanonicalizer`) are explicitly NOT RFC 8785 conformant — `JsonCanonicalCanonicalizer.cs` self-documents this in XML remarks.

**A2.3's substantive content is retroactively softened:** A2.3 said *"Name canonicalizer: 'Canonical-encoded payload per `Sunfish.Foundation.Canonicalization.JsonCanonical` (per ADR 0054 Amendment A1; RFC 8785 / JCS).'"* Retract: the cited symbol is not yet shipped. **Replacement text:** *"Canonical-encoded payload per `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` (existing pragmatic canonicalizer; RFC 8785 strict conformance deferred to follow-up amendment when ADR 0054 A1 ships its promised `packages/foundation-canonicalization/` package)."*

**Consumer-side resolution** lives in the W#23 hand-off addendum §A1 (sibling PR #357): Phase 3's cross-language canonicalization test pins `CanonicalJson.Serialize` for Phase 2.1; strict RFC 8785 deferred.

#### A4.2 — Re-verification (with `git ls-tree`)

Per the strengthened spot-check discipline, A4 explicitly re-verifies via independent commands (reproducible by anyone reading this ADR):

```
$ git ls-tree -r origin/main | grep -iE "Canonicalization\." | head -10
# (zero matches in the Sunfish.Foundation.Canonicalization.* namespace; closest
#  is Sunfish.Kernel.Signatures.Canonicalization.JsonCanonicalCanonicalizer
#  which self-documents that it is NOT RFC 8785 conformant)

$ git show origin/main:packages/foundation/Crypto/CanonicalJson.cs | head -10
# (file exists; this is the existing pragmatic canonicalizer that A4.1's
#  replacement text pins)

$ git ls-tree origin/main packages/foundation-canonicalization 2>&1
# fatal: Not a valid object name: ...
# (the package does not exist on origin/main; ADR 0054 A1 promised but
#  implementation checklist unchecked)
```

#### A4.3 — Cohort metrics update

| Metric | Before A4 | After A4 |
|---|---|---|
| Substrate-amendment council batting average | 9-of-9 needing post-acceptance fixes | 10-of-10 (W#23 hand-off council added 1) |
| Council false-claim rate (both directions) | 1-of-9 (false-negative only; A2.4 retracted by A3) | **2-of-9** (false-negative + false-positive) |
| Threshold for warranting "verify council's claims" rung-6 task | ">~2-of-N" per A3.4 | **threshold met** |

**XO commitment:** standing rung-6 task — every substrate ADR amendment that ships under auto-merge gets a XO-side spot-check of the cited-symbol table within 24h of merge (cheap; single `git ls-tree` / `git grep` command per cited symbol). If a false-positive or false-negative surfaces, file an A_(N+1) retraction matching this A4 / A3 pattern.

#### A4.4 — Lesson extended in memory

The `feedback_council_can_miss_spot_check_negative_existence.md` memory is extended (separate PR / memory edit) to cover positive-existence claims. New title: *"Council subagents can miss in BOTH directions; spot-check both negative-existence claims AND positive-existence verification claims before applying their mechanical fixes."*
