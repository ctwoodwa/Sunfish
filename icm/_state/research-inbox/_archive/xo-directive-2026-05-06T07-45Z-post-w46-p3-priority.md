---
type: resumed
workstream-or-chapter: post-W#46-P3 priority sweep — W#48/W#51/W#52/W#53 all unblocked
last-pr: "#646 (W#23.2 equipment-photo hand-off + inbox cleanup)"
---

W#46 Phase 3 (ILiveAnnouncer / IFocusTrap / UICore.Primitives + FirstAid + Conformance)
merged PR #645. This clears the final gate for W#51 Phase 3a + W#52 Phase 3a.

**Priority order for COB (all gates verified on origin/main as of 2026-05-06T07:45Z):**

1. **W#48 Phase 1b** — IIntegrationAtlasProvider + IntegrationAtlasView + ActiveProviderSnapshot
   + IDecryptCapabilityProvider + AddSunfishIntegrationAtlas() + 4 AuditEventType constants
   + ContractSurfaceTests.NoMethodReturnsDecryptedBytes.
   Hand-off: `atlas-integration-config-stage06-handoff.md` §Phase 1 +
   `atlas-integration-config-p15-cycle-break-handoff.md` §Phase 1b table.
   Pre-merge council mandatory (new public type surface).

2. **W#53 Phase 2** — 6 canonical Helm widgets (IdentityGlance / PermissionOverview /
   ActiveStandingOrders / RecoveryContact / KeyFingerprint / SecurityPosture) +
   Blazor + React adapters + WCAG contract tests.
   Hand-off: `helm-identity-atlas-stage06-handoff.md` §Phase 2.
   ~12-19h / 3-4 PRs.

3. **W#51 Phase 3a** — `blocks-quarterdeck`: AlertTicker + WatchStatusPanel + VantagePoint
   Blazor components (ILiveAnnouncer + IFocusTrap now available).
   Hand-off: `quarterdeck-entry-point-stage06-handoff.md` §Phase 3a.

4. **W#52 Phase 3a** — `blocks-tactical`: Sonar Room UI + Lookout panel
   (ILiveAnnouncer + IFocusTrap now available).
   Hand-off: `tactical-anomaly-detection-stage06-handoff.md` §Phase 3a.

**Do not block W#48 P1b on W#53 P2** — they are independent. Run concurrently if token
budget permits; otherwise W#48 P1b first (smaller: 1 PR, ~3-4h).
