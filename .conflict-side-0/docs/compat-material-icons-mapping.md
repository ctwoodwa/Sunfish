# compat-material-icons Parameter Mapping & Divergence Log

> **Audit trail — treated as public API.** Consumers rely on this document to understand
> what their Material Icons / Material Symbols code maps to after migration. Any change
> to an entry (promoting a parameter from "mapped" to "throws", changing a default,
> adding a divergence, expanding the starter-set typed-icon constants) is a **breaking
> change** for consumers and must land under the policy gate in the same PR as the code
> change. See `packages/compat-material-icons/POLICY.md`.

## Conventions

- **Mapped** — Material parameter value translates 1:1 to a Sunfish parameter / value.
- **Forwarded** — Attribute is passed through via `AdditionalAttributes` (e.g. `class`,
  `style`, `title`, `tabindex`). No semantic transform.
- **LogAndFallback** — Value is accepted but not implemented; behavior falls back to a
  default. Reserved for cosmetic / axis-level parameters with no functional impact.
- **Throws** — Raises `NotSupportedException` with a migration hint. Applies to values
  that would silently change behavior if dropped, or that are out of scope for Phase 2.
- **Not-in-scope** — Intentionally not mirrored; consumers who need the feature should
  pass through `AdditionalAttributes` / `style` or maintain a parallel code path.

---

## MaterialIcon (legacy Material Icons)

- **Material target:** `<span class="material-icons">home</span>` — Google's official
  CSS-ligature rendering path; also the shape that `Blazorise.Icons.Material` and
  `MudBlazor.FontIcons.MaterialIcons` emit when consumed through MudBlazor's `MudIcon`.
- **Sunfish target:** Emits a `<span class="material-icons sf-material-icon ...">`
  directly — `material-icons` stays for Google Fonts to match its CSS selector, and the
  `sf-material-icon` prefix is a Sunfish hook consumers can style against.

| Material surface | Type | Sunfish mapping | Status |
|---|---|---|---|
| Ligature text (`home`, etc.) | `string?` via `Name` | Set as element text content → resolved by Google's webfont via ligatures. | Supported |
| Size (MudBlazor `Size="Size.Small"`) | `IconSize?` | `Small`→`sf-material-size-sm`, `Medium`→`sf-material-size-md`, `Large`→`sf-material-size-lg`, `ExtraLarge`→`sf-material-size-xl`. Omit to inherit. | Supported |
| `AriaLabel` | `string?` | Emitted as `aria-label`; adds `role="img"` when present. | Supported |
| `Class` / `Style` / arbitrary attributes | — | Splatted via `AdditionalAttributes` onto the host `<span>`. | Forwarded |
| MudBlazor `Color` (e.g. `Color.Primary`) | — | Not mirrored — Sunfish has no per-icon color primitive in Phase 2. | Not-in-scope |
| MudBlazor `ViewBox` / SVG-sizing | — | N/A — Material Icons uses font glyphs, not SVG, through this wrapper. | Not-in-scope |

### Divergences

- **Glyph visuals require the consumer's font.** If the Material Icons webfont is not
  loaded (Google Fonts `<link>` or self-hosted `@font-face`), the raw ligature text
  (e.g. `home`) is shown — same failure mode as Google's official rendering path.
- **No SVG rendering path.** Some downstream wrappers (MudBlazor) can switch between
  CSS-ligature and inline-SVG rendering. This compat surface only supports the
  CSS-ligature path; SVG rendering is deferred and requires a `ui-core` contract
  extension.
- **MudBlazor `Color` is not mirrored.** Consumers who relied on `Color="Color.Primary"`
  must migrate to CSS (`style="color: var(--sf-primary)"` or an `AdditionalAttributes`
  class) until Sunfish adds a first-class icon-color contract.

---

## MaterialSymbol (newer Material Symbols)

- **Material target:** `<span class="material-symbols-outlined">home</span>` (and
  `-rounded` / `-sharp` variants) — Google's Material Symbols rendering path; also what
  `MudBlazor.FontIcons.MaterialSymbols` produces.
- **Sunfish target:** Emits a `<span class="material-symbols-<variant> sf-material-symbol ...">`
  directly.

| Material surface | Type | Sunfish mapping | Status |
|---|---|---|---|
| Ligature text | `string?` via `Name` | Element text content. | Supported |
| `Variant` (class-level) | `MaterialSymbolVariant` | `Outlined`→`material-symbols-outlined` (default), `Rounded`→`material-symbols-rounded`, `Sharp`→`material-symbols-sharp`. | Supported |
| `Size` | `IconSize?` | Same mapping as `MaterialIcon.Size`. | Supported |
| `AriaLabel` | `string?` | Emitted as `aria-label`; adds `role="img"` when present. | Supported |
| Arbitrary attributes | — | Splatted via `AdditionalAttributes`. | Forwarded |
| `FILL` axis (0/1) | — | Not mirrored in Phase 2 — pass `style="font-variation-settings: 'FILL' 1"` via `AdditionalAttributes`. | Not-in-scope |
| `wght` axis (100–700) | — | Not mirrored in Phase 2 — pass via `style="font-variation-settings: 'wght' 400"`. | Not-in-scope |
| `GRAD` axis | — | Not mirrored in Phase 2. | Not-in-scope |
| `opsz` axis (20/24/40/48) | — | Not mirrored in Phase 2. | Not-in-scope |
| Code-point rendering (vs ligature) | — | Not mirrored — only ligature rendering is supported. | Not-in-scope |

### Divergences

- **Only three class-level variants are mirrored.** Google publishes Outlined / Rounded
  / Sharp as distinct variable-font files, each selected via its CSS class. The
  variable-font axes (FILL / wght / GRAD / opsz) are not mirrored in Phase 2 because
  Sunfish has no per-axis contract; consumers can still drive them via
  `AdditionalAttributes` pass-through.
- **Default variant is Outlined.** An unparameterized `<MaterialSymbol Name="home" />`
  renders as `material-symbols-outlined`, matching Google's documented default and
  MudBlazor's `MaterialSymbols.Outlined.*` static class convention.

---

## Typed Identifier Constants (`MaterialIconName`)

- **Material source:** MudBlazor exposes
  `MaterialIcons.{Outlined|Filled|Rounded|Sharp|TwoTone}.<Name>` and
  `MaterialSymbols.{Outlined|Rounded|Sharp}.<Name>` — one static class per variant,
  hundreds of members each.
- **Sunfish surface:** A single flat `MaterialIconName` class of `const string`
  ligature identifiers (50-entry starter set). The ligature name is the same across
  all variants — the variant is selected by choosing `MaterialIcon` vs
  `MaterialSymbol` plus the `Variant` parameter — so we do not duplicate the constants
  per variant.

### Divergences

- **Flat, not nested.** `MudBlazor` consumers using `MaterialIcons.Filled.Home`
  migrate to `MaterialIconName.Home` + `<MaterialIcon Name="@MaterialIconName.Home" />`.
  There is no `MaterialIconName.Filled.Home` form.
- **50-entry starter set, not the full catalog.** The Material catalog exceeds 2,500
  icons; this starter set covers the icons most migrators reach for first. Consumers
  with broader needs pass arbitrary ligature strings directly to
  `MaterialIcon.Name` / `MaterialSymbol.Name` — the identifier is forwarded verbatim
  to the Google webfont via CSS ligatures.
- **No codepoint constants.** Only the ligature form is exposed; code-point-based
  rendering is out of scope (see Not-in-scope rows above).

---

## Asset Shipping — Consumer Responsibility

`compat-material-icons` **does not ship any font files, CSS, or `<link>` tag**. The
consumer remains responsible for loading the Material Icons / Material Symbols webfont
via one of:

- Google Fonts `<link>` in `index.html` / `App.razor`
- Self-hosted variable-font file plus `@font-face` declaration
- An existing MudBlazor / Blazorise layout (no change needed during migration)

If the webfont is not loaded, the raw ligature text (e.g. `home`) is visible — same
failure mode as Google's documented rendering path. This is intentional: hard
invariant #1 (no vendor NuGet dependency) means no asset shipping either.

---

## Future Coverage

Candidates for future policy-gated PRs:

- **Source-generated typed catalog** covering all ~2,500 Material icons. Hand-maintenance
  does not scale past the current 50-entry starter set.
- **Per-axis parameters** on `MaterialSymbol` (`Fill`, `Weight`, `Grade`, `OpticalSize`)
  once Sunfish exposes a first-class axis contract.
- **MudBlazor `Color` compat** once Sunfish adds an icon-color primitive.
- **Code-point rendering** as an alternative to ligatures (for i18n-sensitive apps
  where ligature matching misfires).
- **Filled-variant compat** — MudBlazor exposes `MaterialIcons.Filled.*` as a distinct
  class. Currently `MaterialIcon` (legacy) emits the filled base class; a future
  `MaterialIconVariant` enum could expose Outlined / Filled / Rounded / Sharp / TwoTone
  for the legacy path to match MudBlazor's shape exactly.
