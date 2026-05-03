# ADR 0028 Amendment A1 — Council Review (Stage 1.5 Adversarial)

**Date:** 2026-04-30
**Reviewer:** research session, four-perspective adversarial council per UPF Stage 1.5 (mobile carve-out scope)
**Amendment under review:** [ADR 0028 — A1 "Mobile reality check; iOS Phase 2.1 ships append-only events"](../../../docs/adrs/0028-crdt-engine-selection.md) (PR #342, branch `docs/adr-0028-a1-mobile`, auto-merge intentionally DISABLED pre-council per cohort discipline)
**Companion intake:** [`property-ios-field-app-intake-2026-04-28.md`](../../00_intake/output/property-ios-field-app-intake-2026-04-28.md) (W#23)
**Companion ADR:** [ADR 0028 main](../../../docs/adrs/0028-crdt-engine-selection.md) — Loro on desktop (Accepted 2026-04-22)

---

## 1. Verdict

**Accept with amendments. Grade: B (Solid).**

The architectural shape is sound: deferring Loro on iOS until a viable Swift binding exists, capturing append-only events on-device, and resolving conflicts at the Anchor merge boundary is the honest Phase 2.1 answer. The substantive gaps are: (1) LWW is named without enumerating which field-capture domains are actually safe under it — A1 treats append-only signatures and mutating WorkOrder.Status the same, and they aren't; (2) `URLSessionConfiguration.background` is asserted to provide durability semantics it only provides under specific `discretionary` + `sessionSendsLaunchEvents` settings A1 doesn't name; (3) the forward-compatibility claim at A1.7 between a `device_local_seq` queue and a future CRDT layer is hand-waved — CRDTs use vector clocks, not monotonic counters; (4) cited-symbol verification at A1.6 passed internally but fails externally on two of four citations + one forward-reference to an unauthored ADR. Five required amendments + three encouraged. None block W#23 Stage 02 spike; all should land before W#23 Stage 06 build emits its first persisted `device_local_seq`.

---

## 2. Findings (severity-tagged)

### F1 — `URLSessionConfiguration.background` durability claim is half-true (Major)

A1.1 asserts background URLSession provides "durability + retry semantics out-of-band — OS finishes uploads after suspension, retries on network change." True with caveats A1 doesn't name:

- **`discretionary` defaults defer uploads.** When `discretionary = true` (default for background sessions created from extensions; recommended in Apple docs for non-time-sensitive uploads), iOS defers uploads until conditions are favorable — Wi-Fi + plugged-in. A 09:00 property-inspection capture on a cellular-only iPad in the field may not leave the device until 19:00. A1's "uploads survive suspension" framing implies near-real-time; reality is hours-of-delay-possible.
- **`sessionSendsLaunchEvents` gates background relaunch.** Without it, the OS will not relaunch the app for completion handling; retry-on-failure stalls until user foregrounds.
- **`NSURLErrorBackgroundSessionWasDisconnected` (-997)** fires on device reboot mid-upload or force-quit. Memory-based upload bodies restart from zero; only file-based `uploadTask(with:fromFile:)` is OS-resumable. Multi-megabyte photo/PDF blobs need the file-based path.
- **Share-extension limitations:** if the field app ever grows a Share extension (drag from Photos.app), that path has restricted background-session support.
- **Empty-body POST quirk:** event-envelope-only POSTs (no blob) have been silently dropped in some iOS versions; needs explicit test on the W#23 OQ-I3 iOS 16 baseline.

Stage 06 implementer reading A1 alone will under-spec `URLSessionConfiguration` and learn the gotchas at integration-test time. **Major** — Stage 06 will hit this.

### F2 — LWW is conflated across fundamentally different domain shapes (Critical)

A1.2 says: "LWW for fields concurrently captured; latest `captured_at` wins, ties by `device_id` lexicographic. Append-only domains (signatures, audit) have no conflicts. Mutating domains (asset condition) use LWW." This treats six event types as one class. They are not:

- **Append-only by nature** (`Signature` per ADR 0054, `Receipt`, `Mileage`): LWW isn't even invoked. `(device_id, device_local_seq)` uniquely identifies the event; no field-level conflict possible. A1 is correct here.
- **Append-only with embedded mutable state** (`Inspection`, `WorkOrderResponse`): photos and findings are append-only at the leaf, but `status` is a flag with required ordering (draft → submitted → signed-off; acknowledged → in-progress → blocked → complete). Two contractors flipping `status` in the same minute is a real conflict that LWW resolves *deterministically wrong* a non-trivial fraction of the time.
- **Truly mutating** (`Asset`): some fields are LWW-safe (e.g., `LastSeenLocation`); status flags are not (rolling back from `complete` to `in-progress` because a stale device reconnects with an older state is the foot-gun).

Causality is the missing concept. CRDTs solve this with Lamport stamps / vector clocks; LWW with wall-clock + device-id tiebreak does not — `captured_at` carries no happened-before information. Two devices both offline an hour, both editing `WorkOrder.Status`, will produce a deterministic merge — but not necessarily the human-correct one.

ADR 0054's council found exactly this gap on concurrent revocation (council A4: "earliest-by-Lamport-timestamp"). A1 has reproduced it in a different domain. **Critical** — substrate ships a forensic foot-gun otherwise; status transitions look correct (deterministic, append-trail-preserved) and only surface as disputes weeks later.

### F3 — Cited-symbol verification gaps in A1.6 (Major)

A1.6 lists four cross-references, all asserted Accepted on `origin/main`. Verification:

- **`Sunfish.Foundation.Crypto.Signature` / `SignedOperation<T>`** — both exist (per ADRs 0003, 0004, 0049). **Pass.**
- **ADR 0032 "Keychain-stored pairing token's actor identifier"** — ADR 0032 defines workspace switching with a single root Ed25519 keypair stored once in OS keystore (line 114). It does **not** define a "pairing token" surface, does not name `device_id` as an actor identifier, does not specify a Keychain-stored pairing-token shape. The cited surface is invented. **Fail.**
- **ADR 0054 (canonicalization compatibility)** — ADR 0054's own A1 amendment introduces `Sunfish.Foundation.Canonicalization.JsonCanonical` (RFC 8785/JCS). A1 of this ADR refers to "RFC 8785" but does not name the canonicalizer symbol. **Partial pass.**
- **ADR 0046-A2 (`EncryptedField`)** — only `0046-A1` exists on `origin/main` (`docs/adrs/0046-a1-historical-keys-projection.md`); no `0046-A2`. The `EncryptedField` type doesn't exist. Vapourware citation. **Fail.**

Cohort precedent: A2 missed `ITenantKeyProvider`; A4 missed `IRecoveryClock` method-vs-property. A1 here missed *two* citations + 1 forward-ref (next finding) — slightly worse than the cohort average. **Major** — mechanical fixes, but load-bearing for the at-rest-encryption claim and device-identity chain.

### F4 — ADR 0061 cited but doesn't exist (Major)

A1's "Affected packages" and the PR body's "Linked artifacts" both reference ADR 0061 (three-tier peer transport). **ADR 0061 is not on `origin/main`.** It exists only as `icm/00_intake/output/mesh-vpn-cross-network-transport-intake-2026-04-28.md` (status `design-in-flight`). OQ-A1.1's default ("Phase 2.2 may add iOS-direct-to-Anchor read via Tailscale per ADR 0061") depends on an ADR that hasn't been authored. Forward-reference to vapourware ADR — same anti-pattern A1.6 was supposed to catch. **Major.**

### F5 — `device_local_seq` ↔ CRDT forward-compatibility is asserted, not analyzed (Major)

A1.7: "Phase 2.1 event queue → Phase 3 CRDT-on-mobile. The contract is forward-compatible: Phase 3 can replace the queue OR coexist (queue as durable transport; CRDT as in-memory representation)."

This is the Devil's Advocate's strongest line and A1 doesn't engage:

- `device_local_seq` is monotonic per-device. Useful for dedup, useless for cross-device causality.
- CRDTs use vector clocks / Lamport stamps. Loro internally uses Lamport; Yjs uses version-vector + Lamport.
- A queue can be replayed *into* a CRDT (each envelope becomes an operation tagged with the device's then-current Lamport stamp at replay time), but original Lamport stamps are not recoverable from `device_local_seq` alone.
- "OR coexist" is the harder option. Running both means *two divergent representations of the same domain state* with merge logic between them — exactly the dual-write pathology ADR 0028 main was chosen to avoid by picking Loro.

The vague claim influences W#23 Phase 2.1 design choices today (whether the queue should be CRDT-replayable; what "replayable" actually means) without being concrete enough to act on. **Major.**

### F6 — Paper §6.1, §15, §19 not reconciled (Minor)

Paper §6.1 specifies three peer-transport tiers (mDNS / Mesh VPN / Managed Relay). A1's `URLSessionConfiguration.background` to Bridge = tier 3 (Managed Relay per paper §17.2). A1 doesn't say "iOS Phase 2.1 uses tier 3 only; tier 2 is Phase 2.2; tier 1 if Anchor + iPad on same Wi-Fi". Paper §15 puts CRDT property tests at Level 1; A1 doesn't commit Level-1 tests for the iOS event queue (LWW determinism, dedup under retry storm, deterministic tiebreak). Paper §19 treats compaction as first-class for CRDTs; A1's iOS event queue *also has a growth problem* (offline-iPad accumulation: hundreds of events × tens of MB blobs after a week offline) and A1 doesn't specify a queue-size cap, TTL, or local-VACUUM policy. **Minor** in aggregate — housekeeping, not load-bearing.

### F7 — Keychain semantics for `device_local_seq` persistence under-specified (Minor)

OQ-A1.2 asserts `device_local_seq` is "persisted in Keychain alongside the pairing token; reinstall preserves the sequence". Depends on `kSecAttrAccessible` policy (`AfterFirstUnlock` recommended; `Always` deprecated; `WhenPasscodeSet` wipes on passcode-removal). A1 doesn't specify. iCloud Keychain sync is also relevant: if `device_id` syncs across the user's iCloud-paired devices, iPhone + iPad share a `device_id` and "single-actor per device" breaks. Default Sunfish stance is likely "device-local only, no iCloud Keychain" but A1 should say so. **Minor.**

### F8 — Operator device-key read-back contradicts "no on-device merge" framing (Encouraged)

A1.2 says "the iOS app never reads back state from Anchor — it only captures and sends." For most domains this holds. For `Signature` (ADR 0054 two-identity model), the iPad must hold the operator's *current* device-key locally to sign at capture time — meaning it reads operator-key state from somewhere (Bridge HTTPS at pairing, refreshed periodically). Not a CRDT merge, but a state read. A1 should acknowledge: signatures captured during an offline window are signed under the iPad's then-cached operator key; verification resolves at the Anchor merge boundary via ADR 0046-A1's historical-keys projection. **Encouraged** — clarification, not a flaw.

---

## 3. Recommended amendments

### A1 (Required) — Replace LWW blanket with per-event-type conflict-policy table

Replace A1.2's blanket framing with:

| event_type | Policy | Notes |
|---|---|---|
| `Inspection` | append-only at photo/finding leaf; **forward-only `status`** (draft → submitted → signed-off; backward-LWW rejected at merge, emitted as conflict-event for human review) | |
| `Receipt` | append-only | Immutable post-capture |
| `Asset` | LWW per field; **`Asset.WorkOrderStatus` forward-only** (acknowledged → in-progress → blocked → complete; no rollback) | |
| `Signature` | append-only (per ADR 0054) | Revocations are separate append events |
| `Mileage` | append-only | Immutable post-capture |
| `WorkOrderResponse` | append-only at response leaf; status forward-only same as `Asset.WorkOrderStatus` | |

Forward-only-status guard is load-bearing — prevents stale-device-clobbers-fresh-state. **Required because F2 is Critical.**

### A2 (Required) — Specify `URLSessionConfiguration.background` settings explicitly

Add `### A1.2.1 URLSession configuration` paragraph naming: `discretionary = false` for foreground-captured events; `sessionSendsLaunchEvents = true`; `allowsCellularAccess = true` (default named); `kSecAttrAccessibleAfterFirstUnlock` for Keychain entries; iOS 16 baseline (matches W#23 OQ-I3). Pin file-based `uploadTask(with:fromFile:)` (OS-resumable) over memory-based for blob uploads. Flag `NSURLErrorBackgroundSessionWasDisconnected` as a known retry case. **Required because F1 is Major.**

### A3 (Required) — Fix cited-symbol citations in A1.6

1. **`device_id` derivation** — replace "Keychain-stored pairing token per ADR 0032" with "`device_id` derived from the install's root Ed25519 public key per ADR 0032; pairing-token surface for multi-device flows TBD pending Phase 2.2 multi-device ADR". Drops invented surface cleanly.
2. **`EncryptedField` per ADR 0046-A2** — replace with "at-rest encryption via SQLCipher (W#23 intake §"In scope"): whole-database encryption only in Phase 2.1; per-field encryption ADR may be authored later if needed". Drops vapourware reference.
3. **ADR 0054 canonicalization** — name the symbol: "Canonical-encoded payload per `Sunfish.Foundation.Canonicalization.JsonCanonical` (per ADR 0054 Amendment A1; RFC 8785 / JCS)". Makes Stage 06 contract explicit.

**Required because F3 is Major and Decision Discipline Rule 6 was created to catch this.**

### A4 (Required) — Drop forward-references to unauthored ADR 0061

Replace "ADR 0061 (three-tier peer transport)" citations with `icm/00_intake/output/mesh-vpn-cross-network-transport-intake-2026-04-28.md` (status `design-in-flight`) and note that Phase 2.2 cross-network-direct sync is *gated on* that intake being promoted to an Accepted ADR. **Required because F4 is Major.**

### A5 (Required) — Pick the Phase 3 forward-compatibility option concretely

Replace A1.7's "OR coexist" with **Option α (recommended):** "The Phase 2.1 event queue is a one-way migration source for any future CRDT-on-mobile. Phase 3 CRDT replays the queue into Lamport-timestamped operations *at the merge boundary*, then deprecates the queue. There is no coexistence period; queue-shutdown precedes CRDT-startup. Pre-Phase-3 events carry no causality beyond per-device monotonic ordering; this is acceptable for the field-capture domain." **Required because F5 is Major.**

### A6 (Encouraged) — Add Phase 2.1 queue-growth + queue-compaction policy

Per paper §19 first-class-compaction principle: queue-size cap (suggested 5000 events or 500 MB blob storage; user-visible warning at 80%); queue-event TTL (30 days unsubmitted = warning; 90 days = forced foregrounded reauth + sync); local SQLite VACUUM on submitted-and-acknowledged events; blob-cleanup after Anchor confirms ingest. **Encouraged.**

### A7 (Encouraged) — Add Level-1 property-test commitments per paper §15

State explicitly that the iOS event queue ships with Level-1 property tests covering: `(device_id, device_local_seq)` uniqueness under retry storm; LWW determinism under reordered delivery; deterministic tiebreak by `device_id` lexicographic order; forward-only-status-flag rejection on rollback attempt (per A1). **Encouraged.**

### A8 (Encouraged) — Acknowledge operator device-key read-back in A1.2

Add: "Note: the iPad reads its current operator device-key from Bridge at pairing time and refreshes periodically (per ADR 0046-A1 historical-keys); this is a state read, not a CRDT merge. Signatures captured during an offline window are signed under the iPad's then-cached operator key; verification resolves at the Anchor merge boundary via the historical-keys projection." **Encouraged because F8 is a clarification.**

---

## 4. Quality rubric grade

**Grade: B (Solid).** Path to A is mechanical (A1–A5 land).

- **C threshold (Viable):** All structural elements present (driver, why-mobile-different, decision, conflict resolution, reconsider triggers, compatibility, affected packages, cited-symbol verification, open questions, pre-acceptance audit). No critical *planning* anti-patterns. **Pass.**
- **B threshold (Solid):** Stage 0 sparring evident in A1.1 (three constraints justifying deviation: no-mature-binding + single-actor-profile + URLSession-suffices); FAILED conditions present in A1.3 (four reconsider triggers, two measurable: 6+ months Loro Swift maintainer activity, "material data loss in real workflow"); Cold Start Test plausible. **Pass.**
- **A threshold (Excellent):** Misses on three counts: (1) Pedantic Lawyer / Pessimistic Risk Assessor perspectives weren't fully run pre-PR — F1 + F2 would have surfaced under either; (2) cited-symbol verification at A1.6 was done internally but failed externally on 2-of-4 + 1 forward-ref (F3 + F4); (3) Reference Library doesn't link to Apple `URLSessionConfiguration` reference docs, RFC 8785, or paper §6.1/§15/§19 directly. **Does not reach A.**

A grade of **B with required amendments A1–A5 applied promotes to A.**

---

## 5. Council perspective notes (compressed)

- **Distributed-systems reviewer:** "LWW on wall-clock is fine for append-only and naturally LWW-safe attributes (Asset.LastSeenLocation, Receipt content). It is *not* fine for status-flag transitions with required ordering — WorkOrderResponse.Status, Inspection.Status, Asset.WorkOrderStatus. Append-only with forward-only-flag-guard is the right policy. ADR 0054's council found the same gap on concurrent revocation; A1 has reproduced it." Drives F2 + amendment A1.
- **iOS engineer pragmatist:** "GRDB.swift solid; SQLCipher fine; `URLSessionConfiguration.background` correct primitive. Apple docs are unambiguous that `discretionary` defaults are *not* what A1 implies — they're 'defer until plugged-in-on-Wi-Fi'. Stage 06 will set discretionary=false explicitly or hit the gotcha. Plus `sessionSendsLaunchEvents`, file-based upload tasks, `kSecAttrAccessibleAfterFirstUnlock`. None advanced — minimum viable iOS background-upload spec." Drives F1 + amendment A2.
- **Cohort discipline reviewer:** "A1.6 self-audit passes its own checklist but fails external verification on 2-of-4 citations: ADR 0032 doesn't define pairing-token surface, ADR 0046-A2 doesn't exist (only 0046-A1). ADR 0061 cited as if Accepted, only an intake. Per cohort lesson, exactly the class of error council exists to catch. Mechanical fixes — substrate is fine. Auto-merge-disabled was the right call." Drives F3 + F4 + amendments A3 + A4.
- **Forward-compatibility reviewer:** "`device_local_seq` ↔ CRDT-vector-clock is the load-bearing translation A1 doesn't analyze. Monotonic-per-device is *strictly weaker* than vector-clock; queue can be replayed *into* CRDT (each event becomes an operation tagged with current Lamport stamp at replay), but pre-Phase-3 events carry no genuine causality. 'OR coexist' is the worst case — dual representations with merge logic between them, exactly what ADR 0028 main was chosen to avoid. Pick one-way migration explicitly. Concur the four-trigger A1.3 list — those are reasonable." Drives F5 + amendment A5.

---

## 6. Cohort discipline scorecard

| Cohort baseline | This amendment |
|---|---|
| 7-of-7 prior amendments needed post-acceptance fixes | Will be 8-of-8 if A3/A4 fixes are applied post-merge |
| Cited-symbol verification: avg ~1 missed symbol per amendment | This amendment: 2 missed (`ADR 0032 device_id`, `ADR 0046-A2 EncryptedField`) + 1 forward-ref to unauthored ADR 0061 |
| Council pre-merge vs post-merge | Pre-merge (correct call: A2 cohort cost ~24h held-state post-merge; A4 cost zero pre-merge) |
| Severity profile | 1 Critical (F2), 4 Major (F1, F3, F4, F5), 2 Minor (F6, F7), 1 Encouraged (F8) |

The cohort lesson holds: every substrate amendment so far has needed council fixes; pre-merge council is dramatically cheaper than post-merge. None of the findings here are show-stoppers; all are mechanical fixes XO can apply post-merge.

---

## 7. Closing recommendation

**Accept A1 with required amendments A1–A5 applied before W#23 Stage 06 build emits its first persisted `device_local_seq`.** The architectural decision (no Loro on iOS Phase 2.1; append-only event queue; LWW-with-guards at Anchor merge boundary) is correct and consistent with substrate-cohort design taste. Mechanical fixes are 1–2 hours of XO work; the LWW domain table (A1) is the only substantive addition and clarifies a load-bearing policy that would otherwise land as silent forensic debt.

W#23 Stage 02 spike can begin immediately on the architectural decision; Stage 06 build gates on A1–A5.
