---
type: directive
workstream-or-chapter: multi-workstream — post-session-13 priority queue
last-pr: "697 (ADR 0082-A1 + W#54/W#50 Phase 2b rulings)"
---

Updated 2026-05-06 (session 16):
- Item 4 (W#54 Phase 2) DONE — PR #695 merged; Phase 2b follow-on added to "also ready"
  with XO ruling at `xo-ruling-2026-05-06T20-00Z-w54-phase2b-atmosphere-mapping.md`
- W#50 Phase 2a DONE — PR #696 merged (DefaultEngineRoomDataProvider); Phase 2b follow-on
  added to "also ready" with XO ruling at `xo-ruling-2026-05-06T20-00Z-w50-phase2b-command-service.md`
- ADR 0082-A1 shipped (AtmosphereHealth.Unknown + NoopKeyRotationScheduler warning) — this PR
- Renumbered: items 5-9 shift to 4-8

## COB Priority Queue (post-session-16)

1. **W#53 Phase 2 PR 2c-react** — TypeScript React adapter renderers for all 6 Helm widgets.
   H9 parity gate MUST be cleared before Phase 2 closes. WCAG/a11y subagent mandatory.
   Hand-off at `helm-identity-atlas-stage06-handoff.md`.

2. **W#46 Phase 2b** — codegen pipeline: `tokens.json` → C# const records + CSS custom
   properties + Markdown reference table + WCAG 1.4.3/1.4.11 contrast CI gate + CVD ΔE2000
   audit. (~3h; design-engineering subagent council mandatory)

3. **W#46 Phase 4** — UNBLOCKED (P1b follow-up shipped PR #680). Blazor + React + MAUI
   Win/Mac adapter implementations + a11y harness extension + CI gates.
   (~8h; WCAG/a11y subagent mandatory)

4. **W#55 Phase 2** — Ship's Office reference impl: `DefaultShipsOfficeDataProvider` +
   `SUNFISH_SHIPSOFFICE_PERM001` Roslyn analyzer. (~6h; security-engineering subagent
   mandatory)

5. **W#52 Phase 2** — `DefaultAlertRouter` + `DefaultThreatTriggerService`. CRITICAL: read
   `tactical-p2-system-principal-authority-addendum.md` first. Security council mandatory.
   (~6-8h)

6. **W#48 Phase 2** — `DefaultIntegrationAtlasProvider` in new `blocks-integrations/`
   package. Read `atlas-integration-config-p2-blocks-integrations-addendum.md` before
   starting. (~8-10h)

7. **W#58 Phase 1** — `AnchorIdentityAtlasSurface` + 5 Anchor Blazor identity pages.
   Hand-off at `identity-atlas-implementations-stage06-handoff.md`.
   **GATED on item 1 closing (H1: W#53 PR 2c-react must merge first).**
   Security-engineering subagent mandatory; READ-ONLY invariant enforced.
   (~10h / 2 PRs)

8. **W#1 WS-B** — `TenantSelection` migration: `AuditQuery.Tenant` + `EntityQuery.Tenant` +
   `DataExport.TenantId` from `TenantId?` → `TenantSelection?`. Hand-off at
   `tenant-selection-wsb-stage06-handoff.md`. **NOW READY** (security follow-up PR #692 ✓).
   (~4-6h; breaking api-change pipeline)

**Also ready (no specific ordering constraint):**
- **W#54 Phase 2b** — `SickBayDataProvider` Mission Envelope integration: wire
  `IMissionEnvelopeProvider`, count ProbeStatus per dimension → `WarningProbeCount` /
  `CriticalProbeCount` / `AtmosphereHealth` classification. **READ XO ruling first:**
  `xo-ruling-2026-05-06T20-00Z-w54-phase2b-atmosphere-mapping.md`.
  Security-engineering subagent mandatory. (~2-3h / 1 PR)
- **W#50 Phase 2b** — `DefaultEngineRoomCommandService` + `IDocumentQuarantineStore`
  (in foundation-engine-room). **READ XO ruling first:**
  `xo-ruling-2026-05-06T20-00Z-w50-phase2b-command-service.md`.
  Security-engineering subagent mandatory. (~3-4h / 1 PR)
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
