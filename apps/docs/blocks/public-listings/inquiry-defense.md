# Inquiry-Form Defense

`Sunfish.Blocks.PublicListings` ships the server-side 5-layer inquiry-form defense per ADR 0059 §"Inquiry-form abuse posture (ADR 0043 T2 boundary)". The Bridge inquiry route runs every submission through this contract; only submissions that pass all configured layers are forwarded to ADR 0057's `IPublicInquiryService.SubmitInquiryAsync` (W#22).

## Layers

| Layer | Contract | Implementation | Status |
|---|---|---|---|
| **1 — CAPTCHA** | `ICaptchaVerifier` (W#28 Phase 3) | `InMemoryCaptchaVerifier` for tests; `RecaptchaV3CaptchaVerifier` (`providers-recaptcha`) for production | ✓ shipped |
| **2 — Rate limit** | `IInquiryRateLimiter` (Phase 5a) | `InMemoryInquiryRateLimiter` — sliding-window per-IP (5/hr) + per-tenant (50/hr) per ADR 0059 | ✓ shipped |
| **3 — Email + MX** | `IEmailMxResolver` (Phase 5a) | `MailAddress` format check + delegated DNS MX lookup; `StubEmailMxResolver` for tests | ✓ shipped |
| **4 — Abuse score** | `IInboundMessageScorer` (W#20) | (cross-substrate adaptation pending; XO beacon `cob-question-2026-04-30T03-58Z-w28-p5-w20-substrate-adaptation.md`) | gated |
| **5 — Manual triage** | `IUnroutedTriageQueue` (W#20) | (same cross-substrate adaptation) | gated |

`IInquiryFormDefense.EvaluateAsync` orchestrates the layers fail-closed; rejected submissions return `InquiryDefenseResult.Fail` with the rejecting layer + reason for audit emission.

## Audit emission (W#28 Phase 7)

When `InquiryFormDefense` is constructed with a `PublicListingAuditEmitter`, every rejection emits `AuditEventType.InquiryRejected` with body keys:

| Key | Value |
|---|---|
| `tenant` | The owning tenant id |
| `listing_id` | The listing id (or sentinel when not yet resolved at this layer) |
| `rejected_at_layer` | `Captcha` / `RateLimit` / `EmailFormatAndMx` / `AbuseScore` / `ManualTriage` |
| `reason` | Human-readable reason for the rejection |

Pass paths do not emit at the defense layer — `InquirySubmitted` is emitted by the leasing-pipeline boundary (W#22 `IPublicInquiryService`) once the inquiry is persisted.

## Composition example

```csharp
// Phase 3 substrate
var captcha = new RecaptchaV3CaptchaVerifier(httpClient, recaptchaConfig);

// Phase 5a substrates
var rate = new InMemoryInquiryRateLimiter();
var mx = new DnsMxResolver(); // production impl

// Phase 7 audit emitter
var emitter = new PublicListingAuditEmitter(auditTrail, signer, tenant);

// Defense orchestrator
var defense = new InquiryFormDefense(captcha, rate, mx, emitter);

// Bridge route (Phase 5c) calls Evaluate before forwarding to W#22
var verdict = await defense.EvaluateAsync(submission, ct);
if (!verdict.Passed)
{
    return BadRequest(new { layer = verdict.RejectedAt, reason = verdict.Reason });
}
await leasingPipeline.SubmitInquiryAsync(domainRequest, anonymousCapability, ct);
```

## See also

- [Overview](./overview.md)
- [Audit Emission](./audit-emission.md)
- [ADR 0059](../../../docs/adrs/0059-public-listing-surface.md) — Public listing surface
- [ADR 0043](../../../docs/adrs/0043-capability-tiers.md) — Anonymous / Prospect / Applicant tiers
