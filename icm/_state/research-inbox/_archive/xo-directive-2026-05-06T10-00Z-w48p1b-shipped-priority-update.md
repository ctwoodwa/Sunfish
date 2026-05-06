---
type: resumed
workstream-or-chapter: W#48 Phase 1b shipped — all Phase 1 complete; Phase 2 unblocked
last-pr: "#660 (W#48 Phase 1b IIntegrationAtlasProvider + Phase 2 addendum updated)"
---

W#48 Phase 1b merged PR #660 (2026-05-06). All Phase 1 / Phase 1.5 / Phase 1b contracts now
on origin/main. `AddSunfishIntegrationAtlas()` ships contracts+stores only per addendum ruling.
DIVERGENCE: `IssueXxxAsync` returns `Task<StandingOrderId>` — see addendum §IssueXxxAsync note.

**Priority order for COB (2026-05-06T10:00Z):**

1. **W#57** (~1 PR, ~2-3h) — `StandingOrderAppliedEvent` + `IStandingOrderEventStream`.
   Clears W#53 Phase 2 H8 + W#46 Phase 3 halt-C (DefaultPermissionResolver cache invalidation).
   Hand-off: `wayfinder-adr-0065-a1-event-stream-handoff.md`.

2. **W#53 Phase 2** (~12-19h, 3-4 PRs) — 6 canonical Helm widgets + Blazor + React adapters.
   If W#57 not yet landed: use H8 periodic-fallback workaround per hand-off.
   Hand-off: `helm-identity-atlas-stage06-handoff.md` §Phase 2.

3. **W#51 Phase 2** (~4-5h, 1 PR) — DefaultQuarterdeckDataProvider + permission pre-resolution.
   Hand-off: `quarterdeck-entry-point-stage06-handoff.md` §Phase 2.

4. **W#52 Phase 2** (~6-8h, 1-2 PRs) — DefaultAlertRouter + DefaultThreatTriggerService.
   Read `tactical-p2-system-principal-authority-addendum.md` FIRST (ISystemPrincipalProvider
   identity check — NOT IPermissionResolver for IssueEmergencyStandingOrder).
   Hand-off: `tactical-anomaly-detection-stage06-handoff.md` §Phase 2.

5. **W#48 Phase 2** (~8-10h, 2 PRs) — DefaultIntegrationAtlasProvider in `blocks-integrations`.
   Read `atlas-integration-config-p2-blocks-integrations-addendum.md` before starting.
   IssueXxxAsync must return `StandingOrderId` (not `StandingOrder`) — see addendum.
   Hand-off: `atlas-integration-config-stage06-handoff.md` §Phase 2 + addendum.
