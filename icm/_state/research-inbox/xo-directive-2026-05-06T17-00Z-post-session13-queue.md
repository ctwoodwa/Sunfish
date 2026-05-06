---
type: directive
workstream-or-chapter: multi-workstream — post-session-13 priority queue
last-pr: "690 (ledger sweep — W#1 WS-A building + W#51 Phase 2 complete)"
---

Replaces `xo-directive-2026-05-06T16-30Z-post-session12-corrected.md` (both archived).
Items 6 (W#51 Phase 2b) and 9 (W#1 WS-A Phase 1) from that directive are **DONE** — PRs
#689 and #688 merged. W#1 security follow-up (6 MFs on origin/main) is now item 0.

## COB Priority Queue (post-session-13)

**0. [URGENT] W#1 WS-A security follow-up — must ship before WS-B or any other new work.**
6 must-fix security items are on origin/main from PR #688. Full code-level spec in
`xo-directive-2026-05-06T14-15Z-w1-security-followup-before-wsb.md` (keep in inbox; do NOT
archive until this follow-up PR merges). Also in auto-memory `project_workstream_01_adr_0084.md`.
WS-B HELD until follow-up ships.

1. **W#53 Phase 2 PR 2c-react** — TypeScript React adapter renderers for all 6 Helm widgets.
   H9 parity gate MUST be cleared before Phase 2 closes. WCAG/a11y subagent mandatory.
   Hand-off at `helm-identity-atlas-stage06-handoff.md`.

2. **W#46 Phase 2b** — codegen pipeline: `tokens.json` → C# const records + CSS custom
   properties + Markdown reference table + WCAG 1.4.3/1.4.11 contrast CI gate + CVD ΔE2000
   audit. (~3h; design-engineering subagent council mandatory)

3. **W#46 Phase 4** — UNBLOCKED (P1b follow-up shipped PR #680). Blazor + React + MAUI
   Win/Mac adapter implementations + a11y harness extension + CI gates.
   (~8h; WCAG/a11y subagent mandatory)

4. **W#54 Phase 2** — Sick Bay reference impl: `DefaultStretcherBearerPolicy` +
   `DefaultFirstAidSurface` + `NoopKeyRotationScheduler` + IActorPrincipalResolver wiring.
   (~4-5h; security-engineering subagent mandatory pre-merge; H4 `IFieldDecryptor`
   reflection test required)

5. **W#55 Phase 2** — Ship's Office reference impl: `DefaultShipsOfficeDataProvider` +
   `SUNFISH_SHIPSOFFICE_PERM001` Roslyn analyzer. (~6h; security-engineering subagent
   mandatory)

6. **W#52 Phase 2** — `DefaultAlertRouter` + `DefaultThreatTriggerService`. CRITICAL: read
   `tactical-p2-system-principal-authority-addendum.md` first. Security council mandatory.
   (~6-8h)

7. **W#48 Phase 2** — `DefaultIntegrationAtlasProvider` in new `blocks-integrations/`
   package. Read `atlas-integration-config-p2-blocks-integrations-addendum.md` before
   starting. (~8-10h)

8. **W#58 Phase 1** — `AnchorIdentityAtlasSurface` + 5 Anchor Blazor identity pages.
   Hand-off at `identity-atlas-implementations-stage06-handoff.md`.
   **GATED on item 1 closing (H1: W#53 PR 2c-react must merge first).**
   Security-engineering subagent mandatory; READ-ONLY invariant enforced.
   (~10h / 2 PRs)

**After item 0 (security follow-up) ships:**
9. **W#1 WS-B** — `TenantSelection` migration: `AuditQuery.Tenant` + `EntityQuery.Tenant` +
   `DataExport.TenantId` from `TenantId?` → `TenantSelection?`. Hand-off at
   `tenant-selection-wsb-stage06-handoff.md`. (~4-6h; breaking api-change pipeline)

**Also ready (no specific ordering constraint):**
- **W#51 Phase 3a** — `blocks-quarterdeck` top-deck panels (WatchStatusPanel + alert ticker).
  Hand-off at `quarterdeck-entry-point-stage06-handoff.md` §Phase 3a. Both gates cleared:
  W#49 Phase 1 ✓ + W#46 Phase 3 ✓. WCAG/a11y subagent mandatory.
- **W#44** — ExtensionFields feature-evaluation hook. Hand-off at
  `extension-fields-feature-gate-stage06-handoff.md`.
- **W#47** — Anchor MAUI ISystemRequirementsRenderer. Hand-off at
  `system-requirements-renderer-anchor-stage06-handoff.md`.

Cap concurrent PRs at 3.
