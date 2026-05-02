---
id: 34
title: Accessibility Harness Per Adapter
status: Accepted
date: 2026-04-27
tier: adapter
concern:
  - accessibility
  - ui
composes:
  - 17
  - 30
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0034 — Accessibility Harness Per Adapter

**Status:** Accepted (2026-04-27)
**Date:** 2026-04-27
**Deciders:** Chris Wood (BDFL)
**Related ADRs:** [0017](./0017-web-components-lit-technical-basis.md) (Web Components/Lit technical basis), [0030](./0030-react-adapter-scaffolding.md) (React adapter scaffolding)
**Related spec:** [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../superpowers/specs/2026-04-24-global-first-ux-design.md) §7 (Accessibility harness)

---

## Context

The Global-First UX spec establishes per-component accessibility contracts — name, role, keyboard contract, focus order, live-region behaviour, reduced-motion fallback, RTL icon policy — expressed once per component and executed automatically by a harness so the contract is a build gate, not documentation.

Sunfish's adapter architecture forces three execution targets for the same contract:

- **`ui-core`** — Web Components authored in Lit (per ADR 0017). Runs in a real browser, rendering into shadow DOM. Axe-core runs directly against the live DOM.
- **`ui-adapters-react`** — React wrappers around the Web Components. Stories run under Storybook for React; axe still runs against the real browser DOM.
- **`ui-adapters-blazor`** — Razor components compiled under Blazor Server + Blazor WASM. The canonical test tool is bUnit, which renders components to HTML strings in .NET test context — there is no live browser to run axe against, and no Storybook equivalent in the .NET ecosystem that matches Storybook's a11y addon coverage.

No single harness covers all three rendering targets. The spec's mandate is "first-class accessibility," which rules out skipping the Blazor axe gate.

---

## Decision

Three distinct harnesses per adapter, driven by one contract expressed once in the Storybook story's `parameters.a11y.sunfish` block. Each harness reads the same contract object and asserts the same invariants in its native idiom.

| Adapter | Harness stack |
|---|---|
| `ui-core` | Storybook 8 + Web Test Runner + Playwright + `@axe-core/playwright` + `@storybook/addon-a11y` |
| `ui-adapters-react` | Storybook 8 for React + Vitest + Playwright + `@axe-core/playwright` (stories imported from `ui-core` where semantics are identical; React-only wrappers get their own) |
| `ui-adapters-blazor` | bUnit → new `ui-adapters-blazor.A11y` bridge project that serializes bUnit's `IRenderedFragment` output into a Playwright-hosted HTML page, then runs `@axe-core/playwright` against it |

The contract object shape is:

```ts
parameters: {
  a11y: {
    sunfish: {
      name: string,                    // the accessible name assertion
      role: string,                    // the ARIA/semantic role
      keyboard: { keys: string[], behaviour: string }[],
      focusOrder: string[],            // element IDs in expected tab order
      liveRegion?: "polite" | "assertive" | "off",
      reducedMotion: "respects" | "n/a",
      rtlIconMirror: "mirrors" | "non-directional",
    }
  }
}
```

All three harnesses parse this block, run axe (WCAG 2.2 AA), and additionally check the Sunfish-specific invariants (focus order, live region, RTL icon mirror) using adapter-native assertions. Failures gate the component's CI check.

---

## Consequences

### Positive

- Accessibility contract is expressed once; enforced three times.
- Contract drift between adapters is impossible — the story file lives beside the source, gets updated with it.
- New contributors can read one component's `parameters.a11y.sunfish` and know what the component promises.

### Negative / costs

- Three CI matrix entries per component (Node + .NET test jobs); CI time scales 3× per component.
- bUnit-to-axe bridge is new engineering (~1 week for the bridge + test harness).
- `ui-adapters-blazor.A11y` bridge must stay in sync with bUnit's `IRenderedFragment` API across bUnit releases.

### Trust impact

None — the harness runs at CI time, not runtime. No production code path is affected.

---

## Alternatives considered

**Option A — Single harness via Playwright against all three.** Rejected: bUnit output is HTML-in-a-string, not a running app. Hosting Blazor components under Playwright means spinning up Blazor Server test apps, which changes the component's execution environment and doubles test runtime.

**Option B — Skip Blazor a11y gate; rely on manual review.** Rejected: violates the spec's "first-class" mandate. Axe catches what manual review misses (colour contrast, missing labels, tab-trap errors). A build-time gate is the only credible enforcement.

**Option C — Port `ui-core` to Blazor-renderable Web Components.** Rejected: ADR 0017 already sets Lit as the Web Components authoring layer. Blazor can consume Web Components directly; the problem is test harness, not runtime.

---

## Rollout

Week 1 of Phase 1 — three pilot components (`sunfish-button`, `sunfish-dialog`, `sunfish-syncstate-indicator`) exercise all three harnesses end-to-end. Go/no-go gate at end of Week 1 decides whether the harness pattern scales to the full component inventory or the bridge needs re-design.

If the bridge fails the Week 1 gate: fallback is Option B with a documented accessibility-debt register published alongside each release until a bridge replacement lands.

---

## Amendment 1 (2026-04-25) — Node-side contract export pipeline

The original ADR specified that the contract is "expressed once in the Storybook story's
`parameters.a11y.sunfish` block." Implementation surfaced a real cross-language gap:
.NET (the Blazor adapter's harness language) cannot read the contract directly from
`.stories.ts` because the file is TypeScript with imports from `lit` and other Node-only
modules — running it under .NET would require shipping the JS engine into the test
harness.

**The solution adopted in Plan 4 Task 1.6:** a build-time export step bridges the contract
across the language boundary. `packages/ui-core/scripts/export-a11y-contracts.mts` runs
under `tsx`, dynamic-imports each `.stories.ts` to read the live `parameters.a11y.sunfish`
object, and emits `packages/ui-core/dist/a11y-contracts.json` keyed by component tag.
The Blazor bridge's `ContractReader.Default.Load(tagName)` reads that JSON and
deserialises into `SunfishA11yContract` (mirror record of the JS shape).

**Why this is not a deviation from the ADR's "single source of truth":**
- The Storybook story file remains the authoritative source for the contract.
- The JSON file is a build artifact (gitignored under `packages/ui-core/dist/`).
- The Blazor `ContractReader` errors with actionable guidance ("run
  `pnpm --filter @sunfish/ui-core build:contracts`") if the JSON is missing.
- The .NET-side `SunfishA11yContract` record's property names match the JS-side
  contract keys via `[JsonPropertyName(...)]` attributes — a 1:1 mirror.

**Why tsx instead of `@storybook/csf-tools` (which the original Plan 4 design assumed):**
- AST-based extraction via CSF tools requires full literal-only contract objects;
  any computed value (e.g., reading a constant from another file) breaks the parse.
- `tsx`'s ESM-with-esbuild loader executes the actual TypeScript at runtime, so
  contracts that reference `import`-ed values still work.
- tsx adds ~30 MB to dev dependencies (acceptable; not shipped to consumers).

**Failure modes covered by `ContractReader`:**
- Missing JSON → `FileNotFoundException` with the build-command guidance above.
- Unknown tag → `KeyNotFoundException` listing the tags the JSON does contain.
- Runtime cache invalidation via `ContractReader.Reload()` for long-running test sessions.

**No protocol or wire-format implications.** The contract bridge is purely a build-time
+ test-time concern; production runtime is untouched.

This amendment is accepted alongside the original ADR. No status change.
