# ADR 0028 — CRDT Engine Selection

**Status:** Accepted (2026-04-22; **A1 + A2 + A3 + A4 amendments landed 2026-04-30; A6 amendment proposed 2026-04-30** — see §"Amendments (post-acceptance)")
**Date:** 2026-04-22 (Accepted) / 2026-04-30 (A1 mobile amendment / A2 council-fix amendments / A3 retraction of A2.4 false-vapourware / A4 retraction of A2.10 false-positive `JsonCanonical` claim / A6 version-vector compatibility contract — proposed pending pre-merge council; A5 cross-form-factor migration is queued separately and depends on A6's compatibility relation)
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

### A5 (PROPOSED) — Cross-device + cross-form-factor migration semantics

**Driver:** W#33 Mission Space Matrix discovery (`icm/01_discovery/output/2026-04-30_mission-space-matrix.md` §5.7) identifies cross-form-factor migration as a **Gap** with one peripheral hint (ADR 0028-A1's iOS Phase 2.1 capture-only events) and no general specification. Paper §13.4 covers QR-onboarding multi-device flow but does not formalize cross-form-factor data filtering; paper §15.2 covers schema-version mixed-cluster testing but not feature-surface migration; ADR 0046 covers encrypted-field rotation but not cross-device transfer as a Mission-Space-aware migration. A5 generalizes A1's iOS-specific carve-out into a form-factor-agnostic migration semantic that operates on top of A6's compatibility relation.

**Pipeline:** `sunfish-api-change` (introduces migration-semantics contract; affects cross-form-factor data flow; downstream consumers across foundation + accelerators).

**Authoring sequence:** A6 first, A5 second. A5 cites A6's compatibility relation as input. A5's amendment number is lower than A6's (5 < 6) for substrate-numbering hygiene — when A5 was reserved during A6 authoring, the reservation was for this specific companion semantic. Authoring order does not match numeric order.

**Companion amendment:** A6 ships first (now on `origin/main` post-A7 council fixes); A5 builds on A6.5 (one-sided receive-only mode) as the canonical "long-offline reconnect" pattern that A5's migration semantics inherit and generalize.

#### A5.1 — Form-factor type signature + migration table

The form-factor tuple is:

```text
FormFactorProfile ::= {
    formFactor:           enum { Laptop, Desktop, Tablet, Phone, Watch, Headless, Iot, Vehicle },
    inputModalities:      set<enum { Pointer, Keyboard, Touch, Voice, Pen, GestureSensor, None }>,
    displayClass:         enum { Large, Medium, Small, MicroDisplay, NoDisplay },
    networkPosture:       enum { AlwaysConnected, IntermittentConnected, OfflineFirst, AirGapped },
    storageBudgetMb:      uint32,
    powerProfile:         enum { Wallpower, Battery, LowPower, IntermittentBattery },
    sensorSurface:        set<enum { Camera, Mic, Gps, Accelerometer, BiometricAuth, NfcReader, BarcodeScanner }>,
    instanceClass:        enum { SelfHost, ManagedBridge }   // matches A6.1's reduced enum per A7.6
}
```

**Encoded form (per `Sunfish.Foundation.Crypto.CanonicalJson.Serialize`, casing per A7.8):**

```json
{
  "formFactor": "tablet",
  "inputModalities": ["touch", "pen", "voice"],
  "displayClass": "medium",
  "networkPosture": "intermittentConnected",
  "storageBudgetMb": 8192,
  "powerProfile": "battery",
  "sensorSurface": ["camera", "mic", "gps", "biometricAuth"],
  "instanceClass": "selfHost"
}
```

**Cross-form-factor migration table (canonical):**

| Source → Target | Same data set? | Filter applied | Capability re-evaluation | Notes |
|---|---|---|---|---|
| **Laptop → Laptop** (cluster-add) | Full sync | None | None (same surface) | Default home-office add-second-machine flow |
| **Laptop → Desktop** | Full sync | None | None (same surface) | Form-factor-equivalent |
| **Laptop ↔ Tablet** (added to team) | Tablet receives feature-gated subset | `tabletProfile.capabilities ∩ workspace.declaredCapabilities` | Re-evaluate at sync time per A6.5 (one-sided receive-only if A6 incompat) | Tablet lacks keyboard-heavy admin UIs by default; data flows in both directions |
| **Laptop ↔ Phone** (added to team) | Phone receives mobile-first subset | `phoneProfile.capabilities ∩ workspace.declaredCapabilities ∩ mobileSafeSet` | Re-evaluate; per ADR 0028-A1, phone is append-only event-queue path until Phase 3+ CRDT-on-mobile lands | iOS append-only path per A6.11 (capture-context tagging) |
| **Laptop ↔ Watch** (added to team) | Watch receives glanceable-summary subset | `watchProfile.capabilities ∩ glanceableSet` (read-only by default) | Re-evaluate; watch is read-only consumer until proven otherwise | No write-back path in v0; deferred to a future amendment |
| **Tablet → Phone** (form-factor down) | Phone receives intersection of tablet + phone surfaces | `phoneProfile.capabilities ∩ tablet.allowedSurface` | Re-evaluate; A6 compatibility check first | Same-tier downgrade |
| **Laptop ↔ Headless** (e.g., bridge node addition) | Headless receives substrate-only data; no UI surface | `headlessProfile.capabilities ∩ substrateOnlySet` | Re-evaluate; UI features filtered out | Backend-only deployment |
| **Laptop ↔ IoT** (e.g., sensor node) | IoT receives sensor-config + ingest-target only | `iotProfile.capabilities ∩ sensorIngestSet` | Re-evaluate; everything else filtered | High-asymmetry: IoT writes data, reads almost nothing |
| **Laptop ↔ Vehicle** (e.g., in-car field-capture) | Vehicle receives gps-tagged-write + voice-input subset | `vehicleProfile.capabilities ∩ vehicleSafeSet` | Re-evaluate; driver-distraction filter applied | Reserved profile; v0 doesn't ship a Vehicle adapter |

The "filter applied" column is the **derived-surface filter**: each form-factor's expected capability surface is computed as the intersection of (a) the form-factor's `FormFactorProfile.capabilities` (declared by the device adapter at install time) and (b) the workspace's `declaredCapabilities` (the union of capabilities any installed plugin offers). The filter never adds; it only removes.

**Non-table generalization:** the table above is canonical for the form factors named in `FormFactorProfile.formFactor`. New form factors (e.g., when a Vehicle adapter actually ships) extend the table by declaring their `FormFactorProfile` and a corresponding row.

#### A5.2 — Migration semantics rules

When form factor F is added to or removed from a workspace, A5 specifies:

> **Rule 1 — Additive sync respects derived surface.** When F is added (becomes a peer in the workspace's federation), F's incoming sync receives only data classified by features in `F.derivedSurface` (per A5.1's filter). Data outside F's derived surface is sequestered in F's local queue with a `FormFactorFilteredOut` flag; the data is NOT dropped.
>
> **Rule 2 — Outgoing writes are unfiltered.** Whatever F creates flows back to the workspace per the normal CRDT path; the derived-surface filter is INBOUND-ONLY. F can author any data it can construct UI / API surface for; the workspace's other peers receive and merge that data normally.
>
> **Rule 3 — Capability re-evaluation cadence.** Each time F initiates a federation handshake (per A6.3), F's `FormFactorProfile` is re-evaluated against the workspace's current `declaredCapabilities`. If the capability set has changed since the last handshake, F's derived surface is recomputed and the local sequestration set is reconsidered (sequestered events that now match the surface are released; previously-released events that no longer match are re-sequestered).
>
> **Rule 4 — Hardware-tier change triggers immediate re-evaluation.** When F's hardware capabilities change (e.g., the device's `storageBudgetMb` shrinks because the user installed other apps; the user disables the camera permission; the device is in a low-battery profile), the next sync triggers a fresh `FormFactorProfile` calculation. Re-evaluation is at sync-time, not real-time — A5 does not specify a continuously-monitoring background process.
>
> **Rule 5 — Cross-tier downgrades preserve data.** Per the data-loss-vs-feature-loss invariant (A5.4 below), downgrading a form factor's tier (e.g., user replaces flagship phone with budget phone with less storage) does NOT delete data. Data exceeding the new storage budget is sequestered with a `StorageBudgetExceeded` flag; the workspace's other peers retain it; F can continue to access it via federation re-fetch when the storage budget is restored.

#### A5.3 — Hardware-tier re-evaluation mechanics

A5 specifies how hardware-tier changes propagate:

```text
HardwareTierChangeEvent ::= {
    nodeId:            NodeId,
    previousProfile:   FormFactorProfile,    // the FormFactorProfile at the time of the previous successful handshake
    currentProfile:    FormFactorProfile,    // the FormFactorProfile at the time this event fires
    triggeringEvent:   enum { StorageBudgetChanged, NetworkPostureChanged, SensorPermissionChanged, PowerProfileChanged, AdapterUpgrade, AdapterDowngrade, ManualReprofile },
    detectedAt:        ISO 8601 UTC
}
```

**Mechanics:**

1. The form-factor adapter (e.g., the iOS adapter; the Headless adapter) is responsible for detecting hardware-tier changes and producing `HardwareTierChangeEvent` records.
2. On detection, the local node:
   - Recomputes `FormFactorProfile`
   - If the new `FormFactorProfile.capabilities` ≠ the previous, recomputes `derivedSurface`
   - Sequesters/releases data per the new derived surface (per Rule 1)
   - Emits a `Sunfish.Kernel.Audit.AuditEventType.HardwareTierChanged` audit event with the `(previousProfile, currentProfile, triggeringEvent)` tuple
3. Next federation handshake (per A6.3) carries the new `FormFactorProfile` in the version vector exchange. Other peers update their cached view of this peer's surface.

**Detection cadence (form-factor-adapter responsibility, not A5's):**

| Form factor | Detection cadence | Why |
|---|---|---|
| iOS / iPadOS | On app foreground, on permission-change OS callback, on `lowPowerModeEnabled` flip | OS surfaces these events directly |
| Android | On app foreground, `ConnectivityManager.NetworkCallback`, `BatteryManager` broadcast | Same as iOS pattern |
| MAUI desktop | On app start; periodic 5-min poll for storage-budget changes | Desktop adapters have less OS event surface |
| Headless / Bridge | On adapter startup; on systemd `LowResources` signal (Linux) | Less likely to need re-evaluation |
| Watch | On app foreground; pairing-handshake to phone re-fires | Watch profile is mostly static |

A5 does NOT specify the per-adapter detection logic — that's the adapter's contract. A5 specifies only the format of the event and the mechanics of consuming it.

#### A5.4 — Data-loss-vs-feature-loss invariant

A5 makes one explicit substrate-tier guarantee:

> **Invariant DLF — feature deactivation never causes data loss.** Data created under capability C, when C becomes unavailable on form factor F (because F was downgraded; because C was removed from the workspace by an admin; because F now lacks the sensor/permission C requires), is preserved as **read-only-but-not-lost**. The data remains in F's local replica (or, if F's storage budget is exceeded, in another peer's replica accessible via federation re-fetch). The data is never silently dropped.

**Concrete behaviors required by Invariant DLF:**

1. **Sequestration over deletion.** When F's derived surface contracts and previously-visible data falls outside it, the data is moved to an in-replica "sequestered" partition with a `FormFactorFilteredOut` or `StorageBudgetExceeded` flag — NOT deleted.
2. **Re-emergence on surface expansion.** If F's derived surface later expands (capability restored; storage budget increased; permission re-granted), sequestered data matching the expanded surface is automatically released back to active visibility.
3. **Cross-peer rescue.** If F's storage budget shrinks below what's needed even for sequestration, F may evict the sequestered partition entirely — but the workspace's other peers retain it (the CRDT log is global). F can re-fetch on demand via federation, paying the network cost only.
4. **Audit-trail preservation.** Every sequestration/release transition emits a `Sunfish.Kernel.Audit.AuditEventType` event (`DataSequestered` / `DataReleased`) with the `(form_factor, capability_change, data_set_summary)` tuple. The audit trail itself is append-only per ADR 0049 — sequestration of *audit data* is forbidden.

**What Invariant DLF does NOT promise:**

- It does not promise the data remains *visible* on F after a feature loss — visibility is the feature; only persistence is the invariant.
- It does not promise F has the *capability* to render the sequestered data — F may lack the UI surface to show it. The workspace as a whole has it via the other peers.
- It does not promise *write-back* to sequestered records — sequestered records are read-only on F until released.

#### A5.5 — Forward-compat policy

When an older deployment receives data from a newer one (newer schema epoch, or newer Mission Envelope features), A5 specifies:

> **Forward-compat rule (canonical).** Older nodes treat unknown fields, capabilities, and feature-tier annotations as *informational* — they store them losslessly via `CanonicalJson` unknown-key tolerance (verified per F12 of A6 council review) but do not act on them. Older nodes do NOT generate the unknown surface (because they can't), but they preserve it on read+write round trips so newer peers see no loss.

**Concrete behaviors:**

1. **Unknown capability tags are preserved.** If a newer peer writes a record with `capability_tag = "future-only-feature"` and the older peer has no plugin that recognizes this tag, the older peer:
   - Stores the tag verbatim in its CRDT replica
   - Surfaces no UI for it (the plugin is absent on the older peer)
   - Re-emits the tag verbatim when it pushes the record back to other peers
2. **Unknown feature-tier annotations propagate.** If a newer peer marks a record as `tier: "beta-only"`, the older peer treats this as an opaque tag — preserves it, doesn't filter on it.
3. **Schema-epoch crossings are NOT auto-forward-compat.** Per A6.2 rule 1, schema-epoch differences hard-reject federation. An older node receiving newer-epoch data does not silently accept it — A6's compatibility relation triggers first. A5's forward-compat policy applies only WITHIN a compatible epoch (A6 already gated).
4. **Reverse direction (newer reads older).** Newer peers receiving older data treat missing fields as *absent* (the field genuinely wasn't set), not as *defaulted*. If a feature requires the field to be present, that record is not surfaced under that feature; it remains accessible under the older feature surface.

#### A5.6 — Rollback semantics

When a user explicitly downgrades a form factor (e.g., reverts to an older app version; re-images device with prior workspace version), A5 specifies:

> **Rollback rule (canonical).** Rollback is a special case of cross-tier hardware-change (per A5.3) where the change is operator-initiated. The same Invariant DLF guarantees apply: data created under post-rollback-unavailable features is sequestered, not deleted. Rollback does NOT require an explicit confirmation step at A5's substrate tier — but rollbacks SHOULD trigger a UX warning at the application tier.

**Substrate-tier mechanics:**

1. The form-factor adapter detects the rollback (e.g., older binary loads with older `FormFactorProfile.capabilities`).
2. `HardwareTierChangeEvent` fires with `triggeringEvent = AdapterDowngrade` per A5.3.
3. Re-evaluation runs per A5.2 rule 4. Data in the no-longer-available surface is sequestered.
4. `Sunfish.Kernel.Audit.AuditEventType.AdapterRollbackDetected` is emitted with the `(previous_adapter_version, new_adapter_version, sequestered_data_set_summary)` tuple. (Audit dedup per A6.5.1: at most one event per `(node_id, adapter_id, version_pair)` tuple per **6-hour window** to absorb rapid rollback-and-re-roll-forward sequences.)

**What rollback does NOT cause:**

- Federation rejection. The rollback is the operator's choice; A5's compatibility relation (still gated by A6) determines if federation continues.
- Loss of audit trail. Audit data is append-only per ADR 0049; rollback preserves the historical audit log.
- Loss of cryptographic state. Per A5.7 below, encrypted-state keys are re-derivable from the workspace's recovery substrate; a rollback that loses local key material is recoverable via the standard recovery flow (ADR 0046).

#### A5.7 — Encrypted-state key transfer formalization

A5 formalizes the cross-form-factor key transfer that paper §13.4 (QR-onboarding) hints at, building on ADR 0046 (encrypted-field substrate):

> **Cross-form-factor key transfer (canonical).** When form factor F is added to a workspace via QR-onboarding (paper §13.4), the onboarding flow transfers per-tenant encryption keys from the inviting peer to F. A5 specifies the protocol shape; the cryptographic primitives are reused from ADR 0046 + ADR 0032 (Ed25519 root keypair).

**Protocol shape:**

1. **QR scan establishes the trust anchor.** F scans a QR code displayed by the inviting peer; the QR code carries the inviting peer's Ed25519 public key + a one-time secret derived per ADR 0032. F derives its session key from the QR-code secret (zero-knowledge of the long-term keys).
2. **Form-factor registration handshake.** F sends its newly-generated Ed25519 public key + its `FormFactorProfile` over the QR-derived session. The inviting peer signs F's `FormFactorProfile` (binding the form factor to the workspace's identity surface) and returns the signed profile + the workspace's per-tenant encryption keys (per ADR 0046's `IFieldDecryptor` substrate; A5 does NOT define new key types).
3. **Per-tenant key set is filtered by form-factor capabilities.** If F's `FormFactorProfile` does not include `BiometricAuth` capability, the per-tenant keys for biometric-protected fields are NOT transferred to F. F can still see those records but cannot decrypt them; sequestration applies.
4. **Audit emission.** The inviting peer emits `Sunfish.Kernel.Audit.AuditEventType.FormFactorProvisioned` with `(form_factor, transferred_key_set_summary)`. F emits `Sunfish.Kernel.Audit.AuditEventType.FormFactorEnrollmentCompleted` with `(workspace_id, accepted_capabilities)`.

**Key-rotation mechanics (per ADR 0046):**

When per-tenant keys rotate (per ADR 0046's rotation primitive — currently deferred per A4.3), all form factors with the prior keyVersion receive the new keyVersion via the standard rotation broadcast (ADR 0046 territory). A5 does NOT special-case form-factor-specific rotation; the rotation broadcast is form-factor-agnostic.

**What A5.7 does NOT cover:**

- The actual cryptographic primitives — those are ADR 0032 (Ed25519) + ADR 0046 (per-tenant encryption keys + EncryptedField) territory.
- The QR code's serialization format — that's a ~ADR-0032-A1 follow-up (paper §13.4 names QR-onboarding but no ADR has formalized the QR payload schema yet).
- Form-factor revocation (e.g., user loses the tablet; workspace admin revokes the tablet's keys) — that's a separate mechanism in ADR 0046's substrate territory; A5 cites the gap but defers to ADR 0046 for the mechanism.

#### A5.8 — Acceptance criteria

For a `Sunfish.Foundation.Migration.IFormFactorMigrationService` implementation to be considered A5-conformant, it MUST:

- [ ] Encode/decode `FormFactorProfile` via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` round-trip
- [ ] Emit `HardwareTierChangeEvent` on each detected hardware-tier change per A5.3
- [ ] Recompute `derivedSurface` on each `HardwareTierChangeEvent` and apply the sequestration/release transitions per A5.2 + A5.4
- [ ] Emit the 4 new `Sunfish.Kernel.Audit.AuditEventType` constants per A5.3 / A5.4 / A5.6 / A5.7:
  - `HardwareTierChanged` (A5.3)
  - `DataSequestered` + `DataReleased` (A5.4)
  - `AdapterRollbackDetected` (A5.6)
  - `FormFactorProvisioned` + `FormFactorEnrollmentCompleted` (A5.7)
- [ ] Honor Invariant DLF: under any sequence of `HardwareTierChangeEvent`s, every record present at any point is either visible OR sequestered (with a flag); no record is silently deleted
- [ ] Forward-compat: round-trip canonical JSON preserves unknown fields/capability tags/feature-tier annotations losslessly (verified per F12 of A6 council review)
- [ ] Test coverage:
  - 8 cross-form-factor migration tests (one per `FormFactorProfile.formFactor` value)
  - 4 cross-hardware-tier downgrade tests (storage / network / power / sensor-permission)
  - Sequestration round-trip test (capability removed + restored; data re-emerges)
  - Rollback test (adapter version downgrade triggers `AdapterRollbackDetected` exactly once per 6-hour window)
  - Forward-compat test (older deployment receives newer record with unknown capability tag; round-trips cleanly)
  - QR-onboarding key-transfer integration test (per A5.7; depends on ADR 0032 + ADR 0046 substrate)
- [ ] Audit-emission rate-limiting (per A6.5.1 pattern):
  - `AdapterRollbackDetected`: at most once per `(node_id, adapter_id, version_pair)` tuple per **6-hour window**
  - Other A5 events: standard audit-substrate behavior (no special dedup beyond existing patterns)

#### A5.9 — Cited-symbol verification (Decision Discipline Rule 6 + structural-citation correctness per A7's lesson)

Per the standing rung-6 task + the A7 council lesson on structural citation correctness:

**Existing on `origin/main`** (verified 2026-04-30 via `git ls-tree` / `git grep` / structural read):

- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` (pinned per A4 / A6.1; positive-existence verified)
- `Sunfish.Kernel.Audit.AuditEventType` (verified existing per F11 of A6 council)
- `Sunfish.Kernel.Audit.AuditPayload` + `IAuditTrail` (verified per A2/A4)
- `Sunfish.Foundation.Crypto.IOperationSigner` (verified per A2/A4)
- ADR 0028-A1 — verified Accepted; specifies iOS Phase 2.1 envelope (the predecessor A5 generalizes)
- ADR 0028-A6 — verified Accepted post-A7 (the immediate predecessor A5 builds on)
- ADR 0032 — verified Accepted; provides Ed25519 root keypair (used in A5.7)
- ADR 0046 — verified Accepted; provides `EncryptedField` + `IFieldDecryptor` (used in A5.4 / A5.7)
- ADR 0049 — verified Accepted; provides audit-trail substrate (consumed by A5)

**Structural-citation spot-check (per A7 lesson):**

A5 cites three external concepts that need structural verification:

- **Paper §13.4 (QR-onboarding multi-device flow):** verified existing; covers QR-onboarding at the conceptual level. Paper does NOT formalize the QR payload schema — A5.7 explicitly notes this gap and does NOT cite a formalization that doesn't exist.
- **Paper §15.2 (schema-version mixed-cluster testing):** verified existing; covers the testing scenarios (line range matches per W#33 §5.7 spot-check).
- **ADR 0046 rotation primitive:** verified existing-but-deferred — ADR 0046 has the substrate but A4.3 explicitly defers the rotation primitive to a future amendment. A5.7 cites this state honestly ("currently deferred per A4.3").

**Introduced by A5** (not on `origin/main`; ship in implementation hand-off):

- `Sunfish.Foundation.Migration.FormFactorProfile` (A5.1 type signature)
- `Sunfish.Foundation.Migration.IFormFactorMigrationService` (A5.8 acceptance contract)
- `Sunfish.Foundation.Migration.HardwareTierChangeEvent` (A5.3)
- `AuditEventType.HardwareTierChanged` (new constant)
- `AuditEventType.DataSequestered` (new constant)
- `AuditEventType.DataReleased` (new constant)
- `AuditEventType.AdapterRollbackDetected` (new constant)
- `AuditEventType.FormFactorProvisioned` (new constant)
- `AuditEventType.FormFactorEnrollmentCompleted` (new constant)
- `Sunfish.Foundation.Migration.MigrationAuditPayloads` (factory for the new audit constants; mirrors `VersionVectorAuditPayloads` per A6.6)

#### A5.10 — Open questions (deferred)

- **OQ-A5.1:** is `FormFactorProfile.formFactor = Vehicle` a real Phase 2+ concept or premature? Reserved per the table in A5.1; the corresponding row is informational. If no Vehicle adapter ships within ~12 months, defer-but-don't-remove.
- **OQ-A5.2:** does the `derivedSurface` recomputation cadence need a configurable upper bound (e.g., "no more than once per minute")? Recommend deferring; sync-time-only re-evaluation per A5.2 rule 3 already bounds the rate.
- **OQ-A5.3:** is `FormFactorFilteredOut` vs `StorageBudgetExceeded` the right granularity for sequestration flags? Or should there be a single `Sequestered` flag with a `reason: enum`? Recommend the latter as a v0.1 amendment if test coverage finds the two-flag model awkward; ship the two-flag model now for clarity.
- **OQ-A5.4:** when both peers in a federation have rolled-back form-factor adapters but the workspace's other peers haven't, does A5 specify a "minority rollback" vs "majority rollback" behavior? Currently A5 treats each peer independently — each peer's rollback fires independently; the workspace as a whole is unchanged. If a "majority rollback" semantic is needed (e.g., the workspace itself rolls back), that's a separate mechanism in a future ADR.
- **OQ-A5.5:** the QR-onboarding payload schema (per A5.7's gap acknowledgment) — should A5 declare a forcing function for ADR 0032-A1 to formalize the schema, or is implicit forcing-via-W#23-build-out enough? Recommend the latter; W#23 iOS Field-Capture App's Phase 5 (pairing flow) will need the formal schema and naturally produces the forcing function.

#### A5.11 — Companion amendment dependencies

A5 has hard dependencies on:

- **ADR 0028-A6** (post-A7) for the compatibility relation that gates federation in the first place. A5 is downstream of A6 — A5 only fires once A6 has determined the peers are compatible.
- **ADR 0028-A1** (post-A2/A3/A4) for the iOS Phase 2.1 envelope that A5 generalizes (paper §13.4 cross-form-factor case). The companion A1.x intake (PR #397) augments A1's envelope with capture-context tagging per A6.11; A5 inherits that augmentation transparently.
- **ADR 0032** for Ed25519 keypair semantics (used in A5.7 QR-onboarding handshake).
- **ADR 0046** for encrypted-field substrate (used in A5.7 cross-form-factor key transfer).

A5 has soft dependencies on:

- **~ADR 0063 Mission Space Negotiation Protocol** (queued per W#33 §7.2). The negotiation-protocol mechanism is what surfaces capability-availability changes that A5's `derivedSurface` filter consumes. Without ~ADR 0063, A5's `workspace.declaredCapabilities` is computed naively (union of all installed plugins). With ~ADR 0063, the computation incorporates negotiated capability gates. A5 ships the naive form; ~ADR 0063 substitutes the negotiated form when it lands.

#### A5.12 — Open questions affecting future amendments

A5 names two areas that may produce follow-up amendments:

- **A5-followup-1:** Form-factor revocation mechanics. When a workspace admin revokes a form factor's access (e.g., user loses tablet; admin marks tablet as compromised), what does A5 do beyond the standard ADR 0046 key revocation? Currently A5 does nothing special — ADR 0046's revocation flow handles the cryptographic side; the form factor's `FormFactorProfile` is moot once it can't decrypt. But explicit revocation audit + UI surface may need an A5.x amendment.
- **A5-followup-2:** Multi-form-factor concurrent-edit semantics under different `derivedSurface` filters. If laptop creates a record under capability C, and tablet (which lacks C in its derived surface) attempts a concurrent edit on the same record's metadata — does the merge see the record? Currently per Rule 2, tablet's outgoing writes are unfiltered; tablet COULD edit the metadata if it has the metadata-only capability. The CRDT merge logic resolves the result. A5 doesn't explicitly spec this case; likely needs an A5.x amendment if test coverage finds the case underspecified.

#### A5.13 — Cohort discipline

Per `feedback_decision_discipline.md` cohort batting average (now 11+ substrate amendments needing post-acceptance fixes; council false-claim rate 2-of-10):

- **Pre-merge council canonical** for A5. Auto-merge on this PR is intentionally DISABLED until a Stage 1.5 council subagent reviews. Council should specifically pressure-test:
  - The Invariant DLF (data-loss-vs-feature-loss) under all 8 form-factor migration combinations — is "sequestration" a complete answer or are there edge cases where data is genuinely unrecoverable?
  - The forward-compat policy under schema-epoch crossings — does A5.5's "gated by A6" claim actually hold across all 4 A6 compatibility-failure modes?
  - The QR-onboarding key-transfer (A5.7) — is the per-tenant-key-filtering-by-capability mechanism cryptographically sound, or does it leak information about which capabilities the form factor lacks?
  - The migration table (A5.1) — are the 8 form-factor rows + cross-pairs sufficient for v0, or are there obvious gaps the council would surface (e.g., specific cross-OS migration cases)?
  - The 6-hour `AdapterRollbackDetected` audit-dedup window — same scaling-protection pattern as A6.5.1, but is 6h the right value for this event class?
- **Cited-symbol verification** per A5.9 (every introduced symbol explicitly marked; every existing reference verified on `origin/main`; structural-citation spot-check on all field-on-type claims per the A7 lesson)
- **Standing rung-6 spot-check** within 24h of A5 merging (per ADR 0028-A4.3 + A7.12 commitment)

---

### A6 (PROPOSED) — Version-vector compatibility contract for mixed-version Sunfish clusters

**Driver:** W#33 Mission Space Matrix discovery (`icm/01_discovery/output/2026-04-30_mission-space-matrix.md` §5.8) confirmed via A4 spot-check that Sunfish has **no version-vector compatibility contract** for mixed-version clusters. Paper §6.1 (line 180) mentions vector clocks operationally — for *gossip mechanics* (anti-entropy reconciliation between peers that have already agreed they are compatible peers) — but is silent on the upstream question of *whether* two nodes carrying different (kernel × plugin × adapter × schema-epoch × channel × instance-class) tuples can federate at all. Paper §15.2's *"'Couch device' (offline for 3+ major versions) → capability negotiation rejects with clear error"* references the gap without specifying the rejection logic.

A1's iOS Phase 2.1 carve-out (append-only event queue → Anchor merge boundary) is the immediate consumer: when the iPad reconnects after a long offline window, the merge boundary needs a defined contract for "is this iPad's event-envelope shape still compatible with the current Anchor's CRDT store?" — and today that contract doesn't exist.

**Pipeline:** `sunfish-api-change` (introduces compatibility-contract API; affects federation-time handshake; downstream consumers across foundation + accelerators).

**Companion amendment:** A5 (cross-form-factor migration) is a sibling intake authored separately; it builds on A6's compatibility relation. A5 generalizes A1's iOS carve-out to all form factors. **A6 ships first** because A5 depends on A6's comparison semantics.

#### A6.1 — Version-vector type signature

The version-vector tuple is:

```text
VersionVector ::= {
    kernel:        SemVer,                  # e.g., "1.3.0"
    plugins:       Map<PluginId, SemVer>,   # per-plugin version; ordered by PluginId for canonical encoding
    adapters:      Map<AdapterId, SemVer>,  # blazor / react / maui-blazor; per-adapter version
    schemaEpoch:   uint32,                  # monotonic per-schema-cutover; per ADR 0001 schema-registry-governance
    channel:       enum { Stable, Beta, Nightly },
    instanceClass: enum { SelfHost, ManagedBridge, Embedded }
}
```

**JSON canonical shape** (per `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` — pinned per A4's retraction of `JsonCanonical`):

```json
{
  "kernel": "1.3.0",
  "plugins": {"sunfish.blocks.maintenance": "1.2.0", "sunfish.blocks.public-listings": "1.0.0"},
  "adapters": {"blazor": "1.3.0", "react": "1.1.0"},
  "schema_epoch": 7,
  "channel": "stable",
  "instance_class": "self_host"
}
```

**Normalization rules:**

- `plugins` and `adapters` keys are sorted lexicographically before serialization (canonical order matters for ContentHash binding + signature generation; per `CanonicalJson.Serialize` rules)
- SemVer comparison follows [SemVer 2.0](https://semver.org) precedence (major > minor > patch)
- `schemaEpoch` is monotonic; never decreases during the install's lifetime (ADR 0001)
- `channel` is partial-order: `stable < beta < nightly` (more permissive channels can read stable data; reverse is not implied)
- `instanceClass` is incomparable across classes — no implicit order

#### A6.2 — Compatibility relation (the contract)

Given two version vectors `V1` (local) and `V2` (remote), `V1` MAY federate with `V2` iff **all** of the following hold:

1. **`schemaEpoch` equality.** `V1.schemaEpoch == V2.schemaEpoch`. Schema-epoch crossings are coordinated cutovers (per ADR 0001); peers MUST be on the same epoch to federate. (One-sided: a node MAY *receive* events from a peer one epoch behind to facilitate that peer's catch-up, but not write to it. Implementation detail in A6.4.)
2. **Kernel SemVer compatibility window.** `V1.kernel.major == V2.kernel.major` AND `|V1.kernel.minor - V2.kernel.minor| ≤ 2`. Patch versions never gate compatibility. The 2-minor-version window matches the gRPC API design guide deprecation default; tightenable per-deployment via configuration (out of A6 scope; future tunable).
3. **Plugin set intersection covers required plugins.** For each plugin `p` declared as `required: true` in either node's manifest (per ADR 0007 bundle-manifest-schema), `p ∈ V1.plugins ∩ V2.plugins` AND their SemVer comparison passes the same major + 2-minor rule. Optional plugins MAY be missing from one side without blocking federation.
4. **Adapter set is informational only.** Adapters are UI-tier; federation is data-tier. `adapters` field is included in the version vector for diagnostic purposes (e.g., "node A is Blazor 1.3, node B is MAUI Blazor 1.2") but does NOT gate federation. Two nodes with disjoint adapter sets can federate cleanly.
5. **Channel ordering.** `V1.channel ≤ V2.channel` OR `V1.channel == V2.channel`. Stable-channel nodes can read from beta/nightly nodes (in case of beta-channel canary deployments testing forward-compat); the reverse is forbidden by default to prevent stable production data from being polluted by unstable beta state. Configurable at the operator level (production deployments typically pin `channel == stable` strictly).
6. **Instance-class compatibility.** SelfHost ↔ ManagedBridge ↔ Embedded are all mutually compatible at the data-tier layer. Cross-instance-class federation IS supported. (Operationally, ManagedBridge instances may impose additional capability restrictions per ADR 0031 hybrid-multi-tenant-saas; those are downstream of A6.)

**Comparison semantics summary table:**

| Field | Compatibility rule | Failure → |
|---|---|---|
| `schemaEpoch` | Strict equality | Reject; surface as `SchemaEpochMismatch` (one-sided receive-only allowed; see A6.4) |
| `kernel` SemVer | Same major + ≤2 minor | Reject; surface as `KernelVersionIncompatible` |
| `plugins` (required) | Intersection covers; SemVer window | Reject; surface as `RequiredPluginIncompatible` with name + V1/V2 versions |
| `adapters` | Informational | Never blocks |
| `channel` | `V1 ≤ V2` (stable→beta→nightly partial order) | Reject by default; surface as `ChannelDowngradeForbidden` |
| `instanceClass` | All classes compatible | Never blocks at A6 layer |

#### A6.3 — Federation-time handshake

The version-vector exchange happens during the **handshake phase** of peer connection establishment (before any gossip / anti-entropy traffic per paper §6.1). It uses the same Noise-pattern session that secures the connection (per ADR 0027 kernel-runtime-split + ADR 0032 multi-team workspace switching).

**Handshake sequence:**

1. Peer A initiates connection to peer B over Tier 1 (mDNS / LAN), Tier 2 (mesh VPN per ADR 0061), or Tier 3 (managed relay).
2. Noise session establishment (per ADR 0027) completes; both peers have authenticated each other via Ed25519 root keypair (per ADR 0032).
3. **Version-vector exchange** (this amendment):
   - Both peers send their canonical-JSON-encoded `VersionVector` in a single `VersionVectorExchange` message inside the established Noise channel
   - Each peer evaluates the compatibility relation (A6.2) against the received `VersionVector`
   - If compatible: proceed to gossip (per paper §6.1); BOTH peers MUST agree (the relation is symmetric for everything except `channel` ordering and one-sided `schemaEpoch` receive-only)
   - If incompatible: see A6.4
4. Gossip / anti-entropy reconciliation proceeds per paper §6.1 only after handshake clears.

**Wire format:**

```text
Sunfish.Foundation.Versioning.IVersionVectorExchange
    + IVersionVectorMessage SendAsync(VersionVector local, CancellationToken ct)
    + IVersionVectorMessage ReceiveAsync(CancellationToken ct)
    + IVersionVectorIncompatibility EvaluateCompatibility(VersionVector local, VersionVector remote)
```

**Cited symbol verification (Decision Discipline Rule 6):** The above types are introduced by this amendment; none exist on `origin/main` today. Implementation hand-off MUST scaffold them in `Sunfish.Foundation.Versioning` namespace.

#### A6.4 — Behavior on incompatibility

When the compatibility relation (A6.2) yields a rejection, the receiving peer SHALL:

1. **Emit an audit event** (per ADR 0049): `AuditEventType.VersionVectorIncompatibilityRejected` with payload-body fields:
   - `local_kernel`: SemVer string
   - `remote_kernel`: SemVer string
   - `local_schema_epoch`: uint32
   - `remote_schema_epoch`: uint32
   - `failed_rule`: enum `SchemaEpochMismatch | KernelVersionIncompatible | RequiredPluginIncompatible | ChannelDowngradeForbidden`
   - `failed_rule_detail`: string (e.g., for `RequiredPluginIncompatible`: `"sunfish.blocks.maintenance: local=1.2.0, remote=1.0.0, window=major+2-minor"`)
   - `remote_node_id`: string (from Noise handshake)
2. **Close the federation session cleanly** (no half-open state) — both peers SHOULD log the incompatibility but neither should retry-loop.
3. **Surface a user-visible error** at the next operator-facing UX moment (Anchor desktop status bar; Bridge admin dashboard; iOS app banner). The error message format:

   > **Cannot sync with `<remote_node_short_id>`.** This node is running `<remote_kernel>` with schema epoch `<remote_schema_epoch>`; we're on `<local_kernel>` with schema epoch `<local_schema_epoch>`. *Reason: `<failed_rule_detail>`.* To resolve: `<recovery_action>`.

   Recovery actions per failure rule:
   - `SchemaEpochMismatch` → "Update one or both nodes to align schema epochs (run `sunfish migrate`)"
   - `KernelVersionIncompatible` → "Update the older kernel to at least `<local_kernel.major>.<local_kernel.minor - 2>.0`"
   - `RequiredPluginIncompatible` → "Install / update plugin `<plugin_name>` on both nodes to at least `<min_compatible_version>`"
   - `ChannelDowngradeForbidden` → "Either pin both nodes to the same channel, OR set `--allow-channel-downgrade` (at your own risk)"

#### A6.5 — One-sided receive-only mode (long-offline reconnect)

Paper §15.2's "couch device offline for 3+ major versions" scenario needs special handling. When peer A (current) detects peer B is N kernel-minor-versions behind (where N > 2; outside the standard window), A SHALL still accept event uploads from B (one-sided receive-only) iff:

1. `V1.schemaEpoch == V2.schemaEpoch` (epoch hasn't crossed during B's offline window — if it has, hard-reject; B must run `sunfish migrate` first)
2. B's events use a v0-compatible envelope (per ADR 0028-A1 iOS event envelope contract — append-only events with `device_local_seq` are explicitly forward-compatible)

In this mode:

- A acks B's events as normal (no functional difference for B's send path)
- A does NOT attempt to write back to B (B's outdated kernel may not understand A's event shape)
- A emits `AuditEventType.LegacyDeviceReconnected` with `(remote_node_id, remote_kernel, kernel_minor_lag)` — operator sees B is overdue for an update
- B's user-facing UX shows "Synced — your device is N versions behind; please update soon to enable two-way sync"

When B finally updates and re-handshakes with a current `V2`, the bidirectional path resumes.

#### A6.6 — Acceptance criteria for A6 implementation hand-off

- [ ] `Sunfish.Foundation.Versioning.VersionVector` record per A6.1
- [ ] `IVersionVectorExchange` interface + reference impl (Noise-channel-backed) per A6.3
- [ ] `IVersionVectorIncompatibility` result type with `FailedRule` enum + `Detail` string per A6.2
- [ ] `EvaluateCompatibility` static helper or service implementing A6.2's rule table
- [ ] Two new `AuditEventType` constants in `Sunfish.Kernel.Audit`: `VersionVectorIncompatibilityRejected`, `LegacyDeviceReconnected`
- [ ] `VersionVectorAuditPayloads` factory class with 2 methods (matches `TaxonomyAuditPayloadFactory` / `FieldEncryptionAuditPayloadFactory` patterns)
- [ ] DI registration extended on `AddSunfishKernel()` (or appropriate root extension)
- [ ] Tests (target ~25-35):
  - Round-trip encode/decode of `VersionVector` JSON via `CanonicalJson.Serialize`
  - 6 compatibility-rule tests (one per A6.2 rule)
  - 4 rejection-error-format tests (one per `FailedRule` enum value)
  - One-sided receive-only test (legacy device reconnect)
  - Schema-epoch mismatch hard-rejects even with current kernel
  - Channel downgrade rejection by default; allow-flag opt-in
  - Required-plugin missing hard-rejects; optional-plugin missing soft-passes
  - Adapter set difference does not gate
  - Cross-instance-class compatibility (SelfHost ↔ ManagedBridge ↔ Embedded)
  - Audit emission shape per A6.4
- [ ] User-visible error UX surfaces in Anchor + Bridge + iOS (per A6.4 message format)

#### A6.7 — Cited-symbol verification (Decision Discipline Rule 6)

Per the cohort lesson + standing rung-6 task, every cited `Sunfish.*` symbol classified:

**Existing on `origin/main`** (verified 2026-04-30 via `git ls-tree` / `git grep`):
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` (pinned per A4)
- `Sunfish.Kernel.Audit.AuditEventType` + `AuditPayload` + `IAuditTrail` (verified existing)
- `Sunfish.Foundation.Crypto.IOperationSigner` (existing per A2 / A4)
- ADR 0001 schema-registry-governance — verified Accepted on `origin/main`
- ADR 0027 kernel-runtime-split — verified Accepted
- ADR 0031 bridge-hybrid-multi-tenant-saas — verified Accepted
- ADR 0032 multi-team-anchor-workspace-switching — verified Accepted
- ADR 0007 bundle-manifest-schema — verified Accepted
- ADR 0049 audit substrate — verified Accepted
- ADR 0061 three-tier-peer-transport — verified Accepted (per A3 retraction)

**Introduced by A6** (not on `origin/main`; ship in implementation hand-off):
- `Sunfish.Foundation.Versioning.VersionVector`
- `Sunfish.Foundation.Versioning.IVersionVectorExchange`
- `Sunfish.Foundation.Versioning.IVersionVectorIncompatibility`
- `Sunfish.Foundation.Versioning.FailedRule` (enum)
- `Sunfish.Foundation.Versioning.VersionVectorAuditPayloads`
- `AuditEventType.VersionVectorIncompatibilityRejected` (new constant)
- `AuditEventType.LegacyDeviceReconnected` (new constant)

#### A6.8 — Open questions (deferred)

- **OQ-A6.1:** does the kernel SemVer compatibility window (major + ≤2 minor) need to be tunable per-deployment? Recommend deferring to a Phase 2.2+ deployment-config story; A6 ships the canonical default.
- **OQ-A6.2:** when a plugin is `required: true` on one side but absent on the other, should the rejection cite "plugin not installed" vs "plugin version mismatch"? Both feel surfacing-worthy; A6.2's reference impl covers both via the `failed_rule_detail` string but a follow-up amendment may split into two `FailedRule` values.
- **OQ-A6.3:** is `instanceClass = Embedded` a real Phase 2.1 concept or is it premature? A6.1 names it for forward-compat; the compatibility relation treats it as informational; if no real Embedded instance ships in the next ~6 months, defer-but-don't-remove.

#### A6.9 — Companion amendment dependencies (sibling: A5)

A5 (cross-form-factor migration; queued separately at `icm/00_intake/output/2026-04-30_cross-form-factor-migration-intake.md`) builds on A6's compatibility relation. Specifically:

- A6 specifies the static compatibility check at federation time
- A5 generalizes ADR 0028-A1's iOS Phase 2.1 carve-out (append-only event queue → Anchor merge boundary) into a form-factor-agnostic migration semantic
- A5 cites A6.5 (one-sided receive-only) as the canonical "long-offline reconnect" pattern that A5's migration semantics inherit

XO authors A5 next per discovery §7.2 sequencing.

#### A6.10 — Cohort discipline

Per `feedback_decision_discipline.md` + cohort batting average (now 7+ substrate amendments needing post-acceptance fixes):

- **Pre-merge council canonical** for A6. Auto-merge on this PR is intentionally DISABLED until a Stage 1.5 council subagent reviews. Council should specifically pressure-test:
  - Comparison-semantics edge cases (one-sided vs two-sided incompatibility)
  - The 2-minor-version SemVer window (is gRPC's deprecation-window analog the right precedent here?)
  - The receive-only mode's audit-event shape under high-throughput legacy-device reconnect storms
- **Cited-symbol verification** per A6.7 (every introduced symbol explicitly marked; every existing reference verified on `origin/main`)
- **Standing rung-6 spot-check** within 24h of A6 merging (per ADR 0028-A4.3 commitment)

---

### A7 (REQUIRED, mechanical) — A6 council-review fixes

**Driver:** Stage 1.5 adversarial council review of A6 at `icm/07_review/output/adr-audits/0028-A6-council-review-2026-04-30.md` (PR #396, merged 2026-04-30) returned verdict **B (Solid) with 6 required + 4 encouraged amendments**. Per `feedback_decision_discipline` Rule 3, mechanical council fixes auto-accept; A7 absorbs all 10 recommendations into A6's surface before W#33 Stage 06 build emits its first `VersionVectorExchange` message. The architectural shape of A6 (tuple version vector + handshake-time exchange + small enumerated rules + one-sided receive-only) survives unchanged; A7 fixes the substrate-tier gaps the council surfaced.

**Council severity profile (per review §6):** 1 Critical (F1), 5 Major (F2 + F3 + F4 + F5 + F6), 4 Minor (F7 + F8 + F9 + F10), 4 verification-passes (F11 + F12 + F13 + F14). All 14 findings are addressed below or explicitly verified-as-passing.

#### A7.1 — Symmetric-evaluation handshake (council A1 / F1 Critical)

A6.3 step 3 ("BOTH peers MUST agree (the relation is symmetric for everything except channel ordering and one-sided schemaEpoch receive-only)") is replaced with an explicit two-phase verdict commit:

> **3a.** Each peer evaluates compatibility against the received version-vector (per A6.2) AND records its own verdict (`compatible` | `incompatible`).
>
> **3b.** Each peer sends a `VersionVectorVerdict` follow-up message carrying its verdict + (if `incompatible`) the `failed_rule` + `failed_rule_detail` per A6.4. This message is sent inside the same authenticated channel before any teardown.
>
> **3c.** Federation proceeds iff BOTH verdicts are `compatible`. If either side reports `incompatible`, both peers MUST close the federation session cleanly per A6.4 (audit emission + UX surface). No half-open state.
>
> **3d.** Asymmetric rules (channel ordering rule 5; one-sided receive-only schemaEpoch in A6.5; required-plugin evaluation in A6.2 rule 3 as augmented per A7.3) are evaluated by each peer independently against the received vector — but BOTH peers must independently agree that the asymmetry resolves to `compatible` for federation to proceed. This eliminates the asymmetric-evaluation pathology where peer A says compatible and peer B says incompatible mid-handshake (the canonical case being a stable–nightly channel pairing during beta canary rollout).

The wire-format addition is one new message type:

```text
VersionVectorVerdict ::= {
    verdict:             enum { Compatible, Incompatible },
    failed_rule:         optional FailedRule,            # set iff verdict == Incompatible
    failed_rule_detail:  optional string                 # set iff verdict == Incompatible
}
```

`VersionVectorVerdict` is canonical-JSON-encoded per `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` (same encoding contract as `VersionVector`).

#### A7.2 — Honest framing of the SemVer compatibility window (council A2 / F2 Major)

A6.2 rule 2 ("matches the gRPC API design guide deprecation default") is replaced with an honest v0-simplification framing:

> **Rule 2 — Kernel SemVer compatibility window (v0 model).** `V1.kernel.major == V2.kernel.major` AND `|V1.kernel.minor - V2.kernel.minor| ≤ 2`.
>
> **Rationale:** A6 ships an arithmetic-window v0 model for simplicity. The canonical P2P prior art (libp2p protocol-version negotiation; Apple CloudKit zone-capability sets; Yjs / Automerge format-version handshake; IPFS bitswap version intersection) uses **explicit supported-version sets with intersection-wins negotiation** rather than arithmetic windows. The arithmetic-window model is a v0 simplification that will be revisited in a Phase 3+ amendment when (a) kernel format actually changes (today the format is stable; SemVer minor mostly tracks feature additions, not breaking changes), or (b) a real cross-version interop case demonstrates the arithmetic model is too restrictive (e.g., a 1.5.0 ↔ 1.0.0 pairing where actual format compatibility is preserved but the arithmetic window forbids the federation). Track at OQ-A6.4.
>
> **Configuration:** tunable per-deployment (per OQ-A6.1) via a `MaxKernelMinorLag: uint8` setting; default 2. Operators MAY raise this in homogeneous deployments where format stability is independently verified.

A new open question is appended to A6.8:

> **OQ-A6.4:** When does Sunfish migrate from the arithmetic-window kernel-compat model to the libp2p-style explicit-version-set model? Trigger candidates: (a) first kernel format-breaking change ships (forces explicit-set anyway); (b) field deployment surfaces an arithmetic-window false-rejection (legacy device with a still-valid format being rejected purely on minor-distance); (c) Phase 3+ when CRDT-on-mobile lands and per-message versioning becomes the natural pattern. **Default expectation:** revisit at Phase 3.

#### A7.3 — Plugin-required citation correctness + wire-format augmentation (council A3 / F3 Major)

A6.2 rule 3 cited a `required: true` field per ADR 0007 that does not exist on `ModuleManifest` — ADR 0007 has `requiredModules: string[]` on `BusinessCaseBundleManifest` and `required: bool` only on `ProviderRequirement`. Two coupled fixes:

**A7.3.1** — A6.2 rule 3 is rewritten to cite ADR 0007's actual schema:

> **Rule 3 — Required-plugin intersection.** For each plugin `p` listed in `BusinessCaseBundleManifest.requiredModules` of any bundle installed on either peer (per ADR 0007 bundle-manifest-schema), `p ∈ V1.plugins ∩ V2.plugins` AND its SemVer comparison passes the same major-equal + 2-minor-lag rule as kernel (rule 2). Plugins not listed in any peer's bundle's `requiredModules` (i.e., installed-but-optional plugins) MAY be missing from one side without blocking federation.

**A7.3.2** — A6.1's `plugins` map shape is augmented so the wire format carries the required-flag (eliminates the asymmetric-evaluation foot-gun in F1 specifically for rule 3):

> **A6.1 (revised plugins shape):** `plugins` becomes `Map<PluginId, PluginVersionVectorEntry>` where `PluginVersionVectorEntry = { version: SemVer, required: bool }`. The `required` flag is set per-bundle: for each plugin `p` in any installed bundle's `requiredModules`, the entry's `required = true`; otherwise `required = false`. Both peers receive the same canonical view of which plugins each declares required. A6.2 rule 3's evaluation is then symmetric — both peers consult the union of required-flags from both sides' plugin maps.

The revised JSON canonical shape is given in A7.8 (camelCase pin) below.

#### A7.4 — Audit-emission de-duplication (council A4 / F4 Major)

A new sub-section A6.5.1 specifies de-duplication windows for both A6 audit event types:

> **A6.5.1 — Audit-emission rate limits.**
>
> Both `VersionVectorIncompatibilityRejected` and `LegacyDeviceReconnected` audit events use de-duplication windows to prevent audit-flood under realistic operational scenarios:
>
> - **`VersionVectorIncompatibilityRejected`:** at most one emission per `(remote_node_id, failed_rule, failed_rule_detail)` tuple per **1-hour rolling window**. Subsequent rejections from the same misconfigured peer with the same failure are subsumed.
> - **`LegacyDeviceReconnected`:** at most one emission per `(remote_node_id, kernel_minor_lag)` tuple per **24-hour rolling window**. A reconnecting legacy device that flaps 50 times in an hour generates 1 audit, not 50.
>
> **Implementation:** `VersionVectorAuditPayloads` factory class consults a recent-emissions cache (in-memory; per-node-bounded; eviction-safe) before constructing the payload. Cache resets are not load-bearing — worst case under reset is one duplicate emission per de-dup window. The audit-substrate (per ADR 0049) is independent of dedup: dedup is enforced at the *emission* boundary, not the *substrate* boundary.

A6.6 acceptance criteria gain two test rows:

- [ ] De-duplication tests for both audit types (rapid-reconnect storm; misconfigured-peer-retry storm)
- [ ] Cache-reset behavior test (at most one duplicate in the de-dup window after reset)

#### A7.5 — iOS append-only path: per-event version-vector semantics (council A5 / F5 Major)

A new sub-section A6.11 specifies how A6's compatibility relation is carried across the iOS A1 append-only path (where the per-event envelope explicitly does NOT carry a `VersionVector`):

> **A6.11 — iOS append-only path version-vector semantics.**
>
> Per ADR 0028-A1's iOS event-queue contract (post-A2 fixes), the per-event envelope is `{ device_local_seq, captured_at, device_id, event_type, payload }` — without a per-event `VersionVector`. A6 specifies how cross-version interop is preserved on this path:
>
> 1. **Per-event capture-context tagging.** Each event is augmented at capture time with `captured_under_kernel: SemVer` (the kernel version running on the iPad when the event was captured) and `captured_under_schema_epoch: uint32` (the schema epoch the iPad was on at capture time). These two fields are added to the iOS A1 envelope. Per F12-verified `CanonicalJson.Serialize` unknown-field-tolerance, this is a forward-compat addition: older receivers ignore the fields silently; newer receivers consume them.
> 2. **Merge-boundary evaluation.** When Anchor's merge service consumes an iPad event, it evaluates A6.2 rule 2 against `event.captured_under_kernel` (not against the iPad's *current* version-vector at upload time). Rule 1 is evaluated against `event.captured_under_schema_epoch`.
> 3. **Cross-epoch events are sequestered, not dropped.** When an event's `captured_under_schema_epoch` does not match Anchor's current epoch, the event is sequestered with a `LegacyEpochEvent` audit-record + held for human review (the iPad captured an event under an old epoch; an operator decides whether the migration logic can safely apply it). Hard-dropping is not the default — epochs change rarely, and silent loss of captured field-data is unacceptable.
> 4. **Forward-compat for the envelope itself.** The iOS A1 envelope's evolution path is `envelope_version: uint8` added in a future amendment when the envelope shape itself needs to change (vs adding optional fields, which CanonicalJson tolerance handles for free). Until then, additive-only field evolution is the pattern.

**Coordinated A1 amendment required.** A1's envelope schema must be augmented per A6.11; this lands in a follow-up A1.x amendment authored separately. A6 declares the augmentation needed; A1.x ratifies it on the A1 side.

A6.6 acceptance criteria gain two test rows:

- [ ] iOS A1 envelope test that includes `captured_under_kernel` + `captured_under_schema_epoch` fields with round-trip via CanonicalJson
- [ ] Merge-boundary test for cross-epoch event sequestration (event with stale `captured_under_schema_epoch` lands in `LegacyEpochEvent` audit record, not silently dropped)

#### A7.6 — Strip `instanceClass = Embedded` (council A6 / F6 Major-on-reflection)

A6.1's `instanceClass` enum is reduced from `{ SelfHost, ManagedBridge, Embedded }` to `{ SelfHost, ManagedBridge }`:

> **A6.1 (revised instanceClass):** `instanceClass: enum { SelfHost, ManagedBridge }`. The previously-named `Embedded` value is deferred until a real Embedded consumer ships. Per F12-verified `CanonicalJson.Serialize` unknown-field-tolerance, adding the value later is forward-compat (an additive enum bump). YAGNI applied per UPF anti-pattern 13 (premature precision).

OQ-A6.3 is dropped (the question is answered: defer per YAGNI).

**Verification gate at Stage 06:** A7.6 is *required pending verification* of CanonicalJson enum-bump tolerance. The hand-off MUST include this test:

- Encode `VersionVector` with a hypothetical-future enum value via `CanonicalJson.Serialize`; deserialize on a `JsonStringEnumConverter`-default consumer; observe behavior.

If the test reveals enum-value forward-compat is *not* tolerant (older deserializers reject unknown enum values), A7.6 is reversed in a follow-up amendment that restores the reserved `Embedded` slot — the reserve-fields argument wins over the YAGNI argument in that case. Default expectation: System.Text.Json's `JsonStringEnumConverter` rejects unknown values by default — so the verification result will likely require A7.6 to be reversed OR the implementation to opt into `JsonNumberEnumConverter` / a custom converter with `AllowIntegerValues = true` and an unknown-value fallback. The verification result decides the path.

#### A7.7 — Transport-pattern-agnostic handshake wording (council A7 / F7 Minor)

A6.3 step 2 is reworded to drop the load-bearing claim that ADR 0027 specifies a Noise pattern (it does not; ADR 0027 is the kernel-runtime split, not the transport pattern selection):

> **2. The transport channel is established and authenticated** (Noise pattern per a future transport-tier ADR, or any equivalent authenticated channel; A6's contract is transport-pattern-agnostic at the substrate tier — it requires only mutual authentication of Ed25519 root keypairs per ADR 0032). ADR 0027 defines the kernel-runtime split that makes the kernel transport-pluggable; the actual Noise pattern selection is downstream of A6's compatibility contract and lands in a future transport-tier ADR.

#### A7.8 — Pin canonical-JSON casing to camelCase (council A8 / F8 Minor)

A6.1's JSON canonical example is rewritten with camelCase keys matching the rest of Sunfish (e.g., `tenantId`, `actorId` per `LeaseAuditPayloadFactory`), and incorporating A7.3.2's plugin-shape change:

```json
{
  "kernel": "1.3.0",
  "plugins": {"sunfish.blocks.maintenance": {"version": "1.2.0", "required": true}, "sunfish.blocks.public-listings": {"version": "1.0.0", "required": false}},
  "adapters": {"blazor": "1.3.0", "react": "1.1.0"},
  "schemaEpoch": 7,
  "channel": "stable",
  "instanceClass": "selfHost"
}
```

Multi-word keys are camelCase (`schemaEpoch`, `instanceClass`); enum string values are camelCase (`selfHost`, `managedBridge`); single-word keys remain as-is. CanonicalJson.Serialize sorts keys alphabetically regardless of casing — the casing is a consumer-side convention.

#### A7.9 — Reword A6.2 rule 5's channel direction explanation (council A9 / F10 Encouraged)

A6.2 rule 5 is reworded with cleaner direction-of-arrow framing:

> **Rule 5 — Channel ordering.** A node on a more-permissive channel (`nightly > beta > stable`) MAY federate with a node on a less-permissive channel (consumer-direction; e.g., a nightly-canary node receiving stable production state to validate against). A node on a less-permissive channel MUST NOT accept federation from a more-permissive node by default — this would pollute the less-permissive node's state with state captured under a less-tested code path. Operators MAY override per-deployment via `--allow-channel-downgrade` (e.g., for staged rollout testing where stable receives beta-channel events deliberately). **Configurable at the operator level**: production deployments typically pin `channel == stable` strictly, blocking even the default-allowed direction.

(The rule's *behavior* is unchanged from A6.2; the *prose* is clearer.)

#### A7.10 — What A6 doesn't cover (council A10 / Encouraged scoping)

A new sub-section A6.12 explicitly names what is OUT of A6's scope, to prevent future amendments from accidentally over-claiming A6's surface:

> **A6.12 — What A6 doesn't cover.**
>
> A6 covers static compatibility evaluation at federation handshake time. A6 does NOT cover:
>
> - **Dynamic mid-session compatibility re-evaluation** if a peer upgrades during a long-lived federation session (out of scope; periodic re-handshake addresses this; spec belongs in a future transport-tier ADR).
> - **Schema-epoch migration mechanics.** A6 only checks epoch equality (rule 1) + the iOS sequestration carve-out (A6.11.3); the actual `sunfish migrate` command's contract is ADR 0001 + a future migration-tooling ADR.
> - **Plugin runtime version skew within a single node** (e.g., a node where the kernel was upgraded but a plugin wasn't restarted) — this is a single-node concern, not a federation concern.
> - **CRDT operation-log compatibility within an established federation.** This is paper §15 and A1's iOS event-queue contract; A6 is upstream of these (A6 decides whether the federation can even open; per-operation compat is decided post-handshake by the engine).
> - **Capability negotiation beyond compatibility.** A6 answers "are these two nodes structurally compatible to federate?"; the broader negotiation of *which features each side offers* is the Mission Space Negotiation Protocol (queued as ~ADR 0063 per W#33 §7.2).

#### A7.11 — Cited-symbol verification (re-applied per A4.3 standing rung-6 task)

Per the post-A4 standing commitment, A7's added/modified citations are spot-checked in both directions:

**Verified existing on `origin/main`** (positive-existence):
- All A6 citations remain valid as-listed in A6.7 (no removals).
- ADR 0007 `BusinessCaseBundleManifest.requiredModules` — verified existing (per F3 council finding which prompted A7.3 specifically).
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` — verified existing (per F12 council spot-check).

**Introduced by A7** (not on `origin/main`; ship in implementation hand-off):
- New wire-format message type: `VersionVectorVerdict` (per A7.1).
- Augmented type shape: `PluginVersionVectorEntry { version: SemVer, required: bool }` (per A7.3.2).
- Iterated tuple field: `instanceClass` enum reduced to two values (per A7.6); requires CanonicalJson enum-bump tolerance verification at hand-off.
- Augmented iOS A1 envelope: `captured_under_kernel: SemVer` + `captured_under_schema_epoch: uint32` (per A7.5; coordinated A1.x amendment required).
- New audit-record type: `LegacyEpochEvent` for iOS cross-epoch sequestration (per A7.5.3).

#### A7.12 — Cohort discipline log

Per `feedback_decision_discipline.md` cohort batting average:

- **Council batting average** (substrate amendments needing post-acceptance fixes): **8-of-N** (A6 council surfaced 1 Critical + 5 Major + 4 Minor — all mechanical to absorb pre-merge per the auto-merge-disabled posture). Cohort lesson holds: pre-merge council remained dramatically cheaper than post-merge.
- **Council false-claim rate (both directions):** unchanged at 2-of-9 across the cohort. The A6 council made 0 false-existence + 0 false-non-existence claims (F11–F14 are explicit positive-existence verifications with verification commands). F3 surfaces a NEW failure mode — *structural citation correctness* (the cited field exists in the cited ADR, but at the wrong layer of the schema). XO updates the `feedback_council_can_miss_spot_check_negative_existence` memory to cover this third direction (separate memory edit).
- **Standing rung-6 task reaffirmed:** XO spot-checks A7's added/modified citations (per A7.11) within 24h of merge. If any A7-added claim turns out to be incorrect, file an A8 retraction matching the A3 / A4 retraction pattern.

#### A7.13 — Companion amendment (A1.x) declared

A7.5's iOS-envelope augmentation requires a coordinated A1.x amendment that ratifies the envelope-shape change on the A1 side. This A1.x amendment is queued as a separate intake; A7 declares the dependency (Stage 06 build of A6's iOS path gates on A1.x landing). The A1.x intake stub is filed at `icm/00_intake/output/2026-04-30_ios-envelope-capture-context-tagging-intake.md` (XO follow-up; small mechanical amendment).

---
