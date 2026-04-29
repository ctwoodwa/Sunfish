# ADR 0058 (Vendor Onboarding Posture) — Council Review

**Reviewer:** research session (adversarial council, UPF Stage 1.5)
**Date:** 2026-04-29
**Subject:** ADR 0058 v. 2026-04-29 (Proposed; cluster #18 EXTEND)
**Companion artifacts read:** ADR text; `packages/blocks-maintenance/Models/Vendor.cs`; `packages/blocks-maintenance/Sunfish.Blocks.Maintenance.csproj`; ADR 0046 (key-loss-recovery); ADR 0052 (bidirectional-messaging-substrate); ADR 0054 (electronic-signatures, post-amendment); ADR 0043 (unified-threat-model); ADR 0051 (foundation-integrations-payments); ADR 0056 (foundation-taxonomy-substrate); `icm/00_intake/output/starter-taxonomies-v1-charters-2026-04-29.md`; `icm/00_intake/output/property-vendors-intake-2026-04-28.md`; cluster reconciliation review.

---

## 1. Verdict

**Accept-with-amendments — not safe to ship as drafted.** The ADR's product framing and direction are right (extend rather than fork; use magic-link instead of vendor-IdP; field-level-encrypt TIN; reuse audit substrate). But the ADR's "no novel primitives" claim is wrong on its face: at least three substrate primitives the ADR cites by name (`EncryptedField`, `IFieldDecryptor`, `IMessagingGateway`, `MagicLinkBody`, plus the `PaymentPreference` enum and a `T2 boundary` reading of ADR 0043) **do not exist at the names used**, and one (the `Vendor` record shape change) is a load-bearing breaking change misclassified as additive. The pre-acceptance audit's self-assessment that "AP-1, AP-3, AP-9, AP-12, AP-21 do not apply" is itself an AP-13 confidence-without-evidence finding. Six required amendments below close the gap; none change Option A or the high-level decision.

---

## 2. Anti-pattern findings

| AP | Severity | Where it fires |
|---|---|---|
| **AP-1 Unvalidated assumption** | **Critical** | OQ-V1 explicitly admits `EncryptedField` + `IFieldDecryptor` may not exist. They don't — `grep` of ADR 0046 returns zero matches for either symbol. The ADR nonetheless treats them as load-bearing typed fields in `W9Document.TinEncrypted` and as a Foundation.Recovery cross-package wiring point. Stage 06 cannot proceed without these types being designed; the ADR does not own that design. |
| **AP-1 Unvalidated assumption** | **Major** | "Existing `Vendor` callers keep working; new fields are additive." Existing `Vendor` is a 7-parameter **positional record**: `Vendor(VendorId Id, string DisplayName, string? ContactName, string? ContactEmail, string? ContactPhone, VendorSpecialty Specialty, VendorStatus Status)`. Adding `required init`-only fields (`OnboardingState`, `Specialties`) is a positional-to-init-only shape change that breaks all existing callers (`new Vendor(id, name, ...)`); replacing `VendorSpecialty Specialty` with `IReadOnlyList<TaxonomyClassification> Specialties` further breaks. This is an api-change-shape modification, not a feature-change-shape modification. |
| **AP-3 Vague success criteria** | Med | "TIN PII discipline structural" is asserted as a positive consequence but the structural mechanism — `IFieldDecryptor` capability check — is admittedly TBD per OQ-V1. Whether the discipline is **structural** (mechanically enforced by the type system or runtime) or **policy-only** (documentation that says "always use IFieldDecryptor") cannot be evaluated until the type exists. Same trap that fired on ADR 0051 PCI scope. |
| **AP-13 Confidence without evidence** | **Major** | Pre-acceptance audit asserts "Confidence Level: MEDIUM-HIGH … None of AP-1, AP-3, AP-9, AP-12, AP-21 apply." All four except AP-9 apply (see other rows of this table). Self-grading "AP-21 does not apply" while citing six ADRs whose contents the ADR does not verify is the textbook AP-13 instance. |
| **AP-18 Unverifiable gate** | Med | "PII protection structural" and "capability-checked + audit-emitting" are asserted as gates but no acceptance criterion is stated. "How do we know structural was achieved?" should be a checklist item. |
| **AP-19 Discovery amnesia / missing tool fallbacks** | **Critical** | The ADR cites types-by-name from six ADRs without verifying any of the names. Inventory: <br>• `EncryptedField` — not in ADR 0046 (zero grep matches). <br>• `IFieldDecryptor` — not in ADR 0046. <br>• `IMessagingGateway` — ADR 0052's actual interface is `IOutboundMessageGateway` (and a separate `IInboundMessageReceiver`). <br>• `MagicLinkBody` template — not named in ADR 0052. <br>• `PaymentPreference` (per ADR 0051) — ADR 0051 defines `PaymentMethodReference`, not `PaymentPreference`. The four-value enum `ACH \| Check \| Zelle \| Other` in this ADR is invented locally. <br>• `SignatureScope` as string `"Sunfish.Signature.Scopes/vendor-w9-acknowledgment"` — ADR 0054 Amendment A7 (already Accepted) says `SignatureScope` is `IReadOnlyList<TaxonomyClassification>`, not a slash-separated string. <br>• `ADR 0043 T2 boundary` — ADR 0043's T-catalog is **CI/CD threats** (T1=compromised maintainer, T2=compromised dependency, T3=subagent, T4=CI action, T5=insider). It does **not** define a vendor-facing trust gradient, nor a "T2 ingress risk" for a public form. The mapping is invented. |
| **AP-21 Assumed facts without sources** | **Critical** | Multiple cross-ADR consumes-from claims (above) are stated without verification. The "industry heuristic; cited in marketing-funnel literature, not load-bearing" parenthetical at least flags one assumption; the cross-ADR claims do not. Same AP-21 pattern that fired on ADR 0051 algorithm-agility. |
| **AP-11 Zombie-projects / no kill criteria** | Low | Several state-machine paths (Suspended → Active, Retired → Active, expired-magic-link reissue) are unaddressed. Implementation could ship without these and later discover gaps. |
| **AP-9 Skipping Stage 0** | N/A | Three options are real and triangulated; Stage 0 was not skipped. |

**Two Critical APs (AP-1 + AP-19/21) and two Major (AP-1 + AP-13).** The Critical pair both flow from the same root cause: cross-ADR types named without verification. This is the same failure mode that surfaced on the ADR 0051, 0053, and 0054 council reviews — Stage 1.5 is catching it again.

---

## 3. Top 3 risks

1. **`EncryptedField` + `IFieldDecryptor` do not exist; the ADR is gating TIN handling on phantom types (highest impact, AP-1 + AP-19, surfaced by Skeptical Implementer + Pessimistic Risk Assessor).** ADR 0046 covers per-tenant DEK derivation in service of the **key-loss recovery scheme** — the historical-keys projection for SignatureEvent verification — not field-level encryption of structured records. The `Foundation.Recovery` package is a candidate home, but neither the `EncryptedField` value type, the `IFieldDecryptor` capability-checked decryption interface, nor the audit-emitting decrypt-on-read pattern are designed. OQ-V1 acknowledges the gap but treats it as a Stage 02 lookup; in fact it requires its own Phase 0 ADR or a substantial extension to ADR 0046. Without it, Stage 06 will discover that the TIN field has no encryption layer and either (a) ship plaintext-at-rest TIN behind a TODO (catastrophic regulatory + reputational risk) or (b) halt and re-open the ADR. **Impact:** TIN PII discipline is the ADR's #1 stated quality bar; the substrate to enforce it isn't designed.

2. **`Vendor` record shape change is breaking, not additive (Pessimistic Risk Assessor + Pedantic Lawyer).** The existing `Vendor` is a positional record with seven parameters. Existing call sites (`InMemoryMaintenanceService` + `WorkOrder.Vendor` per ADR 0053 + tests + likely Anchor wiring) use positional construction. Switching to init-only with `required` fields breaks every constructor invocation; renaming `Specialty` (singular enum) to `Specialties` (taxonomy list) breaks every accessor. The ADR labels this as "feature-change" pipeline variant but a positional → init-only conversion + an enum-to-list field is the canonical api-change-shape signature. **Mirror to ADR 0053**: the same trap fired on ADR 0053's `WorkOrder` record migration (where this council review forced amendment A6 to relabel as api-change). The cluster's "EXTEND" reframing has now created the same hazard on Vendor. **Impact:** wrong pipeline variant → review rigor mismatch; Stage 06 hand-off undersized; downstream work (W#19 Work Orders, kitchen-sink demos, Anchor wiring) will hit compile breaks.

3. **ADR 0043 trust-tier mapping is invented, not "addendum" (Pedantic Lawyer + Outside Observer).** ADR 0043's T1–T5 catalog is the CI/CD chain-of-permissiveness threat model — compromised maintainer credentials, dependency supply chain, subagent regression, CI-action compromise, insider. It is **not** a public-facing identity/auth tier system, and it has no concept of a "Bridge T2 ingress boundary." The capability gradient (Anonymous → Vendor → Vendor-with-portal) is a real and reasonable design, but framing it as "ADR 0043 trust-model gets a concrete vendor-tier specification" is a category error. An outside reader pulling ADR 0043 to understand "T2 boundary" finds CI/CD content that has nothing to do with vendor magic-links. **Impact:** trust narrative incoherence; downstream ADRs that try to build on this trust framing will inherit the confusion; future security review will reject "we covered this per ADR 0043."

**Risks named but not in top-3:**
- `IMessagingGateway` / `MagicLinkBody` API names are wrong (real names: `IOutboundMessageGateway`, no `MagicLinkBody`)
- `SignatureScope` value is shown as a slash-separated string but ADR 0054 A7 says it's a `TaxonomyClassification` reference
- `PaymentPreference` enum is invented locally and ascribed to ADR 0051 which doesn't define it
- `PostalAddress` lives in `blocks-properties`, creating a new cross-package dep from `blocks-maintenance` not flagged in §Affected packages
- 14-day TTL for `VendorMagicLink` is asserted without rationale — ADR 0052 ThreadToken pattern uses 90-day TTL; the ADR diverges with no justification
- TIN retention 4-year is OQ-V5 deferred; should be in scope since the ADR ships `Retired` state
- No FAILED conditions / kill triggers for the substrate itself (only revisit triggers for follow-on features)
- Magic-link rate limiting (anti-spray) is not specified; an attacker who guesses tokens can attempt many

---

## 4. Top 3 strengths

1. **Option triangulation is real and well-rejected.** Option B (new `blocks-vendors` package) is rejected with a coherent reframing rationale; Option C (dynamic-forms substrate) is rejected with a substrate-not-yet-shipped argument plus a domain-fit argument (W-9 is a federal pinned form, not a per-tenant variable form). The Option C rejection in particular is sharp — recognizing that JSONB dynamic-form data resists field-level encryption + capability-audited reads is exactly the right architectural objection.

2. **Magic-link onboarding flow is correctly framed as anonymous capability holder, not identity-bearing token.** Steps 1–6 of the onboarding flow are operationally specific and traceable to existing substrates. The "vendor proves possession of the link, not identity to a Sunfish IdP" framing is the right paper-§ alignment for Bridge as Zone-C public-input boundary. Step 5's "kernel encrypts TIN under tenant DEK" is right *intent* even though the substrate is missing.

3. **Compatibility plan + revisit triggers are externally observable.** The five revisit triggers (foreign vendor / W-8, marketplace, package split, state-1099, background-check automation) each have observable signal conditions. The "5+ tenants ask for vendor self-service OR a single tenant has 50+ vendors" trigger for Vendor-with-portal is the kind of concrete forcing function that prevents zombie-project drift. Same quality as ADR 0053's revisit triggers.

---

## 5. Required amendments (Accept-with-amendments)

Six required (mandatory before Stage 06); two encouraged.

### A1 (REQUIRED) — Reckon with `EncryptedField` + `IFieldDecryptor` as a design dependency, not a lookup

The ADR currently states "`EncryptedField` is provided by `Foundation.Recovery` per-tenant-key wrapping (ADR 0046)" and OQ-V1 defers verification to Stage 02. Replace this section with: (a) a clear statement that ADR 0046 does not currently expose these types, (b) a decision-shaped subsection — either "promote to ADR 0046-A2 amendment" or "ship as ADR 0058-A1 dedicated companion ADR" or "scope the design into the Stage 06 hand-off as Phase 0 with full type sketches," and (c) a halt-condition: "Stage 06 does not start until A1 lands as Accepted." Diff-shape:

```
  ## Cross-package wiring
- - **`Sunfish.Foundation.Recovery.IFieldDecryptor` (ADR 0046):** TIN decryption — capability-checked + audit-emitting
+ - **`Sunfish.Foundation.Recovery.IFieldDecryptor` (NEW; not yet in ADR 0046; A1 below):** TIN decryption — capability-checked + audit-emitting

+ ### A1 Field-level encryption substrate dependency
+ ADR 0046 does not currently define `EncryptedField` or `IFieldDecryptor`. This ADR takes a hard
+ dependency on both. Resolution path:
+   - Option α: Promote to a follow-up ADR 0046-A2 amendment authored before Stage 06. (RECOMMENDED.)
+   - Option β: Ship the design inline in the Stage 06 hand-off as Phase 0 with a dedicated review.
+ Halt condition: Stage 06 build does not start until the substrate types ship as Accepted.
```

### A2 (REQUIRED) — Re-pipeline the Vendor record shape change as api-change

Existing `Vendor` is a 7-parameter positional record; the ADR's target shape has init-only required fields and a list-typed `Specialties`. This is a positional → init-only conversion plus a field-shape change — two breaking modifications. Diff-shape:

```
- **Pipeline variant:** `sunfish-feature-change` (extends existing `blocks-maintenance`; …)
+ **Pipeline variant:** `sunfish-api-change` for the `Vendor` record shape change; `sunfish-feature-change` for the rest of the surface (new entity types, magic-link flow, Bridge route).

  ### Migration
- `VendorSpecialty` enum → `IReadOnlyList<TaxonomyClassification>` is the only breaking shape change. …
+ Two breaking shape changes:
+   1. `Vendor` record migrates from positional (7 ctor params) to init-only with `required` fields. All existing call sites must be updated.
+   2. `VendorSpecialty Specialty` (single enum) → `IReadOnlyList<TaxonomyClassification> Specialties` (collection of taxonomy refs). All existing accessors (`vendor.Specialty`) break.
+ Migration: emit one-time data conversion at Stage 06; existing enum values map to seeded `Sunfish.Vendor.Specialties@1.0.0` taxonomy nodes 1:1; existing positional callers (`InMemoryMaintenanceService`, tests, Anchor wiring) updated as part of Stage 06 hand-off.
```

### A3 (REQUIRED) — Correct the cross-ADR type references

The ADR uses several names that don't match the source ADRs. Correct each:

- `IMessagingGateway` → `IOutboundMessageGateway` (per ADR 0052 confirmed grep).
- `IMessagingGateway.SendAsync` with `MagicLinkBody` template — drop the template-name claim or open OQ-V6 to define it (ADR 0052 does not name this body type).
- `PaymentPreference?` field with `(per ADR 0051)` annotation → use ADR 0051's actual `PaymentMethodReference` shape, or open OQ-V7 noting that vendor outbound-payout method preference is a NEW type this ADR introduces (not provided by ADR 0051). Specify ownership.
- `SignatureScope = "Sunfish.Signature.Scopes/vendor-w9-acknowledgment"` (string) → `IReadOnlyList<TaxonomyClassification>` referencing the `vendor-w9-acknowledgment` node within `Sunfish.Signature.Scopes@1.0.0` (per ADR 0054 Amendment A7, already Accepted). The ADR must also note that this taxonomy node does not yet exist in the Phase 1 seed and must be added (charter editor change).

### A4 (REQUIRED) — Fix the ADR 0043 trust-model framing

The "Bridge T2 ingress risk" + "ADR 0043 trust-model gets a concrete vendor-tier specification" framings are category errors. ADR 0043's T1–T5 catalog is CI/CD threats. Replace those references with one of:

- (preferred) Frame the capability gradient as a stand-alone vendor-trust gradient in this ADR, citing ADR 0032 (capability-projection-and-attenuation) for the capability semantics and ADR 0028 (per-record-class consistency) for the CP-class state machine. Drop the ADR 0043 references in §Capability gradient and §Trust impact.
- (alternate) Open ADR 0043-A1 to extend the threat catalog with a public-input-boundary tier (T6 Anonymous-capability-holder) that this ADR can reference. Higher-cost; only worth it if other ADRs (Leasing Pipeline, public-listing inquiry) want the same vocabulary.

### A5 (REQUIRED) — Define magic-link TTL + rate-limit divergence from ADR 0052

ADR 0052's ThreadToken pattern uses 90-day TTL. This ADR uses 14-day TTL with no rationale. Either:

- Justify the 14-day choice (likely: tighter window for unconsumed onboarding tokens reduces TIN-collection-form attack surface; reissue path exists via `ReinviteW9` purpose).
- Or align to 90-day unless a security argument for divergence is captured.

Also specify magic-link rate limiting (anti-spray): how many attempts per IP per token before the token is invalidated; what audit emission fires on threshold breach. ADR 0052 may already have this; reference if so.

### A6 (REQUIRED) — Add FAILED conditions / kill triggers + state-machine completeness

Implementation checklist + revisit triggers cover follow-on features but no FAILED condition for the substrate itself. Add three named:

- **FAIL-1.** TIN decryption observed in code path that does not run through `IFieldDecryptor` capability check → halt; emergency ADR amendment.
- **FAIL-2.** `VendorMagicLink` consumed-from-IP audit field shows token consumption from >5 distinct IPs within 24h → revoke + emit security alert; rotate underlying HMAC secret.
- **FAIL-3.** `OnboardingState = Active` reached with `W9 = null` → invariant violation; halt build, fix path.

Plus address state-machine gaps not currently covered: `Suspended → Active` (recovery path), `Retired → Active` (rehire path; should it be allowed?), expired-magic-link reissue (currently `ReinviteW9` purpose exists but flow not specified).

### A7 (ENCOURAGED) — TIN retention policy per OQ-V5 should be in-scope, not deferred

OQ-V5 (4-year IRS retention after Retired) is in scope because the ADR ships `Retired` state. Promote the recommendation in OQ-V5 (4-year retention then crypto-shred) into the Decision section as a concrete policy with a Phase 2.2 implementation hand-off note. Without this, Stage 06 ships `Retired` but no retention enforcement, and the policy gap surfaces in audit later.

### A8 (ENCOURAGED) — Add `PostalAddress` and `ActorId` cross-package dependency notes

`PostalAddress` lives in `blocks-properties` (not foundation); `ActorId` lives in `packages/foundation/Assets/Common/`. Both are now cross-package consumers of `blocks-maintenance`. Update §Affected packages to reflect the new `blocks-properties` dependency for `PostalAddress`, or move `PostalAddress` to a more foundational location (probably already a separate hand-off; reference if so).

---

## 6. Quality rubric grade

**C (Viable), borderline-fail.**

Rationale:

- All 5 CORE sections present (Context, Decision drivers, Considered options, Decision, Consequences). ✓
- Multiple CONDITIONAL sections present (Compatibility plan, Open questions, Revisit triggers, References, Pre-acceptance audit). ✓
- Stage 0 evidence is real (three options triangulated). ✓
- FAILED conditions / kill triggers — **missing** for the substrate itself; only revisit triggers for follow-on features. ✗ (B-grade requirement.)
- Confidence Level stated (MEDIUM-HIGH) — but with AP-1, AP-13, AP-19, AP-21 firing, this is mis-calibrated. Should be LOW-MEDIUM. ✗ (B-grade requirement: stated AND calibrated.)
- Cold Start Test asserted — but a Stage 02 contributor reading "ADR + ADR 0046 + ADR 0052 + ADR 0054 + ADR 0056" will find that `EncryptedField`, `IFieldDecryptor`, `IMessagingGateway`, `MagicLinkBody`, `PaymentPreference`, and `T2 boundary` don't exist at the names cited. Cold Start Test fails on its face. ✗ (A-grade requirement.)
- Reference Library is good — IRS forms cited, sister ADRs cited. ✓
- Replanning triggers explicit (5 named). ✓
- Knowledge Capture present (cluster reframing rationale captured). ✓

**To reach B:** close A1 + A2 + A3 + A4 + A6 (the five hardest required). The remaining (A5 + A7 + A8) are recalibration.

**To reach A:** all required + recalibrate confidence to LOW-MEDIUM until A1 (substrate dependency) lands as Accepted.

The ADR is **viable** but the rate of cross-ADR fact-claims that don't survive verification is too high to ship as-is. The pre-acceptance self-audit explicitly says "AP-1, AP-21 don't apply" while this review finds both Critical — that's the canonical AP-13 signal that the audit was performed without source verification.

---

## 7. Reviewer's bottom line for the CTO

The architectural intent is correct: extend rather than fork; magic-link rather than IdP; field-level-encrypt the TIN; reuse audit substrate. The cluster reframing (NEW → EXTEND) is consistent with prior work. **But the ADR over-claims substrate readiness.** Three of the named consumed-from types do not exist at the names cited (EncryptedField, IFieldDecryptor, IMessagingGateway). One is invented locally and ascribed to a sister ADR (PaymentPreference). One is wrong post-amendment (SignatureScope as string). One reference is a category error (ADR 0043 T-tier mapping). The `Vendor` record shape change is mis-pipelined as feature-change. The TIN-handling structural-vs-policy claim is unverifiable until the substrate exists.

**This is the same failure mode that fired on ADR 0051 (algorithm-agility unverified), ADR 0053 (state-set merge underspecified), and ADR 0054 (signature-scope shape underspecified).** Three for three. The XO is consistently writing ADRs that look complete but cite types that don't exist at the names used. Recommendation to the CTO is **not to accept this ADR until the cross-ADR fact-claims are verified.** A1, A2, A3, A4, A6 must land before Stage 06 starts.

If A1–A6 do not land within ~1 working day, the right move is **Reject and re-propose with a Phase 0 substrate ADR (EncryptedField/IFieldDecryptor) authored first.** The ADR's product framing is good enough that Reject would feel harsh; Accept-with-amendments is the right verdict — but the amendments are load-bearing, not cosmetic.

**Estimated rewrite cost:** 3–5 hours of ADR editing; 0 code changes for the ADR itself; the A1 dependency may itself require 4–8 hours of new ADR authoring (the EncryptedField/IFieldDecryptor design is its own ADR-shape problem).

**Process recommendation for the XO:** before drafting the next cluster ADR, run an explicit "cited-symbol verification pass" — for every type-name and interface-name referenced from a sister ADR, grep the sister ADR for that exact symbol. The seven-symbol miss in this ADR (EncryptedField, IFieldDecryptor, IMessagingGateway, MagicLinkBody, PaymentPreference, SignatureScope-as-string, T2-boundary) is a sign the discipline isn't yet in the ADR-drafting workflow. Adding it as a pre-acceptance audit checkbox would catch the same class of miss for ADRs 0059+.
