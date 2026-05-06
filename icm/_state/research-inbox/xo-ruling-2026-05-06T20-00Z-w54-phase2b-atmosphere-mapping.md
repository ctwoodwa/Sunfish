---
type: ruling
workstream-or-chapter: W#54 Phase 2b — SickBayDataProvider Mission Envelope integration
resolves: cob-question-2026-05-06T18-00Z-w54-mission-envelope-integration.md
---

XO ruling on all three open questions + two ADR 0082 amendment items.

## Q1 — Correct IMissionEnvelopeProvider API

`GetCurrentAsync(ct)` — no TenantId parameter. MissionEnvelope is process-level, not
tenant-scoped. The Phase 2b `SickBayDataProvider.GetAtmosphereAsync` implementation:

```csharp
var envelope = await _missionEnvelopeProvider.GetCurrentAsync(ct).ConfigureAwait(false);
```

Inject `IMissionEnvelopeProvider` (from `Sunfish.Foundation.MissionSpace`) as a constructor
parameter alongside the existing `SickBayOptions` and `TimeProvider`. The hand-off §2.1
citation of `GetCurrentEnvelope(tenant)` was a draft-time error; this ruling supersedes it.

## Q2 — Dimension-to-ProbeStatus mapping

Each of the 10 typed dimension records on `MissionEnvelope` has a `ProbeStatus` field
(verified: `HardwareCapabilities`, `UserCapabilities`, `RegulatoryCapabilities`,
`RuntimeCapabilities`, `FormFactorSnapshot`, `EditionCapabilities`, `NetworkCapabilities`,
`TrustAnchorCapabilities`, `SyncStateSnapshot`, `VersionVectorSnapshot`).

**Warning probes** (WarningProbeCount): `ProbeStatus.Stale` OR `ProbeStatus.PartiallyDegraded`
**Critical probes** (CriticalProbeCount): `ProbeStatus.Failed` OR `ProbeStatus.Unreachable`
**Healthy probes**: `ProbeStatus.Healthy` (excluded from both counts)

**AtmosphereHealth derivation** (from existing enum XML docs):
```
Green:  warnings == 0 && criticals == 0
Yellow: warnings >= 1 && criticals == 0
Orange: (warnings >= 2 && criticals == 0) || (criticals == 1)
Red:    criticals >= 2
```

**Projection helper (canonical Phase 2b pattern):**
```csharp
private static (int warnings, int criticals) CountProbes(MissionEnvelope e)
{
    var statuses = new[]
    {
        e.Hardware.ProbeStatus,
        e.User.ProbeStatus,
        e.Regulatory.ProbeStatus,
        e.Runtime.ProbeStatus,
        e.FormFactor.ProbeStatus,
        e.Edition.ProbeStatus,
        e.Network.ProbeStatus,
        e.TrustAnchor.ProbeStatus,
        e.SyncState.ProbeStatus,
        e.VersionVector.ProbeStatus,
    };
    int w = statuses.Count(s => s is ProbeStatus.Stale or ProbeStatus.PartiallyDegraded);
    int c = statuses.Count(s => s is ProbeStatus.Failed or ProbeStatus.Unreachable);
    return (w, c);
}

private static AtmosphereHealth Classify(int w, int c) => (w, c) switch
{
    (0, 0) => AtmosphereHealth.Green,
    (_, 0) => AtmosphereHealth.Yellow,
    (_, 1) => AtmosphereHealth.Orange,
    _      => AtmosphereHealth.Red,
};
```

Note: `AtmosphereHealth.Unknown` will be added via ADR 0082-A1 (see below). Phase 2b
MUST return `AtmosphereHealth.Unknown` when `_missionEnvelopeProvider` is null OR when
the envelope has not yet been fetched (startup transient). This sentinel signals to the
UI that the real probe data is pending, preventing a misleading Green.

## Q3 — ForceEnableActive sourcing

**Phase 2b ruling: `ForceEnableActive = false` (explicit stub, not implicit default).**

No read path exists in Phase 1 contracts for querying active force-enable overrides:
- `IInstallForceEnableSurface` is write-only (`RequestAsync` only)
- `IFeatureForceEnableSurface.ResolveAsync(featureKey, dimension)` requires a specific
  featureKey — no "get all active force-enables" API exists
- `DefaultInstallForceEnableSurface` has no persistence layer in Phase 1

Phase 3 addendum (out of W#54 scope): add `ValueTask<bool> HasActiveInstallOverrideAsync(ct)`
to `IInstallForceEnableSurface` once `DefaultInstallForceEnableSurface` acquires an
`IInstallForceEnableRepository` backing store. COB should add a `// Phase 3: wire
IInstallForceEnableSurface.HasActiveInstallOverrideAsync` comment at the return site.

## ADR 0082 amendment items (escalated from W#54 P2 council)

Both are filed as **ADR 0082-A1** in this PR:

**A1.1 — AtmosphereHealth.Unknown sentinel:**
Add to `foundation-sick-bay/AtmosphereHealth.cs`:
```csharp
/// <summary>
/// Provider has not yet projected real probe data. UI MUST render a neutral
/// pending state (e.g., spinner or "—"), NEVER Green. Returned by stub
/// implementations before Phase 2b wires IMissionEnvelopeProvider.
/// </summary>
Unknown,
```
The `Unknown` value MUST precede `Green` in the enum definition so new code that
reads the enum doesn't silently default to the lowest numeric value (Green=0). Insert as
first value or a clearly documented non-zero position — XO ruling: **first value (0)**,
with `Green` becoming 1. This means any code doing `default(AtmosphereHealth)` gets
`Unknown` (safe sentinel) rather than `Green` (misleading success).

The Phase 2 stub `SickBayDataProvider.GetAtmosphereAsync` ships `Unknown` (not `Green`)
after this amendment lands. Phase 2b replaces it with real probe data.

**A1.2 — NoopKeyRotationScheduler documentation note:**
Add to `ISickBayCommandService.TriggerKeyRotationAsync` and/or the Phase 2b hand-off:
"Hosts MUST NOT register `NoopKeyRotationScheduler` in any environment that surfaces
a user-visible confirmation ('rotation triggered') — the noop implementation completes
successfully without scheduling real rotation, creating a false security assurance."
Add to ADR 0082 §Implementation checklist: "NoopKeyRotationScheduler is a Phase 2 build
stub only; it MUST be replaced before any user-visible rotation trigger is wired."

## Phase 2b acceptance criteria (for COB)

1. `SickBayDataProvider` constructor takes `IMissionEnvelopeProvider? missionEnvelopeProvider`
   (nullable optional — null → return `AtmosphereHealth.Unknown`)
2. `GetAtmosphereAsync` calls `GetCurrentAsync(ct)`, counts ProbeStatus per mapping above
3. `ForceEnableActive = false` with inline `// Phase 3: wire IInstallForceEnableSurface` comment
4. Returns `AtmosphereHealth.Unknown` when provider is null or envelope not yet cached
5. After A1 lands: stub returns `Unknown`; Phase 2b returns real classification
6. Tests: `GetSnapshotAsync_WithAllHealthyProbes_ReturnsGreen`,
   `GetSnapshotAsync_WithOneWarning_ReturnsYellow`,
   `GetSnapshotAsync_WithOneCritical_ReturnsOrange`,
   `GetSnapshotAsync_WithTwoCriticals_ReturnsRed`,
   `GetSnapshotAsync_WithNullProvider_ReturnsUnknown`

## Hand-off addendum location

COB should open Phase 2b as a follow-on PR. No new hand-off file needed — this ruling
IS the addendum. Archive this ruling after Phase 2b PR merges.
