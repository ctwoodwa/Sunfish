# Style Audit — SunfishDialog vs Bootstrap 5.3 Modal

**Pipeline variant:** sunfish-quality-control
**Stage:** 07_review
**Audited component:** `Sunfish.UIAdapters.Blazor.Components.Feedback.SunfishDialog` (Bootstrap skin)
**Reference:** Bootstrap 5.3 Modal — https://getbootstrap.com/docs/5.3/components/modal/
**Docs source:** Context7 `/websites/getbootstrap_5_3` (fetched 2026-04-22)
**Sunfish files audited:**
- `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Feedback\Dialog\SunfishDialog.razor`
- `C:\Projects\Sunfish\packages\ui-adapters-blazor\Providers\Bootstrap\BootstrapCssProvider.cs` (lines 638-646)
- `C:\Projects\Sunfish\packages\ui-adapters-blazor\Providers\Bootstrap\Styles\components\_dialog.scss`
- `C:\Projects\Sunfish\packages\ui-adapters-blazor\Providers\Bootstrap\wwwroot\css\sunfish-bootstrap.css` (compiled — BS5 modal rules present on lines 5454-5745; no `sf-dialog-*` overrides)

---

## TL;DR

The Bootstrap skin of `SunfishDialog` is **structurally non-Bootstrap**. `BootstrapCssProvider.DialogClass()` returns `"modal-dialog"` and `DialogOverlayClass()` returns `"modal-backdrop fade show"`, but the Razor template renders the *inside* of the dialog with proprietary `sf-dialog-title`, `sf-dialog-body`, `sf-dialog-close`, and `sf-dialog-actions` classes — **none of which are styled in the Bootstrap skin** (the compiled stylesheet ships only BS5's own `.modal-*` rules plus a single `.sf-bs-dialog--draggable { cursor: move; }` block). The result is a naked `.modal-dialog` floating in a `.modal-backdrop` with unstyled children and no `.modal`, `.modal-content`, `.modal-header`, `.modal-body`, or `.modal-footer` wrappers. Essentially every BS5 modal feature (sizes, centering, scrollable, fullscreen, fade-in transform, header border, footer gap, close icon, dark mode) is **unreachable** through the current template, regardless of parameter values.

Priority of fixes: P0 structural class mapping → P1 size/centered/scrollable/fullscreen parameters → P2 fade/backdrop state machine and focus trap → P3 dark mode opt-in and `.btn-close` icon parity.

---

## Focus-Area Matrix

| Area | BS5 expectation | Sunfish today | Status |
|---|---|---|---|
| Root element | `<div class="modal fade show" tabindex="-1" aria-labelledby aria-hidden>` wraps everything | Root is `.modal-backdrop fade show` with `.modal-dialog` as direct child | **Missing `.modal` wrapper** — backdrop is hosting the dialog |
| Content chrome | `.modal-content` contains header/body/footer, provides bg, border, radius, shadow | Not emitted — children sit directly inside `.modal-dialog` | **P0** |
| Header | `.modal-header` + `.modal-title` (`h1.fs-5` or `h5`) + `.btn-close` | `.sf-dialog-title` (div) + `<span>` + `<button class="sf-dialog-close">&times;</button>` | **P0** |
| Body | `.modal-body` with `--bs-modal-padding` | `.sf-dialog-body` (unstyled) | **P0** |
| Footer | `.modal-footer` with `--bs-modal-footer-gap: .5rem` | `.sf-dialog-actions` (unstyled) | **P0** |
| Size variants | `.modal-sm` / `.modal-lg` / `.modal-xl` modifiers on `.modal-dialog` | No `Size` parameter; user must pass raw `Width` / `Style` strings | **P1** |
| Centered | `.modal-dialog-centered` | No `Centered` parameter | **P1** |
| Scrollable body | `.modal-dialog-scrollable` | Not exposed; custom `Height` would clip instead of scroll | **P1** |
| Fullscreen | `.modal-fullscreen`, `.modal-fullscreen-{bp}-down` | Not exposed | **P1** |
| Fade animation | `.modal.fade .modal-dialog { transform: translate(0,-50px); transition: transform .3s ease-out; }` → `.show` toggles to `none` | No enter/leave state: dialog pops in/out via `@if Visible` | **P2** |
| Backdrop state | Backdrop `.fade` → `.show` sequenced; z-index 1050; `opacity .5` | Class string hard-codes `fade show` from first frame — no fade-in | **P2** |
| Scroll lock | JS adds `.modal-open` on `<body>` + compensates scrollbar via `padding-right` | No scroll-lock — background scrolls behind dialog | **P2** |
| Focus trap | BS JS traps focus; `aria-hidden` toggled on open/close; initial focus lands inside modal | Not implemented; keyboard tab escapes to page content | **P2** |
| Escape key | `keyboard: true` closes on Esc | Not handled | **P2** |
| Close button | `<button class="btn-close" aria-label="Close">` — CSS masked SVG X icon | `<button class="sf-dialog-close">&times;</button>` — literal multiplication sign | **P3** |
| Dark mode | Inherits via `data-bs-theme="dark"` on root or ancestor | Opt-in not surfaced; custom classes unaware of theme tokens | **P3** |
| a11y labelling | `aria-labelledby` points to title id; `aria-modal="true"`; `tabindex="-1"` on `.modal` | `role="dialog"` + `aria-modal` only; no `aria-labelledby`, no `tabindex` | **P2** |

---

## Prioritised Gap List

### P0 — Structural Mapping Missing (BREAKING for Bootstrap skin)

**Gap 1. No `.modal` wrapper.** BS5 expects a `.modal` root owning backdrop behaviour, stacking context, and `--bs-modal-*` tokens.

> BS5 reference — https://getbootstrap.com/docs/5.3/components/modal/
> ```html
> <div class="modal fade" tabindex="-1" aria-labelledby="..." aria-hidden="true">
>   <div class="modal-dialog">...</div>
> </div>
> ```

**Sunfish today** (`SunfishDialog.razor:8`, `BootstrapCssProvider.cs:646`):
```razor
<div class="modal-backdrop fade show" @onclick="HandleOverlayClick">
    <div class="modal-dialog" role="dialog" aria-modal="true">...</div>
</div>
```
The backdrop *is* the overlay — a `.modal` wrapper never exists.

**Fix:** Split overlay and modal root. `DialogOverlayClass()` should return the backdrop on its own layer (`"modal-backdrop fade show"`); the dialog container should be a sibling `<div class="modal fade show d-block" tabindex="-1" aria-modal="true" aria-labelledby="@_titleId">` holding the `.modal-dialog`. Use `CssProvider.DialogRootClass()` (new) + `DialogClass()` + `DialogBackdropClass()` so the Material/FluentUI skins can render differently.

---

**Gap 2. No `.modal-content` / `.modal-header` / `.modal-body` / `.modal-footer`.** These classes supply padding, borders, radius, shadow, and footer gap via `--bs-modal-*`. Without them the dialog shows zero padding and hairline borders fall back to UA defaults.

> BS5 reference — https://getbootstrap.com/docs/5.3/components/modal/
> ```html
> <div class="modal-dialog">
>   <div class="modal-content">
>     <div class="modal-header">
>       <h1 class="modal-title fs-5">Title</h1>
>       <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
>     </div>
>     <div class="modal-body">...</div>
>     <div class="modal-footer">
>       <button class="btn btn-secondary">Close</button>
>       <button class="btn btn-primary">Save</button>
>     </div>
>   </div>
> </div>
> ```

**Sunfish today** (`SunfishDialog.razor:87-135`): renders `<div class="sf-dialog-title">`, `<div class="sf-dialog-body">`, `<div class="sf-dialog-actions">` directly under `.modal-dialog`. No `sf-dialog-*` rule exists in the Bootstrap skin — only `.sf-bs-dialog--draggable { cursor: move; }` (`_dialog.scss:8-10`). These slots are unstyled in production.

**Fix:** Introduce CSS provider hooks `DialogContentClass()`, `DialogHeaderClass()`, `DialogTitleClass()`, `DialogBodyClass()`, `DialogFooterClass()`, `DialogCloseClass()`. Bootstrap returns `modal-content` / `modal-header` / `modal-title fs-5` / `modal-body` / `modal-footer` / `btn-close`; Material/FluentUI keep their current `sf-dialog-*` vocabulary. The Razor template emits those provider classes instead of hard-coded `sf-dialog-*`.

---

### P1 — Sizing, Centering, Scrollable, Fullscreen (Feature parity)

**Gap 3. No size variants.** BS5 ships four widths via modifier classes on `.modal-dialog` with breakpoint-aware max-widths (sm-up: `--bs-modal-width: 500px` default; `modal-sm: 300px`; `modal-lg / modal-xl: 800px` then `1140px` at `xl`).

> ```html
> <div class="modal-dialog modal-sm">...</div>
> <div class="modal-dialog modal-lg">...</div>
> <div class="modal-dialog modal-xl">...</div>
> ```

**Sunfish today:** Only raw `Width` / `Height` string parameters (`SunfishDialog.razor:54-55`) written to inline `style`. No `Size` enum.

**Fix:** Add `[Parameter] public DialogSize Size { get; set; } = DialogSize.Medium;` (enum: `Small | Medium | Large | ExtraLarge`). Extend `DialogClass(bool draggable, DialogSize size)` in provider to append `modal-sm` / `modal-lg` / `modal-xl`. Keep `Width` as a per-call override — but document that on the Bootstrap skin it overrides the `--bs-modal-width` token, not the class modifier.

---

**Gap 4. No vertical centering.** BS5 provides `.modal-dialog-centered`, which is combinable with `.modal-dialog-scrollable`.

> ```html
> <div class="modal-dialog modal-dialog-centered modal-dialog-scrollable">...</div>
> ```

**Sunfish today:** No `Centered` parameter. Dialog relies on default `.modal-dialog` margin — visually top-biased.

**Fix:** Add `[Parameter] public bool Centered { get; set; }`. Provider appends `modal-dialog-centered` when true.

---

**Gap 5. No scrollable body.** BS5 `.modal-dialog-scrollable` makes `.modal-body` scroll while header/footer stay pinned — critical for long content on short viewports.

**Sunfish today:** Setting `Height` on the root `.modal-dialog` clips content instead (no internal overflow rules are defined for `.sf-dialog-body`).

**Fix:** Add `[Parameter] public bool Scrollable { get; set; }`. Provider appends `modal-dialog-scrollable`. Document that Material/FluentUI should mirror this with their own `overflow:auto` on the body slot.

---

**Gap 6. No fullscreen modes.** BS5 offers `.modal-fullscreen` plus breakpoint-down variants `.modal-fullscreen-{sm|md|lg|xl|xxl}-down` (CSS present in `sunfish-bootstrap.css` lines 5630-5750 — already shipped, just not reachable).

> ```html
> <div class="modal-dialog modal-fullscreen-sm-down">...</div>
> ```

**Sunfish today:** Not exposed.

**Fix:** Add `[Parameter] public DialogFullscreen Fullscreen { get; set; } = DialogFullscreen.None;` with values `None | Always | SmDown | MdDown | LgDown | XlDown | XxlDown`. Provider emits the matching class. Contract lives in `foundation`/`ui-core` so Material/FluentUI can opt to honor it.

---

### P2 — Animation, Backdrop, Focus, Keyboard, a11y (Behaviour parity)

**Gap 7. No enter/leave transition.** BS5 transitions the dialog via `transform: translate(0, -50px)` → `translate(0, 0)` over `.3s ease-out` while the backdrop cross-fades via `opacity`.

> Compiled reference (`sunfish-bootstrap.css:5494-5558`):
> ```css
> .modal.fade .modal-dialog { transition: transform .3s ease-out; transform: translate(0, -50px); }
> .modal.show .modal-dialog { transform: none; }
> .modal-backdrop.fade { opacity: 0; }
> .modal-backdrop.show { opacity: var(--bs-backdrop-opacity); }
> ```

**Sunfish today:** `@if (Visible)` toggles the entire subtree in one tick. `DialogOverlayClass()` hard-codes `modal-backdrop fade show` so `opacity: 0 → .5` never animates.

**Fix:** Two-phase render — track `_isMounted` (just rendered) and `_isShown` (after one frame). Emit `.modal fade` + `.modal-backdrop fade`, then add `.show` on the next frame via JS interop (e.g. `requestAnimationFrame`) or a CSS `display:block` + small `delay(10ms)`. On close, remove `.show` first, wait for `transitionend`, then unmount. This is the single biggest polish win.

---

**Gap 8. No scroll lock on `<body>`.** BS5 JS adds `modal-open` to `<body>` and compensates the scrollbar with `padding-right` so layout does not jank. Sunfish has neither the class nor the padding.

**Fix:** Publish a tiny `sunfishDialog.lock(open)` JS interop in the adapter bundle (`wwwroot/js/`) that sets `document.body.classList.add('modal-open')` and adjusts `paddingRight` to `getScrollbarWidth()`. Expose `[Parameter] public bool LockScroll { get; set; } = true;`.

---

**Gap 9. No focus trap / initial focus / Esc close.** BS5 focuses the modal, traps tab within it, and closes on Esc (`keyboard: true`).

**Fix:** Add keyboard handlers:
- `@onkeydown="HandleKey"` at `.modal` level → close on `"Escape"` when `CloseOnEscape` parameter is true.
- JS interop to trap tab: cache `document.activeElement` on open, focus first `[autofocus]` or first focusable, wrap tab/shift-tab.
- Restore focus on close.

---

**Gap 10. Incomplete a11y wiring.** BS5 expects `aria-labelledby` on the modal referencing the title element, `tabindex="-1"` on the modal root, and that `aria-modal="true"` lives on the element *with* `role="dialog"`. Today Sunfish puts `role="dialog"` on `.modal-dialog` (the inner layout node) rather than the modal root.

**Fix:** Move `role="dialog"` + `aria-modal="true"` + `tabindex="-1"` to the new `.modal` root. Generate `_titleId = $"sf-dialog-title-{Guid.NewGuid():N}"` and emit `aria-labelledby="@_titleId"`. Give `<span>@Title</span>` the matching `id` and the `modal-title fs-5` classes (upgrade from `<span>` to `<h1>` or `<h5>` for screen-reader heading semantics).

---

### P3 — Icon Polish and Dark Mode (Cosmetic parity)

**Gap 11. Non-BS close glyph.** BS5 `.btn-close` renders an SVG X via CSS `background-image` with mask support and inherits dark-mode colour via `.btn-close-white` or the `[data-bs-theme="dark"]` token stack.

**Sunfish today** (`SunfishDialog.razor:94`):
```html
<button type="button" class="sf-dialog-close" aria-label="Close">&times;</button>
```
Literal `&times;` char — no icon, no hover state, no dark-mode contrast (class has no rules in the Bootstrap skin).

**Fix:** Provider returns `"btn-close"` from `DialogCloseClass()` on Bootstrap; Razor drops the `&times;` text node. Material returns `"sf-dialog-close"` with its existing icon glyph; FluentUI mirrors.

---

**Gap 12. Dark mode not wired.** BS5 supports `data-bs-theme="dark"` on the modal itself or any ancestor. The compiled Sunfish stylesheet already has `[data-bs-theme=dark]` selectors for scheduler backdrops (line 17039) but the dialog never emits a theme attribute.

**Fix:** Add `[Parameter] public string? Theme { get; set; }` (`"light"` | `"dark"` | null). When non-null, emit `data-bs-theme="@Theme"` on the `.modal` root. Default null keeps ancestor inheritance.

---

## Additional Observations

- **Overlay click swallowing:** `HandleOverlayClick` uses `@onclick:stopPropagation="true"` on the inner content — correct — but because today's overlay *is* the backdrop, clicking the dialog gutter (between `.modal-dialog` and the viewport edge) can close the dialog unexpectedly. With a `.modal` root on top of a separate `.modal-backdrop`, static-backdrop semantics (`data-bs-backdrop="static"`) also become implementable via a `StaticBackdrop` parameter.
- **Draggable:** `sf-bs-dialog--draggable { cursor: move; }` is only a cursor hint — no JS drag behaviour exists in the Bootstrap skin. Either wire it up or document `Draggable` as Material-only.
- **Sass partial is a stub.** `Providers/Bootstrap/Styles/components/_dialog.scss` is 11 lines and carries only the draggable cursor. Comment on line 4 says "Bootstrap's modal component handles dialogs natively" — that assumption holds only once the template uses BS5 classes end-to-end. Today it is false.
- **Two-way binding:** `Visible` / `VisibleChanged` is fine; add it to the proposed interop so JS-side close (Esc, backdrop) round-trips back to the bound field via `InvokeAsync`.

---

## Recommended Remediation Order

1. **P0 (one PR):** Extend `ISunfishCssProvider` with `DialogRootClass`, `DialogContentClass`, `DialogHeaderClass`, `DialogTitleClass`, `DialogBodyClass`, `DialogFooterClass`, `DialogCloseClass`, `DialogBackdropClass`. Update Bootstrap/Material/FluentUI providers. Rewrite `SunfishDialog.razor` to consume them. Parity tests assert each skin renders its expected class list.
2. **P1 (one PR):** Add `Size`, `Centered`, `Scrollable`, `Fullscreen` parameters; wire through provider class methods. Add kitchen-sink demos for each variant.
3. **P2 (sequence of PRs):** (a) fade transition via deferred `.show` toggle + `transitionend`, (b) scroll-lock JS interop, (c) focus trap + Esc handler, (d) `aria-labelledby` / `tabindex` / move `role="dialog"` to root.
4. **P3 (one PR):** `btn-close` glyph via provider swap, `Theme` parameter for `data-bs-theme`.

Each PR is pipeline `sunfish-api-change` (P0-P2 modify the ICssProvider contract) or `sunfish-feature-change` (P3). Breaking-change note required for P0.

---

## References

- Bootstrap 5.3 Modal component — https://getbootstrap.com/docs/5.3/components/modal/
- Bootstrap 5.3 Color modes — https://getbootstrap.com/docs/5.3/customize/color-modes/
- Compiled proof-of-rules: `packages/ui-adapters-blazor/Providers/Bootstrap/wwwroot/css/sunfish-bootstrap.css` lines 5454-5745
- Sunfish provider surface: `packages/ui-adapters-blazor/Providers/Bootstrap/BootstrapCssProvider.cs:638-646`
- Template: `packages/ui-adapters-blazor/Components/Feedback/Dialog/SunfishDialog.razor:87-135`

**Word count:** ~1,650.
