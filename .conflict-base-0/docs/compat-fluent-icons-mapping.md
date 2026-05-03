# compat-fluent-icons Parameter Mapping & Divergence Log

> **Audit trail — treated as public API.** Consumers rely on this document to understand
> what their Fluent UI System Icons code maps to after migration. Any change to an entry
> (promoting a parameter from "mapped" to "throws", changing a default, adding a
> divergence, expanding the starter-set typed-icon classes, or adding a new `Size*`
> bucket) is a **breaking change** for consumers and must land under the policy gate in
> the same PR as the code change. See `packages/compat-fluent-icons/POLICY.md`.

## Conventions

- **Mapped** — Fluent parameter value translates 1:1 to a Sunfish parameter / value.
- **Forwarded** — Fluent attribute is passed through via `AdditionalAttributes` (e.g.
  `class`, `style`, `tabindex`). No semantic transform.
- **LogAndFallback** — Value is accepted but not implemented; a warning is logged via
  `ILogger` and rendering falls back to the default behavior. Reserved for cosmetic /
  slot parameters with no functional impact on the glyph itself.
- **Throws** — Raises `NotSupportedException` with a migration hint. Applies to values
  that would silently change behavior if dropped, or that are out of scope for Phase 2A.
- **Not-in-scope** — Feature intentionally not mirrored; consumers must follow the
  documented alternative.

---

## FluentIcon

- **Fluent target:** `<FluentIcon Value="@(new Size20.Regular.Home())" />`
  (Microsoft.FluentUI.AspNetCore.Components.Icons source shape)
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Utility.SunfishIcon`

| Fluent parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Value` | `object?` | Typed icon → duck-typed `Name` property → `SunfishIcon.Name`. `string` → `SunfishIcon.Name`. `RenderFragment` → `SunfishIcon.Icon`. Other → normalized via `CompatIconAdapter.ToRenderFragment`. | Supported |
| `Size` | `IconSize?` (Sunfish enum) | When set, passed directly to `SunfishIcon.Size`. When `null`, the compat wrapper infers from the `Value` type's namespace (e.g. `Size20` → Medium). | Supported |
| `AriaLabel` | `string?` | `SunfishIcon.AriaLabel`. | Supported |
| `Slot` | `string?` | Logged and dropped; Sunfish does not model slot-named icons at this layer. | LogAndFallback |
| `Title` | `string?` | Logged and dropped; use HTML `title` via `AdditionalAttributes`. | LogAndFallback |
| `Color` | `string?` (Fluent palette token) | Throws. Use `IconThemeColor` on a Sunfish-native path, or style via CSS. | Throws |
| `CustomColor` | `string?` (CSS color) | Throws. Pass a `style` attribute via `AdditionalAttributes` instead. | Throws |
| `Width` | `string?` (`"24px"` style) | Throws. Sunfish `IconSize` is enum-only in Phase 2A. | Throws |
| `ChildContent` | `RenderFragment?` | Forwarded when `Value` is null. | Supported |
| `Class` / other attributes | — | Forwarded via `AdditionalAttributes`. | Forwarded |

### Fluent pixel-size → Sunfish IconSize mapping

When `Size` is `null` and `Value` is a typed Fluent icon, the compat wrapper extracts
the pixel size from the namespace segment (`Size20` → 20) and buckets into Sunfish's
four-step `IconSize` enum:

| Fluent pixel size | Sunfish `IconSize` |
|---:|---|
| 10, 12 | Small |
| 16, 20 | Medium |
| 24, 28 | Large |
| 32, 48 | ExtraLarge |

Phase 2A ships the `Size20` bucket only; the mapping table is wired to accept the
remaining buckets when they land (future policy-gated PRs).

### Divergences

- **Glyph visuals differ when the active provider is not Fluent.** The rendered icon is
  whatever the active Sunfish `ISunfishIconProvider` resolves for the identifier — which
  may visually differ from Fluent's own glyph. Consumers keeping Fluent UI's Blazor
  package loaded alongside the compat shim will still get Fluent's glyph where the
  provider returns HTML Fluent's styles recognize; otherwise the provider's own rendering
  applies.
- **Regular vs. Filled variant distinction is not carried through.** Both
  `new Size20.Regular.Home()` and `new Size20.Filled.Home()` resolve to the same kebab-
  case identifier `"home"`. Whether the active Sunfish provider differentiates filled
  vs. outline glyphs is provider-dependent. A future Phase can introduce a
  `"home:filled"` suffix convention if provider demand emerges.
- **Pixel-precise size control not supported.** Fluent lets consumers override
  rendered size via `Width` / `Height` or by choosing a specific `Size*` bucket.
  Sunfish maps into a four-step enum only. Consumers needing pixel-precise control
  should style via CSS on the host element.
- **Palette color tokens not translated.** Fluent's `Color="Accent"` idiom has no
  automatic Sunfish translation — these throw rather than silently dropping, since
  dropping would change the rendered appearance.

---

## Typed identifier lattice: Size20.Regular / Size20.Filled

Phase 2A ships a **50-icon starter set per variant** in the `Size20` bucket only:

| Class | Bucket | Variant | Member count | Fluent full catalog (rough) |
|---|---|---|---:|---:|
| `Size20.Regular` | 20px | Regular (outline) | 50 | ~2,000 per variant × 8 sizes = 60,000+ types |
| `Size20.Filled` | 20px | Filled (solid) | 50 | ~2,000 per variant × 8 sizes = 60,000+ types |

Consumers whose icons are not in the starter set can:

1. Pass a plain string literal directly:
   ```razor
   <FluentIcon Value="@(\"rocket\")" />
   ```
2. Submit a policy-gated PR to extend the starter class, or to add a new `Size*` bucket
   (`Size16`, `Size24`, etc.).

### Starter set (Phase 2A)

Both `Size20.Regular` and `Size20.Filled` ship these 50 icons. The variant distinction
lives at the nested-class-path level; both map to the same kebab-case identifier:

| Category | Icons |
|---|---|
| Core UI / navigation | `Home`, `Search`, `Settings`, `Person`, `Navigation`, `Grid`, `List`, `Filter`, `Sort` |
| Communication | `Mail`, `Chat`, `Phone` |
| Time | `Calendar`, `Clock` |
| Files / media | `Folder`, `Document`, `Image`, `Video`, `Music` |
| File actions | `Save`, `Edit`, `Delete`, `Add`, `Dismiss`, `Copy`, `Print`, `Download`, `Upload`, `Share` |
| Arrows | `ArrowUp`, `ArrowDown`, `ArrowLeft`, `ArrowRight`, `ChevronUp`, `ChevronDown` |
| Favorites / social | `Heart`, `Bookmark` |
| Status / feedback | `Info`, `Warning`, `ErrorCircle`, `CheckmarkCircle` |
| Media control | `Play`, `Pause`, `Stop`, `VolumeUp` |
| Visibility / security | `Eye`, `EyeOff`, `Lock`, `Unlock`, `Key` |

### Divergences

- **Partial lattice only.** Fluent's full `Size × Variant × Name` lattice generates tens
  of thousands of types. Shipping the full lattice would inflate the package and lock
  us into Fluent's catalog evolution cadence. The starter set is intentionally scoped;
  expanding it (or adding new `Size*` buckets) is cheap but deliberate and policy-gated.
- **`Size20` only.** The Fluent UI ecosystem treats 20px as the default icon size for
  mid-density UI chrome. Phase 2A ships this one bucket; other buckets are out of scope
  for the initial release.
- **Duck-typed `Name` contract.** Each icon class exposes a `Name` property
  (kebab-case). This is the contract `FluentIcon` relies on — adding new icon classes
  must preserve that shape.

---

## Out-of-scope features (Not-in-scope)

The following Fluent UI System Icons features are explicitly **not** mirrored in Phase 2A:

| Fluent feature | Reason |
|---|---|
| `Icon.FromImageUrl(url)` factory | Requires a Sunfish contract decision for URL-to-fragment resolution |
| Additional `Size*` buckets (10/12/16/24/28/32/48) | Out of scope for Phase 2A; extend under policy gate |
| `Width` / `Height` pixel-precise sizing | Sunfish `IconSize` enum is four-step only |
| `Color` palette tokens | No automatic Fluent-palette-to-Sunfish-IconThemeColor translation |
| `CustomColor` CSS color strings | Pass `style` via `AdditionalAttributes` instead |
| `OnClick` and interaction parameters | Use a Sunfish button wrapper; icons are presentational |

Consumers using these features should evaluate whether the Sunfish icon-provider
ecosystem meets their needs, pass a plain string identifier, or maintain a parallel
Fluent UI Icons code path outside the compat package.

---

## Future coverage

Deferred to follow-up PRs (track as separate ICM intake items if prioritized):

- **Additional `Size*` buckets** — `Size16` and `Size24` are the most likely next
  candidates based on Fluent UI usage patterns.
- **Starter-set expansion** — per-request additions under the policy gate.
- **Variant distinction plumbing** — optional `":filled"` / `":regular"` suffix
  convention so providers that differentiate can resolve the richer identifier.
- **`Icon.FromImageUrl` factory** — requires a Sunfish contract extension.
- **Pixel-precise sizing** — requires a `ui-core` `SunfishIcon.SizePx` (or similar)
  contract extension.
- **Color palette translation table** — a Fluent-palette-token to `IconThemeColor` map
  so `Color="Accent"` resolves without a throw.

---

## Notes for compat-icon-expansion Phase 2B/2C/Phase 3

These notes seed the architecture decisions for the remaining icon-compat packages:

1. **Duck-typed `Name` contract beats reflection on nested-class paths.** Phase 2A
   settled on reading a single `Name` property off the `Value` instance rather than
   walking the nested type path (e.g. `Size20.Regular.Home` → `"size20-regular-home"`).
   The `Name` property is simpler, cheaper, and gives the typed-icon classes a single
   authoritative source of truth. Phase 2B (material) / 2C (bootstrap) / Phase 3
   packages should use the same pattern unless the upstream vendor exposes a different
   identifier contract.
2. **Size extraction via namespace-segment parsing.** Fluent's `Size20` / `Size24` /
   etc. naming convention lets the compat wrapper infer a default `IconSize` without
   requiring the consumer to restate it. For vendors that don't encode size in the
   type lattice (FA, Heroicons), this path is simply skipped — the explicit `Size`
   parameter remains authoritative.
3. **Starter-set strategy is the shared default.** Match Phase 1 (`compat-font-awesome`
   ships 50 per family). Phase 2A ships 50 per variant in one size bucket. Phase 2B
   (material) should pick one Variant discriminator (Filled / Outlined) as the default
   bucket and ship 50 there; Phase 2C (bootstrap) is simpler — one flat set of 50.
4. **`UnsupportedParam.Throw` for semantics-changing parameters.** Consistent with the
   shared POLICY invariant #3. `Color` / `CustomColor` / `Width` throw rather than
   log-and-drop because silently removing them changes what the user sees on screen.
5. **LogAndFallback for slot/tooltip parameters.** `Slot` / `Title` are presentational
   metadata — dropping them doesn't change the glyph, only the surrounding structural
   semantics. This follows the same classification rubric used in
   `compat-font-awesome` for `FixedWidth` / `Pull` / `Border`.
