# compat-font-awesome Parameter Mapping & Divergence Log

> **Audit trail — treated as public API.** Consumers rely on this document to understand
> what their Font Awesome code maps to after migration. Any change to an entry
> (promoting a parameter from "mapped" to "throws", changing a default, adding a
> divergence, expanding the starter-set typed-icon classes) is a **breaking change** for
> consumers and must land under the policy gate in the same PR as the code change. See
> `packages/compat-font-awesome/POLICY.md`.

## Conventions

- **Mapped** — FA parameter value translates 1:1 to a Sunfish parameter / value.
- **Forwarded** — FA attribute is passed through via `AdditionalAttributes` (e.g.
  `class`, `style`, `tabindex`). No semantic transform.
- **LogAndFallback** — Value is accepted but not implemented; a warning is logged via
  `ILogger` and rendering falls back to the default behavior. Reserved for cosmetic /
  animation parameters with no functional impact.
- **Throws** — Raises `NotSupportedException` with a migration hint. Applies to values
  that would silently change behavior if dropped, or that are out of scope for Phase 1.
- **Not-in-scope** — Pro-only feature intentionally not mirrored. Consumers must
  maintain a parallel FA-Pro code path.

---

## FontAwesomeIcon

- **FA target:** `<FontAwesomeIcon Icon="@FasIcons.Star" Size="lg" />` (historical
  `Blazored.FontAwesome` / `Blazicons.FontAwesome` source shape)
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Utility.SunfishIcon`

| FA parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Icon` | `object?` | `string` → `SunfishIcon.Name`; `RenderFragment` → `SunfishIcon.Icon`; other → normalized via `CompatIconAdapter.ToRenderFragment`. | Supported |
| `Size` | `string?` | `"xs"/"sm"/"1x"`→Small, `"md"/"2x"`→Medium, `"lg"/"3x"`→Large, `"xl"/"2xl"/"4x"/"5x"/"6x"`→ExtraLarge. | Supported |
| `Size` | `string?` | `"7x"/"8x"/"9x"/"10x"` | Throws (no Sunfish equivalent; consider scaling via CSS on the host element) |
| `AriaLabel` | `string?` | `SunfishIcon.AriaLabel`. | Supported |
| `FixedWidth` | `bool` | Sunfish has no fixed-width mode. | LogAndFallback |
| `ListItem` | `bool` | Parent `<FaList>` owns list semantics. | LogAndFallback |
| `Pull` | `string?` (`"left"`/`"right"`) | Sunfish has no float/pull model. | LogAndFallback |
| `Border` | `bool` | No built-in bordered-icon mode. | LogAndFallback |
| `Rotation` | `int?` | 90/180/270 → LogAndFallback (not rendered in Phase 1). Other values → Throws. | LogAndFallback / Throws |
| `Flip` | `string?` | Sunfish `IconFlip` enum not exposed through this wrapper in Phase 1. | LogAndFallback |
| `Spin` | `bool` | Animation not modeled by providers. | LogAndFallback |
| `Pulse` | `bool` | Animation not modeled by providers. | LogAndFallback |
| `Transform` | `string?` | FA transform mini-language. | Throws (Phase 1 scope) |
| `ChildContent` | `RenderFragment?` | Forwarded when `Icon` is null. | Supported |
| `Class` / other attributes | — | Forwarded via `AdditionalAttributes`. | Forwarded |

### Divergences

- **Glyph visuals differ.** The rendered icon is whatever the active Sunfish
  `ISunfishIconProvider` resolves for the identifier — which may visually differ from
  Font Awesome's glyph. Consumers keeping FA's CSS/font loaded will still get the FA
  glyph where the provider returns HTML that FA's styles recognize; otherwise the
  provider's own rendering applies.
- **`Spin` / `Pulse` / `Rotation` / `Flip` are visual-only dropped.** These parameters
  are logged and ignored rather than throwing because their absence does not change
  functional behavior (the icon still renders). Promoting any of them to a fully-
  rendered state requires a new policy-gated PR (and possibly a `ui-core` contract
  extension to `SunfishIcon`).
- **`Transform` throws** because the mini-language (`"shrink-6 rotate-45 up-2"`) can
  functionally change which glyph the user sees (e.g. combining icons via masking).
  Silent-dropping would hide that migration signal.

---

## FaList

- **FA target:** `<FaList>` — a `<ul class="fa-ul">` wrapper.
- **Sunfish target:** no native component — renders a `<ul>` with class
  `sf-fa-list fa-ul` (the `fa-ul` class is preserved so consumer CSS keyed to FA styles
  keeps working).

| FA parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `ChildContent` | `RenderFragment?` | Forwarded verbatim. | Supported |
| `Class` / other attributes | — | `class` is merged with `sf-fa-list fa-ul`; others forwarded. | Forwarded |

### Divergences

- Sunfish has no first-class icon-list primitive; the container is a plain `<ul>`
  with the FA-compat class preserved. Consumers relying on FA's spacing styles should
  keep `fa-ul`'s CSS loaded in `index.html`.

---

## FaListItem

- **FA target:** `<FaListItem Icon="@(...)">Text</FaListItem>`
- **Sunfish target:** `<li>` with a nested `<span class="fa-li"><FontAwesomeIcon /></span>`.

| FA parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Icon` | `object?` | Forwarded to nested `<FontAwesomeIcon Icon>`. | Supported |
| `Size` | `string?` | Forwarded to nested `<FontAwesomeIcon Size>`. | Supported |
| `ChildContent` | `RenderFragment?` | Rendered as the item's text content. | Supported |
| `Class` / other attributes | — | `class` merged; others forwarded. | Forwarded |

### Divergences

- Declares a `[CascadingParameter] FaList? Parent` but does not assert parent presence
  in Phase 1. A later PR may throw when rendered outside `<FaList>` (matching compat-
  shared's `CompatChildComponent` pattern).

---

## FaLayers / FaLayersText / FaLayersCounter

- **FA target:** Nested stack of positioned overlays — e.g.
  ```razor
  <FaLayers>
      <FontAwesomeIcon Icon="circle" />
      <FaLayersText Value="!" />
      <FaLayersCounter Value="7" />
  </FaLayers>
  ```
- **Sunfish target:** plain `<span class="sf-fa-layers fa-layers fa-fw">` container with
  inner `<span class="fa-layers-text">` / `<span class="fa-layers-counter">` children.
  The FA class names are preserved so consumer CSS keeps working.

| Component | Parameter | Status |
|---|---|---|
| `FaLayers` | `Size` | Currently forwarded as a hint; LogAndFallback internally. |
| `FaLayers` | `ChildContent` | Supported |
| `FaLayersText` | `Value` (`string?`) | Supported |
| `FaLayersText` | `ChildContent` | Supported (used when `Value` is null/empty) |
| `FaLayersCounter` | `Value` (`string?`) | Supported |
| `FaLayersCounter` | `ChildContent` | Supported |
| All three | `Class` / other attributes | Forwarded |

### Divergences

- **Positioning relies on FA's own CSS** (`fa-layers`, `fa-layers-text`,
  `fa-layers-counter`). Consumers who have fully migrated off FA's CSS will see the
  overlays rendered un-positioned. A future Phase can ship a `sf-fa-layers` stylesheet
  that replicates the positioning independently of FA's CSS.
- `FaLayers.Size` is accepted for source-shape parity but not plumbed to the inner
  icons in Phase 1. Consumers should set `Size` on each child `<FontAwesomeIcon>`.

---

## Typed identifier classes: FasIcons / FarIcons / FabIcons

Phase 1 ships a **50-icon starter set per style family**:

| Class | Family | Member count | Full FA catalog size |
|---|---|---:|---:|
| `FasIcons` | Solid (`fas`) | 50 | 2,000+ |
| `FarIcons` | Regular (`far`) | 50 | ~150 (Free tier) |
| `FabIcons` | Brands (`fab`) | 50 | ~500 |

Consumers whose icons are not in the starter set can:

1. Pass a string literal directly: `<FontAwesomeIcon Icon="@(&quot;rocket&quot;)" />`.
2. Submit a policy-gated PR to extend the starter class.

### Divergences

- **Not the full catalog.** Shipping 2,000+ string constants for Solid alone would
  inflate the package and lock us into FA's naming evolutions. The starter set is
  intentionally scoped to the most common icons; expanding it is cheap but deliberate.
- **Brand-icon trademarks.** `FabIcons` only stores kebab-case names; the actual
  brand marks remain the trademark of their respective owners and are not bundled.

---

## Pro-only features (Not-in-scope)

Per `packages/compat-font-awesome/POLICY.md`, the following Font Awesome **Pro** tier
features are explicitly **not mirrored**:

| FA Pro feature | Reason |
|---|---|
| `FaDuotoneIcon` / Duotone family | Pro-only — no free equivalent to delegate to |
| Sharp family (`fa-sharp`, `FaSharpIcon`) | Pro-only |
| Chisel family | Pro-only |
| Thin family | Pro-only |
| Pro-only icon identifiers | No free resolution path |
| Kit Code integration (`FaConfig`) | Out of scope — Phase 1 does not configure FA globally |

Consumers using these features should evaluate whether the Sunfish icon-provider
ecosystem meets their needs, or maintain a parallel FA-Pro code path outside the compat
package.

---

## Future coverage

Deferred to follow-up PRs (track as separate ICM intake items if prioritized):

- **Deeper `Transform` support** — rotate / flip / grow / shrink via a Sunfish-native
  contract extension on `SunfishIcon`.
- **`Spin` / `Pulse` rendering** — currently LogAndFallback. Requires a `ui-core`
  decision on animation primitives.
- **`Rotation` rendering** — currently LogAndFallback for 90/180/270. Requires the
  same animation-primitive decision.
- **`Flip` rendering** — the Sunfish `IconFlip` enum exists on `SunfishIcon` already;
  a Phase-2 PR can wire the FA `Flip` string through it.
- **Typed-icon starter-set expansion** — per-request additions under the policy gate.
- **`FaConfig` / global FA configuration shim** — e.g. a default `FixedWidth` flag
  propagated via cascading parameter. Out of scope for Phase 1.
- **`FaList` / `FaLayers` standalone CSS** — a `sf-fa-list.css` stylesheet so
  consumers who drop FA's CSS entirely keep list and layer positioning.

---

## Notes for compat-icon-expansion Phase 2/3

These notes seed the architecture decisions for the icon-compat packages shipping
after Phase 1:

1. **Typed-icon starter-set strategy** — we ship 50 icons per style family rather than
   the full catalog. This keeps the package small but means consumers may need to
   either extend the class (policy-gated PR) or fall back to string literals. Phase 2/3
   packages (`compat-fluent-icons`, `compat-material-icons`, `compat-bootstrap-icons`,
   `compat-lucide`, …) should adopt the same starter-set strategy unless the upstream
   catalog is small enough to ship completely.
2. **Parent-less child components** — `FaListItem` does not enforce the
   `<FaList>` parent in Phase 1. When a second icon-compat package needs an
   enforced parent-child relationship, lift a `CompatIconChildComponent` base into
   `compat-shared` rather than forking `CompatChildComponent`.
3. **CSS-class preservation** — `FaList` / `FaLayers` preserve the FA-native CSS
   classes (`fa-ul`, `fa-layers`, etc.) alongside the Sunfish-prefixed ones
   (`sf-fa-list`, `sf-fa-layers`). This lets migrators keep FA's stylesheet loaded
   without visual regressions. The same split-class pattern is a safe default for
   every icon-compat package.
4. **`UnsupportedParam.Throw` for out-of-scope features** — consistent with the
   shared POLICY invariant #3. Phase-2 icon packages should use the same helper.
