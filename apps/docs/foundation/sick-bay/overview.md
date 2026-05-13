# Sick Bay — Foundation Contracts

`Sunfish.Foundation.SickBay` declares the contracts for the Sick Bay
aggregation surface per
[ADR 0082](../../../docs/adrs/0082-sick-bay-aggregation-surface.md).

## Contracts

| Type | Description |
|---|---|
| `ISickBayDataProvider` | `GetSnapshotAsync(TenantId, ct)` + `SubscribeSnapshotAsync(TenantId, ct)`. MUST NOT reference `IFieldDecryptor` (ADR 0046-A2 §4 + H4 reflection test). |
| `ISickBayCommandService` | `TriggerKeyRotationAsync(TenantId, fieldPurpose, triggerReason, ct)`. Emits `SickBayKeyRotationTriggered` before calling `IKeyRotationScheduler.ScheduleAsync` (audit-before-operation invariant). |
| `IMedevacService` | Six-state machine: Idle → PendingAuthorization → InProgress → Complete. Four-eyes invariant: self-approval emits `SickBayMedevacSelfApprovalRejected` then throws. |
| `IFirstAidSurface` | `GetContextualHintsAsync(surfaceKey, ct)`. Unknown keys return empty list. |
| `IStretcherBearerPolicy` | `GetEligibleRespondersAsync(TenantId, ct)`. List is for notification routing ONLY — not for permission or authority decisions. |
| `IKeyRotationScheduler` | `ScheduleAsync(TenantId, fieldPurpose, triggerReason, ct)`. Abstraction layer over W#32 / ADR 0046-A2 rotation substrate. |

## Data model

| Type | Description |
|---|---|
| `SickBaySnapshot` | Aggregated snapshot: `Pharmacy` + `Lab` + `Atmosphere` + `MedevacState` + `CapturedAt`. |
| `PharmacyInventoryEntry` | Per-field-purpose record: `RecordCount` (k=3 floor), `RotationStatus`, `LastRotatedAt`, `HasCompromiseFlag`. |
| `PharmacyRecordCount` | k=3 anonymity floor. `Suppressed` sentinel when count < 3; `Exact(n)` factory. |
| `LabDiagnosticResult` | Per-probe: `ProbeName`, `Status` (`ProbeStatus`), `Degradation` (`DegradationKind`). |
| `AtmosphereReadout` | `OverallHealth` (`AtmosphereHealth`) + warning/critical probe counts. |
| `AtmosphereHealth` | `Unknown` (zero-value sentinel), `Green`, `Yellow`, `Orange`, `Red`. |
| `MedevacState` | `Idle`, `Requested`, `PendingAuthorization`, `Authorized`, `InProgress`, `Complete`. |
| `FirstAidHint` | `Key`, `Title`, `Body` (plain-text only; rejects `<`, `>`, `&`, control chars), `Level`. |
| `StretcherBearerRole` | `DCA`, `MPA`, `CommsOfficer`, `SonarOfficer` — constrained subset of `ShipRole`. |
| `SickBayOptions` | `RegisteredFieldPurposes`, `FallbackPollingInterval` (default 60s), `RegisterNoopKeyRotationScheduler` (default false). |

## Audit constants (ADR 0082 §6)

All 10 event types are kebab-case discriminators in `kernel-audit`:

`sick-bay.pharmacy.viewed` · `sick-bay.key-rotation.triggered` ·
`sick-bay.lab.viewed` · `sick-bay.atmosphere.viewed` ·
`sick-bay.medevac.initiated` · `sick-bay.medevac.authorized` ·
`sick-bay.medevac.cancelled` · `sick-bay.medevac.completed` ·
`sick-bay.medevac.self-approval-rejected` · `sick-bay.recovery-contact.managed`

## Medevac state-transition table (ADR 0082 §2)

| From | Event | To |
|---|---|---|
| `Idle` | `RequestAsync` | `PendingAuthorization` |
| `PendingAuthorization` | `AuthorizeAsync` (different principal) | `InProgress` |
| `PendingAuthorization` | `AuthorizeAsync` (same principal) | `Idle` (four-eyes rejection) |
| `PendingAuthorization`, `InProgress` | `CancelAsync` | `Idle` |
| `InProgress` | `CompleteAsync` | `Complete` |
| `Idle`, `Complete` | `CancelAsync` | `InvalidOperationException` |
