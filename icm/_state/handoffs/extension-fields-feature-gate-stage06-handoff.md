# W#44 — ExtensionFields Feature-Evaluation Hook (ADR 0075)
## Stage 06 Hand-off: sunfish-PM implementation spec

**ADR:** `docs/adrs/0075-extensionfields-feature-evaluation-hook.md` — Status: Accepted
**Workstream:** W#44 (ADR 0009 follow-up #5)
**Pipeline variant:** `sunfish-api-change`
**Estimated effort:** ~11-15h / 4 PRs
**Pre-merge council:** mandatory per ADR 0069 D1 — dispatch BEFORE enabling auto-merge

---

## Halt conditions (stop and write a COB question if any apply)

1. `ExtensionFieldSpec.cs` has been modified since this hand-off was authored — re-read and verify the positional record shape still matches before adding parameters.
2. `AuditEventType.cs` has gained event types prefixed `ExtensionField*` — de-duplicate rather than creating conflicts.
3. Any existing `IExtensionFieldCatalog` implementation other than `ExtensionFieldCatalog` is found in `packages/foundation-catalog/` — the new async overload must be added to all implementations or the interface change will break them.
4. `packages/kernel-audit/AuditRecord.cs` constructor signature differs from the 7-field positional record cited in ADR 0075 §A0.2 — stop and flag if it has changed.

---

## Background

ADR 0005 establishes `IExtensionFieldCatalog` as the single registry for extension fields on canonical entities. ADR 0009 establishes `IFeatureEvaluator` as the feature evaluation surface. ADR 0075 wires them together: extension fields may carry an optional `FeatureKey?` gate; when a caller uses the new async overload, the catalog evaluates each gated field and returns `MaterializedExtensionField` records indicating whether each field is visible (Ungated/GatedOn), hidden (Hidden), sequestered (Sequestered), or redacted (Redacted).

The existing synchronous `GetFields(Type)` is preserved unchanged — backward-compatible seam.

**Dependencies the COB must verify exist on origin/main before starting:**
- `packages/foundation-catalog/ExtensionFields/ExtensionFieldSpec.cs` — current 8-parameter positional sealed record
- `packages/foundation-featuremanagement/IFeatureEvaluator.cs` + `FeatureKey.cs` + `FeatureEvaluationContext.cs`
- `packages/kernel-audit/IAuditTrail.cs` + `AuditEventType.cs` + `AuditRecord.cs`
- `packages/foundation-migration/Services/ISequestrationStore.cs` + `Models/Enums.cs` (must have `SequestrationFlagKind.FeatureGateOff`)
- `packages/foundation/Capabilities/ICapabilityGraph.cs` + `CapabilityAction.cs` + `Resource.cs`
- `packages/foundation/Crypto/IOperationSigner.cs` + `PrincipalId.cs`
- `packages/foundation-recovery/Crypto/FieldDecryptionDeniedException.cs` (shape precedent)

---

## Phase 1 — Substrate types + audit constants + csproj (~2-3h)

**Files to create / modify:**

### `packages/foundation-catalog/ExtensionFields/FeatureGateOffPolicy.cs` (NEW)
```csharp
namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Policy applied when an <see cref="ExtensionFieldSpec.FeatureKey"/> evaluates
/// to OFF. Default is <see cref="Hide"/>; <see cref="Sequester"/> composes
/// the W#35 / ADR 0028-A5.4 sequestration partition; <see cref="Redact"/> is
/// destructive and requires explicit operator capability per ADR 0046.
/// </summary>
public enum FeatureGateOffPolicy
{
    /// <summary>Default. Field is excluded from materialization; underlying data is preserved in
    /// storage but not surfaced. Reversible: when the gate flips ON, the field reappears.</summary>
    Hide = 0,

    /// <summary>Field is excluded from materialization AND its underlying data is registered with
    /// <c>Sunfish.Foundation.Migration.ISequestrationStore</c> (W#35). Composes ADR 0028-A5.4.
    /// Reversible.</summary>
    Sequester = 1,

    /// <summary>Field is excluded from materialization AND its underlying data is destroyed
    /// (tombstoned). Requires explicit operator capability and emits a destructive audit record.
    /// NOT reversible.</summary>
    Redact = 2,
}
```

### `packages/foundation-catalog/ExtensionFields/GateState.cs` (NEW)
```csharp
namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>Outcome of feature-gate evaluation for one <see cref="ExtensionFieldSpec"/>.</summary>
public enum GateState
{
    /// <summary>Spec has no FeatureKey; field is unconditionally visible.</summary>
    Ungated = 0,

    /// <summary>Spec has a FeatureKey and the gate evaluated ON.</summary>
    GatedOn = 1,

    /// <summary>Spec is gated OFF and policy is <see cref="FeatureGateOffPolicy.Hide"/>; field is
    /// excluded from materialization but data is preserved. Currently never returned by
    /// <see cref="IExtensionFieldCatalog.GetFieldsAsync"/>; see ADR 0075 §Open questions item 5.</summary>
    Hidden = 2,

    /// <summary>Spec is gated OFF and policy is <see cref="FeatureGateOffPolicy.Sequester"/>; field is
    /// excluded but tracked in ISequestrationStore. Currently never returned by
    /// <see cref="IExtensionFieldCatalog.GetFieldsAsync"/>; see ADR 0075 §Open questions item 5.</summary>
    Sequestered = 3,

    /// <summary>Spec is gated OFF and policy is <see cref="FeatureGateOffPolicy.Redact"/>; underlying
    /// data has been tombstoned. Currently never returned by
    /// <see cref="IExtensionFieldCatalog.GetFieldsAsync"/>; see ADR 0075 §Open questions item 5.</summary>
    Redacted = 4,
}
```

### `packages/foundation-catalog/ExtensionFields/MaterializedExtensionField.cs` (NEW)
```csharp
namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Result of <see cref="IExtensionFieldCatalog.GetFieldsAsync"/>: a registered spec
/// plus the gate-evaluation outcome. Consumers switch on <see cref="GateState"/> to
/// decide whether to surface the field (Ungated or GatedOn) or skip it.
/// </summary>
public sealed record MaterializedExtensionField(
    ExtensionFieldSpec Spec,
    GateState GateState);
```

### `packages/foundation-catalog/ExtensionFields/ExtensionFieldSpec.cs` (MODIFY — append 2 parameters)

Append two optional positional parameters to the existing 8-parameter record:
```csharp
// Existing 8 params stay unchanged. ADD at the end:
Sunfish.Foundation.FeatureManagement.FeatureKey? FeatureKey = null,
FeatureGateOffPolicy FeatureGateOffPolicy = FeatureGateOffPolicy.Hide);
```

The full record becomes a 10-parameter positional sealed record. Existing call sites using fewer than 10 positional args or named args are unaffected.

### `packages/foundation-catalog/ExtensionFields/ExtensionFieldRedactionDeniedException.cs` (NEW)
```csharp
namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Thrown when GetFieldsAsync evaluates a spec whose FeatureGateOffPolicy is Redact to OFF,
/// but ICapabilityGraph denies the redact-extension-field action. Shape parallel to
/// Sunfish.Foundation.Recovery.Crypto.FieldDecryptionDeniedException per ADR 0075 §A0.2.
/// </summary>
public sealed class ExtensionFieldRedactionDeniedException : Exception
{
    public ExtensionFieldRedactionDeniedException(
        string action, string entityTypeFullName, string fieldKey, string reason)
        : base($"Extension-field redaction denied (action='{action}', entity='{entityTypeFullName}', field='{fieldKey}'): {reason}")
    {
        Action = action;
        EntityTypeFullName = entityTypeFullName;
        FieldKey = fieldKey;
        Reason = reason;
    }

    public string Action { get; }
    public string EntityTypeFullName { get; }
    public string FieldKey { get; }
    public string Reason { get; }
}
```

### `packages/kernel-audit/AuditEventType.cs` (MODIFY — append 5 constants)

Add a new section at the END of the file (after the last existing section), following the exact `// ===== ADR XXXX =====` section-header pattern:

```csharp
// ===== ADR 0075 — extension-field feature-gate hook =====

/// <summary>Spec has a FeatureKey; gate evaluated ON; field appears in the materialized list.</summary>
public static readonly AuditEventType ExtensionFieldGated = new("ExtensionFieldGated");

/// <summary>Spec is gated OFF; policy is Hide; field excluded from materialized list.</summary>
public static readonly AuditEventType ExtensionFieldFiltered = new("ExtensionFieldFiltered");

/// <summary>Spec is gated OFF; policy is Sequester; field excluded AND ISequestrationStore.SequesterAsync called.</summary>
public static readonly AuditEventType ExtensionFieldSequestered = new("ExtensionFieldSequestered");

/// <summary>Spec is gated OFF; policy is Redact; field excluded AND underlying data tombstoned.</summary>
public static readonly AuditEventType ExtensionFieldRedacted = new("ExtensionFieldRedacted");

/// <summary>IFeatureEvaluator.IsEnabledAsync threw; gate treated as OFF (fail-closed); exception captured in payload.</summary>
public static readonly AuditEventType ExtensionFieldGateEvaluationFailed = new("ExtensionFieldGateEvaluationFailed");
```

### `packages/foundation-catalog/Sunfish.Foundation.Catalog.csproj` (MODIFY — add 3 ProjectReferences)

Add inside the existing `<ItemGroup>` with `<ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />` or in a new ItemGroup:

```xml
<ProjectReference Include="..\foundation-featuremanagement\Sunfish.Foundation.FeatureManagement.csproj" />
<ProjectReference Include="..\kernel-audit\Sunfish.Kernel.Audit.csproj" />
<ProjectReference Include="..\foundation-migration\Sunfish.Foundation.Migration.csproj" />
```

**Acceptance criteria P1:**
- `dotnet build packages/foundation-catalog` succeeds
- `dotnet build packages/kernel-audit` succeeds
- No breaking changes to existing call sites (new record params are optional with defaults)

---

## Phase 2 — Catalog wiring + audit factory + DI extension (~4-5h)

### `packages/foundation-catalog/ExtensionFields/Audit/ExtensionFieldGateAuditPayloads.cs` (NEW)

Create the audit-payload factory parallel to `packages/foundation-migration/Audit/MigrationAuditPayloads.cs`. Read that file first for the exact pattern.

The factory must produce `SignedOperation<AuditPayload>` envelopes using an injected `IOperationSigner`. Five methods: one per `AuditEventType` constant added in Phase 1. Each method signature:

```csharp
public static SignedOperation<AuditPayload> CreateXxx(
    IOperationSigner signer,
    TenantId tenantId,
    Type entityType,
    ExtensionFieldSpec spec,
    // ... event-specific args)
```

Read `packages/foundation-migration/Audit/MigrationAuditPayloads.cs` for the exact construction pattern of `AuditPayload` + `SignedOperation<AuditPayload>`.

### `packages/foundation-catalog/ExtensionFields/IExtensionFieldCatalog.cs` (MODIFY — add async overload)

Add to the interface (do NOT modify existing members):

```csharp
using Sunfish.Foundation.FeatureManagement;

// Add to IExtensionFieldCatalog:

/// <summary>
/// Returns the materialized field set in the given context. Each spec with a non-null
/// <c>FeatureKey</c> is evaluated against <c>IFeatureEvaluator</c>; gated-OFF specs
/// are filtered / sequestered / redacted per <c>FeatureGateOffPolicy</c>; every decision
/// emits an audit record. Null-evaluator host ⇒ gating is skipped (all specs returned
/// as <c>GateState.Ungated</c>).
/// </summary>
ValueTask<IReadOnlyList<MaterializedExtensionField>> GetFieldsAsync(
    Type entityType,
    FeatureEvaluationContext context,
    CancellationToken cancellationToken = default);
```

Also mark the existing `GetFields(Type)` with an `[Obsolete]` warning:

```csharp
[Obsolete("Use GetFieldsAsync which supports feature-gate evaluation. GetFields does not apply FeatureGateOffPolicy.")]
IReadOnlyList<ExtensionFieldSpec> GetFields(Type entityType);
```

### `packages/foundation-catalog/ExtensionFields/ExtensionFieldCatalog.cs` (MODIFY — major additions)

The concrete class gains:
1. Constructor parameter additions (all nullable — lazy DI):
   ```csharp
   using Sunfish.Foundation.FeatureManagement;
   using Sunfish.Foundation.Capabilities;
   using Sunfish.Foundation.Crypto;
   using Sunfish.Kernel.Audit;
   using Sunfish.Foundation.Migration;

   public ExtensionFieldCatalog(
       IFeatureEvaluator? featureEvaluator = null,
       IAuditTrail? auditTrail = null,
       ISequestrationStore? sequestrationStore = null,
       ICapabilityGraph? capabilityGraph = null,
       IOperationSigner? signer = null,
       INodeIdProvider? nodeIdProvider = null,
       TimeProvider? clock = null)
   ```
   Note: `INodeIdProvider` is in `packages/foundation/`. Verify exact namespace + type.

2. Private static readonly `CapabilityAction`:
   ```csharp
   private static readonly CapabilityAction RedactExtensionFieldAction = new("redact-extension-field");
   ```

3. `GetFieldsAsync` implementation per ADR 0075 §Decision logic:
   - For each spec: if `spec.FeatureKey is null` → return `GateState.Ungated`
   - If `featureEvaluator is null` → return `GateState.Ungated` (skip gating entirely)
   - Call `featureEvaluator.IsEnabledAsync(spec.FeatureKey.Value, context, ct)` in a try/catch
     - On throw: emit `ExtensionFieldGateEvaluationFailed`, treat as OFF (fail-closed)
   - If ON: emit `ExtensionFieldGated`, return `GateState.GatedOn`
   - If OFF: apply `spec.FeatureGateOffPolicy`:
     - `Hide`: emit `ExtensionFieldFiltered`, return nothing (field excluded)
     - `Sequester`: throw if `sequestrationStore is null`; call `SequesterAsync` with `SequestrationFlagKind.FeatureGateOff`; emit `ExtensionFieldSequestered`; return nothing
     - `Redact`: call `AssertRedactAuthorisedAsync`; tombstone; emit `ExtensionFieldRedacted`; return nothing

4. `AssertRedactAuthorisedAsync` private method per ADR 0075 §`Redact` capability gate (full spec):
   - Throws `InvalidOperationException` if `capabilityGraph is null || signer is null`
   - Calls `capabilityGraph.QueryAsync(signer.IssuerId, new Resource($"extension-field#{entityType.FullName}#{spec.Key.Value}"), RedactExtensionFieldAction, clock.GetUtcNow(), ct)`
   - On `false`: throws `ExtensionFieldRedactionDeniedException`

**Key implementation details:**
- Read the `GetFieldsAsync` implementation carefully for thread-safety. The existing lock pattern in `GetFields` must be respected.
- `INodeIdProvider` — verify it exists; if not, use a string constant for the node-id in the `SequesterAsync` call and flag as halt-condition.
- `IAuditTrail is null` → skip audit emission silently (matches `TenantKeyProviderFieldDecryptor` pattern).

### `packages/foundation-catalog/ExtensionFields/ExtensionFieldCatalogExtensions.cs` (MODIFY — add gating overload)

Add a new DI extension alongside existing `AddSunfishExtensionFieldCatalog`:

```csharp
/// <summary>
/// Registers <see cref="ExtensionFieldCatalog"/> wired with feature-gate evaluation,
/// audit emission, sequestration, and capability-graph dependencies (all nullable).
/// See ADR 0075 §Lazy-DI optionality for null-evaluator semantics.
/// </summary>
public static IServiceCollection AddExtensionFieldCatalogWithFeatureGating(
    this IServiceCollection services)
{
    services.AddSingleton<IExtensionFieldCatalog>(sp => new ExtensionFieldCatalog(
        featureEvaluator: sp.GetService<IFeatureEvaluator>(),
        auditTrail: sp.GetService<IAuditTrail>(),
        sequestrationStore: sp.GetService<ISequestrationStore>(),
        capabilityGraph: sp.GetService<ICapabilityGraph>(),
        signer: sp.GetService<IOperationSigner>(),
        nodeIdProvider: sp.GetService<INodeIdProvider>(),
        clock: sp.GetService<TimeProvider>()));
    return services;
}
```

**Acceptance criteria P2:**
- `dotnet build packages/foundation-catalog` succeeds with no errors
- `IExtensionFieldCatalog.GetFieldsAsync` is callable from a test project
- Null-evaluator path: `GetFieldsAsync` returns all specs with `GateState.Ungated`
- Existing `GetFields` sync path unchanged (no behavior regression)

---

## Phase 3 — Unit tests (~3-4h)

**File:** `packages/foundation-catalog/tests/ExtensionFields/ExtensionFieldGatingTests.cs` (NEW)

Test cases per ADR 0075 §Implementation checklist Phase 7 (copy exactly):

| Test | Condition | Expected |
|---|---|---|
| (a) | Spec has no FeatureKey | Returns `GateState.Ungated` regardless of evaluator state |
| (b) | Gated spec, gate evaluates ON | Returns `GateState.GatedOn` + emits `ExtensionFieldGated` |
| (c) | Gated spec, gate OFF, policy Hide | Field excluded (not in returned list) + emits `ExtensionFieldFiltered` |
| (d) | Gated spec, gate OFF, policy Sequester | Field excluded + `ISequestrationStore.SequesterAsync` called with `FeatureGateOff` + emits `ExtensionFieldSequestered` |
| (e-1) | Gated spec, gate OFF, policy Redact, capability granted | Tombstone path + emits `ExtensionFieldRedacted` |
| (e-2) | Gated spec, gate OFF, policy Redact, capability DENIED | Throws `ExtensionFieldRedactionDeniedException` + emits `ExtensionFieldRedacted` with denial |
| (e-3) | Gated spec, policy Redact, null `capabilityGraph` or null `signer` | Throws `InvalidOperationException` per lazy-DI |
| (f) | Null evaluator | All specs returned as `GateState.Ungated` (no gate evaluation) |
| (g) | `CapabilityAction` constant | Assert `RedactExtensionFieldAction.Name == "redact-extension-field"` + `CapabilityAction.{Read,Write,Delete,Delegate,Sign}` unchanged |
| (h) | Evaluator throws | Gate treated as OFF (fail-closed) + emits `ExtensionFieldGateEvaluationFailed` |

Use NSubstitute for `IFeatureEvaluator`, `IAuditTrail`, `ISequestrationStore`, `ICapabilityGraph`, `IOperationSigner`.

The test project csproj must add `PackageReference` to NSubstitute if not already present. Check `packages/foundation-catalog/tests/tests.csproj` first.

**Acceptance criteria P3:**
- `dotnet test packages/foundation-catalog` — all tests green including new tests
- Test (g) explicitly verifies `CapabilityAction` closed-static-slots invariant

---

## Phase 4 — Stage 06 deliverables (~2h)

### `apps/kitchen-sink` (MODIFY)

Register a feature-gated extension field in the kitchen-sink demo. Example:

1. Register a sample extension field spec on an existing entity type (e.g., `Lease` or `Vendor`) with a `FeatureKey` pointing to a feature key like `"sunfish.demo.extensionfields.betaGatedField"`.
2. Register that feature key in `InMemoryFeatureCatalog` with a default of `false` (gated off by default).
3. Register `IFeatureEvaluator` + `IFeatureCatalog` in the host's DI.
4. Call `GetFieldsAsync(typeof(YourEntity), ctx)` somewhere visible (e.g., a test page or the existing Properties demo if one exists) and log/display the `GateState`.
5. Show that flipping the feature to `true` makes the field appear in the materialized list.

The kitchen-sink demo does NOT need to implement the full Sequester or Redact policies. Hide is sufficient for the demo.

### `apps/docs` (MODIFY or CREATE)

Create `apps/docs/foundation/catalog/feature-gated-extension-fields.md` (follow the pattern of other foundation overview pages). Cover:
- The two new `ExtensionFieldSpec` parameters
- `GetFieldsAsync` vs `GetFields` distinction
- Three policies (Hide / Sequester / Redact) and their reversibility
- DI setup (`AddExtensionFieldCatalogWithFeatureGating`)
- Brief code sample for a gated field registration

If `apps/docs/foundation/catalog/` does not exist, create the directory with a stub `overview.md` and `feature-gated-extension-fields.md`.

### XML docs

Ensure all new public types (`FeatureGateOffPolicy`, `GateState`, `MaterializedExtensionField`, `ExtensionFieldRedactionDeniedException`) and all new/modified interface members have XML documentation summaries. The new `IExtensionFieldCatalog.GetFieldsAsync` overload's xml-doc must reference `GateState` and `FeatureGateOffPolicy`.

### `CHANGELOG.md` (MODIFY — prepend entry)

Following the existing format:
```
## [Unreleased]

### Added
- `ExtensionFieldSpec.FeatureKey` and `ExtensionFieldSpec.FeatureGateOffPolicy` — optional parameters enabling operator-runtime field gating (ADR 0075, W#44).
- `IExtensionFieldCatalog.GetFieldsAsync(Type, FeatureEvaluationContext, CancellationToken)` — async overload that evaluates feature gates and returns `MaterializedExtensionField` records.
- `FeatureGateOffPolicy` enum (Hide / Sequester / Redact).
- `MaterializedExtensionField` record + `GateState` enum.
- `ExtensionFieldRedactionDeniedException` — thrown when Redact policy is denied by the capability graph.
- 5 new `AuditEventType` constants: `ExtensionFieldGated`, `ExtensionFieldFiltered`, `ExtensionFieldSequestered`, `ExtensionFieldRedacted`, `ExtensionFieldGateEvaluationFailed`.
- `AddExtensionFieldCatalogWithFeatureGating(IServiceCollection)` DI extension.
```

**Acceptance criteria P4:**
- `dotnet build apps/kitchen-sink` succeeds
- Kitchen-sink demo registers a gated field; flipping the feature gate changes materialization
- All new public APIs have XML docs
- Changelog entry present

---

## Cross-cutting notes

- **INodeIdProvider existence:** Before writing the `ExtensionFieldCatalog` constructor, grep for `INodeIdProvider` in `packages/foundation/`. If it does not exist, use a `string nodeId = "local"` constructor parameter and flag the gap in the commit message.
- **No new migration package.** The `Sunfish.Foundation.Migration.csproj` is only consumed (ProjectReference); no types are added to it in this workstream.
- **Phase ordering:** Phases 1 → 2 → 3 → 4. Do not start Phase 2 until Phase 1 builds cleanly. Do not start Phase 4 deliverables until Phase 3 tests are green.
- **Commit message format:** `feat(foundation-catalog): W#44 P{N} — <description>` for each PR.
- **Pre-merge council mandatory** per ADR 0069 D1. File council review BEFORE enabling auto-merge on ANY of the 4 PRs.

---

*Hand-off authored: 2026-05-05 by XO (research). ADR 0075 Status: Accepted as of PR #567.*
