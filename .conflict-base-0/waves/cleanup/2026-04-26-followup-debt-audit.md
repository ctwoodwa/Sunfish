# Forward-looking debt audit — session 2026-04-26

**Author:** subagent (cleanup audit)
**Scope:** PRs #87 → #134 (all PRs touched in the 2026-04-25/26 session)
**Method:** PR-body scan + source-tree grep (TODO/CLAIM/FIXME/HACK/XXX/Fact(Skip)/NoWarn)
**Status:** read-only audit — no code or CI changes in this PR

---

## Executive summary

| Category | Count | Notes |
|---|---|---|
| `Fact(Skip)` markers in test code | **20** | 11 tracked-fixture (DataDisplay), 5 axe-real-bug, 2 environmental, 1 prod race, 1 streaming parking-lot |
| `Theory(Skip)` markers in test code | **2** | Both in `SyncStatePaletteAuditTests`, awaiting designer-led palette refinement |
| `TODO`/`CLAIM:`/`FIXME` in source code (non-docs) | **~33** in `packages/`, **~29** in `accelerators/`, 0 in `tooling/`, 5 in `apps/docs/` | Pre-existing, not introduced by this session |
| `<NoWarn>SUNFISH_I18N_002</NoWarn>` Path-A suppressions | **21 csprojs** | All on PR #128 branch (not yet on main); each carries follow-up TODO |
| `<NoWarn>CS1591</NoWarn>` (XML doc warnings) | **52 csprojs** | Pre-existing baseline; not session debt |
| Open PRs documenting follow-up debt (#127–#134) | **8** | Several unmerged but actively contributing debt to track |
| Bridge `TODO(w5.5.x)` deployment markers | **15 occurrences across 8 files** | Tracked under Wave 5.5 — separate workstream |
| A11y-cascade-surfaced bugs not yet fixed (PR #127 cohort) | **2 pre-existing** + remediated via PR #134 | The 2 (`SunfishGridColumnMenu`, `SunfishSpreadsheet`) come from PR #113; addressed in PR #123 (open) |

**Recommended priority order:**
1. **Land PR #128** (LocUnused wiring with 21 Path-A NoWarns) — debt is real but tracked; getting it on main starts the consumer-wiring follow-up clock.
2. **Land PR #134** (11 a11y fixes from cascade extension) + **PR #127** (the cascade tests themselves) — flip 11 fact bodies from fail→pass.
3. **Land PR #118** (kernel-lease real race fix) — un-skips a `Fact(Skip)` covering a real production bug.
4. **Apply the main-ruleset** (`infra/github/apply-main-ruleset.sh`) — PR #126 landed the JSON; the human-gated apply step is still pending per `infra/github/README.md`.
5. **Tighten `approval_policy`** from `first_time_contributors` → `all_external_contributors` per PR #130 recommendation.

---

## 1. Skipped tests (`Fact(Skip)` / `Theory(Skip)`) — full inventory

### 1a. Real production bug, deferred

| File | Line | Skip reason |
|---|---|---|
| `packages/kernel-lease/tests/FleaseLeaseCoordinatorTests.cs` | 204 | "Real production race in Release broadcast" — **fix queued in PR #118** |

### 1b. Real a11y bug surfaced by Wave-1 cascade — Cluster B (AI folder), pending fix

| File | Line | Component | Rule |
|---|---|---|---|
| `packages/ui-adapters-blazor-a11y/tests/AI/SunfishChatA11yTests.cs` | 48 | `SunfishChat.TypingIndicator` | `aria-prohibited-attr` |
| `packages/ui-adapters-blazor-a11y/tests/AI/SunfishAIPromptA11yTests.cs` | 61 | `SunfishAIPrompt` (history aside) | `target-size` |

Note: PR #103 fixed `SunfishInlineAIPrompt` chip target-size; the two skips above remain. **Recommended follow-up:** dispatch a single subagent to fix both (theme CSS for AIPrompt history; restructure SunfishChat typing bubble to `role="status"` per the skip note).

### 1c. Tracked-fixture skips (definition-only or typed-generic) — DataDisplay cluster

11 files, all marked "Requires complex fixture - tracked", added by PR #113:

| File | Component class | Reason |
|---|---|---|
| `tests/DataDisplay/Gantt/SunfishGanttDependenciesA11yTests.cs:27` | `SunfishGanttDependencies<T>` | definition-only, needs parent host |
| `tests/DataDisplay/Gantt/SunfishGanttA11yTests.cs:33` | `SunfishGantt<T>` | typed task graph + view |
| `tests/DataDisplay/DataGrid/SunfishTreeListColumnA11yTests.cs:27` | column definition | no isolated DOM |
| `tests/DataDisplay/DataGrid/SunfishPivotGridRowFieldA11yTests.cs:26` | column definition | no isolated DOM |
| `tests/DataDisplay/DataGrid/SunfishPivotGridMeasureFieldA11yTests.cs:26` | column definition | no isolated DOM |
| `tests/DataDisplay/DataGrid/SunfishPivotGridColumnFieldA11yTests.cs:26` | column definition | no isolated DOM |
| `tests/DataDisplay/DataGrid/SunfishGridColumnA11yTests.cs:26` | column definition | no isolated DOM |
| `tests/DataDisplay/DataGrid/SunfishDataSheetColumnA11yTests.cs:27` | column definition | no isolated DOM |
| `tests/DataDisplay/DataGrid/SunfishDataSheetA11yTests.cs:28` | `SunfishDataSheet<T>` | column definitions + typed rows |
| `tests/DataDisplay/DataGrid/SunfishDataGridA11yTests.cs:34` | `SunfishDataGrid<T>` | needs `IDownloadService` injection |

All 10 (plus the Gantt pair) are intentional — they exist to keep coverage symmetric. **Recommended follow-up:** one targeted task to author `MockDownloadService` + a typed-row fixture builder so DataGrid/DataSheet/Gantt skips can flip live.

### 1d. Environmental / runtime-required skips

| File | Line | Skip reason |
|---|---|---|
| `accelerators/bridge/tests/Sunfish.Bridge.Tests.Integration/HealthCheckTests.cs` | 11 | "Requires Podman/Docker runtime" — keep, intentional |

### 1e. Designer-blocked

| File | Line | Skip reason |
|---|---|---|
| `tooling/Sunfish.Tooling.ColorAudit/tests/SyncStatePaletteAuditTests.cs` | 67 | "Awaiting designer-led palette refinement" — see `waves/global-ux/week-2-cvd-palette-audit.md` |
| `tooling/Sunfish.Tooling.ColorAudit/tests/SyncStatePaletteAuditTests.cs` | 92 | (same) |

### 1f. Streaming parking-lot (Phase C)

Documented in `docs/superpowers/plans/2026-04-18-platform-phase-C-input-modalities.md:1096`. Not yet implemented in test code; Phase C is queued.

---

## 2. PR #128 Path-A `<NoWarn>SUNFISH_I18N_002</NoWarn>` debt

PR #128 is **open** and adds the LocUnused analyzer cascade wire-up. It surfaces 21 csprojs whose scaffolded SharedResource bundles (per commits `93c53ba2`, `0485abc5`, `d987042d`, `a540410e`) have no consumer reference yet. Each csproj got a Path-A NoWarn:

| Path-A suppressed csproj | Tracking note |
|---|---|
| `packages/foundation/Sunfish.Foundation.csproj` | foundation SharedResource (8 keys) — needs consumer wiring |
| `packages/ui-core/Sunfish.UICore.csproj` | (same) |
| `packages/ui-adapters-blazor/Sunfish.UIAdapters.Blazor.csproj` | (same) |
| `packages/blocks-accounting/Sunfish.Blocks.Accounting.csproj` | per-block bundle |
| `packages/blocks-assets/Sunfish.Blocks.Assets.csproj` | (same) |
| `packages/blocks-businesscases/...` | (same) |
| `packages/blocks-forms/...` | (same) |
| `packages/blocks-inspections/...` | (same) |
| `packages/blocks-leases/...` | (same) |
| `packages/blocks-maintenance/...` | (same) |
| `packages/blocks-rent-collection/...` | (same) |
| `packages/blocks-scheduling/...` | (same) |
| `packages/blocks-subscriptions/...` | (same) |
| `packages/blocks-tasks/...` | (same) |
| `packages/blocks-tax-reporting/...` | (same) |
| `packages/blocks-tenant-admin/...` | (same) |
| `packages/blocks-workflow/...` | (same) |
| `apps/kitchen-sink/Sunfish.KitchenSink.csproj` | demo bundle |
| `accelerators/anchor/Sunfish.Anchor.csproj` | onboarding/errors/state |
| `accelerators/bridge/Sunfish.Bridge/Sunfish.Bridge.csproj` | onboarding/errors/state |

**Follow-up shape (post-merge of #128):** one PR per package or one per layer (foundation → ui-core → adapters → blocks) that:
1. Adds `IStringLocalizer<SharedResource>` consumers in the package source.
2. Removes the `;SUNFISH_I18N_002` token from that csproj's `<NoWarn>`.
3. Verifies the build still succeeds.

The "scaffolded ahead of consumer wiring" pattern (memory: `feat(foundation): scaffold SharedResource bundle`) is intentional and supports incremental delivery; this debt should not be conflated with mistakes.

---

## 3. Source-code TODO / CLAIM / FIXME inventory

### 3a. Codebase-wide (`packages/` + `accelerators/`)

The session did **not** add new TODOs to source code beyond the Path-A NoWarn TODO comments in csprojs (covered in §2). All other markers below are **pre-existing**, surfaced here as the broader debt baseline:

#### packages/ — high-value follow-up TODOs (excludes SCSS theme files, which carry many noise TODOs)

| File:line | Theme |
|---|---|
| `packages/blocks-accounting/Models/DepreciationSchedule.cs:12, 28` | G17 — `ComputeScheduleAsync` not implemented |
| `packages/blocks-accounting/Services/IAccountingService.cs:98` | (same — cross-reference) |
| `packages/blocks-rent-collection/Services/InMemoryRentCollectionService.cs:168` | credit-memo logic awaiting payment-reconciliation follow-up |
| `packages/blocks-rent-collection/Services/CreateScheduleRequest.cs:7`, `Models/RentSchedule.cs`, `Models/Invoice.cs` | `LeaseId` migration to `Sunfish.Blocks.Leases.Models.LeaseId` (G14) |
| `packages/blocks-tax-reporting/Models/TaxReport.cs`, `Services/TaxReportCanonicalJson.cs` | Ed25519 signing of canonical bytes — future pass |
| `packages/ingestion-spreadsheets/SpreadsheetIngestionPipeline.cs:76` | G21 — migrate to `IEntityStore.CreateBatchAsync` |
| `packages/ingestion-core/Quota/{IIngestionQuotaStore,InMemoryIngestionQuotaStore}.cs` | "sunfish/platform#TODO" — tracking placeholder; needs real link |
| `packages/kernel-crdt/SnapshotScheduling/ShallowSnapshotManager.cs:17, 119` | ADR 0028 — backend-swap when real backend lands |
| `packages/kernel-crdt/Backends/StubCrdtEngine.cs` | (related) |
| `packages/ui-core/Contracts/ISunfishCssProvider.cs:12` | phase-2 follow-up — split provider interface by category |

#### accelerators/ — Bridge deployment TODOs (W5.5.x family)

15 occurrences across 8 files in `accelerators/bridge/deploy/`:

| File | Markers |
|---|---|
| `terraform/main.tf` | `TODO(w5.5.2)` NAT gateway; `TODO(w5.5.3)` SQS/RabbitMQ broker; `TODO(w5.5.4)` HTTPS/ACM listener; `TODO(w5.5.5)` ServiceDiscovery |
| `terraform/outputs.tf` | `TODO(w5.5.4)` https URL once cert wired |
| `terraform/README.md` | All four documented in "Known TODOs" section |
| `bicep/modules/stack.bicep` + `bicep/README.md` | `TODO(w5.5.1)` DAB config from Key Vault / Files |
| `k8s/40-network-policies.yaml` + `k8s/README.md` + `k8s/21-bridge-dab.yaml` | `TODO(w5.5.6)` tighten relay HTTPS egress; `TODO(w5.5.1)` DAB config |

**Status:** Already tracked under Wave 5.5. Out of scope for this session's cleanup.

#### accelerators/ — Anchor TODOs

| File:line | Theme |
|---|---|
| `accelerators/anchor/Components/QrScanner.razor:6`, `Components/Pages/Onboarding.razor:61`, `README.md:80` | Camera path TODO until .NET 11 preview API |
| `accelerators/anchor/Services/QrOnboardingService.cs:28` | Encode-then-base64 path deferred (platform integration) |
| `accelerators/anchor/Services/AnchorBootstrapHostedService.cs:92` | Wave 6.8 — replace synthesized-Guid fallback with join-team flow |
| `accelerators/bridge/Sunfish.Bridge/Proxy/TenantWebSocketReverseProxy.cs:28, 57` | TODO(5.3.B) — chain `RequireAuthorization("browser-shell")` once auth wired |

### 3b. Documentation — "deferred / parking-lot" trail

- `apps/docs/blocks/rent-collection/deferred-integrations.md` — explicit "honest list" of modeled-but-not-wired integrations
- `apps/docs/blocks/tax-reporting/signed-hash-export.md:44` — Ed25519 signing future pass
- `docs/superpowers/plans/2026-04-18-platform-phase-C-input-modalities.md:1096` — streaming parking-lot

These are intentional documentation artifacts, **not debt to action immediately**.

---

## 4. PR-body forward-looking notes (open PRs, session)

| PR # | State | Forward-looking notes (paraphrased from PR body) |
|---|---|---|
| #114 | OPEN | Promotes SUNFISH_A11Y_001 to `--fail-on-missing` — Phase 1 surface clean (3 components: button/dialog/syncstate-indicator); pre-condition land before adding more Lit components |
| #115 | OPEN | Drops Node-Husky bootstrap so fresh worktrees commit cleanly — small chore |
| #117 | OPEN | Inline-correction to cascade-batch report regarding dual-namespace components — see §6 below |
| #118 | OPEN | Real Release broadcast race fix; un-skips kernel-lease test — see §1a |
| #119 / #120 | #120 MERGED | Plan-5 Task 9 close-out (p95 12.44 min) — no code debt |
| #121, #122, #125 | OPEN | Locale completeness work for he-IL, ja-JP, hi-IN, zh-CN, ko-KR, fr-FR, de-DE, es-ES, pt-BR — incremental |
| #123 | OPEN | Fix for SunfishGridColumnMenu + SunfishSpreadsheet (the 2 PR #113 surfaced bugs) |
| #124 | OPEN | Promotes SUNFISH_I18N_002 to Error severity (parity with I18N_001) |
| #127 | OPEN | 91 new bUnit-axe test files; surfaced 11 new a11y violations |
| #128 | OPEN | Wires LocUnused as ProjectReference cascade — see §2 |
| #129 | OPEN | Bans `pull_request_target` / `workflow_run` triggers via CI gate |
| #130 | OPEN | Audit recommends tightening `approval_policy` to `all_external_contributors` (human-gated apply) |
| #131 | CLOSED | Replaced by #133 (commitlint type) |
| #132 | OPEN | Adds explicit `permissions:` blocks to ci.yml / commitlint.yml / global-ux-gate.yml |
| #133 | OPEN | Auto-merge scope audit — verdict GREEN, no scoping change needed; documents pattern |
| #134 | OPEN | Fixes 11 a11y violations from PR #127 cascade |

---

## 5. CI / infra gaps still to land (operational follow-ups)

| Item | Where | Status |
|---|---|---|
| Apply ruleset to `main` | `infra/github/apply-main-ruleset.sh` | Script exists (PR #126); **human-gated apply step pending** |
| Tighten `approval_policy` | per PR #130 audit recommendation | Pending owner approval |
| Ban `pull_request_target` / `workflow_run` triggers | PR #129 | Pending merge |
| Workflow-level `permissions:` blocks on remaining 3 workflows | PR #132 | Pending merge |
| Drop legacy Node-Husky bootstrap | PR #115 | Pending merge |
| Promote SUNFISH_A11Y_001 to `--fail-on-missing` | PR #114 | Pending merge — needs Phase 1 surface stays clean |
| Promote SUNFISH_I18N_002 to Error severity | PR #124 | Pending merge — pairs with PR #128 |

---

## 6. Architectural follow-ups (require ICM api-change pipeline)

### 6a. Dual-namespace components (PER ADR 0022 — DO NOT DEDUPE WITHOUT api-change ICM)

Per memory `project_dual_namespace_components.md`:

| Component | Rich (DataDisplay) | MVP (canonical leaf) |
|---|---|---|
| `SunfishGantt` | `Components/DataDisplay/Gantt/` | `Components/Scheduling/` |
| `SunfishScheduler` | `Components/DataDisplay/Scheduler/` | `Components/Scheduling/` |
| `SunfishSpreadsheet` | `Components/DataDisplay/Spreadsheet/` | `Components/Editors/` |
| `SunfishPdfViewer` | `Components/DataDisplay/` | `Components/Media/` |

These are **intentional rich-vs-MVP coexistence** with active callers under both namespaces and divergent public APIs. Do not refactor into a single file; do not "dedupe." If reconciliation is genuinely needed, it requires:
1. `sunfish-api-change` ICM pipeline (NOT a refactor)
2. Migration guide
3. Consumer updates
4. Major version bump

PR #117 documents the inline correction. PR #112 inadvertently described these as "duplicate component files" (its "Discovered architecture debt" section); future readers should treat that section as superseded.

### 6b. SCSS theme TODOs in Material/Bootstrap/FluentUI providers

Many `_*.scss` files under `packages/ui-adapters-blazor/Providers/Material/Styles/{components,patterns}/` contain TODOs left from initial provider scaffolding. These are noise-level individually but warrant a single pass to either resolve or convert to issue references. Not blocking.

---

## 7. Documentation gaps

- **Changelog entries** for the 23 a11y fixes in PR #112 + 5 a11y fixes from PRs #102/103/104/105 + 11 fixes in PR #134 — only relevant once cut (no version cut yet this cycle).
- **Kitchen-sink demos** for newly-fixed components (PR #112 cohort) — verify each fixed component still demos as expected; no missing-demo evidence found, but a verification pass would close the loop.
- **API documentation** for new parameters introduced by PRs #112 (`AriaLabel`, `InMenu`, `InTreeOverride`, `InList`, `Title`) and #134 (`Label` on `SunfishProgressCircle`, `Nested` on `SunfishAppBar`) — XML doc-comments are likely present but should be spot-checked before next docs-site publish.

---

## 8. Recommended next-batch dispatch (3–5 highest-leverage follow-ups)

1. **Land PRs #128 + #134 + #127 as a coordinated trio.** PR #128 establishes the LocUnused gate; PR #127 ships the cascade tests; PR #134 fixes the a11y bugs the cascade catches. Together they convert ~32 latent failures into active gates plus 11 production fixes. (Each can merge independently; sequencing reduces flake noise.)
2. **Land PR #118.** The kernel-lease release-race fix un-skips a real production bug from a `Fact(Skip)`. Single-package change; high leverage; small blast radius.
3. **Apply the main-ruleset (`apply-main-ruleset.sh`).** Owner-only step; closes the gap the ruleset PR identified.
4. **Dispatch one subagent to remove the SunfishChat typing-bubble + SunfishAIPrompt history-aside skips** (§1b). Two small CSS / markup fixes; un-skips two more facts.
5. **Plan a "DataGrid fixture builder" task** to author `MockDownloadService` + typed-row helper so the 10 DataGrid/Sheet skips can flip live (§1c). One non-trivial PR.

---

## 9. Explicit "DO NOT TOUCH" list

| Item | Reason |
|---|---|
| `Components/DataDisplay/{Gantt,Scheduler,Spreadsheet}/` + `Components/Scheduling/` + `Components/Editors/` + `Components/Media/SunfishPdfViewer.*` | Dual-namespace by design (ADR 0022). Dedupe requires `sunfish-api-change` ICM. |
| `infra/github/branch-protection-main.json` + `branch-protection-main-before.json` + `apply-branch-protection.sh` | Legacy branch-protection API kept for **rollback reference and historical diff** per `infra/github/README.md`. The active mechanism is the Ruleset (`main-ruleset.json`). |
| `accelerators/bridge/deploy/{terraform,bicep,k8s}/**` `TODO(w5.5.x)` markers | Tracked under Wave 5.5 deployment workstream — separate plan, do not action piecemeal. |
| `accelerators/bridge/tests/.../HealthCheckTests.cs:11` `Fact(Skip = "Requires Podman/Docker runtime")` | Intentional environmental skip. |
| `tooling/Sunfish.Tooling.ColorAudit/tests/SyncStatePaletteAuditTests.cs:67, 92` `Theory(Skip)` | Awaits human designer decision per `waves/global-ux/week-2-cvd-palette-audit.md`. |
| All 21 `<NoWarn>SUNFISH_I18N_002</NoWarn>` Path-A entries on PR #128 branch | Per the PR body, scaffolded SharedResource keys are real (commit `93c53ba2` and friends); resist the urge to delete the keys (Path B was rejected). Only the consumer-wiring follow-up clears these. |
| `<NoWarn>CS1591</NoWarn>` baseline across 52 csprojs | Pre-existing project-wide policy (XML doc warnings off for non-public-API projects); not session debt. |
| `apps/docs/blocks/rent-collection/deferred-integrations.md` | This page IS the documented deferred-list; it documents the debt rather than being debt itself. Don't "resolve" by deleting the page. |

---

## 10. Self-verdict

**GREEN** — full audit completed across PR bodies (#87–#134) and source-tree scans for `TODO|CLAIM:|FIXME|HACK|XXX|Fact(Skip)|Theory(Skip)|NoWarn`. ~33 packages-side + ~29 accelerators-side TODO markers identified, 22 skipped tests inventoried, 21 Path-A NoWarns documented (PR #128 branch), 8 open PRs categorized, 5 highest-leverage follow-ups recommended.
