---
sort_order: 43
number: 40
slug: foundation-missionspace-phase-1-substrate
title: "Foundation.MissionSpace Phase 1 substrate (ADR 0062 + A1 contract surface)"
status: "built"
status_cell: "`built` (5/5 phases shipped 2026-05-01: P1 #461 scaffold + 16 types + 8 audit constants; P2 #463 coordinator + observer fanout + 100ms coalescing + 100-pending overflow; P3 #464 force-enable surface + per-dimension policy resolver; P4 #465 10 default `IDimensionProbe<T>` impls + bespoke probe surface; P5 ledger flip + DI extension + `MissionSpaceAuditPayloads` factory + `apps/docs/foundation/mission-space/overview.md`)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/foundation-mission-space-stage06-handoff.md` + `docs/adrs/0062-mission-space-negotiation-protocol.md` (post-A1; landed via PR #406)"
---

## Notes

**Built 2026-05-01 — 107 / 107 tests green; halt-conditions clean.** ADR 0062 + A1 Accepted on origin/main; W#33 §7.2 closed (5/5 substrate ADRs landed). **THIS IS THE CANONICAL-LOAD-BEARING SUBSTRATE for the entire cohort** — every dimension, every gate, every install-UX surface, every regulatory rule, every Bridge subscription event handler ultimately surfaces through `IMissionEnvelopeProvider` + `IFeatureGate<TFeature>`. ADR 0063, ADR 0064, all 4 sibling amendments (ADR 0028-A9 / ADR 0036-A1 / ADR 0007-A1 / ADR 0031-A1), and W#36's Anchor-side handler all reference types in this substrate. Phase 1 ships `Sunfish.Foundation.MissionSpace` package + ~16 types (`MissionEnvelope` + `IMissionEnvelopeProvider` + `IFeatureGate<TFeature>` + `IDimensionProbe<TDimension>` + 5-value `DegradationKind` taxonomy + `ProbeStatus` 5-value enum + `EnvelopeChangeSeverity` 4 levels including `ProbeUnreliable` + `LocalizedString` + `ForceEnablePolicy` taxonomy + 10 dimension records consuming W#34/W#35/W#36/ADR-0036-A1/ADR-0009 sibling types) + 10 default `IDimensionProbe<TDimension>` impls + `IFeatureBespokeProbe<TBespokeSignal>` extension surface + `ICapabilityForceEnableSurface` operator-only override + 9 new `AuditEventType` constants (post-A1.2 IFeature/IFeatureGate rename; post-A1.4 observer-overflow; post-A1.12 verdict-surfaced) + DI extension + apps/docs page. **7 halt-conditions named** (cross-package dimension dependencies on stuck PRs; envelopeHash sha256 platform-determinism; observer fanout 100ms coalescing; DegradationKind UX surface NOT in Phase 1 scope; force-enable composition with W#22/W#28/W#36; envelope immutability; cohort stuck-PR coupling on W#30 P6+/W#37 — interface-stub fallback for Phase 4 probes). Substrate-only; consumer wiring (per-feature gate authoring across cohort) is separate workstreams. Closes ADR 0062 substrate gap; downstream consumers (W#39 Regulatory + W#41+ install-UX renderer) compose.
