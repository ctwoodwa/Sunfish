---
id: 59
title: Public Listing Surface (Bridge-served)
status: Accepted
date: 2026-04-29
tier: block
concern:
  - threat-model
  - ui
composes:
  - 28
  - 43
  - 49
  - 51
  - 57
extends: []
supersedes: []
superseded_by: null
amendments:
  - A1
  - A3
  - A4
  - A5
  - A6
  - A8
---
# ADR 0059 — Public Listing Surface (Bridge-served)

**Status:** Accepted (2026-04-29 by CO; council-reviewed B-grade; amendments A1–A10 **landed 2026-04-29** — see §"Amendments (post-acceptance, 2026-04-29 council)")
**Date:** 2026-04-29 (Proposed) / 2026-04-29 (Accepted) / 2026-04-29 (A1–A10 landed)
**Author:** XO (research session)
**Pipeline variant:** `sunfish-feature-change` (new block `blocks-public-listings` + Bridge route + ADR 0043 addendum)
**Council review:** [`0059-council-review-2026-04-29.md`](../../icm/07_review/output/adr-audits/0059-council-review-2026-04-29.md) — Accept with amendments. 4 required findings (A1, A2, A6, A7) + 6 encouraged (A3–A5, A8–A10), all addressed below:
1. **A1** — Boundary-contract reconciliation with ADR 0057: delete ADR 0059's `InquirySubmission` record; Bridge route maps a route-local primitive DTO to ADR 0057's `PublicInquiryRequest` at the controller boundary; `IPublicInquiryService.SubmitInquiryAsync(...)` is the single boundary contract. Resolves AP-1 (boundary contract drift) and AP-21 (assumed-fact-without-verification).
2. **A2** — Package-name correction: `packages/blocks-leasing-pipeline` → `packages/blocks-property-leasing-pipeline` per cluster sibling convention (`blocks-property-*`) per ADR 0057 line 75/77/118/493 and `project_property_ops_cluster_2026_04_28` memory. Resolves AP-1 (sub-2).
3. **A6** — Redaction-enforcement: make `PublicListing` block-internal; only tier-bound projection records cross the package boundary; `IListingRepository` requires `RedactionTier` and returns tier-bound projections. The "structurally" claim is now structurally true. Resolves AP-18 (unverifiable gate, redaction).
4. **A7** — `RedactionPolicy.CustomFieldTiers` typo defense: kernel-side validator at `PublicListingsEntityModule` registration rejects unknown keys at startup; runtime fallback for unknown keys is `RedactionTier.Applicant` (most-restrictive / fail-closed). Resolves AP-13 (typo silently leaks data).

**Resolves:** [property-public-listings-intake-2026-04-28.md](../../icm/00_intake/output/property-public-listings-intake-2026-04-28.md); cluster workstream #28.

---

## Context

Phase 2 commercial intake ships rental properties with public-facing listings as a mandatory channel: prospects find the listing on the public web (Google search → property page), submit an inquiry, and that inquiry kicks off the leasing pipeline (per ADR 0057). Today the BDFL uses Rentler.com for this surface; the goal is to move it onto Sunfish so the listing-to-application pipeline is owned end-to-end (data custody, no third-party tracking, no per-listing fees).

This ADR specifies the public-input boundary: the Bridge-hosted public listing pages + the inquiry form that sits at the boundary. The inquiry's downstream lifecycle (Inquiry → Prospect → Applicant) is owned by ADR 0057 Leasing Pipeline. This ADR ships the *surface* — pages, structured data, abuse posture, capability promotion gate.

Cross-cutting constraints:

- **SEO matters.** A listing that doesn't surface in Google is invisible. Pages must be server-rendered, indexed, and structured (JSON-LD `RealEstateListing` page-level type with `mainEntity` of `Apartment` / `SingleFamilyResidence` per Amendment A4) for organic discovery.
- **Privacy by default.** Property fields aren't all public. Street address may be redacted until a prospect commits; vendor relationships, financials, asset inventory all stay private. Listing redaction is structural, not policy.
- **ADR 0043 T2 ingress.** Anonymous form submission is a public-input boundary by definition. The 5-layer defense from ADR 0052 amendment A1 applies, plus CAPTCHA-gating + per-IP rate limiting specifically for the inquiry form.
- **Capability promotion is the access mechanism.** Anonymous (browse listings) → Prospect (inquiry submitted + email verified; can review criteria document) → Applicant (application submitted; per ADR 0057). Each step is gated; promotion is explicit, not implicit.
- **Bridge owns the route.** Anchor (Zone A local-first node) is operator-only; it doesn't serve public pages. The listing surface lives in `accelerators/bridge/` and is one of three canonical Bridge public-input boundaries (along with vendor magic-link onboarding per ADR 0058 and signature-capture pages per ADR 0054).

---

## Decision drivers

- **No existing block covers this.** `blocks-properties` ships the property entity but no public-rendering surface; `blocks-listings` doesn't exist. New block.
- **Inquiry entity ownership is ADR 0057.** This ADR's inquiry form posts INTO the Leasing Pipeline's `Inquiry` entity; it doesn't define one. Cross-ADR boundary stays clean.
- **CAPTCHA + rate limit are not enough.** Pure anti-spam tools fail against motivated abuse (real human submitting hundreds of fake inquiries to harm a competitor). Capability promotion (email-verified prospect tier) is the load-bearing defense; CAPTCHA filters the bottom 90% so human review only sees the top 10%.
- **Redaction ladder, not redaction switch.** Different listing fields surface at different capability tiers. Block-level header (rent + bedrooms + neighborhood) is anonymous; full address is prospect-only; floor plans + photo set is applicant-only.

---

## Considered options

### Option A — New `blocks-public-listings` block + Bridge route [RECOMMENDED]

Standalone block with `PublicListing` entity (FK to `Property` + FK to `PropertyUnit`); Bridge route at `/listings/{listing-slug}` with server-side rendering; inquiry form posts to ADR 0057's `Inquiry` entity via internal API.

- **Pro:** Clean separation — listing-surface concerns don't bleed into property-management concerns
- **Pro:** Bridge route is the only place the surface lives; Anchor doesn't carry the rendering code
- **Pro:** Redaction logic lives at the block boundary; consumers can't leak un-redacted data by accident
- **Con:** New block adds package count; if listing surface stays small, mild over-architecting

**Verdict:** Recommended. The listing surface is genuinely separable; abstracting at block boundary is correct.

### Option B — Inline listing rendering into `blocks-properties`

Add public-rendering methods to existing `blocks-properties` block; no new package.

- **Pro:** No new package
- **Con:** Property entity has substantial private state; mixing public-rendering concerns risks leaking it (the redaction discipline becomes harder to enforce)
- **Con:** Bridge ends up importing `blocks-properties` directly; coupling the consumer-side rendering tier to the entity-storage tier

**Verdict:** Rejected. Conflates entity ownership with surface concerns; risks data leak.

### Option C — Static-site export (Bridge generates static HTML at publish time; no live route)

Listings flip from "draft" to "published" by triggering a static HTML build; pages are CDN-served.

- **Pro:** Maximum performance + minimum runtime cost
- **Pro:** Cacheable + indexable trivially
- **Con:** Inquiry form needs a server endpoint anyway; can't be static-only
- **Con:** Listing edits don't appear until rebuild; UX regression vs server-rendering
- **Con:** Capability promotion (anonymous → prospect) requires server-side state; static can't do it cleanly

**Verdict:** Rejected. Inquiry form + capability promotion both need server runtime; static-export adds complexity without removing server dependency.

---

## Decision

**Adopt Option A.** New `blocks-public-listings` block + Bridge route + ADR 0043 addendum. Server-rendered HTML (Razor + Bridge's existing Blazor Server runtime); JSON-LD structured data; CAPTCHA-gated inquiry form posting to ADR 0057's `Inquiry` entity; capability promotion at the form boundary.

### Initial contract surface

```csharp
namespace Sunfish.Blocks.PublicListings;

public sealed record PublicListing
{
    public required PublicListingId Id { get; init; }
    public required TenantId Tenant { get; init; }
    public required PropertyId Property { get; init; }
    public PropertyUnitId? Unit { get; init; }                          // null for whole-property listing (single-family)
    public required PublicListingStatus Status { get; init; }            // Draft | Published | Unlisted
    public required string Headline { get; init; }                       // "Charming 2-bedroom in West End"
    public required string Description { get; init; }                    // markdown-formatted body
    public required IReadOnlyList<ListingPhotoRef> Photos { get; init; }
    public required Money? AskingRent { get; init; }                     // ADR 0051; nullable while in Draft
    public DateTimeOffset? AvailableFrom { get; init; }
    public required ShowingAvailability ShowingAvailability { get; init; }
    public required RedactionPolicy Redaction { get; init; }             // see below
    public required string Slug { get; init; }                            // URL-safe; tenant-scoped uniqueness; e.g., "123-main-st-2"
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public DateTimeOffset? UnlistedAt { get; init; }
}

public enum PublicListingStatus { Draft, Published, Unlisted }

public sealed record ListingPhotoRef
{
    public required ListingPhotoRefId Id { get; init; }
    public required string BlobRef { get; init; }                        // CDN-served; per-tenant key encrypted at rest
    public required int OrderIndex { get; init; }
    public required string AltText { get; init; }                        // accessibility + SEO
    public required RedactionTier MinimumTier { get; init; }             // Anonymous | Prospect | Applicant
}

public sealed record RedactionPolicy
{
    public required AddressRedactionLevel Address { get; init; }
    public required bool IncludeFinancialsForProspect { get; init; }     // typically false
    public required bool IncludeAssetInventoryForApplicant { get; init; } // typically false; deferred to lease-execution
    public IReadOnlyDictionary<string, RedactionTier> CustomFieldTiers { get; init; } = ImmutableDictionary<string, RedactionTier>.Empty;
}

public enum AddressRedactionLevel
{
    NeighborhoodOnly,    // anonymous tier: "West End" or "Block 1200, Main Street"
    BlockNumber,         // prospect tier: "1200 block of Main St"
    FullAddress,         // applicant tier: "1234 Main St #2"
}

public enum RedactionTier { Anonymous, Prospect, Applicant }

public sealed record ShowingAvailability
{
    public required ShowingAvailabilityKind Kind { get; init; }          // OpenHouse | ByAppointment | Self_GuidedSmartLock
    public IReadOnlyList<DateTimeOffset> OpenHouses { get; init; } = []; // populated when Kind = OpenHouse
    public string? AppointmentLinkOverride { get; init; }                // typically link to ADR 0057's appointment-scheduling
}

public enum ShowingAvailabilityKind { OpenHouse, ByAppointment, SelfGuidedSmartLock }

public readonly record struct PublicListingId(Guid Value);
public readonly record struct ListingPhotoRefId(Guid Value);

// Capability promotion contracts
public interface ICapabilityPromoter
{
    /// <summary>Promote anonymous → prospect after email verification.</summary>
    Task<ProspectCapability> PromoteToProspectAsync(string verifiedEmail, IPAddress ipAddress, CancellationToken ct);
}

public sealed record ProspectCapability
{
    public required ProspectCapabilityId Id { get; init; }
    public required string MacaroonToken { get; init; }                  // ADR 0032 macaroon; short-lived
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }              // typically 7 days
    public required IReadOnlyList<PublicListingId> AccessibleListings { get; init; }
}

public readonly record struct ProspectCapabilityId(Guid Value);

// Inquiry form post is NOT defined in this block. Per Amendment A1, the Bridge
// route's inquiry submission consumes ADR 0057's IPublicInquiryService.SubmitInquiryAsync(
//   PublicInquiryRequest request, AnonymousCapability capability, CancellationToken ct)
// directly. The route-local form-post DTO + primitive→domain mapping live in
// accelerators/bridge and never cross the blocks-public-listings package boundary.
// See §"Amendments (post-acceptance, 2026-04-29 council)" §A1 for the full pattern.
```

### Bridge route surface

| Path | Purpose | Auth tier |
|---|---|---|
| `GET /listings` | All published listings for tenant; sitemap-friendly | Anonymous |
| `GET /listings/{slug}` | Listing detail page (server-rendered, JSON-LD) | Anonymous |
| `POST /listings/{slug}/inquiry` | Inquiry form submission | Anonymous (CAPTCHA + rate-limited) |
| `GET /listings/criteria/{capability-token}` | Criteria-document review (per ADR 0057) | Prospect |
| `POST /listings/criteria/{capability-token}/start-application` | Promote to Applicant | Prospect |

All routes server-render. JSON-LD primary type is `RealEstateListing` with `mainEntity` of `Apartment` (multi-family) or `SingleFamilyResidence` (single-family) per Amendment A4 — `RealEstateListing` carries the `datePosted` + `leaseLength` fields that drive Google's listing-rich-result eligibility. OpenGraph tags for social sharing. `robots.txt` allows indexing of `/listings*`; `sitemap.xml` lists all `Published` slugs.

### Inquiry-form abuse posture (ADR 0043 T2 boundary)

Per ADR 0043 trust-model + ADR 0052 amendment A1's 5-layer defense pattern. The 5-layer defense is the **Bridge controller's** responsibility (executed before invoking `IPublicInquiryService.SubmitInquiryAsync` per Amendment A1); ADR 0057's service does not duplicate the layers. Per Amendment A5, every reject path is **HTTP 200 OK + audit + friendly user-facing message** — no 4xx/5xx leaks abuse signal:

1. **CAPTCHA verify** — reCAPTCHA v3 / hCaptcha; reject score < 0.3 (Phase 2.1 starting heuristic per Amendment A8; tunable per tenant). **Fail-closed** when verifier unreachable.
2. **Per-IP rate limit** — sliding window 5/hr per IP, 50/hr per tenant; exceeded → 200 OK + friendly retry page + audit. **Fail-closed** when rate-limiter store unavailable.
3. **Email format + MX check** — NXDOMAIN: 200 OK + friendly "verify email" + audit. DNS-unreachable: pass through to layer 4 with `MxCheckResult.Skipped` (fail-open on infra issue per A5).
4. **`IInboundMessageScorer` plug-in** — same interface from ADR 0052; tenant-pluggable spam classifier. **Fail-open** (per ADR 0052; default `NullScorer` accepts).
5. **Manual unrouted-triage** — anything ambiguous goes to triage queue per ADR 0052. Bounded queue depth (tenant-config `MaxQueueDepth`, default 1000) per A5; on overflow, 200 OK + audit + operator alert.

Step 4's scorer can be tenant-customized per ADR 0052; default `NullScorer` accepts all that pass 1-3. See §"Amendments (post-acceptance, 2026-04-29 council)" §A5 for the full per-layer fail-closed/fail-open table.

### Capability promotion (ADR 0043 addendum: anonymous → prospect → applicant)

- **Anonymous:** Browse `Published` listings + see `RedactionTier.Anonymous` content. No identity required.
- **Prospect** (on inquiry submit + email verification): receives short-lived macaroon (per ADR 0032); 7-day TTL; can review criteria doc + see `RedactionTier.Prospect` content. Does NOT promote to Applicant automatically.
- **Applicant** (on explicit "start application" action): macaroon promotes; capability now spans inquiry → application; reviewable in cockpit.

ADR 0043 gets a new T-tier entry (`T2-LISTING-INGRESS`) per the same pattern as ADR 0052's `T2-MSG-INGRESS`; concrete defenses delegated to this ADR.

### Redaction enforcement

`RedactionPolicy` is an init-only record on `PublicListing`. The block exposes `IListingRenderer` with a `RenderForTier(PublicListingId id, RedactionTier tier)` method that returns ONLY the fields permitted at that tier. The Bridge route consumes this; renderer enforces redaction at the block boundary so callers can't accidentally leak.

Tests verify: anonymous render contains neighborhood-only address; prospect render contains block-number address; applicant render contains full address. Tests use the redaction-policy-as-test-fixture pattern.

---

## Consequences

### Positive

- Listing surface is structurally separated from property entity (clean redaction enforcement)
- SEO + structured data + OpenGraph for organic discovery
- Capability promotion is the load-bearing defense (CAPTCHA filters abuse but doesn't eliminate it)
- Bridge route is the only public-input boundary for listings (centralized abuse-posture enforcement)
- Inquiry form posts cleanly into ADR 0057 Leasing Pipeline (no overlap with leasing-pipeline ADR's scope)
- Reuses ADR 0052's `IInboundMessageScorer` plug-in surface for spam scoring (no new abstractions)

### Negative

- New block adds package count; could be inlined into `accelerators/bridge` directly if it stays small (revisit trigger)
- CAPTCHA service dependency — Phase 2.1 uses Google reCAPTCHA v3; provider-neutrality posture preserved by abstracting via `ICaptchaVerifier` interface
- Static-export performance benefits foregone; SSR cost is real (mitigated by edge caching at Bridge tier)
- Redaction logic complexity — `IListingRenderer` is the single chokepoint; bug there leaks data

### Trust impact / Security & privacy

- **Anonymous browsing is structurally limited to published + redacted content.** No way to query unpublished listings or unredacted fields from anonymous tier.
- **Inquiry form 5-layer defense** matches ADR 0052 inbound posture; CAPTCHA + rate limit + scoring + triage.
- **Capability tokens are macaroons** (per ADR 0032); short-lived; revocable; carry the listing-set permissioning inline.
- **Image blobs are tenant-key encrypted at rest**; Bridge route decrypts on-render and serves with `Cache-Control: public, max-age=300, stale-while-revalidate=3600` to balance privacy + performance.
- **Audit emit on every promotion** + every inquiry submit + every CAPTCHA failure. ADR 0049 substrate.
- **No third-party tracking pixels** — Sunfish self-hosts analytics or omits them (CO call per tenant).

---

## Compatibility plan

### Existing callers

`Property` entity from `blocks-properties` consumed read-only via FK. No `Property` changes; new `PublicListing` references it. `Inquiry` entity (per ADR 0057) consumed via boundary contract; no `Inquiry` changes here.

### Affected packages

| Package | Change |
|---|---|
| `packages/blocks-public-listings` | **New** — `PublicListing` + `IListingRenderer` + `ICapabilityPromoter` + `RedactionPolicy` |
| `packages/foundation-integrations/Captcha/` | **New (small)** — `ICaptchaVerifier` interface + `RecaptchaV3CaptchaVerifier` adapter (Phase 2.1) |
| `accelerators/bridge` | **Modified** — adds `/listings*` route family + Razor templates + JSON-LD + sitemap + robots.txt |
| `packages/blocks-property-leasing-pipeline` (per ADR 0057) | **Consumed** — `IPublicInquiryService.SubmitInquiryAsync(PublicInquiryRequest, AnonymousCapability, ct)` is the single boundary; see Amendments §A1. (Package name corrected per A2.) |
| `apps/docs/blocks/public-listings/` | **New** — listing surface documentation |

### Migration

No existing data to migrate (greenfield). Listings are authored via Owner Cockpit (W#29) once that block ships; in the meantime, listings can be authored directly via `IListingRepository` or seeded via kitchen-sink demo.

---

## Implementation checklist

- [ ] `packages/blocks-public-listings` package with `PublicListing` + `ListingPhotoRef` + `RedactionPolicy` + `ShowingAvailability` types (full XML doc + nullability + `required`)
- [ ] `IListingRenderer` interface + InMemory implementation enforcing tier-based redaction
- [ ] `ICapabilityPromoter` interface + InMemory implementation issuing ADR 0032 macaroons
- [ ] `IListingRepository` interface + InMemory implementation; **returns `RenderedListing<TTier>` projection types only**; un-redacted `PublicListing` is `internal` to the block (per Amendment A6)
- [ ] `MessagingEntityModule` — wait, **`PublicListingsEntityModule`** per ADR 0015
- [ ] `ICaptchaVerifier` interface in `Foundation.Integrations.Captcha/`
- [ ] `RecaptchaV3CaptchaVerifier` adapter (provider-neutrality compliant per ADR 0013)
- [ ] Bridge route family at `/listings*` with Razor templates + JSON-LD + sitemap + robots.txt
- [ ] Inquiry-form 5-layer defense (CAPTCHA + rate limit + email+MX + scorer + triage)
- [ ] **3** new `AuditEventType` constants owned by THIS ADR (per Amendment A3 reconciliation): `PublicListingPublished`, `PublicListingUnlisted`, `CapabilityPromotedToProspect`. The other three (`InquirySubmitted`/`InquiryRejected`/`CapabilityPromotedToApplicant`) move to ADR 0057's twelve-event ledger (renamed `InquiryReceived` + `InquiryRejected` per-`InquirySubmissionResult` variants + folded into `ApplicationStarted`)
- [ ] `PublicListingAuditPayloadFactory` mirroring W#31 + W#19 + W#20 + W#21 + W#18 patterns
- [ ] `apps/docs/blocks/public-listings/overview.md` + `apps/docs/blocks/public-listings/redaction-tiers.md`
- [ ] Tests: redaction-tier rendering; CAPTCHA failure path; rate-limit exceeded; inquiry post → ADR 0057 Inquiry entity; capability promotion; macaroon expiry

---

## Open questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-L1 | reCAPTCHA v3 vs hCaptcha as Phase 2.1 default | Stage 02 — recommend reCAPTCHA v3 (industry default; better UX); revisit if ADR 0013 vendor-neutrality posture forces a non-Google default |
| OQ-L2 | `Property` entity field-level redaction policy — defined here or in `blocks-properties`? | Stage 02 — define here (`PublicListing.Redaction.CustomFieldTiers`), reference Property fields by name; `blocks-properties` doesn't need to know about redaction |
| OQ-L3 | Self-guided smart-lock showing — Phase 2.1 or deferred? | Phase 2.2+; substrate accepts `ShowingAvailabilityKind.SelfGuidedSmartLock` but the actual lock-integration is out of scope this ADR |
| OQ-L4 | OpenHouse + appointment scheduling — own this ADR or ADR 0057 Leasing Pipeline? | Stage 02 — `ShowingAvailability` value-of-record lives here; the actual booking/calendar lives in ADR 0057 |
| OQ-L5 | CDN strategy for image blobs (Cloudflare R2 + signed URLs?) | Stage 02 — recommend per-tenant CDN config in `MessagingProviderConfig`-style; `accelerators/bridge` configures the CDN per its hosting model |

---

## Revisit triggers

- **`blocks-public-listings` stays under ~500 LOC after Phase 2.1** — fold into `accelerators/bridge` directly to reduce package count (revisit when first 6 months of usage observed)
- **MLS cross-posting demand** (BDFL or another tenant wants Zillow/Apartments.com syndication) — Phase 4+ ADR
- **Paid promotion / featured listings** — out of scope indefinitely; revisit only on explicit BDFL request
- **CAPTCHA service deprecation** (Google deprecates reCAPTCHA v3 or hCaptcha materially changes its surface) — replace with new adapter under existing `ICaptchaVerifier`
- **Capability-promotion abuse pattern** (real attacker goes through email-verify gauntlet to harm a tenant's leasing pipeline) — ADR 0043 addendum re-evaluation; might need slow-promotion delay or tenant-side approval gate

---

## References

### Predecessor and sister ADRs

- [ADR 0008](./0008-foundation-multitenancy.md) — multi-tenancy
- [ADR 0013](./0013-foundation-integrations.md) — provider-neutrality (`ICaptchaVerifier`)
- [ADR 0015](./0015-module-entity-registration.md) — entity-module
- [ADR 0031](./0031-bridge-hybrid-multi-tenant-saas.md) — Bridge serves the public boundary
- [ADR 0032](./0032-capability-projection-and-attenuation.md) — macaroons for prospect/applicant tiers
- [ADR 0043](./0043-trust-model-and-threat-delegation.md) — T2-LISTING-INGRESS catalog entry (this ADR adds it)
- [ADR 0049](./0049-audit-trail-substrate.md) — audit emission
- [ADR 0051](./0051-foundation-integrations-payments.md) — `Money` for asking rent
- [ADR 0052](./0052-bidirectional-messaging-substrate.md) — `IInboundMessageScorer` reused; 5-layer defense pattern
- [ADR 0057](./0057-leasing-pipeline-fair-housing.md) — `Inquiry` entity is the inquiry-form post target

### Roadmap and intakes

- [Phase 2 commercial intake](../../icm/00_intake/output/phase-2-commercial-mvp-intake-2026-04-27.md) — Rentler.com replacement framing
- [Public listings intake](../../icm/00_intake/output/property-public-listings-intake-2026-04-28.md) — original cluster scope
- [Cluster INDEX](../../icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md) — sequencing

### External

- [Google reCAPTCHA v3 documentation](https://developers.google.com/recaptcha/docs/v3) — Phase 2.1 default
- [hCaptcha documentation](https://docs.hcaptcha.com/) — provider-neutrality fallback
- [Schema.org `Apartment`/`Place`/`RealEstateListing`](https://schema.org/Apartment) — JSON-LD structured data
- [OpenGraph protocol](https://ogp.me/) — social sharing tags
- [robots.txt + sitemap.xml](https://developers.google.com/search/docs/crawling-indexing/robots/intro) — search-engine indexing

---

## Amendments (post-acceptance, 2026-04-29 council)

The council review ([`0059-council-review-2026-04-29.md`](../../icm/07_review/output/adr-audits/0059-council-review-2026-04-29.md)) graded the ADR **B (Solid)** and identified 4 required amendments (A1, A2, A6, A7 — all High severity, all blocking Stage 06 W#28 build) plus 6 encouraged (A3–A5, A8–A10). The CO accepted with amendments; this section authors all ten. After A1–A10 land, the rubric grade lifts to **A** on re-review.

The council's "Pre-acceptance audit" section (lines 343 in the original ADR) self-asserted "None of AP-1, -3, -9, -12, -21 apply." On council re-read, AP-1 fires twice (boundary contract mismatch with ADR 0057; package-name mis-prefix), AP-13 fires twice (reCAPTCHA threshold without source; Cache-Control vs encrypted-at-rest tension), AP-18 fires once (positive-only redaction tests), AP-19 fires twice (unspecified fail-closed/fail-open per layer; triage queue not bounded), and AP-21 fires twice (schema.org primary-type ambiguity; assumed CAPTCHA threshold). The amendments below close all of these.

### A1 — Reconcile the Inquiry boundary contract with ADR 0057 (HIGH; load-bearing for Stage 06 build) [REQUIRED]

**Problem.** The original Decision section defined a local `InquirySubmission` record (lines 162–172 pre-amendment) with primitive types (`string ProspectEmail`, `string ProspectName`, `IPAddress ClientIp`, `string CaptchaToken`, `string UserAgent`). ADR 0057 line 302–326 specifies the actual boundary as:

```csharp
// ADR 0057 (canonical, accepted)
public interface IPublicInquiryService
{
    Task<InquirySubmissionResult> SubmitInquiryAsync(
        PublicInquiryRequest request,
        AnonymousCapability capability,
        CancellationToken ct);
}

public sealed record PublicInquiryRequest
{
    public required PropertyId? PropertyOfInterest { get; init; }
    public required ContactPoint ContactInfo { get; init; }
    public PersonName? ProspectName { get; init; }
    public required string Message { get; init; }
    public required CaptchaToken Captcha { get; init; }
    public required ClientFingerprint ClientFingerprint { get; init; }
}

public enum InquirySubmissionResult { Accepted, RateLimited, CaptchaFailed, AbuseDetected }
```

`PublicInquiryRequest` uses domain types (`ContactPoint` per FHIR Pattern G, `PersonName`, `CaptchaToken`, `ClientFingerprint`); ADR 0059's `InquirySubmission` used primitives. The two contracts are not assignment-compatible. Worse, name collision with `InquirySubmissionResult` (the ADR 0057 enum) made it look like the contract was wired when in fact ADR 0059's `InquirySubmission` was a fresh, parallel surface that no one would have called.

**Resolution (chosen path: A1.opt-i).** ADR 0059 deletes its `InquirySubmission` record entirely. The Bridge route `POST /listings/{slug}/inquiry` accepts a route-local primitive form-post DTO (defined inside `accelerators/bridge`, NOT in `blocks-public-listings`), maps it to ADR 0057's `PublicInquiryRequest` at the controller boundary, and calls `IPublicInquiryService.SubmitInquiryAsync(...)`:

```csharp
// accelerators/bridge — route-local DTO; never crosses the block boundary
internal sealed record PublicInquiryFormPost
{
    public required string ProspectName { get; init; }
    public required string ProspectEmail { get; init; }
    public string? ProspectPhone { get; init; }
    public required string MessageBody { get; init; }
    public required string CaptchaToken { get; init; }
    public required string ClientIp { get; init; }
    public required string UserAgent { get; init; }
}

// Controller maps form-post → ADR 0057 PublicInquiryRequest:
var request = new PublicInquiryRequest
{
    PropertyOfInterest = listing.Property,
    ContactInfo = ContactPoint.FromEmail(form.ProspectEmail, form.ProspectPhone),
    ProspectName = PersonName.TryParse(form.ProspectName),
    Message = form.MessageBody,
    Captcha = new CaptchaToken(form.CaptchaToken),
    ClientFingerprint = ClientFingerprint.From(form.ClientIp, form.UserAgent),
};
var capability = AnonymousCapability.ForPublicInquiry(listing.Tenant);
var result = await _inquiryService.SubmitInquiryAsync(request, capability, ct);
return result switch
{
    InquirySubmissionResult.Accepted => RedirectToInquiryConfirmation(listing.Slug),
    _ => InquiryAcceptedAuditedReject(result), // see A5: every reject path is 200 OK + audit, never 4xx that leaks signal
};
```

**Why this carve.** ADR 0057 is older and accepted; ADR 0059 is the consumer. The Bridge route is the trust boundary; primitive→domain mapping at the controller is the canonical place for that lift (per ADR 0031 Bridge-as-Zone-C posture). No new contract surface in `blocks-public-listings` for inquiry; the entire inquiry boundary belongs to ADR 0057.

**ADR-text edits already landed:** the `InquirySubmission` record + `// Inquiry form post — boundary contract; submits to ADR 0057 Inquiry entity` block is removed from §"Initial contract surface" (see §A1-edit below). The §"Bridge route surface" line for `POST /listings/{slug}/inquiry` is annotated to clarify the controller-side mapping. The implementation checklist line "Inquiry-form 5-layer defense ..." is amended to call out that the 5-layer defense is **the Bridge controller's responsibility before invoking `IPublicInquiryService.SubmitInquiryAsync`** — ADR 0057's service does not duplicate the layers.

**Cascade.** The W#28 hand-off (`icm/_state/handoffs/property-public-listings-stage06-handoff.md`) currently references ADR 0059's `InquirySubmission` shape; XO will publish a hand-off addendum redirecting to ADR 0057's `PublicInquiryRequest` + the controller-mapping pattern above. That cascade is tracked separately; it does not block this ADR's amendment merge.

### A1-edit — `InquirySubmission` record deleted from §"Initial contract surface"

The following block is **removed** from the Initial contract surface code listing (it appeared between the `ProspectCapability` definition and the §"Bridge route surface" header):

```csharp
// REMOVED PER A1 — boundary belongs to ADR 0057's IPublicInquiryService
// public sealed record InquirySubmission { ... }
```

Future Stage 06 contributors reading the ADR will see the controller-mapping example in §A1 above and the unchanged reference to `IPublicInquiryService` in ADR 0057. There is no `InquirySubmission` type in the `Sunfish.Blocks.PublicListings` namespace.

### A2 — Correct affected-package name (HIGH; tied to A1) [REQUIRED]

The §"Affected packages" table line for the leasing-pipeline package is corrected:

- **Before:** `packages/blocks-leasing-pipeline` (per ADR 0057)
- **After:** `packages/blocks-property-leasing-pipeline` (per ADR 0057)

Cluster sibling convention is `blocks-property-*` per `project_property_ops_cluster_2026_04_28` memory and ADR 0057 line 75/77/118/493 (which all use the prefixed name). The original `blocks-leasing-pipeline` was a verbatim authoring slip; ADR 0057 has never used the unprefixed form. Edit landed in the affected-packages table above.

### A3 — Audit-event constants reconciled with ADR 0057 (MEDIUM) [ENCOURAGED]

The implementation checklist's six audit-event constants (`PublicListingPublished`, `PublicListingUnlisted`, `InquirySubmitted`, `InquiryRejected`, `CapabilityPromotedToProspect`, `CapabilityPromotedToApplicant`) overlap with ADR 0057's twelve lifecycle audit records. Reconciliation:

| Audit event | Owning ADR | Emit-from | Rationale |
|---|---|---|---|
| `InquiryReceived` | **ADR 0057** (chronologically first; canonical name) | `IPublicInquiryService.SubmitInquiryAsync` impl | ADR 0057 owns the lifecycle; ADR 0059 does NOT double-emit on the controller side |
| `InquiryRejected` (CAPTCHA fail / rate-limit / abuse-fingerprint hit) | **ADR 0057** | `IPublicInquiryService.SubmitInquiryAsync` impl returns `InquirySubmissionResult.{RateLimited|CaptchaFailed|AbuseDetected}`; service emits per-result | The 5-layer defense outcomes are inquiry-lifecycle events, not listing-surface events |
| `PublicListingPublished` | **ADR 0059** | `IListingRepository` mutation (Draft→Published) | Listing-surface lifecycle; never overlaps with inquiry-lifecycle |
| `PublicListingUnlisted` | **ADR 0059** | `IListingRepository` mutation (Published→Unlisted) | Listing-surface lifecycle |
| `CapabilityPromotedToProspect` | **ADR 0059** | `ICapabilityPromoter.PromoteToProspectAsync` impl | Promotion is a listing-surface gate (anonymous→prospect happens at inquiry+verify); ADR 0057 receives the promoted capability but does not own the event |
| `CapabilityPromotedToApplicant` | **ADR 0057** (re-categorized) | `ApplicationStarted`-equivalent in ADR 0057 lifecycle | Capability becomes Applicant when the application starts, which is an ADR 0057 event; ADR 0059 does NOT emit on this transition |

**Net effect on ADR 0059's checklist:** the six-event list shrinks to **three events owned by this ADR**: `PublicListingPublished`, `PublicListingUnlisted`, `CapabilityPromotedToProspect`. The other three move to ADR 0057's twelve-event ledger (or are renamed there: `InquirySubmitted` → `InquiryReceived`; `InquiryRejected` stays; `CapabilityPromotedToApplicant` → folded into `ApplicationStarted`).

ADR 0057 hand-off file `property-leasing-pipeline-stage06-handoff.md` already names the 12-record ledger; XO will verify that ledger covers `InquiryRejected` (per-result variants) on next re-read of that hand-off.

### A4 — JSON-LD primary type pinned (MEDIUM; SEO-correctness) [ENCOURAGED]

The original §"Bridge route surface" referenced "JSON-LD `Apartment`/`Place` schema for SEO" without picking a primary type or specifying nesting. Schema.org semantics:

- `Place` is the supertype (no rich-result eligibility);
- `Apartment` is a subtype of `Accommodation` (the *unit being offered*);
- `RealEstateListing` is a subtype of `WebPage` (the *page about the listing*; carries `datePosted` + `leaseLength`, which drive Google's listing-rich-result eligibility).

**Decision.** The page-level `@type` is **`RealEstateListing`**; the offered-unit lives under `mainEntity` as `Apartment` (multi-family) or `SingleFamilyResidence` / `House` (single-family). Recommended emission shape:

```json
{
  "@context": "https://schema.org",
  "@type": "RealEstateListing",
  "url": "https://{tenant-domain}/listings/{slug}",
  "datePosted": "{PublicListing.PublishedAt:O}",
  "leaseLength": "P12M",
  "name": "{PublicListing.Headline}",
  "description": "{PublicListing.Description}",
  "mainEntity": {
    "@type": "Apartment",
    "numberOfBedrooms": {Property.Bedrooms},
    "numberOfBathroomsTotal": {Property.Bathrooms},
    "floorSize": { "@type": "QuantitativeValue", "value": {Property.SquareFeet}, "unitCode": "FTK" },
    "address": {
      "@type": "PostalAddress",
      "addressLocality": "{redacted-per-tier}",
      "addressRegion": "{Property.State}",
      "postalCode": "{Property.PostalCode}",
      "streetAddress": "{redacted-per-tier or omitted}"
    }
  },
  "offers": {
    "@type": "Offer",
    "price": "{PublicListing.AskingRent.Amount}",
    "priceCurrency": "{PublicListing.AskingRent.Currency}",
    "availabilityStarts": "{PublicListing.AvailableFrom:O}"
  }
}
```

For single-family, swap `Apartment` → `SingleFamilyResidence`. The address fields participate in the same redaction tier as the rest of the listing (per `RedactionPolicy.Address`); anonymous-tier renders omit `streetAddress` entirely and emit `addressLocality` at neighborhood granularity only.

The §"External" references list keeps the existing `https://schema.org/Apartment` link and adds `https://schema.org/RealEstateListing` for the page-level type.

### A5 — 5-layer defense fail-closed posture per layer (MEDIUM) [REQUIRED, restated as REQUIRED-equivalent in council §"Required amendments"]

The original §"Inquiry-form abuse posture" listed five layers but did not specify failure-mode posture. Posture is now explicit:

| Layer | Failure mode | User-visible response | Rationale |
|---|---|---|---|
| 1. CAPTCHA verify (`ICaptchaVerifier`) | **Fail-closed.** Verifier unreachable / 5xx / timeout (>2s) → reject inquiry | **HTTP 200 OK** with a friendly "verification temporarily unavailable; please retry" page; audit-emit `InquiryRejected{Reason=CaptchaUnavailable}`. **Never** 4xx/5xx, which leaks signal that the form is the gate. | Public-input boundary; better to lose a few legitimate submits than admit one abuse round. |
| 2. Per-IP rate limit (sliding window 5/hr per IP, 50/hr per tenant) | **Fail-closed.** Rate-limiter store unavailable → reject | **HTTP 200 OK** with the same friendly "please retry" page; audit-emit `InquiryRejected{Reason=RateLimitUnavailable}`. | Without rate limiting, layer 5 (triage queue) becomes the DoS-by-exhaustion target. |
| 3. Email format + MX check | **Fail-closed on NXDOMAIN; fail-open on DNS-unreachable.** | NXDOMAIN: HTTP 200 OK + friendly "please verify your email" + audit-emit `InquiryRejected{Reason=InvalidEmail}`. DNS-unreachable: pass to layer 4 with `MxCheckResult.Skipped` flag. | NXDOMAIN is a positive abuse signal; DNS-unreachable is a Bridge-host infra problem and shouldn't block legitimate submits. |
| 4. `IInboundMessageScorer` plug-in (per ADR 0052) | **Fail-open** (scorer error → fall through with `score=null`; default `NullScorer` accepts). | No user-visible difference (success path or layer 5 routing). | Per-ADR-0052 substrate behavior; pluggable per tenant; default accepts. |
| 5. Manual unrouted-triage queue | **Bounded with explicit overflow handling.** Tenant-config `MaxQueueDepth` (default 1000); on overflow, reject + operator alert | **HTTP 200 OK** + friendly "thanks, we'll be in touch shortly" page (per privacy posture; never reveal queue is full); audit-emit `InquiryRejected{Reason=QueueOverflow}` + operator-alert (per ADR 0049). | Otherwise becomes the DoS-by-exhaustion target. |

**Universal rule (cross-layer).** Every reject path returns **HTTP 200 OK + a friendly user-facing message + an audit emit**. **No layer ever returns a 4xx or 5xx that could provide an attack signal** (timing, status-code-classification, response-body-shape) to an abuse actor probing the form. The only exceptions are ADR-0052 layer 5xx (Bridge framework-tier failures unrelated to the form), which the upstream framework owns. This matches ADR 0052 amendment A1's "200 OK + audit + triage" posture for inbound public surfaces.

### A6 — Strengthen the redaction-enforcement claim structurally (HIGH) [REQUIRED]

**Problem.** The original §"Redaction enforcement" claimed "renderer enforces redaction at the block boundary so callers can't accidentally leak" — the language of a structural defense. But §"Initial contract surface" exposed `PublicListing` as a public init-only record (all fields populated regardless of tier) and the implementation checklist named `IListingRepository` as part of the cross-package surface. A sloppy `accelerators/bridge` Razor page (or any future consumer) could inject `IListingRepository` and read `.AskingRent` / `.Photos` / `.Redaction` directly, bypassing the renderer. That is a *disciplinary* defense, not a structural one — the bytes always exist; you're trusted to use the renderer.

**Resolution (chosen path: A6.opt-i — make data-leak path structurally closed).**

1. `PublicListing` is now `internal` to the `Sunfish.Blocks.PublicListings` package. The init-only record stays the same internally; it is no longer part of the public-surface contract.
2. `IListingRepository` requires `RedactionTier` as a query parameter on read methods and returns a `RenderedListing<TTier>` projection record (one of three closed-set types: `RenderedListing<Anonymous>`, `RenderedListing<Prospect>`, `RenderedListing<Applicant>`). Each projection contains only the fields permitted at that tier.
3. The cross-package surface now exposes:

   ```csharp
   public interface IListingRepository
   {
       Task<RenderedListing<TTier>?> GetBySlugAsync<TTier>(TenantId tenant, string slug, CancellationToken ct)
           where TTier : IRedactionTier;

       Task<IReadOnlyList<RenderedListing<Anonymous>>> ListPublishedAsync(TenantId tenant, CancellationToken ct);
   }

   // Tag types — closed set; cannot be defined outside the block:
   public interface IRedactionTier { static abstract RedactionTier Tier { get; } }
   public sealed class Anonymous : IRedactionTier { public static RedactionTier Tier => RedactionTier.Anonymous; }
   public sealed class Prospect  : IRedactionTier { public static RedactionTier Tier => RedactionTier.Prospect; }
   public sealed class Applicant : IRedactionTier { public static RedactionTier Tier => RedactionTier.Applicant; }
   ```

4. `IListingRenderer.RenderForTier(...)` becomes the **only** path that crosses tiers (e.g., when the Bridge controller has a Prospect macaroon and needs to upgrade an `Anonymous` projection to a `Prospect` projection). Renderer takes the macaroon (per ADR 0032) + the source projection and returns the higher-tier projection or null.
5. ADR 0043's `T2-LISTING-INGRESS` catalog entry is amended (in this ADR's compatibility plan) to note: **non-Bridge consumers of `blocks-public-listings` (kitchen-sink demos, future Anchor operator views, kernel-audit scrapers) MUST consume the public `IListingRepository` projection types; direct access to `PublicListing` is structurally unavailable.**
6. Implementation checklist update: `IListingRepository` line in §"Implementation checklist" is amended to "`IListingRepository` interface + InMemory implementation; **returns `RenderedListing<TTier>` projection types only**; un-redacted `PublicListing` is `internal` (per A6)."

**The "structurally" claim is now structurally true.** A consumer cannot read un-redacted bytes without the renderer + a tier-gating macaroon, because the un-redacted record is not in the public contract surface.

### A7 — `RedactionPolicy.CustomFieldTiers` typo defense (HIGH) [REQUIRED]

**Problem.** The original `RedactionPolicy.CustomFieldTiers` was `IReadOnlyDictionary<string, RedactionTier>` with `ImmutableDictionary<string, RedactionTier>.Empty` default. A typo in a custom-field key (`"BedRooms"` vs `"Bedrooms"`) silently disabled redaction for that field, falling through to the default tier. With a missing-key default of Anonymous, the typo silently *leaks* the field at every tier — exactly the failure mode §"Trust impact" claimed was structurally prevented.

**Resolution (chosen path: A7.opt-iii — kernel-side validator + Applicant-default fallback; defense-in-depth).**

1. **Kernel-side validator at registration (per ADR 0015).** `PublicListingsEntityModule` validates at module-registration time that every key in any tenant's `CustomFieldTiers` matches a public field on the `Property` entity (use reflection over `Property` public surface; tenant-config-loaded keys are checked against the reflected set). **Unknown keys → registration fails** (typo becomes a startup-time error, not a runtime silent leak). Validator location: `packages/blocks-public-listings/Modules/PublicListingsEntityModule.cs`.
2. **Runtime fallback for unknown keys is `RedactionTier.Applicant`.** Even if the validator is bypassed (e.g., a future code path adds `CustomFieldTiers` post-registration), the runtime renderer's missing-key default is the most-restrictive tier — a typo means "field is never shown" rather than "field is always shown." The default in the type definition is amended:

   ```csharp
   public sealed record RedactionPolicy
   {
       public required AddressRedactionLevel Address { get; init; }
       public required bool IncludeFinancialsForProspect { get; init; }
       public required bool IncludeAssetInventoryForApplicant { get; init; }

       /// <summary>
       /// Per-custom-field redaction tier. Keys MUST match public field names on
       /// <see cref="Property"/> (validated at <see cref="PublicListingsEntityModule"/>
       /// registration time per ADR 0015). Runtime missing-key fallback is
       /// <see cref="RedactionTier.Applicant"/> (most-restrictive / fail-closed) per A7.
       /// </summary>
       public IReadOnlyDictionary<string, RedactionTier> CustomFieldTiers { get; init; }
           = ImmutableDictionary<string, RedactionTier>.Empty;
   }

   // Renderer-side (illustrative):
   internal RedactionTier ResolveTier(string fieldName, RedactionPolicy policy)
       => policy.CustomFieldTiers.TryGetValue(fieldName, out var tier)
           ? tier
           : RedactionTier.Applicant;   // fail-closed default
   ```
3. **Forward path: strongly-typed enum keys (Phase 2.2+).** When `Property` field set stabilizes, generate a source-generated enum from the `Property` public surface and migrate `CustomFieldTiers` to `IReadOnlyDictionary<PropertyField, RedactionTier>`. This eliminates the typo class entirely. Tracked as a §"Revisit triggers" entry below.

**Net effect on §"Trust impact".** The "structurally limited" claim now matches reality: a custom-field-tier typo **(a) fails at startup via the kernel validator; or (b) fails-closed at runtime via the Applicant default**. Both paths are explicit; neither silently leaks.

### A8 — Cite or qualify the reCAPTCHA threshold (LOW) [ENCOURAGED]

The original §"Inquiry-form abuse posture" specified `reCAPTCHA v3 / hCaptcha; reject score < 0.3` without citing a source. Google's official documentation says only "experiment with thresholds per site" — 0.3 is community-folklore, not a Google-published default. Amendment:

- **Inline note added to layer 1 of §"Inquiry-form abuse posture":** "CAPTCHA score threshold (default 0.3) is a Phase 2.1 starting heuristic to be tuned per tenant against observed abuse-rate data; Google's published guidance is 'experiment per site.' Tunable via `RecaptchaV3CaptchaVerifier.MinimumScore` config (`Foundation.Integrations.Captcha`)."
- **OQ-L1 expanded** (§"Open questions"): the threshold itself is added as part of the OQ-L1 Phase 2.1 acceptance criterion: "tune per tenant after first 30 days of inquiry-form telemetry."

### A9 — Resolve Cache-Control vs encrypted-at-rest tension (LOW) [ENCOURAGED]

The original §"Trust impact" simultaneously claimed (a) "Image blobs are tenant-key encrypted at rest" and (b) photos served with `Cache-Control: public, max-age=300, stale-while-revalidate=3600`. For an encrypted blob *decrypted per-request and then served `public`*, the cache stores the *decrypted bytes* at every CDN edge — privacy regression versus the at-rest claim.

**Resolution.** Photos are tier-bound, and Cache-Control is tier-conditional:

- **`MinimumTier == Anonymous` photos** (the listing-card / public-detail-page photos): public caching is correct because the photos are intended for public consumption; the at-rest encryption protects *unpublished drafts*, not *published anonymous-tier* artifacts. `Cache-Control: public, max-age=300, stale-while-revalidate=3600` is retained for these.
- **`MinimumTier >= Prospect` photos** (e.g., interior shots, floor plans, specific-unit identifying photos): cache as **`Cache-Control: private, max-age=60, must-revalidate`**, served via macaroon-bound signed URLs (per ADR 0032). The CDN edge receives the encrypted blob URL only with a valid capability header; without macaroon, the request is 200 OK + redirect to the public-tier image, never a 4xx.
- **Draft photos (any tier)**: `Cache-Control: no-store`. Drafts are never public.

This distinction is now explicit in §"Trust impact" and the implementation checklist.

### A10 — Negative-test discipline for redaction-tier acceptance (LOW) [ENCOURAGED]

The original tests-list under §"Redaction enforcement" only tested positive cases ("anonymous render contains neighborhood-only address; prospect render contains block-number address; applicant render contains full address"). Negative-case discipline is the load-bearing assertion for a redaction chokepoint. Amendment to the test list:

```
✓ Anonymous render CONTAINS neighborhood-only address.
✓ Anonymous render does NOT contain block-number or full address.
✓ Anonymous render does NOT contain photos with `MinimumTier >= Prospect`.
✓ Anonymous render does NOT contain `AskingRent` if RedactionPolicy redacts financials at anonymous tier.
✓ Prospect render CONTAINS block-number address.
✓ Prospect render does NOT contain full address.
✓ Prospect render does NOT contain photos with `MinimumTier == Applicant`.
✓ Applicant render CONTAINS full address + all photos.
✓ Applicant render does NOT contain custom fields whose `CustomFieldTiers` key is unrecognized (per A7 — Applicant-default = "field invisible because key invalid").
✓ Renderer with mismatched macaroon (Anonymous macaroon requesting Prospect tier) returns null + audit-emit `RedactionTierMismatch`.
```

These tests live in `packages/blocks-public-listings/tests/RedactionEnforcementTests.cs`. The renderer-implementation suite is structurally complete only when all ten assertions pass.

### Revisit triggers (added per A7)

The existing §"Revisit triggers" list gains one entry:

- **`Property` field set stabilizes (Phase 2.2+)** — migrate `RedactionPolicy.CustomFieldTiers` from `IReadOnlyDictionary<string, RedactionTier>` to a source-generated strongly-typed enum (`PropertyField` → `RedactionTier`). Eliminates the typo class entirely. Track when `Property` reflection-surface size has been stable for two consecutive minor versions.

---

- [x] **AHA pass.** Three options: new block, inline-into-properties, static-export. Option A chosen with explicit rejection rationale for B (data-leak risk) and C (server runtime needed anyway).
- [x] **FAILED conditions / kill triggers.** 5 named: under-500-LOC fold-back, MLS cross-posting demand, paid promotion, CAPTCHA deprecation, capability-promotion abuse pattern.
- [x] **Rollback strategy.** Greenfield; no existing data. Rollback = revert ADR + revert block creation. Bridge route gets removed.
- [x] **Confidence level.** **MEDIUM-HIGH.** Substrate composition is well-understood; only novel piece is `RedactionPolicy` enforcement at the renderer boundary, which has good test patterns.
- [x] **Anti-pattern scan.** Original self-scan claimed "None of AP-1, -3, -9, -12, -21 apply"; council re-read found AP-1 (boundary contract drift + package-name mis-prefix), AP-13 (CAPTCHA-threshold + cache vs encrypted-at-rest), AP-18 (positive-only redaction tests), AP-19 (unspecified per-layer failure mode + unbounded triage queue), and AP-21 (schema.org primary-type ambiguity) all fired. **All resolved by Amendments A1–A10**; see §"Amendments (post-acceptance, 2026-04-29 council)".
- [x] **Revisit triggers.** Five named with externally-observable signals.
- [x] **Cold Start Test.** Implementation checklist is 12 specific tasks. Stage 02 contributor reading this ADR (post-A1–A10) + ADR 0031 + ADR 0032 + ADR 0052 + ADR 0057 should be able to scaffold without asking for substrate clarification. Pre-amendment, the boundary contract drift with ADR 0057 broke this — Amendment A1 closes the gap by deleting the parallel `InquirySubmission` record and pointing all inquiry-flow consumers at ADR 0057's `IPublicInquiryService.SubmitInquiryAsync(PublicInquiryRequest, AnonymousCapability, ct)`.
- [x] **Sources cited.** ADR 0008, 0013, 0015, 0031, 0032, 0043, 0049, 0051, 0052, 0057 referenced. reCAPTCHA, hCaptcha, schema.org, OpenGraph, robots.txt cited.
