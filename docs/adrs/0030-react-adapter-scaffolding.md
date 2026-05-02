---
id: 30
title: React Adapter Scaffolding
status: Accepted
date: 2026-04-22
tier: adapter
concern:
  - ui
composes:
  - 14
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0030 — React Adapter Scaffolding

**Status:** Accepted (2026-04-22)
**Date:** 2026-04-22
**Resolves:** Paper §5.2 (UI Kernel four-tier layering) and [ADR 0014](0014-adapter-parity-policy.md) both require React parity with the Blazor adapter. Today no `packages/ui-adapters-react/` directory exists — the second adapter is zero lines of code while the Blazor adapter is 20+ components deep. This ADR scopes the scaffolding work (project shape, component-surface contract, build pipeline, test strategy) without committing to full component parity in a single pass. Wave 0.6 of the paper-alignment plan.

---

## Context

- The Blazor adapter under `packages/ui-adapters-blazor/` ships 20+ components (SunfishButton, SunfishDataGrid, SunfishDialog, SunfishCard, SunfishBadge, SunfishSpinner, SunfishTooltip, SunfishTabs, and more), each wired through `ISunfishCssProvider` and the expanding provider-interface contract that ADRs 0023–0025 extended.
- Paper §5.2 is explicit: *"The Compatibility and Adapter Layer … applies equally to UI adapters (alternative component libraries)."* React is not a nice-to-have; it is the paper's named second-adapter parity case.
- ADR 0014 codifies this as a hard rule: every feature in one adapter must be in the other, and parity tests verify equivalence.
- We are pre-v1 with no external React consumers. We do not owe backward compatibility to anyone outside the monorepo.
- Node.js is already in the CI toolchain for SCSS compilation, so adding a JS/TS build is marginal, not a step change.

---

## Decision drivers

- **Paper is source of truth.** Parity is a must-have, not an aspiration.
- **Scaffolding unblocks Wave 3.5.** A working skeleton lets future waves deliver component parity incrementally instead of a monolithic 20-component port.
- **.NET-to-JS boundary is non-trivial.** The tooling choice (bundler, TS config, test runner) sets the adapter's ceiling for years. Get it right once.
- **Component-preview story matters.** Adapter parity is meaningless if we can't visually compare a Blazor SunfishButton and a React SunfishButton side-by-side under the same provider skin.
- **Minimize infra creep.** Reuse existing Node tooling where possible. No monorepo tooling we don't already own.

---

## Considered options

### Option A — Vite + React + TypeScript, shipped as an NPM package

- Standard React-library shape. `@sunfish/ui-adapters-react` NPM package name.
- Pro: frictionless for React consumers; matches ecosystem norms.
- Con: not .NET-native — separate package manager, separate release cadence, no NuGet artifact.

### Option B — React-via-Blazor-WebAssembly (reuse existing .NET shell)

- Rejected. Defeats the purpose. React parity means React-native, not Blazor-wrapping-React. Also breaks paper §5.2's "alternative component libraries" framing.

### Option C — React + Vite + Storybook, one repo subdir

- Pro: Storybook becomes the adapter-parity verification surface (every component has a story in both Blazor preview and React Storybook under all three provider skins).
- Pro: minimal new infra (Vite handles bundling; Storybook is incremental over Vite).
- Con: adds Node.js build steps to the CI pipeline (already there for SCSS, so marginal).

### Option D — React + Turborepo monorepo setup

- Rejected. Overkill for one adapter. Revisit if we add Vue or Svelte later.

---

## Decision (recommended)

**Adopt Option C — React + Vite + Storybook, one repo subdir at `packages/ui-adapters-react/`.**

Structure:

```
packages/ui-adapters-react/
├── package.json              # NPM: @sunfish/ui-adapters-react
├── vite.config.ts            # library-mode build
├── tsconfig.json
├── src/
│   ├── index.ts              # Barrel
│   ├── components/
│   │   ├── SunfishButton/    # Proof-of-concept: Button, DataGrid, Dialog
│   │   ├── SunfishDataGrid/
│   │   └── SunfishDialog/
│   ├── contracts/
│   │   └── ICssProvider.ts   # Framework-agnostic contract port (matches Sunfish.UICore)
│   └── providers/
│       ├── BootstrapCssProvider.ts
│       ├── FluentUICssProvider.ts
│       └── MaterialCssProvider.ts
├── .storybook/               # Component previews
└── tests/                    # Vitest + React Testing Library
```

**Scope for this scaffolding wave (Wave 3.5):**

1. Project shape + build pipeline working end-to-end (`npm run build` produces a shippable package).
2. Contracts port: `ICssProvider`, `IIconProvider`, `ButtonVariant` enum — TypeScript equivalents of the C# contracts in `packages/ui-core/Contracts/`.
3. Three proof-of-concept components: SunfishButton, SunfishDataGrid, SunfishDialog — the three most-used components in the compat-vendor packages.
4. Storybook with those three components rendered under all three provider skins (nine stories total).
5. Tests: Vitest + React Testing Library, same shape as `packages/ui-adapters-blazor/tests/Components/`.

**Explicitly out of scope for the scaffold:**

- Full parity with all 20+ Blazor components — deferred to future waves.
- React-native equivalents of the compat packages (compat-telerik, future compat-syncfusion, etc.) — much later.

**Rationale:**

- Pre-release status (see `project_pre_release_latest_first_policy` memory) means the scaffold doesn't need day-one 100% parity.
- Storybook becomes the adapter-parity verification surface — a visual diff that both adapters render the same component correctly under the same skin.
- Three proof-of-concept components prove the contracts-port approach works end-to-end before scaling.
- Vite + TypeScript matches modern React ecosystem norms and keeps the JS footprint publishable.

---

## Decision consequences

### Positive

- Paper §5.2 parity requirement is partially satisfied; the remaining work is implementation, not architecture.
- Storybook preview surface becomes the visual QA bench for cross-adapter consistency, complementing the Blazor kitchen-sink demos.
- NPM-publishable artifact enables React consumers to adopt Sunfish without .NET tooling.
- Establishes the pattern for any future third adapter (Vue, Svelte) without re-litigating tool choices.

### Negative

- Node.js adds to the build tool chain (already present for SCSS). Marginally larger CI surface.
- TypeScript contracts duplicate C# contracts; schema drift risk — mitigated by `contracts/` tests that snapshot the TS contract shape and assert it matches the canonical C# source (future: codegen).
- Two adapter codebases to evolve in parallel — per ADR 0014 this was already the expected cost of parity.

---

## Compatibility plan

- React adapter is NPM-only from day one; no NuGet.
- Namespace: `@sunfish/ui-adapters-react` (scoped NPM package).
- Versioning: NPM semver aligned with the C# package versioning cycle. A 0.x release accompanies each corresponding C# 0.x release until v1.
- No deprecation cycle needed for anything — the adapter does not exist yet, so every addition is greenfield.

---

## Implementation checklist

- [ ] Scaffold `packages/ui-adapters-react/` structure.
- [ ] Vite library-mode config + TypeScript project.
- [ ] Port `ICssProvider` + 3 provider classes from C# to TypeScript.
- [ ] Port `ButtonVariant` enum + 2 other enums.
- [ ] Implement SunfishButton / SunfishDataGrid / SunfishDialog proof-of-concept components.
- [ ] Storybook with 9 stories (3 components × 3 providers).
- [ ] Vitest + React Testing Library test harness.
- [ ] GitHub Actions (or equivalent) CI step that runs `npm run build` + `npm test`.
- [ ] Update `Sunfish.slnx`? No — slnx is .NET-only. The React package is out-of-solution.

---

## References

- Paper §5.2 — `_shared/product/local-node-architecture-paper.md`
- [ADR 0014 — Adapter Parity Policy](0014-adapter-parity-policy.md)
- [ADR 0023 — Dialog Provider-Interface Expansion](0023-dialog-provider-slot-methods.md)
- [ADR 0024 — Button Variant Enum Expansion](0024-button-variant-enum-expansion.md)
- [ADR 0025 — CSS Class Prefix Policy](0025-css-class-prefix-policy.md)
- `packages/ui-core/Contracts/ISunfishCssProvider.cs`
- `packages/ui-adapters-blazor/` (structural reference)
- [Vite library mode docs](https://vitejs.dev/guide/build.html#library-mode)
