---
type: directive
workstream-or-chapter: multi-workstream — post-session-12 priority queue (corrected)
last-pr: "#682 (cob-idle session-12 end)"
---

Post-session-12 gate-sweep confirmed. Corrected priority queue follows — W#53 PR 2b+2c-blazor
already shipped (PRs #665/#666+#667); cohort batting average now 41-of-41.

NOTE: XO directive PR #683 is also in-flight with the ledger updates for W#46/48/51/52/54/55;
its item 1 (W#53 PR 2b) was stale — this directive supersedes it.

## COB Priority Queue (post-session-12, corrected)

1. **W#53 Phase 2 PR 2d-react** — TypeScript React adapter renderers for all 6 Helm widgets.
   H9 parity gate MUST be cleared before Phase 2 closes. WCAG/a11y subagent mandatory.
   Hand-off at `helm-identity-atlas-stage06-handoff.md`.

2. **W#46 Phase 2b** — codegen pipeline: `tokens.json` → C# const records + CSS custom properties
   + Markdown reference table + WCAG 1.4.3/1.4.11 contrast CI gate + CVD ΔE2000 audit.
   (~3h; design-engineering subagent council mandatory)

3. **W#46 Phase 4** — UNBLOCKED (P1b follow-up shipped PR #680). Blazor + React + MAUI Win/Mac
   adapter implementations + a11y harness extension + CI gates. (~8h; WCAG/a11y subagent mandatory)

4. **W#54 Phase 2** — Sick Bay reference impl: `DefaultStretcherBearerPolicy` +
   `DefaultFirstAidSurface` + `NoopKeyRotationScheduler` + IActorPrincipalResolver wiring.
   (~4-5h; security-engineering subagent mandatory pre-merge; H4 `IFieldDecryptor` reflection
   test required)

5. **W#55 Phase 2** — Ship's Office reference impl: `DefaultShipsOfficeDataProvider` +
   `SUNFISH_SHIPSOFFICE_PERM001` Roslyn analyzer. (~6h; security-engineering subagent mandatory)

6. **W#51 Phase 2b** — `DefaultQuarterdeckCommandService` + `AcknowledgeAlertAsync` deferred from
   PR #670. Check hand-off at `quarterdeck-entry-point-stage06-handoff.md`.

7. **W#52 Phase 2** — `DefaultAlertRouter` + `DefaultThreatTriggerService`. CRITICAL: read
   `tactical-p2-system-principal-authority-addendum.md` first. Security council mandatory.
   (~6-8h)

8. **W#48 Phase 2** — `DefaultIntegrationAtlasProvider` in new `blocks-integrations/` package.
   Read `atlas-integration-config-p2-blocks-integrations-addendum.md` before starting. (~8-10h)

9. **W#1 WS-A Phase 1** — `TenantId.System` sentinel + `TenantSelection` + `IMayHaveTenant`
   Obsolete; hand-off at `tenant-selection-wsa-stage06-handoff.md`; non-breaking; WS-B gated.

10. **W#53 Phase 3-deferred** — Identity Atlas implementations (Anchor + Bridge accelerator
    `IIdentityAtlasSurface` impls); deferred pending separate hand-off from XO.

Cap concurrent PRs at 3. Recommended start: items 1 + 2 (can run in parallel).
