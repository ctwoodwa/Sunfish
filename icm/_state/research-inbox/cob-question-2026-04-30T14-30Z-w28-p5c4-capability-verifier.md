---
type: cob-question
workstream: 28
last-pr: 378
filed-by: COB
filed-at: 2026-04-30T14-30Z
---

# W#28 Phase 5c-4 — Prospect-capability verification path

## Context

Phase 5c-4 wires the capability-tier Bridge routes:

- `GET /listings/criteria/{token}` — Prospect-tier criteria document
- `POST /listings/criteria/{token}/start-application` — promotes to Applicant via `ILeasingPipelineService.SubmitApplicationAsync`

Both routes receive a `{token}` URL segment carrying the
`ProspectCapability.MacaroonToken` minted by Phase 4
(`MacaroonCapabilityPromoter`). Before serving either route, the Bridge
must verify the macaroon binds to the requesting tenant + the targeted
listing + a valid TTL.

## What's missing

Phase 4 ships the issuer (`MacaroonCapabilityPromoter` →
`IMacaroonIssuer.MintAsync`) but no complementary verifier for the
specific caveats the promoter writes:

- `capability-id = <guid>`
- `tenant = <slug>`
- `email = <addr>`
- `email-verified = true`
- `issued-from-ip = <ip>`
- `expires = <iso8601>`
- `listing-allowed = <listing-id>` (one per accessible listing)

The generic `Sunfish.Foundation.Macaroons.IMacaroonVerifier` evaluates
caveats against a `MacaroonContext`, but `MacaroonContext` only carries
`Now`, `SubjectUri`, `ResourceSchema`, `RequestedAction`, `DeviceIp` —
no tenant slug, no listing id, no email-verified expectation. Sunfish-
specific caveat semantics (e.g. parsing `tenant = X` and matching
against the requesting tenant) need a verifier-side mapping that
doesn't exist on `origin/main`.

## What COB needs from XO

Three options; default is A:

**Option A (default — add `IProspectCapabilityVerifier` in `blocks-public-listings`):**
Introduce a small block-local verifier that:
1. Decodes the base64url token via `MacaroonCodec.DecodeBase64Url`.
2. Calls `IMacaroonVerifier.VerifyAsync` with a `MacaroonContext` whose
   `Now = DateTimeOffset.UtcNow` (relies on the generic time caveat).
3. Parses the caveat list to extract `tenant` + `listing-allowed` set
   + `email`, and rejects if the requesting tenant or target listing
   id don't match the caveats.
4. Returns a `ProspectCapability` projection on success;
   `FieldDecryptionDeniedException`-style typed denial on failure.

Pros: Phase 5c-4 lands in 1 PR; verifier surface lives next to the
issuer; no foundation changes; matches the W#32 audit-disabled-vs-enabled
overload pattern.

Cons: Caveat parsing is duplicated logic against the issuer's caveat
shape (drift risk if the promoter ever changes caveat names).

**Option B (extend `MacaroonContext` with Sunfish-specific fields):**
Add `TenantSlug`, `RequestedListingId` (etc.) to `MacaroonContext` and
generalize the foundation verifier to evaluate caveats with these
predicates. Touches foundation surface; affects every macaroon
consumer (federation-pattern-c too).

Pros: Single canonical verifier for every macaroon use site.

Cons: Foundation api-change pipeline; ripple into existing
federation-pattern-c tests; out of W#28 scope.

**Option C (defer the GET criteria route; ship only POST start-application):**
The `start-application` POST is still mostly substantive (form fields
+ `SubmitApplicationRequest` mapping); the GET criteria page is
informational and can be a no-auth public page that doesn't require
the macaroon. Capability-tier rendering of `criteria/{token}` would
ship in a follow-up.

Pros: Avoids the verifier question entirely for now.

Cons: Doesn't match the ADR 0059 §"Capability promotion" spec which
specifically scopes criteria to Prospect tier.

## What I shipped before halting

W#28 substrate is end-to-end functional in dev/demo via PRs #375 +
#376 + #377 + #378 (Phases 5b/5c-1/5c-2/5c-3 covering robots.txt +
sitemap.xml + index/detail SSR + inquiry POST). Phase 5c-4 routes are
not yet shipped because the verifier seam is ambiguous.

## What unblocks Phase 5c-4

XO direction on Options A/B/C above. Phase 5c-4 ships within ~1.5–3h
once the path is clear.

## Cross-references

- W#28 hand-off:
  `icm/_state/handoffs/property-public-listings-stage06-handoff.md`
  §"Phase 5 — Inbound 5-layer defense + Bridge route family"
- W#28 Phase 5b unblock addendum (precedent for cross-substrate
  adaptation):
  `icm/_state/handoffs/property-public-listings-stage06-phase5b-addendum.md`
- Issuer:
  `packages/blocks-public-listings/Capabilities/MacaroonCapabilityPromoter.cs`
- Macaroon substrate: `packages/foundation/Macaroons/`
