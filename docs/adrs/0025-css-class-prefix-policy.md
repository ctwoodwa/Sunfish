---
id: 25
title: CSS Class Prefix Policy (`sf-*`, `mar-*`, `k-*`)
status: Accepted
date: 2026-04-22
tier: ui-core
concern:
  - ui
composes:
  - 14
  - 17
  - 22
  - 23
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0025 — CSS Class Prefix Policy (`sf-*`, `mar-*`, `k-*`)

**Status:** Accepted (2026-04-22)
**Date:** 2026-04-22
**Pre-release modification:** Because Sunfish is pre-v1 with breaking changes approved, the compat-alias cycle for single-hyphen → BEM renames (e.g., `.sf-dialog-title` → `.sf-dialog__title`) is **dropped**. Renames execute directly in one sweep, no one-minor deprecation window. `mar-*` and `k-*` already had no alias per the original recommendation; that stands. Verified 2026-04-22 that `sf-*` does **not** collide with Syncfusion (`e-*`), DevExpress (`dx-*`), or Infragistics (`igc-*`/`igx-*`) CSS vocabularies — only Telerik's `k-*` was ever in our namespace and is already scoped for deletion.
**Resolves:** Sunfish CSS and Razor class emission today uses three prefix conventions inconsistently: `sf-*` (Sunfish-generic, dominant), `mar-*` (Marilo legacy, scattered in DataGrid), and `k-*` (Kendo-inspired, one survivor). The Tier 4 re-audit ([`TIER-4-RE-AUDIT.md`](../../icm/07_review/output/style-audits/TIER-4-RE-AUDIT.md)) and the style-audit synthesis ([`SYNTHESIS.md`](../../icm/07_review/output/style-audits/SYNTHESIS.md) cross-cutting decision #1; task #48) flagged the prefix inconsistency as a systemic issue blocking further style-parity work. This ADR picks a single canonical prefix policy, documents the migration path, and sets the compat-alias rules so downstream consumers don't break.

---

## Context

Distribution measured 2026-04-22 via grep against `packages/ui-adapters-blazor/**/*.css`:

| Prefix | Occurrences | Files | Notes |
|---|---|---|---|
| `sf-*` | **6,898** | 11 | Overwhelmingly dominant across all providers and shell components |
| `mar-*` | **98** | 4 | Concentrated in DataGrid (resize handles, cell-selected, command buttons) and one utility component |
| `k-*` | **1** | 1 | Single survivor in `SunfishFloatingLabel.razor.css` |

`sf-*` breakdown by major file:

- `sunfish-fluentui.css` — 4,023 occurrences
- `sunfish-bootstrap.css` — 1,558 occurrences
- `sunfish-material.css` — 993 occurrences
- Shell and utility components — ~324 occurrences total

`mar-*` breakdown:

- `sunfish-bootstrap.css` — 39 occurrences (DataGrid resize handle, cell-selected, cmd button, column menu trigger)
- `sunfish-fluentui.css` — 29 occurrences (same DataGrid surfaces)
- `sunfish-material.css` — 29 occurrences (same DataGrid surfaces)
- `SunfishFloatingLabel.razor.css` — 1 occurrence

The `mar-*` prefix originated from "Marilo," an earlier incarnation of the DataGrid work. It survived because renames never happened when the code migrated into Sunfish. `k-*` is a Kendo/Telerik-inspired single survivor, likely copy-pasted from a compat-shim prototype.

The BEM convention is partially-applied: most component classes use BEM double-underscore (`sf-calendar__cell--selected`), but some use single-hyphen (`sf-dialog-title`, `sf-dialog-body`). SYNTHESIS Theme 1 identifies this BEM inconsistency as the root cause of the Calendar-Fluent dead-CSS cascade (different authors used different conventions; the Razor and the CSS drifted).

The Tier 4 re-audit explicitly identifies prefix policy as a Phase 2 blocker — further style-parity work cannot safely rename classes until the target convention is decided, because every rename risks creating another dead-CSS cascade if the Razor and CSS don't update in lockstep.

---

## Decision drivers

- **Internal-only class surface vs. consumer-facing API surface.** Some classes (`sf-datagrid__row--selected`) consumers may target with their own CSS for customization; others (`sf-bs-datagrid-col--locked-end`) are provider-internal. Policy needs to distinguish.
- **Brand alignment.** Sunfish is the product name; `sf-*` is brand-aligned and already dominant. `mar-*` is historical debt with no current meaning.
- **Migration risk.** Changing a CSS class is a consumer-visible breaking change. Any consumer with custom CSS targeting `sf-*`/`mar-*` classes will break if we rename without a compat alias.
- **Provider-specific vs. provider-agnostic.** Some classes apply across all providers (`sf-button`); some are provider-specific overrides (`sf-bs-*`, `sf-fluent-*`). A mixed policy exists today; the question is whether to formalize or collapse it.
- **BEM convention.** Orthogonal to prefix, but interlocked — the decision to enforce BEM double-underscore can be made in this ADR to avoid a second decision pass.
- **Future React adapter.** React adapters typically use CSS-in-JS or module-scoped class names, but if the React adapter consumes the same CSS bundles (e.g., via `ui-components-web` per [ADR 0017](0017-web-components-lit-technical-basis.md)), it inherits the prefix convention.

---

## Considered options

### Option A — `sf-*` everywhere, flat

All classes use `sf-*`. Provider-specific classes become `sf-<component>-<provider>-<slot>` (e.g., `sf-datagrid-bs-locked-col`). `mar-*` is renamed; `k-*` is deleted.

**Tradeoffs:**

- Pro: Simplest mental model. One prefix, one rule.
- Pro: Brand-aligned.
- Con: Provider-specific classes get long names (`sf-datagrid-bs-locked-col` vs. `sf-bs-datagrid__col--locked-end`).
- Con: Flat namespace means no easy way to scope a selector to "only Bootstrap provider overrides."
- Con: Consumers lose the ability to target "all provider-internal classes" with a single selector — provider-internal and consumer-API classes share the prefix.

### Option B — `mar-*` everywhere, preserve history

Rename all `sf-*` to `mar-*`. Minimal churn — 98 existing usages stay, 6,898 rename (wait, that's actually massive churn).

**Tradeoffs:**

- Pro: None practical. Included for completeness.
- Con: 70× more `sf-*` than `mar-*` in the codebase. "Preserve history" means "rename the dominant convention to the minority one."
- Con: `mar-*` has no meaning to any current contributor. Optimizes for a contributor who left.
- Con: Contradicts brand.

Rejected on arithmetic alone.

### Option C — `sf-*` for public API, framework-scoped prefixes for provider internals (partial status quo, formalized)

Formalize what's partially in place today:

- **`sf-<component>`** — consumer-facing class. Stable API. Consumers may target with their own CSS.
- **`sf-<component>__<slot>`** — BEM block/element for slots within the component. Consumer-facing.
- **`sf-<component>--<modifier>`** — BEM modifier. Consumer-facing.
- **`sf-<provider>-<component>__<slot>`** — provider-internal override class. Internal implementation detail; not part of the API. Renames freely between versions.

Examples:

- `sf-button` — public API
- `sf-button__icon` — public API, BEM element
- `sf-button--primary` — public API, BEM modifier
- `sf-bs-button` — Bootstrap-provider-internal class. Has `.btn.btn-primary` selectors downstream.
- `sf-fluent-button__icon` — Fluent-provider-internal icon slot override.

`mar-*` is deleted via renames. `k-*` is deleted. BEM double-underscore is the canonical convention for slots/modifiers.

**Tradeoffs:**

- Pro: Matches the majority-existing convention. Minimal conceptual change.
- Pro: Provider-internal classes are namespaced — `sf-bs-*` won't collide with `sf-fluent-*`.
- Pro: Clear API/implementation boundary. Consumers know what's safe to target.
- Pro: Compat aliases for `mar-*` → `sf-*` are straightforward to author.
- Con: Two-tier vocabulary to document (public vs. internal).
- Con: Author discipline required — a component author accidentally using `sf-bs-*` in shared component code is a mistake CI has to catch.

### Option D — Leave as-is; document the mess

Status-quo preservation. Document `sf-*`/`mar-*`/`k-*` coexistence in a style guide; no renames.

**Tradeoffs:**

- Pro: Zero migration cost.
- Pro: No consumer-breaking changes.
- Con: Documents a mistake as intentional. Every new contributor asks "which prefix should I use?"
- Con: Directly blocks Phase 2 style-parity work (per Tier 4 re-audit) — every class-name decision restarts the argument.
- Con: "Mess" is a reputational issue once the repo goes public.

---

## Decision (recommended)

**Adopt Option C** — formalize the tiered prefix policy:

1. **`sf-<component>`** is the canonical public-API prefix. BEM double-underscore for slots (`sf-<component>__<slot>`); double-hyphen for modifiers (`sf-<component>--<modifier>`).
2. **`sf-<provider>-<component>`** is the canonical provider-internal prefix (where `<provider>` ∈ `{bs, fluent, material}`). Renames freely; consumers do not target these.
3. **`mar-*` is deprecated and deleted via rename.** Every `mar-*` occurrence becomes `sf-*` per rule 1 or rule 2.
4. **`k-*` is deleted** — the single `SunfishFloatingLabel.razor.css` survivor renames to `sf-floating-label-*`.
5. **BEM double-underscore is canonical.** Existing single-hyphen slot classes (`sf-dialog-title`, `sf-dialog-body`, etc.) rename to BEM form (`sf-dialog__title`, `sf-dialog__body`) as part of the ADR 0023 refactor. Scope: this ADR decides the *convention*; individual component ADRs (like 0023) execute the rename for their slice.
6. **compat aliases required for public-API renames only.** `mar-*` was never a public API — delete without alias. Single-hyphen → BEM rename (`sf-dialog-title` → `sf-dialog__title`) requires a compat alias for one minor version because consumers might target it.

Rationale: Option C has the best fit-to-reality — it makes the 99% case (`sf-*`) canonical, the 1% case (`mar-*`, `k-*`) deleted, and documents the public/internal split that's partially in place but unwritten. Total rename volume is ~99 usages (`mar-*` + `k-*`) — manageable in one PR per provider.

---

## Consequences

### Positive

- **Prefix anarchy ends.** Every class has a canonical prefix with a documented meaning. New contributors get a one-rule answer.
- **API/implementation boundary is explicit.** `sf-<component>` is stable API; `sf-<provider>-*` is implementation. Renames between versions are clear about which tier they affect.
- **Phase 2 style-parity work unblocks.** Tier 4 re-audit explicitly flagged this as the Phase 2 blocker. This ADR clears it.
- **BEM consistency.** Adopting BEM double-underscore project-wide removes the Theme 1 root cause for future components (the Razor-vs-CSS drift).
- **Small migration footprint.** ~99 `mar-*`/`k-*` usages rename. The 6,898 `sf-*` usages are unaffected except where BEM re-styling is needed.
- **compat-telerik unaffected.** compat-telerik emits Telerik's own `k-*` classes on top of Sunfish — those are *Telerik* classes, not Sunfish `k-*` legacy. No collision.

### Negative

- **Consumer-visible renames for single-hyphen → BEM.** Any consumer CSS targeting `.sf-dialog-title { ... }` breaks when it becomes `.sf-dialog__title`. Mitigated by compat alias CSS for one minor version cycle.
- **Author discipline required.** Nothing automatically enforces "don't use `sf-bs-*` in a framework-agnostic component file." Mitigated by: lint rule (if feasible), code review, and CI class-audit.
- **Tiered vocabulary to document.** `_shared/engineering/coding-standards.md` gains a CSS section explaining the two-tier model.
- **98 `mar-*` renames happen in-PR.** Each rename is a Razor change plus CSS change plus any test assertion that targets the class by name. Audits didn't count test-file references; an additional grep is part of the implementation checklist.
- **Future framework prefix growth.** If Sunfish adds a Radix or Ant Design provider, `sf-radix-*` and `sf-ant-*` follow the same rule. No cap; that's fine.

---

## Compatibility plan

1. **`mar-*` renames happen silently.** No alias — `mar-*` was never documented as public API. Release note lists the deleted classes for anyone who discovered them anyway.
2. **`k-*` rename (one class) happens silently.** Same reasoning.
3. **Single-hyphen → BEM renames (public-API tier) ship with compat aliases for one minor version.** Example:
   ```css
   /* Compat alias — remove in next minor */
   .sf-dialog-title { @extend .sf-dialog__title; }
   ```
   Or, if `@extend` is not viable in the generated CSS, duplicate the rule block on both selectors.
4. **Release note highlights public-API renames.** List every single-hyphen → BEM migration for consumer awareness.
5. **CI class-audit check** (Phase 2 add): a script scans every Razor file's emitted class strings and every CSS file's selector set; flags classes using disallowed prefixes (`mar-`, standalone `k-` outside compat-telerik) or classes emitted but unstyled and vice versa.
6. **compat-telerik carve-out.** compat-telerik's emission of Telerik-native `k-*` classes stays untouched — those are Kendo's, not ours. The class-audit check whitelists `packages/compat-telerik/` as an exempt folder.

---

## Implementation checklist

- [ ] Update `_shared/engineering/coding-standards.md` (or create a CSS conventions section) documenting the tiered policy + BEM rule.
- [ ] Rename all 98 `mar-*` occurrences → `sf-*` per tier. DataGrid surfaces: `mar-datagrid-cmd-btn` → `sf-datagrid__cmd-btn`; `mar-datagrid-column-menu-trigger` → `sf-datagrid__column-menu-trigger`; `mar-datagrid-cell--selected` → `sf-datagrid__cell--selected`; `mar-datagrid-col--locked-end` → `sf-datagrid__col--locked-end`.
- [ ] Update Razor emission sites for every renamed class.
- [ ] Update CSS selector blocks for every renamed class across all three skins (`sunfish-bootstrap.css`, `sunfish-fluentui.css`, `sunfish-material.css`).
- [ ] Rename the single `k-*` survivor in `SunfishFloatingLabel.razor.css`.
- [ ] Coordinate BEM migrations with [ADR 0023](0023-dialog-provider-slot-methods.md). Dialog slots migrate in the same PR (`sf-dialog-title` → `sf-dialog__title`, etc.) with one-version compat aliases.
- [ ] Grep test assertions for `mar-*` / `k-*` / single-hyphen slot references; update.
- [ ] Author class-audit script (Phase 2, separate PR): Razor emission vs. CSS selector set diff; fail build on orphaned or unstyled classes.
- [ ] Release note: public-API renames listed; `mar-*` deprecation called out as internal cleanup.
- [ ] Update [`SYNTHESIS.md`](../../icm/07_review/output/style-audits/SYNTHESIS.md) cross-cutting decision #1 and task #48 status — "resolved by ADR 0025."
- [ ] Update `.wolf/buglog.json` entry #1 ("Razor class emission doesn't match CSS selectors") — the class-audit script from this ADR is the systemic prevention for future occurrences.

---

## References

- [ADR 0014](0014-adapter-parity-policy.md) — UI Adapter Parity Policy. CSS classes cross the adapter boundary; this ADR's rules apply to any adapter using the shared CSS bundles.
- [ADR 0017](0017-web-components-lit-technical-basis.md) — Web Components. Future `ui-components-web` consumers will inherit the prefix convention via the shared CSS.
- [ADR 0022](0022-example-catalog-and-docs-taxonomy.md) — Example Catalog. Demo pages display classes implicitly; no text-level change but catalog entries reflect renames.
- [ADR 0023](0023-dialog-provider-slot-methods.md) — Dialog Provider-Interface Expansion. Dialog BEM migration happens in coordination with this ADR.
- [`icm/07_review/output/style-audits/SYNTHESIS.md`](../../icm/07_review/output/style-audits/SYNTHESIS.md) — Theme 1 (BEM drift root cause); cross-cutting decision #1; task #48.
- [`icm/07_review/output/style-audits/TIER-4-RE-AUDIT.md`](../../icm/07_review/output/style-audits/TIER-4-RE-AUDIT.md) — Phase 2 blocker identification.
- `.wolf/buglog.json` entry #1 — Razor class emission doesn't match CSS selectors (systemic prevention via class-audit CI check derives from this ADR).
- [BEM documentation](https://getbem.com/naming/) — double-underscore slot / double-hyphen modifier reference.
