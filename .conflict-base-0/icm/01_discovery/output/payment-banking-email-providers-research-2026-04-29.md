# Provider Research — Payments / Banking / Email for Phase 2 Commercial MVP

**Stage:** 01 Discovery
**Status:** Research artifact — **DEFERRED per CEO directive 2026-04-29 (functional Mac/Windows demo is top priority over provider integration)**. Document preserved as durable reference for when provider tier returns to critical path.
**Date:** 2026-04-29
**Author:** CTO (research session)
**Audience:** CEO (BDFL) for spending sign-off when re-prioritized; PM (sunfish-PM) for adapter scaffolding once ADRs 0051 + 0052 Accepted **and CEO directs return to provider work**
**Triggered by:** CEO directive 2026-04-29 — "maintain MVP pressure; need research and recommendations on providers like Stripe/Plaid/Postmark for bank statement integration and rent collection."
**Deferred by:** CEO directive 2026-04-29 (same day) — "having a functional demo i can see and interact with on my mac or windows device is my top priority over provider integration."
**Resolves:** Phase 2 commercial intake (`phase-2-commercial-mvp-intake-2026-04-27.md`) provider-selection open questions for `providers-stripe`, `providers-plaid`, `providers-postmark` (or alternatives)
**Companion:** ADR 0051 (Foundation.Integrations.Payments — Proposed; council review B grade) + ADR 0052 (Bidirectional Messaging Substrate — Proposed; council review B+ grade)

---

## Executive summary

For BDFL's Phase 2 commercial MVP — 6 property LLCs + 1 holding company, ~10–30 leases, modest monthly volume — the recommended primary providers are:

| Concern | Recommended primary | Recommended parity-pair (per ADR 0013) | Estimated cost (Phase 2 scale) |
|---|---|---|---|
| **Rent collection + vendor payments** | **Stripe** (ACH-first; card fallback) | Adyen as future parity if international expansion | ~$15–60/mo + per-transaction fees (~$0.80 per ACH; 2.9%+30¢ per card) |
| **Bank statement integration** | **Plaid** (read-only transactions + balances) | Finicity (Mastercard-owned) as enterprise parity | ~$3–6/mo base + per-call (estimate <$10/mo total at Phase 2 scale) |
| **Outbound email** (statements, invoices, criteria docs) | **Postmark** (best transactional deliverability) | SendGrid (Twilio) as parity-test pair; AWS SES as cost-optimized alternative | $15/mo for 10K emails (Postmark Starter); SES ~$0.10/1K |

**Total provider-tier monthly cost at Phase 2 scale: ~$25–80/mo + per-transaction fees on rent/vendor flows.** Insignificant relative to operational savings (replaces Wave + Rentler + bank PDF download workflow per Phase 2 commercial intake).

**No external dependencies block ADR amendments or provider scaffolding once CEO signs off on adoption.** All three providers have mature .NET SDKs, sandbox environments, and PCI/regulatory postures that align with Sunfish's PCI SAQ-A target and provider-neutrality enforcement (ADR 0013 + workstream #14).

---

## 1. Stripe — rent collection + vendor payments

### Why Stripe is the primary recommendation

- **Industry-default for SMB.** Stripe's Standard, Connect, and Checkout products dominate the SMB property-management space. AppFolio, Buildium, RentRedi, Avail all integrate Stripe under the hood.
- **ACH-first economics for rent.** ACH transfers cost ~$0.80 flat per transaction (vs 2.9%+30¢ for card). On a $1,500 rent payment, ACH = $0.80; card = $43.80. For 25 leases × 12 months = 300 transactions/year, ACH-only saves ~$13K/year vs card.
- **PCI SAQ-A by default.** Stripe Elements + Stripe Checkout serve the entire payment-card capture page; Sunfish never touches PAN or CVV. This is the load-bearing PCI scope discipline ADR 0051 specifies as structural.
- **Stripe Connect for multi-tenant separation.** Each property LLC gets its own Connect account; bank routing is per-LLC; statements are per-LLC. Maps cleanly to Sunfish's `TenantId` per ADR 0008.
- **Mature .NET SDK.** `Stripe.NET` is officially maintained, semver-stable, async-first, idempotency-key-aware. ~5 years of production track record.
- **3DS / SCA support out-of-box.** ADR 0051's `ScaChallengeAffordance` enum maps directly to Stripe's `next_action.use_stripe_sdk` redirect flow.
- **ACH return / R-code handling first-class.** Stripe `charge.failed` + `charge.dispute.*` webhooks cover the full NACHA R01–R85 lifecycle.

### Cost model at BDFL scale

| Item | Cost | Notes |
|---|---|---|
| Stripe account base | $0/month | No platform fee |
| ACH transfers (rent) | $0.80 each | $5 cap per transfer |
| Card transfers (fallback) | 2.9% + $0.30 each | If tenant pays by card |
| Connect account per LLC | $0 fixed; Connect-specific volume fees apply | Use Standard accounts (vs Express/Custom) for cleanest separation |
| Disputes / chargebacks | $15 fee per dispute (refundable if won) | Rare for rent payments |
| Estimated monthly: 6 LLCs × ~25 ACH rent transfers/month avg | ~$120/month in fees | Negligible |

### Per-tenant configuration

**Recommended: Stripe Connect with Standard accounts (one per property LLC).** Each LLC owner connects their own Stripe account via OAuth; Sunfish stores the connected `account_id` per `TenantId`. Funds settle directly to each LLC's bank (no Sunfish-mediated escrow; Sunfish is a connector, not a money-handler — keeps Sunfish out of money-transmitter regulation).

Alternative considered: single Stripe account with metadata tagging (`metadata.tenant_id`). Rejected because (a) commingles funds across LLCs in a single Sunfish-owned merchant account, creating money-transmitter regulatory exposure; (b) per-LLC reporting becomes a metadata-filter rather than first-class Stripe construct; (c) BDFL wouldn't be able to migrate off Sunfish without disrupting payment flows.

### `providers-stripe` adapter scope (when ADR 0051 Accepted)

- Implements `IPaymentGateway` from `Foundation.Integrations.Payments`
- Wraps `Stripe.NET` SDK (vendor namespace bound; analyzer SUNFISH_PROVNEUT_001 enforces blocks-* never reference `Stripe.*` directly)
- ChargeAsync → `PaymentIntent.Create`
- CaptureAsync → `PaymentIntent.Capture`
- RefundAsync → `Refund.Create`
- VoidAsync → `PaymentIntent.Cancel`
- Webhook signature verification → `Stripe.EventUtility.ConstructEvent` with per-tenant signing secret
- 3DS challenge → returns `ScaChallenge` with redirect URI from Stripe `next_action`

Estimated scaffold time: **~2 days PM** for first-slice (sandbox-only; covers ACH + card + 3DS + refund). Production hardening (idempotency-key persistence, retry budgets, webhook deadletter queue) adds another ~1 day.

### Risks

- **Vendor lock-in (mitigated by ADR 0013).** Provider-neutrality means a future Adyen adapter is additive, not a forced migration.
- **Stripe terms-of-service for property management.** Stripe's TOS treats long-term residential rentals as standard merchant activity. Short-term rentals (Airbnb-class) have different TOS terms — out of Phase 2 scope.
- **Underwriting on each LLC.** Each property LLC needs its own EIN + business documentation for Stripe Connect onboarding. ~30-min setup per LLC. BDFL will need to do this manually (not Sunfish-automatable).
- **Bank account verification.** Stripe ACH requires tenant bank-account verification (microdeposits or Plaid Link). Plaid recommendation below addresses this — Stripe's Plaid Link integration is native + free.

### Rejected alternatives

| Vendor | Why rejected for Phase 2 primary |
|---|---|
| Adyen | Enterprise-tier; setup overhead too high for 6-LLC scale; better as future parity-pair |
| Square | POS-optimized; weaker recurring/scheduled payment semantics than Stripe |
| Braintree (PayPal) | Fading; merging into PayPal Commerce; SDK quality has degraded |
| Authorize.Net | Aging API; no native Connect-equivalent for multi-tenant separation |
| Stripe Atlas (forming an LLC via Stripe) | LLCs already exist; not relevant |

---

## 2. Plaid — bank statement integration

### Why Plaid is the primary recommendation

- **Industry-default for bank-data aggregation.** 12,000+ US banks supported including small/regional/credit unions BDFL likely uses for property LLCs.
- **Read-only access — minimal Sunfish liability.** Plaid Items grant Sunfish *read* access to transactions + balances. Sunfish never holds bank credentials; Plaid manages OAuth + token refresh + reauthentication prompts.
- **Replaces Wave Accounting's PDF download workflow.** Phase 2 commercial intake's stated migration target. Plaid's `/transactions/get` API delivers the same data Wave's Plaid integration delivers — directly.
- **Stripe Plaid Link integration.** When Stripe ACH onboarding asks for tenant bank account, Plaid Link verifies it instantly (vs 1–3-day microdeposit). Free for both vendors when used together — meaningful UX win for BDFL's tenants.
- **.NET SDK quality.** Official Plaid is Python/JS-first. **`Going.Plaid`** (community-maintained, MIT licensed, ~3 years production track record) is the production-grade .NET SDK. Track upstream stability before committing; alternative: hand-rolled HttpClient wrapper (~3-4h additional scaffolding).
- **Pricing scales linearly with bank accounts.** ~$0.50/connected-account/month base + per-call fees on `/transactions/get` (typically $0.02–$0.10 per call depending on tier). At 6 LLC bank accounts × 1 daily sync = ~$3–6/mo base + ~$5/mo in calls. Predictable.

### Cost model at BDFL scale

| Item | Cost | Notes |
|---|---|---|
| Development tier | Free (100 Items) | 6 LLCs = 6 Items; well under cap |
| Production base | ~$0.50/Item/month after development cap | ~$3/mo for 6 LLCs |
| Transaction sync calls | $0.02–$0.10 per call | ~$1–$5/mo at daily sync × 6 accounts |
| Auth product (account/routing for Stripe ACH) | $1.20 per Auth call | One-time per onboarding; ~$8 total for 6 LLCs |
| Identity product (account holder name verification) | Optional, ~$1 per call | Skip in Phase 2; revisit if fraud surfaces |
| Estimated monthly | **~$8–15/mo** | Negligible |

### Per-tenant configuration

**Recommended: one Plaid Item per property LLC's primary bank account** (some LLCs may have multiple — operating account + reserve account; each is its own Item).

- Sunfish stores `access_token` per `(TenantId, ItemId)` in tenant-key-encrypted secrets store (per ADR 0046 Foundation.Recovery primitives, now shipped via PR #223)
- Re-auth flows (when Plaid-supplied access tokens expire after bank-side credential changes) trigger Plaid Link's update-mode UI; Sunfish presents this in Anchor admin

### `providers-plaid` adapter scope

- Implements an `IBankFeedGateway` interface (new on `Foundation.Integrations` — sister to `IPaymentGateway`; minor ADR 0013-compatible extension; does NOT require ADR 0051 amendment, but warrants a new sub-ADR or amendment to `Foundation.Integrations` package contract)
- Wraps `Going.Plaid` SDK (or hand-rolled HttpClient — Stage 02 decision)
- `LinkTokenCreateAsync` → returns Link token for in-browser flow
- `ItemPublicTokenExchangeAsync` → exchanges public token for access token
- `TransactionsGetAsync` → daily sync; cursor-based incremental sync via `/transactions/sync`
- Webhook handler → `transactions:updated`, `item:error`, `item:webhook_update_acknowledged`
- Reauth flow handler → exposes Plaid Link update-mode URL when bank credentials expire

Estimated scaffold time: **~2 days PM** for first-slice (sandbox-only; covers Link + Auth + Transactions Sync + webhook). Production hardening (cursor checkpoint persistence, error retry, reauth UX wiring) adds ~1 day.

### Risks

- **Open Banking transition (US).** Plaid's screen-scraping legacy is being deprecated as US banks move to FDX-standard APIs (Open Banking phase-in 2025-2027). Plaid is leading the transition; existing tokens migrate automatically. Low Phase 2 risk.
- **Bank-side reauthentication friction.** Some banks require quarterly or monthly reauth. UX work in Anchor needs to handle this gracefully; documented for PM.
- **`Going.Plaid` SDK upstream risk.** Community-maintained; if the maintainer disappears, Sunfish would need to fork or hand-roll. Mitigation: `providers-plaid` is a thin adapter; replacement cost is bounded.
- **Plaid Identity product missing.** Account-holder name verification is gated behind Plaid Identity (separate product, $1 per call). For property management this matters less than for lending; deferred.

### Rejected alternatives

| Vendor | Why rejected for Phase 2 primary |
|---|---|
| Finicity (Mastercard) | Stronger enterprise; weaker SMB pricing; fewer small/regional banks supported |
| Yodlee (Envestnet) | Older platform; clunky API; deprecated by most fintechs |
| MX | Strong on UI components; weaker on raw transaction-data fidelity |
| Direct bank SFTP | BDFL's banks may not offer; one-off integration per bank; rejected on cost |
| Bank-PDF parsing (current Wave workflow) | The thing we're replacing. |

---

## 3. Postmark — outbound email

### Why Postmark is the primary recommendation

- **Best-in-class transactional deliverability.** Postmark separates marketing and transactional email streams architecturally; transactional inboxing rates exceed 99.5% even on cold senders. SendGrid (Twilio-owned) commingles streams by default; deliverability suffers.
- **Inbound parsing first-class.** ADR 0052 (Bidirectional Messaging Substrate, Proposed) requires inbound webhook for prospect application replies, vendor work-order replies, leaseholder maintenance requests. Postmark Inbound's webhook payload is the cleanest in the industry; ~$1.25 per 10K inbound messages.
- **DKIM / SPF / DMARC tooling.** Postmark's domain-verification UI walks tenants through DNS setup. ADR 0052 defers per-tenant custom domain to Phase 2.3; Phase 2.1 ships shared sender domain `messages.bridge.sunfish.dev` — Postmark handles this configuration cleanly.
- **.NET SDK quality.** Official `Postmark` NuGet package; mature; well-maintained.
- **Pricing competitive at SMB scale.** $15/month for 10K emails. BDFL's Phase 2 volume — monthly statements (6 LLCs × 25 leases = 150 statements/mo) + invoices + receipts + reminders + criteria-docs (intake + showings + applications) — projects to ~500–1500 emails/month. Even with growth headroom: $15/mo flat covers 10K, comfortable for 5+ years.
- **Compliance posture.** CAN-SPAM (US) compliance is structural in Postmark Streams config — mandatory unsubscribe headers, sender authentication enforced. TCPA (SMS-specific) doesn't apply.

### Cost model at BDFL scale

| Item | Cost | Notes |
|---|---|---|
| Postmark Starter (10K emails/month) | $15/month | Phase 2 needs ~500–1500/mo |
| Postmark Inbound (per message) | $1.25 per 10K (~$0.0001 each) | Phase 2 inbound volume ~50–200/mo = effectively free |
| DKIM/SPF/DMARC | Free | DNS config only |
| Custom Streams (transactional + broadcast separation) | Free at Starter tier | Per-tenant separation if needed Phase 2.3 |
| Estimated monthly | **$15/month** | Fixed cost |

### Per-tenant configuration

**Recommended: per-tenant Postmark Server** (Postmark's terminology for a logical sender configuration). Each property LLC gets its own Server with its own Streams (transactional + broadcast); per-LLC reputation is isolated.

- Sunfish stores per-tenant Postmark API token in tenant-key-encrypted secrets (Foundation.Recovery primitives)
- Sender identity defaults to `messages.bridge.sunfish.dev` Phase 2.1; per-tenant custom domain (`messages.{llc}.com`) is Phase 2.3 enhancement

### `providers-postmark` adapter scope (when ADR 0052 Accepted)

- Implements `IOutboundMessageGateway` from reframed ADR 0052 contracts
- Wraps `Postmark` NuGet package
- DispatchAsync → `Postmark.Client.SendMessage`
- Status webhook → `delivery`, `bounce`, `spamcomplaint`, `open`, `click`
- Templated rendering → Postmark templates OR Sunfish-side rendering with raw HTML send (Stage 02 decision)

**Companion `providers-postmark-inbound` adapter:**
- Implements `IInboundMessageReceiver` from reframed ADR 0052
- Bridge webhook endpoint → receives Postmark Inbound JSON
- Signature verification → Postmark inbound webhook signing
- Thread-token extraction from `Reply-To` header → routes to ADR 0052 thread substrate

Estimated scaffold time: **~1 day PM** for outbound first-slice; **~1 day PM** for inbound first-slice. Total ~2 days for the pair.

### Risks

- **Pricing at scale.** Postmark gets expensive past ~50K emails/month (Starter tier max is 10K; next tier is $50/mo for 50K). At BDFL Phase 2 scale this is years away. SES would be the cost-optimized alternative if/when volume crosses ~100K/month.
- **Inbound parsing edge cases.** Forwarded emails, threaded replies, attachments — Postmark Inbound handles these well but always test against real-world property-management email patterns (tenants forward maintenance requests with screenshots etc.) before going live.
- **Per-tenant isolation cost.** Each Postmark Server is on the same account base price; multiple Servers don't multiply cost (good).

### Rejected alternatives

| Vendor | Why rejected for Phase 2 primary |
|---|---|
| SendGrid (Twilio) | Marketing-tilted; transactional deliverability slightly worse; cluster intake's Bidirectional Messaging ADR keeps SendGrid as **parity-test pair** for adapter validation; not primary |
| AWS SES | Cheapest at scale ($0.10/1K) but DKIM + reputation setup is manual, deliverability requires warming, support is community-only; reserves as Phase 2.3+ cost-optimization fallback |
| Mailgun | Similar to Postmark in shape; smaller ecosystem; reputation slightly worse for transactional |
| ConvertKit / Mailchimp / similar | Marketing platforms; wrong tier for transactional |

---

## 4. Cross-cutting considerations

### Provider-neutrality enforcement (ADR 0013)

All three vendor SDKs (`Stripe.NET`, `Going.Plaid`, `Postmark`) are referenced **only** within their respective `providers-*` packages. The Roslyn analyzer (`SUNFISH_PROVNEUT_001`, shipped via workstream #14 / PR #196) prevents `using Stripe;` or equivalent from any `blocks-*` or `foundation-*` source file.

If sunfish-PM accidentally references vendor SDKs in domain code, the build fails. CTO does not need to babysit this — enforcement gate is mechanical.

### Per-tenant credential isolation (Foundation.Recovery primitives)

`foundation-recovery` shipped today (PR #223). Per-tenant API keys for all three providers are stored encrypted at rest under tenant key:

- Stripe Connect `account_id` + webhook signing secret
- Plaid `access_token` per Item
- Postmark Server API token

Adapter packages resolve these via `CredentialsReference` per ADR 0013. The secrets-management adapter is a follow-up ADR (ADR 0013 Follow-up #2) — not blocking Phase 2 if Sunfish ships with environment-variable resolver as the first secrets-adapter implementation.

### Compliance posture

| Concern | Handler | Status |
|---|---|---|
| **PCI SAQ-A** | Stripe Elements + Checkout serve full payment-page; Sunfish never touches PAN/CVV | Structural per ADR 0051 amendments |
| **NACHA (ACH)** | Stripe handles R01–R85 return codes natively | Adapter wires `AchReturnEvent` per ADR 0051 |
| **CAN-SPAM** | Postmark Streams + unsubscribe headers | Structural |
| **TCPA** | Out-of-scope; no SMS in Phase 2 | Defer to Phase 3 if Twilio activates |
| **CCPA / GDPR** | Postmark + Plaid + Stripe all compliant; Sunfish-side: `tenant-key-encrypted PII at rest`, audit substrate emission per ADR 0049 | Substrate-level |
| **Money transmitter regulation** | Avoided by Stripe Connect Standard accounts (funds settle direct to LLC bank, not via Sunfish) | Architectural choice |

### Sandbox testing

All three vendors offer free sandbox environments:

- **Stripe Test Mode** — instant; no signup beyond a free Stripe account; mock cards and ACH numbers
- **Plaid Development Tier** — free for 100 Items; identical API to Production; uses Plaid's bank-sandbox environment with mock data
- **Postmark Development Server** — free; sends to verified email addresses only; doesn't burn deliverability reputation

PM scaffolding work uses sandbox throughout. Production cutover requires CEO sign-off (separate decision, not part of scaffolding).

---

## 5. Cost projection summary

| Category | Monthly cost (Phase 2 BDFL scale: 6 LLCs × ~25 leases) |
|---|---|
| Stripe (account base) | $0 |
| Stripe ACH transaction fees (~$0.80 × 150 transactions/month) | ~$120/month |
| Stripe card fees (assume 5% of payments by card) | ~$15/month |
| Plaid base (6 Items) | ~$3/month |
| Plaid transaction sync calls | ~$5/month |
| Plaid Auth (one-time per onboarding) | <$10 total |
| Postmark Starter (10K emails/month) | $15/month |
| Postmark Inbound (~200/month) | <$1/month |
| **Total fixed monthly** | **~$23/month** |
| **Total variable monthly** | **~$140/month** at full rent-collection volume |
| **Combined Phase 2 monthly** | **~$163/month** |

Compared to BDFL's current toolchain (Wave subscription + Rentler subscription + bank-portal time), **net savings are positive** even before accounting for time saved on PDF reconciliation.

---

## 6. CTO recommendation

**Adopt Stripe + Plaid + Postmark as Phase 2 primary providers.** Total Phase 2 scaffolding work: **~5 days PM** (Stripe ~2 days + Plaid ~2 days + Postmark outbound+inbound ~2 days; some parallelism possible).

**Sequencing within MVP critical path:**

1. **CTO ships ADR 0051 amendments** (next 1-2 turns) — unblocks Stripe + Plaid scaffolding
2. **CTO ships ADR 0052 amendments** (next 1-2 turns) — unblocks Postmark scaffolding
3. **CEO signs off on:**
   - ADR 0051 acceptance (after amendments merge)
   - ADR 0052 acceptance (after amendments merge)
   - **Provider adoption decision** (this document) — ~$163/month projected variable cost
4. **PM scaffolds first-slice adapters** (~5 days):
   - `providers-stripe` (sandbox; ACH + card + 3DS + refund + webhook)
   - `providers-plaid` (sandbox; Link + Transactions Sync + reauth)
   - `providers-postmark` + `providers-postmark-inbound` (sandbox)
5. **PM wires to Phase 2 commercial flows:** rent collection in `blocks-rent-collection`; bank reconciliation in `blocks-accounting`; statement email job
6. **CEO sandbox-validation pass** — BDFL runs through the Phase 2 monthly cycle on sandbox data before production cutover

**Total path from this document to BDFL operating Phase 2 commercial flows on Sunfish:** ~3-4 weeks if executed without surprises and with CEO sign-off at each gate.

---

## 7. Decisions for CEO

1. **Adopt Stripe + Plaid + Postmark as primary providers?** Default = yes per CTO recommendation. Override = pick alternatives (CTO can re-research) or defer the decision.
2. **Approve ~$163/month projected variable provider-tier cost?** Scaling concern is on the rent-collection variable side; fixed cost is ~$23/month. CTO judgment: this is the right tier; cheaper alternatives (SES, hand-rolled banking) cost more in setup time + reliability than they save in monthly bills.
3. **Sequencing trade-off:** ship providers in parallel with cluster Phase 2 EXTENDs (Vendors #18, WorkOrders #19, Leases #27)? Or pause cluster and surge providers?
   - **CTO recommendation: surge providers.** Phase 2 commercial MVP needs them; cluster EXTENDs can resume after providers ship.
   - **CEO override:** if you want to keep cluster + provider work in parallel, PM has bandwidth for ~1 cluster EXTEND + 1 provider scaffold simultaneously per session; total wall-clock is slower but parallel.

CTO awaits CEO sign-off on items 1 + 2 + 3 before committing PM to provider scaffolding hand-offs.

---

## Sign-off

CTO (research session) — 2026-04-29
