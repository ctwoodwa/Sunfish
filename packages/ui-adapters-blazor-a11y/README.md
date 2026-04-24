# Sunfish.UIAdapters.Blazor.A11y

bUnit-to-axe accessibility bridge for Sunfish's Blazor adapter. Implements ADR 0034's
third harness so the Blazor render target is held to the same a11y contract as
`ui-core` (Web Components) and `ui-adapters-react`.

**Status:** Scaffolding (Plan 4 Task 1.1). Lifecycle + assertions land in Tasks 1.2 – 1.7.

---

## What this package does

1. Renders Razor components via bUnit (`IRenderedFragment.Markup`).
2. Wraps that markup in a full HTML5 document with theme CSS injected.
3. Hosts the document inside a Playwright chromium headless browser.
4. Runs `axe-core` against the hosted document; returns structured `AxeResult` data.
5. Evaluates Sunfish-specific contract assertions (focus order, keyboard map, RTL
   icon mirror, etc.) against the same hosted page.

## Why not single-harness across all three adapters

See [ADR 0034](../../docs/adrs/0034-a11y-harness-per-adapter.md). Short version:

- bUnit renders to HTML strings, not a live browser. Playwright needs a running
  document to run axe against.
- No in-ecosystem tool bridges bUnit `IRenderedFragment` directly to Playwright.
- Building the bridge is cheaper than either (a) hosting Blazor Server test apps
  under Playwright (doubles test runtime, changes execution semantics), or
  (b) skipping Blazor a11y audit entirely (violates the first-class mandate).

## Contract source-of-truth

Sunfish's per-component a11y contract lives in the Storybook `parameters.a11y.sunfish`
block on each component's `.stories.ts`. This bridge consumes the contract by reading
`dist/a11y-contracts.json` — a build artifact emitted by `packages/ui-core/` via the
`export-a11y-contracts.mjs` script (Plan 4 Task 1.6). The bridge never re-parses
Storybook CSF directly.

## Architecture in one paragraph

`PlaywrightPageHost` launches a single chromium headless instance shared across the
test assembly. `AxeRunner.RunAxeAsync` takes a bUnit `IRenderedFragment` + `IPage`,
serialises the fragment markup into a full HTML doc, loads it via
`page.SetContentAsync`, injects axe via `page.AddScriptTagAsync`, and returns typed
results. `SunfishA11yAssertions` adds the contract-driven assertions on top (focus
initial target, focus trap, keyboard map, directional-icon mirror under RTL).

## Known limitations

- **Shadow DOM:** bUnit's `Markup` serialises open shadow roots by default (Blazor
  doesn't use shadow DOM natively, so this is primarily a concern if a component
  explicitly opts in). Closed shadow roots would be invisible to axe; Sunfish's
  components all use open shadow DOM per ADR 0017.
- **CDP emulation:** CVD simulations rely on Playwright's Chrome DevTools Protocol
  hook (`Emulation.setEmulatedVisionDeficiency`). This requires chromium-family
  browsers; Firefox/WebKit alternatives are documented as manual fallbacks.
- **Test runtime:** per Plan 4 Task 1.8 measurement, the target per-component
  runtime is < 5 s × 36 scenarios per component, distributed across 2 dotnet-test
  shards for CI.

## Running locally

After the implementation lands in Tasks 1.2 – 1.7:

```bash
# Install Playwright browsers (one-time, ~400 MB).
pwsh -Command "& packages/ui-adapters-blazor-a11y/bin/Debug/net11.0/playwright.ps1 install chromium"

# Run the tests.
dotnet test packages/ui-adapters-blazor-a11y/tests/tests.csproj
```

## References

- [ADR 0034 — A11y harness per adapter](../../docs/adrs/0034-a11y-harness-per-adapter.md)
- [Plan 4 — A11y Foundation cascade](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-a11y-foundation-plan.md)
- [bUnit documentation](https://bunit.dev/)
- [Playwright for .NET](https://playwright.dev/dotnet/)
- [axe-core API](https://github.com/dequelabs/axe-core/blob/develop/doc/API.md)
