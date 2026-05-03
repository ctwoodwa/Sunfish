# Capability-Tier Flow (Anonymous → Prospect → Applicant)

The W#28 public-listings surface implements ADR 0043's macaroon-capability gradient end-to-end. A web visitor moves through three tiers as their identity is progressively verified, and the surface they see at each tier is structurally enforced.

## The three tiers

```
Anonymous           Prospect              Applicant
   │                   │                     │
   ▼                   ▼                     ▼
GET /listings    GET /listings/criteria  POST /listings/criteria/
                 /{token}                /{token}/start-application
   │                   │                     │
   ▼                   ▼                     ▼
RedactionTier.   RedactionTier.        SubmitApplicationRequest
Anonymous        Prospect              → Application persisted
```

| Tier | Identity proof | Token | TTL |
|---|---|---|---|
| Anonymous | None | None | n/a |
| Prospect | Email-verified via verification flow | `ProspectCapability.MacaroonToken` (base64url) | 7 days |
| Applicant | Application persisted + payment + signature | (Applicant capability — issued by W#22 `ConfirmApplicationAndPromoteAsync`) | varies |

## The macaroon issuer + verifier pair

Every tier transition is mediated by a macaroon. The issuer + verifier are paired (canonical Sunfish substrate pattern from W#21 + W#32):

- `MacaroonCapabilityPromoter.PromoteToProspectAsync` — mints a token after email verification
- `IProspectCapabilityVerifier.VerifyAsync` — verifies a token before serving any Prospect-tier surface

The verifier is **block-local**, not generic. Sunfish-specific caveats (`tenant`, `email-verified`, `listing-allowed`, `expires`) fall outside `Sunfish.Foundation.Macaroons.FirstPartyCaveatParser`'s grammar — calling the generic verifier on a promoter-minted token would fail-closed on every caveat. The block-local `MacaroonProspectCapabilityVerifier` performs:

1. Decode base64url → `Macaroon`
2. Signature-chain check via `MacaroonCodec.ComputeChain` + `IRootKeyStore` (no caveat evaluation; pure HMAC chain compare)
3. Block-local Sunfish-caveat parsing (`tenant`, `listing-allowed`, `email-verified`, `expires`, `email`, `capability-id`)
4. Project the verified shape as `VerifiedProspectCapability { CapabilityId, Tenant, Email, ExpiresAt, AllowedListings }`

`ProspectCaveatNames` (internal const class) is the single source of truth for caveat keys — both issuer and verifier consume from the same constants to prevent drift.

## Bridge route family

Five Bridge endpoints implement the lifecycle:

### Anonymous tier

- `GET /robots.txt` — `Allow: /listings`, `Disallow: /listings/criteria/`
- `GET /sitemap.xml` — enumerates only `Published` listings for the requesting tenant
- `GET /listings` — index of every `Published` listing rendered at `RedactionTier.Anonymous`
- `GET /listings/{slug}` — detail with schema.org `Accommodation` JSON-LD + OpenGraph (XSS-defended via `HtmlEncoder` and `</` → `<\/` escape inside JSON-LD)

### Inquiry submission (Anonymous → email-verification flow)

- `POST /listings/{slug}/inquiry` — runs the 5-layer inquiry-form defense (CAPTCHA / rate-limit / email+MX / abuse-score / manual triage), mints an `AnonymousCapability` (30-min TTL), forwards to `IPublicInquiryService.SubmitInquiryAsync`
- The email-verification flow (out of scope for this doc) verifies the email and triggers `ICapabilityPromoter.PromoteToProspectAsync` to issue the Prospect macaroon

### Prospect tier

- `GET /listings/criteria/{token}` — verifies the token, then renders the prospect's `AllowedListings` at `RedactionTier.Prospect` (more financial detail than the Anonymous index). `<meta name="robots" content="noindex, nofollow">` — capability-tier pages must not be search-indexed.

### Prospect → Applicant transition

- `POST /listings/criteria/{token}/start-application` — verifies the token against the form-targeted listing, looks up the `Prospect` by `email` via `ILeasingPipelineService.GetProspectByEmailAsync`, constructs `SubmitApplicationRequest` (carrying `DecisioningFacts` + `DemographicProfileSubmission` + `ApplicationFee` + `SignatureEventRef`), invokes `SubmitApplicationAsync`, returns `202 Accepted` with the application id.

The orphaned-capability case (verified token + missing `Prospect` row) returns `410 Gone` rather than 401 — the capability is not invalid, but the resource it referenced has been deleted (a data-inconsistency signal worth distinguishing from auth failure).

## Denial taxonomy

Every Prospect-tier route returns `401 Unauthorized` on capability denial with the reason surfaced in `ProblemHttpResult.Detail`. The macaroon's signature gate prevents replay-as-error-oracle abuse — there is no information leak in surfacing the denial reason.

| Reason | Cause |
|---|---|
| `decode failed: ...` | Token is not valid base64url-encoded macaroon |
| `no-root-key for location '...'` | Issuer's root key isn't registered for verification |
| `signature-mismatch` | HMAC chain doesn't match — token was tampered or issued by a different key |
| `wrong-tenant: ...` | Caveat tenant ≠ requesting tenant |
| `listing-not-in-allowed-set: ...` | The route's listing isn't in the caveat's `listing-allowed` set |
| `email-not-verified` | Caveat states `email-verified = false` |
| `expired: ...` | Now > caveat `expires` |
| `missing-caveat: ...` | Required caveat absent |
| `unknown-caveat-key: ...` | Unrecognized caveat key |

## Audit emission

Every verification + denial emits a `kernel-audit` record:

- `AuditEventType.ProspectCapabilityVerified` (success)
- `AuditEventType.ProspectCapabilityDenied` (any rejection — `Reason` field carries the taxonomy entry above)
- `AuditEventType.ProspectStartedApplication` (Slice C `start-application`)
- `AuditEventType.ProspectLookupOrphan` (verified email + missing `Prospect` row)

## See also

- [Inquiry-Form Defense](./inquiry-defense.md) — 5-layer abuse-defense pipeline (Anonymous tier)
- [Audit Emission](./audit-emission.md) — full audit-event taxonomy for the public-listings substrate
- [ADR 0043 addendum](../../../docs/adrs/0043-capability-gradient.md) — capability-tier framework
- [ADR 0059](../../../docs/adrs/0059-public-listing-surface.md) — public-listing surface architecture
- [ADR 0032](../../../docs/adrs/0032-multi-team-anchor-workspace-switching.md) — macaroon substrate
