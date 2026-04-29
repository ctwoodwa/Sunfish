# ADR 0059 (Public Listing Surface — Bridge-served) — Council Review

**Reviewer:** research session (adversarial council, UPF Stage 1.5)
**Date:** 2026-04-29
**Subject:** ADR 0059 v. 2026-04-29 (greenfield; new substrate composing ADRs 0008/0013/0015/0031/0032/0043/0049/0051/0052/0057)
**Companion artifacts read:** ADR 0059 text (origin/main b730e5f); ADR 0057 text (`Inquiry`/`IPublicInquiryService` boundary contract); ADR 0043 (T-tier catalog convention); ADR 0052 (5-layer defense); origin/main package tree (`packages/blocks-public-listings` does NOT yet exist; `packages/blocks-property-leasing-pipeline` does NOT yet exist; `Foundation.Integrations.Captcha/` does NOT yet exist); workstream ledger rows #22 and #28; W#22 hand-off file `icm/_state/handoffs/property-leasing-pipeline-stage06-handoff.md` (commit e896526, post-dating ADR 0059).

---

## 1. Verdict

**Accept with amendments — the ADR's architecture is sound, but it ships a contract surface that does not match its named integration target (ADR 0057).** The recommended option (new block + Bridge route) is correct; redaction-at-renderer is correct; capability-promotion-as-load-bearing-defense is correct. But three of the seven required amendments below are non-cosmetic — they fix real upstream-downstream mismatches that will break Stage 06 build for both W#22 and W#28 if not reconciled. Two are pure clarity fixes. Two are forward-defense additions.

The ADR earns **B (Solid)**, not A. It explicitly self-grades MEDIUM-HIGH confidence in its pre-acceptance audit, which is calibrated correctly — but the section "Anti-pattern scan: None of AP-1, -3, -9, -12, -21 apply" is wrong on AP-1 and AP-21. Both fire.

---

## 2. Anti-pattern findings

| AP | Severity | Where it fires |
|---|---|---|
| **AP-1 Unvalidated assumption** | **High** | Lines 31, 41, 159–172, 254 ("Inquiry entity (per ADR 0057) consumed via boundary contract"). The ADR assumes ADR 0057's `Inquiry` boundary contract matches the `InquirySubmission` record it defines locally. **It does not.** ADR 0057 specifies `IPublicInquiryService.SubmitInquiryAsync(PublicInquiryRequest, AnonymousCapability, CancellationToken)`; ADR 0059 specifies `InquirySubmission` with primitive `string ProspectEmail`, `string ProspectName`, `IPAddress ClientIp`, `string CaptchaToken`. ADR 0057 uses domain types — `ContactPoint` (FHIR Pattern G), `PersonName`, `CaptchaToken`, `ClientFingerprint`. The two contracts are not assignment-compatible. Stage 06 will discover this. |
| **AP-1 Unvalidated assumption (sub-2)** | **High** | Line 257 references `packages/blocks-leasing-pipeline` as the consumer. ADR 0057's package name is `packages/blocks-property-leasing-pipeline` (cluster sibling convention `blocks-property-*` per `project_property_ops_cluster_2026_04_28` memory). The package name in 0059's affected-packages table is wrong. |
| **AP-3 Vague success criteria** | Med | Implementation checklist line 277 enumerates 6 audit-event constants; ADR 0057 enumerates 12 lifecycle audit records. Some overlap (`InquiryReceived` vs `InquirySubmitted`); some don't (`PublicListingPublished`). No reconciliation table. Stage 06 implementer cannot tell which ADR owns which audit event. |
| **AP-9 First-idea-unchallenged** | Low | Three options triangulated (new block / inline / static). Survives. |
| **AP-13 Confidence without evidence** | Med | Line 192 specifies "reCAPTCHA v3 score < 0.3 reject" without citing where the 0.3 threshold comes from. Google's own guidance is "experiment per site" — the 0.3 default is a folklore default, not a Google-published recommendation. Either cite a source or note it's a Phase 2.1 starting heuristic to be tuned. |
| **AP-13 (sub-2)** | Med | Line 238 specifies `Cache-Control: public, max-age=300, stale-while-revalidate=3600`. For an **encrypted blob decrypted per-request** that is then served `public`, the cache will store the *decrypted bytes* at every CDN edge. That's a privacy regression vs the "tenant-key encrypted at rest" claim three lines earlier. The combined posture is internally inconsistent. |
| **AP-18 Unverifiable gate** | Med | Line 211 says "Tests verify: anonymous render contains neighborhood-only address; prospect render contains block-number address; applicant render contains full address." That tests three positive cases. It does not test the **negative** cases (anonymous render does NOT contain block number; prospect render does NOT contain full address; applicant render does NOT contain non-applicant fields). Negative-test discipline is exactly the load-bearing assertion for a redaction-enforcement chokepoint. |
| **AP-19 Missing tool fallbacks** | Low | CAPTCHA service unavailable failure mode is not specified. If reCAPTCHA returns 5xx or times out, does the inquiry form fail-open (accept) or fail-closed (reject + show user-facing error + audit-log)? The 5-layer defense list does not name this. For a public-input boundary, fail-closed is the right default — but the ADR doesn't say. |
| **AP-19 (sub-2)** | Med | The 5-layer defense (lines 191–196) does not specify which layers fail-closed and which fail-open. Layer 1 (CAPTCHA): assumed fail-closed but unspecified. Layer 2 (rate limit): assumed fail-closed but unspecified. Layer 3 (MX check): if DNS lookup fails, what happens? Layer 4 (`IInboundMessageScorer`): per ADR 0052 it should fall through to `NullScorer` if pluggable not provided — fine. Layer 5 (manual triage): is the *queue* itself rate-limited? If 10000 fake inquiries pass layers 1–4 they all flood the triage queue (a DoS by exhaustion). |
| **AP-21 Assumed facts without sources** | **High** | Line 20 + 185 + 331: "JSON-LD `Place`/`Apartment` schema" / "JSON-LD `Apartment`/`Place` schema" / Schema.org reference cites `Apartment`/`Place`/`RealEstateListing`. These three schema.org types have **distinct semantics**: `Place` is the supertype; `Apartment` is a subtype of `Accommodation` (the *unit being offered*); `RealEstateListing` is a subtype of `WebPage` (the *page about the listing*). Best practice for a listing page is `RealEstateListing` (with `datePosted`, `leaseLength`) **wrapping** an `Apartment`/`SingleFamilyResidence` accommodation. The ADR doesn't pick one as primary or specify nesting. A Stage 06 implementer reading "JSON-LD `Apartment`/`Place` schema" may emit the wrong primary type and lose the `datePosted`/`leaseLength` fields that drive Google's listing-rich-result eligibility. |
| **AP-21 (sub-2)** | Med | Line 192 specifies reCAPTCHA threshold 0.3 (see AP-13 above). Same root: assumed-fact without source. |

**Critical-path APs:** AP-1 fires twice (boundary contract mismatch + package name mismatch); AP-21 fires twice (schema.org semantics + CAPTCHA threshold). Both AP-1 sub-findings will block Stage 06 build. AP-21 is a quality/SEO-correctness issue that won't block build but will degrade the *primary justification* for choosing SSR (organic discovery via structured data).

The ADR's own "Pre-acceptance audit" line 343 says "None of AP-1, -3, -9, -12, -21 apply." Three of those five fire on real reading.

---

## 3. Top 3 risks

1. **Boundary contract drift between ADR 0059 and ADR 0057 (highest impact).** ADR 0057 defines the inquiry submission as `IPublicInquiryService.SubmitInquiryAsync(PublicInquiryRequest, AnonymousCapability, ct) → InquirySubmissionResult` where `PublicInquiryRequest` uses domain types (`ContactPoint`, `PersonName`, `CaptchaToken`, `ClientFingerprint`). ADR 0059 defines `InquirySubmission` with primitives (`string ProspectEmail`, `string ProspectName`, `string CaptchaToken`, `IPAddress ClientIp`, `string UserAgent`). **The Bridge route's `POST /listings/{slug}/inquiry` hands a payload to a service whose signature it does not satisfy.** Type-name collision additionally makes it look like the contract is wired ("InquirySubmission" → "InquirySubmissionResult") when in fact one is a fresh ADR-0059 type and the other is the ADR-0057 enum. **Stage 06 W#28 implementer will either (a) fork the contract and create a translation layer, (b) re-litigate one or both ADRs, or (c) ship two parallel contract surfaces.** Resolution options: (i) ADR 0059 deletes its `InquirySubmission` record and consumes ADR 0057's `PublicInquiryRequest` directly, with the Bridge route doing primitive→domain mapping at the controller boundary; (ii) ADR 0057 amends `IPublicInquiryService` to accept ADR 0059's primitive shape and do the lifting itself; (iii) introduce a dedicated DTO layer in `accelerators/bridge` with explicit mapping. Option (i) is cleanest — Bridge is the trust boundary; primitive-to-domain mapping at the controller is the right place. **Impact if unresolved: re-opening one of the two ADRs mid-build; one or two days of churn; the other 6 cluster ADRs that consume the leasing pipeline are unaffected so the blast radius is contained but real.**

2. **Redaction-enforcement single-chokepoint claim is structurally weaker than the ADR claims (second-highest impact).** Line 209 + 231: "renderer enforces redaction at the block boundary so callers can't accidentally leak." This is true **if and only if** every consumer goes through `IListingRenderer.RenderForTier()`. But the ADR also exposes `IListingRepository` (line 271) and `PublicListing` (line 83) as an init-only record with all fields populated regardless of tier. A sloppy `accelerators/bridge` Razor page that injects `IListingRepository` and reads `.AskingRent`/`.Photos`/`.Redaction` directly bypasses the renderer entirely. **The structural claim "data leak is structurally impossible" is overstated; the actual claim is "the recommended path is the renderer, but other paths exist."** This is the difference between a capability-bound type (where the bytes don't exist outside the renderer) and a discipline-bound type (where the bytes always exist; you're trusted to use the renderer). The ADR ships the latter and claims the former. Real fix would be: (a) make `PublicListing` internal to the block; (b) only `RenderedListing<TTier>` types cross the package boundary; (c) `IListingRepository` returns those tier-bound types, not `PublicListing` directly; OR (a) make `RedactionPolicy` a default; (b) `IListingRepository` requires a `RedactionTier` argument and returns a tier-redacted projection. Either way, currently the renderer is *one path* among multiple, not *the* path. **Impact: the threat-model claim that "anonymous browsing is structurally limited to published + redacted content" needs an asterisk; ADR 0043's T2-LISTING-INGRESS tier should explicitly note that non-Bridge consumers (kitchen-sink demos, future Anchor operator views, kernel-audit scrapers) need their own redaction discipline.**

3. **`RedactionPolicy.CustomFieldTiers` is `IReadOnlyDictionary<string, RedactionTier>` — string-keyed, type-unsafe, typo-vulnerable (third-highest impact).** Line 119: a typo in a field name silently disables redaction for that field. There is no compile-time check, no analyzer, no test fixture that enumerates Property fields. If the field name string `"Bedrooms"` is misspelled `"BedRooms"` in a tenant's listing config, the substrate accepts the typo, fails to look up the field, and falls through to default tier (which appears to be Anonymous from line 119's `ImmutableDictionary<string, RedactionTier>.Empty` default). For a redaction substrate, fail-open on typo is the worst-case posture: it silently leaks data. Three real fixes: (a) replace `string` keys with a generated enum or sealed class enumerated from the `Property` entity's public surface; (b) require a custom-field-tier value for every tenant at registration time, with a kernel-side validator that rejects unknown keys at startup; (c) keep the string keys but invert the default to Applicant tier (most-restrictive by default), so a typo means "field is never shown" rather than "field is always shown." Option (c) is the smallest fix and the most-correct fail-closed posture. **Impact: when a Phase 2.1 tenant adds a custom property field and configures its redaction tier, a typo silently leaks the field. This is exactly the failure mode the ADR's Section 5 "Trust impact" claims is structurally prevented.**

---

## 4. Top 3 strengths

1. **Capability promotion as the load-bearing defense (not CAPTCHA) is exactly right.** Line 32 explicitly names this: "CAPTCHA filters the bottom 90% so human review only sees the top 10%; capability promotion (email-verified prospect tier) is the load-bearing defense." This is the right threat-model framing — pure anti-spam (CAPTCHA + rate limit) fails against motivated abuse; capability-progression with a delay+verify gate breaks the abuse economic model because each fake prospect costs the attacker an email-verify cycle. The macaroon-bound prospect tier (per ADR 0032) is the right primitive. **This is the single best architectural decision in the ADR.**

2. **Bridge owns the route + Anchor doesn't carry rendering code is structurally faithful to ADR 0031 / 0032 (Zone-A vs Zone-C separation).** Line 24's "Anchor (Zone A local-first node) is operator-only; it doesn't serve public pages" is a clean derivation from the local-node-architecture-paper §20.7. The listing surface is Bridge's natural concern (Zone-C public-input boundary); making it part of `accelerators/bridge` rather than `packages/blocks-public-listings` would have been wrong (paper §20.7 says Bridge is hosted-node-as-SaaS with per-tenant data-plane isolation; the listing block is the data-plane artifact, the Bridge route is the Zone-C surface). The ADR gets the carve right.

3. **Provider-neutrality is enforced at the right boundary (`ICaptchaVerifier` adapter pattern).** Line 274 + 229: `ICaptchaVerifier` interface in `Foundation.Integrations.Captcha/` with `RecaptchaV3CaptchaVerifier` as the Phase 2.1 concrete adapter. This matches ADR 0013 vendor-neutrality posture and the existing `Foundation.Integrations.Payments/` / `Foundation.Integrations.Messaging/` patterns. A future hCaptcha or Cloudflare Turnstile adapter slots in without ADR re-litigation. The OQ-L1 question ("reCAPTCHA v3 vs hCaptcha as Phase 2.1 default") is pre-resolved by this pattern — the substrate doesn't care; tenants choose at config time.

---

## 5. Required amendments (Accept-with-amendments)

### A1 — Reconcile the Inquiry boundary contract with ADR 0057 (HIGH; load-bearing for Stage 06 build)

ADR 0059's `InquirySubmission` record (lines 162–172) and ADR 0057's `IPublicInquiryService.SubmitInquiryAsync(PublicInquiryRequest, AnonymousCapability, ct)` boundary do not match. **Pick one path:**

- **Recommended (A1.opt-i):** Delete ADR 0059's `InquirySubmission` record. The Bridge route at `POST /listings/{slug}/inquiry` accepts a primitive Razor form-post DTO (which can stay defined in `accelerators/bridge` as a route-local type, NOT in `blocks-public-listings`), maps it to ADR 0057's `PublicInquiryRequest` at the controller boundary, calls `IPublicInquiryService.SubmitInquiryAsync(...)`, and returns the `InquirySubmissionResult` enum result. No new contract surface in `blocks-public-listings` for inquiry; the boundary belongs entirely to ADR 0057.
- **Alternative (A1.opt-ii):** Amend ADR 0057's `IPublicInquiryService` to accept ADR 0059's primitive shape and do domain-type lifting itself. (Not recommended; pushes the trust boundary inward.)

If A1.opt-i is chosen, the ADR also needs to be edited to remove the `InquirySubmission` record from its "Initial contract surface" code block, and the affected-packages table line 257 corrected to `packages/blocks-property-leasing-pipeline` (not `blocks-leasing-pipeline`).

### A2 — Correct the affected-package name (HIGH; tied to A1)

Line 257: `packages/blocks-leasing-pipeline` → `packages/blocks-property-leasing-pipeline`. Cluster sibling convention per the property-ops cluster (memory `project_property_ops_cluster_2026_04_28`); ADR 0057 itself uses the prefixed name on line 96. Trivial textual fix.

### A3 — Reconcile audit-event constants between ADR 0059 and ADR 0057 (MEDIUM)

ADR 0059 line 277 names six audit events: `PublicListingPublished`, `PublicListingUnlisted`, `InquirySubmitted`, `InquiryRejected`, `CapabilityPromotedToProspect`, `CapabilityPromotedToApplicant`. ADR 0057 names twelve lifecycle audit records (per its line 406's audit table — at minimum `InquiryReceived` is there). At least one rename collision (`InquiryReceived` vs `InquirySubmitted`) and overlap on the capability-promotion events.

Add a sub-section to ADR 0059 explicitly listing which ADR owns each audit-event constant. Recommendation: ADR 0057 owns all `Inquiry*` and `Application*` events (including `InquiryReceived` — pick one name globally; recommend ADR 0057's `InquiryReceived` since it ships first chronologically); ADR 0059 owns `PublicListing*` + `CapabilityPromoted*` events. The Bridge route emits the ADR 0057 audit on inquiry submit; ADR 0059 doesn't double-emit.

### A4 — Specify the JSON-LD primary type and nesting (MEDIUM; SEO-correctness)

Lines 20, 185, 331: pick one. Recommended structure (cite this in the ADR):

```json
{
  "@context": "https://schema.org",
  "@type": "RealEstateListing",
  "datePosted": "...",
  "leaseLength": "P12M",
  "mainEntity": {
    "@type": "Apartment",
    "numberOfBedrooms": ...,
    "floorSize": { "@type": "QuantitativeValue", "value": ..., "unitCode": "FTK" },
    "address": { "@type": "PostalAddress", ... }
  }
}
```

`RealEstateListing` is the page-level type (subtype of `WebPage`); `Apartment` is the offered-unit type (subtype of `Accommodation`). Google's listing-rich-result eligibility uses `RealEstateListing.datePosted` and `RealEstateListing.leaseLength`; emitting only `Apartment` loses these fields. For single-family-residence listings, the `mainEntity` becomes `SingleFamilyResidence` (or `House`); for multi-family, `Apartment`.

### A5 — Specify fail-closed/fail-open per layer in the 5-layer defense (MEDIUM)

Lines 191–196 list five layers but don't specify failure-mode posture for each. Add:

| Layer | Failure mode | Rationale |
|---|---|---|
| 1. CAPTCHA verify | **Fail-closed** (reject inquiry; show user-facing "verification temporarily unavailable; please retry"; audit-emit) | Public-input boundary; better to lose a few legitimate submits than admit one abuse round |
| 2. Per-IP rate limit | **Fail-closed** (rate-limiter unavailable → reject + 503 + audit) | Rate limiter is the abuse-DoS defense; without it, layer 5 (triage queue) becomes the DoS target |
| 3. Email + MX check | **Fail-closed if DNS resolves to NXDOMAIN; fail-open if DNS itself unreachable** (DNS-unreachable is a Bridge-host problem, not a submitter problem) | NXDOMAIN is a positive signal of abuse; DNS-unreachable is a transient infra issue and shouldn't block legitimate submits |
| 4. `IInboundMessageScorer` | **Fail-open** (scorer error → fall through to triage queue with `score=null`; per ADR 0052) | Pluggable per tenant; default `NullScorer` accepts; per-ADR-0052 substrate behavior |
| 5. Triage queue | **Bounded with explicit overflow handling** — tenant-config max queue depth (default 1000); on overflow, reject inquiries with audit-emit and operator alert | Otherwise becomes the DoS-by-exhaustion target |

### A6 — Strengthen the redaction-enforcement claim OR weaken its statement (HIGH)

Line 209 + 231: "renderer enforces redaction at the block boundary so callers can't accidentally leak" overstates the current design. Two options:

- **Recommended (A6.opt-i):** Change `IListingRepository` (line 271 in checklist) to require `RedactionTier` as a query parameter and return a tier-bound projection type rather than `PublicListing` directly. Make the un-redacted `PublicListing` record `internal` to the block. This makes the data-leak path structurally closed.
- **Acceptable (A6.opt-ii):** Keep the design as-is but rewrite the claim. "The renderer is the *recommended* path; consumers reading from `IListingRepository` directly are responsible for tier-bound rendering before serving any data to a non-applicant audience. The block ships analyzer/test fixtures to flag direct-repository reads in `accelerators/bridge` Razor pages."

Either choice is fine, but the current "structurally" + open repository contract are inconsistent.

### A7 — Make `RedactionPolicy.CustomFieldTiers` typo-safe OR fail-closed (HIGH)

Line 119: replace open-ended `IReadOnlyDictionary<string, RedactionTier>` with one of:

- **Recommended (A7.opt-i):** Inject a kernel-side validator: at module-registration time (per ADR 0015), `PublicListingsEntityModule` validates that every key in any tenant's `CustomFieldTiers` matches a public field on the `Property` entity (use reflection at startup; fail registration on mismatch). Typo → startup error, not silent leak.
- **Recommended (A7.opt-ii):** Invert default tier from Anonymous to Applicant. A field whose key doesn't match any known property field never renders for anyone (fail-closed on typo).
- **Best (A7.opt-iii):** Combine both — kernel validator at registration, and Applicant-default for the runtime fallback if validator is bypassed.

Optional but encouraged:

### A8 — Cite or qualify the reCAPTCHA threshold (LOW)

Line 192: `score < 0.3` — either cite the source for 0.3 (Google's published guidance is "experiment per site"; common community starting points are 0.5 or 0.3) or note this is a Phase 2.1 starting heuristic and add to OQ-L1.

### A9 — Resolve the Cache-Control vs encrypted-at-rest tension (LOW)

Line 238 + 237: tenant-key encrypted at rest, but served `Cache-Control: public, max-age=300, stale-while-revalidate=3600`. Either:

- For listing photos that are tier-Anonymous, public caching is fine — they're public anyway; the at-rest encryption is for *unpublished* drafts, not *published* photos. Note this distinction explicitly.
- For listing photos that are tier-Prospect or tier-Applicant, change to `Cache-Control: private, max-age=...` and serve via signed URLs that bind to the capability macaroon.

### A10 — Add negative-test discipline to redaction-tier acceptance criteria (LOW)

Line 211: amend the test list to include negative assertions. "Anonymous render does NOT contain block-number address; Anonymous render does NOT contain photos with `MinimumTier ≥ Prospect`; Prospect render does NOT contain photos with `MinimumTier == Applicant`; Applicant render does NOT contain fields whose `CustomFieldTiers` key is unrecognized (per A7)." This closes the "right things visible" + "wrong things invisible" checklist.

---

## 6. Quality rubric grade

**B (Solid).** 

Rationale:

- All 5 CORE sections present (Context, Decision drivers, Considered options, Decision, Consequences). ✓
- Multiple CONDITIONAL sections present (Compatibility plan, Open questions, Revisit triggers, References, Pre-acceptance audit, Trust impact / Security & privacy). ✓
- Stage 0 evidence is real (three options triangulated with explicit rejection rationale; not first-idea). ✓
- FAILED conditions / kill triggers are concrete and externally observable (5 named at lines 298–302). ✓
- Confidence Level stated (MEDIUM-HIGH). Calibrated correctly given the AP-1 + AP-21 findings. ✓
- Cold Start Test stated (line 345: "Stage 02 contributor reading this ADR + ADR 0031 + ADR 0032 + ADR 0052 + ADR 0057 should be able to scaffold without asking for substrate clarification") — but **the boundary contract mismatch with ADR 0057 means that statement is structurally false**. A fresh contributor reading ADR 0059 + ADR 0057 will discover the mismatch within the first hour of Stage 06 build and need to ask. ✗ on cold-start-self-sufficiency.
- Reference Library + Knowledge Capture present (10 ADR refs + 5 external refs). ✓
- Replanning triggers explicit (5 named). ✓

To reach **A**: close A1 (boundary contract reconciled), A2 (package name corrected), A6 (redaction-enforcement claim either strengthened or honestly stated), A7 (typo-safety on `CustomFieldTiers`). A3, A4, A5 are quality-of-life improvements but not A-blockers.

The ADR is closer to A than the score suggests — most of the substrate composition is right; the failure modes are concentrated in a small number of fixable lines.

---

## 7. Reviewer's bottom line for the CTO

ADR 0059 does the right architectural thing (capability promotion as load-bearing defense; redaction-at-renderer; Bridge owns the route; provider-neutrality via `ICaptchaVerifier`). It composes the right substrates (0031, 0032, 0043, 0049, 0051, 0052, 0057). The recommended Option A is correct.

**But it ships a contract surface that does not match its named integration target.** ADR 0057's `IPublicInquiryService.SubmitInquiryAsync(PublicInquiryRequest, ...)` boundary cannot accept ADR 0059's `InquirySubmission` record. A Stage 06 W#28 implementer will discover this within the first hour and either fork the contract, re-litigate, or ship two parallel surfaces. The package-name mismatch (`blocks-leasing-pipeline` vs `blocks-property-leasing-pipeline`) is in the same family of pre-Stage-06 footguns.

**Recommendation: Accept with amendments A1, A2, A6, A7 as required (about 1–2 hours of ADR editing, zero code changes); A3, A4, A5, A8, A9, A10 as encouraged.** All ten amendments are textual; no architectural rework. The Stage 06 W#28 hand-off (which does not yet exist) should reference the amended ADR.

If A1 + A2 do not land before W#28 hand-off authoring begins, the right move is **Reject and re-propose** rather than ship a Stage 06 hand-off built on a contract mismatch. The cost of re-litigation post-Stage-06 is meaningfully higher than the cost of fixing it now.

---

**Counterfactual check:** if ADR 0057 were not yet drafted, ADR 0059's `InquirySubmission` record would be a perfectly reasonable contract on its own. The mismatch is downstream of ADR 0057 being authored (and merged) before ADR 0059 was finalized — a real risk of parallel ADR drafting that the ICM pipeline doesn't structurally prevent. Worth a meta-note for future research-session work: when two ADRs share a boundary, draft them together or have the second cite the first's exact contract surface, not a paraphrase.
