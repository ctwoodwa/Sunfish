# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

See [`_shared/engineering/releases.md`](_shared/engineering/releases.md) for release
mechanics (cadence, tagging, GitHub Releases) and
[`_shared/product/compatibility-policy.md`](_shared/product/compatibility-policy.md)
for the semver rules that govern what qualifies as patch / minor / major.

## [Unreleased]

### Added

- Quarterdeck entry-point surface in `Sunfish.Foundation.Quarterdeck` + `Sunfish.Blocks.Quarterdeck` — `IQuarterdeckDataProvider` (snapshot + subscription: OOD watch + mission envelope + recent orders + alerts + KPI cards + permission-pre-resolved department links), `IQuarterdeckCommandService` (`AcknowledgeAlertAsync` with §Trust audit-ordering invariant), `DefaultQuarterdeckDataProvider` + `DefaultQuarterdeckCommandService` reference implementations, `QuarterdeckOptions`, `IQuarterdeckAlertSource` / `IDepartmentKpiSource` pluggable sources (with startup uniqueness validation), 3 `AuditEventType` constants, 3 `ShipAction` constants. Per ADR 0080, W#51.
- Six WCAG-conformant Blazor panels in `Sunfish.Blocks.Quarterdeck`: `DepartmentNavPanel` (skip-link + nav landmark + aria-disabled denied items + `RenderMainLandmark` param), `AlertTickerPanel` (dual assertive/polite live regions + `IAsyncDisposable` timer), `WatchStatusPanel` (OOD handover dialog with `role="alertdialog"` + focus trap + keyboard suppression), `SearchPanel` (ARIA 1.2 combobox + `ISearchAsYouType<QuarterdeckSearchResult>` + polite result-count live region), `RecentOrdersPanel` (full-row link per SC 2.4.4 + timezone display), `MissionEnvelopePanel` (always-in-DOM status span per ARIA22 + text + CSS class per SC 1.4.1). Per ADR 0080 §Phase 3a/3b, W#51.
- `QuarterdeckPage` in Anchor — all six panels wired under `/quarterdeck` route; nav link added; `AddSunfishQuarterdeck()` registered in `MauiProgram.cs`. Per ADR 0080 §Phase 4, W#51.
- `ExtensionFieldSpec.FeatureKey` and `ExtensionFieldSpec.FeatureGateOffPolicy` — optional parameters enabling operator-runtime field gating (ADR 0075, W#44).
- `IExtensionFieldCatalog.GetFieldsAsync(Type, FeatureEvaluationContext, CancellationToken)` — async overload that evaluates feature gates and returns `MaterializedExtensionField` records.
- `FeatureGateOffPolicy` enum (Hide / Sequester / Redact).
- `MaterializedExtensionField` record + `GateState` enum.
- `ExtensionFieldRedactionDeniedException` — thrown when Redact policy is denied by the capability graph.
- 5 new `AuditEventType` constants: `ExtensionFieldGated`, `ExtensionFieldFiltered`, `ExtensionFieldSequestered`, `ExtensionFieldRedacted`, `ExtensionFieldGateEvaluationFailed`.
- `AddExtensionFieldCatalogWithFeatureGating(IServiceCollection)` DI extension.
- OOD Watch Rotation substrate in `Sunfish.Foundation.Wayfinder` — `IOodWatchService` + `IOodWatchRepository` + `internal IOodWatchSweepRepository` + `OodWatch` record + `OodWatchId` / `OodRole` / `OodWatchState` / `OodHandoverKind` + `OodWatchConflictException` + `DefaultOodWatchService` + `OodWatchExpiryService` (`internal sealed` hosted background sweep with 5-min default cadence). Per ADR 0078, W#49.
- 3 new `AuditEventType` constants for OOD: `OodWatchStarted`, `OodWatchRelieved`, `OodWatchExpired`.
- `OodHandoverKind` enum — `Voluntary` (severity `"Normal"`) vs `CommandRelieved` (severity `"High"`) discriminator on `IOodWatchService.HandoverWatchAsync`; surfaces in the `OodWatchRelieved` audit payload as `handoverKind`. Per W#49 P2 amendment R3.
- `StandingOrder.IssuedDuringWatchId` — optional 11th positional `OodWatchId?` field correlating Standing Order issuances with the OOD watch active at issuance time. Per ADR 0078 §1.
- Engine Room observability surface in `Sunfish.Foundation.EngineRoom` + `Sunfish.Blocks.EngineRoom` — `IEngineRoomDataProvider` (health summary, CRDT growth metrics, 30-second heartbeat subscription), `IEngineRoomCommandService` (quarantine / release / compact with §Trust audit-emission ordering), `DefaultEngineRoomDataProvider` + `DefaultEngineRoomCommandService` reference implementations, `IDocumentQuarantineStore` persistence seam, `EngineRoomOptions`, `ISyncDaemonHealthSource` / `ICrdtDocumentRegistry` optional seams, 6 OTel instruments (`EngineRoomMetrics`), 8 `AuditEventType` constants. Per ADR 0079, W#50.
- Five WCAG-conformant Blazor panels in `Sunfish.Blocks.EngineRoom`: `EngineRoomHealthBanner` (subsystem tile list + EOOW badge), `MainPropulsionPanel` (accessible grid + trace log), `ElectricalPanel` (CRDT growth gauge + sr-only table), `DamageControlPanel` (quarantine/compact command surface with `role="alertdialog"` + deliberation-pause + focus management), `QaWorkshopPanel` (stub; full implementation requires separate intake). Per ADR 0079 §Phase 3a/3b, W#50.
- `EngineRoomPage` in Anchor Blazor — read-only observability panels (HealthBanner + MainPropulsion + Electrical + QA Workshop) wired under `/engine-room` route; nav link added to NavMenu. Per ADR 0079 §Phase 4, W#50.

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [Unreleased] — 2026-04-26 session

High-velocity session: ~30 PRs (#93–#138) landed across CI infrastructure,
accessibility, internationalization, analyzers, security hardening, and
documentation. Entries below are scoped to that session and will be promoted
into a tagged release once the next version is cut.

### Added

- Hindi (`hi-IN`), Japanese (`ja-JP`), Hebrew (`he-IL`), Chinese (`zh-CN`),
  Korean (`ko-KR`), French (`fr-FR`), German (`de-DE`), Spanish (`es-ES`), and
  Brazilian Portuguese (`pt-BR`) translations for the Bridge and Anchor
  accelerators (PRs #121, #122, #125).
- New `SUNFISH_I18N_002` analyzer that flags unused localization resources, now
  wired across all consuming projects via a ProjectReference cascade
  (PRs #111, #128).
- New `SUNFISH_A11Y_001` analyzer that enforces accessibility-name coverage on
  Sunfish components, now promoted to a hard `--fail-on-missing` gate for the
  Phase 1 surface (PRs #111, #114).
- bUnit + axe-core accessibility test cascade extended to Charts, Navigation,
  Media, and 11 nested DataDisplay component folders (PRs #113, #127).
- CI guard rails: cross-plan health gate, RESX `<comment>` XSS scanner, and a
  permanent CI assertion that mirrors the historical Plan 2 Task 3.6 binary
  gate (PRs #99, #100, #101).
- New ADR 0037 documenting the decision to stay on GitHub Actions (with `act`
  for local runs), plus a Mac developer runbook (PR #136).

### Changed

- `SUNFISH_I18N_002` severity raised to **Error** so untranslated or unused
  resources fail the build on the Phase 1 surface (PR #124).
- CI pipeline now skips heavy build/test gates on docs-only PRs via
  `paths-ignore` for `*.md`, `.wolf/`, `docs/`, `waves/`, `icm/`, and
  `_shared/` (PR #116).
- Global-UX CI workflow gained build cache layers and concurrency cancellation,
  and the long-running a11y audit was moved off the PR critical path
  (PR #108).
- Branch protection on `main` was rewritten as a GitHub Ruleset so that
  `paths-ignore` works correctly for required checks (PRs #126, #138).

### Fixed

- Twenty-three critical and serious accessibility bugs across six component
  categories: progress bars, dialogs, target sizes, navigation structure, grid
  column menu, spreadsheet, popup, chip, inline AI prompt, split button, and
  more (PRs #102, #103, #104, #105, #112, #123, #134).
- Kernel lease release now broadcasts and drains peer subscribers before
  `ReleaseAsync` returns, eliminating a real release-broadcast race
  (PR #118).
- Husky `prepare` script tolerates a missing .NET SDK, so Node-only CI and
  fresh worktrees can install and commit cleanly (PRs #94, #115).
- CI build failures resolved: MSB1006 semicolon escaping in MSBuild
  properties, NETSDK1112 cache miss, selective `WarningsAsErrors` for the
  analyzers job, removal of the `continue-on-error` a11y-audit from aggregator
  needs, and the bash shell pinning needed for semicolon-safe MSBuild
  invocations (PRs #106, #107, #109, #110).
- Docs workflow MAUI workload error and global-ux-gate workflow-loading
  failure (post-cascade regressions) (PR #137).

### Security

- Banned the `pull_request_target` trigger across all workflows to harden
  against workflow-injection from fork PRs (PR #129).
- Minimal `permissions:` blocks added to every workflow as part of public-repo
  hardening (PR #132).
- Audited fork-PR approval requirements and the auto-merge scope (manual CLI
  only — no automation gate) and captured the current GitHub settings state
  (PRs #130, #133).

### Internal

- Phase-1 finalization plan published with parallel a11y and i18n cascades
  across four waves (PRs #86, #87, #88, #89, #90, #91, #92, #93).
- Plan 5 entry verdict, scaffolding, severity-gap report, branch-protection
  script (gated on human approval), TDD a11y-audit-runner, and final
  pipeline-p95 measurement (12.44 min) landed (PRs #95, #96, #97, #98, #119,
  #120).
- Documentation cleanup: forward-looking debt audit from the session,
  cascade-batch correction noting that `SunfishGantt` and `SunfishScheduler`
  are intentional dual-namespace components (not duplicates) (PRs #117,
  #135).

[Unreleased]: https://github.com/ctwoodwa/Sunfish/compare/main...HEAD
