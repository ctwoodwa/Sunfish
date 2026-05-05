---
id: 67
title: Atlas Integration-Config UI Surface
status: Accepted
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
amendments:
  - id: council-fix-pass-2026-05-04
    date: 2026-05-04
    summary: |
      Council BLOCK fix-pass on PR #539 (canonical council file
      `icm/07_review/output/adr-audits/0067-council-review-2026-05-04.md`).
      4 BLOCKING + 5 non-mechanical + 8 mechanical + 6 structural-citation
      findings addressed. Substantive rework: (B1) validator-owned
      liveness probe replaces non-existent `ValidateAsync` calls into
      `IPaymentGateway` / `IMessagingGateway` / `IMeshVpnAdapter`;
      (B2) `IFieldDecryptor` capability sourcing specified for user-driven
      and background re-validation paths; (B3) `validation-status` moved
      off the Standing-Order journal to a new `IValidationStatusStore`
      (avoids unbounded append-only growth under background re-validation
      cadence); (B4) `IStandingOrderIssuer.IssueAsync` composition fixed
      (`ActorId` not `PrincipalId`, `IAuditTrail` as method parameter,
      `Task<StandingOrder>` return); (NM1) package shape kept additive
      to existing `packages/ui-core/Wayfinder/Integrations/` per ADR 0066;
      (NM2) license-acknowledgement track CUT entirely, deferred to
      ADR 0067-A1 follow-up amendment with `BannedSymbols.txt` enforcement
      from ADR 0061 remaining canonical until that amendment lands.
  - id: recouncil-mechanical-2026-05-05
    date: 2026-05-05
    summary: |
      Re-council NEEDS-AMENDMENT fix-pass (mechanical-tier; auto-acceptable
      per Decision Discipline Rule 3). Re-council: PR #559 / council file
      `icm/07_review/output/adr-audits/0067-council-review-2026-05-05-recouncil.md`.
      6 mechanical findings addressed: (1) Postmark probe URL fixed from
      `/servers` (Account-Token endpoint) to `/server` (Server-Token
      endpoint, §5.3 + §6.2); (2) NM2 license-acknowledgement deletion
      residue swept at 6 dangling-reference sites (lines 91, 115, 121,
      148, 165, 467) — two inaccurately attributed to ADR 0061 an
      interactive-acknowledgement posture ADR 0061 does not have;
      (3) `ProviderDescriptor.Key` reconciliation drift in §3.1 + §3.7
      reframed — `ProviderName` (kebab-case) and `ProviderDescriptor.Key`
      (reverse-DNS) are different string shapes serving different purposes;
      (4) §3.5 IAuditTrail clarification — constructor-injected trail MUST
      be the canonical kernel-audit singleton, no separate audit channel;
      (5) §3.13 test-fixture audit-disabled overload clarified (mirrors
      TenantKeyProviderFieldDecryptor two-overload pattern);
      (6) §3.14 `IntegrationCapabilityPurposes.IntegrationValidation`
      named constant added as Phase 1 deliverable (avoids magic-string
      drift per cohort precedent).
---
# ADR 0067 — Atlas Integration-Config UI Surface

**Status:** Accepted
**Date:** 2026-05-04
**Authors:** XO research session
**Pipeline variant:** `sunfish-feature-change`
**Council posture:** standard adversarial + WCAG/a11y subagent (mandatory; credential-capture forms hit SC 3.3.7 / 3.3.8 / 4.1.3 / 1.3.1) + security-engineering subagent (credential storage, transport, rotation, license posture)
**Consumer scope:** Anchor admin (Zone A local-first); Bridge tenant admin (Zone C hosted); both share the same `IIntegrationAtlasProvider` contract

---

## Status

Accepted 2026-05-05 (PR #539). Sibling to ADR 0066 (Helm + Identity Atlas Surface). 0066 defines
the *generic* Atlas-issuance contract (`IAtlasProvider<T>`, `IIdentityAtlasSurface`, `IHelmWidget`);
ADR 0067 specializes that contract for the **integration-config layer** — the Wayfinder configuration
tier covering payment gateways, messaging transports, mesh-VPN control planes, and CAPTCHA verifiers.
Stage 06 hand-off at `icm/_state/handoffs/atlas-integration-config-stage06-handoff.md`.

---

## Context

The Wayfinder discovery (W#34 §5.6) classified **Layer 6 — integration configuration** as **Partial coverage**. Sunfish has fully-specified provider-neutrality contracts for every external integration category we ship today:

- ADR 0013 (Provider neutrality) — domain modules never reference vendor SDKs directly; providers are selected by name.
- ADR 0051 (Foundation.Integrations.Payments) — `IPaymentGateway` adapter contract; first-wave adapters `providers-stripe` / `providers-square`.
- ADR 0052 (Bidirectional Messaging Substrate) — provider-neutral messaging gateway; first-wave adapters `providers-postmark` / `providers-sendgrid` / `providers-twilio`.
- ADR 0061 (Three-Tier Peer Transport) — `IPeerTransport` with `TransportTier` enum; mesh adapters `providers-mesh-headscale` / `providers-mesh-tailscale` / `providers-mesh-netbird`; license-screening posture (SSPL / BSL adapters excluded at compile time via `BannedSymbols.txt` analyzer enforcement; an admin opt-in acknowledgement track is deferred to ADR 0067-A1 — see §9.7).
- ADR 0028 §Phase 2.3 (CAPTCHA — already partially landed via `ICaptchaVerifier` in `packages/foundation-integrations/Captcha/`).

What the portfolio does **not** specify is *how a tenant administrator selects, configures, validates, and rotates* these providers. Each adapter today either has no admin UI at all or invents its own ad-hoc surface. There is no consistent place a Bridge tenant admin (or an Anchor desktop user managing their own node) goes to ask "which payment gateway is active in this tenant?" — let alone to change it. There is no place that captures credentials in a uniform encrypted-at-rest envelope, no place that runs validation against the provider before activating it, and no place that emits audit events when a provider rotates.

**ADR 0065** (Wayfinder System + Standing Order Contract) gave Sunfish the substrate for *every* configuration layer: a `StandingOrder` is the event-type primitive that captures a configuration change; `IStandingOrderIssuer` validates and issues; `IAtlasProjector` computes the materialized view; `AtlasView` / `AtlasSettingSnapshot` carry the current value at a Wayfinder path. **ADR 0066** (Helm + Identity Atlas) generalized that into a UI-surface contract: `IAtlasProvider<T>` renders a configuration section from the projection; `IIdentityAtlasSurface` is the canonical issuance UX; `IHelmWidget` is the read-only live-state observer. ADR 0067 is the **first concrete `IAtlasProvider<T>` specialization** — the Atlas surface for integration providers — and serves as the proof-of-pattern for the four remaining Wayfinder layers (security policy, account-identity, domain config, user preferences).

**Wayfinder vs Helm boundary.** Wayfinder is the **deep-config surface**: the place a tenant admin goes to *change* configuration, where every mutation is a Standing Order issued through `IStandingOrderIssuer` and audited by construction. Helm is the **live-state-glance surface**: the place an operator goes to *observe* current system state, where reads are projection-only and no mutation occurs. ADR 0067 is a Wayfinder layer — the integration-configuration tier — not a Helm widget. ADR 0066 carved this boundary explicitly; ADR 0067 inherits it.

**ADR 0066 vs ADR 0067 type distinction (for fresh readers).** ADR 0066 introduces `AtlasView` (the foundation-tier projection output of `IAtlasProjector`); ADR 0067 introduces `IntegrationAtlasView` (a category-specific projection consumed by `IIntegrationAtlasProvider`). The two are different types: `AtlasView` is the substrate-level Wayfinder projection; `IntegrationAtlasView` (§3.6) is the Atlas-fine integration-category shape this ADR ships. `IIntegrationAtlasProvider` composes `IAtlasProjector` internally to produce `IntegrationAtlasView`; the rendering host never sees a raw `AtlasView` for the integration tier. The terms "Atlas surface" (the rendered UI) and "Atlas provider" (the `IAtlasProvider<T>` implementation) are used interchangeably throughout this ADR — they refer to the same concept viewed from the rendering side and the contract side respectively.

The decision is load-bearing for Phase 2 commercial scope (W#5 — six tenants spanning payment / banking / SMS / email integrations) and for several already-queued workstreams (W#22 Leasing Pipeline payment+email; W#28 Public Listings CAPTCHA+email; W#30 mesh-VPN provider selection). Without this surface, every future block that needs a provider-backed integration must invent its own admin UX, defeating the framework-agnostic principle.

---

## Decision drivers

1. **One surface, every category.** Whether the admin is configuring a payment gateway, an email transport, an SMS transport, a mesh VPN, or a CAPTCHA verifier, the rendering pipeline is identical: pick a provider, enter credentials in a form whose schema is declared *by the provider adapter*, validate, issue. Avoiding category-specific UI codepaths is the framework-agnostic test.
2. **Schema-driven, not hardcoded.** Adapter packages must be able to ship without coordinated UI changes. A new `providers-paddle` package landing in 2026-Q3 must be able to register itself, declare its credential schema, and appear in the Atlas dropdown without a single line of code change to `foundation-integrations` or to the rendering Atlas component. This is the same pattern VS Code's settings.json + Settings UI co-evolution uses (extensions self-declare configuration schema; the Settings UI reflects).
3. **Audit-by-construction.** Every provider change, every credential update, every validation outcome MUST emit an `AuditRecord` per ADR 0049 — not by convention but because the issuance API requires the audit-emitter dependency. A provider rotation that is not audited is a compliance gap (especially for the Phase 2 commercial scope where providers move money).
4. **Sensitive-by-default storage.** API keys, webhook secrets, mesh control-plane tokens, SMS auth tokens — all sensitive credentials MUST be stored as `EncryptedField` per ADR 0046-A2. Plaintext appears only at the moment the adapter consumes the value via `IFieldDecryptor`. The Atlas form view MUST mask sensitive fields by default and offer an explicit show/hide toggle with WCAG-compliant accessible labelling (SC 1.3.1).
5. **Optimistic issuance + Mission-Space-gated availability.** Issue the Standing Order first; validate after. A failing validation does NOT roll the order back — it marks `integrations.{category}.validation-status` as `failed`, which the ADR 0062 `IMissionEnvelopeProvider` reads to gate downstream feature availability. This decouples *config persistence* from *config workability* and lets the admin save a partially-correct state to fix later.
6. **License-screening deferred to ADR 0067-A1.** ADR 0061 enforces SSPL/BSL adapter exclusion at compile time via `BannedSymbols.txt` analyzer enforcement; there is no admin opt-in path on origin/main and ADR 0067 v1 does not introduce one. A follow-up amendment (ADR 0067-A1) is queued to design a license-acknowledgement opt-in track if and when general counsel approves the posture; until that lands, the v1 Atlas surface ships with `IntegrationCategory.MeshVpn` populated by adapters whose license posture is `Permissive` (per ADR 0061's already-canonical exclusion list). See §9.7.
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
- No uniform license-posture enforcement: every mesh adapter would individually consult ADR 0061's `BannedSymbols.txt` list rather than inheriting it from a central contract.

**Verdict.** Rejected — this is the implicit current state, and the partial-coverage classification in W#34 §5.6 is a direct consequence of staying here.

### Option B — Unified `IIntegrationAtlasProvider` with dynamic schema rendering (this ADR's choice)

A single `IIntegrationAtlasProvider` (specialization of ADR 0066's `IAtlasProvider<T>`) handles every integration category. Adapter packages declare an `IntegrationProviderSchema` (provider name, category, credential field specs, autocomplete hints). The Atlas component reads the schema and dynamically renders the form. Validation, audit emission, encrypted storage, and rotation flow are all centralized.

**Pros.**
- One implementation of credential masking, accessible authentication, and Status Messages — verified once against WCAG 2.2 AA.
- New adapters compose without UI changes — they ship a schema; the Atlas surface picks it up.
- Audit emission is contractual; you cannot construct an `IIntegrationAtlasProvider` without the audit-trail dependency, so omission is impossible.
- Multi-transport routing has a natural location (the routing path under each category).
- (License-posture acknowledgement was a v1 driver in an earlier draft; deferred to ADR 0067-A1 per the council fix-pass — see §9.7.)
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
- Cross-category concerns (credential masking, WCAG-compliant accessible authentication, audit emission) must be re-implemented or re-composed per category — multiplicative work and multiplicative a11y test burden.
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
- License-posture for mesh VPN — ADR 0061's compile-time `BannedSymbols.txt` exclusion can still be enforced, but if a future ADR 0067-A1 admin-opt-in track lands, Option C cannot capture the acknowledgement.
- Anchor's local-first model wants config to live in the per-node Atlas (so it follows the node), not in a file outside the Sunfish state.

**Verdict.** Rejected. Option C is what we have for *bootstrapping* (host-level config like `KEK_PATH`); it is not viable for tenant-facing integration configuration.

---

## Decision

Adopt Option B. Sunfish ships a unified `IIntegrationAtlasProvider` contract — the first concrete specialization of ADR 0066's `IAtlasProvider<T>` — with dynamic schema-driven rendering, encrypted credential storage, audit-by-construction, optimistic issuance with Mission-Space-gated availability, and non-destructive provider rotation. License-acknowledgement opt-in for SSPL/BSL adapters is explicitly out of scope for v1 and deferred to ADR 0067-A1 (see §9.7); ADR 0061's `BannedSymbols.txt` analyzer enforcement remains the canonical exclusion mechanism until that follow-up amendment lands.

The contract surface (§3 — §6) is **additive** to the existing `packages/ui-core/` package introduced by ADR 0066: new types live under `packages/ui-core/Wayfinder/Integrations/` with namespace `Sunfish.UICore.Wayfinder.Integrations`. No new package is created — this preserves ADR 0066's "additive, no new package required" framing. Reference implementations (§7) live in the same `packages/ui-core/Wayfinder/Integrations/` subtree. Adapter packages (`providers-stripe`, `providers-postmark`, `providers-twilio`, `providers-mesh-tailscale`, etc.) gain a single new export — the `IIntegrationSchemaProvider` registration (§6.1) — without changing their existing adapter contracts.

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
```

`{category}` is the kebab-cased `IntegrationCategory` value (`payments`, `transactional-email`, `marketing-email`, `sms`, `mesh-vpn`, `captcha`). `{provider}` is the provider name as registered (e.g. `providers-stripe`). `{credential}` is the credential field key declared in the provider's `IntegrationProviderSchema` (e.g. `api-key`, `webhook-secret`).

**Validation status is NOT a Standing Order.** Validation outcomes are transient state — repeatedly refreshed by background re-validation runs — not configuration intent. Per the council fix-pass, validation status lives in a separate `IValidationStatusStore` (§3.13) with its own append/read contract and audit emission, NOT in the Standing-Order journal. Modeling it as a Standing Order would generate ~100k orders per provider per tenant per year under the §5.6.1 staleness cadence, which violates the substrate's append-only-log scaling envelope.

**License-acknowledgement is NOT a Standing Order in v1.** Per the council fix-pass and ADR 0067-A1 deferral (§9.7), no `license-acknowledged.{provider}` path is issued in v1. ADR 0067-A1 will design the path shape if a license-acknowledgement opt-in track is approved; until then ADR 0061's compile-time `BannedSymbols.txt` enforcement is canonical and Atlas does not surface acknowledgement UX.

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

### §2.4 — `license-acknowledged.{provider}` (deferred to ADR 0067-A1)

The license-acknowledgement Standing-Order path was previously specified here. Cut from v1 per the council fix-pass and §9.7 — ADR 0067-A1 will define the path shape (and the issuance ordering relative to `active-provider`) if and when general counsel approves an SSPL/BSL admin-opt-in track. Until ADR 0067-A1 lands, ADR 0061's compile-time `BannedSymbols.txt` enforcement is the canonical exclusion mechanism; the Atlas surface emits no acknowledgement-related Standing Orders.

### §2.5 — Validation-status state (NOT a Standing Order)

Validation outcomes are stored in a dedicated `IValidationStatusStore` (§3.13) with its own append/read contract and its own audit emission (`IntegrationValidationSucceeded` / `IntegrationValidationFailed` per §8). The store is read by the ADR 0062 `IMissionEnvelopeProvider` to gate Sunfish features that require the integration. A `Failed` / `Invalid` / `Unreachable` status does NOT prevent the active-provider value from persisting; it only signals to consumers that the integration is not currently usable. See §3.13 for the contract and §5.6.1 for the staleness/TTL semantics.

---

## §3 — New types

All types live under `packages/ui-core/Wayfinder/Integrations/` (additive to the existing `packages/ui-core/` package — no new package), namespace `Sunfish.UICore.Wayfinder.Integrations`, except where noted. This placement preserves ADR 0066's "additive, no new package required" framing — see §"Decision".

### §3.1 — `IntegrationProviderSchema`

```csharp
public sealed record IntegrationProviderSchema
{
    public required string ProviderName { get; init; }
    public required IntegrationCategory Category { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<CredentialFieldSpec> CredentialFields { get; init; }
    public int SchemaVersion { get; init; } = 1;
    public string? DocumentationUrl { get; init; }
}
```

`ProviderName` is the canonical Atlas-fine identity for schema lookup (e.g. `providers-stripe`, kebab-case). It is distinct from `ProviderDescriptor.Key`'s reverse-DNS form (e.g. `sunfish.providers.stripe`) — see §3.7 reconciliation note. `SchemaVersion` enables forward migration when a provider's credential shape changes (§4.2).

> **License posture deferred.** A `LicensePostureKind? LicensePosture` field was previously specified on this record; it is cut from v1 per the council fix-pass and §9.7. ADR 0067-A1 will reintroduce a license-posture surface if and when an SSPL/BSL opt-in track is approved by general counsel. Until then, the v1 surface assumes all registered adapters comply with ADR 0061's compile-time `BannedSymbols.txt` exclusion list (i.e., effectively `Permissive` posture) and the Atlas UI does not render any license-acknowledgement affordance.

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

### §3.3 — `LicensePostureKind` (deferred to ADR 0067-A1)

The `LicensePostureKind` enum was previously specified here (`Permissive` / `WeakCopyleft` / `StrongCopyleft`). Cut from v1 per the council fix-pass — ADR 0067-A1 will reintroduce the type and the per-license-kind UX (acknowledgement modal, audit event, Standing-Order path) if and when general counsel approves an SSPL/BSL admin-opt-in track. ADR 0061's compile-time `BannedSymbols.txt` analyzer enforcement is canonical until that follow-up amendment lands; see §9.7.

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
    /// <summary>Lists registered provider schemas for <paramref name="category"/>; consumed by the Atlas dropdown in the rendering host.</summary>
    ValueTask<IReadOnlyList<IntegrationProviderSchema>> GetAvailableProvidersAsync(
        IntegrationCategory category,
        CancellationToken ct);

    /// <summary>Returns the active provider name for <paramref name="category"/>, or <c>null</c> when no provider is active.</summary>
    ValueTask<string?> GetActiveProviderAsync(
        IntegrationCategory category,
        CancellationToken ct);

    /// <summary>Activates <paramref name="providerName"/> for <paramref name="category"/> by issuing a Standing Order at <c>integrations.{category}.active-provider</c>; non-destructive — prior provider's credentials are preserved per §5.7.</summary>
    Task<StandingOrder> IssueProviderChangeAsync(
        IntegrationCategory category,
        string providerName,
        CancellationToken ct);

    /// <summary>Encrypts <paramref name="plaintextBytes"/> via <c>IFieldEncryptor</c> and issues the resulting <c>EncryptedField</c> at the credential's Standing-Order path; emits <c>IntegrationCredentialUpdated</c> audit event (key only, never value).</summary>
    Task<StandingOrder> IssueSensitiveCredentialAsync(
        IntegrationCategory category,
        string providerName,
        string credentialKey,
        ReadOnlyMemory<byte> plaintextBytes,
        CancellationToken ct);

    /// <summary>Issues <paramref name="value"/> as-is at the credential's Standing-Order path (no encryption); used for non-sensitive credentials per the schema's <c>CredentialFieldSpec.Sensitive</c> flag.</summary>
    Task<StandingOrder> IssueNonSensitiveCredentialAsync(
        IntegrationCategory category,
        string providerName,
        string credentialKey,
        JsonNode value,
        CancellationToken ct);

    /// <summary>Runs the registered <c>IIntegrationProviderValidator</c> for <paramref name="providerName"/>, persists the outcome via <c>IValidationStatusStore</c> (§3.13), and returns the result. Background re-validation is a separate concern — see §5.3 for capability sourcing on user-driven vs unattended runs.</summary>
    Task<IntegrationValidationResult> ValidateProviderAsync(
        IntegrationCategory category,
        string providerName,
        CancellationToken ct);

    /// <summary>Issues two Standing Orders atomically (transactional + marketing email routing paths) per §2.3; throws <c>UnknownProviderException</c> if either named provider is not registered.</summary>
    Task<StandingOrder> IssueRoutingAsync(
        IntegrationEmailRouting routing,
        CancellationToken ct);
}
```

**Issuance composition rationale.** Each `Issue*Async` wrapper above composes `IStandingOrderIssuer.IssueAsync(StandingOrderDraft draft, ActorId issuedBy, IAuditTrail auditTrail, CancellationToken ct)` (per the actual signature on origin/main at `packages/foundation-wayfinder/IStandingOrderIssuer.cs`). The wrapper is intentionally **domain-shaped** (not substrate-shaped) per the council §9.2 disposition: callers pass category-specific arguments rather than constructing a `StandingOrderDraft` themselves, and the `ActorId issuedBy` and `IAuditTrail auditTrail` parameters required by `IssueAsync` are sourced internally — `ActorId` from `IIntegrationAtlasContext.CurrentActorId` (§3.11), `IAuditTrail` from the constructor-injected `IAuditTrail` dependency (§6.1). The wrappers return `Task<StandingOrder>` (matching `IStandingOrderIssuer.IssueAsync`'s return shape) so downstream callers retain access to the realized `StandingOrder.Id`, `IssuedAt`, and `AuditRecordId` without requiring a separate read against the Standing-Order repository.

This composition pattern is the canonical "domain-shaped wrapper over substrate API" idiom that ADR 0066's `IIdentityAtlasSurface` also follows — keeping the rendering-host API simple while preserving substrate audit-by-construction guarantees. The constructor-injected `IAuditTrail` MUST be the host's canonical kernel-audit singleton; ADR 0067 implementations MUST NOT introduce a separate audit channel.

> **License-acknowledgement issuance method removed.** The `IssueLicenseAcknowledgementAsync(IntegrationCategory, string, LicensePostureKind, CancellationToken)` method previously specified on this contract is cut from v1 per the council fix-pass and §9.7. ADR 0067-A1 will reintroduce the method (or a successor shape) if and when an SSPL/BSL opt-in track is approved.

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
    ActorId ActivatedBy);
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

**Reconciliation with `ProviderDescriptor`.** Adapter packages ALREADY register a `ProviderDescriptor` (per ADR 0013) at startup. `IIntegrationSchemaProvider` is *additive* — it does not replace `ProviderDescriptor`; it carries the additional schema metadata that the Atlas surface needs (credential field specs, autocomplete hints) which is intentionally absent from `ProviderDescriptor` (which serves the runtime-routing concern, not the configuration-UX concern). Note that `IntegrationProviderSchema.ProviderName` (e.g. `providers-stripe`, kebab-case) and `ProviderDescriptor.Key` (e.g. `sunfish.providers.stripe`, reverse-DNS) carry different string shapes serving different purposes — `ProviderName` is the Atlas-fine identity for schema lookup; `ProviderDescriptor.Key` is the catalog-substrate routing identity. They are not required to be identical strings; see §9.1 for the deferred question of reconciling the two naming conventions.

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
}
```

`Unreachable` distinguishes a provider that cannot be reached (network, DNS, suspended account) from `Invalid` (provider is reachable but rejects the credentials). `Unknown` is the initial state before any validation has run, and the post-rotation state when the new provider has no credentials yet.

> **License-acknowledgement status value removed.** A `LicenseAcknowledgementRequired = 4` enum value previously sat alongside the four above; it is cut from v1 per the council fix-pass and §9.7. ADR 0067-A1 will reintroduce the value (or a sibling enum) if and when an SSPL/BSL opt-in track is approved.

### §3.10 — `LicenseAcknowledgementRequiredException` (deferred to ADR 0067-A1)

The `LicenseAcknowledgementRequiredException` class was previously specified here. Cut from v1 per the council fix-pass and §9.7 — there is no `IssueProviderChangeAsync` license-posture precondition in v1 because no license-acknowledgement track exists. ADR 0067-A1 will reintroduce the exception (or a successor shape) if and when an SSPL/BSL opt-in track is approved by general counsel.

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

### §3.13 — `IValidationStatusStore`

```csharp
public interface IValidationStatusStore
{
    /// <summary>Returns the most recent validation status for <paramref name="provider"/> in <paramref name="tenant"/>; <c>null</c> when no validation has ever run for the (tenant, provider) pair.</summary>
    ValueTask<ProviderValidationStatusEntry?> GetCurrentAsync(
        TenantId tenant,
        string provider,
        CancellationToken ct);

    /// <summary>Persists a new status snapshot. Emits an audit record (<c>IntegrationValidationSucceeded</c> or <c>IntegrationValidationFailed</c> per §8) via the constructor-injected <c>IAuditTrail</c>; for production wiring, the audit emission is non-optional. For test fixtures (<c>InMemoryValidationStatusStore</c>), an audit-disabled construction overload is permitted, mirroring <c>TenantKeyProviderFieldDecryptor</c>'s two-overload pattern.</summary>
    ValueTask UpdateAsync(
        TenantId tenant,
        string provider,
        ProviderValidationStatusEntry entry,
        ActorId issuedBy,
        CancellationToken ct);

    /// <summary>Returns the historical status entries for <paramref name="provider"/> in <paramref name="tenant"/>, newest first; bounded by <paramref name="limit"/>. Used for surfacing "last 10 validation runs" in the Atlas UI.</summary>
    IAsyncEnumerable<ProviderValidationStatusEntry> HistoryAsync(
        TenantId tenant,
        string provider,
        int limit,
        CancellationToken ct);
}

public sealed record ProviderValidationStatusEntry
{
    public required ProviderValidationStatus Status { get; init; }
    public required DateTimeOffset ValidatedAt { get; init; }
    public required ActorId ValidatedBy { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
```

**Substrate distinction.** `IValidationStatusStore` is a separate substrate from `IStandingOrderIssuer` because validation outcomes are **transient state**, not configuration intent. Standing Orders are append-only and intended to capture operator decisions ("activate provider X", "set credential Y") — issuing one Standing Order per validation run for every provider every 5 minutes (per §5.6.1's TTL-based re-validation) would generate ~100k orders per provider per tenant per year, breaking the substrate's append-only-log scaling envelope (the council fix-pass specifically called this out as a load-bearing scaling failure mode). The store's storage strategy is implementation-defined: it MAY use a per-(tenant, provider) keyed compaction strategy (only the last N entries retained for history), an append-only log with offline compaction, or any other strategy that satisfies the contract. The store's audit emission via `IAuditTrail` is non-optional for production wiring — every `UpdateAsync` call MUST emit either `IntegrationValidationSucceeded` (status == Valid) or `IntegrationValidationFailed` (any other status), per §8. Test fixtures (`InMemoryValidationStatusStore`) MAY be constructed with an audit-disabled overload (mirroring `TenantKeyProviderFieldDecryptor`'s two-overload pattern); production DI wiring MUST use the audit-enabled constructor.

**Reference implementation.** Phase 2 (§10) ships `DefaultValidationStatusStore` (file-system-backed for Anchor; SQL-backed for Bridge) and `InMemoryValidationStatusStore` (test fixture; same shape as `InMemoryAuditTrail` per `packages/kernel-audit/InMemoryAuditTrail.cs`).

### §3.14 — `IDecryptCapabilityProvider`

```csharp
public interface IDecryptCapabilityProvider
{
    /// <summary>Returns a short-lived <see cref="IDecryptCapability"/> bound to <paramref name="tenant"/> for the requested <paramref name="purpose"/> and TTL, or <c>null</c> when no capability can be issued for the requested scope. Implementations MUST validate the requested tenant scope before issuing — cross-tenant capability issuance is prohibited per ADR 0046-A4.</summary>
    ValueTask<IDecryptCapability?> AcquireAsync(
        TenantId tenant,
        string purpose,
        TimeSpan ttl,
        CancellationToken ct);
}
```

`IDecryptCapabilityProvider` is a NEW symbol introduced by this ADR (no prior origin/main artifact). The reference implementation ships in `foundation-recovery` per Phase 2 (§10); the host's `AddSunfishRecovery()` extension registers it as a singleton. The `purpose` string is canonical: ADR 0067 uses `"integration-validation"` for both user-driven and background paths; ADR 0067-A1 may add additional purpose strings if license-acknowledgement decryption requires a separate scope.

**Purpose-string named constant (Phase 1 deliverable).** Per cohort precedent (`TenantKeyProviderFieldEncryptor.PurposeLabel`, `KeyDerivationPurposeLabels`, etc.), the purpose string MUST NOT be a bare magic string at call sites. Phase 1 MUST declare:

```csharp
/// <summary>Purpose-string constants for <see cref="IDecryptCapabilityProvider.AcquireAsync"/> calls issued by ADR 0067 paths.</summary>
public static class IntegrationCapabilityPurposes
{
    /// <summary>Purpose for acquiring a decrypt capability scoped to integration-config credential validation.</summary>
    public const string IntegrationValidation = "integration-validation";
}
```

All ADR 0067 call sites (both user-driven and background re-validation paths) MUST reference `IntegrationCapabilityPurposes.IntegrationValidation` rather than the bare string literal.

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

**Encrypted-field key-rotation handling (§4.2.1).** Sensitive credentials are stored as `EncryptedField` (per ADR 0046-A2), which carries a `KeyVersion`. When a tenant's KEK rotates from v1 → v2, existing credential Standing Orders retain `KeyVersion = 1` envelopes. The v1 reference implementation **maintains both v1 and v2 decryptors simultaneously**: `IFieldDecryptor` resolves the per-version DEK at decrypt time (per `TenantKeyProviderFieldDecryptor` on origin/main) and the validator continues to function across the rotation. A re-encrypt sweep that rewrites prior credentials at the new key version is **out of v1 scope** (deferred to a future amendment per §9.4); ADR 0067 v1 relies on the multi-version decryptor pattern. Adapter authors do NOT see `KeyVersion` — the substrate handles it.

**License-acknowledgement migration (deferred to ADR 0067-A1).** A previous draft of this section specified a license-acknowledgement migration ladder (Permissive → WeakCopyleft etc.). Cut from v1 per the council fix-pass and §9.7 — ADR 0067-A1 will reintroduce the migration semantics if and when a license-acknowledgement track is approved.

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

After all credentials are issued, the admin clicks "Validate" (or a background re-validation timer fires per §5.6.1):

1. Caller invokes `ValidateProviderAsync(category, provider, ct)`.
2. Implementation:
   - Materializes the credentials from the Atlas projection (`IAtlasProjector.ProjectAsync` returns the current `AtlasView`; the §3.6 `IntegrationAtlasView.CredentialsByProvider[provider]` carries the per-credential `AtlasSettingSnapshot`).
   - Acquires an `IDecryptCapability` per §5.3.1 (sourcing differs for user-driven vs background runs).
   - Decrypts sensitive fields via `IFieldDecryptor.DecryptAsync(field, capability, tenant, ct)` — each successful decrypt emits a `FieldDecrypted` audit record per ADR 0046-A4; failures emit `FieldDecryptionDenied`.
   - Resolves the registered `IIntegrationProviderValidator` for `(category, provider)` (per §6.2 resolution rules) and calls its `ValidateAsync(sensitiveCredentials, nonSensitiveCredentials, ct)` — the validator owns its own liveness probe (HTTP API call, mesh control-plane handshake, etc.); it does NOT delegate to any method on `IPaymentGateway` / `IMessagingGateway` / `IMeshVpnAdapter` (those contracts have no `ValidateAsync` method on origin/main, and extending them would require its own ADR amendment).
   - Captures the result.
3. Implementation calls `IValidationStatusStore.UpdateAsync(tenant, provider, entry, actorId, ct)` (§3.13) which persists the outcome and emits the audit event.
4. Audit event `IntegrationValidationSucceeded` (status == Valid) or `IntegrationValidationFailed` (any other status) emitted by the store, NOT by issuing a Standing Order — see §2.5 for the rationale.
5. The result is returned to the caller and rendered in the surface (§5.6 — visual feedback).

**No transport-contract `ValidateAsync` extension.** Per the council fix-pass, the validation flow does NOT call into the runtime egress contracts:

- `IPaymentGateway` (ADR 0051) exposes only `AuthorizeAsync` / `CaptureAsync` / `RefundAsync`.
- `IMessagingGateway` (ADR 0052) exposes only `SendAsync` / `GetStatusAsync`.
- `IMeshVpnAdapter` (ADR 0061) exposes only `AdapterName` / `GetMeshStatusAsync` / `RegisterDeviceAsync`.

None has a `ValidateAsync` method, and ADR 0067 cannot extend those published transport contracts — that would be a breaking-change amendment to ADR 0051 / 0052 / 0061 each. Instead, every `IIntegrationProviderValidator` implementation owns its own liveness probe (e.g., `StripeIntegrationValidator` issues an HTTP `GET /v1/account` against the Stripe API using the configured credentials; `PostmarkIntegrationValidator` issues `GET /server` (singular — fetches the current server's settings using the Server-Token, matching the minimal-side-effect pattern of Stripe's `/v1/account`); `TailscaleIntegrationValidator` issues a `GET /api/v2/tailnet/{tailnet}/keys`). Validators are free to reuse the adapter package's own HTTP client; what they MUST NOT do is depend on a substrate method that does not exist.

### §5.3.1 — Decrypt-capability sourcing

`IFieldDecryptor.DecryptAsync` requires an `IDecryptCapability` per ADR 0046-A2/A3 — the capability is the access-control gate. ADR 0067 sources the capability via three paths depending on the validation trigger:

**1. User-driven validation (admin clicked "Validate").** The session capability of the issuing principal is used. `DefaultIntegrationAtlasProvider` reads `IIntegrationAtlasContext.CurrentActorId` (§3.11) and resolves the corresponding session capability via the host's authentication scope (Bridge: ASP.NET `HttpContext.User` + the recovery substrate's session-bound `IDecryptCapability`; Anchor: the local-node identity's capability). The session capability's `ValidateForDecrypt(tenant, now)` MUST succeed for the current tenant; rejection causes `ValidateProviderAsync` to fail-closed per the negative case below.

**2. Background re-validation (scheduler or webhook trigger).** No human principal is present. `DefaultIntegrationAtlasProvider` resolves a **system-principal capability** with explicit tenant scope from the injected `IDecryptCapabilityProvider`. The capability is short-lived (TTL = 60s, refreshed per validation run) and tenant-scoped — the `IDecryptCapabilityProvider` MUST emit a fresh capability bound to the specific tenant whose validation status is being refreshed. Cross-tenant leakage in the Bridge multi-tenant case is prevented by tenant-scoped capability issuance: the host process holds N tenant capabilities; the wrong one is rejected by `IDecryptCapability.ValidateForDecrypt`. Every system-principal capability acquisition is itself audited by the recovery substrate (per ADR 0046-A4) — successful decrypts emit `FieldDecrypted` records carrying the capability id, and rejections emit `FieldDecryptionDenied`.

**3. Negative case (fail-closed).** If no capability is available — capability provider not registered, capability validation fails, capability TTL expired, capability tenant-scope mismatched — `ValidateProviderAsync` MUST NOT silently skip decryption. Instead it MUST:

- Return `IntegrationValidationResult { Status = Unknown, ErrorCode = "no-decrypt-capability", ErrorMessage = "Decrypt capability unavailable; validation skipped." }` for the user-driven path.
- Emit `IntegrationValidationFailed` audit (not `Succeeded`) with the same `ErrorCode`.
- Persist the `Unknown` status to `IValidationStatusStore` so consumers (`IMissionEnvelopeProvider`) treat the integration as not-currently-usable.
- For the background-driven path, additionally emit a host-level diagnostic so the operator notices a misconfigured `IDecryptCapabilityProvider`.

The `ErrorCode` constant `"no-decrypt-capability"` is the canonical name for this failure mode. Adapter packages MUST NOT introduce alternative error codes for decrypt-capability failures; this is a host-platform concern, not a per-adapter concern.

`IDecryptCapabilityProvider` is a NEW symbol introduced by this ADR (no prior origin/main artifact); the contract surface is a single method `ValueTask<IDecryptCapability?> AcquireAsync(TenantId tenant, string purpose, TimeSpan ttl, CancellationToken ct)` returning `null` when no capability can be issued for the requested scope. Implementations are host-supplied; reference implementations ship in `foundation-recovery` per Phase 2 (§10).

### §5.3.2 — Plaintext lifetime constraints

The `IIntegrationProviderValidator.ValidateAsync` method receives sensitive credentials as `ReadOnlyMemory<byte>` (not `JsonNode`) per §6.2.1. Implementations MUST:
1. Clear (`CryptographicOperations.ZeroMemory`) any owned plaintext buffers in a `finally` block after `ValidateAsync` returns.
2. NEVER log credential bytes, include them in exception messages, or retain references after the method returns.
3. NEVER cache decrypted credentials across calls.
Phase 3 per-adapter parity tests assert these properties (positive: inject a marker credential, verify no log output contains the marker; negative: simulate a provider failure, assert exception message does not contain the credential).

### §5.3.3 — Validation in-flight UX

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

### §5.5 — License posture acknowledgement (deferred to ADR 0067-A1)

A license-acknowledgement modal flow was previously specified here for `StrongCopyleft` providers (e.g. `providers-mesh-headscale`). Cut from v1 per the council fix-pass and §9.7 — ADR 0061's compile-time `BannedSymbols.txt` analyzer enforcement is the canonical exclusion mechanism for SSPL/BSL adapters until ADR 0067-A1 designs an admin-opt-in track. Modal-accessibility requirements (focus trap, focus restoration, ESC-cancel, SC 3.3.4 explicit-action) move to ADR 0067-A1.

### §5.6 — Visual feedback

After validation completes the surface MUST present a clear status indicator per the WCAG 4.1.3 Status Messages contract:

| Status | Visual | Accessibility |
|---|---|---|
| `Valid` | Green check icon + "Connected" text | `aria-live="polite"`; text-only readers receive the "Connected" message |
| `Invalid` | Red alert icon + error code + error message | `role="alert"`; immediately announced |
| `Unreachable` | Amber warning icon + "Unreachable: {reason}" | `role="alert"`; announced |
| `Unknown` (pre-validation) | Neutral grey + "Not yet validated" | not announced (initial state) |
| `Unknown` (post-rotation, post-credential-clear) | Neutral grey + "Not yet validated" | `aria-live="polite"` — IS a transition the user needs to know about |

Color is never the sole signal; every state pairs a shape-distinct icon with the color (per SC 1.4.1). Required icon shapes: `Valid` = check mark (✓); `Invalid` = circle-with-X; `Unreachable` = cloud-with-slash; `Unknown` = em-dash or empty circle. The icon's accessible name (`aria-label` or visually-hidden text) MUST match the status label text.

**`Unknown` announcement disambiguation (per WCAG SC 4.1.3 council finding §6.4).** The renderer MUST distinguish two arrival paths to `Unknown`:
- **First render** (no prior validation has ever run for the (tenant, provider) pair): `Unknown` is the initial state; MUST NOT be announced via `aria-live`.
- **Post-rotation** (the `active-provider` Standing Order changed and the new provider has no credentials yet, OR the admin cleared credentials via §5.7's "Clear unused credentials" affordance): `Unknown` IS a state transition; MUST be announced via `aria-live="polite"`.

Implementations track the previous-state via a per-category renderer-local hook; the `IntegrationAtlasView.StatusByCategory` projection is augmented with a transient "previous status" sidecar (`§3.6` covers the projection shape).

The Status Message DOM region MUST be a single element per category panel with `aria-atomic="true"`. Status MUST NOT be re-announced on successive identical validation outcomes; only *status transitions* replace the live-region content. On `Unknown` first-render the live region MUST contain an empty text node, not stale content from a prior session.

### §5.6.1 — Validation staleness and Mission-Space gating

Validation outcomes are *additive*. A new `ValidateProviderAsync` run DOES NOT overwrite a prior `Valid` status until the new outcome is `Valid` *or* the prior `Valid` outcome is older than `ValidationStalenessTtl` (default 24h, tenant-configurable). The `IMissionEnvelopeProvider` (ADR 0062) reads the *most-recently-Valid status within TTL*, falling back to `Unknown` once stale. This prevents a transient network outage (yielding `Unreachable`) from immediately demoting feature availability. `IntegrationAtlasView.StatusByCategory` exposes the most-recently-Valid status, not the last validation outcome. Background re-validation runs at the same TTL cadence (default every 6h, configurable), each persisted to `IValidationStatusStore` with the `ActorId` of the host's system principal.

### §5.7 — Provider rotation

Admin changes the active provider (e.g. Stripe → Square):

1. Atlas calls `IssueProviderChangeAsync(Payments, "providers-square", …)`.
2. Implementation issues the Standing Order. The previous provider's credentials at `integrations.payments.credentials.providers-stripe.*` are NOT deleted.
3. Audit event `IntegrationProviderChanged` emitted carrying both `previousProvider` and `newProvider` in the payload.
4. The Atlas projection now treats `providers-square`'s credentials (if any) as the "live" credentials. If `providers-square` has no credentials yet, the `IntegrationAtlasView.StatusByCategory[Payments]` reads `Unknown` and the surface prompts credential entry.
5. The admin retains the option to revert by re-issuing `active-provider = "providers-stripe"`; the prior credentials are still in the Standing Order log.
6. To explicitly clear stale credentials, the admin uses a "Clear unused credentials" affordance (§7.3 — out of v1 surface scope; deferred).

**Webhook-secret rotation window (providers-stripe, providers-twilio).** For providers that issue webhooks back to Sunfish, a rotation event creates a transition window where webhooks signed with the *previous* webhook secret may arrive after the new active provider's Standing Order has issued. The `IIntegrationProviderValidator` for these providers MUST accept signatures from EITHER the previous OR the new webhook-secret during the rotation grace window, and MUST surface both secrets to the adapter's webhook-signature-verification path. The Atlas projection retains both credential sets precisely to support this.

**Grace-window authority.** The grace window ends when the previous credential's `EncryptedField` is excluded from a future Atlas projection — i.e., the next rotation evicts it via the §5.7 step 6 "Clear unused credentials" admin action, OR a future rotation issues a new credential at the same `(provider, credentialKey)` path that supersedes the prior value via Standing-Order LWW semantics. There is no automatic time-bound expiry of the previous webhook secret; eviction is admin-driven (or rotation-driven via subsequent issuance), not clock-driven. This is the substrate-canonical definition of "grace window closes" — it composes with ADR 0065's append-only Standing-Order log without requiring substrate-side TTL tracking.

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
    services.AddSingleton<IValidationStatusStore, DefaultValidationStatusStore>();
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
- `IFieldDecryptor` (from `foundation-recovery` — registered by `AddSunfishRecovery()`)
- `IDecryptCapabilityProvider` (from `foundation-recovery` — provides short-lived capabilities for validation; registered by `AddSunfishRecovery()`)
- `IValidationStatusStore` (from this package — registered by `AddSunfishIntegrationAtlas()` itself)
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

Note: sensitive credentials passed as `ReadOnlyMemory<byte>` (already decrypted bytes) per §5.3.2; non-sensitive as `JsonNode` (raw Standing Order values).

Adapter packages register one validator per provider:

```csharp
services.AddSingleton<IIntegrationProviderValidator, StripeIntegrationValidator>();
```

**Validators are decoupled from runtime-gateway contracts.** `IPaymentGateway` (ADR 0051) has no `ValidateAsync` method; neither does `IMessagingGateway` (ADR 0052). Each `IIntegrationProviderValidator` implementation owns its own health-probe logic, independent of the runtime-egress contract. Examples:
- `StripeIntegrationValidator` issues a Stripe `/v1/account` API call using the provider credentials and maps the HTTP status to `ProviderValidationStatus`.
- `PostmarkIntegrationValidator` issues a Postmark `GET /server` API call (singular; fetches the current server's settings using the Server-Token) to verify the server token is valid. Note: `/servers` (plural) requires the Account-Token (`X-Postmark-Account-Token` header), which is a different credential than the Server-Token — using `/servers` to validate a Server-Token yields HTTP 401.
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

A reference implementation in `packages/ui-core/Wayfinder/Integrations/` consuming the §6.1 dependencies. Composes:

- `IStandingOrderIssuer.IssueAsync` for every Standing Order emission.
- `IAtlasProjector.ProjectAsync` for the projection feeding `IntegrationAtlasView`.
- `IAuditTrail.AppendAsync` for every audit event.
- `IFieldEncryptor.EncryptAsync` for sensitive credential issuance.

Tests cover: provider listing, provider activation, credential issuance (sensitive + non-sensitive), validation result issuance via `IValidationStatusStore`, validator-owned probe dispatch (with the `IIntegrationProviderValidator` discovery + missing-validator path), capability sourcing (user-driven session capability, background system-principal capability, fail-closed `Unknown + no-decrypt-capability` path), routing issuance, rotation non-destruction, and a parity test against an `InMemoryIntegrationAtlasProvider` (§7.2) that exercises the full happy path without external dependencies. License-posture-related tests are deferred to ADR 0067-A1 along with the deferred surface.

### §7.2 — `InMemoryIntegrationAtlasProvider`

A simpler in-memory variant for unit tests in consumer packages (blocks-leases, blocks-public-listings) that need to inject a working Atlas without spinning up the full Wayfinder substrate. Composes the in-memory `InMemoryAuditTrail` (per `packages/kernel-audit/InMemoryAuditTrail.cs`) and an in-memory Standing Order ledger.

### §7.3 — Anchor + Bridge component families

**Anchor (Blazor):** `accelerators/anchor/` adds a `Settings/Integrations/` page hosting:
- `<AtlasIntegrationConfig>` — the root component; one tab per `IntegrationCategory`.
- `<AtlasIntegrationCategoryPanel>` — one per category; renders the active-provider dropdown + credential form + validation status.
- `<AtlasCredentialField>` — renders a single `CredentialFieldSpec`; handles masking/reveal toggle + autocomplete attribute.
- (License-acknowledgement modal deferred to ADR 0067-A1 — see §9.7.)
- `<AtlasEmailRoutingPanel>` — special-case routing UI for email category.

**Bridge (React/TSX):** `accelerators/bridge/` adds a parallel React component family with identical naming:
- `<AtlasIntegrationConfig />`
- `<AtlasIntegrationCategoryPanel />`
- `<AtlasCredentialField />`
- (License-acknowledgement modal deferred to ADR 0067-A1 — see §9.7.)
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

/// <summary>A provider validation run reported failure (Invalid / Unreachable / Unknown).</summary>
public static readonly AuditEventType IntegrationValidationFailed = new("IntegrationValidationFailed");

// IntegrationLicenseAcknowledged audit event deferred to ADR 0067-A1; see §9.7.
```

Audit-record payloads (the `AuditRecord.Payload` JSON) carry per-event detail:

| Event | Payload fields |
|---|---|
| `IntegrationProviderChanged` | `category`, `previousProvider`, `newProvider`, `tenantId` |
| `IntegrationCredentialUpdated` | `category`, `provider`, `credentialKey` (NEVER value), `tenantId` |
| `IntegrationValidationSucceeded` | `category`, `provider`, `validatedAt`, `tenantId` |
| `IntegrationValidationFailed` | `category`, `provider`, `validatedAt`, `errorCode`, `errorMessage`, `tenantId` |

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

### §9.5 — License-acknowledgement principal-revocation behavior (moved to ADR 0067-A1)

The question of whether a revoked-principal license acknowledgement still satisfies the activation invariant moves to ADR 0067-A1 along with the rest of the license-acknowledgement track. Composes with the W#37 Tenant Security Policy ADR.

### §9.6 — OAuth-flow provider support (out of v1 scope)

The §6.3 custom-renderer escape hatch explicitly names "OAuth redirect dance" as the canonical example of a credential-capture workflow that cannot fit `CredentialFieldSpec`. v1 ships without any OAuth-flow providers. The first OAuth provider (e.g. a future `providers-google-workspace`, `providers-quickbooks`) requires its own ADR addressing: (a) callback URL whitelisting and per-tenant callback uniqueness; (b) CSRF-resistant `state` token generation and cross-tenant collision prevention to prevent state-token collision across tenants; (c) PKCE challenge/verifier flow; (d) `aria-live` announcements for the popup/redirect lifecycle (per WCAG SC 4.1.3). Adapter authors MUST NOT add an OAuth-backed `IIntegrationSchemaProvider` to v1 without a companion ADR that addresses these requirements — doing so would leave CSRF and cross-tenant `state` collision undefined at the surface level. **Disposition:** OAuth-flow providers are explicitly OUT of v1 scope; the first OAuth provider triggers a follow-up ADR.

### §9.7 — License-acknowledgement deferred to ADR 0067-A1

Per the council fix-pass, the license-acknowledgement track (`LicensePostureKind`, `LicenseAcknowledgementRequiredException`, `IssueLicenseAcknowledgementAsync`, `IntegrationLicenseAcknowledged` audit event, `license-acknowledged.{provider}` Standing-Order path, §5.5 modal flow) is CUT from v1 because ADR 0061's actual posture excludes SSPL/BSL adapters at compile time via `BannedSymbols.txt` analyzer enforcement — there is no admin-acknowledgement opt-in path on origin/main and ADR 0067 cannot invent one without an ADR amendment to ADR 0061.

A follow-up amendment (**ADR 0067-A1**) is queued at `icm/00_intake/output/2026-05-05_adr-0067-a1-license-acknowledgement-intake.md` to address the question if and when general counsel approves an SSPL/BSL admin-opt-in track. Until ADR 0067-A1 lands:
- ADR 0061's `BannedSymbols.txt` enforcement remains the canonical exclusion mechanism.
- v1 ships with `IntegrationCategory.MeshVpn` populated only by adapters whose license posture is permissive (`providers-mesh-headscale` per ADR 0061's existing analyst review; `providers-mesh-tailscale` and `providers-mesh-netbird` per their respective non-copyleft licenses).
- The Atlas UI does not render any license-acknowledgement affordance.
- Phase 1 / Phase 2 deliverables omit `LicensePostureKind`, `LicenseAcknowledgementRequiredException`, license-acknowledgement audit event, and the §5.5 modal flow.
- W#37 Tenant Security Policy considerations (revoked-principal acknowledgement) move to ADR 0067-A1.

**Disposition:** Deferred — explicitly out of v1 scope. ADR 0067-A1 intake stub filed.

### §9.8 — Encrypted-field key-rotation re-encryption sweep

Sensitive credentials are stored as `EncryptedField` (per ADR 0046-A2) which carries a `KeyVersion`. When a tenant's KEK rotates from v1 → v2, existing credential Standing Orders retain `KeyVersion = 1` envelopes. ADR 0067 v1 picks: **maintain v1 + v2 decryptors simultaneously; sweep is async background, deferred.** The reference `IFieldDecryptor` (`TenantKeyProviderFieldDecryptor` on origin/main) already supports per-version DEK resolution — the validator continues to function across the rotation without an immediate re-encrypt.

**Open:** A future amendment may introduce `IIntegrationCredentialReencryptSweep` (an async background job that rewrites prior credentials at the new key version) once tenants accumulate enough cross-version credentials to motivate compaction. v1 ships without it.

**Disposition:** Posture chosen (multi-version decryptor); sweep deferred. No blocking issue for v1.

### §9.9 — Webhook secret rotation grace-window authority (resolved per §5.7)

The grace window for accepting BOTH old and new webhook secrets ends when the prior credential's `EncryptedField` is excluded from a future Atlas projection (admin-driven via §5.7's "Clear unused credentials" action, or rotation-driven via subsequent issuance at the same `(provider, credentialKey)` path). There is no clock-driven expiry.

**Disposition:** Resolved per §5.7 disposition. No further open question.

---

## §10 — Implementation checklist

### Phase 1 — Contract surface

**Scope:** additive to the existing `packages/ui-core/` package introduced by ADR 0066, under `packages/ui-core/Wayfinder/Integrations/` with namespace `Sunfish.UICore.Wayfinder.Integrations`; contracts only.

Deliverables:
- `IIntegrationAtlasProvider` (§3.5)
- `IntegrationProviderSchema`, `CredentialFieldSpec`, `CredentialAutocompleteHint`, `CredentialFieldKind` (§3.1, §3.2)
- `IntegrationCategory`, `IntegrationCategoryMapping` (§3.4)
- `IntegrationAtlasView`, `ActiveProviderSnapshot` (§3.6)
- `IIntegrationSchemaProvider` (§3.7)
- `IntegrationValidationResult`, `ProviderValidationStatus` (§3.8, §3.9)
- `IIntegrationAtlasContext` (§3.11)
- `IntegrationEmailRouting` (§3.12)
- `IValidationStatusStore`, `ProviderValidationStatusEntry` (§3.13)
- `IDecryptCapabilityProvider` (§5.3.1; new symbol introduced by this ADR)
- `IntegrationCapabilityPurposes` static class with `IntegrationValidation` const (§3.14)
- `IIntegrationProviderValidator` (§6.2)
- `ICustomIntegrationRenderer` (§6.3)
- `AddSunfishIntegrationAtlas()` registration extension (§6.1)
- `ContractSurfaceTests.NoMethodReturnsDecryptedBytes` (reflection over `IIntegrationAtlasProvider`)

Tests not required for a pure-interface phase; XML docs required on every public type.

### Phase 2 — Reference implementation + audit

**Scope:** `DefaultIntegrationAtlasProvider`, `InMemoryIntegrationAtlasProvider`, the validation-status store, and audit constants.

Deliverables:
- `DefaultIntegrationAtlasProvider` in `packages/ui-core/Wayfinder/Integrations/` (§7.1)
- `InMemoryIntegrationAtlasProvider` in same subtree (§7.2)
- `DefaultValidationStatusStore` + `InMemoryValidationStatusStore` in same subtree (§3.13)
- 4 new `AuditEventType` constants in `packages/kernel-audit/AuditEventType.cs` (§8) — `IntegrationProviderChanged`, `IntegrationCredentialUpdated`, `IntegrationValidationSucceeded`, `IntegrationValidationFailed`
- Audit-record payload factory helpers (one per event type) in same subtree — `IntegrationAuditPayloads.cs`
- `SUNFISH_INTEGRATION_AUDIT001` Roslyn analyzer in `packages/foundation-wayfinder-analyzers`
- Unit tests covering all §5 flows (excluding deferred license-acknowledgement)
- Audit-redaction corpus test (per §8 redaction rule) — `IntegrationAuditRedactionTests`
- `DefaultIntegrationAtlasProviderTests.SensitiveCredential_IsEncryptedBeforeStandingOrder`
- `ProviderRotationTests.RotationAudit_DoesNotContainPriorCredentials`
- `ValidationCapabilityFailClosedTests` (per §5.3.1 negative case — three failure modes)
- `ValidatorIsolationTests` (asserts the surface does NOT call `IPaymentGateway` / `IMessagingGateway` / `IMeshVpnAdapter` `ValidateAsync` — none exist)
- `IFieldDecryptor` scope-isolation unit test (per §6.1.1)

### Phase 3a — Provider package availability gate

**Scope:** verify the first-wave adapter packages exist on origin/main before Phase 3b runs. No Phase 3b adapter work begins until each named provider package has at minimum a stub `ProviderDescriptor` registration on origin/main.

Deliverables:
- Verify on origin/main: `providers-mesh-headscale` (exists), `providers-recaptcha` (exists).
- Track via existing workstreams: `providers-stripe` + `providers-square` (W#5 commercial); `providers-postmark` + `providers-sendgrid` + `providers-mailchimp` (W#22 leasing-pipeline + Phase 2 commercial); `providers-twilio` (W#5 / W#22); `providers-mesh-tailscale` + `providers-mesh-netbird` (W#30 mesh-VPN); `providers-hcaptcha` (W#28 public listings).
- For each not-yet-existing package, the Phase 3b deliverable is gated on the named workstream landing first; the ADR 0067 hand-off does NOT author the provider packages themselves.

### Phase 3b — Schema-provider + validator additions

**Prerequisite gate:** Phase 3a verified. Each adapter package adds its `IIntegrationSchemaProvider` registration and `IIntegrationProviderValidator` implementation; ADR 0067 work-stream is responsible for the Atlas-side surface contracts only.

**Scope:** schema + validator registration in each existing or newly-landed first-wave adapter package.

Deliverables (each conditional on the underlying provider package landing per Phase 3a):
- `providers-stripe`: `StripeSchemaProvider` + `StripeIntegrationValidator` (§4.1, §6.2)
- `providers-postmark`: `PostmarkSchemaProvider` + `PostmarkIntegrationValidator`
- `providers-twilio`: `TwilioSchemaProvider` + `TwilioIntegrationValidator`
- `providers-mesh-headscale` / `providers-mesh-tailscale`: `*SchemaProvider` + `*IntegrationValidator` (validator owns its own control-plane probe; does NOT call `IMeshVpnAdapter`)
- `providers-recaptcha`: `RecaptchaSchemaProvider` + `RecaptchaIntegrationValidator`
- Per-adapter parity tests asserting schema shape matches the actual credential consumption code, plaintext lifetime constraints (§5.3.2), marker-credential leak tests, and `ValidatorIsolationTests` per-provider variant

### Phase 4 — Anchor + Bridge rendering

**Scope:** Anchor Blazor component family + Bridge React component family per §7.3.

Deliverables:
- Anchor: `accelerators/anchor/Pages/Settings/Integrations/` + `<AtlasIntegrationConfig>` family
- Bridge: `accelerators/bridge/src/admin/integrations/` + `<AtlasIntegrationConfig />` family
- Parity tests asserting structural DOM equivalence
- Component-level a11y tests against WCAG 2.2 AA criteria: 1.3.1 (info and relationships — masking state), 1.4.1 (use of color — status icons), 1.4.11 (non-text contrast — status icons 3:1), 2.4.6 (headings and labels — descriptive `DisplayLabel`), 2.5.3 (label in name — adjacent-control accessible names), 3.3.2 (labels/instructions — HelpText rendering), 3.3.7 (redundant entry — sensitive "leave unchanged" path), 3.3.8 (accessible authentication — `CredentialAutocompleteHint` enum values), 4.1.2 (name, role, value — show/hide toggle `aria-pressed`), 4.1.3 (status messages — including validation in-flight `aria-busy` and post-rotation `Unknown` announcement). Tests MUST also cover the WAI-ARIA APG Tabs keyboard contract (§7.3.1). (Modal SCs 3.3.4 / 2.1.2 / 2.4.3 deferred to ADR 0067-A1.)
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
| `IPaymentGateway` | `packages/foundation-integrations/Payments/IPaymentGateway.cs` | `AuthorizeAsync`, `CaptureAsync`, `RefundAsync` only — NO `ValidateAsync` | §5.3 + §6.2: validator owns its own probe (does not call gateway); see §A0.6 |
| `IMessagingGateway` | `packages/foundation-integrations/Messaging/IMessagingGateway.cs` | `SendAsync`, `GetStatusAsync` only — NO `ValidateAsync` | §5.3 + §6.2: same disposition as `IPaymentGateway` |
| `IMeshVpnAdapter` | `packages/foundation-transport/IMeshVpnAdapter.cs` | `AdapterName`, `GetMeshStatusAsync`, `RegisterDeviceAsync` — NO `ValidateAsync` | §5.3 + §6.2: same disposition; mesh validator owns its own control-plane probe |
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

### §A0.4 — `IMeshVpnAdapter` is on origin/main (corrected)

`IMeshVpnAdapter` IS present on `origin/main` at `packages/foundation-transport/IMeshVpnAdapter.cs`. A prior draft of this §A0 incorrectly classified it as uncommitted; that error was caught by the council fix-pass and is corrected here. The actual interface surface on origin/main is:

```csharp
public interface IMeshVpnAdapter : IPeerTransport
{
    string AdapterName { get; }
    Task<MeshNodeStatus> GetMeshStatusAsync(CancellationToken ct);
    Task RegisterDeviceAsync(MeshDeviceRegistration registration, CancellationToken ct);
}
```

**Validation dispatch resolved.** `IMeshVpnAdapter` has no `ValidateAsync` method; ADR 0067 does NOT extend it (that would be a breaking change to a published transport contract requiring its own ADR amendment to ADR 0061). Per the council fix-pass, mesh-VPN validation is owned by `IIntegrationProviderValidator` itself — each `*MeshIntegrationValidator` issues its own control-plane probe (e.g., `TailscaleIntegrationValidator` calls Tailscale's `/api/v2/tailnet/{tailnet}/keys`; `HeadscaleIntegrationValidator` calls Headscale's API equivalent). See §5.3 + §6.2.

### §A0.5 — No new package introduced

No new package is introduced by ADR 0067. Per the council fix-pass and ADR 0066 PR #529's "additive, no new package required" framing, all ADR 0067 types live under `packages/ui-core/Wayfinder/Integrations/` (additive to the existing `packages/ui-core/` package). The `Sunfish.UICore.Wayfinder.Integrations` namespace is net-new but the package boundary is preserved. If ADR 0066's namespace shape changes during council review, ADR 0067 Phase 1 inherits the rename mechanically; the package-shape decision is locked in for both ADRs.

### §A0.6 — Origin/main drift corrections applied to this ADR body

| Drift item | Finding | ADR body resolution |
|---|---|---|
| `IStandingOrderIssuer.IssueAsync` signature | Returns `Task<StandingOrder>`, takes `ActorId`, requires `IAuditTrail`. Earlier draft had `ValueTask<StandingOrderId>` + `PrincipalId`. | §3.5 methods now return `Task<StandingOrder>` and source `ActorId` from `IIntegrationAtlasContext` (§3.11); the wrapper composes `IStandingOrderIssuer.IssueAsync(draft, actorId, auditTrail, ct)` per the §3.5 issuance composition rationale. `PrincipalId` swept to `ActorId` throughout (§3.6 `ActiveProviderSnapshot.ActivatedBy`). |
| `IPaymentGateway` has no `ValidateAsync` | Method does not exist on origin/main | §5.3 + §6.2 rewritten per council fix-pass: validators own their own liveness probes; ADR does NOT extend `IPaymentGateway`. |
| `IMessagingGateway` has no `ValidateAsync` | Method does not exist on origin/main | §5.3 + §6.2 same fix as above. |
| `IMeshVpnAdapter` has no `ValidateAsync` | Method does not exist on origin/main; the symbol IS on main (earlier §A0.4 false-uncommitted claim corrected). | §5.3 + §6.2 same fix; mesh validators own their own control-plane probe. |
| `IFieldEncryptor.EncryptAsync` takes `ReadOnlyMemory<byte>` | Draft §5.2 implied `JsonNode` wrapping | §5.2 updated: `IssueSensitiveCredentialAsync` takes `ReadOnlyMemory<byte>`. |
| `IFieldDecryptor.DecryptAsync` requires `IDecryptCapability` | Earlier draft §5.3 silent on capability acquisition | §5.3.1 specifies capability sourcing for user-driven (session capability) and background (system-principal capability) paths + fail-closed negative case with `ErrorCode = "no-decrypt-capability"`. |
| `StandingOrder` is append-only | Earlier draft §2.5 `validation-status.{provider}` modeled as a Standing Order would generate ~100k orders/provider/tenant/year under §5.6.1 cadence | §3.13 introduces `IValidationStatusStore` as a separate substrate; Standing-Order journal carries configuration intent only. |
| `AtlasSettingSnapshot.LastIssuedBy` is `StandingOrderId` | Earlier draft §3.6 implied it was a principal | §3.6 prose clarified; rendering host resolves issuer display name via `IStandingOrderRepository.GetAsync(standingOrderId)`. |
| ADR 0061 does NOT have an admin-acknowledgement opt-in path | Earlier draft invented a `LicensePostureKind` / acknowledgement-modal flow contradicting ADR 0061's `BannedSymbols.txt` enforcement | License-acknowledgement track CUT entirely; deferred to ADR 0067-A1 follow-up amendment (intake stub at `icm/00_intake/output/2026-05-05_adr-0067-a1-license-acknowledgement-intake.md`). |
| ADR 0066 declares `Sunfish.UICore.Wayfinder` additive to existing `packages/ui-core/` | Earlier draft declared a net-new `packages/ui-core-wayfinder/` package | §3 / §"Decision" / §10 Phase 1 updated: surface placed under `packages/ui-core/Wayfinder/Integrations/` namespace `Sunfish.UICore.Wayfinder.Integrations`; no new package. |

### §A0.7 — Net-new symbols introduced by this ADR (no prior origin/main artifact)

| Symbol | Where declared | Purpose |
|---|---|---|
| `IIntegrationAtlasProvider` | §3.5 | Headline contract; specializes `IAtlasProvider<IntegrationAtlasView>` |
| `IIntegrationSchemaProvider` | §3.7 | Adapter-side schema registration |
| `IIntegrationProviderValidator` | §6.2 | Per-provider liveness probe (validator owns dispatch) |
| `IIntegrationAtlasContext` | §3.11 | Tenant + actor identity boundary |
| `IValidationStatusStore` | §3.13 | Transient validation-status store (not a Standing Order) |
| `IDecryptCapabilityProvider` | §3.14 | Issues short-lived `IDecryptCapability` for validation runs |
| `IntegrationProviderSchema` | §3.1 | Adapter-declared credential schema |
| `CredentialFieldSpec`, `CredentialAutocompleteHint`, `CredentialFieldKind` | §3.2 | Schema field shape |
| `IntegrationCategory`, `IntegrationCategoryMapping` | §3.4 | Atlas-fine taxonomy + mapping to `ProviderCategory` |
| `IntegrationAtlasView`, `ActiveProviderSnapshot` | §3.6 | Projection consumed by Atlas component |
| `IntegrationValidationResult`, `ProviderValidationStatus`, `ProviderValidationStatusEntry` | §3.8, §3.9, §3.13 | Validation outcome shapes |
| `IntegrationEmailRouting` | §3.12 | Multi-transport email routing |
| `ICustomIntegrationRenderer` | §6.3 | Schema-extension escape hatch |
| `AddSunfishIntegrationAtlas()` | §6.1 | DI registration extension |
| 4 new `AuditEventType` constants | §8 | `IntegrationProviderChanged`, `IntegrationCredentialUpdated`, `IntegrationValidationSucceeded`, `IntegrationValidationFailed` |

All symbols above are introduced by this ADR; council fix-pass verified none collide with prior origin/main symbols.

---

## §A1 — Council review checklist (apply before status flip)

Council verifies, in addition to the standard adversarial review:

- **WCAG/a11y subagent:**
  - SC 3.3.7 (Redundant Entry): no credential is asked twice in the same session; sensitive credentials with prior values render in "leave unchanged" mode per §5.2.1.
  - SC 3.3.8 (Accessible Authentication): every sensitive `CredentialFieldSpec` has an `AutocompleteHint`; show/hide toggle has an accessible name; `CredentialAutocompleteHint` enum constrains autocomplete tokens to the WHATWG-valid set; no tenant-level credential uses `CurrentPassword`.
  - SC 1.3.1 (Info and Relationships): masking state is conveyed structurally, not just visually.
  - SC 4.1.3 (Status Messages): validation outcomes use `aria-live="polite"` for success, `role="alert"` for errors; validation in-flight emits `aria-busy` + interim live-region message per §5.3.3; post-rotation `Unknown` is announced (transition); first-render `Unknown` is not.
  - SC 1.4.1 (Use of Color): no validation state relies on color alone.
  - SC 1.4.11 (Non-text Contrast): status icons (§5.6) MUST meet 3:1 contrast against the surface background; renderer concern, but the contract commits to it. Phase 4 visual-regression tests assert the 3:1 ratio.
  - SC 2.4.6 (Headings and Labels): `CredentialFieldSpec.DisplayLabel` SHOULD be descriptive — bare-noun labels like "Key" fail the SC. First-wave schema authors are responsible; Phase 3 sanity-check tests verify minimum descriptiveness.
  - SC 2.5.3 (Label in Name): adjacent-control accessible names include `DisplayLabel` verbatim per §3.2.
  - SC 3.3.2 (Labels or Instructions): `HelpText` rendered as persistent `aria-describedby` text per §3.2.
  - SC 4.1.2 (Name, Role, Value): show/hide toggle exposes `aria-pressed` + `aria-controls` per §3.2.1.
  - WAI-ARIA APG Tabs: category navigation arrow-key contract per §7.3.1.
  - (License-acknowledgement modal SCs — 3.3.4, 2.1.2, 2.4.3 — moved to ADR 0067-A1 along with the deferred surface.)
- **Security-engineering subagent:**
  - Sensitive credentials traverse `IFieldEncryptor` before any persistence path → asserted by `DefaultIntegrationAtlasProviderTests.SensitiveCredential_IsEncryptedBeforeStandingOrder` (Phase 2).
  - Audit payloads cannot leak credential values → asserted by marker-corpus test in `IntegrationAuditRedactionTests` (Phase 2) + `SUNFISH_INTEGRATION_AUDIT001` analyzer (Phase 2).
  - Provider rotation does not leak old credentials into the new provider's audit payload → asserted by `ProviderRotationTests.RotationAudit_DoesNotContainPriorCredentials` (Phase 2).
  - Capability sourcing is fail-closed: missing/expired/wrong-tenant `IDecryptCapability` produces `ProviderValidationStatus.Unknown` + `ErrorCode = "no-decrypt-capability"`, NOT a silent skip — asserted by `ValidationCapabilityFailClosedTests` (Phase 2) covering all three failure modes.
  - The contract surface admits no method returning decrypted credentials to a rendering host → asserted by `ContractSurfaceTests.NoMethodReturnsDecryptedBytes` (reflection over `IIntegrationAtlasProvider`, Phase 1).
- **Pedantic-Lawyer perspective:** (license-acknowledgement track moved to ADR 0067-A1; this perspective re-engages on that follow-up amendment).
- **Skeptical Implementer:**
  - Dynamic schema rendering is testable end-to-end without a running provider.
  - A new provider can be added without touching `foundation-integrations` or the rendering hosts.
  - The custom-renderer escape hatch (§6.3) is genuinely a safety valve, not a default.
  - Validation flow does NOT call into transport contracts (`IPaymentGateway` / `IMessagingGateway` / `IMeshVpnAdapter`) — asserted by `ValidatorIsolationTests` reflection check (Phase 2).

---

## Amendments

(none — ADR is in `Proposed` status; amendments tracked in frontmatter on acceptance.)
