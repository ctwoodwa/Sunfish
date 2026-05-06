---
type: directive
workstream-or-chapter: multi-workstream gate-sweep PRs #678/#670/#680
last-pr: "#680"
---

Gate-sweep complete for PRs #678 (IActorPrincipalResolver), #670 (W#51 Phase 2 partial),
#680 (W#46 P1b subscribe-before-load), #681 (pre-legal template — no workstream impact).

Ledger updated: W#46/48/51/52/54/55 status cells reflect new state.

## COB Priority Queue (post-sweep)

1. **W#53 Phase 2 PR 2b** — `QuickTogglesWidget` + `RecentStandingOrdersWidget` + Blazor adapters
   (H8 cleared PR #662; full reactive subscribe-before-load available; hand-off at
   `helm-identity-atlas-stage06-handoff.md`)

2. **W#46 Phase 2b** — codegen pipeline: `tokens.json` → C# const records + CSS custom properties
   + Markdown reference table + WCAG 1.4.3/1.4.11 contrast CI gate + CVD ΔE2000 audit
   (design-engineering subagent council mandatory; hand-off at
   `shared-design-system-stage06-handoff.md`)

3. **W#46 Phase 4** — **NOW UNBLOCKED** (P1b follow-up shipped PR #680); Blazor + React + MAUI
   adapter implementations + a11y harness extension + CI gates. WCAG/a11y subagent mandatory.

4. **W#51 Phase 2b** — `DefaultQuarterdeckCommandService` + `AcknowledgeAlertAsync`; deferred from
   PR #670 (`DefaultQuarterdeckDataProvider` shipped; command service deferred). Check hand-off at
   `quarterdeck-entry-point-stage06-handoff.md`.

5. **W#52 Phase 2** — FULLY UNBLOCKED (IActorPrincipalResolver shipped PR #678); read
   `tactical-p2-system-principal-authority-addendum.md` before starting; CRITICAL security
   council mandatory.

6. **W#54 Phase 2** — FULLY UNBLOCKED (IActorPrincipalResolver PR #678); H3 (ADR 0068 Accepted)
   still gates `KeyRotationTrigger` type-swap but doesn't block starting Phase 2; H4
   `IFieldDecryptor` reflection check mandatory pre-merge.

7. **W#55 Phase 2** — FULLY UNBLOCKED (IActorPrincipalResolver PR #678); `SUNFISH_SHIPSOFFICE_PERM001`
   analyzer mandatory; security-engineering subagent pre-merge.

8. **W#48 Phase 2** — FULLY UNBLOCKED (IActorPrincipalResolver PR #678); must go to
   `blocks-integrations` package per XO ruling (read
   `atlas-integration-config-p2-blocks-integrations-addendum.md` first).

9. **W#1 WS-A Phase 1** — `TenantId.System` sentinel + `TenantSelection` + `IMayHaveTenant`
   Obsolete; hand-off at `tenant-selection-wsa-stage06-handoff.md`; non-breaking; WS-B gated.

10. **W#53 Phase 2 PR 2c** — React adapter renderers + WCAG tests (WCAG/a11y subagent mandatory;
    after PR 2b ships).

Cap concurrent fallback PRs at 3. Priority queue has depth; no idle signal needed.
