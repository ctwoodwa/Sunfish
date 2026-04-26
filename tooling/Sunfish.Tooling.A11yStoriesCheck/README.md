# Sunfish.Tooling.A11yStoriesCheck

CI gate that emits **SUNFISH_A11Y_001** when a Sunfish UI component has no sibling
`*.stories.ts` file.

## Why a Node tool, not a Roslyn analyzer?

The source is TypeScript, not C#. A Roslyn analyzer would need either a marker `.cs`
file in `packages/ui-core/` (no `.cs` files exist there — it's a pnpm package) or would
trigger on the wrong compilation. An MSBuild-style Node check matches the existing pattern
in `tooling/locale-completeness-check/` and `tooling/css-logical-audit/`, which solves
the same shape of cross-language CI assertion.

## What it checks

Walks `packages/ui-core/src/components/` and, for every Lit component file
(`sunfish-*.ts`), verifies that a sibling `sunfish-*.stories.ts` exists.

```
packages/ui-core/src/components/
  button/
    sunfish-button.ts            ← component
    sunfish-button.stories.ts    ← sibling stories file (REQUIRED)
```

A component without sibling stories cannot participate in the a11y Storybook test-runner
pipeline (Plan 4 Task 4.2; the `a11y-storybook` job in `.github/workflows/global-ux-gate.yml`).
Missing stories are a CI-visible quality gate.

## Scope (v1)

**v1 (this release):** ui-core Lit components only.

**Deferred to v2:**
- `.razor` component coverage in `packages/ui-adapters-blazor/`. Razor stories live
  in a different harness; needs separate scan rules.
- React adapter coverage in `packages/ui-adapters-react/`. Adapter components mirror
  ui-core's contracts; coverage may be redundant — needs Plan 6 input.

## Usage

```bash
# Report only; exits 0 if clean
node tooling/Sunfish.Tooling.A11yStoriesCheck/check.mjs

# CI mode — exits 1 if any component is missing stories
node tooling/Sunfish.Tooling.A11yStoriesCheck/check.mjs --fail-on-missing

# JSON output (machine-readable)
node tooling/Sunfish.Tooling.A11yStoriesCheck/check.mjs --json

# Run own fixture tests
node --test tooling/Sunfish.Tooling.A11yStoriesCheck/tests/
```

## Wired into CI

The `a11y-stories-check` job in `.github/workflows/global-ux-gate.yml` runs this tool
on every PR touching `packages/ui-core/**`. Initial mode is **report-only** (no
`--fail-on-missing`) to avoid blocking the cascade in flight; promote to fail mode
once the Phase 1 surface is fully covered (per Plan 5 spec §"CI gates").

## Severity

Warning (initial). Plan 5 §"CI gates" reserves Error promotion for after the Phase 1
cascade has cleaned up legitimate gaps. Promotion path is `--fail-on-missing` flag
flipped on in the workflow YAML.

See [Plan 5 spec](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-week-6-ci-gates-plan.md)
and [Plan 5 implementation plan](../../docs/superpowers/plans/2026-04-25-plan-5-ci-gates-implementation-plan.md).
