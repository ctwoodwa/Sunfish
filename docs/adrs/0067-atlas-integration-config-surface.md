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

### Option D — Six per-category `IAtlasProvider<T>` specializations

Instead of one `IIntegrationAtlasProvider`, ship six smaller per-category interfaces (`IPaymentsAtlasProvider`, `ITransactionalEmailAtlasProvider`, etc.) each specializing ADR 0066's `IAtlasProvider<T>` directly, with category-specific view models and dedicated dynamic-schema renderers.

**Pros.**
- Category-specific types eliminate the need for the `IntegrationCategory` routing dispatch inside the implementation — each category is its own DI registration.
- A new category is a new interface, which makes the category boundary explicit in the type system.
- Smaller interfaces are easier to mock and test in isolation.

**Cons.**
- Cross-category concerns (credential masking, WCAG-compliant accessible authentication, audit emission, license-posture acknowledgement) must be re-implemented or re-composed per category — multiplicative work and multiplicative a11y test burden.
- The tenant-admin "what is configured?" summary view requires aggregating six providers rather than projecting one `IntegrationAtlasView`.
- New-category extension (e.g. `IntegrationCategory.SignatureCapture`) requires a new interface, new DI registration, and new rendering host wiring — not a single enum value.
- Six DI registrations, six validator discovery loops, six schema-provider enumerations — operational fan-out for no behavioral gain.

**Verdict.** Rejected. Option D's category-specific type clarity is outweighed by the multiplicative cost on cross-category concerns. The single-surface framing wins on consistency, new-category extension, and the unified "what's configured?" view. Considered here per council §5.1 recommendation.

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

**Out-of-v1 categories.** `BankingFeed`, `Billing`, `FeatureFlags`, `ChannelManager`, `Storage`, and `Identity` are out of ADR 0067 v1 scope. They are tracked separately: `BankingFeed` per Phase 2 W#5 follow-on; `Identity` per ADR 0066 (which uses a substantially different issuance flow — OAuth/SAML/WS-Federation). The `IntegrationCategory` enum and `IIntegrationSchemaProvider` contract are forward-compatible: new categories are added by extending `IntegrationCategory` and registering schemas without touching `foundation-integrations` or the rendering hosts.

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

For `TransactionalEmail` and `MarketingEmail`, each has its own routing path per the §2 template:

```
integrations.transactional-email.routing   — string (active provider name)
integrations.marketing-email.routing       — string (active provider name, optional)
```

The `IntegrationAtlasView.EmailRouting` (§3.6) aggregates both into an `IntegrationEmailRouting` projection. Issuing `IssueRoutingAsync` with an `IntegrationEmailRouting` record writes both paths atomically (as two Standing Orders in sequence). The `TransactionalProvider` is required; `MarketingProvider` may be null (no-marketing-email mode).

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
    public CredentialAutocompleteHint AutocompleteHint { get; init; } = CredentialAutocompleteHint.Off;
    public CredentialFieldKind Kind { get; init; } = CredentialFieldKind.SingleLineText;
}

public enum CredentialAutocompleteHint
{
    Off = 0,
    CurrentPassword = 1,
    NewPassword = 2,
    Username = 3,
    OneTimeCode = 4,
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

`Off` is the default and is appropriate for opaque API keys and webhook secrets (which do not correspond to WHATWG autocomplete field names). `CurrentPassword` SHOULD be reserved for fields the password manager should treat as recoverable user passwords, not tenant-level secrets (per SEC-17 — see §3.2 security note). Adapter authors MUST NOT use `CurrentPassword` for tenant-scoped credentials such as API keys or webhook secrets; use `Off` or `NewPassword` (one-time entry without offer-to-save) for those. The §4.1 Stripe example is updated: `secret-key` and `webhook-secret` use `CredentialAutocompleteHint.Off`.

`DisplayLabel` MUST be rendered as the input's visible `<label>` (Bridge: `htmlFor`-associated; Anchor: `for`-associated). Any adjacent control's accessible name (show/hide toggle, "Replace value" button per §5.2.1) MUST include `DisplayLabel` verbatim as a substring (e.g. "Show Secret API key", "Replace Secret API key"), per SC 2.5.3 Label in Name. Localization of `DisplayLabel` is the adapter author's responsibility.

When `HelpText` is non-null, the renderer MUST render it in a persistent visible text node adjacent to the input (NOT as placeholder text) and the input MUST declare `aria-describedby` referencing that node's id. `PlaceholderText` is a *visual hint only* and MUST NOT be the sole carrier of any instruction the operator needs to complete the field (SC 3.3.2). Adapter authors who include critical instructions MUST place them in `HelpText`, not `PlaceholderText`.

#### §3.2.1 — Masked-field rendering contract

When `CredentialFieldSpec.Sensitive == true`, the renderer MUST emit a toggle button with:
- A stable accessible name that changes with state: "Show {DisplayLabel}" when masked, "Hide {DisplayLabel}" when revealed (localized per the host UI locale).
- `aria-pressed="true|false"` reflecting the current reveal state (or `role="switch"` as an alternative).
- `aria-controls` referencing the input's `id`.
- A visible icon paired with visible text or `aria-label` — the button MUST be operable by AT users without relying on icon shape alone (SC 1.3.1, SC 4.1.2).
- Toggling MUST update the input's `type` attribute between `"password"` and `"text"` AND announce the new state via the button's `aria-pressed` change. No separate live-region is needed because state is on the control itself.

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
    /// <summary>
    /// Projects an <see cref="IntegrationCategory"/> value to the coarser-grained
    /// <see cref="ProviderCategory"/> used by the catalog substrate.
    ///
    /// This mapping is one-direction only (<see cref="IntegrationCategory"/> →
    /// <see cref="ProviderCategory"/>). The reverse projection is intentionally
    /// undefined: <c>Messaging</c> maps to multiple <c>IntegrationCategory</c>
    /// values (<c>TransactionalEmail</c>, <c>MarketingEmail</c>, <c>Sms</c>).
    /// Do not introduce a <c>ToIntegrationCategory(ProviderCategory)</c> overload.
    /// </summary>
    /// <remarks>See §9.1 for the deferred question of widening ProviderCategory to include MeshVpn and Captcha values.</remarks>
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
        CancellationToken ct);

    Task<StandingOrder> IssueProviderChangeAsync(
        IntegrationCategory category,
        string providerName,
        CancellationToken ct);

    Task<StandingOrder> IssueSensitiveCredentialAsync(
        IntegrationCategory category,
        string providerName,
        string credentialKey,
        ReadOnlyMemory<byte> plaintextBytes,
        CancellationToken ct);

    Task<StandingOrder> IssueNonSensitiveCredentialAsync(
        IntegrationCategory category,
        string providerName,
        string credentialKey,
        JsonNode value,
        CancellationToken ct);

    Task<StandingOrder> IssueLicenseAcknowledgementAsync(
        IntegrationCategory category,
        string providerName,
        LicensePostureKind postureKind,
        CancellationToken ct);

    Task<IntegrationValidationResult> ValidateProviderAsync(
        IntegrationCategory category,
        string providerName,
        CancellationToken ct);

    Task<StandingOrder> IssueRoutingAsync(
        IntegrationEmailRouting routing,
        CancellationToken ct);
}
```

**Issuance ordering invariant** (per §5.5): `IssueProviderChangeAsync` for a `LicensePostureKind.StrongCopyleft` provider MUST throw `LicenseAcknowledgementRequiredException` (§3.10) if no `IntegrationLicenseAcknowledged` Standing Order exists for that (tenant, provider) pair. Callers must invoke `IssueLicenseAcknowledgementAsync` first.

**"Exists" defined:** an `IntegrationLicenseAcknowledged` Standing Order "exists" for a (tenant, provider) pair when the most-recent Standing Order at `integrations.{category}.license-acknowledged.{provider}` has a non-null `NewValue`. The acknowledgement is a **tenant-level legal commitment** — it is satisfied by any acknowledgement issued by *any* tenant principal, not exclusively by the same principal who is now attempting activation. A system administrator who acknowledges on behalf of the organization satisfies the invariant for all subsequent activations by any other tenant principal.

**CRDT consistency model for the pre-activation check.** The pre-issuance check reads the *same Standing Order log replica* that the issuer's local projection is computed from (not a quorum-read). Concurrent acknowledgements on separate replicas merge as duplicate `IntegrationLicenseAcknowledged` audit records (deduplicated by `(tenant, provider, postureKind)` on projection); the activation-after-acknowledge invariant is satisfied as long as *some* acknowledgement is present in the merged log at projection time. The invariant is enforced by a `StandingOrderValidator` in the validation chain (per ADR 0065 §4), not solely at the API entry point — the API-layer guard is a UI convenience shortcut, not the authoritative enforcement point.

#### §3.5.1 — Context and identity resolution

`DefaultIntegrationAtlasProvider` resolves the current `TenantId` and `ActorId` from an injected `IIntegrationAtlasContext` (§3.11) on every method call. The context is the host's authentication boundary — callers never assert identity; they receive a tenant-scoped provider instance. In Bridge, the context is implemented over ASP.NET authentication middleware; in Anchor, over the local-node identity.

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

The view is the projection consumed by the Atlas component family. `CredentialsByProvider[providerName][credentialKey]` returns the `AtlasSettingSnapshot` (per ADR 0065 §5) for that credential, which carries the value, last-issued-at, and the `StandingOrderId` that issued the value (per ADR 0065 §5 — `LastIssuedBy` is a pointer to the issuance event, not a principal identifier). The rendering host resolves the issuing actor display name from the Standing Order log via `IStandingOrderRepository.GetAsync(standingOrderId)` if it needs to surface 'last changed by' affordances.

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
    public LicenseAcknowledgementRequiredException(
        string providerName,
        LicensePostureKind postureKind,
        string? message = null)
        : base(message ?? $"License acknowledgement required for provider '{providerName}' (posture: {postureKind}).")
    {
        ProviderName = providerName;
        PostureKind = postureKind;
    }

    public string ProviderName { get; }
    public LicensePostureKind PostureKind { get; }
}
```

Thrown by `IssueProviderChangeAsync` when activation is attempted without a prior acknowledgement Standing Order. The Atlas form-renderer catches this exception and surfaces the acknowledgement modal (§5.5). Uses a positional constructor — `required` on `Exception` subclass init-only properties does not work at the throw site because C# `required` members must be set via object initializer syntax, which is unsupported after a `base(...)` call chain.

> Note: because this is an `Exception` subclass, implementations MUST add a constructor that populates `Message` with a diagnostic string: `base($"License acknowledgement required for '{providerName}' (posture: {postureKind}). Call IssueLicenseAcknowledgementAsync before IssueProviderChangeAsync.")`.

### §3.11 — `IIntegrationAtlasContext`

```csharp
public interface IIntegrationAtlasContext
{
    TenantId CurrentTenantId { get; }
    ActorId CurrentActorId { get; }
}
```

The Atlas provider never accepts caller-asserted tenant or principal — the host's authentication middleware sets both values before the Wayfinder layer runs. In Bridge, `IIntegrationAtlasContext` is implemented as a scoped service backed by `HttpContext.User` + tenant-resolution middleware. In Anchor, it is implemented as a singleton backed by the local-node identity. No `IIntegrationAtlasProvider` method accepts `TenantId` or `ActorId` as parameters.

### §3.12 — `IntegrationEmailRouting`

```csharp
public sealed record IntegrationEmailRouting
{
    public required string TransactionalProvider { get; init; }
    public string? MarketingProvider { get; init; }
}
```

Used by `IssueRoutingAsync` (§3.5). Implementations MUST verify that `TransactionalProvider` and `MarketingProvider` (if non-null) are registered `IIntegrationSchemaProvider` provider names with the appropriate email category. Issuance MUST throw `UnknownProviderException` for unregistered names. Provider-name comparison is ordinal-case-sensitive against the registered set.

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
                        Sensitive = true, AutocompleteHint = CredentialAutocompleteHint.Off,
                        PlaceholderText = "sk_live_…",
                        HelpText = "Stripe dashboard → Developers → API keys" },
                new() { Key = "publishable-key", DisplayLabel = "Publishable API key",
                        Sensitive = false,
                        AutocompleteHint = CredentialAutocompleteHint.Off,
                        PlaceholderText = "pk_live_…" },
                new() { Key = "webhook-secret", DisplayLabel = "Webhook signing secret",
                        Sensitive = true, AutocompleteHint = CredentialAutocompleteHint.Off,
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

**License-acknowledgement independence.** License acknowledgement (§2.4) is independent of credential schema version. An `IntegrationLicenseAcknowledged` Standing Order remains valid for the (tenant, provider) pair across schema-version increments, as long as the provider's `LicensePosture` value has not changed in a more-restrictive direction.

**License-posture migration (§4.2.1).** When an adapter's `SchemaVersion` increments and the new schema carries a more-restrictive `LicensePostureKind` than the prior version (`Permissive → WeakCopyleft`, `Permissive → StrongCopyleft`, `WeakCopyleft → StrongCopyleft`), any existing `IntegrationLicenseAcknowledged` Standing Order for that (tenant, provider) pair is invalidated. `ValidateProviderAsync` MUST return `LicenseAcknowledgementRequired` until a fresh acknowledgement issues; `IssueProviderChangeAsync` MUST throw `LicenseAcknowledgementRequiredException`. Less-restrictive direction transitions (`StrongCopyleft → Permissive`) retain the prior acknowledgement. A schema bump that changes *only* the license posture without changing credentials MUST use a new `ProviderName` (e.g., `providers-mesh-headscale-agpl-fork`) rather than a `SchemaVersion` increment — schema versions are for credential-shape changes.

---

## §5 — Issuance + validation flow

### §5.1 — Provider activation (no license posture)

1. Admin selects category (e.g. Payments) in the Atlas UI.
2. Atlas reads `GetAvailableProvidersAsync(Payments)` → list of registered provider schemas.
3. Admin selects a provider (e.g. providers-stripe).
4. Atlas calls `IssueProviderChangeAsync(Payments, "providers-stripe", ct)`.
5. `IIntegrationAtlasProvider` issues a Standing Order at `integrations.payments.active-provider` with value `"providers-stripe"`.
6. Audit event `IntegrationProviderChanged` emitted (via the `IAuditTrail` injected at construction).
7. Atlas form-renderer reveals the credential fields (per the schema's `CredentialFields`).

### §5.2 — Credential capture

For each credential field the admin enters a value:

1. Atlas calls either `IssueSensitiveCredentialAsync(category, provider, key, plaintextBytes, ct)` or `IssueNonSensitiveCredentialAsync(category, provider, key, jsonNodeValue, ct)`, dispatching on the corresponding `CredentialFieldSpec.Sensitive` flag at the call site.
2. The split API enforces the sensitive/non-sensitive distinction at compile time — sensitive credentials cannot be passed as `JsonNode` and non-sensitive credentials cannot be passed as `ReadOnlyMemory<byte>`.
3. For `IssueSensitiveCredentialAsync`: the implementation takes the plaintext bytes directly; it calls `IFieldEncryptor.EncryptAsync(plaintextBytes, tenantId, ct)` to produce an `EncryptedField` envelope, then serializes the envelope via its registered `EncryptedFieldJsonConverter` and writes the result as the Standing Order value. The rendering host MUST NOT pass `JsonNode` through the contract for sensitive fields; the split API makes this impossible at compile time. For `IssueNonSensitiveCredentialAsync`: the implementation takes `JsonNode value` and issues it as-is.
4. Audit event `IntegrationCredentialUpdated` emitted (with `credentialKey` but never the value — the audit record carries the key name, the path, and the principal; the value lives only in the Standing Order log).

### §5.2.1 — Existing-value "leave unchanged" mode

When rendering a credential form for a provider whose Standing Order log already carries a value for a `Sensitive == true` field, the renderer MUST present the field in a "set, leave unchanged" mode: a masked indicator (e.g. eight bullets) with a "Replace {DisplayLabel}" affordance; the field is NOT pre-populated with a decrypted value (the Atlas provider never returns decrypted bytes to the rendering host). Submitting the form with the field untouched MUST NOT call `IssueSensitiveCredentialAsync` for that credential — no re-issuance occurs, no audit event emits, and the existing Standing Order is preserved unchanged.

This satisfies SC 3.3.7 (Redundant Entry — no re-typing of a credential the admin already supplied) and SC 3.3.8 (Accessible Authentication — no cognitive re-test). Schema-version migration (§4.2) for `Sensitive == true` fields that survive the migration MUST use this same "leave unchanged" path for the surviving fields; only newly-introduced fields prompt entry.

### §5.3 — Validation

After all credentials are issued, the admin clicks "Validate":

1. Atlas calls `ValidateProviderAsync(category, provider, ct)`.
2. Implementation:
   - Materializes the credentials from the Atlas projection.
   - Decrypts sensitive fields via `IFieldDecryptor`.
   - Routes to the adapter's category-specific validation hook (per §6.2 — each `IIntegrationProviderValidator` owns its own probe logic).
   - Captures the result.
3. Implementation issues a Standing Order at `integrations.{category}.validation-status.{provider}` carrying the JSON `{ status, lastValidatedAt, errorCode, errorMessage }`.
4. Audit event `IntegrationValidationSucceeded` (status == Valid) or `IntegrationValidationFailed` (any other status) emitted.
5. The result is returned to the caller and rendered in the surface (§5.6 — visual feedback).

### §5.3.1 — Decrypt capability and plaintext lifetime

**Capability acquisition.** `DefaultIntegrationAtlasProvider.ValidateProviderAsync` acquires an `IDecryptCapability` from the injected `IDecryptCapabilityProvider`. The capability is scoped to `(tenant, purpose="integration-validation", ttl=60s)` and is audited by `IFieldDecryptor` per ADR 0046-A4. The `IDecryptCapabilityProvider` is a §6.1 dependency; its absence at DI time causes `ValidateProviderAsync` to throw `DecryptCapabilityUnavailableException`, surfaced to the admin as a host-misconfiguration error. No decrypted credential ever crosses the contract boundary to a rendering host; decryption happens within the same DI scope as the per-category validator.

**Plaintext lifetime constraints.** The `IIntegrationProviderValidator.ValidateAsync` method receives sensitive credentials as `ReadOnlyMemory<byte>` (not `JsonNode`) per §6.2.1. Implementations MUST:
1. Clear (`CryptographicOperations.ZeroMemory`) any owned plaintext buffers in a `finally` block after `ValidateAsync` returns.
2. NEVER log credential bytes, include them in exception messages, or retain references after the method returns.
3. NEVER cache decrypted credentials across calls.
Phase 3 per-adapter parity tests assert these properties (positive: inject a marker credential, verify no log output contains the marker; negative: simulate a provider failure, assert exception message does not contain the credential).

### §5.3.2 — Validation in-flight UX

While `ValidateProviderAsync` is in flight the renderer MUST:
- Set `aria-busy="true"` on the category panel.
- Disable the Validate button and re-label it "Validating…" (or render a spinner with `aria-label="Validating"`).
- Emit a polite live-region message "Validating {DisplayName}…" on click so AT users receive immediate feedback (SC 4.1.3).
- On completion, restore the button label and clear `aria-busy` BEFORE the §5.6 status announcement, so the two announcements do not overlap.
Implementations SHOULD debounce repeated clicks (one validation in flight per category at a time).

### §5.4 — Multi-transport routing (email)

After both `TransactionalEmail` and (optionally) `MarketingEmail` have active providers and validated credentials:

1. Admin selects routing — for instance, "send transactional via Postmark, marketing via SendGrid."
2. Atlas calls `IssueRoutingAsync(new IntegrationEmailRouting { TransactionalProvider = "providers-postmark", MarketingProvider = "providers-sendgrid" }, ct)`.
3. Implementation issues two Standing Orders atomically — one at `integrations.transactional-email.routing` with value `"providers-postmark"` and one at `integrations.marketing-email.routing` with value `"providers-sendgrid"` — per the §2.3 per-category paths.
4. Audit event `IntegrationProviderChanged` emitted for each routing path issued (the path itself encodes the change; the audit payload includes the previous and new routing value for diff visibility).

### §5.5 — License posture acknowledgement (mesh VPN)

When the admin selects a `StrongCopyleft` provider (e.g. `providers-mesh-headscale`):

1. Atlas reads the schema's `LicensePosture == LicensePostureKind.StrongCopyleft`.
2. Atlas presents the acknowledgement modal (Anchor: a Blazor `<AtlasLicenseAcknowledgementDialog>`; Bridge: a React `<LicenseAcknowledgementDialog />`). Modal copy is per-license-kind, drawn from a localized resource file in `packages/ui-core-wayfinder/Resources/`.
3. The modal MUST require the admin to:
   - Read the posture explanation (heading + paragraph identifying the license + the obligations).
   - Click an explicit "I acknowledge and accept the license obligations" button; checkbox-only acknowledgement is forbidden per ADR 0061's "explicit acknowledgement" requirement (and per WCAG SC 3.3.4 Error Prevention for legal commitments).
4. On acknowledgement, Atlas calls `IssueLicenseAcknowledgementAsync(category, provider, postureKind, ct)`.
5. Implementation issues a Standing Order at `integrations.{category}.license-acknowledged.{provider}` with `{ acknowledgedBy, acknowledgedAt, postureKind }`.
6. Audit event `IntegrationLicenseAcknowledged` emitted.
7. Modal closes; provider activation flow resumes (§5.1 from step 4).

If the admin attempts `IssueProviderChangeAsync` without a prior acknowledgement, the implementation throws `LicenseAcknowledgementRequiredException` (§3.10); the Atlas catches it and surfaces the modal as a recovery path.

### §5.5.1 — Modal accessibility contract

The license-acknowledgement dialog MUST:
- Declare `role="dialog"` and `aria-modal="true"`.
- Reference the dialog heading via `aria-labelledby` and the posture explanation text via `aria-describedby`.
- On open, move focus to the dialog's heading or first *non-button* focusable element — NOT the acknowledgement button directly (which would skip the obligation text per SC 3.3.4).
- Trap Tab/Shift-Tab focus within the dialog while open (SC 2.1.2 — users must be able to exit via ESC, not trapped without exit).
- Handle ESC as "cancel" — equivalent to declining the acknowledgement; MUST NOT issue the `IssueLicenseAcknowledgementAsync` Standing Order on ESC.
- Restore focus to the triggering provider-selection control on close (SC 2.4.3).

Parity: Anchor (Blazor `<AtlasLicenseAcknowledgementDialog>`) and Bridge (React `<LicenseAcknowledgementDialog />`) MUST implement identical focus semantics; parity tests exercise open/close keyboard navigation.

### §5.6 — Visual feedback

After validation completes the surface MUST present a clear status indicator per the WCAG 4.1.3 Status Messages contract:

| Status | Visual | Accessibility |
|---|---|---|
| `Valid` | Green check icon + "Connected" text | `aria-live="polite"`; text-only readers receive the "Connected" message |
| `Invalid` | Red alert icon + error code + error message | `role="alert"`; immediately announced |
| `Unreachable` | Amber warning icon + "Unreachable: {reason}" | `role="alert"`; announced |
| `LicenseAcknowledgementRequired` | Amber warning + "Acknowledgement required" + button surfacing the modal | `role="alert"`; announced |
| `Unknown` (pre-validation) | Neutral grey + "Not yet validated" | not announced (initial state) |

Color is never the sole signal; every state pairs a shape-distinct icon with the color (per SC 1.4.1). Required icon shapes: `Valid` = check mark (✓); `Invalid` = circle-with-X; `Unreachable` = cloud-with-slash; `LicenseAcknowledgementRequired` = scroll/document; `Unknown` = em-dash or empty circle. The icon's accessible name (`aria-label` or visually-hidden text) MUST match the status label text.

The Status Message DOM region MUST be a single element per category panel with `aria-atomic="true"`. Status MUST NOT be re-announced on successive identical validation outcomes; only *status transitions* replace the live-region content. On `Unknown` state the live region MUST contain an empty text node, not stale content from a prior session. On `LicenseAcknowledgementRequired`, the announcement MUST precede focus moving to the modal trigger.

### §5.6.1 — Validation staleness and Mission-Space gating

Validation outcomes are *additive*. A new `ValidateProviderAsync` run DOES NOT overwrite a prior `Valid` status until the new outcome is `Valid` *or* the prior `Valid` outcome is older than `ValidationStalenessTtl` (default 24h, tenant-configurable). The `IMissionEnvelopeProvider` (ADR 0062) reads the *most-recently-Valid status within TTL*, falling back to `Unknown` once stale. This prevents a transient network outage (yielding `Unreachable`) from immediately demoting feature availability. `IntegrationAtlasView.StatusByCategory` exposes the most-recently-Valid status, not the last validation outcome.

### §5.7 — Provider rotation

Admin changes the active provider (e.g. Stripe → Square):

1. Atlas calls `IssueProviderChangeAsync(Payments, "providers-square", …)`.
2. Implementation issues the Standing Order. The previous provider's credentials at `integrations.payments.credentials.providers-stripe.*` are NOT deleted.
3. Audit event `IntegrationProviderChanged` emitted carrying both `previousProvider` and `newProvider` in the payload.
4. The Atlas projection now treats `providers-square`'s credentials (if any) as the "live" credentials. If `providers-square` has no credentials yet, the `IntegrationAtlasView.StatusByCategory[Payments]` reads `Unknown` and the surface prompts credential entry.
5. The admin retains the option to revert by re-issuing `active-provider = "providers-stripe"`; the prior credentials are still in the Standing Order log.
6. To explicitly clear stale credentials, the admin uses a "Clear unused credentials" affordance (§7.3 — out of v1 surface scope; deferred).

**Webhook-secret rotation window (providers-stripe, providers-twilio).** For providers that issue webhooks back to Sunfish, a rotation event creates a transition window where webhooks signed with the *previous* webhook secret may arrive after the new active provider's Standing Order has issued. The `IIntegrationProviderValidator` for these providers MUST accept both the old and new webhook secrets during validation and must surface both secrets to the adapter's webhook-signature-verification path until the previous provider's credentials are explicitly cleared. The Atlas projection retains both credential sets precisely to support this; clearing the old webhook secret before the transition window closes is an explicit admin action, not an automatic consequence of rotation.

---

## §6 — DI surface + composition

### §6.1 — `AddSunfishIntegrationAtlas()` extension

```csharp
public static IServiceCollection AddSunfishIntegrationAtlas(this IServiceCollection services)
{
    // Verify recovery substrate is registered first (must call AddSunfishRecovery() before this).
    if (!services.Any(d => d.ServiceType == typeof(IFieldEncryptor)))
        throw new InvalidOperationException(
            "AddSunfishRecovery() must be called before AddSunfishIntegrationAtlas(). " +
            "IFieldEncryptor is required by DefaultIntegrationAtlasProvider.");

    services.AddSingleton<IIntegrationAtlasProvider, DefaultIntegrationAtlasProvider>();
    // IIntegrationAtlasContext is registered by the host (Bridge: scoped via HttpContext;
    // Anchor: singleton via local-node identity). NOT registered here — host responsibility.
    return services;
}
```

Note: `IFieldDecryptor` is no longer registered by this extension. It is the responsibility of `AddSunfishRecovery()`.

`DefaultIntegrationAtlasProvider` consumes:
- `IStandingOrderIssuer` (from `foundation-wayfinder`)
- `IAtlasProjector` (from `foundation-wayfinder`)
- `IAuditTrail` (from `kernel-audit`)
- `IFieldEncryptor` (from `foundation-recovery` — registered by `AddSunfishRecovery()`)
- `IDecryptCapabilityProvider` (from `foundation-recovery` — provides short-lived capabilities for validation; registered by `AddSunfishRecovery()`)
- `IIntegrationAtlasContext` (from host — Bridge: scoped; Anchor: singleton)
- `IEnumerable<IIntegrationSchemaProvider>` (from registered adapter packages)
- `IEnumerable<IIntegrationProviderValidator>` (per §6.2)

#### §6.1.1 — IFieldDecryptor scope isolation

`IFieldDecryptor` MUST NOT be registered in the same DI container scope as components that the rendering host can resolve. The `foundation-recovery` package's `AddSunfishRecovery()` extension handles scope isolation: in Bridge, `IFieldDecryptor` is registered as an internal-scoped service gated behind a host-marker interface not accessible from the tenant-facing middleware chain; in Anchor, it is accessible only from the host process's Blazor scoped container via the platform bootstrapper (`MauiProgram`), never from child Blazor-component scopes. Phase 2 includes a unit test asserting that `IFieldDecryptor` cannot be resolved from a Blazor-scoped `IServiceProvider` built via `AddSunfishIntegrationAtlas()` alone.

### §6.2 — `IIntegrationProviderValidator`

The category-specific validation hook is a separate contract per category to avoid forcing every adapter to depend on every validation surface.

```csharp
public interface IIntegrationProviderValidator
{
    IntegrationCategory SupportedCategory { get; }
    string SupportedProvider { get; }

    Task<IntegrationValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> sensitiveCredentials,
        IReadOnlyDictionary<string, JsonNode> nonSensitiveCredentials,
        CancellationToken ct);
}
```

Note: sensitive credentials passed as `ReadOnlyMemory<byte>` (already decrypted bytes) per §5.3.1; non-sensitive as `JsonNode` (raw Standing Order values).

Adapter packages register one validator per provider:

```csharp
services.AddSingleton<IIntegrationProviderValidator, StripeIntegrationValidator>();
```

**Validators are decoupled from runtime-gateway contracts.** `IPaymentGateway` (ADR 0051) has no `ValidateAsync` method; neither does `IMessagingGateway` (ADR 0052). Each `IIntegrationProviderValidator` implementation owns its own health-probe logic, independent of the runtime-egress contract. Examples:
- `StripeIntegrationValidator` issues a Stripe `/v1/account` API call using the provider credentials and maps the HTTP status to `ProviderValidationStatus`.
- `PostmarkIntegrationValidator` issues a Postmark `/servers` API call to verify the server token is valid.
- `TailscaleIntegrationValidator` calls the Tailscale API's `/api/v2/tailnet/{tailnet}/keys` endpoint to verify the auth key.

For mesh-VPN, if `IMeshVpnAdapter` (W#30 in-flight) ships with a `ProbeAsync` method, validators MAY delegate to it; otherwise they issue their own probe. This is a per-adapter implementation decision, not a contract requirement.

#### §6.2.1 — Resolution rules

- **Lookup:** `DefaultIntegrationAtlasProvider` resolves a validator by `(SupportedCategory, SupportedProvider)` exact match.
- **Duplicate registrations:** `AddSunfishIntegrationAtlas()` at DI build time throws `DuplicateValidatorRegistrationException` if two validators share the same `(SupportedCategory, SupportedProvider)` pair.
- **Missing validator:** If no validator is registered for an active provider, `ValidateProviderAsync` issues `ProviderValidationStatus.Unknown` with `ErrorCode = "no-validator-registered"` — no exception, no audit event. The surface displays "This provider does not support automated validation."
- **Internal by convention:** Adapter packages MUST mark validator implementations `internal sealed`; the only consumer is `DefaultIntegrationAtlasProvider`. `IIntegrationProviderValidator` is marked `[EditorBrowsable(EditorBrowsableState.Never)]`.

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

#### §7.3.1 — Category-tab keyboard contract

Category navigation MUST follow the WAI-ARIA Authoring Practices Tabs pattern: container `role="tablist"`, each tab `role="tab"` with `aria-selected` and `aria-controls`, panels `role="tabpanel"` with `aria-labelledby`. Keyboard: Left/Right arrows move between tabs (cyclic); Home/End jump to first/last; Tab key moves focus into the active panel. Roving `tabindex` (active tab `tabindex="0"`, others `tabindex="-1"`). Parity tests MUST exercise arrow-key navigation, not only DOM structure.

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

The redaction rule is contractual: audit payload fields named `value`, `apiKey`, `secret`, `password`, `token`, `webhookSecret`, or any field whose name starts with `credential.` or ends with `.value` MUST never appear in an audit record produced by ADR 0067 code.

**Enforcement: typed payload factories (allowlist, not denylist).** Audit payloads for ADR 0067 events MUST be constructed via typed factory methods — one per `AuditEventType` constant — declared in `IntegrationAuditPayloads.cs` (a Phase 2 deliverable). Each factory accepts only the fields enumerated in the §8 payload table above. Free-form `JsonNode` or `Dictionary<string, object>` construction is prohibited for ADR 0067 events; a Roslyn analyzer in `packages/foundation-wayfinder-analyzers` (SUNFISH_INTEGRATION_AUDIT001, severity Error) enforces this at compile time. The Phase 2 corpus test is the runtime backstop: for every `IIntegrationProviderValidator` registered, a marker credential is injected, all §5 flows are exercised, and every emitted `AuditRecord.Payload` JSON is scanned — the marker MUST NOT appear.

> **AuditRecord construction.** Per ADR 0049, each `AuditRecord.Payload` is a `SignedOperation<AuditPayload>`; the payload must be signed by the issuing actor's key before `IAuditTrail.AppendAsync` is called. Phase 2 deliverables include per-event-type factory helpers in `IntegrationAuditPayloads.cs` that accept the payload-field values and produce correctly signed, redaction-verified `AuditRecord` instances.

**Matcher semantics:** the forbidden-field-name check is **case-insensitive** (e.g. `Secret`, `SECRET`, `ApiKey`, and `apikey` all match). The matcher walks the `AuditPayload.Body` dictionary recursively — including nested object values, list elements, and nested dictionaries — and checks each *key* (never value text) against the forbidden patterns. Negative-test cases must include: (a) a key like `previousProvider` containing the word "secret" as a *value* (must pass — only key names are screened); (b) a key like `details.value` (must fail — ends with `.value`); (c) a key like `webhook-secret` normalized to `webhooksecret` for matching (must fail — matches `webhookSecret` case-insensitively after removing hyphens).

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

### §9.5 — License-acknowledgement principal-revocation behavior

When the principal who issued an `IntegrationLicenseAcknowledged` Standing Order leaves the tenant or has their `IntegrationsAdmin` role revoked, the acknowledgement persists in the log. Whether a revoked-principal acknowledgement satisfies the issuance-ordering invariant (§3.5) is a security-policy decision deferred to W#37 (Tenant Security Policy ADR). Until W#37 lands, the implementation MAY treat revoked-principal acknowledgements as still valid (log-immutability principle); W#37 is expected to introduce an explicit "re-acknowledge required" policy for SSPL/BSL providers when the acknowledging principal's role lapses.

### §9.6 — OAuth-flow provider support

The §6.3 custom-renderer escape hatch explicitly names "OAuth redirect dance" as the canonical example of a credential-capture workflow that cannot fit `CredentialFieldSpec`. v1 ships without any OAuth-flow providers. The first OAuth provider (e.g. a future `providers-google-workspace`, `providers-quickbooks`) requires its own ADR addressing: (a) callback URL whitelisting and per-tenant callback uniqueness; (b) CSRF-resistant `state` token generation and cross-tenant collision prevention; (c) PKCE challenge/verifier flow; (d) `aria-live` announcements for the popup/redirect lifecycle (per WCAG SC 4.1.3). Adapter authors MUST NOT add an OAuth-backed `IIntegrationSchemaProvider` to v1 without a companion ADR that addresses these requirements — doing so would leave CSRF and cross-tenant `state` collision undefined at the surface level.

---

## §10 — Implementation checklist

### Phase 1 — Contract surface

**Scope:** the `packages/ui-core-wayfinder/` package introduced by ADR 0066, under sub-namespace `Sunfish.UICore.Wayfinder.Integrations`; contracts only.

Deliverables:
- `IIntegrationAtlasProvider` (§3.5)
- `IntegrationProviderSchema`, `CredentialFieldSpec`, `CredentialAutocompleteHint`, `CredentialFieldKind` (§3.1, §3.2)
- `IntegrationCategory`, `IntegrationCategoryMapping` (§3.4)
- `LicensePostureKind` (§3.3)
- `IntegrationAtlasView`, `ActiveProviderSnapshot` (§3.6)
- `IIntegrationSchemaProvider` (§3.7)
- `IntegrationValidationResult`, `ProviderValidationStatus` (§3.8, §3.9)
- `LicenseAcknowledgementRequiredException` (§3.10)
- `IIntegrationAtlasContext` (§3.11)
- `IntegrationEmailRouting` (§3.12)
- `IIntegrationProviderValidator` (§6.2)
- `ICustomIntegrationRenderer` (§6.3)
- `AddSunfishIntegrationAtlas()` registration extension (§6.1)
- `ContractSurfaceTests.NoMethodReturnsDecryptedBytes` (reflection over `IIntegrationAtlasProvider`)

Tests not required for a pure-interface phase; XML docs required on every public type.

### Phase 2 — Reference implementation + audit

**Scope:** `DefaultIntegrationAtlasProvider`, `InMemoryIntegrationAtlasProvider`, and audit constants.

Deliverables:
- `DefaultIntegrationAtlasProvider` in `packages/ui-core-wayfinder/` (§7.1)
- `InMemoryIntegrationAtlasProvider` in same package (§7.2)
- 5 new `AuditEventType` constants in `packages/kernel-audit/AuditEventType.cs` (§8)
- Audit-record payload factory helpers (one per event type) in same package — `IntegrationAuditPayloads.cs`
- `SUNFISH_INTEGRATION_AUDIT001` Roslyn analyzer in `packages/foundation-wayfinder-analyzers`
- Unit tests covering all §5 flows
- Audit-redaction corpus test (per §8 redaction rule) — `IntegrationAuditRedactionTests`
- `DefaultIntegrationAtlasProviderTests.SensitiveCredential_IsEncryptedBeforeStandingOrder`
- `ProviderRotationTests.RotationAudit_DoesNotContainPriorCredentials`
- `IFieldDecryptor` scope-isolation unit test (per §6.1.1)

### Phase 3 — Adapter schema providers

**Prerequisite gate:** W#30 mesh-VPN substrate PR merged to origin/main; `IMeshVpnAdapter` present or a follow-up amendment filed with a tracked workstream reference for any `ProbeAsync`/validation method needed by `TailscaleIntegrationValidator`. Reference §A0.4.

**Scope:** every existing first-wave adapter package gains an `IIntegrationSchemaProvider` registration.

Deliverables:
- `providers-stripe`: `StripeSchemaProvider` + `StripeIntegrationValidator` (§4.1, §6.2)
- `providers-postmark`: `PostmarkSchemaProvider` + `PostmarkIntegrationValidator`
- `providers-twilio`: `TwilioSchemaProvider` + `TwilioIntegrationValidator`
- `providers-mesh-tailscale`: `TailscaleSchemaProvider` + `TailscaleIntegrationValidator` (per §6.2 mesh-VPN dependency on `IMeshVpnAdapter` — coordinate with W#30 build)
- `providers-recaptcha`: `RecaptchaSchemaProvider` + `RecaptchaIntegrationValidator`
- Per-adapter parity tests asserting schema shape matches the actual credential consumption code, plaintext lifetime constraints (§5.3.1), and marker-credential leak tests

### Phase 4 — Anchor + Bridge rendering

**Scope:** Anchor Blazor component family + Bridge React component family per §7.3.

Deliverables:
- Anchor: `accelerators/anchor/Pages/Settings/Integrations/` + `<AtlasIntegrationConfig>` family
- Bridge: `accelerators/bridge/src/admin/integrations/` + `<AtlasIntegrationConfig />` family
- Parity tests asserting structural DOM equivalence
- Component-level a11y tests against WCAG 2.2 AA criteria: 1.3.1 (info and relationships — masking state), 1.4.1 (use of color — status icons), 2.1.2 (no keyboard trap — modal dismissal), 2.4.3 (focus order — modal open/close), 2.5.3 (label in name — adjacent-control accessible names), 3.3.2 (labels/instructions — HelpText rendering), 3.3.4 (error prevention legal — acknowledgement explicit button), 3.3.7 (redundant entry — sensitive "leave unchanged" path), 3.3.8 (accessible authentication — `CredentialAutocompleteHint` enum values), 4.1.2 (name, role, value — show/hide toggle `aria-pressed`), 4.1.3 (status messages — including validation in-flight `aria-busy`). Tests MUST also cover the WAI-ARIA APG Tabs keyboard contract (§7.3.1).
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

| Symbol / path | File on origin/main | Verified signature / existence | Match? |
|---|---|---|---|
| `IPaymentGateway` | `packages/foundation-integrations/Payments/IPaymentGateway.cs` | `AuthorizeAsync`, `CaptureAsync`, `RefundAsync` only — NO `ValidateAsync` | §6.2 validator is decoupled; see §A0.6 |
| `IMessagingGateway` | `packages/foundation-integrations/Messaging/IMessagingGateway.cs` | `SendAsync`, `GetStatusAsync` only — NO `ValidateAsync` | §6.2 validator is decoupled; see §A0.6 |
| `IAtlasProjector` | `packages/foundation-wayfinder/IAtlasProjector.cs` | Present | ✓ |
| `IStandingOrderIssuer.IssueAsync` | `packages/foundation-wayfinder/IStandingOrderIssuer.cs` | `Task<StandingOrder> IssueAsync(StandingOrderDraft, ActorId, IAuditTrail, CancellationToken)` | §3.5 uses correct return type + ActorId; see §A0.6 |
| `AtlasView`, `AtlasSettingSnapshot` | `packages/foundation-wayfinder/` | Present; `AtlasSettingSnapshot.LastIssuedBy` is `StandingOrderId` (pointer to issuance event) | §3.6 clarified |
| `EncryptedField` | `packages/foundation-recovery/EncryptedField.cs` | Present | ✓ |
| `IFieldEncryptor.EncryptAsync` | `packages/foundation-recovery/Crypto/IFieldEncryptor.cs` | `Task<EncryptedField> EncryptAsync(ReadOnlyMemory<byte>, TenantId, CancellationToken)` | §5.2 updated for bytes-not-JsonNode |
| `IFieldDecryptor.DecryptAsync` | `packages/foundation-recovery/Crypto/IFieldDecryptor.cs` | `Task<ReadOnlyMemory<byte>> DecryptAsync(EncryptedField, IDecryptCapability, TenantId, CancellationToken)` | §5.3.1 adds capability acquisition |
| `InMemoryAuditTrail` | `packages/kernel-audit/InMemoryAuditTrail.cs` | Present | ✓ |
| `AuditEventType` | `packages/kernel-audit/AuditEventType.cs` | `public readonly record struct AuditEventType(string Value)` | ✓ |
| `ICaptchaVerifier` | `packages/foundation-integrations/Captcha/ICaptchaVerifier.cs` | Present | ✓ |
| `ProviderDescriptor`, `ProviderCategory` | `packages/foundation-integrations/`, `packages/foundation-catalog/` | Present | ✓ |
| `CredentialsReference` | `packages/foundation-integrations/CredentialsReference.cs` | Present | ✓ |
| ADR 0065 | `docs/adrs/0065-wayfinder-system-and-standing-order-contract.md` | Present on origin/main | ✓ |

### §A0.2 — Drift from intake-stub spec (corrected in body)

The intake spec named several symbols that diverge from origin/main; the ADR body uses the canonical origin/main names. Drift summary:

| Intake spec name | Origin/main canonical name | Where the ADR body resolves |
|---|---|---|
| `IMessageTransport` (outbound) + `IMessageReceiver` (inbound) | `IMessagingGateway` (single contract in `packages/foundation-integrations/Messaging/IMessagingGateway.cs` line 14) | §6.2 — validator is decoupled from the gateway |
| `packages/foundation-integrations-payments/` (separate package) | `packages/foundation-integrations/` (single package with `Payments/` subfolder) | §6.2 + §A0.3 |
| `packages/foundation-integrations-messaging/` (separate package) | `packages/foundation-integrations/Messaging/` (subfolder of single package) | §6.2 + §A0.3 |
| `AuditEventType` constants are static readonly strings | Origin/main shape: `public readonly record struct AuditEventType(string Value)` with `public static readonly AuditEventType Foo = new("Foo")` constants | §8 uses canonical record-struct shape |

The drift is structural metadata only — the underlying contract semantics align with what the intake spec described. The ADR makes the canonical names load-bearing.

### §A0.3 — Soft-prerequisite (in flight; not yet on origin/main)

`IAtlasProvider<T>`, `IIdentityAtlasSurface`, and `IHelmWidget` (introduced by ADR 0066) are per ADR 0066 (PR #529, authored 2026-05-04; ADR 0067 is authored against the ADR 0066 specification text, not an origin/main implementation). **Mitigation:** Phase 1 of the §10 implementation checklist MUST land *after* ADR 0066's Phase 1 — the Phase 1 hand-off explicitly carries this dependency. If ADR 0066's surface drifts from its ADR text during build, ADR 0067's Phase 1 hand-off must be re-validated against the post-build origin/main shape; a regenerated §A0 captures the resolution.

### §A0.4 — Correction: `IMeshVpnAdapter` is already on origin/main

**Council correction (2026-05-04):** `IMeshVpnAdapter` IS present on `origin/main` at `packages/foundation-transport/IMeshVpnAdapter.cs` — it is not an uncommitted working-tree artifact. The §A0 pre-acceptance audit incorrectly classified it as in-flight. The actual interface surface on origin/main is:

```csharp
public interface IMeshVpnAdapter : IPeerTransport
{
    string AdapterName { get; }
    Task<MeshNodeStatus> GetMeshStatusAsync(CancellationToken ct);
    Task RegisterDeviceAsync(MeshDeviceRegistration registration, CancellationToken ct);
}
```

**Blocking implication (BLOCKING — left for CO disposition):** `IMeshVpnAdapter` has no `ValidateAsync` method. §6.2's claim that "the validator calls into the adapter's own `IMeshVpnAdapter.ValidateAsync()`" cannot compile against the committed surface. Adding `ValidateAsync` to `IMeshVpnAdapter` is a breaking change to a published transport contract that requires its own ADR amendment. The §6.2 dispatch path for mesh-VPN validation requires rework (non-mechanical; left for author + CO disposition).

### §A0.5 — Net-new package (no prior origin/main artifact)

`packages/ui-core-wayfinder/` does not yet exist on origin/main; it is the package introduced by ADR 0066 (per its §implementation checklist) and extended by ADR 0067. If ADR 0066's package naming changes during council review, ADR 0067 Phase 1 inherits the rename mechanically.

### §A0.6 — Origin/main drift corrections applied to this ADR body

| Drift item | Finding | ADR body resolution |
|---|---|---|
| `IStandingOrderIssuer.IssueAsync` signature | Returns `Task<StandingOrder>`, takes `ActorId`, requires `IAuditTrail`. ADR draft had `ValueTask<StandingOrderId>` + `PrincipalId`. | §3.5 methods now use `Task<StandingOrder>` + remove PrincipalId (ActorId from ambient context §3.11); §6.1 dependency list adds `IAuditTrail` |
| `IPaymentGateway` has no `ValidateAsync` | Method does not exist on origin/main | §6.2 rewritten: validators decouple from runtime gateway; per-provider probe logic |
| `IMessagingGateway` has no `ValidateAsync` | Method does not exist on origin/main | §6.2 same fix |
| `IFieldEncryptor.EncryptAsync` takes `ReadOnlyMemory<byte>` | Draft §5.2 implied `JsonNode` wrapping | §5.2 updated: `IssueSensitiveCredentialAsync` takes `ReadOnlyMemory<byte>` |
| `IFieldDecryptor.DecryptAsync` requires `IDecryptCapability` | Draft §5.3 silent on capability acquisition | §5.3.1 added: `IDecryptCapabilityProvider` injection |
| `AtlasSettingSnapshot.LastIssuedBy` is `StandingOrderId` | Draft §3.6 implied it was a principal | §3.6 prose updated |

---

## §A1 — Council review checklist (apply before status flip)

Council verifies, in addition to the standard adversarial review:

- **WCAG/a11y subagent:**
  - SC 3.3.7 (Redundant Entry): no credential is asked twice in the same session; sensitive credentials with prior values render in "leave unchanged" mode per §5.2.1.
  - SC 3.3.8 (Accessible Authentication): every sensitive `CredentialFieldSpec` has an `AutocompleteHint`; show/hide toggle has an accessible name; `CredentialAutocompleteHint` enum constrains autocomplete tokens to the WHATWG-valid set; no tenant-level credential uses `CurrentPassword`.
  - SC 1.3.1 (Info and Relationships): masking state is conveyed structurally, not just visually.
  - SC 4.1.3 (Status Messages): validation outcomes use `aria-live="polite"` for success, `role="alert"` for errors; validation in-flight emits `aria-busy` + interim live-region message per §5.3.2.
  - SC 1.4.1 (Use of Color): no validation state relies on color alone.
  - SC 3.3.4 (Error Prevention — Legal): license-acknowledgement uses explicit button click, not checkbox.
  - SC 2.1.2 + SC 2.4.3 (modal): license-acknowledgement dialog implements §5.5.1 focus-trap, focus-restoration, ESC-cancel.
  - SC 2.5.3 (Label in Name): adjacent-control accessible names include `DisplayLabel` verbatim per §3.2.
  - SC 3.3.2 (Labels or Instructions): `HelpText` rendered as persistent `aria-describedby` text per §3.2.
  - SC 4.1.2 (Name, Role, Value): show/hide toggle exposes `aria-pressed` + `aria-controls` per §3.2.1.
  - WAI-ARIA APG Tabs: category navigation arrow-key contract per §7.3.1.
- **Security-engineering subagent:**
  - Sensitive credentials traverse `IFieldEncryptor` before any persistence path → asserted by `DefaultIntegrationAtlasProviderTests.SensitiveCredential_IsEncryptedBeforeStandingOrder` (Phase 2).
  - Audit payloads cannot leak credential values → asserted by marker-corpus test in `IntegrationAuditRedactionTests` (Phase 2) + `SUNFISH_INTEGRATION_AUDIT001` analyzer (Phase 2).
  - Provider rotation does not leak old credentials into the new provider's audit payload → asserted by `ProviderRotationTests.RotationAudit_DoesNotContainPriorCredentials` (Phase 2).
  - License acknowledgement issues a Standing Order whose JSON content is audit-ready (no secrets) → asserted by `IntegrationAuditPayloads` factory unit test (Phase 2).
  - The contract surface admits no method returning decrypted credentials to a rendering host → asserted by `ContractSurfaceTests.NoMethodReturnsDecryptedBytes` (reflection over `IIntegrationAtlasProvider`, Phase 1).
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
