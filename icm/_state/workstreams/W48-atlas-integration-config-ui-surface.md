---
sort_order: 50
number: 48
slug: atlas-integration-config-ui-surface
title: "**Atlas Integration-Config UI Surface** (ADR 0067; W#34 follow-on; `sunfish-feature-change` pipeline)"
status: "built"
status_cell: "`built` — all 5 phases shipped 2026-05-14 (PRs #640 through #834); Integration Atlas Anchor + Bridge Blazor + React parity; docs + kitchen-sink; PR #834 ledger flip merged; pipeline closed"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/atlas-integration-config-stage06-handoff.md` + `icm/_state/handoffs/atlas-integration-config-p2-blocks-integrations-addendum.md` (XO ruling: Phase 2 impl → `blocks-integrations` package) + `docs/adrs/0067-atlas-integration-config-surface.md` (PR #539 merged)"
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

**Phase 1b SHIPPED 2026-05-06 PR #660** — `IIntegrationAtlasProvider` +
`IntegrationAtlasView` + `ActiveProviderSnapshot` + `IDecryptCapabilityProvider` +
`AddSunfishIntegrationAtlas()` + 4 `AuditEventType` constants + `ContractSurfaceTests`
on origin/main. DIVERGENCE: `IIntegrationAtlasProvider.IssueXxxAsync` methods return
`Task<StandingOrderId>` (NOT `Task<StandingOrder>`) — second cycle
`ui-core → foundation-wayfinder → kernel-crdt → ui-core` prevents returning the full
`StandingOrder` aggregate. `DefaultIntegrationAtlasProvider` in Phase 2 must extract
the `StandingOrderId` from `IStandingOrderIssuer.IssueAsync` and return it directly.
**Phase 2 NOW UNBLOCKED** — read `atlas-integration-config-p2-blocks-integrations-addendum.md`
before starting.

**Phase 2 CYCLE RESOLVED — XO ruling 2026-05-06:** `DefaultIntegrationAtlasProvider`
goes in new `packages/blocks-integrations/` package (NOT `ui-core`). Full
architectural spec at `atlas-integration-config-p2-blocks-integrations-addendum.md`.
COB MUST read addendum before starting Phase 2.

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
