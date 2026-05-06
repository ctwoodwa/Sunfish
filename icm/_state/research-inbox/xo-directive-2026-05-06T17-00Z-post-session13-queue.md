---
type: directive
workstream-or-chapter: multi-workstream — post-session-13 priority queue
last-pr: "694 (W#50 Phase 2 + W#56 directive gap-fill)"
---

Updated 2026-05-06 (session 15): added W#50 Phase 2 + W#56 to "also ready"; fixed W#47
hand-off filename. Items 1-9 unchanged.

## COB Priority Queue (post-session-15)

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

9. **W#1 WS-B** — `TenantSelection` migration: `AuditQuery.Tenant` + `EntityQuery.Tenant` +
   `DataExport.TenantId` from `TenantId?` → `TenantSelection?`. Hand-off at
   `tenant-selection-wsb-stage06-handoff.md`. **NOW READY** (security follow-up PR #692 ✓).
   (~4-6h; breaking api-change pipeline)

**Also ready (no specific ordering constraint):**
- **W#50 Phase 2** — `DefaultEngineRoomDataProvider` + `DefaultEngineRoomCommandService` +
  OTel wiring. Both gates cleared: W#46 Phase 1 ✓ + W#49 Phase 1 ✓.
  Security-engineering subagent mandatory. (~4-5h / 1 PR)
  Hand-off at `engine-room-observability-stage06-handoff.md` §Phase 2.
- **W#51 Phase 3a** — `blocks-quarterdeck` top-deck panels (WatchStatusPanel + alert ticker).
  Hand-off at `quarterdeck-entry-point-stage06-handoff.md` §Phase 3a. Both gates cleared:
  W#49 Phase 1 ✓ + W#46 Phase 3 ✓. WCAG/a11y subagent mandatory.
- **W#44** — ExtensionFields feature-evaluation hook. Hand-off at
  `extension-fields-feature-gate-stage06-handoff.md`.
- **W#47** — Anchor MAUI ISystemRequirementsRenderer. Hand-off at
  `foundation-wayfinder-anchor-maui-renderer-stage06-handoff.md`.
- **W#56** — Bridge React ISystemRequirementsRenderer (TypeScript projection contract +
  Bridge JSON endpoint + React renderer). Hand-off at
  `foundation-wayfinder-bridge-react-renderer-stage06-handoff.md`. (~14-19h / 5 PRs;
  WCAG/a11y subagent mandatory before every UI-bearing phase)

Cap concurrent PRs at 3.
