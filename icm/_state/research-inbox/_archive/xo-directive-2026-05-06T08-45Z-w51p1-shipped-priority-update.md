---
type: resumed
workstream-or-chapter: W#51 Phase 1 shipped + W#48 Phase 2 cycle resolved
last-pr: "#655 (W#48 Phase 2 blocks-integrations addendum + W#51 gate-sweep)"
---

W#51 Phase 1 (`foundation-quarterdeck` substrate + `IQuarterdeckAlertSource`) merged PR #651.
W#48 Phase 2 cycle resolution: `DefaultIntegrationAtlasProvider` goes in new
`packages/blocks-integrations/` — NOT ui-core. Addendum at
`atlas-integration-config-p2-blocks-integrations-addendum.md` (PR #655 merged).

**IMPORTANT — W#48 Phase 1b DI amendment (read before building Phase 1b):**
`AddSunfishIntegrationAtlas()` in ui-core registers contracts + InMemoryValidationStatusStore
ONLY. Do NOT register DefaultIntegrationAtlasProvider there. See main hand-off Phase 1b
`ServiceCollectionExtensions.cs` and the Phase 2 addendum for details.

**Priority order for COB (all gates verified 2026-05-06T08:45Z):**

1. **W#48 Phase 1b** (~1 PR, ~3-4h) — IIntegrationAtlasProvider + IntegrationAtlasView +
   ActiveProviderSnapshot + IDecryptCapabilityProvider (in foundation/Crypto/ not ui-core) +
   AddSunfishIntegrationAtlas() (contracts+stores only) + 4 AuditEventType constants.
   DI companion: register TenantKeyDecryptCapabilityProvider in AddSunfishRecoveryCoordinator().
   Pre-merge council mandatory. Hand-off: `atlas-integration-config-stage06-handoff.md` §Phase 1.

2. **W#57** (~1 PR, ~2-3h) — StandingOrderAppliedEvent + IStandingOrderEventStream.
   Clears W#53 Phase 2 H8 halt (removes periodic-fallback workaround from W#53 P2 scope).
   Can overlap with W#48 P1b if token budget permits.
   Hand-off: `icm/_state/handoffs/` — check for `adr-0065-a1-*` or `w57-*` file.

3. **W#53 Phase 2** (~12-19h, 3-4 PRs) — 6 canonical Helm widgets (IdentityGlance /
   PermissionOverview / ActiveStandingOrders / RecoveryContact / KeyFingerprint / SecurityPosture)
   + Blazor + React adapters + WCAG contract tests.
   If W#57 not yet built: use H8 periodic-fallback workaround (leave TODO comment per hand-off §H8).
   Hand-off: `helm-identity-atlas-stage06-handoff.md` §Phase 2.

4. **W#51 Phase 2** (~1 PR, ~4-5h) — DefaultQuarterdeckDataProvider + security wiring
   (§5.2 tenant binding + §5.3 SourceName uniqueness + permission pre-resolution).
   Pre-merge council + security-engineering subagent mandatory.
   Hand-off: `quarterdeck-entry-point-stage06-handoff.md` §Phase 2.

5. **W#52 Phase 1** (~1 PR, ~3-4h) — foundation-tactical substrate.
   All gates cleared (W#46 P1 ✓ P3 ✓ + W#42 ✓ + W#51 P1 ✓).
   Hand-off: `tactical-anomaly-detection-stage06-handoff.md` §Phase 1.

**Also available (any time, ~2-3h each):** W#44 (ExtensionFields), W#47 (Anchor MAUI
ISystemRequirementsRenderer), W#56 (Bridge React ISystemRequirementsRenderer), W#23 P5 (pairing flow).
