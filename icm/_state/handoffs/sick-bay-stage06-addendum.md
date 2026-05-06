# W#54 Stage 06 Hand-off Addendum — Mission Envelope integration (Phase 2b)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-06
**Augments:** [`sick-bay-stage06-handoff.md`](./sick-bay-stage06-handoff.md) §2.1 + Phase 2 halt-condition H2.A
**Spec source:** [ADR 0082 Amendment A1](../../../docs/adrs/0082-sick-bay-aggregation-surface.md#amendment-a1) — Mission Envelope integration + AtmosphereHealth.Unknown sentinel + NoopKeyRotation guidance (Proposed 2026-05-06)
**Status:** W#54 row stays `building` (Phase 2 merged via PR #695; Phase 2b is the new sub-phase introduced by ADR 0082 A1; Phase 3a still gated on H2 KeyFingerprint plus this addendum).

---

## Why this addendum

PR #695 (W#54 Phase 2) shipped four reference impls plus the H4 reflection test, but **deferred the Mission Envelope integration to Phase 2b** because:

1. **Hand-off §2.1 cited an API that does not exist on `origin/main`.** The handoff says `IMissionEnvelopeProvider.GetCurrentEnvelope(tenant)`. The actual contract on `packages/foundation-mission-space/Services/Contracts.cs:51` is `ValueTask<MissionEnvelope> GetCurrentAsync(CancellationToken ct = default)` — process-level, no tenant param.
2. **The dimension-to-probe-status projection logic was undefined.** `MissionEnvelope` exposes ten typed dimension records (each carrying a `ProbeStatus` field) — not a flat probe-result list and not per-dimension `DegradationKind`. The hand-off conflates `ProbeStatus` (per-dimension probe health) with `DegradationKind` (per-feature-verdict gate output).
3. **PR #695 council §Trust review** flagged two stub-behavior risks: `AtmosphereHealth.Green` from the Phase 2 stub is indistinguishable from a real Green, and `NoopKeyRotationScheduler` returning `Task.CompletedTask` silently is misleading-success behind a "rotation triggered" toast.

ADR 0082 Amendment A1 (Proposed) closes all three issues. **This addendum is the COB-side companion** — it corrects the hand-off §2.1 API references, specifies the Phase 2b implementation per ADR 0082 A1.8, and pins the test list. The original hand-off's other phases (Phase 1 merged, Phase 3a, Phase 3b, Phase 4, Phase 5) are unchanged.

---

## §1 — §2.1 API drift correction (mechanical)

The original hand-off §2.1 reads:

> **Lab**: `IMissionEnvelopeProvider.GetCurrentEnvelope(tenant)` (exact API per `Sunfish.Foundation.MissionSpace` — verify on origin/main) → derive `LabDiagnosticResult` per probe.
>
> **Atmosphere**: map `MissionEnvelope` `DegradationKind` counts → `AtmosphereHealth` via documented thresholds...

**Corrected version** (apply mentally when reading the hand-off; the addendum is the canonical source going forward):

> **Lab**: `await IMissionEnvelopeProvider.GetCurrentAsync(ct)` (returns `ValueTask<MissionEnvelope>`; no tenant parameter — `MissionEnvelope` is process-level per ADR 0062 A1.2) → derive one `LabDiagnosticResult` per dimension (10 entries, one per `MissionEnvelope` dimension record).
>
> **Atmosphere**: map per-dimension `ProbeStatus` (the field that exists) — NOT `DegradationKind` (which lives on `FeatureVerdict`, not on dimension records). The projection table is in ADR 0082 A1.2.1; the threshold derivation is in A1.2.2.

**Halt-condition H2.A is RESOLVED** by this addendum: the API signature is now correctly cited.

---

## §2 — Phase 2b implementation specification

Phase 2b is a new sub-phase in ADR 0082 §9, slotted between Phase 2 (merged) and Phase 3a. Estimated **~2-3h, single PR**. Pre-merge **standard adversarial council canonical per ADR 0069 D1**; security-engineering subagent NOT required for this sub-phase (no decryption-path or audit-emission changes).

### 2.1 Add `AtmosphereHealth.Unknown` sentinel

File: `packages/foundation-sick-bay/AtmosphereHealth.cs`

Insert `Unknown` as the **first** enum member (ordinal 0) so that `default(AtmosphereHealth) == Unknown` (preserves §Trust contract — zero-init MUST NOT default to `Green`):

```csharp
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AtmosphereHealth
{
    /// <summary>
    /// The provider has not yet projected real Mission Envelope probe data —
    /// e.g., Phase 2 stub state, or Phase 2b before the first
    /// IMissionEnvelopeProvider.GetCurrentAsync(ct) call has resolved.
    /// UI consumers MUST render a neutral / pending state (e.g., a spinner,
    /// "—" placeholder, or "data not yet available" banner). UI consumers
    /// MUST NOT render Unknown as Green; the semantics differ.
    /// </summary>
    Unknown,

    /// <summary>All probes reporting healthy.</summary>
    Green,

    /// <summary>One or more probes warning; no critical states.</summary>
    Yellow,

    /// <summary>Multiple warnings or one critical probe.</summary>
    Orange,

    /// <summary>Multiple critical probes; immediate intervention required.</summary>
    Red,
}
```

Existing `switch` expressions on `AtmosphereHealth` will get CS8509 warnings. Add an `Unknown` arm (rendering precedent for Phase 3a Blazor: render a neutral spinner / placeholder per ADR 0082 A1.3 WCAG note).

### 2.2 Migrate Phase 2 stub to return `Unknown`

File: `packages/blocks-sick-bay/SickBayDataProvider.cs`

The current `BuildAtmosphereStub` (lines 143–149) returns `AtmosphereHealth.Green`. Migrate to:

```csharp
private static AtmosphereReadout BuildAtmosphereUnknown(DateTimeOffset capturedAt) =>
    new AtmosphereReadout(
        OverallHealth: AtmosphereHealth.Unknown,
        WarningProbeCount: 0,
        CriticalProbeCount: 0,
        ForceEnableActive: false,
        CapturedAt: capturedAt);
```

This stub is used as the fallback when `IMissionEnvelopeProvider` is not injected (constructor parameter is nullable for backward-compat with Phase 2 tests until those tests are updated).

### 2.3 Inject `IMissionEnvelopeProvider`

Add a constructor parameter:

```csharp
public SickBayDataProvider(
    IOptions<SickBayOptions> options,
    IMissionEnvelopeProvider? envelopeProvider = null,
    TimeProvider? timeProvider = null)
{
    ArgumentNullException.ThrowIfNull(options);
    _options = options;
    _envelopeProvider = envelopeProvider;
    _time = timeProvider ?? TimeProvider.System;
}
```

`IMissionEnvelopeProvider` is in `Sunfish.Foundation.MissionSpace` namespace. The `Sunfish.Blocks.SickBay` csproj must add a `ProjectReference` to `packages/foundation-mission-space/`. Verify the project reference does not introduce a cycle: `foundation-mission-space` does NOT depend on `foundation-sick-bay` (verified — `foundation-mission-space` depends only on `foundation`, `foundation/Crypto`, etc.).

### 2.4 Implement `BuildAtmosphereAsync`

```csharp
private async ValueTask<AtmosphereReadout> BuildAtmosphereAsync(
    DateTimeOffset capturedAt,
    CancellationToken ct)
{
    if (_envelopeProvider is null)
    {
        return BuildAtmosphereUnknown(capturedAt);
    }

    MissionEnvelope envelope;
    try
    {
        envelope = await _envelopeProvider.GetCurrentAsync(ct).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch
    {
        // Provider failed; degrade to Unknown rather than throwing.
        // Per ADR 0082 §2 GetSnapshotAsync contract: per-department failure
        // returns a partial / degraded snapshot, not a thrown exception.
        return BuildAtmosphereUnknown(capturedAt);
    }

    int warningCount = 0;
    int criticalCount = 0;

    // Apply ADR 0082 A1.2.1 ProbeStatus → severity bucket projection.
    void Bucket(ProbeStatus status)
    {
        switch (status)
        {
            case ProbeStatus.Healthy:                            break;  // no count
            case ProbeStatus.Stale:              warningCount++;  break;
            case ProbeStatus.PartiallyDegraded:  warningCount++;  break;
            case ProbeStatus.Failed:             criticalCount++; break;
            case ProbeStatus.Unreachable:        criticalCount++; break;
            default:                                              break;  // forward-compat
        }
    }

    Bucket(envelope.Hardware.ProbeStatus);
    Bucket(envelope.User.ProbeStatus);
    Bucket(envelope.Regulatory.ProbeStatus);
    Bucket(envelope.Runtime.ProbeStatus);
    Bucket(envelope.FormFactor.ProbeStatus);
    Bucket(envelope.Edition.ProbeStatus);
    Bucket(envelope.Network.ProbeStatus);
    Bucket(envelope.TrustAnchor.ProbeStatus);
    Bucket(envelope.SyncState.ProbeStatus);
    Bucket(envelope.VersionVector.ProbeStatus);

    bool forceEnableActive = false;  // Phase 2b: stub false until force-enable surface wired
    AtmosphereHealth overall = DeriveOverallHealth(warningCount, criticalCount, forceEnableActive);

    return new AtmosphereReadout(
        OverallHealth:      overall,
        WarningProbeCount:  warningCount,
        CriticalProbeCount: criticalCount,
        ForceEnableActive:  forceEnableActive,
        CapturedAt:         capturedAt);
}

// ADR 0082 A1.2.2 derivation rules.
private static AtmosphereHealth DeriveOverallHealth(int warning, int critical, bool forceEnable)
{
    if (critical >= 3)                                return AtmosphereHealth.Red;
    if (critical >= 1 || forceEnable)                 return AtmosphereHealth.Orange;
    if (warning >= 1)                                 return AtmosphereHealth.Yellow;
    return AtmosphereHealth.Green;
}
```

`ForceEnableActive` is wired to a stub `false` for Phase 2b. A future amendment will inject `IFeatureForceEnableSurface` + the registered feature-keys list and replace the `false` with a real resolution. This is documented in ADR 0082 §A1.2.3.

### 2.5 Lab projection — deferred to Amendment A2

**Lab projection is NOT part of Phase 2b.** Council (PR #701, HA2 Path B) rejected the
`DegradationKind.AdvisoryCaveat` fallback as a §Trust violation: synthesizing a degradation
taxon not present in source data is the same class of misleading-success defect that A1.3
(`AtmosphereHealth.Unknown`) and A1.4 (`NoopKeyRotationScheduler` guidance) reject.

Phase 2b's `BuildSnapshotAsync` returns `Array.Empty<LabDiagnosticResult>()` for the Lab list:

```csharp
var lab = Array.Empty<LabDiagnosticResult>();
// Lab projection deferred to Phase 2c (post-Amendment A2).
// Amendment A2 will widen LabDiagnosticResult.Degradation to DegradationKind?
// (nullable) so that "not-yet-evaluated" is represented as null, mirroring
// the AtmosphereHealth.Unknown sentinel pattern from A1.3.
```

The Phase 2c implementation (post-A2) will:
- Use constructor-style invocation (positional record — no object-initializer syntax):
  `new LabDiagnosticResult(dimensionId, dimensionId, status, null, capturedAt, null)`
- Use `DateTimeOffset capturedAt` directly for `LastRunAt` (NOT `NodaTime.Instant` —
  cohort precedent W#46/W#49/W#50/W#54/W#55: `DateTimeOffset` over NodaTime per ADR 0082 line 360)

### 2.6 Update `BuildSnapshot` and `GetSnapshotAsync`

Replace the synchronous `BuildSnapshot()` with an async `BuildSnapshotAsync(ct)` that awaits the envelope once and uses it for `BuildAtmosphereFromEnvelope` (Lab projection deferred to Phase 2c per HA2 Path B):

```csharp
public async Task<SickBaySnapshot> GetSnapshotAsync(
    TenantId tenant, CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();
    return await BuildSnapshotAsync(ct).ConfigureAwait(false);
}

private async ValueTask<SickBaySnapshot> BuildSnapshotAsync(CancellationToken ct)
{
    var capturedAt = _time.GetUtcNow();

    // Single envelope read per snapshot — feeds both Atmosphere and Lab.
    MissionEnvelope? envelope = null;
    if (_envelopeProvider is not null)
    {
        try
        {
            envelope = await _envelopeProvider.GetCurrentAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch { envelope = null; }
    }

    var atmosphere = envelope is null
        ? BuildAtmosphereUnknown(capturedAt)
        : BuildAtmosphereFromEnvelope(envelope, capturedAt);

    // Lab projection deferred to Phase 2c (post-Amendment A2) per HA2 Path B.
    // A2 widens LabDiagnosticResult.Degradation to DegradationKind? (nullable).
    var lab = Array.Empty<LabDiagnosticResult>();

    return new SickBaySnapshot(
        Pharmacy:     BuildPharmacy(capturedAt),
        Lab:          lab,
        Atmosphere:   atmosphere,
        MedevacState: MedevacState.Idle,
        CapturedAt:   capturedAt);
}
```

Refactor `BuildAtmosphereAsync` into a sync `BuildAtmosphereFromEnvelope(MissionEnvelope, DateTimeOffset)` helper to avoid awaiting the same envelope twice. The Phase 2 wrapper preserves the `IAsyncEnumerable` SubscribeSnapshotAsync stream.

### 2.7 Wire `IMissionEnvelopeObserver`

Replace the "emit-once-then-poll" `SubscribeSnapshotAsync` with an observer-driven implementation:

- Subscribe `this` (or a private inner observer class) to `IMissionEnvelopeProvider.Subscribe(observer)` on first `SubscribeSnapshotAsync` call.
- On `IMissionEnvelopeObserver.OnChangedAsync(EnvelopeChange)` invocation, push a new snapshot into a `Channel<SickBaySnapshot>` and consume it from the `IAsyncEnumerable`.
- Coalesce concurrent change events (debounce ~250ms) — Mission Envelope can flap during multi-dimension probe runs; coalescing prevents UI thrashing.
- Keep the `FallbackPollingInterval` re-poll loop as a backstop (in case the observer is not invoked for some reason). 60s default is fine.
- On the consumer's `CancellationToken` cancellation, unsubscribe via `IMissionEnvelopeProvider.Unsubscribe(observer)`.

### 2.8 `NoopKeyRotationScheduler` registration guidance

Add a `SickBayOptions.RegisterNoopKeyRotationScheduler` boolean property (default `false`). In `AddSunfishSickBayDefaults`:

```csharp
public static IServiceCollection AddSunfishSickBayDefaults(
    this IServiceCollection services,
    Action<SickBayOptions>? configure = null)
{
    var options = new SickBayOptions();
    configure?.Invoke(options);
    services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));

    services.TryAddSingleton<ISickBayDataProvider, SickBayDataProvider>();
    services.TryAddSingleton<IFirstAidSurface, DefaultFirstAidSurface>();
    services.TryAddSingleton<IStretcherBearerPolicy, DefaultStretcherBearerPolicy>();

    if (options.RegisterNoopKeyRotationScheduler)
    {
        services.TryAddSingleton<IKeyRotationScheduler, NoopKeyRotationScheduler>();
    }
    // Else: IKeyRotationScheduler is left unregistered; consumers that
    // resolve it will see DI resolution failure rather than silent Noop.

    return services;
}
```

ADR 0082 §Trust posture (per A1.4): hosts MUST NOT register `NoopKeyRotationScheduler` in any environment that surfaces a user-visible "rotation triggered" affordance. The opt-in flag forces this to be a deliberate decision.

---

## §3 — Phase 2b tests

```
SickBayDataProviderTests (additions; existing tests preserved):
  [Fact] AtmosphereHealth_Unknown_is_ordinal_zero
  [Fact] BuildAtmosphere_returns_Unknown_when_envelope_provider_is_null
  [Fact] BuildAtmosphere_returns_Unknown_when_envelope_provider_throws
  [Fact] BuildAtmosphere_returns_Green_when_all_probes_Healthy
  [Fact] BuildAtmosphere_returns_Yellow_on_one_Stale_probe
  [Fact] BuildAtmosphere_returns_Yellow_on_one_PartiallyDegraded_probe
  [Fact] BuildAtmosphere_returns_Orange_on_one_Failed_probe
  [Fact] BuildAtmosphere_returns_Orange_on_one_Unreachable_probe
  [Fact] BuildAtmosphere_returns_Red_on_three_Failed_probes
  [Fact] BuildAtmosphere_returns_Orange_when_ForceEnableActive_stub_true
    (deferred: until A1.2.3 wires force-enable; Phase 2b stubs false)
  [Fact] BuildAtmosphere_returns_derived_when_envelope_returns_synchronously
    (pins the no-flicker invariant: when ValueTask resolves synchronously, first snapshot
     has derived health and Unknown is never emitted — per council MIN4 / §7 hand-off note)
  [Fact] GetSnapshotAsync_uses_single_envelope_read_per_invocation
  [Fact] SubscribeSnapshotAsync_re_emits_on_IMissionEnvelopeObserver_change
  [Fact] SubscribeSnapshotAsync_unsubscribes_on_cancellation

SickBayServiceCollectionExtensionsTests (additions):
  [Fact] AddSunfishSickBayDefaults_does_not_register_NoopKeyRotationScheduler_by_default
  [Fact] AddSunfishSickBayDefaults_registers_NoopKeyRotationScheduler_when_option_flag_set
```

The H4 reflection test from PR #695 remains in place and continues to pass — Phase 2b adds an `IMissionEnvelopeProvider` reference, NOT an `IFieldDecryptor` reference.

---

## §4 — Phase 2b halt-conditions

| Halt | Action |
|---|---|
| **HA1** (ADR 0082 A1.10) — ADR 0082 A1 not yet `Status: Accepted` on origin/main | Wait. Phase 2b PR may NOT auto-merge until A1 is Accepted. (Auto-merge gated by ADR 0069 D1 anyway; this is operational reinforcement.) |
| **HA2 — RESOLVED Path B** | Lab projection is deferred to Phase 2c (post-Amendment A2). Phase 2b ships `Array.Empty<LabDiagnosticResult>()`. Amendment A2 widens `LabDiagnosticResult.Degradation` to `DegradationKind?`. No action needed by COB for Phase 2b. |
| **Cycle in csproj graph** — adding ProjectReference `foundation-mission-space` to `Sunfish.Blocks.SickBay.csproj` introduces a cycle | HALT. Verify `foundation-mission-space` does NOT depend (transitively) on `foundation-sick-bay` (verified at addendum-time; recheck before PR build). |

---

## §5 — Cohort discipline reminders

- **Pre-merge council canonical** per ADR 0069 D1. Standard adversarial (4 perspectives) is sufficient for Phase 2b; security-engineering subagent NOT required (no decryption-path or audit-emission changes). Auto-merge stays disabled until council verdict received.
- **Cohort batting average:** 30-of-35 substrate amendments needed council fixes (per W#54 P1 ledger note). The Phase 2b PR is a substrate-touch (new public enum value `AtmosphereHealth.Unknown`) — pre-merge council canonical applies.
- **PR title pattern:** `feat(blocks-sick-bay): W#54 Phase 2b — Mission Envelope integration + AtmosphereHealth.Unknown sentinel`.
- **PR description must reference** ADR 0082 A1 + this addendum + the resolved COB beacon.
- **§A0 in PR description** — list the symbols verified to exist on origin/main before the PR builds (mirror this addendum's ADR 0082 §A1.7 verification block).

---

## §6 — Quick reference: changes to the original hand-off

| Original hand-off section | What this addendum changes |
|---|---|
| §2.1 Lab API call | `IMissionEnvelopeProvider.GetCurrentEnvelope(tenant)` → `await provider.GetCurrentAsync(ct)` (no tenant param; returns `ValueTask<MissionEnvelope>`) |
| §2.1 Atmosphere mapping | `MissionEnvelope DegradationKind counts` → per-dimension `ProbeStatus` projection per ADR 0082 A1.2.1 |
| §2.1 SubscribeSnapshotAsync | "subscribe to `IMissionEnvelopeObserver`" — implementation moved to Phase 2b, with debounce + unsubscribe-on-cancel detail |
| §2.4 NoopKeyRotationScheduler | Phase 2 ships it (per PR #695); Phase 2b makes registration host-opt-in via `SickBayOptions.RegisterNoopKeyRotationScheduler` |
| §2.6 Phase 2 tests | Phase 2 reflection test + minimal pharmacy tests already shipped in PR #695; Phase 2b adds the test list in §3 above |
| §2.7 Phase 2 halt H2.A | RESOLVED by this addendum's §1 |
| Phase 3a Blazor — `AtmosphereHealth.Unknown` WCAG tests | Phase 3a hand-off must add two Bunit component tests per council MIN2: `AtmosphereHealth_Unknown_renders_aria_live_polite_region` + `AtmosphereHealth_Unknown_renders_non_color_marker_OR_text_label`. Both verify the SC 1.4.1 + SC 4.1.3 contract from ADR 0082 §8 for the Unknown state badge. |

The original hand-off remains the canonical Phase 1 + Phase 3a + Phase 3b + Phase 4 + Phase 5 spec; Phase 2 is closed by PR #695; Phase 2b is specified by this addendum.
