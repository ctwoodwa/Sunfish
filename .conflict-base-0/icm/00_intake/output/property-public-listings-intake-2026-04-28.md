# Intake Note — Public Listings Surface

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build`.**
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (turn 6 — public-facing inquiry intake).
**Pipeline variant:** `sunfish-feature-change` (with new ADR — public-input boundary)
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)

---

## Problem Statement

When a property is vacant, prospects need a way to discover and inquire about it. Today the BDFL uses Rentler.com for listings. Phase 2 defers full Rentler replacement to Phase 3, but a Sunfish-hosted public listing surface — even if just one page per property — gives the leasing pipeline a coherent entry point and removes a dependency on a third-party listing platform for *first-touch* inquiry.

This is also Sunfish's first **public, anonymous-accessible** surface. Every other Sunfish surface to date has been authenticated. Listings flip the trust model: anonymous users can read public data and submit inquiry forms. That's a new threat-model boundary that ADR 0043 needs to formalize (addendum from this cluster).

## Scope Statement

### In scope

1. **`PublicListing` entity.** Tenant + FK Property + FK PropertyUnit + status (draft | published | unlisted) + headline + description + photos[] + asking_rent + available_date + showing_availability + redaction_rules (which Property fields are public vs private).
2. **Public listing pages on Bridge.** Server-rendered HTML for SEO; structured data (JSON-LD); responsive layout; OpenGraph tags for social sharing; sitemap.xml.
3. **Public inquiry form.** Anonymous-accessible; CAPTCHA-gated (reCAPTCHA v3 recommended); per-IP rate-limited; abuse posture per ADR 0043 addendum. Submits to Inquiry entity (per Leasing Pipeline intake).
4. **Capability promotion gate.** Anonymous form submission → Inquiry entity created → email verification → "prospect" capability granted (short-lived macaroon for criteria-document review and application start).
5. **Listing redaction.** Property entity has fields not all of which are public: street address might be redacted to "Block 1200, Main Street neighborhood" until prospect has applied; vendor info, financials, asset inventory all private.
6. **Listing CMS.** Owner edits listing in cockpit (Owner Cockpit intake); publishing flips visibility on; unpublish hides without deleting.
7. **`blocks-public-listings` package.**
8. **New ADR**: "Public listing surface (Bridge-served)."
9. **ADR 0043 addendum**: public-input boundary; capability promotion (anonymous → prospect → applicant); inquiry-form abuse posture.

### Out of scope

- Inquiry entity / leasing pipeline state machine → [`property-leasing-pipeline-intake-2026-04-28.md`](./property-leasing-pipeline-intake-2026-04-28.md)
- Rentler.com replacement portal (full lease-holder portal) → Phase 3
- Showing scheduling → Leasing Pipeline intake
- Multiple-listing-service (MLS) cross-posting → Phase 4+
- Paid promotion / featured listings → out of scope indefinitely

---

## Affected Sunfish Areas

- `blocks-public-listings` (new)
- Bridge — server-side rendering for public pages
- `foundation-persistence`, ADR 0015
- ADR 0043 (addendum)
- New "Public listing surface" ADR
- ADR 0049 (audit) — listing publication events, inquiry receipts

## Acceptance Criteria

- [ ] New ADR + ADR 0043 addendum accepted
- [ ] PublicListing entity + redaction rules
- [ ] Bridge server-side rendering with SEO + JSON-LD + OG tags
- [ ] Public inquiry form: CAPTCHA + rate limit + abuse posture
- [ ] Capability promotion: anonymous → email-verified prospect via macaroon
- [ ] Listing CMS in Owner Cockpit
- [ ] kitchen-sink demo: 2 properties listed, inquiry submission flow end-to-end
- [ ] apps/docs entry covering listings + redaction + abuse posture

## Open Questions

| ID | Question | Resolution |
|---|---|---|
| OQ-PL1 | Custom domain per tenant LLC (`{tenantname}.bridge.sunfish.dev` vs custom) | Stage 02 — subdomain default; custom domain Phase 2.3 |
| OQ-PL2 | Image hosting + CDN: same blob store as Receipts/Inspections, or CDN-fronted? | Stage 02 — CDN-fronted publicly cacheable variant; private artifacts unchanged |
| OQ-PL3 | Listing photo licensing / ownership: tenant-uploaded only? | Stage 02 — yes |
| OQ-PL4 | Crawler / SEO posture: indexable, robots.txt, sitemap | Stage 02 — indexable; per-tenant noindex toggle |
| OQ-PL5 | Listing analytics: page views, inquiry conversion. Self-hosted or skipped? | Phase 2.3 — skip in 2.1d |

## Dependencies

**Blocked by:** Properties, Leasing Pipeline (Inquiry FK), Messaging Substrate (inquiry receipt notification), New "Public listing surface" ADR, ADR 0043 addendum
**Blocks:** Phase 2.1d deliverable

## Cross-references

- Sibling intakes: Properties, Leasing Pipeline, Messaging Substrate, Owner Cockpit
- ADR 0015, ADR 0043 (addendum), ADR 0049

## Sign-off

Research session — 2026-04-28
