# ADR 0059 — Public Listing Surface (Bridge-served)

**Status:** Proposed (2026-04-29; awaiting council review + acceptance)
**Date:** 2026-04-29
**Author:** XO (research session)
**Pipeline variant:** `sunfish-feature-change` (new block `blocks-public-listings` + Bridge route + ADR 0043 addendum)

**Resolves:** [property-public-listings-intake-2026-04-28.md](../../icm/00_intake/output/property-public-listings-intake-2026-04-28.md); cluster workstream #28.

---

## Context

Phase 2 commercial intake ships rental properties with public-facing listings as a mandatory channel: prospects find the listing on the public web (Google search → property page), submit an inquiry, and that inquiry kicks off the leasing pipeline (per ADR 0057). Today the BDFL uses Rentler.com for this surface; the goal is to move it onto Sunfish so the listing-to-application pipeline is owned end-to-end (data custody, no third-party tracking, no per-listing fees).

This ADR specifies the public-input boundary: the Bridge-hosted public listing pages + the inquiry form that sits at the boundary. The inquiry's downstream lifecycle (Inquiry → Prospect → Applicant) is owned by ADR 0057 Leasing Pipeline. This ADR ships the *surface* — pages, structured data, abuse posture, capability promotion gate.

Cross-cutting constraints:

- **SEO matters.** A listing that doesn't surface in Google is invisible. Pages must be server-rendered, indexed, and structured (JSON-LD `Place`/`Apartment` schema) for organic discovery.
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

// Inquiry form post — boundary contract; submits to ADR 0057 Inquiry entity
public sealed record InquirySubmission
{
    public required PublicListingId ListingId { get; init; }
    public required string ProspectName { get; init; }
    public required string ProspectEmail { get; init; }
    public string? ProspectPhone { get; init; }
    public required string MessageBody { get; init; }                    // free-text; admin-defined max length per tenant
    public required string CaptchaToken { get; init; }                   // reCAPTCHA v3 / hCaptcha
    public required IPAddress ClientIp { get; init; }                    // for per-IP rate limit
    public required string UserAgent { get; init; }                      // abuse-pattern signal
}
```

### Bridge route surface

| Path | Purpose | Auth tier |
|---|---|---|
| `GET /listings` | All published listings for tenant; sitemap-friendly | Anonymous |
| `GET /listings/{slug}` | Listing detail page (server-rendered, JSON-LD) | Anonymous |
| `POST /listings/{slug}/inquiry` | Inquiry form submission | Anonymous (CAPTCHA + rate-limited) |
| `GET /listings/criteria/{capability-token}` | Criteria-document review (per ADR 0057) | Prospect |
| `POST /listings/criteria/{capability-token}/start-application` | Promote to Applicant | Prospect |

All routes server-render. JSON-LD `Apartment`/`Place` schema for SEO. OpenGraph tags for social sharing. `robots.txt` allows indexing of `/listings*`; `sitemap.xml` lists all `Published` slugs.

### Inquiry-form abuse posture (ADR 0043 T2 boundary)

Per ADR 0043 trust-model + ADR 0052 amendment A1's 5-layer defense pattern:

1. **CAPTCHA verify** — reCAPTCHA v3 / hCaptcha; reject score < 0.3
2. **Per-IP rate limit** — sliding window 5/hr per IP, 50/hr per tenant; exceeded → 429 + audit
3. **Email format + MX check** — reject if recipient domain has no MX record
4. **`IInboundMessageScorer` plug-in** — same interface from ADR 0052; tenant-pluggable spam classifier
5. **Manual unrouted-triage** — anything ambiguous goes to triage queue per ADR 0052

Step 4's scorer can be tenant-customized per ADR 0052; default `NullScorer` accepts all that pass 1-3.

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
| `packages/blocks-leasing-pipeline` (per ADR 0057) | **Consumed** — `Inquiry` entity is the post target |
| `apps/docs/blocks/public-listings/` | **New** — listing surface documentation |

### Migration

No existing data to migrate (greenfield). Listings are authored via Owner Cockpit (W#29) once that block ships; in the meantime, listings can be authored directly via `IListingRepository` or seeded via kitchen-sink demo.

---

## Implementation checklist

- [ ] `packages/blocks-public-listings` package with `PublicListing` + `ListingPhotoRef` + `RedactionPolicy` + `ShowingAvailability` types (full XML doc + nullability + `required`)
- [ ] `IListingRenderer` interface + InMemory implementation enforcing tier-based redaction
- [ ] `ICapabilityPromoter` interface + InMemory implementation issuing ADR 0032 macaroons
- [ ] `IListingRepository` interface + InMemory implementation
- [ ] `MessagingEntityModule` — wait, **`PublicListingsEntityModule`** per ADR 0015
- [ ] `ICaptchaVerifier` interface in `Foundation.Integrations.Captcha/`
- [ ] `RecaptchaV3CaptchaVerifier` adapter (provider-neutrality compliant per ADR 0013)
- [ ] Bridge route family at `/listings*` with Razor templates + JSON-LD + sitemap + robots.txt
- [ ] Inquiry-form 5-layer defense (CAPTCHA + rate limit + email+MX + scorer + triage)
- [ ] 6 new `AuditEventType` constants: `PublicListingPublished`, `PublicListingUnlisted`, `InquirySubmitted`, `InquiryRejected`, `CapabilityPromotedToProspect`, `CapabilityPromotedToApplicant`
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

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options: new block, inline-into-properties, static-export. Option A chosen with explicit rejection rationale for B (data-leak risk) and C (server runtime needed anyway).
- [x] **FAILED conditions / kill triggers.** 5 named: under-500-LOC fold-back, MLS cross-posting demand, paid promotion, CAPTCHA deprecation, capability-promotion abuse pattern.
- [x] **Rollback strategy.** Greenfield; no existing data. Rollback = revert ADR + revert block creation. Bridge route gets removed.
- [x] **Confidence level.** **MEDIUM-HIGH.** Substrate composition is well-understood; only novel piece is `RedactionPolicy` enforcement at the renderer boundary, which has good test patterns.
- [x] **Anti-pattern scan.** None of AP-1, -3, -9, -12, -21 apply. All references existing or about-to-ship substrates.
- [x] **Revisit triggers.** Five named with externally-observable signals.
- [x] **Cold Start Test.** Implementation checklist is 12 specific tasks. Stage 02 contributor reading this ADR + ADR 0031 + ADR 0032 + ADR 0052 + ADR 0057 should be able to scaffold without asking for substrate clarification.
- [x] **Sources cited.** ADR 0008, 0013, 0015, 0031, 0032, 0043, 0049, 0051, 0052, 0057 referenced. reCAPTCHA, hCaptcha, schema.org, OpenGraph, robots.txt cited.
