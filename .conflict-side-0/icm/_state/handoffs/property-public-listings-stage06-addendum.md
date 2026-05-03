# W#28 Public Listings — Stage 06 Hand-off Addendum (Phase 5 Boundary Update)

**Addendum date:** 2026-04-29
**Resolves:** ADR 0059 amendment A1 (boundary contract reconciliation per council review; PR #296)
**Original hand-off:** [`property-public-listings-stage06-handoff.md`](./property-public-listings-stage06-handoff.md)
**Workstream:** #28

---

## What this addendum does

ADR 0059's council review found that the local `InquirySubmission` record didn't match ADR 0057's canonical `IPublicInquiryService.SubmitInquiryAsync(PublicInquiryRequest, AnonymousCapability, ct)` boundary contract. Amendment A1 (PR #296) **deleted the local `InquirySubmission` record** + replaced it with a controller-boundary primitive `PublicInquiryFormPost` DTO that the Bridge route maps to ADR 0057's canonical `PublicInquiryRequest` at the controller layer.

W#28 hand-off Phase 5 references the deleted `InquirySubmission` shape directly. This addendum updates Phase 5's specification to match the post-amendment contract.

---

## Resolution — Phase 5 boundary contract update

### Original Phase 5 (now obsolete)

Original hand-off Phase 5 said: "After 5 [defense layers] pass: post `InquirySubmission` to ADR 0057's `Inquiry` entity (boundary call into `blocks-property-leasing-pipeline.ILeasingPipelineService.AcceptInquiryAsync(...)`)."

### Updated Phase 5 (post-amendment)

After the 5-layer defense passes:

1. **Bridge route receives `PublicInquiryFormPost` DTO** — route-local primitive defined in `accelerators/bridge/Listings/`. Fields:
   - `string ProspectName`
   - `string ProspectEmail`
   - `string? ProspectPhone`
   - `string MessageBody`
   - `string CaptchaToken`
   - `string ListingSlug`
   - `IPAddress ClientIp`
   - `string UserAgent`

   This is route-local — never crosses the block boundary; defined in `accelerators/bridge/Listings/PublicInquiryFormPost.cs` (or equivalent).

2. **Controller maps to ADR 0057's `PublicInquiryRequest`** — the canonical domain type from `blocks-property-leasing-pipeline`. Mapping:
   - `ProspectName` → `PersonName` (parse first/last; ADR 0057's domain type)
   - `ProspectEmail` → `ContactPoint(ContactPointSystem.Email, ProspectEmail)`
   - `ProspectPhone` → `ContactPoint(ContactPointSystem.Sms, ProspectPhone)` if present
   - `MessageBody` → request body
   - `CaptchaToken` → already verified by 5-layer defense; pass through for audit
   - `ClientIp` + `UserAgent` → `ClientFingerprint` value object per ADR 0057
   - `ListingSlug` → resolve to `PublicListingId` via `IListingRepository`

3. **Anonymous capability minted** by `ICapabilityPromoter` (Phase 4) — token-only, no email-verified yet
4. **`IPublicInquiryService.SubmitInquiryAsync(PublicInquiryRequest, AnonymousCapability, ct)`** is the actual boundary call (per ADR 0057's contract surface; verify via `git grep -E "(class|record|interface) +PublicInquiryRequest|(class|record|interface) +IPublicInquiryService" packages/`)
5. **Returns `InquirySubmissionResult` enum** — distinct from the deleted `InquirySubmission` record. Possible values per ADR 0057 (verify): `Accepted`, `Rejected`, `RequiresFollowUp`, etc.

### Boundary verification step (REQUIRED before Phase 5 starts)

Per the just-codified `feedback_verify_cited_symbols_before_adr_acceptance` rule and the new template's cited-symbol verification step, run:

```bash
# Verify ADR 0057's actual contract surface
git grep -E "interface +IPublicInquiryService" packages/
git grep -E "record +PublicInquiryRequest" packages/
git grep -E "record +AnonymousCapability" packages/
git grep -E "InquirySubmissionResult" packages/

# If any return empty: the canonical types don't yet exist in source; halt Phase 5 with cob-question
```

**Halt-condition added:** if `IPublicInquiryService` or `PublicInquiryRequest` aren't defined in source on origin/main when Phase 5 starts, write `cob-question-*` beacon. The contract is canonical per ADR 0057 + ADR 0059 amendment A1, but if W#22 Leasing Pipeline hand-off Phase 1+2 haven't shipped them yet, Phase 5 is blocked. Likely answer in that case: pivot to Phase 6 (audit emission) which doesn't depend on the boundary contract.

### Updated Phase 5 PR title

**Old:** `feat(accelerators-bridge): listings route family + 5-layer inquiry defense (ADR 0059 + 0043)`

**New:** `feat(accelerators-bridge): listings route + PublicInquiryFormPost → PublicInquiryRequest mapping + 5-layer defense (ADR 0059 A1 + ADR 0057 + ADR 0043)`

### Updated Phase 5 acceptance criteria

- `accelerators/bridge/Listings/PublicInquiryFormPost.cs` exists as route-local primitive (does NOT escape the controller)
- Controller maps `PublicInquiryFormPost` → ADR 0057's `PublicInquiryRequest` correctly (test fixture covers each field mapping)
- 5-layer defense fires before mapping; failures return 200 OK + audit (per ADR 0059 amendment A5)
- Boundary call: `IPublicInquiryService.SubmitInquiryAsync(...)` is the only place ADR 0057's `Inquiry` entity is created
- Returns `InquirySubmissionResult` (enum, not record); UI consumes enum value
- Integration test covers full flow: route POST → 5-layer defense pass → mapping → service call → `InquirySubmissionResult.Accepted` → audit emission

---

## What this addendum does NOT change

- Phases 1, 2, 3, 4 (entity types, IListingRenderer, ICaptchaVerifier, ICapabilityPromoter) — unchanged; can ship in any order before Phase 5
- Phases 6, 7, 8 (audit emission, cross-package wiring, ledger flip) — unchanged
- Total effort estimate (~14–19h)
- The `RedactionPolicy` + `IListingRenderer` enforcement architecture (independent of inquiry boundary)

---

## How sunfish-PM should pick this up

1. **Continue Phases 1-4 normally** if not yet shipped (they don't depend on the boundary update)
2. **Before Phase 5:** run the cited-symbol verification step above
3. **If `IPublicInquiryService` exists on origin/main** (W#22 has shipped Phase 1+2): proceed with the new mapping pattern
4. **If `IPublicInquiryService` does NOT exist:** write `cob-question-*` beacon; pivot to Phase 6 (audit emission) which is independent of the boundary

---

## Related

- ADR 0059 amendment A1 (PR #296): canonical reconciliation
- ADR 0059 amendment A2 (PR #296): package-name `blocks-leasing-pipeline` → `blocks-property-leasing-pipeline` correction (also affects W#28 hand-off cross-references; minor)
- ADR 0059 amendment A6 (PR #296): `IListingRepository` block-`internal` + `RenderedListing<TTier>` projection types make redaction structurally enforceable (improves W#28 Phase 2 acceptance criterion)
- ADR 0057 (Leasing Pipeline + Fair Housing): canonical `PublicInquiryRequest` + `IPublicInquiryService` shapes
- W#22 Leasing Pipeline hand-off: Phases 1-2 ship the canonical types this hand-off consumes
- `feedback_verify_cited_symbols_before_adr_acceptance` user memory: the pattern that surfaced this drift
