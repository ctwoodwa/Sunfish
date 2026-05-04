---
id: 67
title: Atlas Integration-Config UI Surface
status: Proposed
date: 2026-05-04
tier: ui-core
concern:
  - configuration
  - ui
  - accessibility
  - audit
  - security
composes:
  - 13
  - 46
  - 49
  - 51
  - 52
  - 61
  - 62
  - 65
  - 66
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0067 — Atlas Integration-Config UI Surface

**Status:** Proposed
**Date:** 2026-05-04
**Authors:** XO research session
**Pipeline variant:** `sunfish-feature-change`
**Council posture:** standard adversarial + WCAG/a11y subagent (mandatory; credential-capture forms hit SC 3.3.7 / 3.3.8 / 4.1.3 / 1.3.1) + security-engineering subagent (credential storage, transport, rotation, license posture)
**Consumer scope:** Anchor admin (Zone A local-first); Bridge tenant admin (Zone C hosted); both share the same `IIntegrationAtlasProvider` contract

---

## Status

Proposed. Sibling to ADR 0066 (Helm + Identity Atlas Surface). 0066 defines the *generic* Atlas-issuance contract (`IAtlasProvider<T>`, `IIdentityAtlasSurface`, `IHelmWidget`); ADR 0067 specializes that contract for the **integration-config layer** — the Wayfinder configuration tier covering payment gateways, messaging transports, mesh-VPN control planes, and CAPTCHA verifiers. Pre-merge council canonical (per cohort lesson — 22-of-22 substrate amendments needed council fixes; running council before merge eliminates the post-acceptance amendment cycle).

---

## Context

The Wayfinder discovery (W#34 §5.6) classified **Layer 6 — integration configuration** as **Partial coverage**. Sunfish has fully-specified provider-neutrality contracts for every external integration category we ship today:

- ADR 0013 (Provider neutrality) — domain modules never reference vendor SDKs directly; providers are selected by name.
- ADR 0051 (Foundation.Integrations.Payments) — `IPaymentGateway` adapter contract; first-wave adapters `providers-stripe` / `providers-square`.
- ADR 0052 (Bidirectional Messaging Substrate) — provider-neutral messaging gateway; first-wave adapters `providers-postmark` / `providers-sendgrid` / `providers-twilio`.
- ADR 0061 (Three-Tier Peer Transport) — `IPeerTransport` with `TransportTier` enum; mesh adapters `providers-mesh-headscale` / `providers-mesh-tailscale` / `providers-mesh-netbird`; license-screening posture (SSPL / BSL excluded by default; admin opt-in requires acknowledgement).
- ADR 0028 §Phase 2.3 (CAPTCHA — already partially landed via `ICaptchaVerifier` in `packages/foundation-integrations/Captcha/`).

What the portfolio does **not** specify is *how a tenant administrator selects, configures, validates, and rotates* these providers. Each adapter today either has no admin UI at all or invents its own ad-hoc surface. There is no consistent place a Bridge tenant admin (or an Anchor desktop user managing their own node) goes to ask "which payment gateway is active in this tenant?" — let alone to change it. There is no place that captures credentials in a uniform encrypted-at-rest envelope, no place that runs validation against the provider before activating it, no place that enforces ADR 0061's license-posture acknowledgement for SSPL/BSL mesh providers, and no place that emits audit events when a provider rotates.

**ADR 0065** (Wayfinder System + Standing Order Contract) gave Sunfish the substrate for *every* configuration layer: a `StandingOrder` is the event-type primitive that captures a configuration change; `IStandingOrderIssuer` validates and issues; `IAtlasProjector` computes the materialized view; `AtlasView` / `AtlasSettingSnapshot` carry the current value at a Wayfinder path. **ADR 0066** (Helm + Identity Atlas) generalized that into a UI-surface contract: `IAtlasProvider<T>` renders a configuration section from the projection; `IIdentityAtlasSurface` is the canonical issuance UX; `IHelmWidget` is the read-only live-state observer. ADR 0067 is the **first concrete `IAtlasProvider<T>` specialization** — the Atlas surface for integration providers — and serves as the proof-of-pattern for the four remaining Wayfinder layers (security policy, account-identity, domain config, user preferences).

The decision is load-bearing for Phase 2 commercial scope (W#5 — six tenants spanning payment / banking / SMS / email integrations) and for several already-queued workstreams (W#22 Leasing Pipeline payment+email; W#28 Public Listings CAPTCHA+email; W#30 mesh-VPN provider selection). Without this surface, every future block that needs a provider-backed integration must invent its own admin UX, defeating the framework-agnostic principle.

---

## Decision drivers

1. **One surface, every category.** Whether the admin is configuring a payment gateway, an email transport, an SMS transport, a mesh VPN, or a CAPTCHA verifier, the rendering pipeline is identical: pick a provider, enter credentials in a form whose schema is declared *by the provider adapter*, validate, issue. Avoiding category-specific UI codepaths is the framework-agnostic test.
2. **Schema-driven, not hardcoded.** Adapter packages must be able to ship without coordinated UI changes. A new `providers-paddle` package landing in 2026-Q3 must be able to register itself, declare its credential schema, and appear in the Atlas dropdown without a single line of code change to `foundation-integrations` or to the rendering Atlas component. This is the same pattern VS Code's settings.json + Settings UI co-evolution uses (extensions self-declare configuration schema; the Settings UI reflects).
3. **Audit-by-construction.** Every provider change, every credential update, every license acknowledgement, every validation outcome MUST emit an `AuditRecord` per ADR 0049 — not by convention but because the issuance API requires the audit-emitter dependency. A provider rotation that is not audited is a compliance gap (especially for the Phase 2 commercial scope where providers move money).
4. **Sensitive-by-default storage.** API keys, webhook secrets, mesh control-plane tokens, SMS auth tokens — all sensitive credentials MUST be stored as `EncryptedField` per ADR 0046-A2. Plaintext appears only at the moment the adapter consumes the value via `IFieldDecryptor`. The Atlas form view MUST mask sensitive fields by default and offer an explicit show/hide toggle with WCAG-compliant accessible labelling (SC 1.3.1).
5. **Optimistic issuance + Mission-Space-gated availability.** Issue the Standing Order first; validate after. A failing validation does NOT roll the order back — it marks `integrations.{category}.validation-status` as `failed`, which the ADR 0062 `IMissionEnvelopeProvider` reads to gate downstream feature availability. This decouples *config persistence* from *config workability* and lets the admin save a partially-correct state to fix later.
6. **License-screening as a first-class step.** ADR 0061's SSPL/BSL exclusion (and admin override requiring acknowledgement) is enforced inside the Atlas surface before the Standing Order issues. The acknowledgement event is itself an audit record.
7. **Provider rotation is non-destructive.** Switching from `providers-stripe` to `providers-square` does NOT delete `integrations.payments.credentials.providers-stripe.api-key`. Old credentials remain in Atlas under the previous provider's path until explicitly cleared. Audit-trail records the rotation; the projector treats only the active provider's credentials as "live."
8. **Multi-transport routing where the category demands it.** Email is the singular case in this ADR's scope: a tenant typically wants `providers-postmark` for transactional and `providers-sendgrid` for marketing simultaneously. The schema MUST express both selections as a single routing structure rather than two parallel category configs (which would lose the constraint that they are alternative renderings of "email transport").
9. **WCAG 2.2 AA conformance as contract, not goal.** Per W#34 hardening + the council's general-counsel posture for ADR 0064, conformance is a requirement on every UI-bearing follow-on. Credential capture in particular hits multiple high-risk SCs (3.3.7 Redundant Entry, 3.3.8 Accessible Authentication, 1.3.1 Info and Relationships, 4.1.3 Status Messages). The contract specifies them; the rendering implementation tests them.

---

## Considered options

### Option A — Per-provider standalone settings pages

Each provider adapter ships its own admin Razor page (Anchor) and React component (Bridge). The accelerator hosts compose them under an "Integrations" navigation root.

**Pros.** Minimum coordination cost. Each adapter team owns its own UX. No central abstraction to maintain. Mirrors today's de facto state for the few adapters that have any admin UI at all.

**Cons.**
- Cross-provider consistency is impossible: every adapter invents masking / validation / audit / rotation differently. WCAG 2.2 AA conformance becomes per-adapter (multiplicative work) rather than once at the surface contract.
- No uniform "what is configured?" view. A tenant admin asking "which integrations are live?" must visit N pages.
- New adapters require coordinated UI work, defeating the framework-agnostic principle: a `providers-paddle` adapter cannot ship without engineering effort in the host UI.
- Multi-transport routing (email transactional vs marketing) has no natural home — every email adapter would have to know about the routing concept.
- Audit emission is per-adapter and can be silently omitted; no contract enforcement.
- License-posture acknowledgement (ADR 0061) ends up duplicated in each mesh adapter's UI.

**Verdict.** Rejected — this is the implicit current state, and the partial-coverage classification in W#34 §5.6 is a direct consequence of staying here.

### Option B — Unified `IIntegrationAtlasProvider` with dynamic schema rendering (this ADR's choice)

A single `IIntegrationAtlasProvider` (specialization of ADR 0066's `IAtlasProvider<T>`) handles every integration category. Adapter packages declare an `IntegrationProviderSchema` (provider name, category, credential field specs, license posture). The Atlas component reads the schema and dynamically renders the form. Validation, audit emission, encrypted storage, license acknowledgement, and rotation flow are all centralized.

**Pros.**
- One implementation of credential masking, accessible authentication, and Status Messages — verified once against WCAG 2.2 AA.
- New adapters compose without UI changes — they ship a schema; the Atlas surface picks it up.
- Audit emission is contractual; you cannot construct an `IIntegrationAtlasProvider` without the audit-trail dependency, so omission is impossible.
- Multi-transport routing has a natural location (the routing path under each category).
- License-posture acknowledgement is rendered uniformly for any mesh provider with `LicensePostureKind: StrongCopyleft`.
- Reusable proof-of-pattern for the remaining four Wayfinder layers (security, account-identity, domain config, user prefs) — each layer ships its own `IAtlasProvider<T>` specialization with the same shape.

**Cons.**
- Schema-driven rendering carries an inherent expressiveness ceiling: a provider whose credential capture genuinely needs a custom workflow (OAuth redirect dance, mTLS cert upload with private-key generation, multi-step API-key handshake) will hit the limits of `CredentialFieldSpec`. The contract MUST therefore admit a *schema-extension hook* (per §6.3) so adapter packages can opt into a custom-renderer slot for their own category — at the cost of a bespoke a11y review per slot.
- The dynamic-form rendering is not free; it requires the Anchor (Blazor) and Bridge (React/TSX) host packages to ship parity-tested components.
- Schema migration: when an adapter changes its credential shape (e.g. providers-stripe deprecates webhook signing secret v1 in favor of v2 with an additional field), the Atlas surface must handle a tenant whose Standing Order log carries the old shape.

**Verdict.** Accepted. The expressiveness ceiling is mitigated by the §6.3 escape hatch; schema migration is handled via the schema-version field on `IntegrationProviderSchema` (§4.2). The cons are real but bounded; Option A's costs grow without bound as the provider catalog expands.

### Option C — Static integration config baked into appsettings.json

No runtime admin UI. Tenant operators edit `appsettings.json` (Anchor) or environment variables (Bridge), restart the host. Validation is offline. No audit trail at the application layer (relies on infra-level config-change auditing).

**Pros.** Trivial to implement. Zero new code. Mirrors how most .NET applications historically configure integrations.

**Cons.**
- Phase 2 commercial scope is fundamentally incompatible: a property manager rotating a Stripe key cannot be expected to SSH into a host (Bridge) or edit a JSON file (Anchor). The user is a non-technical operator.
- No application-layer audit trail = compliance gap. ADR 0049 audit-by-construction is bypassed entirely.
- Multi-tenant Bridge: per-tenant integration config in a single appsettings.json is a non-starter. Either every tenant restarts together, or the file becomes a giant per-tenant section.
- License-posture acknowledgement (ADR 0061) for mesh VPN cannot be captured at all — ADR 0061 mandates an interactive acknowledgement.
- Anchor's local-first model wants config to live in the per-node Atlas (so it follows the node), not in a file outside the Sunfish state.

**Verdict.** Rejected. Option C is what we have for *bootstrapping* (host-level config like `KEK_PATH`); it is not viable for tenant-facing integration configuration.

---

## Decision

Adopt Option B. Sunfish ships a unified `IIntegrationAtlasProvider` contract — the first concrete specialization of ADR 0066's `IAtlasProvider<T>` — with dynamic schema-driven rendering, encrypted credential storage, audit-by-construction, license-posture enforcement, optimistic issuance with Mission-Space-gated availability, and non-destructive provider rotation.

The contract surface (§3 — §6) lives in a new `packages/ui-core-wayfinder/` package (net-new alongside the package introduced by ADR 0066). Reference implementations (§7) live in the same package. Adapter packages (`providers-stripe`, `providers-postmark`, `providers-twilio`, `providers-mesh-tailscale`, etc.) gain a single new export — the `IIntegrationSchemaProvider` registration (§6.1) — without changing their existing adapter contracts.

Anchor renders the surface as a Blazor component family (`AtlasIntegrationConfig.razor` + per-category sub-components) under the existing accelerator MAUI Blazor host. Bridge renders the surface as a React/TSX component family under the existing Bridge ASP.NET + React tenant admin. Both consume identical `IIntegrationAtlasProvider` instances DI'd against the per-tenant Wayfinder substrate.

---

## §1 — Surface scope

ADR 0067 covers six integration categories in v1, mapped onto the existing `Sunfish.Foundation.Catalog.Bundles.ProviderCategory` enum (per §1.1):

| `IntegrationCategory` value | Maps to existing `ProviderCategory` | First-wave adapters | Notes |
|---|---|---|---|
| `Payments` | `Payments` | `providers-stripe`, `providers-square` | Single active gateway per tenant |
| `TransactionalEmail` | `Messaging` (subdivided) | `providers-postmark`, `providers-sendgrid` | Routed via routing path (§5.4) |
| `MarketingEmail` | `Messaging` (subdivided) | `providers-sendgrid`, `providers-mailchimp` | Optional; same routing path |
| `Sms` | `Messaging` (subdivided) | `providers-twilio` | Single active per tenant |
| `MeshVpn` | (new category — see §1.1) | `providers-mesh-tailscale`, `providers-mesh-netbird`, `providers-mesh-headscale` | License posture per ADR 0061 |
| `Captcha` | (new category — see §1.1) | `providers-recaptcha`, `providers-hcaptcha` | Public-listings consumer per ADR 0028 §Phase 2.3 |

### §1.1 — Reconciliation with `Sunfish.Foundation.Catalog.Bundles.ProviderCategory`

The pre-existing `ProviderCategory` enum (in `packages/foundation-catalog/Bundles/ProviderCategory.cs`) was authored before the email-transactional-vs-marketing distinction was a Sunfish concern, and predates ADR 0061's `MeshVpn` category and ADR 0028's `Captcha` category. ADR 0067 introduces a finer-grained `IntegrationCategory` enum (§3.4) that:

1. Subdivides `Messaging` into `TransactionalEmail`, `MarketingEmail`, `Sms` — matching how tenant admins reason about routing (a transactional-email outage is a different incident class from a marketing-email outage).
2. Adds two new values, `MeshVpn` and `Captcha`, that did not exist when `ProviderCategory` was authored.
3. Preserves `Payments` 1:1 with the existing enum.

`ProviderCategory` continues to serve as the *bundle-coarse* taxonomy used by the catalog substrate; `IntegrationCategory` is the *Atlas-fine* taxonomy used by the configuration UX. Both enums coexist; the `IntegrationProviderSchema.Category` projects upward to a `ProviderCategory` via a static mapping table (§3.4 §"Mapping to ProviderCategory"). An open question (§9.1) tracks whether `ProviderCategory` should later absorb the new values.

---

## §2 — Standing Order paths

All integration configuration uses a flat dotted-kebab path namespace under the `integrations.` Wayfinder root (per ADR 0065 §3 path-naming convention):

```
integrations.{category}.active-provider                       — string
integrations.{category}.credentials.{provider}.{credential}   — EncryptedField | JsonNode
integrations.{category}.routing                               — JsonNode (optional, multi-transport categories only)
integrations.{category}.license-acknowledged.{provider}       — JsonNode { acknowledgedBy, acknowledgedAt, postureKind }
integrations.{category}.validation-status.{provider}          — JsonNode { status, lastValidatedAt, errorCode?, errorMessage? }
```

`{category}` is the kebab-cased `IntegrationCategory` value (`payments`, `transactional-email`, `marketing-email`, `sms`, `mesh-vpn`, `captcha`). `{provider}` is the provider name as registered (e.g. `providers-stripe`). `{credential}` is the credential field key declared in the provider's `IntegrationProviderSchema` (e.g. `api-key`, `webhook-secret`).

### §2.1 — `active-provider`

A string Standing Order naming the active provider for the category. Issuing a new value is the rotation event; the previous value is preserved in the audit trail and recoverable from the Standing Order log.

### §2.2 — `credentials.{provider}.{credential}`

One Standing Order per credential field. Sensitive fields (per the schema's `CredentialFieldSpec.Sensitive` flag) carry `EncryptedField` JSON shape (per ADR 0046-A2 §"JSON shape"). Non-sensitive fields (a from-address, a webhook callback URL) carry plain `JsonNode`.

### §2.3 — `routing` (multi-transport categories only)

For `TransactionalEmail` and `MarketingEmail`, both routes share a single `integrations.email.routing` path (note the singular `email`, since the routing structure jointly describes both). Schema:

```json
{
  "transactional": "providers-postmark",
  "marketing": "providers-sendgrid"
}
```

`MarketingEmail` is optional; the value may carry only the `transactional` key. `Sms` does not currently use a routing path (only one active provider) but the category reserves the namespace for forward compatibility (e.g. transactional vs. promotional SMS).

### §2.4 — `license-acknowledged.{provider}`

Issued when a tenant admin acknowledges a `LicensePostureKind.StrongCopyleft` provider per ADR 0061. Per §5.5 below, this Standing Order MUST issue *before* the corresponding `active-provider` Standing Order; the issuance contract enforces this ordering.

### §2.5 — `validation-status.{provider}`

Mutable Standing Order updated by validation runs. Read by the ADR 0062 `IMissionEnvelopeProvider` to gate Sunfish features that require the integration. A `failed` status does NOT prevent the active-provider value from persisting; it only signals to consumers that the integration is not currently usable.

---

## §3 — New types

All types live in the new `packages/ui-core-wayfinder/` package, namespace `Sunfish.UICore.Wayfinder.Integrations`, except where noted.

### §3.1 — `IntegrationProviderSchema`

```csharp
public sealed record IntegrationProviderSchema
{
    public required string ProviderName { get; init; }
    public required IntegrationCategory Category { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<CredentialFieldSpec> CredentialFields { get; init; }
    public LicensePostureKind? LicensePosture { get; init; }
    public int SchemaVersion { get; init; } = 1;
    public string? DocumentationUrl { get; init; }
}
```

`ProviderName` is the canonical adapter package name (e.g. `providers-stripe`) — matches `ProviderDescriptor.Key`'s string form (per §3.7 reconciliation). `SchemaVersion` enables forward migration when a provider's credential shape changes (§4.2).

### §3.2 — `CredentialFieldSpec`

```csharp
public sealed record CredentialFieldSpec
{
    public required string Key { get; init; }
    public required string DisplayLabel { get; init; }
    public required bool Sensitive { get; init; }
    public string? PlaceholderText { get; init; }
    public string? HelpText { get; init; }
    public string? AutocompleteHint { get; init; }
    public CredentialFieldKind Kind { get; init; } = CredentialFieldKind.SingleLineText;
}

public enum CredentialFieldKind
{
    SingleLineText = 0,
    MultiLineText = 1,
    Url = 2,
    Email = 3,
    PhoneNumber = 4,
    Json = 5,
}
```

`AutocompleteHint` carries the WCAG 2.2 SC 3.3.8 (Accessible Authentication) `autocomplete` attribute hint — `"current-password"`, `"new-password"`, `"username"`, `"off"` — so password managers can assist credential entry without imposing a cognitive-function test on the operator. The Atlas form-renderer applies this attribute verbatim to the rendered input.

### §3.3 — `LicensePostureKind`

```csharp
public enum LicensePostureKind
{
    Permissive = 0,
    WeakCopyleft = 1,
    StrongCopyleft = 2,
}
```

Per ADR 0061's classification: `Permissive` (MIT, Apache 2.0, BSD) requires no acknowledgement; `WeakCopyleft` (MPL, LGPL) shows a non-blocking informational notice; `StrongCopyleft` (SSPL, BSL, AGPL with non-trivial scope) presents the blocking acknowledgement modal (§5.5) and emits the `IntegrationLicenseAcknowledged` audit event before activation can proceed.

### §3.4 — `IntegrationCategory`

```csharp
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IntegrationCategory
{
    Payments = 0,
    TransactionalEmail = 1,
    MarketingEmail = 2,
    Sms = 3,
    MeshVpn = 4,
    Captcha = 5,
}
```

**Mapping to `ProviderCategory`.** A static `IntegrationCategoryMapping` class in the same namespace exposes:

```csharp
public static class IntegrationCategoryMapping
{
    public static ProviderCategory ToProviderCategory(IntegrationCategory category) => category switch
    {
        IntegrationCategory.Payments => ProviderCategory.Payments,
        IntegrationCategory.TransactionalEmail
            or IntegrationCategory.MarketingEmail
            or IntegrationCategory.Sms => ProviderCategory.Messaging,
        IntegrationCategory.MeshVpn => ProviderCategory.Other,
        IntegrationCategory.Captcha => ProviderCategory.Other,
        _ => ProviderCategory.Other,
    };
}
```

The `Other` mapping for `MeshVpn` and `Captcha` is a deliberate placeholder. §9.1 tracks the open question of whether to widen `ProviderCategory` proper.

### §3.5 — `IIntegrationAtlasProvider`

The headline contract — specializes `IAtlasProvider<IntegrationAtlasView>` from ADR 0066. Rendering hosts (Anchor / Bridge) consume an instance per tenant.

```csharp
public interface IIntegrationAtlasProvider : IAtlasProvider<IntegrationAtlasView>
{
    ValueTask<IReadOnlyList<IntegrationProviderSchema>> GetAvailableProvidersAsync(
        IntegrationCategory category,
        CancellationToken ct);

    ValueTask<string?> GetActiveProviderAsync(
        IntegrationCategory category,
        TenantId tenantId,
        CancellationToken ct);

    ValueTask<StandingOrderId> IssueProviderChangeAsync(
        IntegrationCategory category,
        string providerName,
        TenantId tenantId,
        PrincipalId issuedBy,
        CancellationToken ct);

    ValueTask<StandingOrderId> IssueCredentialAsync(
        IntegrationCategory category,
        string providerName,
        string credentialKey,
        JsonNode value,
        TenantId tenantId,
        PrincipalId issuedBy,
        CancellationToken ct);

    ValueTask<StandingOrderId> IssueLicenseAcknowledgementAsync(
        IntegrationCategory category,
        string providerName,
        LicensePostureKind postureKind,
        TenantId tenantId,
        PrincipalId issuedBy,
        CancellationToken ct);

    ValueTask<IntegrationValidationResult> ValidateProviderAsync(
        IntegrationCategory category,
        string providerName,
        TenantId tenantId,
        CancellationToken ct);

    ValueTask<StandingOrderId> IssueRoutingAsync(
        IntegrationCategory category,
        JsonNode routingValue,
        TenantId tenantId,
        PrincipalId issuedBy,
        CancellationToken ct);
}
```

**Issuance ordering invariant** (per §5.5): `IssueProviderChangeAsync` for a `LicensePostureKind.StrongCopyleft` provider MUST throw `LicenseAcknowledgementRequiredException` (§3.10) if no `IntegrationLicenseAcknowledged` Standing Order exists for that (tenant, provider) pair. Callers must invoke `IssueLicenseAcknowledgementAsync` first.

### §3.6 — `IntegrationAtlasView`

```csharp
public sealed record IntegrationAtlasView
{
    public required IReadOnlyDictionary<IntegrationCategory, ActiveProviderSnapshot?> ActiveByCategory { get; init; }
    public required IReadOnlyDictionary<IntegrationCategory, ProviderValidationStatus> StatusByCategory { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, AtlasSettingSnapshot>> CredentialsByProvider { get; init; }
    public JsonNode? EmailRouting { get; init; }
}

public sealed record ActiveProviderSnapshot(
    string ProviderName,
    DateTimeOffset ActivatedAt,
    PrincipalId ActivatedBy);
```

The view is the projection consumed by the Atlas component family. `CredentialsByProvider[providerName][credentialKey]` returns the `AtlasSettingSnapshot` (per ADR 0065 §5) for that credential, which carries the value, last-issued-at, and last-issued-by.

### §3.7 — `IIntegrationSchemaProvider`

```csharp
public interface IIntegrationSchemaProvider
{
    IReadOnlyList<IntegrationProviderSchema> GetSchemas();
}
```

Adapter packages register one implementation per package via `services.AddSingleton<IIntegrationSchemaProvider, StripeSchemaProvider>()` in their `AddSunfishStripe()` extension. The `IIntegrationAtlasProvider` discovers all registered providers via DI.

**Reconciliation with `ProviderDescriptor`.** Adapter packages ALREADY register a `ProviderDescriptor` (per ADR 0013) at startup. `IIntegrationSchemaProvider` is *additive* — it does not replace `ProviderDescriptor`; it carries the additional schema metadata that the Atlas surface needs (credential field specs, license posture, autocomplete hints) which is intentionally absent from `ProviderDescriptor` (which serves the runtime-routing concern, not the configuration-UX concern). Both registrations share the `ProviderName` / `Key` string identity.

### §3.8 — `IntegrationValidationResult`

```csharp
public sealed record IntegrationValidationResult
{
    public required ProviderValidationStatus Status { get; init; }
    public required DateTimeOffset ValidatedAt { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
```

### §3.9 — `ProviderValidationStatus`

```csharp
public enum ProviderValidationStatus
{
    Unknown = 0,
    Valid = 1,
    Invalid = 2,
    Unreachable = 3,
    LicenseAcknowledgementRequired = 4,
}
```

`LicenseAcknowledgementRequired` is set by `ValidateProviderAsync` when license-posture screening fails before any credential-level validation can run (e.g. SSPL provider, no acknowledgement issued). `Unreachable` distinguishes a provider that cannot be reached (network, DNS, suspended account) from `Invalid` (provider is reachable but rejects the credentials).

### §3.10 — `LicenseAcknowledgementRequiredException`

```csharp
public sealed class LicenseAcknowledgementRequiredException : Exception
{
    public required string ProviderName { get; init; }
    public required LicensePostureKind PostureKind { get; init; }
}
```

Thrown by `IssueProviderChangeAsync` when activation is attempted without a prior acknowledgement Standing Order. The Atlas form-renderer catches this exception and surfaces the acknowledgement modal (§5.5).

---

## §4 — Schema lifecycle

### §4.1 — Adapter registration

Adapter packages declare their schema in a single source-generated or hand-authored `IIntegrationSchemaProvider` implementation:

```csharp
internal sealed class StripeSchemaProvider : IIntegrationSchemaProvider
{
    public IReadOnlyList<IntegrationProviderSchema> GetSchemas() =>
    [
        new IntegrationProviderSchema
        {
            ProviderName = "providers-stripe",
            Category = IntegrationCategory.Payments,
            DisplayName = "Stripe",
            DocumentationUrl = "https://docs.sunfish.dev/integrations/stripe",
            LicensePosture = LicensePostureKind.Permissive,
            CredentialFields =
            [
                new() { Key = "secret-key", DisplayLabel = "Secret API key",
                        Sensitive = true, AutocompleteHint = "current-password",
                        PlaceholderText = "sk_live_…",
                        HelpText = "Stripe dashboard → Developers → API keys" },
                new() { Key = "publishable-key", DisplayLabel = "Publishable API key",
                        Sensitive = false,
                        PlaceholderText = "pk_live_…" },
                new() { Key = "webhook-secret", DisplayLabel = "Webhook signing secret",
                        Sensitive = true, AutocompleteHint = "off",
                        PlaceholderText = "whsec_…",
                        HelpText = "Stripe dashboard → Developers → Webhooks → Signing secret" },
            ],
        },
    ];
}
```

Registration in the adapter's `AddSunfishStripe()` extension is one call:

```csharp
services.AddSingleton<IIntegrationSchemaProvider, StripeSchemaProvider>();
```

### §4.2 — Schema versioning + migration

When an adapter changes its credential shape (e.g. Stripe deprecates the v1 webhook secret in favor of v2 with an additional field), the adapter increments `IntegrationProviderSchema.SchemaVersion`. The `IIntegrationAtlasProvider` reads the active tenant's existing credentials, compares the version, and:

- If the tenant's credentials match the current schema version, render normally.
- If the tenant's credentials are an older version, render the form with the new fields highlighted and a "schema updated" advisory; existing values for fields that survived the migration are pre-filled.
- The migration itself is a per-adapter concern — the adapter's `ValidateProviderAsync` may refuse to validate against an out-of-date credential set; the surface displays the validation error and prompts re-entry.

The Atlas surface itself does NOT auto-migrate values. Adapter authors who need value-shape transformation (rare) ship a `IIntegrationCredentialMigrator` (out of v1 scope; deferred to a future amendment).

---

## §5 — Issuance + validation flow

### §5.1 — Provider activation (no license posture)

1. Admin selects category (e.g. Payments) in the Atlas UI.
2. Atlas reads `GetAvailableProvidersAsync(Payments)` → list of registered provider schemas.
3. Admin selects a provider (e.g. providers-stripe).
4. Atlas calls `IssueProviderChangeAsync(Payments, "providers-stripe", tenantId, principalId, ct)`.
5. `IIntegrationAtlasProvider` issues a Standing Order at `integrations.payments.active-provider` with value `"providers-stripe"`.
6. Audit event `IntegrationProviderChanged` emitted (via the `IAuditTrail` injected at construction).
7. Atlas form-renderer reveals the credential fields (per the schema's `CredentialFields`).

### §5.2 — Credential capture

For each credential field the admin enters a value:

1. Atlas calls `IssueCredentialAsync(category, provider, key, jsonNodeValue, tenantId, principalId, ct)`.
2. Implementation checks the corresponding `CredentialFieldSpec.Sensitive` flag.
3. If `Sensitive == true`: implementation wraps the value in `EncryptedField` (via `IFieldEncryptor`, per ADR 0046-A2 Phase 2 / amendment A4) and issues the Standing Order with the encrypted-field JSON shape.
4. If `Sensitive == false`: implementation issues the value as-is.
5. Audit event `IntegrationCredentialUpdated` emitted (with `credentialKey` but never the value — the audit record carries the key name, the path, and the principal; the value lives only in the Standing Order log).

### §5.3 — Validation

After all credentials are issued, the admin clicks "Validate":

1. Atlas calls `ValidateProviderAsync(category, provider, tenantId, ct)`.
2. Implementation:
   - Materializes the credentials from the Atlas projection.
   - Decrypts sensitive fields via `IFieldDecryptor`.
   - Routes to the adapter's category-specific validation hook (per §6.2 — `IPaymentGateway.ValidateAsync()`, `IMessagingGateway.ValidateAsync()`, etc.).
   - Captures the result.
3. Implementation issues a Standing Order at `integrations.{category}.validation-status.{provider}` carrying the JSON `{ status, lastValidatedAt, errorCode, errorMessage }`.
4. Audit event `IntegrationValidationSucceeded` (status == Valid) or `IntegrationValidationFailed` (any other status) emitted.
5. The result is returned to the caller and rendered in the surface (§5.6 — visual feedback).

### §5.4 — Multi-transport routing (email)

After both `TransactionalEmail` and (optionally) `MarketingEmail` have active providers and validated credentials:

1. Admin selects routing — for instance, "send transactional via Postmark, marketing via SendGrid."
2. Atlas calls `IssueRoutingAsync(IntegrationCategory.TransactionalEmail, jsonNodeRouting, tenantId, principalId, ct)` (the category argument is the *primary* category — the routing path is a single shared `integrations.email.routing`).
3. Implementation issues the routing Standing Order; routing JSON example:

```json
{
  "transactional": "providers-postmark",
  "marketing": "providers-sendgrid"
}
```

4. Audit event `IntegrationProviderChanged` emitted with the routing-update flag (the path itself encodes the change; the audit payload includes the previous and new routing JSON for diff visibility).

### §5.5 — License posture acknowledgement (mesh VPN)

When the admin selects a `StrongCopyleft` provider (e.g. `providers-mesh-headscale`):

1. Atlas reads the schema's `LicensePosture == LicensePostureKind.StrongCopyleft`.
2. Atlas presents the acknowledgement modal (Anchor: a Blazor `<AtlasLicenseAcknowledgementDialog>`; Bridge: a React `<LicenseAcknowledgementDialog />`). Modal copy is per-license-kind, drawn from a localized resource file in `packages/ui-core-wayfinder/Resources/`.
3. The modal MUST require the admin to:
   - Read the posture explanation (heading + paragraph identifying the license + the obligations).
   - Click an explicit "I acknowledge and accept the license obligations" button; checkbox-only acknowledgement is forbidden per ADR 0061's "explicit acknowledgement" requirement (and per WCAG SC 3.3.4 Error Prevention for legal commitments).
4. On acknowledgement, Atlas calls `IssueLicenseAcknowledgementAsync(category, provider, postureKind, tenantId, principalId, ct)`.
5. Implementation issues a Standing Order at `integrations.{category}.license-acknowledged.{provider}` with `{ acknowledgedBy, acknowledgedAt, postureKind }`.
6. Audit event `IntegrationLicenseAcknowledged` emitted.
7. Modal closes; provider activation flow resumes (§5.1 from step 4).

If the admin attempts `IssueProviderChangeAsync` without a prior acknowledgement, the implementation throws `LicenseAcknowledgementRequiredException` (§3.10); the Atlas catches it and surfaces the modal as a recovery path.

### §5.6 — Visual feedback

After validation completes the surface MUST present a clear status indicator per the WCAG 4.1.3 Status Messages contract:

| Status | Visual | Accessibility |
|---|---|---|
| `Valid` | Green check icon + "Connected" text | `aria-live="polite"`; text-only readers receive the "Connected" message |
| `Invalid` | Red alert icon + error code + error message | `role="alert"`; immediately announced |
| `Unreachable` | Amber warning icon + "Unreachable: {reason}" | `role="alert"`; announced |
| `LicenseAcknowledgementRequired` | Amber warning + "Acknowledgement required" + button surfacing the modal | `role="alert"`; announced |
| `Unknown` (pre-validation) | Neutral grey + "Not yet validated" | not announced (initial state) |

Color is never the sole signal; every state pairs an icon shape with the color (per WCAG SC 1.4.1 Use of Color).

### §5.7 — Provider rotation

Admin changes the active provider (e.g. Stripe → Square):

1. Atlas calls `IssueProviderChangeAsync(Payments, "providers-square", …)`.
2. Implementation issues the Standing Order. The previous provider's credentials at `integrations.payments.credentials.providers-stripe.*` are NOT deleted.
3. Audit event `IntegrationProviderChanged` emitted carrying both `previousProvider` and `newProvider` in the payload.
4. The Atlas projection now treats `providers-square`'s credentials (if any) as the "live" credentials. If `providers-square` has no credentials yet, the `IntegrationAtlasView.StatusByCategory[Payments]` reads `Unknown` and the surface prompts credential entry.
5. The admin retains the option to revert by re-issuing `active-provider = "providers-stripe"`; the prior credentials are still in the Standing Order log.
6. To explicitly clear stale credentials, the admin uses a "Clear unused credentials" affordance (§7.3 — out of v1 surface scope; deferred).

---

## §6 — DI surface + composition

### §6.1 — `AddSunfishIntegrationAtlas()` extension

```csharp
public static IServiceCollection AddSunfishIntegrationAtlas(this IServiceCollection services)
{
    services.AddSingleton<IIntegrationAtlasProvider, DefaultIntegrationAtlasProvider>();
    services.TryAddSingleton<IFieldEncryptor, …>(); // composes ADR 0046-A4
    services.TryAddSingleton<IFieldDecryptor, …>();
    return services;
}
```

The implementation `DefaultIntegrationAtlasProvider` consumes:
- `IStandingOrderIssuer` (from `foundation-wayfinder`)
- `IAtlasProjector` (from `foundation-wayfinder`)
- `IAuditTrail` (from `kernel-audit`)
- `IFieldEncryptor` + `IFieldDecryptor` (from `foundation-recovery`)
- `IEnumerable<IIntegrationSchemaProvider>` (from registered adapter packages)
- `IEnumerable<IIntegrationProviderValidator>` (per §6.2)

### §6.2 — `IIntegrationProviderValidator`

The category-specific validation hook is a separate contract per category to avoid forcing every adapter to depend on every validation surface.

```csharp
public interface IIntegrationProviderValidator
{
    IntegrationCategory SupportedCategory { get; }
    string SupportedProvider { get; }

    ValueTask<IntegrationValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, JsonNode> credentials,
        CancellationToken ct);
}
```

Adapter packages register one validator per provider:

```csharp
services.AddSingleton<IIntegrationProviderValidator, StripeIntegrationValidator>();
```

The validator inside `StripeIntegrationValidator` calls `IPaymentGateway.ValidateAsync()` (the contract defined in ADR 0051). For messaging providers, the validator calls `IMessagingGateway.ValidateAsync()` (the contract surface in `packages/foundation-integrations/Messaging/IMessagingGateway.cs` per the §A0 finding below). For mesh-VPN, the validator calls into the adapter's own `IMeshVpnAdapter.ValidateAsync()` — currently in flight in the uncommitted `packages/foundation-transport/IMeshVpnAdapter.cs` per the W#30 build.

### §6.3 — Custom-renderer escape hatch

For providers whose configuration genuinely cannot be expressed via `CredentialFieldSpec` (OAuth redirect dance, mTLS certificate generation, multi-step API-key handshake), adapters may opt into a custom rendering slot:

```csharp
public interface ICustomIntegrationRenderer
{
    string SupportedProvider { get; }
    Type RendererType { get; } // a Razor component type for Anchor
    string ReactComponentSpec { get; } // a React component module path for Bridge
}
```

When a custom renderer is registered for a provider, the Atlas form-renderer dispatches to it instead of the default `CredentialFieldSpec` renderer. The custom renderer is responsible for issuing Standing Orders via the same `IIntegrationAtlasProvider` API; it simply owns the *capture* UX.

WCAG 2.2 AA conformance for a custom renderer is the adapter author's responsibility, with a council a11y review required per package for any `ICustomIntegrationRenderer` registration.

V1 ships without any registered custom renderers; the hook is a safety valve for the cases the cohort discovers in v2+.

---

## §7 — Reference implementations

### §7.1 — `DefaultIntegrationAtlasProvider`

A reference implementation in `packages/ui-core-wayfinder/` consuming the §6.1 dependencies. Composes:

- `IStandingOrderIssuer.IssueAsync` for every Standing Order emission.
- `IAtlasProjector.ProjectAsync` for the projection feeding `IntegrationAtlasView`.
- `IAuditTrail.AppendAsync` for every audit event.
- `IFieldEncryptor.EncryptAsync` for sensitive credential issuance.

Tests cover: provider listing, provider activation, license-posture enforcement (with `LicenseAcknowledgementRequiredException` cases), credential issuance (sensitive + non-sensitive), validation result issuance, routing issuance, rotation non-destruction, and a parity test against an `InMemoryIntegrationAtlasProvider` (§7.2) that exercises the full happy path without external dependencies.

### §7.2 — `InMemoryIntegrationAtlasProvider`

A simpler in-memory variant for unit tests in consumer packages (blocks-leases, blocks-public-listings) that need to inject a working Atlas without spinning up the full Wayfinder substrate. Composes the in-memory `InMemoryAuditTrail` (per `packages/kernel-audit/InMemoryAuditTrail.cs`) and an in-memory Standing Order ledger.

### §7.3 — Anchor + Bridge component families

**Anchor (Blazor):** `accelerators/anchor/` adds a `Settings/Integrations/` page hosting:
- `<AtlasIntegrationConfig>` — the root component; one tab per `IntegrationCategory`.
- `<AtlasIntegrationCategoryPanel>` — one per category; renders the active-provider dropdown + credential form + validation status.
- `<AtlasCredentialField>` — renders a single `CredentialFieldSpec`; handles masking/reveal toggle + autocomplete attribute.
- `<AtlasLicenseAcknowledgementDialog>` — modal per §5.5.
- `<AtlasEmailRoutingPanel>` — special-case routing UI for email category.

**Bridge (React/TSX):** `accelerators/bridge/` adds a parallel React component family with identical naming:
- `<AtlasIntegrationConfig />`
- `<AtlasIntegrationCategoryPanel />`
- `<AtlasCredentialField />`
- `<LicenseAcknowledgementDialog />`
- `<EmailRoutingPanel />`

Parity tests verify that both rendering targets produce structurally equivalent DOM (per ADR's adapter-parity principle), with framework-idiomatic differences allowed (ARIA implementation, focus management) but visible behavior equivalent.

---

## §8 — New audit event types

Add to `packages/kernel-audit/AuditEventType.cs` (per the cohort precedent — the file is a single record-struct type with `public static readonly` field constants per category):

```csharp
// ===== ADR 0067 — Atlas integration-config UI surface =====

/// <summary>The active provider for an integration category was changed.</summary>
public static readonly AuditEventType IntegrationProviderChanged = new("IntegrationProviderChanged");

/// <summary>A credential value for an integration provider was created or updated.</summary>
public static readonly AuditEventType IntegrationCredentialUpdated = new("IntegrationCredentialUpdated");

/// <summary>A provider validation run reported success.</summary>
public static readonly AuditEventType IntegrationValidationSucceeded = new("IntegrationValidationSucceeded");

/// <summary>A provider validation run reported failure (Invalid / Unreachable / LicenseAcknowledgementRequired).</summary>
public static readonly AuditEventType IntegrationValidationFailed = new("IntegrationValidationFailed");

/// <summary>An admin acknowledged a strong-copyleft license posture for a mesh-VPN provider per ADR 0061.</summary>
public static readonly AuditEventType IntegrationLicenseAcknowledged = new("IntegrationLicenseAcknowledged");
```

Audit-record payloads (the `AuditRecord.Payload` JSON) carry per-event detail:

| Event | Payload fields |
|---|---|
| `IntegrationProviderChanged` | `category`, `previousProvider`, `newProvider`, `tenantId` |
| `IntegrationCredentialUpdated` | `category`, `provider`, `credentialKey` (NEVER value), `tenantId` |
| `IntegrationValidationSucceeded` | `category`, `provider`, `validatedAt`, `tenantId` |
| `IntegrationValidationFailed` | `category`, `provider`, `validatedAt`, `errorCode`, `errorMessage`, `tenantId` |
| `IntegrationLicenseAcknowledged` | `category`, `provider`, `postureKind`, `acknowledgedAt`, `tenantId` |

The redaction rule is contractual: audit payload fields named `value`, `apiKey`, `secret`, `password`, `token`, `webhookSecret`, or any field whose name starts with `credential.` or ends with `.value` MUST never appear in an audit record produced by ADR 0067 code. A test asserts this against a corpus of representative audit records.

---

## §9 — Open questions

### §9.1 — Should `ProviderCategory` absorb the new values?

ADR 0067 introduces `IntegrationCategory` with `MeshVpn` and `Captcha` values that don't exist in the pre-existing `ProviderCategory`. A future ADR could widen `ProviderCategory` to carry these values, allowing a 1:1 mapping. Trade-offs:

- **Widen `ProviderCategory`.** Pro: single source of truth. Con: forces every existing `ProviderDescriptor` consumer to add new switch arms.
- **Keep separate.** Pro: no churn on existing consumers. Con: two enums to maintain and reconcile.

Deferred — not blocking for v1. Track as ADR 0067 amendment candidate after first three downstream adopters land.

### §9.2 — Per-tenant vs per-node provider configuration

Bridge is multi-tenant; integration config is per-tenant. Anchor is single-node; integration config is per-node-but-also-per-tenant-when-the-Anchor-spans-tenants (ADR 0032). The current ADR specifies tenant scope on every issuance API; for Anchor's single-tenant case the tenant scope is the implicit local tenant. An open question is whether some categories (e.g. mesh VPN) should be node-scoped rather than tenant-scoped — a node connects to *one* mesh, even if it spans multiple tenants. Tracked for §10 amendment.

### §9.3 — Bring-your-own-vault for credential storage

Some Bridge tenants may want credentials stored in their own KMS (AWS Secrets Manager, HashiCorp Vault, Azure Key Vault) rather than in `EncryptedField`. The existing `CredentialsReference` (in `packages/foundation-integrations/CredentialsReference.cs`) is a primitive for exactly this. Is the Atlas surface obligated to handle the vault-reference case via `CredentialsReference` instead of `EncryptedField` for tenants who opt in? Deferred to a future amendment; v1 ships only with the `EncryptedField` storage path.

### §9.4 — Webhook URL provisioning

Some providers (Stripe, Twilio) issue webhooks back to Sunfish. The webhook URL is *not* a credential the admin enters — it's a value Sunfish must surface for the admin to copy into the provider's dashboard. The Atlas form likely needs a "read-only output field" concept distinct from `CredentialFieldSpec`. Deferred to a future amendment; v1 handles webhook URLs as `HelpText` content directing the admin to the docs.

---

## §10 — Implementation checklist

### Phase 1 — Contract surface

**Scope:** new `packages/ui-core-wayfinder/` package (or extend the package introduced by ADR 0066 if naming consolidates); contracts only.

Deliverables:
- `IIntegrationAtlasProvider` (§3.5)
- `IntegrationProviderSchema`, `CredentialFieldSpec`, `CredentialFieldKind` (§3.1, §3.2)
- `IntegrationCategory`, `IntegrationCategoryMapping` (§3.4)
- `LicensePostureKind` (§3.3)
- `IntegrationAtlasView`, `ActiveProviderSnapshot` (§3.6)
- `IIntegrationSchemaProvider` (§3.7)
- `IntegrationValidationResult`, `ProviderValidationStatus` (§3.8, §3.9)
- `LicenseAcknowledgementRequiredException` (§3.10)
- `IIntegrationProviderValidator` (§6.2)
- `ICustomIntegrationRenderer` (§6.3)
- `AddSunfishIntegrationAtlas()` registration extension (§6.1)

Tests not required for a pure-interface phase; XML docs required on every public type.

### Phase 2 — Reference implementation + audit

**Scope:** `DefaultIntegrationAtlasProvider`, `InMemoryIntegrationAtlasProvider`, and audit constants.

Deliverables:
- `DefaultIntegrationAtlasProvider` in `packages/ui-core-wayfinder/` (§7.1)
- `InMemoryIntegrationAtlasProvider` in same package (§7.2)
- 5 new `AuditEventType` constants in `packages/kernel-audit/AuditEventType.cs` (§8)
- Audit-record payload factory helpers (one per event type) in same package
- Unit tests covering all §5 flows
- Audit-redaction corpus test (per §8 redaction rule)

### Phase 3 — Adapter schema providers

**Scope:** every existing first-wave adapter package gains an `IIntegrationSchemaProvider` registration.

Deliverables:
- `providers-stripe`: `StripeSchemaProvider` + `StripeIntegrationValidator` (§4.1, §6.2)
- `providers-postmark`: `PostmarkSchemaProvider` + `PostmarkIntegrationValidator`
- `providers-twilio`: `TwilioSchemaProvider` + `TwilioIntegrationValidator`
- `providers-mesh-tailscale`: `TailscaleSchemaProvider` + `TailscaleIntegrationValidator` (per §6.2 mesh-VPN dependency on `IMeshVpnAdapter` — coordinate with W#30 build)
- `providers-recaptcha`: `RecaptchaSchemaProvider` + `RecaptchaIntegrationValidator`
- Per-adapter parity tests asserting schema shape matches the actual credential consumption code

### Phase 4 — Anchor + Bridge rendering

**Scope:** Anchor Blazor component family + Bridge React component family per §7.3.

Deliverables:
- Anchor: `accelerators/anchor/Pages/Settings/Integrations/` + `<AtlasIntegrationConfig>` family
- Bridge: `accelerators/bridge/src/admin/integrations/` + `<AtlasIntegrationConfig />` family
- Parity tests asserting structural DOM equivalence
- Component-level a11y tests against WCAG 2.2 AA criteria 3.3.7, 3.3.8, 1.3.1, 4.1.3, 1.4.1
- Snapshot-rendering tests with a representative tenant Atlas state

### Phase 5 — Ledger flip + apps/docs

Deliverables:
- ADR 0067 status flips to `Accepted`
- Frontmatter `status` field updated
- `apps/docs/blocks/integration-config.md` documenting the surface for adapter authors and tenant admins
- `apps/kitchen-sink` demonstration scene wiring the Atlas surface against the in-memory implementations
- `_shared/engineering/coding-standards.md` cross-link from the "Configuration UX" section

---

## §A0 — Pre-acceptance audit

Symbols, file paths, and prior-ADR references in this ADR were verified against `origin/main` immediately prior to authoring. Findings:

### §A0.1 — Verified present (no drift)

| Symbol / path | Origin/main location | Notes |
|---|---|---|
| `IPaymentGateway` | `packages/foundation-integrations/Payments/IPaymentGateway.cs` line 12 | Present per ADR 0051 |
| `IAtlasProjector` | `packages/foundation-wayfinder/IAtlasProjector.cs` line 19 | Present per ADR 0065 |
| `IStandingOrderIssuer` | `packages/foundation-wayfinder/IStandingOrderIssuer.cs` line 28 | Present per ADR 0065 |
| `AtlasView`, `AtlasSettingSnapshot` | `packages/foundation-wayfinder/AtlasView.cs`, `AtlasSettingSnapshot.cs` | Present per ADR 0065 |
| `EncryptedField` | `packages/foundation-recovery/EncryptedField.cs` line 32 | Present per ADR 0046-A2 |
| `IFieldDecryptor` | `packages/foundation-recovery/Crypto/IFieldDecryptor.cs` line 13 | Present per ADR 0046-A4 |
| `AuditEventType` | `packages/kernel-audit/AuditEventType.cs` | Present per ADR 0049 |
| `ICaptchaVerifier` | `packages/foundation-integrations/Captcha/ICaptchaVerifier.cs` | Present per ADR 0028 §Phase 2.3 |
| `ProviderDescriptor`, `ProviderCategory` | `packages/foundation-integrations/ProviderDescriptor.cs`, `packages/foundation-catalog/Bundles/ProviderCategory.cs` | Present per ADR 0013 |
| `CredentialsReference` | `packages/foundation-integrations/CredentialsReference.cs` | Present (relevant to §9.3 open question) |
| ADR 0065 (Wayfinder + Standing Order) | `docs/adrs/0065-wayfinder-system-and-standing-order-contract.md` | Present on origin/main |

### §A0.2 — Drift from intake-stub spec (corrected in body)

The intake spec named several symbols that diverge from origin/main; the ADR body uses the canonical origin/main names. Drift summary:

| Intake spec name | Origin/main canonical name | Where the ADR body resolves |
|---|---|---|
| `IMessageTransport` (outbound) + `IMessageReceiver` (inbound) | `IMessagingGateway` (single contract in `packages/foundation-integrations/Messaging/IMessagingGateway.cs` line 14) | §6.2 references `IMessagingGateway.ValidateAsync()` |
| `packages/foundation-integrations-payments/` (separate package) | `packages/foundation-integrations/` (single package with `Payments/` subfolder) | §6.2 + §A0.3 |
| `packages/foundation-integrations-messaging/` (separate package) | `packages/foundation-integrations/Messaging/` (subfolder of single package) | §6.2 + §A0.3 |
| `AuditEventType` constants are static readonly strings | Origin/main shape: `public readonly record struct AuditEventType(string Value)` with `public static readonly AuditEventType Foo = new("Foo")` constants | §8 uses canonical record-struct shape |

The drift is structural metadata only — the underlying contract semantics align with what the intake spec described. The ADR makes the canonical names load-bearing.

### §A0.3 — Soft-prerequisite (in flight; not yet on origin/main)

`IAtlasProvider<T>`, `IIdentityAtlasSurface`, and `IHelmWidget` (introduced by ADR 0066) are on PR #529 (open as of 2026-05-04) but not yet merged to origin/main. ADR 0067 is authored against the ADR 0066 specification (the ADR text on the PR), not against an origin/main implementation. **Mitigation:** Phase 1 of the §10 implementation checklist MUST land *after* ADR 0066's Phase 1 — the Phase 1 hand-off explicitly carries this dependency. If ADR 0066's surface drifts from its ADR text during build, ADR 0067's Phase 1 hand-off must be re-validated against the post-build origin/main shape; a regenerated §A0 captures the resolution.

### §A0.4 — Soft-prerequisite (in flight; uncommitted)

`IMeshVpnAdapter` is uncommitted in the working tree at `packages/foundation-transport/IMeshVpnAdapter.cs` (W#30 build in flight). §6.2 references `IMeshVpnAdapter.ValidateAsync()` for mesh-VPN validation. **Mitigation:** the W#30 PR will land before ADR 0067 Phase 3 starts; if `IMeshVpnAdapter` lands without a `ValidateAsync` method, the Phase 3 hand-off carries an instruction to add one (or to define the validation hook directly in `providers-mesh-tailscale` as a fallback).

### §A0.5 — Net-new package (no prior origin/main artifact)

`packages/ui-core-wayfinder/` does not yet exist on origin/main; it is the package introduced by ADR 0066 (per its §implementation checklist) and extended by ADR 0067. If ADR 0066's package naming changes during council review, ADR 0067 Phase 1 inherits the rename mechanically.

---

## §A1 — Council review checklist (apply before status flip)

Council verifies, in addition to the standard adversarial review:

- **WCAG/a11y subagent:**
  - SC 3.3.7 (Redundant Entry): no credential is asked twice in the same session.
  - SC 3.3.8 (Accessible Authentication): every sensitive `CredentialFieldSpec` has an `AutocompleteHint`; show/hide toggle has an accessible name.
  - SC 1.3.1 (Info and Relationships): masking state is conveyed structurally, not just visually.
  - SC 4.1.3 (Status Messages): validation outcomes use `aria-live="polite"` for success, `role="alert"` for errors.
  - SC 1.4.1 (Use of Color): no validation state relies on color alone.
  - SC 3.3.4 (Error Prevention — Legal): license-acknowledgement uses explicit button click, not checkbox.
- **Security-engineering subagent:**
  - Sensitive credentials traverse `IFieldEncryptor` before any persistence path.
  - Audit payloads cannot leak credential values (per §8 redaction rule + the Phase 2 corpus test).
  - Provider rotation does not leak old credentials into the new provider's audit payload.
  - License acknowledgement issues a Standing Order whose JSON content is itself audit-ready (no embedded secrets).
  - The contract surface admits no method that returns a decrypted credential to a UI rendering host (decryption happens server/host-side; the UI sees only "is the field set?").
- **Pedantic-Lawyer perspective:**
  - SSPL/BSL acknowledgement copy is reviewable against the actual license text.
  - Audit record retention satisfies the "configuration change auditing" obligation that any tenant policy may impose.
  - Schema-version migration does not silently drop a tenant's prior license acknowledgement.
- **Skeptical Implementer:**
  - Dynamic schema rendering is testable end-to-end without a running provider.
  - A new provider can be added without touching `foundation-integrations` or the rendering hosts.
  - The custom-renderer escape hatch (§6.3) is genuinely a safety valve, not a default.

---

## Amendments

(none — ADR is in `Proposed` status; amendments tracked in frontmatter on acceptance.)
