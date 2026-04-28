# ADR 0051 — Foundation.Integrations.Payments

**Status:** Proposed
**Date:** 2026-04-28
**Resolves:** Phase 2 commercial intake placeholder for ADR 0051 (`Foundation.Integrations.Payments`); cluster intakes Receipts (#26), Work Orders (#19), Vendors (#18), Leasing Pipeline (#22) all reference payment processing as out-of-scope-per-this-intake-and-resolved-by-ADR-0051. Existing `packages/blocks-rent-collection/Models/Payment.cs` notes "Rounding enforcement is deferred to a follow-up" + "Plaid/Stripe integration is deferred" — this ADR is the follow-up.

---

## Context

Sunfish ships a `blocks-rent-collection` package today with `Invoice`, `Payment`, `BankAccount`, `RentSchedule`, and `LateFeePolicy` records as block-level domain types. The `Payment` record uses bare `decimal Amount` with an explicit `"Rounding enforcement is deferred to a follow-up"` doc comment, and `Method` as an opaque string `"cash"|"check"|"ach"|"card"` with `"No enum enforced in this pass — Plaid/Stripe integration is deferred"`. These are placeholders waiting for a payments substrate.

Phase 2 commercial intake (`phase-2-commercial-mvp-intake-2026-04-27.md` §"Scope Statement" item 2) reserves ADR 0051 for *"`Foundation.Integrations.Payments` (payment-specific extensions to ADR 0013: money type, payment state machine, PCI scope discipline, refund authorization, ACH return handling, 3DS/SCA)"*. The intake names `providers-stripe` as the first payment processor adapter, gating on this ADR. Phase 2 commercial work cannot proceed without it: rent collection (lease-holder ACH/card), vendor payments (outbound), application fees (Leasing Pipeline), bank reconciliation (Phase 2 commercial intake's `blocks-accounting`), and tax-prep (vendor 1099-NEC aggregation) all consume payment substrate.

The property-operations cluster surfaced four additional consumer points: Receipts (asset-acquisition cost basis), Work Orders (vendor invoice → payment → completion artifact), Vendors (1099-NEC year-end reporting), Leasing Pipeline (FCRA-compliant application fees + adverse-action timing). All five Phase 2 needs share the same provider adapter (likely Stripe) and the same Money + state-machine vocabulary; resolving them as one substrate is cheaper than five ad-hoc payment integrations.

ADR 0013's provider-neutrality enforcement gate (workstream #14, PR #196 merged 2026-04-28 14:35Z) is mechanically active. `providers-stripe` must comply: `blocks-rent-collection`, `blocks-receipts`, `blocks-vendors`, `blocks-work-orders`, `blocks-leasing-pipeline` cannot reference `Stripe.*` directly; the substrate this ADR specifies is the only legal seam.

PCI compliance is the load-bearing security constraint. Sunfish targets **PCI SAQ-A** scope: we never see PAN (Primary Account Number) or CVV; payment data flows through provider-hosted tokenization (Stripe Elements / Stripe Checkout / equivalent). We persist tokens, last-4-of-card, payment-method type, and billing PII — never primary card data. The substrate must structurally prevent SAQ-A scope creep into SAQ-D (which would require quarterly ASV scans, annual ROC, dedicated PCI engineer, six-figure compliance overhead).

---

## Decision drivers

- **Existing block-level placeholders need replacement.** `blocks-rent-collection/Models/Payment.cs` documents the deferred work explicitly; this ADR is what the comments wait for.
- **Five Phase 2 consumers** depend on this substrate: rent collection, vendor payments, application fees, receipts cost-basis, vendor 1099-NEC.
- **PCI SAQ-A scope is non-negotiable.** Substrate must structurally prevent PAN/CVV from entering Sunfish's persistence or contract surfaces.
- **ADR 0013 enforcement gate is active.** `providers-stripe` (and any alternative) must comply. `blocks-*` cannot reference vendor SDKs.
- **Phase 2 is USD-only.** BDFL property business is single-currency. Multi-currency is a real future need but not a Phase 2 forcing function — defer the conversion / FX subsystem to a follow-up ADR.
- **Audit-trail integration is mandatory.** Every charge, capture, refund, ACH return is a first-class audit record per ADR 0049.
- **State-machine fidelity matters.** Payment state is *not* a simple enum: ACH returns can fire 60+ days after settlement; chargebacks/disputes have multi-week SLAs; SCA challenges introduce mid-flow user redirects. Substrate must model these without leaking provider-specific quirks into block code.

---

## Considered options

### Option A — Narrow Phase 2 contracts; expand later [RECOMMENDED]

Ship `Foundation.Integrations.Payments` with the minimum surface Phase 2 actually needs: `Money` type (USD-only Phase 2; ISO 4217 currency tag for forward-compat), `PaymentMethodReference` (provider token + last-4 + method class; never PAN), `IPaymentGateway` with charge/capture/refund/void, payment state machine including ACH-return + dispute substates, audit-record types per ADR 0049 + 3DS/SCA challenge primitives. Defer multi-currency conversion, recurring-billing primitives (those live in `blocks-billing` / future `Sunfish.Foundation.Billing`), split tender, marketplace flows.

- **Pro:** Mirrors ADR 0013's "minimal contracts; complexity in adapters" philosophy.
- **Pro:** Phase 2 unblocks immediately; expansion is additive (no breaking change required).
- **Pro:** Smaller surface = smaller PCI scope review.
- **Con:** Future multi-currency / billing-class consumers will pressure-test the abstractions; some refactoring possible.

**Verdict:** Recommended. Mirrors the validated ADR 0013 pattern.

### Option B — Comprehensive payments substrate now

Build for SaaS-marketplace scale: multi-currency + FX, recurring billing primitives, split tender, marketplace transfers (Stripe Connect-style), payouts, tax calculation hooks, refund authorization workflows.

- **Pro:** Avoids future refactoring as scope expands.
- **Con:** Massive scope explosion for Phase 2 (BDFL property business has 10–30 leases; not a marketplace).
- **Con:** Larger PCI surface — every additional flow gets PCI scoping review.
- **Con:** YAGNI risk: `blocks-billing` may want different shape than today's intuition.

**Verdict:** Rejected. Designs for hypothetical scale at the cost of Phase 2 deliverable timing.

### Option C — Stripe-shaped contracts

Take Stripe's API surface as the substrate (Charge, PaymentIntent, SetupIntent, Customer, PaymentMethod, Refund) and call them Sunfish names.

- **Pro:** Zero impedance mismatch with Stripe adapter.
- **Con:** Violates ADR 0013's second rule: *"Domain concepts are Sunfish-modeled, not vendor-mirrored. A `Payment` domain entity is not a Stripe `Charge`."*
- **Con:** Adyen / Square / Braintree adapters become contortions.
- **Con:** Stripe API evolution propagates as breaking changes through Sunfish contracts.

**Verdict:** Rejected. Architectural violation of the provider-neutrality rule whose enforcement gate just landed.

---

## Decision

**Adopt Option A.** Ship `Sunfish.Foundation.Integrations.Payments` with narrow Phase 2 contracts. Provider adapters carry vendor complexity. Multi-currency, recurring billing, marketplace flows are out-of-scope follow-ups.

### Initial contract surface

```csharp
namespace Sunfish.Foundation.Integrations.Payments;

// Money — currency-bound decimal; replaces bare `decimal Amount` placeholders
public readonly record struct Money(decimal Amount, CurrencyCode Currency)
{
    public static Money Usd(decimal amount) => new(amount, CurrencyCode.USD);

    // Phase 2 invariants (enforced at construction):
    // - Amount may be negative (refunds); zero allowed; NaN/Infinity rejected
    // - Currency must be a known ISO 4217 code
    // - Operators (+, -, ==) require matching currencies; throw on mismatch
    // - Rounding: banker's (MidpointRounding.ToEven) when conversion required
    // - Display formatting: provider-side (en-US default; locale via I18n)
}

public readonly record struct CurrencyCode(string Iso4217)
{
    public static CurrencyCode USD => new("USD");
    public static CurrencyCode EUR => new("EUR"); // forward-compat; not Phase 2
    // Validates against an ISO 4217 allow-list at construction
}

// PaymentMethodReference — opaque token from provider; PCI-safe
public sealed record PaymentMethodReference
{
    public required TenantId Tenant { get; init; }
    public required string ProviderKey { get; init; }            // e.g., "stripe"
    public required string ProviderTokenId { get; init; }        // pm_xxx, payment_method_id; opaque
    public required PaymentMethodClass Class { get; init; }      // Card | UsBankAccount | DigitalWallet | …
    public required string DisplayLast4 { get; init; }           // "4242"
    public required string DisplayBrand { get; init; }           // "Visa", "ACH", "Apple Pay"
    public DateTimeOffset? ExpiresAt { get; init; }              // card expiry; null for ACH
    public BillingAddress? BillingAddress { get; init; }         // PII; per-tenant-key encrypted
    // Explicitly NOT: PAN, CVV, full card number, raw track data — those are SAQ-D scope
}

public enum PaymentMethodClass { Card, UsBankAccount, DigitalWallet, External }

// IPaymentGateway — primary contract
public interface IPaymentGateway
{
    Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct);
    Task<CaptureResult> CaptureAsync(ChargeId charge, Money? partialAmount, CancellationToken ct);
    Task<RefundResult> RefundAsync(ChargeId charge, RefundRequest request, CancellationToken ct);
    Task<VoidResult> VoidAsync(ChargeId charge, CancellationToken ct);
    Task<ChargeStatus> GetStatusAsync(ChargeId charge, CancellationToken ct);
}

public sealed record ChargeRequest
{
    public required TenantId Tenant { get; init; }
    public required Money Amount { get; init; }
    public required PaymentMethodReference PaymentMethod { get; init; }
    public required CaptureMode CaptureMode { get; init; }       // ImmediateCapture | AuthorizeOnly
    public required string IdempotencyKey { get; init; }         // mandatory; exactly-once semantics
    public required AuditCorrelation Audit { get; init; }
    public string? StatementDescriptor { get; init; }            // shows on cardholder's statement
    public string? OrderReference { get; init; }                 // FK to Invoice/WorkOrder/etc.
    public ScaChallengeAffordance ScaAffordance { get; init; } = ScaChallengeAffordance.AutoFollow;
}

public enum CaptureMode { ImmediateCapture, AuthorizeOnly }

public enum ScaChallengeAffordance
{
    AutoFollow,        // gateway redirects user-agent through SCA challenge
    DeferToCaller,     // gateway returns ChallengeRequired; caller drives the flow
    RejectIfRequired   // ACH-only flows where SCA is not relevant; reject if challenge fires
}

public sealed record ChargeResult
{
    public required ChargeId ChargeId { get; init; }
    public required ChargeStatus Status { get; init; }
    public required Money AmountAuthorized { get; init; }
    public required Money? AmountCaptured { get; init; }         // null if AuthorizeOnly
    public ScaChallenge? PendingChallenge { get; init; }         // present iff Status = AwaitingScaChallenge
    public required IReadOnlyDictionary<string,string> ProviderMetadata { get; init; }
}

public enum ChargeStatus
{
    Authorized,             // funds on hold; capture pending (CaptureMode=AuthorizeOnly)
    Captured,               // funds captured; settlement pending
    Settled,                // funds in our account; ACH-return window may still apply
    AwaitingScaChallenge,   // user must complete SCA before charge can advance
    Failed,                 // declined / blocked / SCA-rejected
    Voided,                 // pre-capture cancellation
    Refunded,               // post-capture full refund
    PartiallyRefunded,
    Disputed,               // chargeback initiated
    AchReturned             // ACH transaction reversed by issuing bank (R-code)
}

// ACH-specific
public sealed record AchReturnEvent
{
    public required ChargeId ChargeId { get; init; }
    public required string ReturnCode { get; init; }            // R01–R85 NACHA codes
    public required string ReturnReason { get; init; }
    public required DateTimeOffset ReturnedAt { get; init; }
    public required AuditCorrelation Audit { get; init; }
}

// Refunds
public sealed record RefundRequest
{
    public required Money Amount { get; init; }                 // partial allowed
    public required string Reason { get; init; }                // free-text + audit
    public required string IdempotencyKey { get; init; }
    public required AuditCorrelation Audit { get; init; }
}

// Disputes / chargebacks
public sealed record DisputeEvent
{
    public required ChargeId ChargeId { get; init; }
    public required string DisputeReason { get; init; }
    public required Money DisputedAmount { get; init; }
    public required DateTimeOffset OpenedAt { get; init; }
    public required DateTimeOffset RespondByAt { get; init; }
    public required IReadOnlyList<string> EvidenceRequested { get; init; }
}

// SCA challenge representation (3DS, EU PSD2)
public sealed record ScaChallenge
{
    public required string ChallengeId { get; init; }
    public required Uri RedirectUri { get; init; }              // user-agent redirects here
    public required Uri ReturnUri { get; init; }                // gateway calls back here on completion
    public required DateTimeOffset ExpiresAt { get; init; }
}

public readonly record struct ChargeId(string Value);
```

(Schema sketch only; XML doc + nullability + `required` + struct vs record decisions firmed at Stage 06.)

### Provider adapter pattern (mandatory per ADR 0013)

```
blocks-rent-collection / blocks-receipts / blocks-vendors / blocks-work-orders / blocks-leasing-pipeline
  ↓ depends on (contracts only)
Foundation.Integrations.Payments
  ↑ implemented by
providers-stripe (first; CC + ACH + 3DS via Stripe Elements/Checkout/SetupIntents)
providers-adyen (future; Phase 4+)
providers-square (future; Phase 4+)
  ↓ uses
Vendor SDK (Stripe.NET)
```

`SUNFISH_PROVNEUT_001` analyzer enforces: no `using Stripe;` in any `blocks-*`. The exclusion list (`Sunfish.Foundation.Integrations` + test projects) does NOT include `blocks-rent-collection` or any other domain block — vendor leakage there is a build error.

### PCI SAQ-A scope discipline (load-bearing constraint)

Substrate is structurally PCI-SAQ-A-shaped:

1. **No PAN / CVV in any contract.** `PaymentMethodReference` holds opaque provider tokens, last-4, brand, billing address. Compile-time impossible to persist primary card data because no contract type carries it.
2. **Tokenization-first wire format.** Every charge originates from a `PaymentMethodReference` (a provider-tokenized handle). Card capture happens entirely in provider-hosted UI surfaces (Stripe Elements / Stripe Checkout / equivalent). Sunfish-hosted forms never collect PAN/CVV.
3. **Webhook surface inherits messaging substrate's signature verification (ADR 0052).** Provider-initiated payment events (`charge.succeeded`, `charge.dispute.created`, `charge.refund.updated`) arrive as `WebhookEventEnvelope`s with provider-specific signature verify before persistence.
4. **Audit-trail records redact sensitive fields.** Audit-substrate (ADR 0049) projections store charge ID, status, audit metadata — never `ProviderTokenId`, billing address, or any payment-method detail beyond `Last4` + `Brand`.
5. **Per-tenant credential resolution per ADR 0013.** `CredentialsReference` for Stripe API keys; concrete secrets resolution via secrets-management adapter (separate ADR; not blocking this one).

This is SAQ-A-class scope: hosted payment page + tokenization. SAQ-A-EP applies if Sunfish-served pages embed the provider iframe directly; SAQ-A applies if the provider serves the entire payment page. Both are within reach of the substrate as designed.

### Audit-substrate integration (ADR 0049)

Every charge / capture / refund / void / dispute / SCA / ACH-return event emits a typed audit record:

| Audit record type | Emitted on |
|---|---|
| `PaymentChargeRequested` | `IPaymentGateway.ChargeAsync` invoked |
| `PaymentAuthorized` | Provider returns `Authorized` |
| `PaymentCaptured` | Provider returns `Captured` |
| `PaymentSettled` | Settlement webhook |
| `PaymentScaChallengeIssued` | Status = `AwaitingScaChallenge` |
| `PaymentScaChallengeCompleted` | User completed SCA flow |
| `PaymentFailed` | Decline / SCA-rejected / blocked |
| `PaymentRefunded` / `PaymentPartiallyRefunded` | Refund applied |
| `PaymentDisputed` | Chargeback initiated |
| `PaymentDisputeResolved` | Won / lost / accepted |
| `AchReturnReceived` | NACHA R-code event |
| `PaymentMethodAttached` | New `PaymentMethodReference` added |
| `PaymentMethodDetached` | Removed |

Audit records carry `ChargeId`, `TenantId`, status transition, audit metadata — never sensitive payment data.

### Money type semantics

- **`decimal Amount`** with **`CurrencyCode Currency`** — storing decimal directly, not minor-units integer. Rationale: existing `blocks-rent-collection/Models/Payment.cs` uses `decimal`; matching avoids a downstream migration. Decimal precision is sufficient for 1¢ accuracy at amounts up to ~7.9 × 10²⁸.
- **Banker's rounding (`MidpointRounding.ToEven`)** when rounding required. Matches typical accounting convention; matches `Math.Round` default in .NET.
- **Operator enforcement.** `Money.+`, `Money.-`, comparison: throw `CurrencyMismatchException` if currencies differ. No silent FX conversion.
- **Negative amounts allowed.** Refunds + adjustments need them.
- **NaN / Infinity rejected.** Throw at construction.
- **Phase 2 uses USD only.** `CurrencyCode.USD` is the only practical value; `CurrencyCode.EUR` etc. compile but no real consumer until multi-currency follow-up ADR.

### What this ADR does NOT do

- **Does not define multi-currency conversion / FX.** Future ADR. Operators throw on mismatch; no implicit conversion.
- **Does not define recurring billing primitives** (subscription, scheduled charges, dunning). Those live in future `Sunfish.Foundation.Billing` or `blocks-billing` work. This ADR's `IPaymentGateway` is one-shot only.
- **Does not define split tender** (multi-method single-payment, like card + gift-card). Future.
- **Does not define marketplace transfers** (Stripe Connect-style payout to third parties). Future.
- **Does not define tax-calculation hooks** (sales tax, VAT). Future; `Foundation.Integrations.Tax` is its own potential ADR.
- **Does not define payout / settlement-reconciliation flows.** Phase 2 commercial intake's `blocks-accounting` deliverable owns reconciliation; this ADR's audit records feed it.
- **Does not define PCI compliance certification scope.** SAQ-A is the *target*; the substrate enables it but compliance certification is an organizational concern, not a code concern.

---

## Consequences

### Positive

- Block-level placeholders in `blocks-rent-collection/Models/Payment.cs` (decimal+rounding-deferred, opaque-string-method) are replaced by structured Money + PaymentMethodReference contracts.
- Five Phase 2 consumers unblock simultaneously on acceptance.
- ADR 0013's enforcement gate gets a second high-fidelity test (after ADR 0052 messaging substrate).
- PCI SAQ-A scope is structural, not policy. The substrate cannot accidentally carry PAN/CVV because no contract type holds it.
- Audit-trail integration is uniform across charge, refund, dispute, SCA, ACH-return.
- ACH-return + dispute + SCA modeled at the substrate level — no provider-specific quirks leak into block code.
- Money + CurrencyCode are forward-compat for multi-currency without breaking changes.

### Negative

- `blocks-rent-collection/Models/Payment.cs` requires a migration: the `decimal Amount` field becomes `Money Amount`; the `string Method` becomes `PaymentMethodReference Method`. Existing tests + JSON serialization update accordingly. (Mitigation: comment in the file already flags this work as deferred; no production callers outside the test suite.)
- 13 audit record types is a lot; reviewers must keep them coherent with ADR 0049 substrate.
- `IPaymentGateway` is one-shot; consumers building recurring billing on top need an additional substrate (future ADR), not this one.
- SCA challenge flow introduces async user-agent redirects mid-flow; state machine includes `AwaitingScaChallenge`; consumers must handle the resume case.
- Phase 2 USD-only feels narrow; first international tenant onboarding will require multi-currency follow-up ADR before they can transact.

### Trust impact / Security & privacy

- **PCI scope is the binding constraint.** Substrate is designed to keep Sunfish in PCI SAQ-A. SAQ-A means:
  - We never see / persist PAN, CVV, full track data
  - We use provider-hosted tokenization for card capture
  - Our database has tokens, last-4, brand, billing PII — NEVER primary card data
  - Quarterly ASV scans not required (a key cost differentiator vs SAQ-D)
- **Billing address is PII.** Stored under per-tenant key encryption (Foundation.Recovery primitives once workstream #15 lands).
- **Audit records redact sensitive fields.** Charge ID + status + metadata only.
- **Provider webhook signature verification mandatory.** Same posture as ADR 0052 messaging substrate; reuses webhook envelope shape from ADR 0013.
- **Idempotency keys mandatory on every charge / refund.** Prevents accidental double-charge under retries.
- **Per-tenant API key isolation.** Stripe API key for Tenant A is not usable by Tenant B's host. Resolved via `CredentialsReference` per ADR 0013.

---

## Compatibility plan

### Existing callers / consumers

`packages/blocks-rent-collection/Models/Payment.cs` is the only existing block-level type that this ADR replaces fields on:

```csharp
// Before
public sealed record Payment(
    PaymentId Id, InvoiceId InvoiceId, decimal Amount,
    Instant PaidAtUtc, string Method, string? Reference);

// After
public sealed record Payment(
    PaymentId Id, InvoiceId InvoiceId, Money Amount,
    Instant PaidAtUtc, PaymentMethodReference Method, string? Reference);
```

Migration is a single-file edit with corresponding test + JSON-serialization-converter updates. The doc comments in the existing file already flag this as expected work (`"Rounding enforcement is deferred to a follow-up"`, `"No enum enforced in this pass — Plaid/Stripe integration is deferred"`).

### Affected packages (new + modified)

| Package | Change |
|---|---|
| `packages/foundation-integrations` | **Modified** — adds `Sunfish.Foundation.Integrations.Payments` namespace |
| `packages/blocks-rent-collection` | **Modified** — `Payment` record fields replace `decimal Amount` → `Money Amount`; `string Method` → `PaymentMethodReference Method` |
| `packages/providers-stripe` | **New** — first payment processor adapter; CC + ACH + 3DS via Stripe Elements / Checkout / SetupIntents |
| `packages/blocks-billing` (existing P2) | **Eventual consumer** — recurring billing engine integration; out of scope for this ADR |
| `packages/blocks-receipts` (cluster intake #26, future) | **Eventual consumer** |
| `packages/blocks-vendors` (cluster intake #18, future) | **Eventual consumer** for outbound vendor payments + 1099-NEC aggregation |
| `packages/blocks-work-orders` (cluster intake #19, future) | **Eventual consumer** for work-order completion → invoice → payment flow |
| `packages/blocks-leasing-pipeline` (cluster intake #22, future) | **Eventual consumer** for application fees |

### Migration

`blocks-rent-collection.Payment` field-shape migration is a single-file edit + test update. No production data exists yet (block has no live tenants at the time of this ADR), so JSON serialization breakage is non-blocking. Migration guide for the field rename lives in the ADR's implementation checklist.

---

## Implementation checklist

- [ ] `Sunfish.Foundation.Integrations.Payments` namespace added to `packages/foundation-integrations/` with full XML doc + nullability annotations
- [ ] `Money` struct + `CurrencyCode` struct + ISO 4217 allow-list validation
- [ ] `PaymentMethodReference` record (PCI-safe; no PAN/CVV; tokens only)
- [ ] `IPaymentGateway` interface (charge / capture / refund / void / status)
- [ ] `ChargeRequest` / `ChargeResult` / `RefundRequest` / `RefundResult` / `VoidResult` / `ScaChallenge` / `AchReturnEvent` / `DisputeEvent` records
- [ ] `ChargeStatus` enum with all 11 states
- [ ] In-memory reference `IPaymentGateway` shipped in `foundation-integrations` for tests/demos (returns `Captured` on every call; minimal stub)
- [ ] Audit record types added to `Sunfish.Kernel.Audit` per ADR 0049 (13 types per the table above)
- [ ] `packages/providers-stripe` scaffolded — CC + ACH + 3DS support against Stripe sandbox; `IPaymentGateway` implementation; webhook signature verification; idempotency-key passthrough
- [ ] Provider-neutrality analyzer (`SUNFISH_PROVNEUT_001`) passes on `blocks-rent-collection` after `Stripe.*` would be banned (build fails if violated — gate already active per workstream #14)
- [ ] `blocks-rent-collection/Models/Payment.cs` migrated: `decimal Amount` → `Money Amount`; `string Method` → `PaymentMethodReference Method`; corresponding test + JSON converter updates
- [ ] Documentation note in `apps/docs` covering substrate + PCI SAQ-A scope discipline + provider-selection guidance + sandbox setup for Stripe testing
- [ ] kitchen-sink demo: Phase 2 commercial path-of-rent (lease-holder pays rent → Stripe Elements tokenizes → IPaymentGateway.ChargeAsync → audit-record emitted → blocks-rent-collection invoice marked paid)
- [ ] Idempotency-key generation pattern documented (recommend: tenant-scoped + business-key-derived; e.g., `tenant:{tid}:invoice:{iid}:charge:{n}`)
- [ ] Phase 2 commercial intake updated to reference Accepted ADR 0051 (one-line edit; chore-class follow-up commit after acceptance)

---

## Open questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-P1 | Money storage: `decimal` (current) vs `long` minor-units (banking convention). | Stage 02. Recommend `decimal` to match existing `Payment.Amount` field; revisit if precision issue surfaces. |
| OQ-P2 | Multi-currency timing — first international tenant. | Phase 4+ separate ADR; substrate forward-compat (CurrencyCode struct already in shape). |
| OQ-P3 | Recurring billing primitives — `Foundation.Billing` ADR vs extending this one. | Separate future ADR (`Foundation.Billing` or expand `blocks-billing`); this ADR is one-shot only. |
| OQ-P4 | First payment processor selection: Stripe vs Adyen vs Square. | Phase 2 commercial intake names Stripe as recommended; confirm. |
| OQ-P5 | ACH return monitoring window. ACH returns can fire 60+ days post-clearing. How long does substrate hold "potentially returnable" status? | Stage 02 — recommend 90 days as safe default; reconciliation flow in `blocks-accounting` watches the window. |
| OQ-P6 | SCA challenge resume flow. AwaitingScaChallenge is async; user might close browser mid-challenge. Recovery path? | Stage 02 — store challenge ID + return URI in audit; cron job + provider polling reconciles abandoned challenges after `ScaChallenge.ExpiresAt`. |
| OQ-P7 | Refund reason taxonomy. Free-text + audit (current proposal) vs structured enum (Stripe-style: `requested_by_customer`, `duplicate`, `fraudulent`). | Stage 02 — free-text + audit for Phase 2; structured taxonomy when third payment provider lands and forces normalization. |
| OQ-P8 | PCI SAQ-A vs SAQ-A-EP. SAQ-A-EP applies if Sunfish pages serve provider iframe; SAQ-A applies if provider serves entire page. Default per UX. | Stage 02. Recommend SAQ-A-EP (Stripe Elements embedded in Sunfish payment forms) as Phase 2.1 default; SAQ-A available for tenants wanting the simpler scope. |
| OQ-P9 | Vendor 1099-NEC reporting: substrate emits payment events; aggregation in `blocks-tax-reporting`. Where does TIN-to-payments mapping live? | Cluster Vendors intake (#18) owns vendor identity + TIN; this ADR's audit records carry vendor reference; aggregation is `blocks-tax-reporting` Phase 2.3. |

---

## Revisit triggers

This ADR should be re-evaluated when any of the following fire:

- **First international tenant** requires non-USD transactions. Multi-currency conversion ADR + substrate amendment.
- **Recurring billing requirement** (subscriptions, scheduled charges, dunning) crosses in-scope threshold. `Foundation.Billing` ADR + substrate composition.
- **Marketplace flows** (third-party payouts, Stripe Connect-style) requested by a real customer. Connect-class substrate.
- **Second payment provider adapter** (Adyen / Square / Braintree) reveals abstraction-leak in the contracts; refactor.
- **PCI scope creep** — any contract change that risks SAQ-D escalation. Architectural review mandatory.
- **A regulated vertical onboards** (cannabis, adult content, firearms) where payment processor refusal becomes a real constraint; multi-provider failover.
- **Stripe API breaking change** — historically rare but possible; adapter migration triggers ADR review for any leaked Stripe-isms.
- **Block-level consumer exceeds five** — substrate-level refactor warranted if the abstraction is straining.

---

## References

### Predecessor and sister ADRs

- [ADR 0007](./0007-bundle-manifest-schema.md) — `ProviderCategory` vocabulary; `Payments` is one of those categories.
- [ADR 0008](./0008-foundation-multitenancy.md) — Per-tenant credential resolution + audit scope.
- [ADR 0013](./0013-foundation-integrations.md) — Provider-neutrality. This ADR is the second major exercise of providers-* pattern (after ADR 0052 messaging substrate); enforcement gate (workstream #14, PR #196) is mechanically active.
- [ADR 0015](./0015-module-entity-registration.md) — `ISunfishEntityModule`.
- [ADR 0046](./0046-key-loss-recovery-scheme-phase-1.md) — Per-tenant credential encryption uses Foundation.Recovery primitives once workstream #15 lands.
- [ADR 0049](./0049-audit-trail-substrate.md) — Audit substrate; 13 payment audit record types emit per the table above.
- [ADR 0052](./0052-bidirectional-messaging-substrate.md) — Messaging substrate; provider webhook envelope shape reused for payment provider webhooks.

### Roadmap and specifications

- [Phase 2 commercial intake](../../icm/00_intake/output/phase-2-commercial-mvp-intake-2026-04-27.md) §"Scope Statement" item 2 — original ADR 0051 placeholder.
- [Property-ops cluster INDEX](../../icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md) — pins ADR drafting order.
- Cluster intakes Receipts (#26), Work Orders (#19), Vendors (#18), Leasing Pipeline (#22) all consume this substrate.

### Existing code / substrates

- `packages/blocks-rent-collection/Models/Payment.cs` — existing block-level type whose deferred-rounding + deferred-Plaid/Stripe-integration comments this ADR resolves.
- `packages/blocks-rent-collection/Models/BankAccount.cs`, `Invoice.cs`, `LateFeePolicy.cs`, `RentSchedule.cs` — surrounding block-level types unaffected by this ADR.
- `packages/foundation-integrations/` — existing package this ADR extends.
- `packages/kernel-audit/` — audit substrate this ADR emits to.
- `packages/analyzers/provider-neutrality/` — enforcement gate `providers-stripe` must satisfy.

### External

- [PCI DSS SAQ-A and SAQ-A-EP](https://www.pcisecuritystandards.org/document_library/) — scope discipline rationale.
- [NACHA Operating Rules](https://www.nacha.org/rules) — ACH return code reference (R01–R85).
- [EU PSD2 SCA / 3D Secure 2 specifications](https://www.europeanpaymentscouncil.eu/) — strong customer authentication challenge model.
- [Stripe API reference](https://stripe.com/docs/api) — first provider adapter target; substrate is vendor-neutral but Stripe API is the design pressure-test.
- [ISO 4217 currency codes](https://www.iso.org/iso-4217-currency-codes.html) — `CurrencyCode` allow-list source.

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options considered: narrow Phase 2 contracts, comprehensive substrate, Stripe-shaped contracts. Option A chosen with explicit rejection rationale for B (YAGNI / scope explosion) and C (ADR 0013 violation).
- [x] **FAILED conditions / kill triggers.** Listed: international tenant onboarding, recurring billing requirement, marketplace flow request, second provider abstraction-leak, PCI scope creep, regulated vertical, Stripe breaking change, consumer-count overflow. Each tied to externally-observable signal.
- [x] **Rollback strategy.** No production data exists yet (block has no live tenants). Rollback = revert this ADR + revert the `Sunfish.Foundation.Integrations.Payments` namespace addition + revert `blocks-rent-collection.Payment` field migration. `Payment` record returns to `decimal Amount` + `string Method` placeholders.
- [x] **Confidence level.** **HIGH.** Mirrors ADR 0013 + ADR 0052 patterns; no novel primitives. PCI SAQ-A discipline is well-understood industry pattern. Risk surface is in the long-tail provider-quirk handling (ACH return codes, dispute SLAs, SCA challenge resume), all of which are adapter concerns, not substrate concerns.
- [x] **Anti-pattern scan.** Glanced at `.claude/rules/universal-planning.md` 21-AP list. None of AP-1 (unvalidated assumptions), AP-3 (vague phases), AP-9 (skipping Stage 0), AP-12 (timeline fantasy), AP-21 (assumed facts without sources) apply. Sources cited; phases are observable; PCI scope claims have authoritative references.
- [x] **Revisit triggers.** Eight explicit conditions named.
- [x] **Cold Start Test.** Implementation checklist is 14 specific tasks, each verifiable. A fresh contributor reading this ADR + ADR 0013 + ADR 0049 + the Phase 2 commercial intake should be able to scaffold `Foundation.Integrations.Payments` + `providers-stripe` against the Stripe sandbox without asking for clarification.
- [x] **Sources cited.** ADR 0007, 0008, 0013, 0015, 0046, 0049, 0052 referenced. PCI DSS, NACHA, EU PSD2, Stripe API, ISO 4217 cited as external authorities. Existing code paths in `blocks-rent-collection` cited.
