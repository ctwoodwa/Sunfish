# Foundation.Migration substrate

`Sunfish.Foundation.Migration` is the foundation-tier substrate for cross-form-factor migration semantics — the building block that makes "user upgraded their phone" / "tablet ran out of storage" / "headless deployment lost a sensor" / "adapter version was rolled back" preserve workspace data instead of dropping it.

It implements [ADR 0028](../../../docs/adrs/0028-crdt-engine-selection.md) amendments A5 (cross-form-factor migration) + A8 (post-A8 council-fixed surface) + A8.5 (field-level write authorization) + A8.7 (`AdapterRollbackDetected` 6-hour dedup window).

## What it gives you

| Type | Role |
|---|---|
| `FormFactorProfile` | Host hardware-tier snapshot per A5.1 (form factor, input modalities, display class, network posture, storage budget, power profile, sensor surface, instance class). |
| `HardwareTierChangeEvent` | Re-profile event per A5.3 — fired when storage / network / sensor / power / adapter / manual triggers change the profile. |
| `DerivedSurface` | The active capability set for a host — intersection of the form factor's derivable capabilities with the workspace's declared requirements per A5.1. |
| `SequesteredRecord` | Per-record entry in the sequestration partition — tracks `(NodeId, RecordId, RequiredCapability, IsEncrypted, IsPrimaryKeyEncrypted, IsCpClass, Flag)`. |
| `IFormFactorMigrationService` / `InMemoryFormFactorMigrationService` | Phase-2 derived-surface filter + Phase-3 sequestration-rule engine + Phase-4 audit emission, in three constructor overloads. |
| `ISequestrationStore` / `InMemorySequestrationStore` | Sequestration partition (Concurrent-dictionary-backed; thread-safe; not durable across process restarts in v0). |
| `MigrationAuditPayloads` | Factory for the 5 sequestration-related audit event bodies (alphabetized; opaque to the substrate). |
| 8 enum discriminators | `FormFactorKind` (8 values per A8.4) / `InputModalityKind` (7) / `DisplayClassKind` (5) / `NetworkPostureKind` (4) / `PowerProfileKind` (4) / `SensorKind` (7) / `TriggeringEventKind` (7) / `SequestrationFlagKind` (5 per A8.3). |

`InstanceClassKind` is reused from `Sunfish.Foundation.Versioning` per A7.6 — no enum duplication.

## Invariant DLF (data-loss-vs-feature-loss)

The defining contract: **feature deactivation never causes data loss**. When a host's derived surface contracts and previously-visible records fall outside it, those records are moved to an in-replica sequestration partition with a flag — not deleted. When the surface later expands, sequestered records matching it are released back to active visibility. If F's storage budget shrinks below what's needed even for sequestration, F may evict the partition entirely — but the workspace's other peers retain the records (the CRDT log is global), so F can re-fetch on demand via federation, paying only the network cost.

| Sequestration flag | Source rule | Trigger |
|---|---|---|
| `FormFactorFilteredOut` | A5.4 rule 1 | Capability lost; record un-readable on the new form factor. |
| `StorageBudgetExceeded` | A5.4 rule 1 | `TriggeringEventKind.StorageBudgetChanged` overrides any other classification. |
| `PlaintextSequestered` | A8.3 rule 5 | Record is plaintext-readable but UI-hidden because the form factor lacks the feature surface. Release on surface expansion is immediate. |
| `CiphertextSequestered` | A8.3 rule 5 | Record is ciphertext-stored but plaintext-unrecoverable because the form factor lacks the per-tenant decryption key. Release requires re-running A5.7 key transfer. |
| `FormFactorQuorumIneligible` | A8.3 rule 6 | CP-class record sequestered → host's vote is ineligible for that record's quorum. |

## Failover for un-decryptable fields (A8.3 rule 7)

`SequesteredRecord.IsPrimaryKeyEncrypted` distinguishes:

- `false` → **field-level redaction**. The record stays visible; un-decryptable fields render as `[encrypted; not available on this form factor]` placeholders.
- `true` → **record-level sequestration**. The whole record is hidden because its primary-key / display-name fields can't be decrypted.

The choice is per-record-type and documented at Stage 06 hand-off — the substrate just exposes the flag.

## Field-level write authorization (A8.5 rule 6)

`CanWriteFieldAsync(nodeId, fieldEntryId, ct)` is the gate consumers call at their CRDT-write boundary. A field is write-sequestered iff it is read-sequestered (because the host lacks the per-tenant key). Phantom writes from hosts that can't decrypt the field never reach the merge — `CanWriteFieldAsync` returns false + emits `FieldWriteSequestered` (when audit is wired).

## API at a glance

```csharp
// Bootstrap (audit-disabled — test/bootstrap)
services.AddInMemoryMigration();

// Bootstrap (audit-enabled — production; both IAuditTrail + IOperationSigner
// must already be registered)
services.AddInMemoryMigration(currentTenantId);

// Register a record so the substrate manages its sequestration state
var store = sp.GetRequiredService<ISequestrationStore>();
await store.RegisterAsync(new SequesteredRecord
{
    NodeId = "this-host",
    RecordId = "lease-123",
    RequiredCapability = "sensor.BiometricAuth",
    IsEncrypted = true,
    IsPrimaryKeyEncrypted = false, // field-level redaction default per A8.3 rule 7
    IsCpClass = false,
});

// On every hardware-tier change, apply the migration rules
var migration = sp.GetRequiredService<IFormFactorMigrationService>();
await migration.ApplyMigrationAsync(new HardwareTierChangeEvent
{
    NodeId = "this-host",
    PreviousProfile = previousProfile,
    CurrentProfile = currentProfile,
    TriggeringEvent = TriggeringEventKind.AdapterDowngrade,
    DetectedAt = DateTimeOffset.UtcNow,
});

// At the CRDT-write boundary — gate writes on field-level authorization
if (!await migration.CanWriteFieldAsync("this-host", $"{recordId}#{fieldName}"))
{
    return WriteResult.Rejected; // FieldWriteSequestered audit emitted
}
```

## Audit emission

Ten new `AuditEventType` discriminators ship with this substrate (per ADR 0049 + A5/A8):

| Event type | Emitted by |
|---|---|
| `HardwareTierChanged` | `ApplyMigrationAsync` (every call). |
| `PlaintextSequestered` / `CiphertextSequestered` | `ApplyMigrationAsync` per A8.3 rule 5 flag classification. |
| `DataReleased` | `ApplyMigrationAsync` on surface-expansion transitions. |
| `FormFactorQuorumIneligible` | `ApplyMigrationAsync` for CP-class records that can't be read. |
| `FieldWriteSequestered` | `CanWriteFieldAsync` rejection. |
| `AdapterRollbackDetected` | `ApplyMigrationAsync` on `TriggeringEventKind.AdapterDowngrade` — dedup'd 1-per-(node_id, previous_form_factor, current_form_factor) per a 6-hour rolling window per A8.7. |
| `FormFactorProvisioned` / `FormFactorEnrollmentCompleted` | Reserved for the A5.7 QR-onboarding handshake (gated on ~ADR-0032-A1 ratification per the W#35 hand-off halt-condition; ships in a later workstream). |
| `LegacyEpochEvent` | Reserved for A7.5.3 — events referencing pre-window schema epochs. |

Payload bodies are alphabetized + opaque to the substrate (mirrors the `VersionVectorAuditPayloads` + `TaxonomyAuditPayloadFactory` conventions used by W#34 + W#30). Audit emission is opt-in: pass `IAuditTrail` + `IOperationSigner` + `TenantId` to the audit-enabled overloads. Without them, the migration service still works — it just doesn't emit.

## Phase 1 scope

What ships in this substrate:

- 8 enum discriminators + 4 records (`FormFactorProfile` / `HardwareTierChangeEvent` / `DerivedSurface` / `SequesteredRecord`).
- `IFormFactorMigrationService` with the A5.1 derived-surface filter + A5.4 + A8.3 + A8.5 rule engine.
- `ISequestrationStore` + `InMemorySequestrationStore` (concurrent-dict, thread-safe).
- 10 new `AuditEventType` constants in `Sunfish.Kernel.Audit` + `MigrationAuditPayloads` factory.
- `AddInMemoryMigration()` DI extension (audit-disabled + audit-enabled).

Deferred follow-ups:

- `IFormFactorMigrationService.EnrollAsync` (A5.7 QR-onboarding) — gated on ~ADR-0032-A1 ratification. The interface declares it but implementation throws until the protocol formalizes.
- Durable sequestration backend (the v0 in-memory store is non-durable).
- Consumer wiring (W#23 iOS Field-Capture App, W#28 Public Listings cross-form-factor scenarios, A6.11 iOS A1 envelope augmentation per A1.x) — separate workstreams.
