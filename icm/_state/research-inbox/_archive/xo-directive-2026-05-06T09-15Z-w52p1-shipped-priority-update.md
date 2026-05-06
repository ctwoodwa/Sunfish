---
type: resumed
workstream-or-chapter: W#52 Phase 1 shipped — ShipRole.System gap resolved
last-pr: "#658 (W#52 Phase 1 foundation-tactical substrate)"
---

W#52 Phase 1 merged PR #658 (2026-05-06T09:04). `foundation-tactical` on origin/main.
ShipRole.System gap RESOLVED: addendum at `tactical-p2-system-principal-authority-addendum.md`.
`IssueEmergencyStandingOrder` uses identity check via ISystemPrincipalProvider (NOT IPermissionResolver).

**Priority order for COB (2026-05-06T09:15Z):**

1. **W#48 Phase 1b** (~1 PR, ~3-4h) — IIntegrationAtlasProvider + IntegrationAtlasView +
   ActiveProviderSnapshot + IDecryptCapabilityProvider + AddSunfishIntegrationAtlas() (contracts+stores
   only; NOT DefaultIntegrationAtlasProvider) + 4 AuditEventType constants + ContractSurfaceTests.
   DI amendment: AddSunfishIntegrationAtlas() must NOT register DefaultIntegrationAtlasProvider.
   Companion: register TenantKeyDecryptCapabilityProvider in AddSunfishRecoveryCoordinator().
   Hand-off: `atlas-integration-config-stage06-handoff.md` §Phase 1 + p2 addendum for DI change.
   Pre-merge council mandatory.

2. **W#57** (~1 PR, ~2-3h) — StandingOrderAppliedEvent + IStandingOrderEventStream.
   Clears W#53 Phase 2 H8 + W#46 Phase 3 halt-C (DefaultPermissionResolver cache invalidation).
   Hand-off: `wayfinder-adr-0065-a1-event-stream-handoff.md`.

3. **W#53 Phase 2** (~12-19h, 3-4 PRs) — 6 canonical Helm widgets + Blazor + React adapters.
   If W#57 not yet built: use H8 periodic-fallback workaround per hand-off.
   Hand-off: `helm-identity-atlas-stage06-handoff.md` §Phase 2.

4. **W#51 Phase 2** (~4-5h, 1 PR) — DefaultQuarterdeckDataProvider + security wiring.
   Hand-off: `quarterdeck-entry-point-stage06-handoff.md` §Phase 2.

5. **W#52 Phase 2** (~6-8h, 1-2 PRs) — DefaultAlertRouter + DefaultThreatTriggerService +
   alert routing + dedup. Read `tactical-p2-system-principal-authority-addendum.md` FIRST.
   Hand-off: `tactical-anomaly-detection-stage06-handoff.md` §Phase 2.
