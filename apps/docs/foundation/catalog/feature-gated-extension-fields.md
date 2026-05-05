# Feature-gated extension fields

`Sunfish.Foundation.Catalog.ExtensionFields` supports operator-issued
feature toggles on registered extension fields. A spec carries an optional
`FeatureKey` and a `FeatureGateOffPolicy`; consumers call
`IExtensionFieldCatalog.GetFieldsAsync` to evaluate gates and receive
`MaterializedExtensionField` records. Per
[ADR 0075 — ExtensionFields Feature-Evaluation Hook](../../../../docs/adrs/0075-extensionfields-feature-evaluation-hook.md).

## Two new optional `ExtensionFieldSpec` parameters

```csharp
public sealed record ExtensionFieldSpec(
    ExtensionFieldKey Key,
    Type ValueType,
    ExtensionFieldScope Scope,
    ExtensionStorage Storage,
    bool IsRequired = false,
    bool IsSearchable = false,
    string? DisplayName = null,
    string? Description = null,
    Sunfish.Foundation.FeatureManagement.FeatureKey? FeatureKey = null, // ← W#44
    FeatureGateOffPolicy FeatureGateOffPolicy = FeatureGateOffPolicy.Hide); // ← W#44
```

`FeatureKey = null` (the default) leaves the field unconditionally visible
— the existing 8-parameter call sites continue to compile and behave
identically.

## `GetFieldsAsync` vs `GetFields`

| API | Behavior |
|---|---|
| `IReadOnlyList<ExtensionFieldSpec> GetFields(Type)` | Returns every registered spec in registration order. **Does NOT apply `FeatureGateOffPolicy`** — all specs surface regardless of evaluator state. Marked `[Obsolete]` (warning-only). |
| `ValueTask<IReadOnlyList<MaterializedExtensionField>> GetFieldsAsync(Type, FeatureEvaluationContext, CancellationToken)` | Evaluates each gated spec via `IFeatureEvaluator`; applies `FeatureGateOffPolicy` to gated-OFF specs; emits one audit record per gate decision. Null-evaluator host returns every spec as `GateState.Ungated`. |

## Three off-policies

| Policy | Reversibility | Behavior on gate-OFF |
|---|---|---|
| `Hide` (default) | Reversible | Field excluded from materialization; underlying data preserved in storage. Flipping the gate ON makes the field reappear. |
| `Sequester` | Reversible | Field excluded **and** its underlying data is registered with `ISequestrationStore.SequesterAsync(nodeId, recordId, SequestrationFlagKind.FeatureGateOff)`. Composes ADR 0028-A5.4. |
| `Redact` | **Irreversible** | Field excluded **and** the catalog calls `ICapabilityGraph.QueryAsync(signer.IssuerId, Resource("extension-field#{Type}#{Key}"), CapabilityAction("redact-extension-field"), now)`. Denied → throws `ExtensionFieldRedactionDeniedException`. Granted → tombstone path (the persistence adapter owns the actual destruction). |

All three paths emit an `AuditEventType.ExtensionField*` record when the
gate evaluator is reachable. **Note:** when required DI dependencies are
missing (`ISequestrationStore` for Sequester; `ICapabilityGraph` +
`IOperationSigner` for Redact), the catalog throws
`InvalidOperationException` *before* any audit is emitted — fail-fast
misconfiguration, not a runtime path.

## DI setup

```csharp
services.AddExtensionFieldCatalogWithFeatureGating();
```

Resolves `IFeatureEvaluator`, `IAuditTrail`, `ISequestrationStore`,
`ICapabilityGraph`, `IOperationSigner`, `TimeProvider` via
`GetService<T>()` — null dependencies leave the corresponding gate path
inactive (per ADR 0075 §Lazy-DI optionality). The `Sequester` and
`Redact` policies throw `InvalidOperationException` at evaluation time
when their required deps (`ISequestrationStore` for Sequester;
`ICapabilityGraph` + `IOperationSigner` for Redact) aren't wired —
intentional fail-fast for misconfiguration.

## Sample registration

```csharp
catalog.Register(typeof(Lease), new ExtensionFieldSpec(
    Key: new ExtensionFieldKey("renewals.autoReminders"),
    ValueType: typeof(bool),
    Scope: ExtensionFieldScope.Bundle,
    Storage: ExtensionStorage.Json,
    DisplayName: "Auto-renewal reminders",
    FeatureKey: FeatureKey.Of("sunfish.blocks.leases.renewals.autoReminders"),
    FeatureGateOffPolicy: FeatureGateOffPolicy.Hide));
```

When the operator issues a Standing Order setting
`features.sunfish.blocks.leases.renewals.autoReminders = true`,
`GetFieldsAsync` returns the spec with `GateState.GatedOn`. While the
toggle is OFF (or unset), the spec is excluded from the materialized
list and an `ExtensionFieldFiltered` audit record fires.

## Evaluator failure semantics

If `IFeatureEvaluator.IsEnabledAsync` throws, the gate is treated as
**OFF** (fail-closed) and the catalog emits both an
`ExtensionFieldGateEvaluationFailed` audit and the policy's normal
gated-OFF audit (`Filtered` / `Sequestered` / `Redacted`). This avoids
data leakage from a stale or broken evaluator. The exception's `Message`
is captured in the failure audit's `exception_message` payload field.

## Audit set

Each evaluation emits exactly one of:

| Event type | Triggered by |
|---|---|
| `ExtensionFieldGated` | gate evaluated ON |
| `ExtensionFieldFiltered` | gate OFF + policy `Hide` |
| `ExtensionFieldSequestered` | gate OFF + policy `Sequester` (after `SequesterAsync` returns) |
| `ExtensionFieldRedacted` | gate OFF + policy `Redact` (audit fires regardless of capability outcome — denied case still records the attempted denial) |
| `ExtensionFieldGateEvaluationFailed` | evaluator threw (paired with the policy's normal audit) |

All audit emission is best-effort — failures in `IAuditTrail.AppendAsync`
are swallowed so the gate-evaluation hot path is never blocked by audit
backend issues. (Cohort precedent: `TenantKeyProviderFieldDecryptor`.)
