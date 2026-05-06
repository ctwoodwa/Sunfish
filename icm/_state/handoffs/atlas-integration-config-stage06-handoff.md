# W#48 Stage 06 Hand-off — Atlas Integration-Config UI Surface (ADR 0067)

**Workstream:** W#48  
**ADR:** [0067 — Atlas Integration-Config UI Surface](../../../docs/adrs/0067-atlas-integration-config-surface.md)  
**Status:** `ready-to-build` (ADR 0067 Accepted 2026-05-05 via PR #539)  
**Owner:** sunfish-PM  
**Pipeline variant:** `sunfish-feature-change`

---

## Prerequisites — verify before starting ANY phase

| Prerequisite | How to verify | State at hand-off authoring |
|---|---|---|
| ADR 0065 W#42 built | `grep -rn "IStandingOrderIssuer" packages/foundation-wayfinder/` ≥1 match | ✓ Built (PRs #503–#514 merged) |
| ADR 0066 Stage 06 Phase 1 (W#53) landed | `grep -rn "IAtlasProvider" packages/ui-core/` ≥1 match | ✗ **NOT YET BUILT** — W#53 queued |
| `IDecryptCapabilityProvider` in `foundation/Crypto/` | `grep -rn "IDecryptCapabilityProvider" packages/foundation/Crypto/` ≥1 match | Verify at build time (see cycle note below) |
| `IFieldEncryptor` / `IFieldDecryptor` via `AddSunfishRecovery()` | `grep -rn "AddSunfishRecovery" packages/foundation-recovery/` ≥1 match | Verify at build time |

**Phase 1 is BLOCKED until ADR 0066 Stage 06 Phase 1 (W#53) lands** — `IAtlasProvider<T>` (which `IIntegrationAtlasProvider` extends) is introduced by ADR 0066's Stage 06 build, not by ADR 0066's document merge. Per ADR 0067 §A0.3 mitigation: "Phase 1 of the §10 implementation checklist MUST land *after* ADR 0066's Phase 1."

---

## Package scope

**No new package.** All deliverables go into the existing `packages/ui-core/` package, under the new
sub-path `packages/ui-core/Wayfinder/Integrations/`. Namespace: `Sunfish.UICore.Wayfinder.Integrations`.

Per ADR 0067 §A0.5 (council-verified): "No new package is introduced by ADR 0067. All ADR 0067 types
live under `packages/ui-core/Wayfinder/Integrations/` (additive to the existing `packages/ui-core/`
package)."

---

## Estimate

~23–35h sunfish-PM / 5 build phases / ~6–8 PRs

| Phase | Scope | Estimate | PRs |
|---|---|---|---|
| 1 | Contract surface | ~3–5h | 1 |
| 2 | Reference impl + audit | ~6–9h | 1–2 |
| 3a | Provider availability gate | ~1h | 1 (verification) |
| 3b | Schema + validator additions | ~4–8h | 2–3 |
| 4 | Anchor + Bridge rendering | ~8–12h | 2 |
| 5 | Ledger flip + apps/docs | ~1–3h | 1 |

---

## Phase 1 — Contract surface

**Gate:** ADR 0066 Stage 06 Phase 1 (W#53) on origin/main. Halt condition H1 below.

**Scope:** `packages/ui-core/Wayfinder/Integrations/` — interfaces and value types only.

### Deliverables

All files in `packages/ui-core/Wayfinder/Integrations/` unless noted. XML docs required on every public type.

**Interfaces:**

`IIntegrationAtlasProvider.cs`
```csharp
// namespace Sunfish.UICore.Wayfinder.Integrations
public interface IIntegrationAtlasProvider : IAtlasProvider<IntegrationAtlasView>
{
    IReadOnlyList<IntegrationProviderSchema> GetSchemas();
    Task<IntegrationAtlasView> GetAtlasViewAsync(CancellationToken ct = default);
    Task<StandingOrder> IssueProviderChangeAsync(
        IntegrationCategory category, string providerId,
        IIntegrationAtlasContext ctx, CancellationToken ct = default);
    Task<StandingOrder> IssueSensitiveCredentialAsync(
        IntegrationCategory category, string providerId, string credentialKey,
        ReadOnlyMemory<byte> plaintextBytes,
        IIntegrationAtlasContext ctx, CancellationToken ct = default);
    Task<StandingOrder> IssueNonSensitiveCredentialAsync(
        IntegrationCategory category, string providerId, string credentialKey,
        JsonNode value,
        IIntegrationAtlasContext ctx, CancellationToken ct = default);
    Task<IntegrationValidationResult> ValidateProviderAsync(
        IntegrationCategory category,
        IIntegrationAtlasContext ctx, CancellationToken ct = default);
    Task<StandingOrder> IssueRoutingAsync(
        IntegrationEmailRouting routing,
        IIntegrationAtlasContext ctx, CancellationToken ct = default);
}
```

`IIntegrationAtlasContext.cs`
```csharp
public interface IIntegrationAtlasContext
{
    TenantId CurrentTenantId { get; }
    ActorId CurrentActorId { get; }
}
```

`IIntegrationSchemaProvider.cs`
```csharp
public interface IIntegrationSchemaProvider
{
    IReadOnlyList<IntegrationProviderSchema> GetSchemas();
}
```

`IIntegrationProviderValidator.cs` (§6.2)
```csharp
// Mark [EditorBrowsable(EditorBrowsableState.Never)] — only DefaultIntegrationAtlasProvider consumes.
// Adapter impls must be internal sealed.
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

`IValidationStatusStore.cs` (§3.13 — NOT a Standing Order; transient state)
```csharp
public interface IValidationStatusStore
{
    Task<ProviderValidationStatusEntry?> GetCurrentAsync(
        TenantId tenantId, IntegrationCategory category, string providerId,
        CancellationToken ct = default);
    Task UpdateAsync(
        TenantId tenantId, IntegrationCategory category, string providerId,
        IntegrationValidationResult result, ActorId actor,
        CancellationToken ct = default);
    IAsyncEnumerable<ProviderValidationStatusEntry> HistoryAsync(
        TenantId tenantId, IntegrationCategory category, string providerId,
        int maxEntries = 20, CancellationToken ct = default);
}
```

`IDecryptCapabilityProvider.cs` (§3.14 / §5.3.1 — new symbol)

**Cycle note:** This interface MUST go in `packages/foundation/Crypto/` (alongside
`IDecryptCapability`), NOT in `foundation-recovery`. `ui-core` references `foundation`
but CANNOT reference `foundation-recovery` — a `foundation-recovery` → `kernel-security`
→ `ui-core` cycle already exists (same reason `KeyFingerprint` moved to
`packages/foundation/Crypto/` during W#53 P1b, per the comment in
`packages/ui-core/Sunfish.UICore.csproj`).

The IMPLEMENTATION (`TenantKeyDecryptCapabilityProvider` or equivalent) lives in
`foundation-recovery` and is registered via `AddSunfishRecovery()` (COB must add the
registration to `packages/foundation-recovery/DependencyInjection/` as a **companion
step in Phase 1b** — not a separate workstream).

```csharp
// File: packages/foundation/Crypto/IDecryptCapabilityProvider.cs
// Namespace: Sunfish.Foundation.Crypto (alongside IDecryptCapability)
public interface IDecryptCapabilityProvider
{
    Task<IDecryptCapability?> AcquireAsync(
        TenantId tenantId, string purpose, TimeSpan ttl,
        CancellationToken ct = default);
}
```

`ICustomIntegrationRenderer.cs` (§6.3 — safety-valve escape hatch; no v1 registrations)
```csharp
public interface ICustomIntegrationRenderer
{
    string SupportedProvider { get; }
    Type RendererType { get; }       // Razor component type for Anchor
    string ReactComponentSpec { get; } // React component module path for Bridge
}
```

**Schemas and value types:**

`IntegrationProviderSchema.cs` (§3.1)
- Properties: `string ProviderId`, `string DisplayName`, `IntegrationCategory Category`,
  `IReadOnlyList<CredentialFieldSpec> CredentialFields`, `string? HelpText`,
  `string? DocumentationUrl`

`CredentialFieldSpec.cs` (§3.2)
- Properties: `string Key`, `string DisplayLabel`, `CredentialFieldKind Kind`,
  `CredentialAutocompleteHint AutocompleteHint`, `bool IsRequired`,
  `string? HelpText`, `string? Placeholder`

`CredentialAutocompleteHint.cs` (§3.2) — WHATWG-constrained enum values only:
- `None`, `CurrentPassword`, `NewPassword`, `OneTimeCode`, `Username`, `Email`, `Url`

`CredentialFieldKind.cs` (§3.2)
- `Text`, `Secret`, `Url`, `ReadOnlyOutput`

`IntegrationCategory.cs` (§3.4) — 6 values:
- `Payments`, `TransactionalEmail`, `MarketingEmail`, `Messaging`, `MeshVpn`, `Captcha`

`IntegrationCategoryMapping.cs` (§3.4) — static class:
- `ToProviderCategory(IntegrationCategory)` — maps to `ProviderCategory` in `foundation-integrations`
- `FromProviderCategory(ProviderCategory)` — inverse; throws on unmapped values

`IntegrationAtlasView.cs` (§3.6)
- Properties: `IReadOnlyDictionary<IntegrationCategory, ActiveProviderSnapshot?> ActiveByCategory`,
  `IReadOnlyDictionary<IntegrationCategory, ProviderValidationStatus> StatusByCategory`,
  `IReadOnlyDictionary<IntegrationCategory, IReadOnlyList<ProviderValidationStatusEntry>> CredentialsByProvider`,
  `IntegrationEmailRouting? EmailRouting`

`ActiveProviderSnapshot.cs` (§3.6)
- Properties: `string ProviderId`, `Instant ActivatedAt`, `ActorId ActivatedBy`,
  `StandingOrderId ActivationOrderId`

`IntegrationValidationResult.cs` (§3.8)
- Properties: `ProviderValidationStatus Status`, `Instant ValidatedAt`,
  `string? ErrorCode`, `string? ErrorMessage`

`ProviderValidationStatus.cs` (§3.9) — 4 values:
- `Unknown`, `Valid`, `Invalid`, `Unreachable`

`ProviderValidationStatusEntry.cs` (§3.13)
- Properties: `TenantId TenantId`, `IntegrationCategory Category`, `string ProviderId`,
  `IntegrationValidationResult Result`, `ActorId RecordedBy`, `Instant RecordedAt`

`IntegrationEmailRouting.cs` (§3.12)
- Properties: `string? TransactionalProvider`, `string? MarketingProvider`

**Constants:**

`IntegrationCapabilityPurposes.cs` (§3.14 — must be a Phase 1 deliverable per council finding NM-6)
```csharp
public static class IntegrationCapabilityPurposes
{
    public const string IntegrationValidation = "integration-validation";
}
```

**DI extension:**

`ServiceCollectionExtensions.cs` (§6.1)
**Cycle note for the guard check:** `IFieldEncryptor` lives in `foundation-recovery`;
`ui-core` cannot reference `foundation-recovery` (cycle). Guard against
`IDecryptCapabilityProvider` instead — it is in `foundation/Crypto/` and is the
direct dependency consumed by Phase 2's `DefaultIntegrationAtlasProvider`.

```csharp
public static IServiceCollection AddSunfishIntegrationAtlas(
    this IServiceCollection services)
{
    if (!services.Any(d => d.ServiceType == typeof(IDecryptCapabilityProvider)))
        throw new InvalidOperationException(
            "AddSunfishRecovery() must be called before AddSunfishIntegrationAtlas(). " +
            "IDecryptCapabilityProvider is required by DefaultIntegrationAtlasProvider.");
    services.AddSingleton<IIntegrationAtlasProvider, DefaultIntegrationAtlasProvider>();
    services.AddSingleton<IValidationStatusStore, DefaultValidationStatusStore>();
    return services;
}
```
Note: `IIntegrationAtlasContext` is registered by the host (Bridge: scoped via HttpContext;
Anchor: singleton via local-node identity) — NOT registered here.

**Tests (Phase 1):**

`tests/ContractSurfaceTests.cs`:
- `NoMethodReturnsDecryptedBytes` — reflection over `IIntegrationAtlasProvider`: confirm no
  method returns `byte[]`, `ReadOnlyMemory<byte>`, `Memory<byte>`, `string` where name
  contains "decrypt" or "credential" (ensures the contract surface admits no raw credential
  return path to rendering host)

### Halt conditions (Phase 1)

- **(H1) ADR 0066 W#53 Phase 1 on origin/main.** Verify: `grep -rn "IAtlasProvider" packages/ui-core/ | grep -v ".Designer."` ≥1 match.
  If zero → HALT; post `cob-question-adr0066-phase1-needed.md` to `icm/_state/research-inbox/`.
- **(H2) Pre-merge council canonical.** Phase 1 PR MUST NOT enable auto-merge until council verdict.
  Dispatch standard 4-perspective + WCAG/a11y subagent (contract-level a11y review:
  autocomplete hint enum, masked credential field spec shape, accessible name conventions).
- **(H3) Sub-path isolation.** Types go in `packages/ui-core/Wayfinder/Integrations/`
  (NOT in `packages/ui-core/Wayfinder/` — that namespace belongs to ADR 0066 W#53).
- **(H4) `ContractSurfaceTests.NoMethodReturnsDecryptedBytes` must pass** before PR is marked ready.

---

## Phase 2 — Reference implementation + audit constants

**Gate:** Phase 1 landed.

> **⚠ CYCLE NOTE — XO RULING AVAILABLE — READ ADDENDUM BEFORE STARTING PHASE 2 ⚠**
>
> `DefaultIntegrationAtlasProvider` depends on `IFieldEncryptor` + `IFieldDecryptor`
> (both in `foundation-recovery`). `ui-core` CANNOT reference `foundation-recovery`
> (cycle: `foundation-recovery → kernel-security → ui-core`). This means
> `DefaultIntegrationAtlasProvider` CANNOT live in `packages/ui-core/` as originally
> specified.
>
> **XO has ruled (Option A):** Phase 2 implementations go in a new
> **`packages/blocks-integrations/`** package. The full architectural decision,
> corrected `csproj`, split DI extension, corrected file locations, and additional
> halt conditions (H9–H11) are documented in:
>
> **`icm/_state/handoffs/atlas-integration-config-p2-blocks-integrations-addendum.md`**
>
> **Read the addendum in full before writing any Phase 2 code.** No cob-question needed.

**Scope:** `DefaultIntegrationAtlasProvider`, `InMemoryIntegrationAtlasProvider`, stores, audit constants,
typed payload factories, SUNFISH_INTEGRATION_AUDIT001 analyzer.

### Deliverables

**Implementations:**

`DefaultIntegrationAtlasProvider.cs` (§7.1) — consumes all §6.1 dependencies:
- `IStandingOrderIssuer` (from `foundation-wayfinder`)
- `IAtlasProjector` (from `foundation-wayfinder`)
- `IAuditTrail` (from `kernel-audit`)
- `IFieldEncryptor` (from `foundation-recovery`)
- `IFieldDecryptor` (from `foundation-recovery` — scope-isolated per §6.1.1)
- `IDecryptCapabilityProvider` (from `foundation/Crypto/` — cycle-safe; impl in `foundation-recovery`)
- `IValidationStatusStore` (from this package)
- `IIntegrationAtlasContext` (from host)
- `IEnumerable<IIntegrationSchemaProvider>` (adapter packages)
- `IEnumerable<IIntegrationProviderValidator>` (adapter packages)

Validator resolution rules (§6.2.1):
- Lookup: `(SupportedCategory, SupportedProvider)` exact match.
- Duplicate: `AddSunfishIntegrationAtlas()` throws `DuplicateValidatorRegistrationException`.
- Missing: return `ProviderValidationStatus.Unknown` with `ErrorCode = "no-validator-registered"`; no exception.

`IssueSensitiveCredentialAsync` pre-condition: call `IFieldEncryptor.EncryptAsync` BEFORE
`IStandingOrderIssuer.IssueAsync` — enforced by unit test (H5 below).

`ValidateProviderAsync` capability flow (§5.3.1):
1. User-driven: acquire from `IDecryptCapabilityProvider.AcquireAsync(tenantId, "integration-validation", ttl)`.
2. Background: acquire using system-principal capability (host provides via DI injection or
   `ISystemPrincipalProvider` analogue for background jobs).
3. Fail-closed: if `IDecryptCapability?` is null → `ProviderValidationStatus.Unknown` +
   `ErrorCode = "no-decrypt-capability"`. No exception.
4. After validation: `CryptographicOperations.ZeroMemory` on plaintext buffers in `finally`.
   NEVER log credential bytes or retain references after `ValidateAsync` returns.

`InMemoryIntegrationAtlasProvider.cs` (§7.2) — in-memory variant for consumer package tests;
composes `InMemoryAuditTrail` + in-memory Standing Order ledger; no real encrypt/decrypt.

`DefaultValidationStatusStore.cs` — in-memory backing for dev; separate implementation details.

`InMemoryValidationStatusStore.cs` — test-friendly; used by `InMemoryIntegrationAtlasProvider`.

**Audit constants** — add to `packages/kernel-audit/AuditEventType.cs` (§8):
```csharp
// ===== ADR 0067 — Atlas integration-config UI surface =====
public static readonly AuditEventType IntegrationProviderChanged =
    new("IntegrationProviderChanged");
public static readonly AuditEventType IntegrationCredentialUpdated =
    new("IntegrationCredentialUpdated");
public static readonly AuditEventType IntegrationValidationSucceeded =
    new("IntegrationValidationSucceeded");
public static readonly AuditEventType IntegrationValidationFailed =
    new("IntegrationValidationFailed");
```

**Typed audit payload factories** — `IntegrationAuditPayloads.cs` in `packages/ui-core/Wayfinder/Integrations/`:
- `CreateProviderChangedPayload(category, previousProvider, newProvider, tenantId)` → `AuditRecord`
- `CreateCredentialUpdatedPayload(category, provider, credentialKey, tenantId)` → `AuditRecord`
  (NEVER include credential value)
- `CreateValidationSucceededPayload(category, provider, validatedAt, tenantId)` → `AuditRecord`
- `CreateValidationFailedPayload(category, provider, validatedAt, errorCode, errorMessage, tenantId)` → `AuditRecord`

Forbidden field names in any audit payload (case-insensitive, recursive key scan):
`value`, `apiKey`, `secret`, `password`, `token`, `webhookSecret`, any key starting with
`credential.` or ending with `.value`.

**SUNFISH_INTEGRATION_AUDIT001 Roslyn analyzer** — add to `packages/foundation-wayfinder-analyzers/`:
- Diagnostic ID: `SUNFISH_INTEGRATION_AUDIT001`
- Severity: Error
- Rule: `AuditRecord` construction for ADR 0067 event types MUST use typed factory methods from
  `IntegrationAuditPayloads`. Free-form `JsonNode` / `Dictionary<string, object>` construction
  for these event types is forbidden.

### Tests

All tests in a new `packages/ui-core/tests/Wayfinder/Integrations/` sub-folder:

- `DefaultIntegrationAtlasProviderTests.cs`:
  - Provider listing (from `IEnumerable<IIntegrationSchemaProvider>`)
  - Provider activation (`IssueProviderChangeAsync` → Standing Order issued)
  - Sensitive credential issuance (`IssueSensitiveCredentialAsync` → `IFieldEncryptor` called)
  - Non-sensitive credential issuance (`IssueNonSensitiveCredentialAsync` → JSON node issued)
  - `ValidateProviderAsync` happy path (capability acquired, validator called, result stored)
  - Missing validator path (returns `Unknown` + `no-validator-registered`, no exception)
  - Routing issuance (`IssueRoutingAsync` → two Standing Orders)
  - Rotation non-destruction (prior provider credentials remain in Standing Order log)
  - Parity test against `InMemoryIntegrationAtlasProvider` full happy path

- `DefaultIntegrationAtlasProviderTests.SensitiveCredential_IsEncryptedBeforeStandingOrder`:
  Assert `IFieldEncryptor.EncryptAsync` is called BEFORE `IStandingOrderIssuer.IssueAsync`
  for any `IssueSensitiveCredentialAsync` invocation.

- `ProviderRotationTests.RotationAudit_DoesNotContainPriorCredentials`:
  After rotation, verify no `AuditRecord` payload contains prior provider credential bytes.

- `ValidationCapabilityFailClosedTests.cs` (3 failure modes per §5.3.1):
  1. `IDecryptCapabilityProvider.AcquireAsync` returns null → result is `Unknown` + `no-decrypt-capability`
  2. `IDecryptCapabilityProvider.AcquireAsync` returns expired capability → same result
  3. `IDecryptCapabilityProvider.AcquireAsync` returns wrong-tenant capability → same result

- `ValidatorIsolationTests.cs` (reflection):
  Verify `DefaultIntegrationAtlasProvider.ValidateProviderAsync` does NOT resolve
  `IPaymentGateway`, `IMessagingGateway`, or `IMeshVpnAdapter` from `IServiceProvider`
  during validation execution.

- `IFieldDecryptorScopeIsolationTests.cs` (§6.1.1):
  Assert `IFieldDecryptor` CANNOT be resolved from a Blazor-scoped `IServiceProvider` built
  via `AddSunfishIntegrationAtlas()` alone.

- `IntegrationAuditRedactionTests.cs` (§8 redaction rule):
  For a marker credential injected into every §5 flow:
  - Every emitted `AuditRecord.Payload` JSON MUST NOT contain the marker string.
  - Verify forbidden field names cannot appear as JSON keys in audit payloads even when
    passed through `IntegrationAuditPayloads` factory methods (allowlist enforcement check).
  - Negative test: key named `previousProvider` with "secret" as its VALUE must pass
    (only key names are screened, not values).
  - Negative test: key `details.value` must fail (ends with `.value`).
  - Negative test: key `webhook-secret` normalized case-insensitively must fail.

### Halt conditions (Phase 2)

- **(H5) `IDecryptCapabilityProvider` in `foundation/Crypto/` on origin/main.**
  Interface verify: `grep -rn "IDecryptCapabilityProvider" packages/foundation/Crypto/` ≥1 match.
  Implementation verify: `grep -rn "IDecryptCapabilityProvider" packages/foundation-recovery/` ≥1 match.
  If the interface is zero → COB must CREATE `packages/foundation/Crypto/IDecryptCapabilityProvider.cs`
  as part of Phase 1b (it's a new symbol per ADR 0067 §A0.7 — not pre-existing).
  If the implementation is zero → COB must also ADD the registration to
  `packages/foundation-recovery/DependencyInjection/` as part of Phase 1b companion step.
  Both the interface and the `AddSunfishRecovery()` registration are Phase 1b deliverables.
- **(H6) Pre-merge council + security-engineering subagent.** Mandatory for Phase 2
  (audit redaction, credential scope isolation, capability sourcing fail-closed semantics).
- **(H7) `SUNFISH_INTEGRATION_AUDIT001` must be severity Error**, not Warning.
  COB verifies: `dotnet build packages/foundation-wayfinder-analyzers` produces zero
  diagnostic-severity-downgrade compiler warnings.
- **(H8) `IntegrationAuditRedactionTests` corpus tests must be green** before PR closes.

---

## Phase 3a — Provider package availability gate

**Gate:** Phase 2 landed.

**Scope:** Verification pass only. No new production code. Documents which provider packages are
available on origin/main and which are blocked on named workstreams.

### Deliverables

Run on origin/main after Phase 2 merges:

```bash
# Verify providers-mesh-headscale (expected: IMeshVpnAdapter in foundation-transport per §A0.4)
grep -rn "providers-mesh-headscale\|HeadscaleAdapter\|HeadscalePeerTransport" packages/ | head -5

# Verify providers-recaptcha (expected: from W#28 blocks-public-listings)
find packages/ -name "*.cs" | xargs grep -l "recaptcha\|ReCaptcha" 2>/dev/null | head -5
```

Create a commit with message `chore(icm): W#48 Phase 3a — provider availability gate` documenting findings.

Provider status at hand-off authoring (to be re-verified at Phase 3a execution):

| Provider | Source workstream | Status at authoring |
|---|---|---|
| `providers-mesh-headscale` | `foundation-transport` (ADR 0061; built) | Likely available via IMeshVpnAdapter stub |
| `providers-recaptcha` | W#28 (blocks-public-listings) | Verify at Phase 3a |
| `providers-stripe` | W#5 commercial Phase 2 | Not yet built |
| `providers-square` | W#5 commercial Phase 2 | Not yet built |
| `providers-postmark` | W#22 leasing-pipeline Phase 2+ | Not yet built |
| `providers-sendgrid` | W#22 / Phase 2 commercial | Not yet built |
| `providers-mailchimp` | W#22 Phase 2 commercial | Not yet built |
| `providers-twilio` | W#5 / W#22 | Not yet built |
| `providers-mesh-tailscale` | W#30 mesh-VPN | Not yet built |
| `providers-mesh-netbird` | W#30 mesh-VPN | Not yet built |
| `providers-hcaptcha` | W#28 public listings | Verify at Phase 3a |

Phase 3b work proceeds only for providers with a verified origin/main package or stub.

---

## Phase 3b — Schema + validator additions

**Gate:** Phase 3a completed; for each provider, its origin/main package exists.

**Scope:** Per-provider `IIntegrationSchemaProvider` registration + `IIntegrationProviderValidator`
implementation in each existing adapter package.

### Deliverables (per available provider)

For each provider verified in Phase 3a:

1. In the provider's adapter package:
   - `<Provider>SchemaProvider : IIntegrationSchemaProvider` — returns `IntegrationProviderSchema`
     with `CredentialFieldSpec` list matching the provider's actual credential consumption code.
   - `<Provider>IntegrationValidator : IIntegrationProviderValidator` (internal sealed) —
     issues its own control-plane probe. Does NOT call `IPaymentGateway`, `IMessagingGateway`,
     or `IMeshVpnAdapter`. Examples per §6.2:
     - Stripe: `GET /v1/account` API call
     - Postmark: `GET /server` (NOT `/servers` — wrong token type; `GET /servers` requires
       Account-Token, but the config stores the Server-Token)
     - Tailscale: `GET /api/v2/tailnet/{tailnet}/keys`
     - Headscale: per-adapter Headscale API control-plane probe
   - `services.AddSingleton<IIntegrationProviderValidator, <Provider>IntegrationValidator>()`
     registered in the provider package's DI extension.

2. Per-provider parity + security tests:
   - Schema shape matches the actual credential consumption code (no drift)
   - Marker-credential leak test: inject marker credential, exercise validation,
     assert marker does not appear in any log/audit output
   - `ValidatorIsolationTests` variant confirming validator does not resolve runtime
     gateway contracts during validation

### WCAG/a11y for Phase 3b

For any provider whose `CredentialFieldSpec` list includes non-obvious accessibility concerns
(e.g., webhook-URL read-only output fields, multi-step credential capture), dispatch
WCAG/a11y subagent before Phase 3b PR merges for that provider.

---

## Phase 4 — Anchor + Bridge rendering

**Gate:** Phase 2 landed. W#46 Phase 3 (`ILiveAnnouncer` / `IFocusTrap`) is a soft prerequisite
for full live-region integration; Phase 4 MAY ship with native `aria-live` annotations as a
polyfill if W#46 Phase 3 is not yet built. No confirmation dialog (deferred to ADR 0067-A1),
so `IFocusTrap` is NOT hard-required for v1.

### Deliverables (Anchor Blazor)

Location: `accelerators/anchor/Pages/Settings/Integrations/`

`AtlasIntegrationConfig.razor` — root component:
- `role="tablist"` container for `IntegrationCategory` tabs
- Roving tabindex: active tab `tabindex="0"`, others `tabindex="-1"`
- Arrow-key navigation (Left/Right cyclic; Home/End); Tab moves into active panel
- One `AtlasIntegrationCategoryPanel` per category

`AtlasIntegrationCategoryPanel.razor` — per-category panel:
- `role="tabpanel"` with `aria-labelledby` pointing to tab
- Active-provider dropdown with `aria-label="Active provider for {category}"`
- `<AtlasCredentialField>` for each `CredentialFieldSpec`
- Validate button with `aria-label="Validate {providerDisplayName}"`; becomes `aria-disabled="true"` + relabeled "Validating…" during in-flight (NOT native `disabled`)
- `aria-busy="true"` on the panel during `ValidateProviderAsync` execution
- Status indicator per §5.6 (shape-distinct icon + text + live region):
  - `Valid` → green check + "Connected" in `aria-live="polite"` region
  - `Invalid` / `Unreachable` → `role="alert"` (announced immediately)
  - `Unknown` first-render → empty text node (NOT announced)
  - `Unknown` post-rotation / post-credential-clear → `aria-live="polite"` announcement
- Status live region: one `aria-atomic="true"` element per panel; only transitions replace content

`AtlasCredentialField.razor` — per-field component:
- `<label>` linked via `aria-labelledby` or `for`/`id` pair; includes `DisplayLabel` verbatim
- `HelpText` rendered as persistent `aria-describedby` text (not tooltip)
- For `CredentialFieldKind.Secret`: show/hide toggle with `aria-pressed` + `aria-controls`
- `autocomplete="{CredentialAutocompleteHint}"` attribute (WHATWG-valid values only)
- "Leave unchanged" placeholder for sensitive fields with existing values (SC 3.3.7):
  field renders `[Unchanged]` placeholder; does not re-expose stored credential
- `IsSensitive = true` with prior value: field type `password`; placeholder "••••••••";
  edit reveals empty input (never pre-populates with prior value)

`AtlasEmailRoutingPanel.razor` — email routing special case:
- Two dropdowns: Transactional + Marketing provider selection
- Each dropdown renders `IntegrationProviderSchema.DisplayName` as option text
- `IssueRoutingAsync` on confirm; two-Standing-Order commit per §5.4

### Deliverables (Bridge React/TSX)

Location: `accelerators/bridge/src/admin/integrations/`

Parallel React component family with identical naming:
- `AtlasIntegrationConfig.tsx`
- `AtlasIntegrationCategoryPanel.tsx`
- `AtlasCredentialField.tsx`
- `EmailRoutingPanel.tsx`

**ARIA implementation notes (React-idiomatic):** use `role`, `aria-*`, and `tabIndex` props
with the same semantics as the Blazor family; React state drives `aria-busy`, `aria-disabled`,
and live-region content replacement.

### Tests (Phase 4)

**WCAG 2.2 AA component-level tests:**

| SC | What to assert |
|---|---|
| SC 3.3.7 (Redundant Entry) | No credential is requested twice in the same session; prior-value fields render "leave unchanged" mode |
| SC 3.3.8 (Accessible Authentication) | Every sensitive `CredentialFieldSpec` has a non-None `AutocompleteHint`; value maps to WHATWG-valid `autocomplete` attribute |
| SC 1.4.1 (Use of Color) | All 4 validation states are distinguishable without color (shape-distinct icons) |
| SC 1.4.11 (Non-text Contrast) | Status icons ≥ 3:1 contrast against surface background |
| SC 4.1.2 (Name, Role, Value) | Show/hide toggle has `aria-pressed` + `aria-controls`; validate button has accessible name |
| SC 4.1.3 (Status Messages) | `aria-busy` during validation in-flight; polite live-region on success; `role="alert"` on Invalid/Unreachable; post-rotation `Unknown` announced; first-render `Unknown` silent |
| SC 2.4.6 (Headings and Labels) | `CredentialFieldSpec.DisplayLabel` is descriptive (test: label is not a bare noun like "Key") |
| SC 2.5.3 (Label in Name) | Button + input accessible names include `DisplayLabel` verbatim |
| SC 3.3.2 (Labels or Instructions) | `HelpText` rendered as `aria-describedby` persistent text |
| WAI-ARIA APG Tabs | Arrow-key navigation; roving tabindex; Home/End; Tab into active panel |

**Parity tests:**
- Structural DOM equivalence between Blazor and React output for a representative Atlas state
- Snapshot-rendering tests with a representative tenant Atlas state (2+ categories, 1 valid + 1 invalid)

### Halt conditions (Phase 4)

- **(H9) Pre-merge council + WCAG/a11y subagent mandatory** (Phase 4 is UI-bearing).
  WCAG/a11y subagent must explicitly verify all SC entries in the table above. Any subagent
  finding rated Critical or Major auto-halts merge until applied.
- **(H10) Bridge React component parity tests must pass** before Phase 4 PR closes.
- **(H11) Snapshot-rendering tests green** before Phase 4 PR closes.
- **(H12) License-acknowledgement modal NOT in scope.** Do not implement §5.5 modal,
  `LicensePostureKind`, or `IntegrationLicenseAcknowledged` audit event. These are deferred
  to ADR 0067-A1. If a v1 provider schema has `LicensePostureKind` in its schema, halt and
  post a `cob-question-*.md`.

---

## Phase 5 — Ledger flip + apps/docs

**Gate:** Phases 1–4 landed.

### Deliverables

- `apps/docs/blocks/integration-config.md` — surface documentation for adapter authors + tenant admins.
  Sections: Overview, Provider Schema authoring guide, Credential field kinds + autocomplete hints,
  Validation validator impl guide (probe isolation requirement), WCAG contract summary.
- `apps/kitchen-sink` demonstration scene: `Settings/Integrations/` page wiring `AtlasIntegrationConfig`
  against `InMemoryIntegrationAtlasProvider` with two seeded provider schemas
  (one valid Payments + one unvalidated Messaging).
- `_shared/engineering/coding-standards.md`: add cross-link from the "Configuration UX" section
  pointing to `apps/docs/blocks/integration-config.md`.
- Flip W#48 ledger row to `built (ADR 0067 Accepted; Phases 1–4 shipped)`.

---

## Pre-merge council posture (all phases)

Per ADR 0067 §A1 council checklist:

- **Standard adversarial review** (4 perspectives minimum) for ALL phases.
- **WCAG/a11y subagent** mandatory for Phase 1 (contract autocomplete enum), Phase 2 (audit
  payload shape), Phase 4 (full UI). See §A1 checklist for per-SC verification items.
- **Security-engineering subagent** mandatory for Phase 2 (credential encryption, audit
  redaction, capability sourcing fail-closed, IFieldDecryptor scope isolation).
- **SUNFISH_INTEGRATION_AUDIT001** analyzer must be green before any Phase 2 PR merges.
- **Cohort batting average:** 30-of-31 substrate amendments needed council fixes as of W#48
  filing — pre-merge council is non-negotiable.

---

## Open items (carry forward from ADR 0067 §9)

- **`ProviderCategory` / `IntegrationCategory` unification** (§9.1): deferred post-v1; track as
  ADR 0067-A1 candidate.
- **Per-node vs per-tenant mesh-VPN config for Anchor** (§9.2): deferred; flag at Phase 3b if
  `providers-mesh-tailscale` / `providers-mesh-netbird` land.
- **Bring-your-own-vault credential storage** (§9.3): deferred; v1 ships `EncryptedField` only.
- **Webhook URL provisioning** (§9.4): deferred; v1 shows HelpText directing to docs.
- **License-acknowledgement track** (§9.7): explicitly out of v1 scope; deferred to ADR 0067-A1
  (intake stub at `icm/00_intake/output/2026-05-05_adr-0067-a1-license-acknowledgement-intake.md`).
- **Encrypted-field key-rotation re-encryption sweep** (§9.8): deferred; multi-version decryptor
  posture already in `TenantKeyProviderFieldDecryptor`; no action needed in W#48.

---

## Authoring notes

- ADR 0067 §A0.6 corrects several origin/main drift items applied during council:
  `IFieldEncryptor.EncryptAsync` takes `ReadOnlyMemory<byte>` (NOT `JsonNode`);
  `IFieldDecryptor.DecryptAsync` requires `IDecryptCapability` parameter;
  `IStandingOrderIssuer.IssueAsync` returns `Task<StandingOrder>` and takes `ActorId` + `IAuditTrail`.
  COB must use these canonical signatures; the ADR body is the ground truth.
- §A0.3 soft-prerequisite: if ADR 0066's contract surface drifts from its ADR spec during W#53 build,
  W#48 Phase 1 hand-off must be re-validated against the post-build origin/main shape;
  a regenerated §A0 captures the resolution.
- `IAtlasProvider<T>` base interface is a forward-reference to ADR 0066 Stage 06. If COB cannot
  locate it at Phase 1 build time, HALT and post a `cob-question-*.md`.
