---
type: resumed
workstream-or-chapter: W#53 Phase 2 cascade in flight; 5+ priority queue refreshed
last-pr: "#667 (W#53 Phase 2 PR 2c-blazor council amendments)"
---

**What landed since 06:15Z directive:**

- PR #664 — `chore(icm)` W#57 gate-sweep: W#57 built + W#53/W#46 halt-C
  cleared (IStandingOrderEventStream available for subscribe-before-load).
- PR #665 — `feat(ui-core)` W#53 Phase 2 PR 2b: QuickTogglesWidget
  (ActionStack) + RecentStandingOrdersWidget (ActivityFeed) on origin/main.
- PR #666 — `feat(ui-adapters-blazor)` W#53 Phase 2 PR 2c-blazor:
  HelmRenderer + WCAG tests landed.
- PR #667 — `chore(ui-adapters-blazor)` post-merge council amendments for
  PR 2c-blazor.
- PRs #663/#665/#666/#667 together complete the Blazor side of W#53 Phase 2;
  React adapter sibling (PR 2d-react) remains.

**Refreshed priority order for COB:**

1. **W#53 Phase 2 React adapter** (~3-4h, 1 PR) — `HelmRenderer` in
   `packages/ui-adapters-react/`; parity with the Blazor renderer per
   ADR 0014; WCAG/a11y subagent mandatory. Hand-off:
   `helm-identity-atlas-stage06-handoff.md` §Phase 2 (PR 2d-react section).

2. **W#46 Phase 2b** (~3h, 1 PR) — design-token codegen: `tokens.json`
   → C# const records + CSS custom properties + Markdown reference table
   + WCAG 1.4.3/1.4.11 contrast CI gate + CVD ΔE2000 audit.
   Design-engineering subagent mandatory. Hand-off:
   `shared-design-system-stage06-handoff.md` §Phase 2b.

3. **W#51 Phase 2** (~4-5h, 1 PR) — `DefaultQuarterdeckDataProvider` +
   permission pre-resolution + DI wiring. Security-engineering subagent
   mandatory. Hand-off: `quarterdeck-entry-point-stage06-handoff.md`
   §Phase 2.

4. **W#52 Phase 2** (~6-8h, 1-2 PRs) — `DefaultAlertRouter` +
   `DefaultThreatTriggerService`. READ
   `tactical-p2-system-principal-authority-addendum.md` FIRST
   (ShipRole.System gap ruling). Security-engineering subagent mandatory.
   Hand-off: `tactical-anomaly-detection-stage06-handoff.md` §Phase 2.

5. **W#48 Phase 2** (~8-10h, 2 PRs) — `DefaultIntegrationAtlasProvider`
   in `blocks-integrations`. READ
   `atlas-integration-config-p2-blocks-integrations-addendum.md` FIRST
   (IssueXxxAsync returns `Task<StandingOrderId>`, not `Task<StandingOrder>`).
   Hand-off: `atlas-integration-config-stage06-handoff.md` §Phase 2 +
   addendum.

6. **W#54 Phase 2** (~4-5h, 1 PR) — `DefaultSickBayDataProvider` +
   IDC role + 4-eyes Medevac workflow. Phase 1 contracts on origin/main
   (PR #628). Hand-off: `sick-bay-stage06-handoff.md` §Phase 2.
   **HALT: blocked on ADR 0082 Status: Accepted** (currently Proposed).

7. **W#55 Phase 2** (~4-5h, 1 PR) — `DefaultShipsOfficeDataProvider` +
   Scribe role + reference content editor. Phase 1 contracts on origin/main
   (PR #624). Hand-off: `ships-office-stage06-handoff.md` §Phase 2.
   **HALT: blocked on ADR 0083 Status: Accepted** (currently Proposed).

8. **W#50 Phase 2** (~5-6h, 1-2 PRs) — observability provider +
   telemetry aggregation. Phase 1 contracts on origin/main (PR #626).
   Hand-off: `engine-room-observability-stage06-handoff.md` §Phase 2.

9. **W#1 WS-A Phase 1** (~3-4h, 1 PR) — `TenantId.System` sentinel +
   `TenantSelection` discriminated union + `IMayHaveTenant` [Obsolete].
   Hand-off pre-authored (PR #637): `tenant-selection-wsa-stage06-handoff.md`.
   **HALT: blocked on ADR 0084 Status: Accepted** (currently Proposed).

10. **W#1 WS-B Phase 1** (~3-4h, 1 PR) — `TenantSelection?` migration
    across all query surfaces. Hand-off pre-authored (PR #637):
    `tenant-selection-wsb-stage06-handoff.md`.
    **HALT: blocked on ADR 0084 + ADR 0085 Status: Accepted AND WS-A built**.

11. **W#23 P6 iOS home screen + queue-status UX** (~4h; iOS toolchain) —
    independent line; better in a fresh iOS-toolchain session.
    Hand-off: main `helm-identity-atlas-stage06-handoff.md` §Phase 6
    (no separate addendum). Gated on P5 pairing-flow being on main
    (PR #620 merged).

**Halt-pending CO accept** (unblocks items 6/7/9/10):

- ADR 0082 Sick Bay → flip `status: Proposed` → `status: Accepted` to
  unblock W#54 Phase 2.
- ADR 0083 Ship's Office → same flip to unblock W#55 Phase 2.
- ADR 0084 TenantSelection sentinel → same flip to unblock W#1 WS-A.
- ADR 0085 TenantSelection query → flip + wait for WS-A built to unblock
  W#1 WS-B.

**Process improvements landed today (not listed in old directive):**
PR #585 (per-workstream files migration) / PR #588 (workflow file move) /
PR #607 (workstream-number pre-flight check) / PR #608 (ADR 0077 Status
flip) / PR #644 (concern enum sweep) / PR #651 (W#51 P1 — foundation-
quarterdeck) / PR #658 (W#52 P1 — foundation-tactical) / PR #660 (W#48
P1b — IIntegrationAtlasProvider) / PR #662 (W#57 — IStandingOrderEventStream).
