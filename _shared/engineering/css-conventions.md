# CSS Class Conventions

**Status:** Accepted (2026-04-22)
**Last reviewed:** 2026-04-22
**Governs:** Every `.razor`, `.razor.cs`, `.razor.css`, `.cs`, `.scss`, and `.css` file in the repo that emits or declares a CSS class.
**Companion docs:** [coding-standards.md](coding-standards.md), [package-conventions.md](package-conventions.md).
**Source:** [ADR 0025 — CSS Class Prefix Policy](../../docs/adrs/0025-css-class-prefix-policy.md).
**Agent relevance:** Loaded by agents authoring UI components, CSS providers, or style audits. High-frequency for any UI work.

Sunfish uses a single canonical CSS class prefix family (`sf-*`) with a two-tier split between consumer-facing API classes and provider-internal implementation classes. This file codifies the policy recorded in ADR 0025 so new components land in the right tier without re-litigating the decision.

---

## The two tiers

### Tier 1 — Public API (`sf-<component>`)

Consumer-facing. Stable across minor versions. Consumers may target these classes with their own CSS for customization.

| Shape | Use for | Example |
|---|---|---|
| `sf-<component>` | Component root | `sf-button`, `sf-datagrid`, `sf-dialog` |
| `sf-<component>__<slot>` | BEM element / slot inside the component | `sf-dialog__title`, `sf-datagrid__row`, `sf-datagrid__cmd-btn` |
| `sf-<component>--<modifier>` | BEM modifier on the root | `sf-button--primary`, `sf-datagrid--size-small` |
| `sf-<component>__<slot>--<modifier>` | BEM modifier on a slot | `sf-datagrid__row--selected`, `sf-datagrid__cell--selected`, `sf-datagrid__col--locked-end` |

Double-underscore (`__`) for slots. Double-hyphen (`--`) for modifiers. Single-hyphen within a token stays as word separator (`col-resize-handle`, `drop-target`).

**Rename rules:** Public-API classes are part of Sunfish's API surface. Breaking renames are tracked in release notes and, pre-v1, executed in one sweep per ADR 0025's pre-release modification (no compat-alias cycle).

### Tier 2 — Provider-internal (`sf-<provider>-<component>`)

Implementation detail. Renames freely between versions. Consumers do not target these.

| Shape | Use for | Example |
|---|---|---|
| `sf-bs-<component>` | Bootstrap-provider override | `sf-bs-datagrid`, `sf-bs-button` |
| `sf-fluent-<component>` | Fluent v9 provider override | `sf-fluent-datagrid`, `sf-fluent-button` |
| `sf-material-<component>` | Material 3 provider override | `sf-material-datagrid`, `sf-material-button` |

Provider-internal classes live in the provider's own SCSS partial (e.g., `Providers/Bootstrap/Styles/components/_data-grid.scss`). The corresponding CSS provider C# class (`BootstrapCssProvider.cs`, `FluentUICssProvider.cs`, `MaterialCssProvider.cs`) emits them via its `ISunfishCssProvider` methods.

A component author using `sf-bs-*` inside a framework-agnostic Razor file is a bug — provider-specific emission must go through the `ISunfishCssProvider` indirection.

---

## Deprecated prefixes

### `mar-*` (Marilo legacy) — DELETED

The `mar-*` prefix originated from "Marilo," an earlier incarnation of the DataGrid work. All occurrences were renamed to `sf-*` in the ADR 0025 sweep (2026-04-22). No compat aliases — `mar-*` was never a public API contract.

**If you encounter `mar-*` in code:** that's a regression. Rename to the equivalent `sf-<component>__<slot>` form per the tier rules above.

### `k-*` (Kendo legacy) — DELETED (except compat-telerik)

A single survivor in `SunfishFloatingLabel.razor.css` referenced `k-*` only in a historical comment; no runtime `k-*` classes exist in first-party Sunfish code.

`packages/compat-telerik/` is the sole exception: it emits Telerik-native `k-*` classes on top of Sunfish as part of the Telerik API shim. Those are Kendo's, not Sunfish's, and are explicitly whitelisted.

---

## Authoring checklist

Before merging a component PR:

- [ ] Every class the Razor emits has a selector in the corresponding SCSS partial, or is explicitly documented as unstyled (e.g., a hook for consumer CSS).
- [ ] Every selector in the SCSS partial is actually emitted by the Razor. No orphan selectors.
- [ ] Slot and modifier names use BEM (`__`, `--`) — never single-hyphen slot names (`sf-dialog-title` is wrong; `sf-dialog__title` is right).
- [ ] Public-API classes (`sf-<component>__<slot>`) are documented in the component's JSDoc/XML comment under "CSS classes."
- [ ] Provider-internal classes go through `ISunfishCssProvider` methods, not hardcoded in shared Razor.
- [ ] No `mar-*` or standalone `k-*` classes. (Run: `grep -rn "mar-\|(?<![a-zA-Z])k-" packages/ apps/` to check.)

---

## CSS custom properties

CSS custom properties follow the same tier rule with a leading `--`:

- `--sf-<component>-<token>` for component-scoped tokens (e.g., `--sf-datagrid-row-bg`, `--sf-dialog-padding`)
- `--sf-color-*`, `--sf-space-*`, `--sf-font-size-*` for foundation-level design tokens
- No `--mar-*` custom properties — rename on sight.

Provider-internal custom properties may use the provider's own native namespace (`--bs-*`, `--colorNeutralStroke1`, `--sf-color-surface-container`) when they're wiring to a native token ladder. That's a deliberate pass-through, not a prefix violation.

---

## References

- [ADR 0025 — CSS Class Prefix Policy](../../docs/adrs/0025-css-class-prefix-policy.md) — the decision, considered options, and rationale.
- [ADR 0023 — Dialog Provider-Interface Expansion](../../docs/adrs/0023-dialog-provider-slot-methods.md) — Dialog BEM migration context.
- [SYNTHESIS.md](../../icm/07_review/output/style-audits/SYNTHESIS.md) — Theme 1 (BEM drift root cause) and cross-cutting decision #1.
- [BEM documentation](https://getbem.com/naming/) — double-underscore slot / double-hyphen modifier reference.
