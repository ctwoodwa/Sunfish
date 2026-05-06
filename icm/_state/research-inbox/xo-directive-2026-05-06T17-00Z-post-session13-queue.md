---
type: directive
workstream-or-chapter: multi-workstream — post-session-13 priority queue
last-pr: "702 (W#52 split ruling + directive update)"
---

Updated 2026-05-06 (session 18 — housekeeping):
- PR #697 MERGED — W#52 Phase 2a (DefaultAlertRouter) on origin/main.
- PR #702 MERGED — W#52 split ruling + this directive update on origin/main.
- PR #699 MERGED — ADR 0082-A1 (AtmosphereHealth.Unknown) on origin/main.
- PR #700 (COB's ADR 0082-A1 addendum) — still open, needs rebase. PR #699 already
  landed the ADR changes. COB must rebase, drop the ADR + beacon conflicts, and retain
  only: sick-bay-stage06-addendum.md + W54 W*.md source + ledger flip.
- W#52 COB question beacon archived (answered by ruling at T20-30Z).
- Superseded T17-31Z W#50 store-placement ruling archived (superseded by T20-00Z).

## COB Priority Queue (post-session-17)

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

5. **W#52 Phase 2b** — `DefaultTacticalRuleEngine` (§2.1): Channel<TacticalSignal> per-tenant
   partitioning + rule-error-rate tracking + `sunfish.*` prefix restriction.
   Phase 2a (DefaultAlertRouter) shipped PR #697. CRITICAL: read
   `tactical-p2-system-principal-authority-addendum.md` first. Security-engineering subagent
   mandatory. (~3-4h). Ruling at `xo-ruling-2026-05-06T20-30Z-w52-phase2b-2c-split.md`.

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
- **W#52 Phase 2c** — `DefaultThreatTriggerService` (§2.3): 8-step `TryIssueAsync` pipeline.
  Per `tactical-p2-system-principal-authority-addendum.md`: identity-based authority check
  (NOT IPermissionResolver; systemPrincipal.ActorId for audit). Security-engineering subagent
  mandatory. (~4-5h / 1 PR). Can ship in parallel with Phase 2b or after.

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
