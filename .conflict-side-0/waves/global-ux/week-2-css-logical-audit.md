# CSS logical-properties audit — initial pass (2026-04-25)

**Wave:** Plan 4B §2 — CSS logical-properties sweep.
**Verdict:** **AUDIT INFRASTRUCTURE LANDED; CASCADE PENDING.**
**Findings:** 233 hand-authored physical-property uses across Sunfish's hand-authored CSS + Razor inline styles. All from production source (DocFX `_site/`, minified third-party, and `@charset`-marked compiler output already excluded).

---

## Summary by package

| Package | Findings |
|---|---:|
| `apps/kitchen-sink` | 183 |
| `packages/ui-adapters-blazor` | 50 |

## Top files

| Count | File |
|---:|---|
| 24 | `apps/kitchen-sink/wwwroot/css/app.css` |
| 14 | `packages/ui-adapters-blazor/Shell/SunfishAppShell.razor.css` |
| 9 | `apps/kitchen-sink/Pages/Components/ResizableContainer/Overview.razor` |
| 7 | `packages/ui-adapters-blazor/Components/DataDisplay/Gantt/SunfishGantt.razor` |
| 7 | `packages/ui-adapters-blazor/Shell/SunfishAccountMenu.razor.css` |
| 6 | `apps/kitchen-sink/Pages/Components/DataDisplay/Heatmap/Accessibility/Demo.razor` |
| 6 | `apps/kitchen-sink/Pages/Components/DataDisplay/Sparkline/Accessibility/Demo.razor` |
| 6 | `apps/kitchen-sink/Pages/IconsPage.razor.css` |
| 5 | `packages/ui-adapters-blazor/Components/LocalFirst/SunfishTeamSwitcher.razor.css` |
| 4 | `apps/kitchen-sink/Pages/Components/DataDisplay/Map/Accessibility/Demo.razor` |
| 4 | `packages/ui-adapters-blazor/Shell/SunfishNotificationBell.razor.css` |

## By rule

| Rule | Count | What it flags |
|---|---:|---|
| `CSS-LP-001` | 491→ | `margin-left:` / `margin-right:` |
| `CSS-LP-008` | 298→ | positional `left:` / `right:` |
| `CSS-LP-002` | 234→ | `padding-left:` / `padding-right:` |
| `CSS-LP-003` | 159→ | `border-left:` / `border-right:` |
| `CSS-LP-006` | 155→ | `text-align: left/right` |
| `CSS-LP-004` | 88→ | `border-top-left-radius:` etc. |
| `CSS-LP-005` | 80→ | `border-bottom-left-radius:` etc. |
| `CSS-LP-007` | 17→ | `float: left/right` |

(*The →* values are pre-filter totals across the 1530 raw matches; final 233 is the post-`@charset`-filter set.)

## Full inventory

`waves/global-ux/css-logical-audit-2026-04-25.json` carries the full machine-readable
inventory: per-finding file, line, column, rule ID, matched text, suggested replacement,
and surrounding-line context.

---

## Cascade plan

The audit infrastructure is the deliverable for THIS wave. Remediation is multi-wave
cascade work (Plan 4B §2 spans Weeks 3-6); the inventory above is the work backlog.

**Suggested order for the cascade:**

1. **Hand-authored CSS files first** (highest signal-to-noise, no Razor markup churn):
   `app.css`, `SunfishAppShell.razor.css`, `SunfishAccountMenu.razor.css`,
   `IconsPage.razor.css`, `SunfishNotificationBell.razor.css`,
   `SunfishTeamSwitcher.razor.css`. ~50 findings.

2. **Razor inline styles** (mostly `style="margin-left:..."` patterns; mechanical
   replace): kitchen-sink demo pages. ~150 findings.

3. **High-density component files** (`SunfishGantt.razor`,
   `Pages/Components/ResizableContainer/Overview.razor`): ~16 findings; component-specific
   review needed since these mix layout + chart-rendering logic.

**Validation:** after each batch, re-run `pnpm audit:css-logical` and verify the
baseline drops; the script's `--fail-on-finding` flag is the CI integration hook
Plan 5 will pick up.

## Cascade out of scope

The audit explicitly excludes:

- **Compiled vendor CSS** (Bootstrap / FluentUI / Material provider bundles —
  detected by `@charset` first-line heuristic). Sunfish doesn't author these;
  vendors ship them with whatever direction-handling the upstream framework has.
- **Generated DocFX output** (`/_site/`).
- **Minified files** (`.min.css`).
- **Third-party static assets** (`/wwwroot/lib/`, `/wwwroot/css/vendor/`).
- **Test fixtures and harness markup** (`/tests/`).

If RTL bugs appear in vendor-bundled CSS during real-locale testing, the fix lives
upstream OR in a Sunfish-specific override stylesheet, not in the bundled vendor CSS.

---

## How to re-run

```bash
# Default human-readable scan
node tooling/css-logical-audit/audit.mjs

# Machine-readable JSON
node tooling/css-logical-audit/audit.mjs --json

# CI gate mode (exit 1 on any finding)
node tooling/css-logical-audit/audit.mjs --fail-on-finding

# Update the inventory checked into the repo
node tooling/css-logical-audit/audit.mjs --json > waves/global-ux/css-logical-audit-YYYY-MM-DD.json
```

A `pnpm audit:css-logical` script in the workspace root wires the default scan for
casual use during cascade work.
