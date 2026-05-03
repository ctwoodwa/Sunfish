---
id: 24
title: ButtonVariant Enum Expansion for Cross-Framework Style Parity
status: Accepted
date: 2026-04-22
tier: ui-core
concern:
  - ui
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0024 — ButtonVariant Enum Expansion for Cross-Framework Style Parity

**Status:** Accepted (2026-04-22)
**Date:** 2026-04-22
**Pre-release note:** Sunfish is pre-v1 with breaking changes approved; Option B (full split to `ButtonAppearance` + `ButtonIntent`) is now technically viable but **not** adopted. Recommendation stands: Option D (additive enum + Variant × FillMode axis matrix). No audit finding yet shows axis-conflation hurting consumers; keep scope minimal. Option B remains available for a future ADR if adoption patterns demand it.
**Resolves:** Today's `ButtonVariant` enum (`packages/foundation/Enums/ButtonVariant.cs`) has six values — `Primary`, `Secondary`, `Danger`, `Warning`, `Info`, `Success`. The style-parity audits ([`SYNTHESIS.md`](../../icm/07_review/output/style-audits/SYNTHESIS.md) Theme 2a, cross-cutting decision #5; Button × Fluent finding #23) flagged missing values that each target framework needs — `Subtle` and `Transparent` for Fluent v9; `Light` and `Dark` for Bootstrap 5. This ADR decides the final enum shape, naming (`ButtonVariant` vs. splitting to `ButtonAppearance` + `ButtonIntent`), and the provider-mapping contract for the new values.

---

## Context

The three target frameworks expose overlapping-but-distinct button vocabulary:

| Framework | Native variant set |
|---|---|
| **Bootstrap 5** | `primary`, `secondary`, `success`, `danger`, `warning`, `info`, `light`, `dark`, `link` (plus `outline-*` via the outline prefix) |
| **Fluent UI v9** | `primary`, `secondary`, `outline`, `subtle`, `transparent` (plus intent layer: `neutral`, `brand`) |
| **Material 3** | `filled`, `tonal`, `outlined`, `text`, `elevated` (plus color role: primary container, secondary container, error) |

Sunfish's six-value `ButtonVariant` enum covers the color-semantic intent axis (primary / secondary / danger / warning / info / success) but not the visual-treatment axis (filled / subtle / outline / text / transparent) that Fluent and Material treat as first-class. BS5 adds `Light` and `Dark` as neutral-surface options the existing enum cannot express.

The audit's concrete finding (SYNTHESIS Batch 1e, item #23): **Button × Fluent v9** needs `Subtle` and `Transparent` — without them, the Fluent skin of `SunfishButton` falls back to `Secondary` styling, which is visually wrong and loses Fluent's own button hierarchy. Similarly (SYNTHESIS Batch 2a): **Button × BS5** needs `Light` and `Dark` to match BS5's own `btn-light` / `btn-dark` idioms.

A separate complication: there's no `ButtonAppearance` type in the codebase today. SYNTHESIS and the Button × Fluent audit use the name `ButtonAppearance` loosely when referring to what the Sunfish code calls `ButtonVariant`. This ADR picks a single name and sticks with it.

Related existing API surface:

- `ButtonSize` — small/medium/large sizing ladder (separate enum, unaffected)
- `FillMode` — flat/clear/filled/outline fill-mode modifier (separate enum, overlaps)
- `RoundedMode` — rounded-corner modifier (separate enum, unaffected)

The existing `FillMode` enum already covers the filled/outline/flat axis; the expansion in this ADR is purely on the color-and-surface-intent axis.

---

## Decision drivers

- **Every first-party framework must express every common button pattern.** If a consumer writes `<SunfishButton Variant="ButtonVariant.Subtle">` and picks the Bootstrap skin, the provider needs a sensible mapping (even if Bootstrap doesn't have a native `btn-subtle`).
- **Additive is non-breaking; renaming is breaking.** Adding enum values is a minor-version change. Renaming `ButtonVariant` to `ButtonAppearance` is a major-version change.
- **Provider completeness.** Every variant value requires a mapping in every `CssProvider.ButtonClass()` implementation. Missing a case silently falls back to `Primary` or throws.
- **React-adapter parity.** The enum lives in `foundation` and is consumed by both Blazor and React adapters. Per [ADR 0014](0014-adapter-parity-policy.md) the React adapter's Button must handle every value the Blazor adapter does.
- **Avoid axis conflation.** Some frameworks separate "intent" (primary/danger) from "appearance" (filled/outline/subtle). If Sunfish conflates them on one enum, we lose cross-product expressiveness (e.g., "danger + subtle"). Either pick one axis and accept the loss, or split.

---

## Considered options

### Option A — Expand `ButtonVariant` with four new values

Add `Subtle`, `Transparent`, `Light`, `Dark` to the existing enum. No rename, no split.

Final shape:

```csharp
public enum ButtonVariant
{
    Primary, Secondary, Danger, Warning, Info, Success,
    Subtle,       // Fluent subtle; BS5 maps to outline-secondary; M3 maps to text
    Transparent,  // Fluent transparent; BS5 maps to link; M3 maps to text
    Light,        // BS5 btn-light; Fluent maps to subtle on neutral; M3 maps to outlined
    Dark          // BS5 btn-dark; Fluent maps to subtle on neutral-inverted; M3 maps to filled on inverse-surface
}
```

**Tradeoffs:**

- Pro: Non-breaking for existing consumers. Adding values to an enum is safe.
- Pro: Matches the audit's concrete asks (SYNTHESIS #23, #2a) exactly.
- Pro: One enum to document, one mapping table per provider.
- Con: Axis conflation. `Danger + Subtle` can't be expressed — consumer picks one or the other.
- Con: Providers need a cross-product decision for each new value (e.g., what does BS5 do for `Transparent`? Map to `btn-link`? Ship a bespoke class?). Some mappings are semantic compromises.
- Con: Enum growth pattern. If a future framework adds another idiom, the enum grows again. No obvious stopping point.

### Option B — Split into `ButtonAppearance` + `ButtonIntent` (breaking)

Introduce two enums:

```csharp
public enum ButtonIntent { None, Primary, Secondary, Danger, Warning, Info, Success, Light, Dark }
public enum ButtonAppearance { Filled, Outline, Subtle, Transparent, Text }
```

`SunfishButton` takes both parameters. `ButtonVariant` is deleted or aliased.

**Tradeoffs:**

- Pro: Full cross-product. `Danger + Subtle` is expressible. Matches how Fluent and Material actually model buttons.
- Pro: Cleaner mental model — "what color" and "how filled" are genuinely different axes.
- Pro: Folds `FillMode` into `ButtonAppearance` (they overlap; see `FillMode` today for `Flat`/`Clear`/`Filled`/`Outline`).
- Con: Breaking change. Every existing `<SunfishButton Variant="...">` call site updates.
- Con: Doubles the API surface for a component most consumers use without thinking.
- Con: Requires deprecation cycle, parity work in React adapter, migration guide. Heavy for a style-parity fix.
- Con: `FillMode` is already in use and has its own provider methods. Folding it in is a second breaking change piggybacked on the first.

### Option C — Status quo + per-provider override parameter

Keep the six-value enum. Add a `[Parameter] public string? AppearanceOverride { get; set; }` on `SunfishButton`. Consumers targeting Fluent pass `"subtle"`; targeting BS5 pass `"light"`. Provider maps to its own class.

**Tradeoffs:**

- Pro: Non-breaking. Zero enum change.
- Con: String parameter with no type safety. Typos silently fall through.
- Con: Consumers need to know provider-specific vocabulary, which is the anti-pattern ADR 0023 just closed for Dialog slots.
- Con: React adapter gets a different string-param problem.

### Option D — Expand `ButtonVariant` plus add a first-class `FillMode` mapping table (recommended hybrid)

Expand the enum per Option A, and clarify in this ADR that `ButtonVariant` is the color/intent axis while the existing `FillMode` enum is the fill treatment axis. Providers are expected to multiply them when rendering.

New `ButtonClass` contract: `string ButtonClass(ButtonVariant variant, ButtonSize size, FillMode fill, RoundedMode rounded, bool disabled)` already exists — this option formalizes that the two enums are orthogonal and documents the mapping matrix.

`Subtle` becomes `Variant=Subtle, FillMode=Filled`, which a provider may special-case. `Transparent` similarly `Variant=Transparent, FillMode=Clear`. `Light`/`Dark` are neutral-surface variants with any FillMode.

**Tradeoffs:**

- Pro: Non-breaking. Option A's upside.
- Pro: Preserves some cross-product via the existing `FillMode` enum. `Danger + Outline` is already expressible today; this doesn't regress.
- Pro: Documents the axis model explicitly instead of leaving it implicit.
- Con: Axis separation is only partial — `Subtle` and `Transparent` are really fill-modes masquerading as variants. Future cleanup (a proper Option B) might supersede this.
- Con: Provider mapping tables grow `8 variants × 4 fill modes × 3 sizes = 96 class permutations` to reason about, though many collapse.

---

## Decision (recommended)

**Adopt Option D** — expand `ButtonVariant` with `Subtle`, `Transparent`, `Light`, `Dark`, and formalize the `ButtonVariant × FillMode` axis matrix in the provider doc comments.

`ButtonVariant` keeps its name (no rename to `ButtonAppearance`). The SYNTHESIS document and the Button × Fluent audit will both be updated to use `ButtonVariant` consistently.

Final enum:

```csharp
public enum ButtonVariant
{
    Primary, Secondary, Danger, Warning, Info, Success,
    Subtle, Transparent, Light, Dark
}
```

Option B (full split) is deferred to a future major-version ADR if adoption patterns demand it. We have no evidence yet that axis-conflation is hurting consumers — all audit findings collapse to specific missing values, not to missing cross-products.

---

## Consequences

### Positive

- **Fluent and Bootstrap button hierarchies become fully expressible.** SYNTHESIS Batch 1e #23 (Fluent `Subtle`/`Transparent`) and Batch 2a `Light`/`Dark` (BS5) resolve without breaking any existing consumer.
- **Non-breaking for existing call sites.** Adding enum values is safe; no existing `<SunfishButton Variant="Primary" />` usage changes.
- **Provider mapping tables get a clear extension point.** Each provider's `ButtonClass()` grows four new cases; missing a case is a compile-time issue (switch-expression exhaustiveness) rather than a runtime fallback.
- **Axis matrix documented.** The `ButtonVariant × FillMode × ButtonSize` mapping is now a named contract, which simplifies future provider authoring.

### Negative

- **Axis conflation persists.** `Subtle` is really a fill treatment, not a color intent, but we're labeling it a variant. If the split to `ButtonAppearance + ButtonIntent` becomes necessary later, today's `Subtle` value becomes the ambiguity that ADR has to resolve.
- **Every provider must implement every new value.** If a provider forgets to handle `Dark` in its switch expression, the C# compiler warns (exhaustive switch) — but if the provider uses a dictionary, runtime fallback to `default` is silent. SYNTHESIS flagged this class of bug on other enums.
- **React adapter must ship the same values on day one.** Per ADR 0014, the React adapter's Button component accepts the same 10 variant values with parity mappings. Work must be coordinated.
- **Enum growth pattern.** Future frameworks (Ant Design, Chakra, Radix) may request more values. The enum-growth rate is the cost of not picking Option B.
- **compat-telerik mapping.** Telerik has its own button-themecolor ladder (`primary`, `secondary`, `tertiary`, `info`, `success`, `warning`, `error`, `dark`, `light`, `inverse`, `base`). compat-telerik maps Sunfish → Telerik; new values need new mapping entries.

---

## Compatibility plan

1. **Additive release.** Ship as a minor version bump. No deprecation, no migration guide needed — existing consumers keep working.
2. **First-party provider coverage in the same PR.** `BootstrapCssProvider`, `FluentUICssProvider`, `MaterialCssProvider` all get switch-expression arms for the four new values. CI fails if any provider has a non-exhaustive switch.
3. **Parity test.** Add a test in `packages/ui-core/tests/CssProviderContractTests.cs` that iterates every `ButtonVariant` value × every provider × every `ButtonSize` × every `FillMode` and asserts a non-empty class string. ~360 permutations; runs in milliseconds.
4. **Doc-comment mapping table.** Each new enum value gets an XML doc comment describing the target mapping per provider (lifted from the tradeoffs section above).
5. **compat-telerik update.** Map `Subtle → ThemeColor.Tertiary` (closest equivalent), `Transparent → ThemeColor.Base`, `Light → ThemeColor.Light`, `Dark → ThemeColor.Dark`. These are Telerik-native values, so no semantic compromise.
6. **Release note.** Frame as a style-parity fix; lead with "Fluent and Bootstrap skins now render correctly for Subtle/Transparent/Light/Dark."

---

## Implementation checklist

- [ ] Update `packages/foundation/Enums/ButtonVariant.cs` — add four values with XML doc comments.
- [ ] Extend `BootstrapCssProvider.ButtonClass()` with arms for `Subtle`, `Transparent`, `Light`, `Dark`. `Light` → `btn-light`; `Dark` → `btn-dark`; `Subtle` → `btn-outline-secondary`; `Transparent` → `btn-link`.
- [ ] Extend `FluentUICssProvider.ButtonClass()` with arms for the four new values. `Subtle` → `sf-btn-subtle` (Fluent `fui-Button--subtle`); `Transparent` → `sf-btn-transparent`; `Light`/`Dark` → Fluent neutral surface variants.
- [ ] Extend `MaterialCssProvider.ButtonClass()` with arms for the four new values. `Subtle` / `Transparent` → M3 `text` button; `Light` → `outlined`; `Dark` → `filled` on inverse-surface.
- [ ] CSS: author selector blocks for every new `.sf-btn-*` class in all three skins (`sunfish-bootstrap.css`, `sunfish-fluentui.css`, `sunfish-material.css`). Both light and dark modes per the [Theme 6 finding](../../icm/07_review/output/style-audits/SYNTHESIS.md) rules.
- [ ] Add exhaustive-switch parity test in `CssProviderContractTests.cs`.
- [ ] compat-telerik: update `SunfishVariantToTelerikThemeColor()` mapping with the four new values.
- [ ] React adapter: author the parity-equivalent component-prop spec before landing Blazor (per ADR 0014).
- [ ] Update `apps/kitchen-sink` Button demo page with the four new variants (per ADR 0022 catalog).
- [ ] Update `apps/docs` Button API reference to list all 10 variants.
- [ ] Update [`SYNTHESIS.md`](../../icm/07_review/output/style-audits/SYNTHESIS.md) naming — s/`ButtonAppearance`/`ButtonVariant`/g in the narrative.

---

## References

- [ADR 0014](0014-adapter-parity-policy.md) — UI Adapter Parity Policy. Dictates React parity on the same PR.
- [ADR 0022](0022-example-catalog-and-docs-taxonomy.md) — Example Catalog. Button demo coverage (new variants) lands as catalog entries.
- [ADR 0023](0023-dialog-provider-slot-methods.md) — Dialog Provider-Interface Expansion. Parallel contract-expansion ADR for Dialog; same parity-test pattern applies here.
- [`icm/07_review/output/style-audits/SYNTHESIS.md`](../../icm/07_review/output/style-audits/SYNTHESIS.md) — Batch 1e item #23; Batch 2a items for BS5 Light/Dark; cross-cutting decision #5 (the naming question).
- [`icm/07_review/output/style-audits/SunfishButton-FluentUI-v9.md`](../../icm/07_review/output/style-audits/SunfishButton-FluentUI-v9.md) — the audit that first surfaced `Subtle`/`Transparent` need.
- [`packages/foundation/Enums/ButtonVariant.cs`](../../packages/foundation/Enums/ButtonVariant.cs) — the enum being expanded.
- [Fluent UI v9 Button spec](https://react.fluentui.dev/?path=/docs/components-button-button--docs) — `subtle` and `transparent` appearances.
- [Bootstrap 5 Buttons](https://getbootstrap.com/docs/5.3/components/buttons/) — `btn-light`, `btn-dark`, `btn-link`.
- [Material 3 Buttons](https://m3.material.io/components/buttons/overview) — `filled`, `tonal`, `outlined`, `text`, `elevated` (mapped onto Sunfish `FillMode` axis).
