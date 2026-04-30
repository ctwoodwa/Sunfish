# W#23 iOS Field-Capture App substrate v1 — Stage 06 hand-off — Council Review

**Date:** 2026-04-30
**Reviewer:** research session, four-perspective adversarial council per UPF Stage 1.5
(scoped: iOS engineer pragmatist; repo architecture / cross-language reviewer;
pairing-flow / security reviewer; cohort-discipline / cited-symbol reviewer)
**Hand-off under review:**
[`icm/_state/handoffs/property-ios-field-app-stage06-handoff.md`](../../_state/handoffs/property-ios-field-app-stage06-handoff.md)
(landed via PR #354, 2026-04-30; auto-merge enabled per cohort cadence)
**Companion artifacts:** ADR 0028 (+ A1+A2+A3); ADR 0048 (+ A1+A2); ADR 0061;
ADR 0046-A1; ADR 0054 (+ A1); W#23 intake `property-ios-field-app-intake-2026-04-28.md`
**Cohort precedents:** W#19 Phase 3 stub addendum (PR #274); W#21 Phase 0 stub
addendum (`property-signatures-stage06-addendum.md`); W#32 mesh-VPN addendum

---

## 1. Verdict

**Accept-with-amendments.** Hand-off has already merged on auto-merge per cohort
cadence; this council's findings flow into a follow-up addendum (matching the
W#19 / W#21 / W#32 precedents) — they do **not** gate the merge.

The substrate-v1 framing is right (8 phases, ~28.5h, capture flows deferred to
follow-up hand-offs, halt-conditions named at every realistic break-point). The
phase decomposition is mechanical against ADR 0028 A1+A2+A3; the per-event-type
LWW table flow-through to Phase 3 is faithful; Phase 0 stub framing for
per-device install identity matches the W#19 / W#21 precedent and the surface
genuinely is small enough to ship inline. **Three load-bearing citation /
contract gaps surface that the addendum should close before Phase 3 ships
its cross-language canonicalization test or Phase 4 ships its sync engine.** All
three are fixable inside the existing structure — none require re-authoring.

XO should author an addendum with the §5 amendments before COB starts Phase 3
(Phase 0–2 can proceed unblocked).

---

## 2. Anti-pattern findings (21-pattern sweep)

| AP # | Name | Severity | Where it fires |
|---|---|---|---|
| **#21** | Assumed facts without sources | **Critical** | Phase 3 cites `Sunfish.Foundation.Canonicalization.JsonCanonical` as a stable .NET-side anchor for cross-language parity. **This symbol does not exist on `origin/main`.** Verified via `git ls-tree -r origin/main \| grep -i Canonicalization`: actual symbols are `Sunfish.Foundation.Crypto.CanonicalJson`, `Sunfish.Foundation.Assets.Common.JsonCanonicalizer`, and `Sunfish.Kernel.Signatures.Canonicalization.JsonCanonicalCanonicalizer`. ADR 0054 A1 *promised* the new namespace + new `packages/foundation-canonicalization/` package but its implementation checklist is **unchecked** on origin/main; ADR 0028-A2.10's "verified existing per ADR 0054 A1 (named explicitly post-A2.3)" line is itself a **false-positive verification claim** (re-using ADR 0028-A3's lesson about negative-existence claims, in the positive direction). The `JsonCanonicalCanonicalizer` that *does* exist self-documents `CanonicalizationKind = "json-canonical/rfc-8785-pragmatic"` and explicitly states "Number serialization differences (e.g., trailing zeros, scientific notation) are handled by `System.Text.Json`'s default writer which is sufficient for Sunfish's signing surface; if a future use case requires strict RFC 8785 number serialization, the canonicalizer here is the swap point." **The .NET side IS NOT RFC 8785.** Phase 3's "byte-for-byte cross-language" gate is therefore a phantom gate today — Swift could implement perfect RFC 8785 and still fail against the existing pragmatic .NET canonicalizer on number-serialization edge cases. |
| **#13** | Confidence without evidence | **Critical** | Phase 4's halt-condition framing for the W#28 Bridge route family is correct in shape but the cited-symbol surface is wrong. Hand-off references `apps/bridge/Pages/...` and `apps/bridge/Controllers/...` (4 occurrences across Phase 4 + Phase 5 + Phase 6). The Bridge accelerator lives at `accelerators/bridge/` (verified `git ls-tree origin/main apps/` returns `apps/{README.md, docs, kitchen-sink, local-node-host}` — **no `apps/bridge`**). The actual Bridge tree is `accelerators/bridge/Sunfish.Bridge/`, with Pages under `accelerators/bridge/Sunfish.Bridge.Client/Pages/`. Hand-off's Phase 5 instruction `apps/bridge/Pages/Field/Pair/{code}.cshtml` would create a wrong-package directory under a non-existent `apps/bridge/` tree, and `apps/bridge/Controllers/FieldPairingController.cs` similarly. |
| **#19** | Discovery amnesia | **Major** | Hand-off cites W#18 `VendorMagicLink` as a precedent for Phase 0's HMAC purpose-label pattern (`field-pairing-token-hmac` matching `vendor-magic-link-hmac`). **`VendorMagicLink` does not exist on `origin/main`** — verified `git ls-tree -r origin/main \| grep -iE "VendorMagicLink\|vendor-magic-link"` returns nothing. W#18 (Vendor Onboarding) is `built` in part but the vendor-magic-link surface specifically isn't on main. The `field-pairing-token-hmac` purpose label pattern *does* match a documented ITenantKeyProvider purpose-label convention (e.g., `thread-token-hmac` from W#20 — verified existing in the ITenantKeyProvider XML doc), but the cited precedent specifically is wrong and the addendum should swap to a real precedent (W#20 `thread-token-hmac`). |
| **#3** | Vague success criteria | **Major** | Phase 3 acceptance lists "10/10 cross-language canonicalization fixtures match byte-for-byte" but does not specify (a) fixture types — number-edge-cases? unicode? nested objects?; (b) how the .NET-side fixtures are generated (which tool emits them — `CanonicalJson.Serialize`? `JsonCanonicalCanonicalizer.Canonicalize`? `JsonCanonicalizer.ToCanonicalBytes`? They produce different bytes); (c) whether the fixture file shape is `(input.json, expected_canonical.bin)` pairs OR `(input.json, expected_canonical.txt)` UTF-8-decoded; (d) provenance — whether fixtures are committed pre-built or regenerated each test run. Without this surface the test will pass on whichever .NET canonicalizer the implementer happens to call first. |
| **#1** | Unvalidated assumptions | **Major** | Phase 2 SQLCipher key derivation: "ship a Phase-1-only-key derived from install root + sentinel `\"sunfish-field-sqlcipher-v1\"`". This generates a single-device-bound at-rest encryption key with no recovery path. If the iPad's Keychain entry is wiped (factory reset, hardware loss, OS migration that fails to migrate Keychain) the local SQLCipher database is unrecoverable — including queued-but-not-yet-synced events. Hand-off does not name this trade-off as a halt-condition or as an accepted-risk. Foundation.Recovery's `PaperKeyDerivation` pattern (cited as the model) explicitly addresses this for the desktop case via paper-printed recovery key; mobile gets neither paper-key nor iCloud-Keychain-sync (`kSecAttrSynchronizable=false` per Phase 0) and inherits no recovery surface. Either name it as accepted-risk with user-visible warning at first launch ("if you lose this device, queued events are lost") or open a Phase 0.5 / Phase 8.5 hand-off for a mobile recovery surface. |
| **#15** | Premature precision | **Minor** | Phase 1 Info.plist enumerates `UIRequiredDeviceCapabilities = [armv7]`. iOS 16+ requires arm64; armv7 is the iOS 7-era 32-bit ARM capability and was removed from `UIRequiredDeviceCapabilities` valid-values when 32-bit support ended (iOS 11). The correct iOS-16-baseline value is `arm64` or — more idiomatically for modern apps — omit the key entirely (the iOS App Store derives device support from the binary's architectures + `MinimumOSVersion`). |
| **#17** | Delegation without context transfer | **Minor** | Phase 0's iOS-side `DeviceId` derivation is "first 16 hex chars of SHA-256 of the public key" — but the ADR 0028-A2.1 per-event-type LWW table relies on `(device_id)` as a tiebreak under Lamport-equal events, and 16 hex chars = 64-bit truncated SHA-256 has a birthday-bound collision probability of ~1 in 2^32 across the install population. For a single-tenant property business with 3–10 devices this is fine; for multi-tenant Phase 2.2+ with thousands of paired iPads it isn't. Either pin the truncation as Phase 2.1-only and name the Phase 3+ growth path, or use the full SHA-256 (or full Ed25519 public key) at the cost of a longer envelope. |

**Anti-patterns avoided cleanly:** #2 (clear three-tier scope decomposition with
"NOT in scope" deferred-list); #4 (rollback path is concrete — net-new
accelerator, no production consumers, ledger-flip-only); #5 (consequences extend
past Decision via capture-flow follow-up table); #6 (Resume Protocol implicit via
8-phase PASS/FAIL gates); #9 (Stage 0 sparring evidence in companion W#23 plan
memory + intake); #11 (no zombie risk — TestFlight smoke test is the kill
trigger); #12 (~28.5h estimate is well-shaped against the 8-phase scope and
matches the cohort's largest-first-slice baseline).

---

## 3. Top 3 risks (cohort-context-weighted)

1. **Phase 3's RFC 8785 cross-language test is unenforceable today.** `Sunfish.Foundation.Canonicalization.JsonCanonical` does not exist on `origin/main`; the existing `CanonicalJson` / `JsonCanonicalCanonicalizer` are explicitly NOT RFC 8785 ("pragmatic", "if a future use case requires strict RFC 8785 number serialization, the canonicalizer here is the swap point"); ADR 0054 A1's promised `packages/foundation-canonicalization/` is unchecked. COB will hit a halt at Phase 3 build time when they `git grep` the symbol and find it doesn't exist. The right sequencing is: ship ADR 0054 A1's foundation-canonicalization package **first** (W#21 dependency surface), then W#23 Phase 3 builds against it. Otherwise W#23 Phase 3 either (a) halts with a `cob-question-*-w23-p3-canonicalizer-missing.md`; or (b) silently builds against the pragmatic canonicalizer + ships a phantom test that doesn't actually enforce RFC 8785 parity. **Highest impact; addendum should resequence.**
2. **Phase 4 / Phase 5 / Phase 6 Bridge paths are wrong-package-tree.** `apps/bridge/` does not exist; Bridge lives at `accelerators/bridge/`. COB will hit this immediately on Phase 4 PR but the hand-off framing is misleading enough that an over-eager implementation could `mkdir apps/bridge/` and create a parallel non-canonical tree. Also: Phase 4's `POST /api/v1/field/event` endpoint does not exist on Bridge (verified `git grep "MapPost" accelerators/bridge/Sunfish.Bridge/` returns no `/api/v1/` matches; Bridge's only `MapPost` calls today are in `MockOktaService` for OAuth shim routes); W#28 Phase 5+ owns the route family per the ledger row + ADR 0028-A2.6, and W#28 Phase 5 is currently `gated on W#22 Leasing Pipeline contracts`. So W#23 Phase 4's halt-condition is **definitely** going to fire; the question is whether XO routes the unblock through W#28 (proper) or via inline Phase 4.5 (precedent-breaking).
3. **No mobile recovery story; SQLCipher key loss = queued-event loss.** Phase 2's key-derivation is single-device-bound with no recovery path. The whole point of the offline-first capture pipeline is durability against connectivity loss; making it brittle against device-loss is an inverted threat model. The cohort precedent (W#21 / W#46 paper-key) doesn't trivially port (no paper key on a phone-only workflow) but the gap should at least be named-and-accepted, not silent. Worst-case: a property manager loses the iPad after capturing 200 receipts offline; queue is lost; receipts must be re-captured from physical originals (which may already have been discarded). Per the W#23 intake's "field-grade reliability" framing this is a substantive gap.

---

## 4. Top 3 strengths

1. **Cohort-canonical phase decomposition.** 8 phases / ~28.5h matches the cohort
   baseline for largest-first-slice (W#19 was 12-19h, W#22 leasing pipeline was
   ~22h, W#28 was 18-25h; W#23 is the largest substrate hand-off authored to
   date and the size is justified by the new-platform surface). Phase 0 stub
   precedent is correctly invoked. Phase boundaries map cleanly to PR scopes
   (~5 PRs estimated). Halt-conditions are named at every realistic break-point
   (7 named, including one CO-class for the Apple Developer Program $99/yr).
2. **ADR 0028-A2.x flow-through is faithful.** The handoff threads ADR 0028-A2.1
   per-event-type LWW table, A2.2 URLSession config (with all 4 settings cited
   exactly), A2.3 cited-symbol fixes, A2.7 compaction policy (5000-event /
   500-MB / 30-90-day TTL), and A2.8 Keychain semantics through their respective
   phases. Phase 4's gate explicitly asserts the URLSession config matches A2.2
   settings byte-for-byte ("smoke test asserts `discretionary == false`,
   `sessionSendsLaunchEvents == true`, `allowsCellularAccess == true`,
   `waitsForConnectivity == true`, `timeoutIntervalForResource >= 7*24*3600`").
   This is the right model for substrate-amendment-to-build flow-through.
3. **Capture-flow deferral is well-shaped.** The 6-row follow-up hand-off table
   (W#23.1 Receipts → W#23.6 Work-Order-Response) names domain + native APIs +
   effort estimate per row, totalling ~37-54h. This is the right level of detail
   for a substrate-handoff "what comes next" — concrete enough that COB can
   point at it as the priority-queue depth-3 backlog, abstract enough that it
   doesn't pre-commit specific Stage 06 phases.

---

## 5. Specific amendments (addendum-required)

1. **A1 (Critical):** Replace Phase 3's `Sunfish.Foundation.Canonicalization.JsonCanonical` reference with one of:
   - **Option 1 (preferred, requires resequencing):** Gate W#23 Phase 3 on ADR 0054 A1's promised `packages/foundation-canonicalization/` package shipping first (likely as a W#21 follow-up phase or a standalone ADR-0054-A1 implementation hand-off). Phase 3 then builds against the new RFC-8785-conformant `Sunfish.Foundation.Canonicalization.JsonCanonical`.
   - **Option 2 (minimal-resequencing):** Replace the "RFC 8785" framing with "canonicalizer-parity-with-`Sunfish.Foundation.Crypto.CanonicalJson`" framing. Pin the existing pragmatic-canonicalizer rules (alphabetical key sort, no whitespace, UTF-8, `System.Text.Json` number serialization) as the parity target. Cross-language test fixtures generated by `CanonicalJson.Serialize`. Add an addendum note that strict RFC 8785 conformance is a follow-up surfaced when ADR 0054 A1's foundation-canonicalization package ships.
   - Either option requires also updating ADR 0028-A2.10's verified-symbols table to remove the false-positive `Sunfish.Foundation.Canonicalization.JsonCanonical` entry. (Same retroactive-correction shape as ADR 0028-A3's retraction of A2.4's false-vapourware claim.)
2. **A2 (Critical):** Replace all `apps/bridge/` path references with `accelerators/bridge/` paths. Specifically:
   - Phase 5: `apps/bridge/Pages/Field/Pair/{code}.cshtml` → `accelerators/bridge/Sunfish.Bridge.Client/Pages/Field/Pair/{code}.razor` (Sunfish.Bridge uses Razor Components / Blazor pages, not `.cshtml` Razor Pages — verify against the existing `Pages/Account/*.razor` pattern)
   - Phase 5: `apps/bridge/Controllers/FieldPairingController.cs` → likely `accelerators/bridge/Sunfish.Bridge/Endpoints/FieldPairingEndpoint.cs` (Bridge's existing pattern is Minimal-API endpoints; verify by checking if Bridge has any `Controllers/` directory at all — currently none exist, the closest is `accelerators/bridge/Sunfish.Bridge/Proxy/`)
   - Phase 6: `POST /api/v1/field/unpair` and Phase 4: `POST /api/v1/field/event` + `POST /api/v1/field/blob/<sha256>` — confirm the route family lands inside `accelerators/bridge/Sunfish.Bridge/` minimal-API surface, not `apps/bridge/`
3. **A3 (Major):** Swap the W#18 `VendorMagicLink` precedent reference for the W#20 `thread-token-hmac` precedent. The HMAC purpose-label pattern is right; the cited symbol is wrong. The actual existing precedent on `origin/main` is `ITenantKeyProvider.DeriveKeyAsync(tenant, "thread-token-hmac", ct)` per the W#20 Phase 0 stub addendum. Phase 0's `field-pairing-token-hmac` purpose label is the natural sibling to that.
4. **A4 (Major):** Tighten Phase 3 acceptance criteria. Specify (a) fixture types covered (number edge cases including very large integers, leading zeros, scientific notation; nested objects with key-order variation; arrays with mixed types; UTF-8 strings with non-ASCII; `null` / `true` / `false` literals); (b) which .NET API generates the canonical bytes (pin to one — recommend `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` for Phase 3, with addendum note if A1 Option 1 resequencing changes this); (c) fixture file format (`(input.json, expected_canonical.bin)` byte-pairs; binary not text to avoid encoding ambiguity); (d) fixtures committed pre-built at `_shared/fixtures/json-canonical/` (note: `_shared/fixtures/` does not exist on origin/main — Phase 3 needs to create it).
5. **A5 (Major):** Add an §"Accepted risk: SQLCipher key loss = queued-event loss" sub-section under Phase 2. Either (a) name it as accepted-risk with user-visible first-launch warning ("if you lose this device, any captured-but-not-synced events will be unrecoverable; sync frequently") OR (b) open a Phase 0.5 / Phase 8.5 follow-up hand-off for a mobile recovery surface (deferred mobile-paper-key, iCloud-Drive blob backup, cross-device pairing-promotion, etc.). Either resolves the silent gap.
6. **A6 (Minor):** Phase 1 Info.plist: drop `UIRequiredDeviceCapabilities = [armv7]` — armv7 is invalid for iOS 16+ baselines and the iOS App Store will likely reject the build. Either set `arm64` or omit the key entirely. Add explicit `CFBundleVersion`, `CFBundleShortVersionString`, and a `LaunchScreen.storyboard` reference (or modern `UILaunchScreen` dictionary equivalent — iOS 14+ pattern). Confirm whether the build needs a `PrivacyInfo.xcprivacy` privacy manifest (Apple-required since iOS 17 for new submissions).
7. **A7 (Minor):** Phase 0 `DeviceId` truncation note. Add a note to Phase 0's `DeviceId.swift` spec: "16-hex-char truncation is sufficient for Phase 2.1 single-tenant property business (≤10 paired devices); Phase 3 multi-tenant scaling may require widening to full SHA-256 hex or Ed25519 public-key bytes." Preserves the Phase-2.1-simplicity choice while naming the Phase-3 growth path.

**Optional A8 (deferred, not addendum-required):** Re-verify whether the `BGTaskScheduler` lifecycle Phase 4 implies actually permits the resource-cost the field-event upload requires. iOS's `BGTaskScheduler` budget is ~30 sec/run and ~1 run/day in pessimistic conditions; combined with Phase 4's `URLSessionConfiguration.background` (which does not consume `BGTaskScheduler` budget — they're orthogonal lanes), the design likely works, but the hand-off does not name the lane separation. The right surface is probably the `BGProcessingTaskRequest` lane (longer time budget; runs on charge + Wi-Fi) for opportunistic blob compaction + URLSession-background for the actual upload. Recommend Phase 4 spec clarifies this; can defer to first build.

---

## 6. Quality rubric grade

**Grade: B (Solid).** Rationale:

- **C threshold (Viable):** All 5 CORE sections present (Scope summary as Context;
  per-phase Gate as Success Criteria; Halt-conditions as Assumption/Validation
  surface; 8 Phases with PASS/FAIL binary gates; Acceptance criteria as
  Verification). Three critical anti-patterns fire (#21 vapourware citation, #13
  wrong-package paths, plus the strong-Major #3 + #19 + #1) but they are content
  gaps inside the spec, not planning-shape gaps. Passes C.
- **B threshold (Solid):** Stage 0 sparring evidence in companion W#23 plan
  memory + intake; Confidence Level not explicitly declared but cohort-canonical
  shape (largest-first-slice; 7 halt-conditions named; capture-flow follow-up
  table) implies HIGH; Cold Start Test plausible (eight phases, file-by-file
  spec, gate-per-phase); FAILED conditions present (7 halt-conditions). Passes B.
- **A threshold (Excellent):** Misses on five counts: (1) `Sunfish.Foundation.Canonicalization.JsonCanonical` is cited as if verified-existing but is not — repeats the exact failure mode ADR 0028-A3 lessons-learned codified ("council can also miss / falsely report — XO must spot-check cited-symbol claims, especially negative-existence"); (2) `apps/bridge/` paths are wrong-tree across 4 occurrences; (3) W#18 `VendorMagicLink` precedent does not exist on origin/main; (4) Phase 3's "byte-for-byte" gate is unenforceable until the canonicalization story is resolved; (5) mobile recovery story is silent. Does not reach A.

A grade of **B** with the §5 amendments applied (especially A1 + A2) promotes
the hand-off to **A**. Without the amendments, COB will hit halt-conditions at
Phase 3 (canonicalizer missing) and Phase 4–6 (wrong-tree Bridge paths) within
the first PR cycle, producing the same `cob-question-*` round-trip cost the
cohort has paid before. Cost-to-fix-now is one addendum PR; cost-to-fix-at-build
is at minimum two halt-condition round-trips + a Phase 3 canonicalizer
sequencing decision.

---

## 7. Cohort-discipline notes

- **Council batting average update (substrate hand-offs):** This makes
  10-of-10 substrate hand-offs / amendments needing post-authoring council
  fixes (cohort batting average locked; pattern is universal — every substrate
  artifact authored without pre-merge council surfaces non-trivial gaps).
- **Council false-negative rate:** ADR 0028-A2.10's positive-existence claim
  for `Sunfish.Foundation.Canonicalization.JsonCanonical` was never spot-
  checked at the time A2 shipped; this council retroactively flags it as a
  **second false-positive verification claim** in the cohort (after A2.4's
  false-negative on ADR 0061 retracted by A3). Total false-claim rate now
  2-of-9; approaching the "warrant a separate verify-council's-cited-symbol-
  claims pass" threshold ADR 0028-A3.4 named (>~2-of-N). XO should consider
  adding a standing rung-6 fallback task for COB: "spot-check the cited-symbol
  table in any substrate ADR amendment that ships under auto-merge."
- **Spot-check discipline applied here:** This council ran `git ls-tree -r
  origin/main \| grep -iE "JsonCanonical\|Canonicalization\|VendorMagicLink"`
  and `git ls-tree origin/main apps/` and `git ls-tree origin/main
  accelerators/bridge/Sunfish.Bridge/` and `git grep -n "MapPost"
  accelerators/bridge/` *before* declaring the symbols missing. Per the
  `feedback_council_can_miss_spot_check_negative_existence` memory, every
  negative-existence claim in §2 / §3 / §5 is independently verified.
- **Auto-merge cadence preserved:** PR #354 has already merged. This council's
  amendments flow into a separate addendum PR (matching W#19 Phase 3 addendum /
  W#21 Phase 0 stub addendum / W#32 mesh-VPN addendum precedents); the merge
  was not gated on this council. XO authors the addendum as a standalone PR.
- **Phase 0–2 are unblocked.** The Critical findings target Phase 3 (canonicalization)
  + Phase 4–6 (Bridge paths). COB can proceed with Phase 0 (per-device install
  identity) + Phase 1 (SwiftUI scaffold) + Phase 2 (GRDB + SQLCipher) without
  waiting for the addendum. The addendum gates Phase 3 onward.

---

## 8. Council perspective notes (compressed)

- **iOS engineer pragmatist:** "GRDB.swift + SQLCipher pairing is canonical
  (production-grade pairing used by 1Password, Day One, etc.); `swift-crypto`
  is the right Ed25519 dep over CryptoKit because it's open-source + cross-
  platform-portable + matches the `_shared/fixtures/` cross-language test
  shape better than CryptoKit's Apple-Silicon-locked impl. `URLSessionConfiguration.background`
  per A2.2 settings is sound. Phase 1 Info.plist has `armv7` which is invalid
  for iOS 16+; minor but will fail App Store validation. Privacy manifest
  (`PrivacyInfo.xcprivacy`) is Apple-required since iOS 17 for new submissions
  — Phase 1 should include it. `BGTaskScheduler` lane isolation from
  `URLSessionConfiguration.background` is sound but unstated; clarify in Phase 4.
  iOS 19 (which ships fall 2026) introduces stricter Background-Tasks budgets
  for new app submissions; Phase 7 TestFlight build should target the iOS-18
  baseline at minimum and verify on iOS 19 beta if available." Drives A6 + A8.
- **Repo architecture / cross-language reviewer:** "Cross-language canonicalization
  test (Phase 3) is the highest-leverage thing this hand-off ships AND the
  highest-risk because the .NET-side anchor doesn't exist as cited.
  `Sunfish.Foundation.Canonicalization.JsonCanonical` is vapourware on
  origin/main — `git ls-tree -r origin/main \| grep Canonicalization` returns
  zero matches in `Sunfish.Foundation.Canonicalization.*`. The actual symbols
  (`CanonicalJson` in foundation-crypto, `JsonCanonicalizer` in foundation-assets,
  `JsonCanonicalCanonicalizer` in kernel-signatures) are explicitly NOT RFC 8785
  conformant — `JsonCanonicalCanonicalizer.cs` self-documents this in the XML
  remarks. ADR 0054 A1's promised `packages/foundation-canonicalization/` is
  unchecked. ADR 0028-A2.10's table claiming the symbol is verified-existing is
  itself a false-positive verification — repeats the exact failure mode A3
  retracted in the negative direction. The `accelerators/anchor-mobile-ios/`
  paths are consistent with ADR 0048-A1's `accelerators/` zone framing — that
  much is right. But `apps/bridge/` paths are wrong-tree (Bridge is at
  `accelerators/bridge/`); this surface is consistent enough through the
  hand-off (4 occurrences) that it's a systematic mis-citation, not a typo." Drives A1 + A2 + A4.
- **Pairing flow / security reviewer:** "Phase 0 stub framing is sound for the
  named scope (per-device install identity + pairing-token-HMAC purpose label).
  The W#19 Phase 3 / W#21 Phase 0 stub precedents are the right model; surface
  is genuinely small enough to ship inline. `kSecAttrAccessibleAfterFirstUnlock`
  + `kSecAttrSynchronizable=false` is the correct Keychain policy for the
  threat model (background URLSession needs post-first-unlock access; no
  iCloud-Keychain-sync because cross-device pairing is explicit, not implicit).
  Phase 5's 8-character alphanumeric pairing code at typical alphabets (A-Z + 0-9
  excluding ambiguous chars: ~30 chars) gives ~30^8 ≈ 6.5×10^11 keyspace; sufficient
  if the code is short-TTL (≤5 min) and rate-limited at the Bridge endpoint
  (≤10 attempts before lockout). Hand-off does not name TTL or rate-limit — addendum
  should pin both. QR code is unnecessary for the in-person pairing UX and adds
  iOS-camera-permission entitlement complexity; oral / typed code is right.
  Threat model if intercepted: limited if TTL ≤5min + rate-limit + bound to
  HMAC-derived `device_id`. Phase 0 'Anchor side cannot reference
  `Sunfish.Foundation.Recovery`' halt is real and worth keeping. SQLCipher key
  derivation is single-device-bound with no recovery path — substantive gap
  the cohort hasn't paid attention to yet." Drives A5 + (sound-but-unstated TTL/rate-limit refinement).
- **Cohort discipline / cited-symbol reviewer:** "Three negative-existence /
  positive-existence claims need spot-checking. (1) `Sunfish.Foundation.Canonicalization.JsonCanonical`
  — claimed verified-existing per ADR 0028-A2.10 but is not (`git ls-tree`
  returns zero matches in the Canonicalization namespace; ADR 0054 A1 promises
  but doesn't deliver). (2) `VendorMagicLink` — claimed precedent for HMAC
  purpose-label pattern but does not exist on origin/main. Real precedent is
  W#20 `thread-token-hmac` which IS verified existing per the
  ITenantKeyProvider XML doc. (3) `apps/bridge/` — claimed Bridge path but
  Bridge lives at `accelerators/bridge/`. Spot-checks completed before flagging.
  This makes the cohort false-claim count 2-of-9; one false-negative (ADR 0061
  retraction A3) + one false-positive (this hand-off's `JsonCanonical`
  inheriting from ADR 0028-A2.10). XO should treat the ADR 0028-A2.10 line as
  a follow-up retroactive correction (same shape as A3's correction of A2.4)." Drives A1 + A2 + A3 + retroactive correction note.

---

## 9. Recommended addendum scope

The §5 amendments fit cleanly into a single addendum PR matching the
`property-signatures-stage06-addendum.md` / `property-work-orders-stage06-addendum.md`
shape:

- **Filename:** `icm/_state/handoffs/property-ios-field-app-stage06-addendum.md`
- **PR title:** `chore(icm): W#23 hand-off addendum — canonicalizer + Bridge-path + cited-symbol fixes`
- **Scope:** 7 amendments (A1–A7); A8 deferred to first build
- **Effort:** ~1-2h (mechanical XO authoring against the §5 spec; no new design
  judgment)
- **Sequencing:** Author before COB starts Phase 3. Phase 0–2 are unblocked.
- **Companion mechanical fix:** add a one-line retroactive correction note to
  ADR 0028-A2.10's verified-symbols table — strike the
  `Sunfish.Foundation.Canonicalization.JsonCanonical` row OR replace with
  "verified existing per ADR 0054 A1 implementation checklist (UNCHECKED on
  origin/main as of 2026-04-30; pin to actual existing
  `Sunfish.Foundation.Crypto.CanonicalJson` until A1 implementation lands)."
  This is the same retroactive-correction shape ADR 0028-A3 used for A2.4.

---

## 10. References

- **Hand-off under review:** [`property-ios-field-app-stage06-handoff.md`](../../_state/handoffs/property-ios-field-app-stage06-handoff.md)
- **Cohort precedents:**
  [`property-work-orders-stage06-addendum.md`](../../_state/handoffs/property-work-orders-stage06-addendum.md) (W#19 Phase 3 stub),
  [`property-signatures-stage06-addendum.md`](../../_state/handoffs/property-signatures-stage06-addendum.md) (W#21 Phase 0 stub),
  [`property-public-listings-stage06-addendum.md`](../../_state/handoffs/property-public-listings-stage06-addendum.md) (W#28 Phase 5 boundary)
- **Substrate ADRs:** ADR 0028 (+ A1+A2+A3); ADR 0048 (+ A1+A2); ADR 0061
  (+ A1–A4); ADR 0046-A1; ADR 0054 (+ A1)
- **False-claim retraction precedent:** ADR 0028-A3 (retracts A2.4's false-
  negative on ADR 0061 vapourware). This council's A1 amendment is the
  positive-existence sibling.
- **Verification commands used (spot-check discipline per
  `feedback_council_can_miss_spot_check_negative_existence`):**
  - `git ls-tree -r origin/main --name-only | grep -iE "JsonCanonical|Canonicalization"`
  - `git ls-tree -r origin/main --name-only | grep -iE "VendorMagicLink|vendor-magic-link"`
  - `git ls-tree origin/main apps/`
  - `git ls-tree -r origin/main accelerators/bridge/Sunfish.Bridge/`
  - `git grep -n "MapPost" -- accelerators/bridge/`
  - `git show origin/main:packages/foundation/Crypto/CanonicalJson.cs`
  - `git show origin/main:packages/kernel-signatures/Canonicalization/JsonCanonicalCanonicalizer.cs`
  - `git show origin/main:docs/adrs/0054-electronic-signature-capture-and-document-binding.md` (Amendment A1 §)
  - `git show origin/main:docs/adrs/0028-crdt-engine-selection.md` (Amendment A2.10 verified-symbols table)
- **Memory notes referenced:**
  `feedback_decision_discipline` Rule 6 (verify cited Sunfish.* symbols),
  `feedback_council_can_miss_spot_check_negative_existence` (spot-check council
  claims, especially negative-existence)
