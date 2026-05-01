# Sunfish.Foundation.Integrations

Runtime contracts for external provider integrations — provider registry, credential references, webhook event envelopes + dispatcher, sync cursors, health checks.

Implements [ADR 0013](../../docs/adrs/0013-provider-neutrality-discipline.md). Cross-cutting; consumed by every block that talks to a third-party (payments, messaging, captcha, signatures, background-check, etc.).

## What this ships

### Provider registry + credentials

- **`IProviderRegistry`** — known external providers + their wiring shape.
- **`MessagingProviderConfig`**, **`PaymentProviderConfig`**, etc. — typed config records.
- **`ICredentialRef`** — opaque reference to a credential (resolved by the host's secrets manager; never inlined).

### Webhook + event envelope

- **`InboundMessageEnvelope`** — normalized inbound payload (per W#20 / ADR 0052) with `MessageChannel` discriminator (Email / Sms / Web / ProviderInternal).
- **`IInboundMessageScorer`** — abuse-scoring seam (consumed by W#28's 5-layer defense).
- **`IUnroutedTriageQueue`** — manual-triage queue for messages that can't be auto-routed.

### Sync cursors + health

- **`ISyncCursor`** — per-provider per-tenant cursor for resumable inbound sync.
- **`IProviderHealthCheck`** — health-status reporting per provider (used by Bridge's health endpoints).

### Captcha (`Captcha/`)

- **`ICaptchaVerifier`** — captcha provider seam; W#28's 5-layer defense Layer 1 consumer.
- **`InMemoryCaptchaVerifier`** — test fixture.

### Payments (`Payments/`)

- **`Money`** — currency-tagged amount value record.
- **`IPaymentGateway`** — payment-provider seam (W#19 Phase 0 stub; ADR 0051 substrate).

### Signatures (`Signatures/`)

- **`SignatureEventRef`** — opaque reference to a captured signature event (kernel-signatures Stage 06 will replace the stub).

### Messaging (`Messaging/`)

- See `InboundMessageEnvelope` + scorer + triage queue above; full W#20 / ADR 0052 substrate.

## ADR map

- [ADR 0013](../../docs/adrs/0013-provider-neutrality-discipline.md) — provider neutrality (Roslyn-enforced via the analyzer)
- [ADR 0051](../../docs/adrs/0051-foundation-integrations-payments.md) — payments substrate
- [ADR 0052](../../docs/adrs/0052-bidirectional-messaging-substrate.md) — messaging substrate
- [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) — signature substrate

## See also

- [apps/docs Overview](../../apps/docs/foundation/integrations/overview.md)
- [Sunfish.Blocks.Messaging](../blocks-messaging/README.md) — block-tier consumer of the messaging substrate
- [Sunfish.Blocks.PublicListings](../blocks-public-listings/README.md) — captcha + abuse-scorer consumer (Layers 1 + 4-5)
