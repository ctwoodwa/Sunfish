---
type: resumed
workstream-or-chapter: IActorPrincipalResolver ruling + ADR 0082/0083/0084/0085 Accepted (PR #672)
last-pr: "#672 (batch ADR status flip — 0082+0083+0084+0085 Accepted)"
---

**XO ruling on COB question PR #671 — `IActorPrincipalResolver`:**

Option A confirmed. Hand-off authored:
`icm/_state/handoffs/actor-principal-resolver-stage06-handoff.md`

Key decisions:
- `DefaultPermissionResolver` line 250 is CORRECT — do not modify it.
- Canonical invariant: `ActorId.Value = PrincipalId.ToBase64Url()` (43-char base64url
  of 32-byte Ed25519 key). SHA-256 derivation is EXPLICITLY FORBIDDEN.
- `InMemoryActorPrincipalResolver`: explicit override dict + canonical base64url fallback
  + null on FormatException (fail-closed).
- DI: `TryAddSingleton<IActorPrincipalResolver, InMemoryActorPrincipalResolver>()` in
  each Phase 2 package's DI extension (NOT in a new ShipCommonServiceExtensions — Phase 5
  authors that file).
- PR #670 (W#51 Phase 2 draft): unblocked — inject `IActorPrincipalResolver` and use the
  pattern documented in the hand-off.

**PR #672 impact — ADRs 0082/0083/0084/0085 all Accepted:**
- W#54 Phase 2 HALT (ADR 0082) → **CLEARED**
- W#55 Phase 2 HALT (ADR 0083) → **CLEARED**
- W#1 WS-A HALT (ADR 0084) → **CLEARED**
- W#1 WS-B HALT (ADR 0084 + 0085) → **CLEARED** (still requires WS-A built first)

**Updated priority order for COB:**

1. **`IActorPrincipalResolver` seam** (~1h, 1 PR) — ships BEFORE any Phase 2 data provider.
   `IActorPrincipalResolver` + `InMemoryActorPrincipalResolver` in `foundation-ship-common`.
   Pre-merge council mandatory. Hand-off:
   `icm/_state/handoffs/actor-principal-resolver-stage06-handoff.md`

2. **W#53 Phase 2 React adapter** (~3-4h, 1 PR) — `HelmRenderer` in
   `packages/ui-adapters-react/`; parity with Blazor renderer; WCAG/a11y subagent mandatory.
   Independent of #1 — can be worked in parallel if capacity allows.
   Hand-off: `helm-identity-atlas-stage06-handoff.md` §Phase 2 (PR 2d-react section).

3. **W#46 Phase 2b** (~3h, 1 PR) — design-token codegen: `tokens.json` → C# const records
   + CSS custom properties + Markdown reference + WCAG 1.4.3/1.4.11 contrast CI gate +
   CVD ΔE2000 audit. Design-engineering subagent mandatory.
   Hand-off: `shared-design-system-stage06-handoff.md` §Phase 2b.

4. **W#46 Phase 1b follow-up** (~1h, 1 PR) — `DefaultPermissionResolver`
   subscribe-before-load cache invalidation (4 tests; IDisposable; InMemoryStandingOrderEventStream).
   Pre-merge council mandatory (concurrency + IDisposable).
   **Ship BEFORE Phase 4.**
   Hand-off: `shared-design-system-permres-cache-invalidation-addendum.md`

5. **W#51 Phase 2** (~4-5h, 1 PR) — `DefaultQuarterdeckDataProvider` +
   `DefaultQuarterdeckCommandService` + permission pre-resolution via `IActorPrincipalResolver`.
   Security-engineering subagent mandatory. Inject `IActorPrincipalResolver`; use pattern from #1.
   **Requires item #1 on origin/main first.**
   Hand-off: `quarterdeck-entry-point-stage06-handoff.md` §Phase 2 + `actor-principal-resolver-stage06-handoff.md` §usage pattern.

6. **W#52 Phase 2** (~6-8h, 1-2 PRs) — `DefaultAlertRouter` + `DefaultThreatTriggerService`.
   READ `tactical-p2-system-principal-authority-addendum.md` FIRST.
   Security-engineering subagent mandatory. Requires item #1 on origin/main.
   Hand-off: `tactical-anomaly-detection-stage06-handoff.md` §Phase 2 + addendum.

7. **W#54 Phase 2** (~4-5h, 1 PR) — `DefaultSickBayDataProvider` + IDC role + 4-eyes
   Medevac workflow. ADR 0082 now Accepted (PR #672). Requires item #1 on origin/main.
   Hand-off: `sick-bay-stage06-handoff.md` §Phase 2.

8. **W#55 Phase 2** (~4-5h, 1 PR) — `DefaultShipsOfficeDataProvider` + Scribe role +
   reference content editor. ADR 0083 now Accepted (PR #672). Requires item #1 on origin/main.
   Hand-off: `ships-office-stage06-handoff.md` §Phase 2.

9. **W#48 Phase 2** (~8-10h, 2 PRs) — `DefaultIntegrationAtlasProvider` in `blocks-integrations`.
   READ `atlas-integration-config-p2-blocks-integrations-addendum.md` FIRST
   (`IssueXxxAsync` returns `Task<StandingOrderId>`). Requires item #1 on origin/main.
   Hand-off: `atlas-integration-config-stage06-handoff.md` §Phase 2 + addendum.

10. **W#46 Phase 4** (~8h, 1 PR) — Blazor + React + MAUI a11y primitive impls + 3 CI gates.
    WCAG/a11y subagent mandatory. Requires item #4 shipped first.
    Hand-off: `shared-design-system-stage06-handoff.md` §Phase 4.

11. **W#1 WS-A Phase 1** (~3-4h, 1 PR) — `TenantId.System` sentinel + `TenantSelection`
    discriminated union + `IMayHaveTenant` [Obsolete]. ADR 0084 now Accepted (PR #672).
    Hand-off pre-authored: `tenant-selection-wsa-stage06-handoff.md`.

12. **W#1 WS-B Phase 1** (~3-4h, 1 PR) — `TenantSelection?` migration across all query
    surfaces. ADR 0084 + 0085 both Accepted (PR #672). **Requires WS-A built first.**
    Hand-off pre-authored: `tenant-selection-wsb-stage06-handoff.md`.

13. **W#50 Phase 2** (~5-6h, 1-2 PRs) — observability provider + telemetry aggregation.
    Hand-off: `engine-room-observability-stage06-handoff.md` §Phase 2.

14. **W#23 P6 iOS home screen** (~4h; iOS toolchain session) — independent line.
    Hand-off: main iOS hand-off §Phase 6.

**Items #2, #3, #11 are independent of item #1 and can run concurrently.**
Items #5–#9 all require item #1 first.
