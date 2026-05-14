---
sort_order: 50
number: 48
slug: atlas-integration-config-ui-surface
title: "**Atlas Integration-Config UI Surface** (ADR 0067; W#34 follow-on; `sunfish-feature-change` pipeline)"
status: "built"
status_cell: "`built` (ADR 0067 Accepted; all 5 phases shipped: P1a PR #640 + P1.5 PR #641/#642 + P1b PR #660 + P2 PR #829 + P3b PR #831 + P4 PR #832 + P5 ledger-flip PR — see Notes for full phase history)"
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
`StandingOrder` aggregate.

**Phase 2 SHIPPED 2026-05-13 PR #829** — `packages/blocks-integrations/` package
(cycle-safe tier per XO ruling); `DefaultIntegrationAtlasProvider` + `InMemoryIntegrationAtlasProvider`
+ `IntegrationAuditPayloads` + `AddSunfishIntegrationAtlasDefaults()` + `SUNFISH_INTEGRATION_AUDIT001`
analyzer + 25 tests. Security council CONDITIONAL-PASS; decrypt-fail-closed fix applied.

**Phase 3b SHIPPED 2026-05-13 PR #831** — `HeadscaleIntegrationSchemaProvider` +
`HeadscaleIntegrationValidator` (in `providers-mesh-headscale`) + `RecaptchaV3IntegrationSchemaProvider`
+ `RecaptchaV3IntegrationValidator` (in `providers-recaptcha`) + DI extensions;
8 council amendments applied (B1/B2/M1-M5/m1); 37 headscale + 30 reCAPTCHA tests.

**Phase 4 SHIPPED 2026-05-14 PR #832** — Anchor Blazor: `AtlasIntegrationConfigPage` +
`AtlasIntegrationConfig` + `AtlasIntegrationCategoryPanel` + `AtlasCredentialField` +
`AtlasEmailRoutingPanel` + `AnchorIntegrationAtlasContext` + MauiProgram DI wiring;
Bridge Blazor: 3 parity components + `BridgeIntegrationAtlasContext` (public, in
`Sunfish.Bridge.Features.Integrations`) + Program.cs DI wiring (AddScoped per-circuit);
React parity: `AtlasIntegrationConfig.tsx` + `AtlasIntegrationCategoryPanel.tsx` +
`AtlasCredentialField.tsx` + `contracts/Integrations.ts` + 20 tests (100% pass);
A11y: 19 structural WCAG 2.2 AA assertions (SCs 1.4.1/3.3.2/3.3.7/3.3.8/4.1.2/4.1.3).
H9 WCAG/a11y council: PASS.

**Phase 5 SHIPPED 2026-05-14** — `apps/docs/blocks/integrations/overview.md` + `toc.yml`;
kitchen-sink integration atlas demo page (`InMemoryIntegrationAtlasProvider` seeded with
Stripe Payments + Twilio Messaging); `_shared/engineering/coding-standards.md` cross-link;
ledger flip to `built`. Pipeline closed.

Key architectural decisions:
- `BridgeIntegrationAtlasContext` is `public sealed` in `Sunfish.Bridge.Features.Integrations`
  (not Bridge.Client) — cross-assembly DI visibility requirement.
- Both accelerators use `InMemoryIntegrationAtlasProvider` factory pattern (bypasses
  `AddSunfishIntegrationAtlas()` which requires `IDecryptCapabilityProvider` not yet wired).
- Bridge uses `AddScoped<IIntegrationAtlasContext>` (per-circuit); Anchor uses `AddSingleton`.
- `IIntegrationAtlasProvider.IssueXxxAsync` returns `Task<StandingOrderId>` not `Task<StandingOrder>`
  due to `ui-core → foundation-wayfinder → kernel-crdt → ui-core` cycle constraint.
