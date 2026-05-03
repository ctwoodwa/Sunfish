# Foundation.MissionSpace substrate

`Sunfish.Foundation.MissionSpace` is the foundation-tier substrate for the runtime mission-envelope negotiation protocol — the building block behind every per-feature gate, capability degradation surface, regulatory rule evaluation, edition / trust-anchor / sync-state transition observation, and operator force-enable override across the Sunfish substrate cohort.

It implements [ADR 0062](../../../docs/adrs/0062-mission-space-negotiation-protocol.md) and amendment A1 (the post-W#33 substrate split that closed the §7.2 gap). Within the W#33 cohort this is the **canonical-load-bearing substrate** — every dimension probe, every gate, every install-UX surface, every Bridge subscription event, and every regulatory rule ultimately surfaces through `IMissionEnvelopeProvider` + `IFeatureGate<TFeature>`.

## What it gives you

| Type | Role |
|---|---|
| `MissionEnvelope` | 10-dimension snapshot of the host's runtime mission space + a self-derived `EnvelopeHash` (SHA-256 of canonical-JSON of every other field). |
| `IDimensionProbe<TDimension>` | Per-dimension probe contract carrying `DimensionChangeKind`, `ProbeCostClass`, and `ProbeAsync`. 10 default reference implementations ship in the `Probes/` folder. |
| `IFeatureBespokeProbe<TBespokeSignal>` | Extension surface for per-feature signals not in the canonical 10-dimension envelope. |
| `IFeatureGate<TFeature>` | Per-feature evaluator: `(MissionEnvelope) → FeatureVerdict`. |
| `IMissionEnvelopeProvider` | Central coordinator: `GetCurrentAsync` (single-flight + cache TTL), `InvalidateAsync`, `Subscribe` / `Unsubscribe` for envelope-change observers. |
| `IMissionEnvelopeObserver` | `EnvelopeChange` subscriber. Receives the coalesced change after the 100ms window expires. |
| `IFeatureForceEnableSurface` | Operator-only override path. Per-dimension policy enforcement gates which dimensions are force-enable-able (Hardware + Runtime are `NotOverridable`; Regulatory + Edition are `OverridableWithCaveat`; everything else is `Overridable`). |
| `FeatureVerdict` + `DegradationKind` (5-value) + `EnvelopeChangeSeverity` (4-value, including `ProbeUnreliable`) + `LocalizedString` + `ForceEnablePolicy` | Verdict + degradation taxonomy used by gates and UX consumers. |
| `DefaultMissionEnvelopeProvider` / `DefaultFeatureForceEnableSurface` / 10 `Default*Probe` types | Reference implementations; thread-safe; not durable. |

## The 10 dimensions (A1.2)

`Hardware` · `Runtime` · `Regulatory` · `Edition` · `User` · `Network` · `TrustAnchor` · `SyncState` · `VersionVector` · `FormFactor`

Each dimension has a record under `Models/Dimensions/`, every probe declares a `ProbeCostClass` (`Low` / `Medium` / `High` / `DeepHigh` / `Live`) per A1.6, and the `MissionEnvelope` carries a `ProbeStatus` for each so consumers can detect probe-unreliable conditions per A1.10.

## Per-dimension force-enable policy (A1.9)

| Dimension | Policy | Reason |
|---|---|---|
| `Hardware` | `NotOverridable` | Capability gates exist for safety + correctness; you can't override the truth that a CPU lacks an instruction. |
| `Runtime` | `NotOverridable` | OS / .NET version mismatches break code, not policy. |
| `Regulatory` | `OverridableWithCaveat` | Operators may override with explicit acknowledgement (per A1.9 + W#39 reader-caution carry-forward). |
| `Edition` | `OverridableWithCaveat` | Trial / SKU caps are policy, not safety. |
| `User`, `Network`, `TrustAnchor`, `SyncState`, `VersionVector`, `FormFactor` | `Overridable` | Operator override is a normal admin path. |

`DefaultFeatureForceEnableSurface.RequestAsync` rejects `NotOverridable` dimensions with a `ForceEnableNotPermittedException` and emits `FeatureForceEnableRejected` in the same call. Successful requests emit `FeatureForceEnabled`; revokes emit `FeatureForceRevoked`.

## Coordinator semantics (A1.4)

`DefaultMissionEnvelopeProvider`:

- **Single-flight envelope construction.** N concurrent `GetCurrentAsync` callers see exactly 1 envelope-factory invocation per cache miss; the others await the in-flight result.
- **Cache TTL.** A 30s outer-bound; probe-status transitions drive most invalidation via `InvalidateAsync`.
- **Observer fanout with 100ms coalescing window.** The first envelope change schedules a fanout 100ms ahead; later changes within the window merge their `ChangedDimensions` set into the pending change. At fanout time, every observer receives the coalesced change.
- **100-pending overflow bound, oldest-first eviction.** Bounded queue; overflow emits `MissionEnvelopeObserverOverflow`.
- **Severity classification.** If any probe is non-`Healthy`, the change ships at `ProbeUnreliable` severity per A1.10. Otherwise `Warning` for any change after the first envelope, `Informational` for the initial envelope.

## Audit emission

Eight new `AuditEventType` discriminators ship with this substrate:

| Event type | Emitted by |
|---|---|
| `FeatureProbed` | A dimension probe ran (per A1.2 rename of `CapabilityProbed`). |
| `FeatureProbeFailed` | A dimension probe failed. Drives `EnvelopeChangeSeverity.ProbeUnreliable`. |
| `FeatureForceEnabled` | Successful operator force-enable per A1.9. |
| `FeatureForceRevoked` | Operator revoked a previously-recorded force-enable. |
| `FeatureForceEnableRejected` | Force-enable rejected (`NotOverridable` dimension). |
| `MissionEnvelopeChangeBroadcast` | Coordinator dispatched a coalesced change to observers. |
| `MissionEnvelopeObserverOverflow` | Pending-change queue overflowed (oldest-first eviction). |
| `FeatureVerdictSurfaced` | A feature verdict was surfaced to a UX consumer (telemetry per A1.12). |

`MissionSpaceAuditPayloads` is the canonical body factory — alphabetized keys, opaque to the substrate, mirroring the `VersionVectorAuditPayloads` / `MigrationAuditPayloads` conventions.

Audit emission is opt-in: pass an `IAuditTrail` + `IOperationSigner` + `TenantId` to the `Default*Surface` constructors (or use the audit-enabled DI overload). Without them, the substrate runs but no records fire — the W#32 both-or-neither contract.

## API at a glance

```csharp
// Bootstrap (audit-disabled — test/bootstrap). Wires the 10 default
// IDimensionProbe<X> implementations + DefaultFeatureForceEnableSurface.
services.AddInMemoryMissionSpace();

// Bootstrap (audit-enabled — production; both IAuditTrail + IOperationSigner
// must already be registered, per the W#32 both-or-neither pattern).
services.AddInMemoryMissionSpace(currentTenantId);

// Hosts compose their own envelope-factory delegate from the registered
// probes and hand it to DefaultMissionEnvelopeProvider:
var provider = new DefaultMissionEnvelopeProvider(async ct =>
{
    var hw = sp.GetRequiredService<IDimensionProbe<HardwareCapabilities>>();
    var rt = sp.GetRequiredService<IDimensionProbe<RuntimeCapabilities>>();
    // … 8 more …
    return new MissionEnvelope { Hardware = await hw.ProbeAsync(ct), … };
});
```

## Phase 1 scope (this package)

- Substrate types, contracts, and reference implementations for the 10-dimension envelope.
- 10 default `IDimensionProbe<TDimension>` implementations.
- `IFeatureBespokeProbe<TBespokeSignal>` extension-surface contract.
- `DefaultMissionEnvelopeProvider` (single-flight + 100ms coalescing + 100-pending overflow).
- `DefaultFeatureForceEnableSurface` (per-dimension policy enforcement + audit emission).
- `MissionSpaceAuditPayloads` body factory.
- 8 new `AuditEventType` discriminators in `Sunfish.Kernel.Audit`.
- `AddInMemoryMissionSpace()` DI extension (audit-disabled + audit-enabled overloads).

Subsequent phases (per ADR 0062 follow-ons): per-feature gate authoring across the cohort (separate workstreams per feature family); UX surfaces for `DegradationKind` (out of substrate scope per A1.2); cross-package dimension-source wiring once W#34 / W#35 / W#37 / W#39 sources stabilize.

## Cohort lesson — substrate-only Phase 1 means substrate-only

This package ships the substrate. It does **not** ship feature gates, regulatory rule content, or UX surfaces — those compose downstream. Specifically:

- W#39 (Foundation.MissionSpace.Regulatory) carries reader-caution + per-regime rule content (legal-counsel-engagement-gated for Phase 3+).
- ADR 0063's install-UX renderer (`MinimumSpec` consumer) is a separate workstream.
- Per-feature `IFeatureGate<TFeature>` implementations live in the consuming package (e.g., `blocks-leases` for lease-related gates).

Substrate-only deployments compose, evaluate, and audit; they do not become regulatory-compliant or UX-complete by virtue of the substrate alone.
