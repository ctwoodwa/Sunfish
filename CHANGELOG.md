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
