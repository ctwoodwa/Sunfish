# Sunfish.Blocks.PublicListings

Block for the public-facing rental-listings surface — the anonymous-browse / inquiry-form / capability-tier-promote pipeline that funnels prospects into `Sunfish.Blocks.PropertyLeasingPipeline`.

Implements [ADR 0059 — Public-listing surface](../../docs/adrs/0059-public-listing-surface.md) and the ADR 0043 capability-gradient addendum.

## What this ships

### Models

- **`PublicListing`** — published rental listing entity (`IMustHaveTenant`); has a slug, headline, description, photo refs, asking-rent, showing availability, redaction policy, and lifecycle status (Draft / Published / Unlisted).
- **`ListingPhotoRef`** — photo asset reference with per-tier visibility (`MinimumTier`).
- **`RedactionPolicy`** — per-listing tier-redaction config (address precision, financials-for-prospect, asset-inventory-for-applicant).
- **`ShowingAvailability`** — open-house / by-appointment / self-guided modes.
- **`RenderedListing`** — tier-redacted projection produced by `IListingRenderer.RenderForTierAsync`.

### Services

- **`IListingRepository`** + `InMemoryListingRepository` — CRUD + slug lookup + tenant enumeration.
- **`IListingRenderer`** + `DefaultListingRenderer` — single chokepoint for tier-based redaction; consumers MUST go through this rather than reading raw `PublicListing` records.

### Capabilities (`Capabilities/`)

- **`ICapabilityPromoter`** + `MacaroonCapabilityPromoter` — mints a `ProspectCapability` (macaroon + caveats) post-email-verification per ADR 0043.
- **`IProspectCapabilityVerifier`** + `MacaroonProspectCapabilityVerifier` — block-local verifier paired to the issuer; performs sig-chain check inline (foundation generic verifier fails-closed on Sunfish-specific caveat predicates) and parses caveats locally; returns `VerifiedProspectCapability` projection.
- **`ProspectCaveatNames`** (internal const) — single source of truth for caveat keys (issuer + verifier consume from the same constants; drift-prevention).

### Defense (`Defense/`)

- **`IInquiryFormDefense`** + `InquiryFormDefense` — 5-layer fail-closed defense pipeline:
  1. CAPTCHA verify (`ICaptchaVerifier`)
  2. Per-IP + per-tenant rate limit (`IInquiryRateLimiter`)
  3. Email format + DNS MX (`IEmailMxResolver`)
  4. Abuse score (`IInboundMessageScorer` from W#20; opt-in via DI)
  5. Manual triage queue (`IUnroutedTriageQueue` from W#20; opt-in via DI)
- **`InquiryFormDefenseOptions`** — score thresholds (default 80 hard / 50 soft).

### Audit

- 6 `AuditEventType` constants in `Sunfish.Kernel.Audit` for the public-listings lifecycle (4 wired in services + 2 reserved for Bridge-route emission); `PublicListingAuditPayloadFactory` builds the bodies; `PublicListingAuditEmitter` is the optional emit-on-write seam.

## Bridge route family

The `accelerators/bridge` SaaS posture exposes a 5-route family consuming this block:

- `GET /robots.txt` + `GET /sitemap.xml` — SEO discovery
- `GET /listings` + `GET /listings/{slug}` — Anonymous-tier SSR pages with schema.org JSON-LD + OpenGraph
- `POST /listings/{slug}/inquiry` — runs the 5-layer defense + forwards to leasing-pipeline `IPublicInquiryService`
- `GET /listings/criteria/{token}` — Prospect-tier criteria document (capability-gated)
- `POST /listings/criteria/{token}/start-application` — Prospect → Applicant promotion

See `apps/docs/blocks/public-listings/capability-tier-flow.md` for the full lifecycle.

## DI

```csharp
services.AddInMemoryPublicListings();
```

Registers repository + renderer + the 5-layer defense pipeline (Layers 1-3 always wired; 4-5 auto-activate when W#20's `AddSunfishMessaging()` is also wired) + the prospect-capability verifier.

## ADR map

- [ADR 0059](../../docs/adrs/0059-public-listing-surface.md) — public-listing surface architecture
- [ADR 0043 addendum](../../docs/adrs/0043-capability-gradient.md) — capability-tier framework
- [ADR 0032](../../docs/adrs/0032-multi-team-anchor-workspace-switching.md) — macaroon substrate

## See also

- [apps/docs walkthrough](../../apps/docs/blocks/public-listings/overview.md)
- [Inquiry-form defense](../../apps/docs/blocks/public-listings/inquiry-defense.md)
- [Capability-tier flow](../../apps/docs/blocks/public-listings/capability-tier-flow.md)
- [Audit emission](../../apps/docs/blocks/public-listings/audit-emission.md)
