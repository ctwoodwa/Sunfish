---
sort_order: 50
number: 48
slug: atlas-integration-config-ui-surface
title: "**Atlas Integration-Config UI Surface** (ADR 0067; W#34 follow-on; `sunfish-feature-change` pipeline)"
status: "building"
status_cell: "`building` (Phase 1a shipped 2026-05-06 PR #640; P1.5 PR1 StandingOrderId shipped PR #641; P1.5 PR2 IDecryptCapability shipped PR #642; **Phase 1b NOW UNBLOCKED** — IIntegrationAtlasProvider + IntegrationAtlasView + IDecryptCapabilityProvider + AddSunfishIntegrationAtlas(); Phases 2-5 pending)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/atlas-integration-config-stage06-handoff.md` + `docs/adrs/0067-atlas-integration-config-surface.md` (PR #539 merged) + `packages/ui-core/Wayfinder/IAtlasProvider.cs` (W#53 P1a — `IAtlasProvider<T>` substrate landed)"
---

## Notes

**Phase 1a shipped 2026-05-06 PR #640.** 16 files in
`packages/ui-core/Wayfinder/Integrations/` — enums + CredentialFieldSpec +
IntegrationProviderSchema + IIntegrationAtlasContext + IIntegrationProviderValidator
+ ICustomIntegrationRenderer + IValidationStatusStore + IIntegrationSchemaProvider
+ IntegrationCapabilityPurposes + IntegrationAtlasContractTests.cs.

**Phase 1.5 COMPLETE 2026-05-06** — both cycle-break moves shipped:
PR #641 `StandingOrderId` + `AuditRecordId` → `foundation/Assets/Common/`;
PR #642 `IDecryptCapability` → `foundation/Crypto/`.

**Phase 1b NOW UNBLOCKED** — COB can build `IIntegrationAtlasProvider` +
`IntegrationAtlasView` + `ActiveProviderSnapshot` + `IDecryptCapabilityProvider` +
`AddSunfishIntegrationAtlas()` + 4 `AuditEventType` constants + full
`ContractSurfaceTests`. Hand-off: `atlas-integration-config-stage06-handoff.md`
§Phase 1 + `atlas-integration-config-p15-cycle-break-handoff.md` §Phase 1b.
Pre-merge council mandatory.

**`IAtlasProvider<TView>` is invariant** (W#53 P1a council resolution —
hand-off cited `out TView` but C# CS1961 rejects on `Task<T>` returns).
Concrete W#48 `IIntegrationAtlasProvider` derives directly from
`IAtlasProvider<IntegrationAtlasView>` without covariant downcast.

Key new types: `IIntegrationAtlasProvider` + `IIntegrationAtlasContext`
+ `IntegrationProviderSchema` + `IIntegrationSchemaProvider` +
`IIntegrationProviderValidator` + `IValidationStatusStore` +
`IDecryptCapabilityProvider` + `IntegrationCapabilityPurposes`. No new
package (additive to `packages/ui-core/Wayfinder/Integrations/`).

**Phase 1 restructured (2026-05-06 per COB question #636):** Three
dependency cycles block some Phase 1 types. New sequence:
- **Phase 1a** (ship now, cycle-safe): enums + value types + constants +
  `IIntegrationAtlasContext` + `IIntegrationProviderValidator` +
  `ICustomIntegrationRenderer` + `IValidationStatusStore`
- **Phase 1.5** (cycle-break moves): `StandingOrderId` + `AuditRecordId`
  → `foundation/Assets/Common/`; `IDecryptCapability` → `foundation/Crypto/`.
  Hand-off at `icm/_state/handoffs/atlas-integration-config-p15-cycle-break-handoff.md`.
- **Phase 1b** (after Phase 1.5 merged): `IIntegrationAtlasProvider` +
  `IntegrationAtlasView` + `ActiveProviderSnapshot` +
  `IDecryptCapabilityProvider` + `AddSunfishIntegrationAtlas()` +
  4 `AuditEventType` constants + `ContractSurfaceTests`.

5 build phases: P1a/1.5/1b → P2 reference impl + audit +
SUNFISH_INTEGRATION_AUDIT001 analyzer; P3a/3b; P4 Anchor+Bridge; P5 docs.
~26-38h / ~7-10 PRs.
