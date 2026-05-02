---
id: 13
title: Foundation.Integrations + Provider-Neutrality Policy
status: Accepted
date: 2026-04-19
tier: foundation
concern:
  - operations
composes:
  - 7
  - 9
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0013 — Foundation.Integrations + Provider-Neutrality Policy

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** Define the runtime contracts Sunfish needs for external provider integrations (payments, banking, feature flags, channel managers, messaging, storage, identity) without binding the platform to any specific vendor.

---

## Context

ADR 0007 introduced `ProviderCategory` and `ProviderRequirement` in `Sunfish.Foundation.Catalog.Bundles` as declarative bundle-manifest concepts. Those describe what a bundle needs, not how the host actually wires the integration at runtime.

Multiple domains now want a provider seam:

- **Billing** (`blocks-billing` P2) — external engines like Lago, Stripe Billing, Recurly.
- **Payments** — Stripe, Adyen, Square.
- **Banking feed** — Plaid, Finicity, bank-direct SFTP.
- **Channel manager** — Airbnb, Vrbo, OTA aggregators via a channel-manager broker.
- **Messaging** — Twilio, SendGrid, SES, in-app push.
- **Feature flags** — OpenFeature / LaunchDarkly / flagd (already has its own provider seam in `Sunfish.Foundation.FeatureManagement`).
- **Storage** — S3, Azure Blob, filesystem.
- **Identity** — OIDC providers, Okta, Entra.

Each needs common plumbing:

- A way to register and enumerate providers.
- A credentials-reference shape that keeps secrets *out* of the contract surface.
- A webhook ingestion pipeline for provider-initiated events.
- A sync-cursor store so pull integrations can resume cleanly.
- A health-check / status surface for ops visibility.

Without a shared home, every provider integration reinvents these, often badly. Sunfish's provider-neutrality policy requires that domain modules (`blocks-billing`, `blocks-reservations`, …) never reference vendor SDKs directly — the seam has to exist somewhere. `Sunfish.Foundation.FeatureManagement` already models this for flags; this ADR generalizes it.

---

## Decision

Introduce **`Sunfish.Foundation.Integrations`** — runtime contracts for external provider wiring. Contracts-only with in-memory reference implementations for tests and demos. Provider-specific adapter packages (Stripe, Lago, Plaid, Twilio, …) live outside Foundation and reference this package.

### Contracts shipped

| Type | Purpose |
|---|---|
| `ProviderDescriptor` | Metadata for a registered provider: key, category, name, version, capabilities, supported regions, description. Reuses `ProviderCategory` from `Sunfish.Foundation.Catalog.Bundles` as the shared vocabulary. |
| `IProviderRegistry` | Registers and enumerates `ProviderDescriptor`s. In-memory default shipped. |
| `CredentialsReference` | Opaque handle to credentials stored outside the contract surface. Holds a provider key, credentials scheme, reference id (vault path, secret manager key), and optional rotation metadata. **Never** holds plaintext secrets. |
| `WebhookEventEnvelope` | Normalized provider-initiated event: provider key, event id, event type, received timestamp, raw body bytes, optional signature metadata, optional tenant scope. |
| `IWebhookEventHandler` | Implemented by modules that want to consume webhook events of a given type from a given provider. |
| `IWebhookEventDispatcher` | Routes envelopes to registered handlers. In-memory default shipped. |
| `SyncCursor` | Tracks last-synced position for a `(provider, tenant, scope)` tuple. Opaque value payload. |
| `ISyncCursorStore` | Persists cursors. In-memory default shipped. |
| `ProviderHealthStatus` | Enum: `Unknown`, `Healthy`, `Degraded`, `Unhealthy`. |
| `IProviderHealthCheck` | Implemented by adapters to report live health. Bridge surfaces status in admin. |

### Provider-neutrality policy (enforceable)

Two rules domain modules must follow:

1. **Domain modules never reference vendor SDKs directly.** `blocks-billing` does not `using Stripe;` — it uses contracts from `Sunfish.Foundation.Integrations` and domain-level billing abstractions. A `Sunfish.Providers.Stripe` adapter package consumes Stripe and implements Sunfish contracts.

2. **Domain concepts are Sunfish-modeled, not vendor-mirrored.** A `Payment` domain entity in `blocks-billing` is not a Stripe `Charge`. A `Reservation` is not an Airbnb `Booking`. Adapters translate; domain stays clean.

Both rules are enforced mechanically at build time — see [Enforcement](#enforcement-added-2026-04-28). Reviewer judgment remains the fallback for cases the mechanical layers don't cover.

### Module-adapter-provider layering

```
Module (e.g. blocks-billing)
  ↓ depends on
Foundation.Integrations contracts
  ↑ implemented by
Provider adapter (e.g. Sunfish.Providers.Stripe)
  ↓ uses
Vendor SDK
```

Adapters live in their own packages (`packages/providers-stripe/`, `packages/providers-plaid/`, …) and are registered via DI at host startup. Bridge's host picks the adapters it wants per deployment; lite-mode can skip provider adapters entirely.

### Credentials

`CredentialsReference` is deliberately minimal:

```csharp
public sealed record CredentialsReference
{
    public required string ProviderKey { get; init; }
    public required string Scheme { get; init; }      // "apiKey", "oauth2", "mtls", …
    public required string ReferenceId { get; init; } // vault path, secret manager key
    public DateTimeOffset? RotatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}
```

The actual secret resolution happens in a secrets-management adapter (Azure Key Vault, AWS Secrets Manager, env, …) that Foundation.Integrations does not define. That adapter interface is a follow-up ADR when the first real integration requires credential rotation — premature now.

### Webhook pipeline

Every provider-initiated event becomes a `WebhookEventEnvelope`. Adapters own:

1. Verifying provider-specific signatures before envelope creation.
2. Mapping provider payloads to a normalized envelope (keeping the raw body for audit).
3. Handing the envelope to `IWebhookEventDispatcher.DispatchAsync`.

Handlers are registered by `(providerKey, eventType)` tuple; multiple handlers can fire per envelope. Dispatcher ordering is registration order.

### Sync cursors

Pull integrations (bank feeds, channel manager pulls, provider data refreshes) persist cursors via `ISyncCursorStore`. Cursor value is opaque bytes — each adapter defines its own semantics (timestamp, opaque token, sequence number). Scope lets the same provider track multiple streams per tenant.

### What this ADR does not do

- Does **not** resolve credentials to plaintext. That's a secrets-management concern.
- Does **not** define rate limiting, retry, or circuit-breaker policy. Adapters own those via standard .NET resiliency primitives (`Microsoft.Extensions.Http.Resilience` is already pinned).
- Does **not** define provider marketplace / catalog UI. Bridge P3 work.
- Does **not** fold in `Sunfish.Foundation.FeatureManagement.IFeatureProvider`. That's a feature-specific provider seam that pre-exists in a narrower domain.

### Package layout

- `packages/foundation-integrations/Sunfish.Foundation.Integrations.csproj`.
- Namespace: `Sunfish.Foundation.Integrations`.
- References `Sunfish.Foundation` and `Sunfish.Foundation.Catalog` (for the shared `ProviderCategory` enum).
- Added to `Sunfish.slnx` under `/foundation/integrations/`.

---

## Enforcement (added 2026-04-28)

Provider-neutrality is enforced at build time via two layered mechanisms:

1. **Roslyn analyzer** — `Sunfish.Analyzers.ProviderNeutrality` rejects vendor SDK
   namespace references (`Stripe.*`, `Plaid.*`, `SendGrid.*`, `Twilio.*`) in any
   project under `packages/blocks-*/` or `packages/foundation-*/`, with the
   `Sunfish.Foundation.Integrations` package explicitly excluded as the contract
   seam. Diagnostic ID: `SUNFISH_PROVNEUT_001`. Auto-attached via
   `Directory.Build.props` (mirrors the `loc-comments` / `loc-unused` analyzer
   auto-wire pattern).

2. **`BannedSymbols.txt`** at solution root — the
   `Microsoft.CodeAnalysis.BannedApiAnalyzers` rule (`RS0030`) rejects specific
   symbols that should be banned globally (e.g. legacy APIs deprecated by ADR
   amendments). Cheap to extend; one line per banned symbol. Auto-attached to
   every packageable, non-test, non-analyzer project via `Directory.Build.props`.

Both layers fail the build (repo-wide `TreatWarningsAsErrors=true` makes analyzer
warnings into errors). The banned-namespace list under the analyzer is extensible
— adding a new vendor (e.g. `Adyen.*`, `Finicity.*`) is a one-line edit to
`BannedVendorNamespaces.cs`. The exclusion set (`Sunfish.Foundation.Integrations`
+ test projects) is encoded both in the auto-attach predicate and in the analyzer
itself as defense in depth.

---

## Consequences

### Positive

- Domain modules get one provider seam instead of N.
- Provider adapters are swappable by redeploy, not by refactor.
- Webhooks, cursors, credentials, health each have a single home.
- Reusing `ProviderCategory` from `Foundation.Catalog.Bundles` keeps bundle manifests and runtime registry speaking the same vocabulary.
- Sunfish stays vendor-agnostic in its public packages.

### Negative

- Contracts-only package — consumers need to either implement adapters or import pre-built ones. Without adapters, the package is inert.
- Bundle manifests' `ProviderRequirement` and the runtime `ProviderDescriptor` have overlapping vocabulary that reviewers must keep consistent.
- In-memory reference implementations are not production-grade (no persistence, no encryption). That's by design — adapters provide real implementations.

### Follow-ups

1. **Reference provider adapter** — a minimal `Sunfish.Providers.Http` or `Sunfish.Providers.Stripe` package demonstrating the full pattern when the first real integration ships (P5 work).
2. **Secrets-management contract ADR** — resolves the question of how `CredentialsReference` actually yields usable secrets. Needed before the first credential-bearing integration ships.
3. **External entity mapping contract** — `IExternalMapping` for "tenant + local entity id → provider-side id" when the first pull integration maps records. Deferred out of this ADR.
4. **Provider capability taxonomy** — today `ProviderDescriptor.Capabilities` is `IReadOnlyList<string>`. A structured capability taxonomy (what does this Payments provider actually support?) is a follow-up when we have three or more payment adapters to compare.
5. **Webhook signature verification helpers** — per-provider signing schemes (HMAC-SHA256, Stripe signature format, …) published as an optional Foundation.Integrations sub-package.

---

## References

- ADR 0007 — Bundle Manifest Schema (`ProviderCategory`, `ProviderRequirement`).
- ADR 0009 — Foundation.FeatureManagement (the `IFeatureProvider` seam is the prior art this ADR generalizes).
- OpenFeature — vendor-neutral flag provider model this ADR extends to other categories.
- `Microsoft.Extensions.Http.Resilience` — pinned in `Directory.Packages.props`; adapters use its retry / circuit-breaker primitives.
