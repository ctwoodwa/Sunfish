# Sick Bay

`Sunfish.Blocks.SickBay` is the block-tier aggregation surface for the
Sick Bay department — pharmacy key-health, lab diagnostics, mission
atmosphere, and the medevac workflow. It implements
[ADR 0082 — Sick Bay Aggregation Surface + IDC Role](../../../docs/adrs/0082-sick-bay-aggregation-surface.md).

## Components

| Component | Role |
|---|---|
| `SickBayBlock` | Root tab container: Pharmacy / Lab / Atmosphere. Pharmacy tab hidden when `CanViewPharmacy=false`. |
| `PharmacyTabContent` | Inventory rows with `RotationHealth` triple-encoded badge (color + icon + text). `PharmacyRecordCount` renders `"< 3"` when suppressed per k=3 floor. |
| `LabTabContent` | Probe-history table with `<caption>` per SC 1.3.1. |
| `AtmosphereTabContent` | Health gauge with polite `aria-live` for status updates; assertive only on Red escalation. |
| `MedevacDialog` | `role="alertdialog"` four-eyes workflow dialog. Initial focus → Cancel. `IFocusTrap` wired. |
| `KeyFingerprintDisplay` | Monospace chunked fingerprint display (gated on W#53 `KeyFingerprint`). |

## Services

| Type | Role |
|---|---|
| `ISickBayDataProvider` | Pharmacy + Lab + Atmosphere snapshot. k=3 floor enforced; no `IFieldDecryptor`. |
| `ISickBayCommandService` | `TriggerKeyRotationAsync` with audit-before-operation (ADR 0082 §6). |
| `IMedevacService` | Six-state machine (Idle → PendingAuthorization → InProgress → Complete). Four-eyes invariant. |
| `IFirstAidSurface` | Contextual hints by surface key (`"pharmacy"`, `"lab"`, `"atmosphere"`). |
| `IStretcherBearerPolicy` | Eligible-responder list for notification routing (NOT authority). |
| `IKeyRotationScheduler` | Rotation scheduling abstraction. `NoopKeyRotationScheduler` ships until W#32 wires the real scheduler. |

## DI registration

```csharp
// Registers all implementations with TryAddSingleton.
// RegisterNoopKeyRotationScheduler=true wires the Noop scheduler (opt-in
// per ADR 0082-A1.4 §Trust posture).
builder.Services.AddSunfishSickBayDefaults(opts =>
{
    opts.RegisterNoopKeyRotationScheduler = true;
    opts.RegisterPurpose("ssn", "Social Security Number");
    opts.RegisterPurpose("dob", "Date of Birth");
    opts.FallbackPollingInterval = TimeSpan.FromSeconds(60);
});
```

## Accessibility

All 11 WCAG 2.2 AA success criteria declared in ADR 0082 §8 are met.
See [Sick Bay WCAG declaration](../../design-system/sick-bay-wcag.md).
