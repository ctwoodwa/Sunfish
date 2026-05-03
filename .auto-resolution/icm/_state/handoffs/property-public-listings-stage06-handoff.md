# Workstream #28 — Public Listings — Stage 06 hand-off

**Workstream:** #28 (Public Listings — cluster module)
**Spec:** [ADR 0059](../../docs/adrs/0059-public-listing-surface.md) (Proposed; council review in flight 2026-04-29)
**Pipeline variant:** `sunfish-feature-change` (new block + Bridge route + ADR 0043 addendum)
**Estimated effort:** 12–16 hours focused sunfish-PM time
**Decomposition:** 8 phases shipping as ~6 PRs
**Prerequisites:** ADR 0059 (Proposed; can begin Phase 1 in parallel with council review; see Halt-conditions); ADR 0057 (Accepted); W#22 Leasing Pipeline boundary contract (`Inquiry` entity from Phase 1 of W#22 hand-off); ADR 0032 (macaroons — Accepted); ADR 0031 (Bridge — Accepted)

**Note:** Council review of ADR 0059 is in flight at hand-off authoring time. If council review produces amendments before sunfish-PM reaches that phase, an addendum will follow (mirror W#19 pattern). Phase 1 + 2 (contract surface + entity types) are unlikely to be affected by amendments; sunfish-PM may proceed on those.

---

## Scope summary

Build the public listing surface end-to-end:

1. **`blocks-public-listings` package** — `PublicListing`, `ListingPhotoRef`, `RedactionPolicy`, `ShowingAvailability`, `ProspectCapability` entities + `IListingRepository` + `IListingRenderer` + `ICapabilityPromoter` + `MessagingEntityModule` (typo in ADR — should be `PublicListingsEntityModule`)
2. **Bridge route family** at `/listings*` — server-rendered Razor + JSON-LD + sitemap + robots.txt + OpenGraph
3. **`Foundation.Integrations.Captcha` substrate** — `ICaptchaVerifier` interface + `RecaptchaV3CaptchaVerifier` adapter (per ADR 0013 provider-neutrality)
4. **Inquiry-form 5-layer defense** — CAPTCHA + per-IP rate limit + email/MX check + `IInboundMessageScorer` (from ADR 0052) + manual triage queue
5. **Capability promotion** — Anonymous → Prospect (email-verified macaroon) → Applicant (per ADR 0043 addendum + ADR 0032 macaroons)
6. **Redaction tier enforcement** — `IListingRenderer.RenderForTier` is the only path to render listing data; structurally enforces tier-based access
7. **Audit emission** — 6 new `AuditEventType` constants per ADR 0049

**NOT in scope:** Owner Cockpit listing-CMS (W#29 Owner Cockpit hand-off); MLS cross-posting (Phase 4+); paid promotion (out of scope indefinitely); image-blob CDN strategy (per-tenant config; deferred to Bridge config).

---

## Phases

### Phase 1 — `blocks-public-listings` package + entity types (~3–4h)

Audit-first: confirm `packages/blocks-public-listings/` doesn't exist (per `feedback_audit_existing_blocks_before_handoff`).

Entity types per ADR 0059's contract surface:

```csharp
public sealed record PublicListing
{
    // ... per ADR 0059 §"Initial contract surface"
}

public sealed record ListingPhotoRef
{
    // ... per ADR 0059
}

public sealed record RedactionPolicy
{
    public required AddressRedactionLevel Address { get; init; }
    public required bool IncludeFinancialsForProspect { get; init; }
    public required bool IncludeAssetInventoryForApplicant { get; init; }
    public IReadOnlyDictionary<string, RedactionTier> CustomFieldTiers { get; init; }
}

public enum PublicListingStatus { Draft, Published, Unlisted }
public enum AddressRedactionLevel { NeighborhoodOnly, BlockNumber, FullAddress }
public enum RedactionTier { Anonymous, Prospect, Applicant }
public enum ShowingAvailabilityKind { OpenHouse, ByAppointment, SelfGuidedSmartLock }

public sealed record ShowingAvailability { /* per ADR 0059 */ }
public sealed record ProspectCapability { /* per ADR 0059 */ }

public readonly record struct PublicListingId(Guid Value);
public readonly record struct ListingPhotoRefId(Guid Value);
public readonly record struct ProspectCapabilityId(Guid Value);
```

Plus `IListingRepository` + `InMemoryListingRepository` + `PublicListingsEntityModule` per ADR 0015.

**Gate:** package builds; entity types ship with full XML doc + nullability + `required`; `dotnet build` clean.

**PR title:** `feat(blocks-public-listings): Phase 1 substrate scaffold (ADR 0059)`

### Phase 2 — `IListingRenderer` redaction enforcement (~2–3h)

`IListingRenderer` is the **single chokepoint** for rendering listing data per redaction tier. Implementation must structurally prevent un-redacted data from leaking to lower tiers:

```csharp
public interface IListingRenderer
{
    Task<RenderedListing> RenderForTierAsync(PublicListingId id, RedactionTier tier, CancellationToken ct);
}

public sealed record RenderedListing
{
    public required PublicListingId Id { get; init; }
    public required string DisplayAddress { get; init; }     // tier-redacted
    public required string Headline { get; init; }
    public required string DescriptionMarkdown { get; init; }
    public required IReadOnlyList<ListingPhotoRef> Photos { get; init; } // tier-filtered
    public required Money? AskingRent { get; init; }
    public required RedactionTier ServedAtTier { get; init; }
}
```

Implementation: `DefaultListingRenderer` reads `PublicListing` via repo + `RedactionPolicy` + filters fields per tier. Photos filtered by `MinimumTier`. Address redacted per `AddressRedactionLevel`.

Tests (≥6): anonymous render = neighborhood-only; prospect render = block-number; applicant render = full; photo set filtered correctly; tier-mismatch (prospect token rendering applicant tier) rejected; missing redaction policy → conservative default.

**Gate:** redaction enforcement structural; cannot bypass via direct repository access (callers MUST use `IListingRenderer`).

**PR title:** `feat(blocks-public-listings): IListingRenderer + tier-based redaction (ADR 0059)`

### Phase 3 — `Foundation.Integrations.Captcha` substrate (~1–2h)

New `packages/foundation-integrations/Captcha/` namespace:

```csharp
public interface ICaptchaVerifier
{
    Task<CaptchaVerifyResult> VerifyAsync(string token, IPAddress clientIp, CancellationToken ct);
}

public sealed record CaptchaVerifyResult
{
    public required bool Passed { get; init; }
    public required double Score { get; init; }            // 0.0–1.0; reCAPTCHA v3 style
    public required string Provider { get; init; }
}

public interface ICaptchaProviderConfig
{
    string SiteKey { get; }
    string SecretKey { get; }
    double MinPassingScore { get; }                        // default 0.3 per reCAPTCHA recommendations
}
```

Adapter in `packages/providers-recaptcha/` (new package per ADR 0013):

```csharp
public sealed class RecaptchaV3CaptchaVerifier : ICaptchaVerifier
{
    // Calls Google reCAPTCHA v3 verify API
    // BannedSymbols.txt allows reCAPTCHA imports only in this package
}
```

**Gate:** verifier works against Google reCAPTCHA v3 API in tests (via mock/fixture; no live API calls); provider-neutrality analyzer passes.

**PR title:** `feat(foundation-integrations): ICaptchaVerifier + providers-recaptcha first adapter (ADR 0059)`

### Phase 4 — `ICapabilityPromoter` + macaroon issuance (~2h)

Per ADR 0059 §"Capability promotion (ADR 0043 addendum)":

```csharp
public interface ICapabilityPromoter
{
    Task<ProspectCapability> PromoteToProspectAsync(string verifiedEmail, IPAddress ipAddress, CancellationToken ct);
}
```

Implementation:
- Mint macaroon via ADR 0032 `IMacaroonIssuer` (must exist; verify in Stage 02)
- 7-day TTL default
- Caveats: tenant scope + accessible-listings list + email-verified=true
- Email verification flow: send link with one-time-use token → user clicks → verifier consumes token → macaroon issued

Add `IMacaroonIssuer` stub in `Foundation.Integrations.Capabilities` if not yet shipped (halt-condition).

**Gate:** macaroon issuance works; macaroon verifies on Bridge route; expiration enforced.

**PR title:** `feat(blocks-public-listings): ICapabilityPromoter + Prospect macaroon issuance`

### Phase 5 — Inbound 5-layer defense + Bridge route family (~3–4h)

Implements ADR 0059 §"Inquiry-form abuse posture (ADR 0043 T2 boundary)" + Bridge route surface.

5 layers in order (each fail-closed):
1. **CAPTCHA verify** via `ICaptchaVerifier` from Phase 3
2. **Per-IP rate limit** — sliding window 5/hr per IP, 50/hr per tenant
3. **Email format + MX check** — `MailAddress` parse + DNS MX query
4. **`IInboundMessageScorer`** — reuses interface from W#20 (ADR 0052)
5. **Manual `IUnroutedTriageQueue`** — reuses interface from W#20

After 5 pass: post `InquirySubmission` to ADR 0057's `Inquiry` entity (boundary call into `blocks-property-leasing-pipeline.ILeasingPipelineService.AcceptInquiryAsync(...)`).

Bridge route family in `accelerators/bridge/Listings/`:
- `GET /listings` — index (server-rendered; tenant-scoped)
- `GET /listings/{slug}` — detail (server-rendered + JSON-LD `Apartment` schema + OpenGraph)
- `POST /listings/{slug}/inquiry` — 5-layer defense + post to W#22
- `GET /listings/criteria/{capability-token}` — Prospect tier; criteria doc
- `POST /listings/criteria/{capability-token}/start-application` — promote to Applicant (boundary call into W#22)

Plus `robots.txt` (`Allow: /listings*`) + `sitemap.xml` (lists `Published` slugs).

**Gate:** integration test covers each-layer-rejection; sitemap.xml lists only Published slugs; JSON-LD validates against schema.org `Apartment`.

**PR title:** `feat(accelerators-bridge): listings route family + 5-layer inquiry defense (ADR 0059 + 0043)`

### Phase 6 — Audit emission (~1–2h)

Add 6 new `AuditEventType` constants under `===== ADR 0059 — Public Listings =====` divider:

```csharp
public static readonly AuditEventType PublicListingPublished = new("PublicListingPublished");
public static readonly AuditEventType PublicListingUnlisted = new("PublicListingUnlisted");
public static readonly AuditEventType InquirySubmitted = new("InquirySubmitted");
public static readonly AuditEventType InquiryRejected = new("InquiryRejected");          // CAPTCHA / rate / scoring fail
public static readonly AuditEventType CapabilityPromotedToProspect = new("CapabilityPromotedToProspect");
public static readonly AuditEventType CapabilityPromotedToApplicant = new("CapabilityPromotedToApplicant");
```

Author `PublicListingAuditPayloadFactory` mirroring W#31 + W#19 + W#20 + W#21 + W#18 + W#22 patterns.

**Gate:** 6 event types ship; factory works; audit emission verified.

**PR title:** `feat(blocks-public-listings): audit emission — 6 AuditEventType + factory (ADR 0049)`

### Phase 7 — Cross-package wiring + apps/docs (~1.5h)

- W#22 Leasing Pipeline `ILeasingPipelineService.AcceptInquiryAsync` boundary call works end-to-end
- `IListingRenderer` exposed via Bridge route at correct redaction tier per macaroon caveats
- Email-verification flow integrates with `IMessagingGateway` from W#20 (Phase 4 Postmark)

apps/docs:
- `apps/docs/blocks/public-listings/overview.md`
- `apps/docs/blocks/public-listings/redaction-tiers.md`
- `apps/docs/blocks/public-listings/seo-structured-data.md`

**Gate:** end-to-end from anonymous browse → inquiry submit → email verify → prospect render → applicant promotion works in InMemory mode.

**PR title:** `feat(blocks-public-listings): cross-package wiring + apps/docs`

### Phase 8 — Ledger flip (~0.5h)

Update row #28 → `built`. Append last-updated entry.

---

## Total decomposition

| Phase | Subject | Hours |
|---|---|---|
| 1 | Package scaffold + entity types | 3–4 |
| 2 | IListingRenderer + redaction enforcement | 2–3 |
| 3 | ICaptchaVerifier + providers-recaptcha | 1–2 |
| 4 | ICapabilityPromoter + macaroon issuance | 2 |
| 5 | 5-layer defense + Bridge route family | 3–4 |
| 6 | Audit emission (6 AuditEventType) | 1–2 |
| 7 | Cross-package wiring + apps/docs | 1.5 |
| 8 | Ledger flip | 0.5 |
| **Total** | | **14–19h** |

---

## Halt conditions

- **ADR 0059 council review surfaces amendments** that change the contract surface materially → halt; XO authors W#28 hand-off addendum (mirror W#19 pattern); sunfish-PM resumes against amended spec
- **`IMacaroonIssuer` from Foundation.Integrations.Capabilities** not yet shipped at Phase 4 → write `cob-question-*`; XO may stub the interface ahead of full ADR 0032 Stage 06
- **W#22 Leasing Pipeline `ILeasingPipelineService.AcceptInquiryAsync`** not shipped at Phase 5 → halt; W#22 Phase 5 (public-input boundary) ships this; sequence W#22 Phase 5 BEFORE W#28 Phase 5 OR stub the interface in W#22 Phase 1 + circle back
- **Google reCAPTCHA v3 API surface change** at Phase 3 → unlikely but possible; halt if verify API differs from documented form
- **`IInboundMessageScorer` not yet shipped** by W#20 at Phase 5 → use NullScorer stub; halt only if 5-layer defense becomes test-blocked

---

## Acceptance criteria

- [ ] `packages/blocks-public-listings` ships with full XML doc + nullability + `required`
- [ ] `IListingRenderer` is the only render path; redaction structurally enforced
- [ ] `Foundation.Integrations.Captcha` substrate + `providers-recaptcha` first adapter
- [ ] `ICapabilityPromoter` issues macaroons via ADR 0032; 7-day TTL
- [ ] Bridge route family `/listings*` server-rendered with JSON-LD + sitemap + OpenGraph + robots.txt
- [ ] 5-layer defense fails-closed at every layer; integration test covers each rejection
- [ ] 6 new `AuditEventType` constants in kernel-audit
- [ ] `PublicListingAuditPayloadFactory` ships
- [ ] W#22 boundary call (Inquiry post) works end-to-end
- [ ] `apps/docs/blocks/public-listings/` 3 pages exist
- [ ] All tests pass; build clean
- [ ] Ledger row #28 → `built`

---

## References

- [ADR 0059](../../docs/adrs/0059-public-listing-surface.md) — substrate spec
- [ADR 0031](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) — Bridge hosting model
- [ADR 0032](../../docs/adrs/0032-capability-projection-and-attenuation.md) — macaroons for prospect capability
- [ADR 0043](../../docs/adrs/0043-trust-model-and-threat-delegation.md) — T2-LISTING-INGRESS
- [ADR 0049](../../docs/adrs/0049-audit-trail-substrate.md) — audit emission
- [ADR 0051](../../docs/adrs/0051-foundation-integrations-payments.md) — `Money` for asking rent
- [ADR 0052](../../docs/adrs/0052-bidirectional-messaging-substrate.md) — `IInboundMessageScorer` reused
- [ADR 0057](../../docs/adrs/0057-leasing-pipeline-fair-housing.md) — `Inquiry` boundary contract
- [W#22 Leasing Pipeline hand-off](./property-leasing-pipeline-stage06-handoff.md) — inquiry post target
- [W#20 Messaging hand-off](./property-messaging-substrate-stage06-handoff.md) — `IInboundMessageScorer` + `IUnroutedTriageQueue` providers
