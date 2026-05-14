---
uid: block-integrations-overview
title: Integration Config — Overview
description: Blazor + React UI surface and provider implementations for the Atlas Integration-Config surface (ADR 0067) — InMemoryIntegrationAtlasProvider, DefaultIntegrationAtlasProvider, AtlasIntegrationConfig, and SUNFISH_INTEGRATION_AUDIT001 analyzer.
keywords:
  - sunfish
  - blocks
  - integrations
  - atlas
  - integration-config
  - adr-0067
  - wcag
---

# Integration Config — Overview

## What this block is

`Sunfish.Blocks.Integrations` is the **provider + audit layer** for the Atlas Integration-Config
UI surface (ADR 0067). It bridges `ui-core` integration contracts with a pluggable provider
architecture that lets tenants configure payments, email, messaging, mesh VPN, and CAPTCHA
providers through a WCAG 2.2 AA–compliant UI surface.

The block ships:

- **`DefaultIntegrationAtlasProvider`** — full `IIntegrationAtlasProvider` implementation:
  encrypt-before-issue ordering, fail-closed capability acquisition, sensitive-credential
  zero-on-done, and decrypt-failure fail-closed per ADR 0067 §7.1.
- **`InMemoryIntegrationAtlasProvider`** — test double for consumer package tests; no encryption,
  no Standing Orders, no audit emission. Ships with `SetActiveProvider` / `SetValidationStatus`
  / `SetRouting` helpers for fixture seeding.
- **`IntegrationAuditPayloads`** — typed audit payload factory per ADR 0067 §8. The
  `SUNFISH_INTEGRATION_AUDIT001` Roslyn analyzer (Error severity) enforces that no code inside
  integration-config namespaces constructs `AuditPayload` directly — all audit emission must
  flow through these factories.
- **`AddSunfishIntegrationAtlasDefaults()`** — DI extension that registers
  `DefaultIntegrationAtlasProvider` as `IIntegrationAtlasProvider`.

The UI components ship in the accelerator projects:

- **Anchor Blazor** — `AtlasIntegrationConfig`, `AtlasIntegrationCategoryPanel`,
  `AtlasCredentialField`, `AtlasEmailRoutingPanel` in
  `accelerators/anchor/Components/Pages/Settings/Integrations/`
- **Bridge Blazor** — `AtlasIntegrationConfig`, `AtlasIntegrationCategoryPanel`,
  `AtlasCredentialField` in `accelerators/bridge/Sunfish.Bridge.Client/Components/Settings/Integrations/`
  (`AtlasEmailRoutingPanel` Bridge parity is a W#48 follow-on)
- **React** — `AtlasIntegrationConfig`, `AtlasIntegrationCategoryPanel`, `AtlasCredentialField`
  in `packages/ui-adapters-react/src/components/Integrations/`

## Package

- Package: `Sunfish.Blocks.Integrations`
- Source: `packages/blocks-integrations/`
- Namespace root: `Sunfish.Blocks.Integrations`

## Dependencies

| Package | Role |
|---|---|
| `Sunfish.UICore` | `IIntegrationAtlasProvider`, `IIntegrationAtlasContext`, `IntegrationAtlasView`, all contract types |
| `Sunfish.Foundation` | `IStandingOrderIssuer`, `IAuditTrail`, `IFieldEncryptor`, `IDecryptCapabilityProvider` |
| `Sunfish.Foundation.Assets.Common` | `TenantId`, `ActorId`, `StandingOrderId` |

The block-tier placement (not `ui-core`) is required so `DefaultIntegrationAtlasProvider` can
reference `foundation-recovery` for `IFieldEncryptor` without forming the
`ui-core → foundation-recovery → kernel-crdt → ui-core` cycle.

## Provider Schema authoring guide

Implement `IIntegrationSchemaProvider` and register it with DI. The Atlas surface aggregates
all registered providers at start-up via `IEnumerable<IIntegrationSchemaProvider>`.

```csharp
public sealed class StripeSchemaProvider : IIntegrationSchemaProvider
{
    public IEnumerable<IntegrationProviderSchema> GetSchemas()
    {
        yield return new IntegrationProviderSchema(
            ProviderId: "stripe",
            DisplayName: "Stripe",
            Category: IntegrationCategory.Payments,
            CredentialFields: [
                new CredentialFieldSpec(
                    Key: "publishable-key",
                    DisplayLabel: "Publishable key",
                    Kind: CredentialFieldKind.Text,
                    AutocompleteHint: CredentialAutocompleteHint.None,
                    IsRequired: true,
                    HelpText: "Starts with pk_live_ or pk_test_",
                    Placeholder: null),
                new CredentialFieldSpec(
                    Key: "secret-key",
                    DisplayLabel: "Secret key",
                    Kind: CredentialFieldKind.Secret,
                    AutocompleteHint: CredentialAutocompleteHint.CurrentPassword,
                    IsRequired: true,
                    HelpText: "Starts with sk_live_ or sk_test_",
                    Placeholder: null),
            ],
            HelpText: "Configure your Stripe account credentials.",
            DocumentationUrl: "https://stripe.com/docs/keys");
    }
}
```

Register in your DI composition root (accelerator Program.cs or host):

```csharp
services.AddSingleton<IIntegrationSchemaProvider, StripeSchemaProvider>();
```

The `InMemoryIntegrationAtlasProvider` factory pattern used by Anchor and Bridge:

```csharp
services.TryAddSingleton<IIntegrationAtlasProvider>(sp =>
{
    var schemas = sp.GetServices<IIntegrationSchemaProvider>().SelectMany(p => p.GetSchemas());
    return new InMemoryIntegrationAtlasProvider(schemas);
});
```

## Credential field kinds and autocomplete hints

| `CredentialFieldKind` | Input rendering | Notes |
|---|---|---|
| `Text` | `<input type="text">` | Plain-text fields; API keys, client IDs, tenant slugs |
| `Secret` | `<input type="password">` | Masked; Show/Hide toggle (SC 4.1.2 `aria-pressed`+`aria-controls`); existing-value "leave unchanged" placeholder (SC 3.3.7) |
| `Url` | `<input type="url">` | URL-format validation on the client |
| `ReadOnlyOutput` | `<output>` or read-only display | Provider-assigned values the user cannot edit |

| `CredentialAutocompleteHint` | HTML `autocomplete` value | When to use |
|---|---|---|
| `None` | `off` | Non-credential identifiers (webhook endpoint slug, sender domain) |
| `CurrentPassword` | `current-password` | Existing secret credentials |
| `NewPassword` | `new-password` | First-time or rotation entry for a secret |
| `OneTimeCode` | `one-time-code` | 2FA tokens, TOTP seeds |
| `Username` | `username` | Provider-account username / login |
| `Email` | `email` | Sender email addresses |
| `Organization` | `organization` | Tenant / account display name fields |

**SC 3.3.8 (WCAG 2.2):** every `Secret` field MUST carry an autocomplete hint. The renderer
enforces this by mapping `CredentialAutocompleteHint.None` to `autocomplete="off"` rather
than omitting the attribute — an absent `autocomplete` attribute on credential inputs fails
password-manager heuristics.

## Validation validator implementation guide

Implement `IIntegrationProviderValidator` and register it with the DI container:

```csharp
public sealed class StripeValidator : IIntegrationProviderValidator
{
    public string ProviderId => "stripe";
    public IntegrationCategory Category => IntegrationCategory.Payments;

    public async Task<IntegrationValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, string> nonSensitiveCredentials,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> sensitiveCredentials,
        CancellationToken ct = default)
    {
        // PROBE ISOLATION REQUIREMENT (ADR 0067 §6.2): validators MUST only call
        // read-only probe endpoints. No write-side API calls; no state mutation.
        // Stripe: POST /v1/tokens?card[number]=... is a read probe (no charge).
        // The validator MUST NOT log, store, or leak the sensitive credential bytes.

        if (!sensitiveCredentials.TryGetValue("secret-key", out var keyBytes))
            return IntegrationValidationResult.Invalid("secret-key-missing");

        // ... probe call using keyBytes.Span ...
        // return IntegrationValidationResult.Valid() or .Unreachable("reason");
    }
}
```

Register in DI:

```csharp
services.AddSingleton<IIntegrationProviderValidator, StripeValidator>();
```

**Probe isolation requirement**: validators MUST call read-only probe endpoints only. Write-side
API calls are forbidden — a validator that creates real resources is a security violation. The
Headscale and reCAPTCHA v3 validators shipped with Sunfish (in `providers-mesh-headscale` and
`providers-recaptcha`) demonstrate compliant probe patterns.

## WCAG contract summary

The Atlas Integration-Config surface (ADR 0067 §A1) is bound by WCAG 2.2 AA + EN 301 549
v3.2.1. The following SCs have explicit implementation requirements:

| SC | Surface | Pattern |
|---|---|---|
| 1.4.1 (use of color) | Category status badge | Shape-distinct icons: ✓ Connected · ✕ Invalid · ⚠ Unreachable · ○ Unknown |
| 3.3.2 (labels or instructions) | All credential fields | `aria-describedby` for help text; sr-only `(required)` text on required fields |
| 3.3.7 (redundant entry) | Secret fields with existing credentials | "Leave unchanged" placeholder; actual credential bytes never pre-populated |
| 3.3.8 (accessible authentication) | Secret fields | `autocomplete` MUST be present (see autocomplete hints table) |
| 4.1.2 (name, role, value) | Show/Hide toggle | `aria-pressed` + `aria-controls` on the toggle button; `aria-disabled` on validate button during validation (keeps button focusable) |
| 4.1.3 (status messages) | Validation status live region | `aria-live="polite"` for Valid/Unknown; `role="alert"` + `aria-atomic="true"` for Invalid/Unreachable; `aria-busy` during in-flight validation |

The tab-strip (AtlasIntegrationConfig) implements WAI-ARIA APG Tabs with roving tabindex,
`FocusAsync()` on ArrowLeft/Right/Home/End, and `hidden` on inactive tabpanels.

## ADR references

- **ADR 0067** — Atlas Integration-Config UI Surface (primary)
- **ADR 0077** — Shared Design System (live region + focus-trap contracts)
- **ADR 0028-A2** — Managed-relay mesh VPN (Headscale integration)
