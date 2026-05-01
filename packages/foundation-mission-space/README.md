# Sunfish.Foundation.MissionSpace

Foundation-tier substrate for the runtime mission-envelope negotiation protocol per [ADR 0062 + A1](../../docs/adrs/0062-mission-space-negotiation-protocol.md). **Canonical-load-bearing for the W#33 §7.2 cohort** — every dimension probe, every feature gate, every install-UX surface, every Bridge subscription event handler ultimately surfaces through `IMissionEnvelopeProvider` + `IFeatureGate<TFeature>`.

## Phase 1 scope (this slice)

- 16 type signatures: 7 enums + 9 records + 4 interfaces.
- `MissionEnvelope` (10-dimension snapshot) + `EnvelopeHash` SHA-256 self-derivation.
- 10 dimension records (Hardware / User / Regulatory / Runtime / FormFactor / Edition / Network / TrustAnchor / SyncState / VersionVector); cross-package wraps for W#34 `VersionVector`, W#35 `FormFactorProfile`, W#37 `SyncState`.
- `FeatureVerdict` + `IFeatureGate<TFeature>` contract; `FeatureAvailabilityState` (3-value) + `DegradationKind` (5-value) + `LocalizedString`.
- `IDimensionProbe<TDimension>` + `IFeatureBespokeProbe<TBespokeSignal>` extension surface.
- `IMissionEnvelopeProvider` + `IMissionEnvelopeObserver` coordinator + observer contracts (Phase 2 ships the implementation).
- `IFeatureForceEnableSurface` + `ForceEnableRecord` + `FeatureForceEnableRequest` + `ForceEnableNotPermittedException`; `ForceEnablePolicy` 3-value enum.
- 9 new `AuditEventType` constants in `Sunfish.Kernel.Audit`.
- Canonical-JSON wire format (camelCase + `JsonStringEnumConverter`) per ADR 0028-A7.8.

## Subsequent phases

- **P2** — `DefaultMissionEnvelopeProvider` (single-flight + per-cost-class timeout + observer fanout per A1.4); cache invalidation per A1.7.
- **P3** — 5-value `DegradationKind` taxonomy verdicts; per-dimension `ForceEnablePolicy` enforcement; `IFeatureForceEnableSurface` + `DefaultFeatureForceEnableSurface`.
- **P4** — 10 default `IDimensionProbe<TDimension>` implementations + extension-surface example.
- **P5** — `MissionSpaceAuditPayloads` factory + DI extension + apps/docs page + ledger flip.

## Cohort discipline

Mirrors W#34 + W#35 + W#36 cohort patterns: PascalCase enum literals + camelCase property names + `JsonStringEnumConverter` for enums + alphabetized audit-payload keys + W#32 both-or-neither audit overload (lands in P5). Phase 1 ships type signatures only — substrate behavior wires across P2–P5.
