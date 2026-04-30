# ADR 0048 Amendment A1 — Council Review (Stage 1.5 Adversarial)

**Date:** 2026-04-30
**Reviewer:** research session, 4-perspective adversarial council per UPF Stage 1.5
**Amendment under review:** A1 — Mobile-scope clarification (Anchor MAUI iOS vs Field-Capture SwiftUI)
**ADR under review:** [`0048-anchor-multi-backend-maui.md`](../../../docs/adrs/0048-anchor-multi-backend-maui.md) (Status: Accepted; A1 pending merge)
**PR:** #347 — auto-merge intentionally DISABLED for council review per cohort discipline (8-of-8 substrate ADR amendments needed post-acceptance fixes)
**Companion intake:** [`property-ios-field-app-intake-2026-04-28.md`](../../00_intake/output/property-ios-field-app-intake-2026-04-28.md)

---

## 1. Verdict

**Accept with amendments.** Grade: **B (Solid)**.

A1 is a smaller-scope amendment than the prior CRDT/encryption substrate cohort and the architectural shape is correct: Anchor on iPad and the W#23 Field-Capture App are genuinely distinct apps with different UX requirements, and the carve-out is the right resolution to the original ADR 0048 phrasing collision. The MAUI-vs-SwiftUI rationale (A1.2 / A1.3) holds up under technical scrutiny. The amendment is mergeable in roughly its current shape.

Three Major findings and four Minor findings need attention before merge — none require redrafting. One claim (A1.1's invocation of ADR 0032 as the source of an "Anchor on iPad" framing) is unsupported by the cited ADR text. The cohort-precedent citation discipline (A1.5) holds for the ADRs it lists but is silent on a load-bearing claim that needs a citation. A1.7's coexistence "open questions" punt three real iOS-platform integration concerns (URL schemes, push notifications, deep-link routing) that the current OQ list does not name.

With the §3 amendments applied, the grade promotes to **A**.

---

## 2. Anti-pattern findings (21-pattern sweep)

| AP # | Name | Severity | Where it fires |
|---|---|---|---|
| **#21** | Assumed facts without sources | **Major** | A1.1 paragraph 2 asserts "Anchor … the multi-team workspace switching desktop-class app per ADR 0032." ADR 0032 is the multi-team-workspace-switching ADR but contains no "iPad," "tablet-class," or "desktop-class" framing. The claim that Anchor's iOS target is "iPad as a tablet-class app" is the amendment's own design call, not an inheritance from ADR 0032. Either cite a different ADR (none exists), drop the "per ADR 0032" attribution, or add a sentence acknowledging this is A1's framing extension. |
| **#1** | Unvalidated assumptions | **Major** | A1.3 asserts "the native-API surface MAUI poorly maps onto (camera / PencilKit / DataScannerViewController) is NOT Anchor's primary surface." This is true *today* (Anchor v1 is workspace switching). It is not pre-validated for Phase 2 Anchor: receipt photo capture, document scanning for lease attachment, signature capture in Anchor's signing surface (per ADR 0054) all overlap the "rejected for Field-Capture" API set. If Anchor on iPad needs camera-based receipt capture, the carve-out logic that "Field-Capture handles native APIs, Anchor handles workspace UI" stops being clean. Needs an explicit boundary statement or revisit-trigger. |
| **#3** | Vague success criteria | **Major** | A1.1's "two apps coexist on the same iPad if a user wants" is asserted but not specified. What does "coexist" mean operationally? Three iOS-platform concerns are unnamed: (a) URL scheme namespacing (`dev.sunfish.anchor` already claimed in `Sunfish.Anchor.csproj` `<ApplicationId>`; Field-Capture needs a sibling, e.g. `dev.sunfish.field`), (b) push-notification entitlement profile per app, (c) deep-link routing semantics if Field-Capture wants to open Anchor to a specific Inspection. A1.7 asks one Keychain question; the rest of the iOS coexistence surface is silent. |
| **#19** | Discovery amnesia | **Minor** | A1.5 cited-symbol verification covers ADRs 0028 / 0028-A1 / 0032 / W#23 intake / `accelerators/anchor/`. It does NOT verify ADR 0044 (the parent ADR that ADR 0048 extends and that A1 implicitly preserves) or `accelerators/anchor/Sunfish.Anchor.csproj` (which is referenced in ADR 0048 itself and whose `<TargetFrameworks>` carve-out semantics are load-bearing for A1's "Native MAUI for ... iOS ..." reading). |
| **#15** | Premature precision | **Minor** | A1.1's table claims Anchor on iPad will have "full Sunfish kernel, multi-actor delegation surface, payments / messaging / signatures viewing." Anchor on Windows (Phase 1, shipped) is the only Anchor that exists. The iPad target's *exact* feature set is a Phase 2 product call, not an A1 carve-out claim. The table over-specifies. Recommend softening to "Anchor's iPad target inherits Anchor's feature set per Phase 2 product scope (TBD by separate intake)." |
| **#11** | Zombie projects (no kill criteria) | **Minor** | A1 has no revisit triggers of its own. It inherits ADR 0048's, but ADR 0048's triggers are about MAUI Avalonia stabilization, not about the Anchor/Field-Capture coexistence model. If MAUI 11 ships an iOS camera abstraction that closes A1.2's gaps, does the carve-out still hold? If Apple deprecates `URLSessionConfiguration.background` or `DataScannerViewController`, does Field-Capture pivot? No revisit hook exists. |
| **#17** | Delegation without context transfer | **Minor** | A1.1 says W#23 Stage 02 owns the `accelerators/anchor-mobile-ios/` vs `apps/field/` decision (per W#23 intake OQ-I1). A1 implicitly resolves this by naming the path `accelerators/anchor-mobile-ios/` throughout but doesn't formally close OQ-I1. W#23's Stage 02 should know whether A1 is an authoritative resolution or a placeholder — the current text reads as both. |

**Anti-patterns avoided cleanly:** #2 (clear A/B/C tabular contrast in A1.1), #4 (rollback path is "the carve-out remains scoped; future Anchor-on-iPad photo capture is a separate intake"), #5 (Consequences extend past Decision via OQs and revisit trigger inheritance), #10 (first idea — "just use MAUI everywhere" — was challenged by W#23 intake before A1 was authored), #12 (no fantasy timelines; A1 doesn't claim ship dates), #13 (A1.2's MAUI-fidelity claims are stated as time-sensitive, with the AP-1 entry in A1.8 acknowledging "future MAUI releases may close the gap" — confidence appropriately bounded).

---

## 3. Specific amendments (Accept-with-amendments conditions)

### A1' (Required) — Drop or rephrase the "per ADR 0032" iPad framing

A1.1 paragraph 2 currently reads "Anchor … the multi-team workspace switching desktop-class app per ADR 0032. On iOS / iPadOS, Anchor would target iPad as a tablet-class app …". ADR 0032 does not contain the iPad / tablet-class framing — that is an A1 design call. Recommend either: (a) drop the "per ADR 0032" qualifier on the iPad-target sentence and frame it as A1's own scope decision, or (b) add a parenthetical acknowledging this is A1 extending ADR 0032 to a tablet form factor not previously named. Mechanical fix; one-sentence rewrite.

### A2' (Required) — Add "Anchor-on-iPad camera scope" boundary statement

Insert into A1.3 (or as a new A1.3.1): an explicit boundary statement on what native-iOS APIs Anchor on iPad WILL and WILL NOT use. Recommend: "Anchor on iPad uses MAUI's native-API abstractions only for ambient platform integration (file pickers, share sheets, basic photo selection from the photo library). Receipt capture, document scanning, signature capture, and any other camera-driven or PencilKit-driven UX is delegated to the Field-Capture App via the data substrate — Anchor renders the resulting artifacts but does not capture them. If a Phase 2+ Anchor-on-iPad scenario surfaces a hard requirement for native-API camera capture, that scenario triggers a new intake; it does not invalidate A1." This cleanly seals the AP-1 finding by codifying the boundary the carve-out logic relies on.

### A3' (Required) — Specify iOS coexistence surface beyond Keychain

Extend A1.7 OQ-A1.1 (currently Keychain-only) into a 4-item OQ block covering the iOS-platform coexistence concerns:

- **OQ-A1.1 (Keychain access groups)** — current text; keep as-is.
- **OQ-A1.2 (URL scheme namespacing)** — Anchor uses ApplicationId `dev.sunfish.anchor`; Field-Capture proposes `dev.sunfish.field` (sibling under same `dev.sunfish.*` prefix). A1 default: separate ApplicationIds + separate URL schemes; cross-app deep linking deferred to Phase 2.2+ multi-app integration ADR.
- **OQ-A1.3 (push notifications)** — separate APNs entitlements per app; no shared notification surface in Phase 2.1; Field-Capture is offline-first and may not need push at all (per W#23 intake's URLSession.background sync pattern).
- **OQ-A1.4 (deep-link routing between apps)** — out of A1 scope; if Field-Capture's Inspection-detail wants to open Anchor's signing surface, that's a Phase 2.2+ multi-app integration concern.

This converts the AP-3 finding from Major to Resolved with one paragraph of additions.

### A4' (Encouraged) — Add A1.5 entries for ADR 0044 and Sunfish.Anchor.csproj

A1.5 should include:

- ADR 0044 (Anchor ships Windows-only for Phase 1) — verified Accepted on `origin/main`; A1 preserves Phase 1 Win-only scope unchanged.
- `accelerators/anchor/Sunfish.Anchor.csproj` — verified existing on `origin/main`; commented-out `<TargetFrameworks>` lines for `net11.0-android;net11.0-ios` remain valid as the iOS re-enable path per ADR 0048; A1 does NOT require uncommenting these.

The csproj entry is load-bearing because A1.1's "Anchor on iPad … MAUI iOS is the right framework" is dependent on ADR 0048's scaffolding-already-exists claim; the csproj is where that scaffolding lives.

### A5' (Encouraged) — Add A1-specific revisit triggers

Add a "## A1 revisit triggers" subsection (or extend ADR 0048's Revisit triggers list with A1-tagged entries):

- **MAUI 11 closes a meaningful subset of the A1.2 native-API fidelity gaps:** revisit whether Field-Capture's SwiftUI-native rejection of MAUI is still warranted at the same level. (Not an automatic flip; the existing investment in SwiftUI may still win on UX. But the rejection rationale needs re-examination.)
- **Anchor on iPad surfaces a hard native-iOS-API requirement** (e.g., owner wants to capture receipts directly in Anchor without switching to Field-Capture): triggers a new intake to decide whether the boundary statement (A2' above) holds, or whether the carve-out needs to widen.
- **Apple deprecates `DataScannerViewController` / `URLSessionConfiguration.background`** or otherwise invalidates A1.2's load-bearing API list: triggers a Field-Capture architecture review.

### A6' (Encouraged) — Resolve W#23 OQ-I1 explicitly

Add a sentence in A1.1 (or as A1.1.1): "This amendment resolves W#23 intake OQ-I1: the Field-Capture App lives at `accelerators/anchor-mobile-ios/`, NOT `apps/field/`. W#23 Stage 02 inherits the resolved path." This costs nothing, removes ambiguity for W#23's Stage 02 implementer, and follows the W#23 intake's own recommendation (which already favored `accelerators/anchor-mobile-ios/`).

### A7' (Encouraged) — Soften A1.1's Anchor-on-iPad feature-set table row

A1.1's table claims Anchor on iPad has "full Sunfish kernel, multi-actor delegation surface, payments / messaging / signatures viewing." Recommend softening to "Anchor's existing feature set, scoped per Phase 2 iPad product intake (TBD)." This addresses AP-15 without losing the contrast against Field-Capture's narrower feature set.

---

## 4. Top 3 risks (impact-weighted)

1. **The carve-out logic is brittle on the Anchor-on-iPad camera question.** If Anchor on iPad ever needs receipt-capture or PencilKit-signature-capture, the "Anchor = workspace UI, Field-Capture = native APIs" split breaks down. A2' codifies the boundary; without it, the next product decision (Phase 2 Anchor on iPad scope) implicitly relitigates A1. Highest practical impact because it's the most likely scenario to actually fire.

2. **OQ-A1.1 (Keychain access groups) is the right default but needs a Phase 2.2+ ADR pointer for cross-app credential sharing.** The W#23 intake's per-device-pairing-token model (Anchor issues, Field-Capture redeems) implies cross-app communication that A1.7 marks "TBD." If Phase 2.1 ships before that ADR exists, the implementer either makes it up or copies a Keychain pattern from Stack Overflow. Bounded by A3'.

3. **MAUI 10 vs MAUI 11 nomenclature drift.** ADR 0048 says "MAUI 10 preview"; the actual `Sunfish.Anchor.csproj` says "MAUI 11 preview (26.2.11588-net11-p3)" and "MAUI 11 GA"; ADR 0044 references both "MAUI 10" and "MAUI 11." A1 inherits ADR 0048's naming. This is not an A1-specific bug, but it's the kind of substrate inconsistency that makes future amendments hard to reason about. Out of A1 scope; flag as a parent-ADR hygiene item for a separate cleanup PR. **Not a blocker for A1 merge.**

---

## 5. Top 3 strengths

1. **The MAUI-vs-SwiftUI rationale (A1.2 / A1.3) is technically sound for a 2026-04-30 snapshot.** The four cited gaps — camera ergonomics, PencilKit fidelity, `URLSessionConfiguration.background`, PDFKit annotation — are real MAUI limitations as of the current preview. MAUI's native-API surface is improving but does not yet expose `URLSessionConfiguration.background` settings (`discretionary`, `sessionSendsLaunchEvents`) directly, and `DataScannerViewController` (iOS 16+ Vision-backed barcode/text scanning) has no MAUI binding. The rationale is correctly time-bounded by A1.8's AP-1 entry. Good defense against future "why didn't you just use MAUI everywhere" questions.

2. **The carve-out preserves ADR 0048's main decision cleanly.** A1.6 explicitly states "this amendment does NOT change the multi-backend MAUI decision for Anchor." That's correct — A1 is a scope clarification, not an architectural reversal. Native MAUI for Win/Mac/iOS/Android + MAUI Avalonia for Linux/WASM remains the Anchor strategy. The Field-Capture carve-out is additive.

3. **The cited-symbol verification (A1.5) follows cohort discipline.** The amendment explicitly invokes "Decision Discipline Rule 6" and verifies each cited symbol against `origin/main`, including the negative case ("`accelerators/anchor-mobile-ios/` does NOT exist on `origin/main`"). This is the right cohort-lesson posture: 8-of-8 prior substrate amendments needed citation fixes; A1 internalizes the lesson rather than relying on the council to catch it. The §A4' / §A5' findings are gap-fills on top of an already-correct discipline pattern, not replacements for missing discipline.

---

## 6. Quality rubric grade

**Grade: B (Solid).** Rationale:

- **C threshold (Viable):** A1 is a structured amendment with Context (A1 preamble), Decision (A1.1), Rationale (A1.2 / A1.3), Out-of-scope sibling (A1.4), Cited symbols (A1.5), Compatibility (A1.6), Open questions (A1.7), Pre-acceptance audit (A1.8). All ADR-amendment template sections present. No critical anti-patterns of the planning kind fire on A1. Passes C.
- **B threshold (Solid):** Stage 0 sparring is implicit (W#23 intake's "MAUI rejected" rationale + this amendment's three-paragraph "why Anchor keeps MAUI" defense); Confidence Level is implicit but signaled by the AP-1 entry's time-sensitivity acknowledgment; Cold Start Test passes (a fresh implementer can read A1.1's table + A1.2 / A1.3 and ship). Passes B.
- **A threshold (Excellent):** Misses on three counts: (1) the AP-21 finding (the unsupported "per ADR 0032" iPad framing) is the kind of unforced citation error the cohort discipline exists to prevent — A1.5's verification did not catch it because the cite is an attribution, not a symbol reference; (2) the iOS coexistence surface (URL schemes, push, deep-linking) is named only at the Keychain layer, leaving three load-bearing platform concerns un-flagged; (3) no A1-specific revisit triggers — A1 inherits ADR 0048's, but ADR 0048's triggers don't cover the carve-out's failure modes.

A grade of **B** with the §3 Required amendments (A1', A2', A3') applied promotes to **A**. The Encouraged amendments (A4'–A7') are quality-of-life improvements for future-self readability.

---

## 7. Council perspective notes (compressed)

- **MAUI / iOS engineer pragmatist:** "A1.2's gap list is accurate as of MAUI 10/11 preview. `URLSessionConfiguration.background` settings are not exposed through MAUI's HTTP abstraction; `DataScannerViewController` has no MAUI binding; PencilKit pressure-data fidelity through the BlazorWebView boundary is degraded but not eliminated. The PDFKit claim is the weakest — MAUI's PDF support is actually MAU-level limited (read-only viewing), and W#23 Phase 5 *is* doing annotation, so the rationale holds. A1.3's Anchor-on-iPad framing is the soft spot: if Anchor wants iPad-side photo upload, MAUI's `MediaPicker` works fine for *that* (album / camera-roll selection), but the fidelity argument breaks down because Anchor doesn't need fidelity for that case. Drives A2' — the boundary statement makes the carve-out robust against the photo-capture-in-Anchor scenario."

- **Repo architecture reviewer:** "`accelerators/anchor-mobile-ios/` is the right path. The W#23 intake's OQ-I1 already defaulted to this answer; A1 should explicitly close OQ-I1 to remove the ambiguity from W#23's Stage 02. Drives A6'. The path naming is consistent with ADR 0048's accelerator-zone framing (Zone-A / Zone-C; new mobile-iOS accelerator is conceptually Zone-A field-class) — though A1 doesn't explicitly invoke the zone framing, that's fine; the intake does."

- **Coexistence reviewer:** "The 'two apps coexist if a user wants' claim is doing more work than A1 acknowledges. iOS-app coexistence is not free: separate Keychain access groups, URL scheme namespacing, push entitlement profiles, deep-link routing semantics, App Store metadata, and onboarding ('which app do I install first?') are all real surfaces. A1.7 names one (Keychain). The other four are silent. The 'integration via data substrate' claim is load-bearing — it works only if Anchor and Field-Capture genuinely never share UI state and only share through the persisted event stream. That's the right architectural posture and matches ADR 0028-A1+A2's append-only event model, so the load-bearing claim is supportable; it just isn't supported in *A1's text*. Drives A3'."

- **Cohort discipline reviewer:** "A1.5 verification is correctly executed for the symbols it lists. Two gaps: ADR 0044 (the parent ADR that ADR 0048 extends; A1 implicitly preserves Phase 1 Win-only scope and should verify that's still the case on origin/main) and `Sunfish.Anchor.csproj` (the actual code surface where A1's 'Native MAUI for iOS' claim materializes; commented-out `<TargetFrameworks>` lines are the load-bearing scaffolding). Drives A4'. Note: ADR 0023 was floated in the council briefing as a possible Anchor-iPad-target citation, but ADR 0023 is `dialog-provider-slot-methods` — unrelated. Anchor has no prior iPad-target ADR; that's why A1 needs to assert the iPad-as-tablet-class framing as its own design call rather than inherit it (drives A1'). PR #342 (ADR 0028-A1+A2) verified merged 2026-04-30T10:31:33Z; A1.5's claim is accurate."

---

## 8. Merge recommendation

**Path A (preferred):** XO applies the three Required amendments (A1', A2', A3') as mechanical edits to PR #347's branch, re-pushes, and re-enables auto-merge. Encouraged amendments (A4'–A7') can either land in the same fix-up commit or as a follow-up `chore(adr): A1 hygiene` PR — either is acceptable.

**Path B (acceptable):** XO accepts A1 as-is and lands the three Required amendments as a follow-up A2 amendment (e.g., "ADR 0048-A2 — A1 hygiene fixes"). This costs one extra ADR file but preserves PR #347's commit history.

The cohort precedent argues for Path A — every prior substrate amendment in the 8-of-8 cohort applied council fixes inline before merge, and that pattern produced cleaner ADR histories than the "merge then amend" alternative.

**Do NOT reject A1.** The architectural decision is correct; the gaps are mechanical and within-scope-of-amendment.

---

**End of council review.**
