# SunfishWindow Gap Analysis

## Summary

The current `SunfishWindow.razor` implementation covers basic window functionality (visibility, modality, title bar with actions, drag hint, positioning, sizing) but is missing a significant number of features documented in the spec. The component implements a simplified version of the Window with a `[Flags] enum`-based action model instead of the spec's compositional `<WindowActions>/<WindowAction>` child-component pattern. Many documented parameters, events, child tags, and behaviors are absent.

---

## Spec to Code Gaps (Documented but not correctly implemented)

### Parameters

| Gap | Severity | Details |
|-----|----------|---------|
| `CloseOnOverlayClick` parameter missing | **[High]** | Docs (`modal.md`, `overview.md`) describe a `CloseOnOverlayClick` bool parameter. The implementation hardcodes overlay click to always close if `WindowAction.Close` is in `Actions`. There is no way to disable overlay-click-to-close, nor is it opt-in as the spec requires. |
| `ContainmentSelector` parameter missing | **[High]** | Docs (`position.md`, `overview.md`) describe a `ContainmentSelector` string parameter to restrict dragging/resizing to a container. Not implemented. |
| `Resizable` parameter missing | **[High]** | Docs (`size.md`, `overview.md`) describe a `Resizable` bool (default `true`) to allow user resizing. Not implemented at all -- no resize handles or logic exist. |
| `State` parameter missing | **[High]** | Docs (`size.md`, `events.md`) describe a `WindowState` enum parameter (`Default`, `Minimized`, `Maximized`) with two-way binding support. The component has Minimize/Maximize action buttons but clicking them only fires `OnAction` -- there is no actual state tracking, minimize/maximize visual behavior, or `StateChanged` event. |
| `MinHeight` parameter missing | **[Medium]** | Documented in `overview.md` and `size.md`. Not implemented. |
| `MaxHeight` parameter missing | **[Medium]** | Documented in `overview.md` and `size.md`. Not implemented. |
| `MinWidth` parameter missing | **[Medium]** | Documented in `overview.md` and `size.md`. Not implemented. |
| `MaxWidth` parameter missing | **[Medium]** | Documented in `overview.md` and `size.md`. Not implemented. |
| `Size` parameter missing | **[Medium]** | Docs describe a predefined size parameter (`Small`/`Medium`/`Large` via `ThemeConstants.Window.Size`). Not implemented. |
| `ThemeColor` parameter missing | **[Medium]** | Docs describe a `ThemeColor` string parameter for titlebar color theming. Not implemented. |
| `AnimationType` parameter missing | **[Medium]** | Docs (`animation.md`) describe a `WindowAnimationType` enum parameter for open/close animations. Not implemented. |
| `AnimationDuration` parameter missing | **[Medium]** | Docs (`animation.md`) describe an `int` parameter (default 300) for animation duration. Not implemented. |
| `PersistContent` parameter missing | **[Low]** | Docs describe a `PersistContent` bool that controls whether minimized Window content stays in the DOM. Not implemented (no minimize behavior exists). |
| `FooterLayoutAlign` parameter missing | **[Low]** | Docs describe a `WindowFooterLayoutAlign` enum parameter. Not implemented (no footer support). |
| `Id` parameter missing | **[Low]** | Docs describe an `Id` string parameter for the root element. Not implemented (though `AdditionalAttributes` could be used as a workaround). |
| Two-way binding for `Width`/`Height` missing | **[Medium]** | Docs state `Width` and `Height` support two-way binding (`@bind-Width`, `@bind-Height`). The implementation has one-way `Width`/`Height` parameters only -- no `WidthChanged`/`HeightChanged` EventCallbacks. |
| Two-way binding for `Top`/`Left` missing | **[Medium]** | Docs state `Top` and `Left` support two-way binding. The implementation has one-way parameters only -- no `TopChanged`/`LeftChanged` EventCallbacks. |

### Events

| Gap | Severity | Details |
|-----|----------|---------|
| `StateChanged` event missing | **[High]** | Docs describe a `StateChanged` EventCallback<WindowState> fired on minimize/maximize/restore. Not implemented. |
| `WidthChanged` event missing | **[Medium]** | Docs describe `WidthChanged` fired on user resize. Not implemented. |
| `HeightChanged` event missing | **[Medium]** | Docs describe `HeightChanged` fired on user resize. Not implemented. |
| `TopChanged` event missing | **[Medium]** | Docs describe `TopChanged` fired when user finishes dragging. Not implemented. |
| `LeftChanged` event missing | **[Medium]** | Docs describe `LeftChanged` fired when user finishes dragging. Not implemented. |

### Methods

| Gap | Severity | Details |
|-----|----------|---------|
| `Refresh()` method missing | **[Low]** | Docs describe a public `Refresh()` method accessible via `@ref`. Not implemented. |

### Child Component / Template Model

| Gap | Severity | Details |
|-----|----------|---------|
| `<WindowContent>` child tag missing | **[High]** | Docs show content placed inside a `<WindowContent>` tag. Implementation uses `ChildContent` RenderFragment directly. This is a different API contract. |
| `<WindowTitle>` child tag missing | **[High]** | Docs show title content inside a `<WindowTitle>` tag supporting HTML/components. Implementation uses a `Title` string parameter and `TitleBarTemplate` RenderFragment -- different API shape. |
| `<WindowActions>` / `<WindowAction>` child tags missing | **[High]** | Docs describe a compositional model where individual `<WindowAction>` components are placed inside `<WindowActions>`, each with `Name`, `Hidden`, `OnClick`, `Icon`, `Title` parameters. Implementation uses a `[Flags] enum WindowAction` parameter instead. This means: no custom actions, no per-action `OnClick`, no per-action `Hidden`/`Icon`/`Title`, no mixing of built-in and custom actions. |
| `<WindowFooter>` child tag missing | **[Medium]** | Docs describe a `<WindowFooter>` tag for bottom content. Not implemented. |

### Behaviors

| Gap | Severity | Details |
|-----|----------|---------|
| Actual drag behavior missing | **[High]** | The implementation sets `data-draggable` on the title bar but contains no JS interop or drag logic. Dragging does not actually work. |
| Actual resize behavior missing | **[High]** | No resize handles, no resize logic, no JS interop for resizing. |
| Minimize visual behavior missing | **[High]** | Clicking Minimize fires `OnAction` but does not collapse to title-bar-only view. |
| Maximize visual behavior missing | **[High]** | Clicking Maximize fires `OnAction` but does not expand to fill viewport. |
| Auto-centering missing | **[Medium]** | Docs state the Window auto-centers when `Top`/`Left` are not set. No centering logic exists in `BuildWindowStyle()`. |
| Render in `SunfishRootComponent` missing | **[Medium]** | Docs state the Window renders as a child of `SunfishRootComponent` when `ContainmentSelector` is not set, ensuring it overlays all content. The component renders in place. |

### Accessibility

| Gap | Severity | Details |
|-----|----------|---------|
| `aria-labelledby` missing | **[Medium]** | WAI-ARIA spec says the window should use `aria-labelledby` pointing to the title element's `id`. Implementation uses `aria-label="@Title"` instead, which fails when `TitleBarTemplate` is used (Title may be null). |
| `aria-modal` always rendered | **[Low]** | WAI-ARIA spec says `aria-modal=true` should only be present on modal windows. Implementation always renders `aria-modal` (set to `"false"` for non-modal windows). Should be conditionally rendered. |
| Keyboard navigation missing | **[Medium]** | Docs reference keyboard navigation demos. No keyboard handling (e.g., Escape to close, Tab trapping for modal) is implemented. |

---

## Code to Spec Gaps (Implemented but not documented)

### Parameters

| Gap | Severity | Details |
|-----|----------|---------|
| `Actions` enum parameter undocumented | **[Medium]** | The `Actions` parameter of type `WindowAction` (flags enum) is not described in the docs. The docs instead describe a `<WindowActions>`/`<WindowAction>` child-component model. This is a fundamentally different API. |
| `TitleBarTemplate` RenderFragment undocumented | **[Low]** | The implementation exposes a `TitleBarTemplate` RenderFragment for custom title bar content. This is not mentioned in the docs (the docs use `<WindowTitle>` instead). |
| `OnAction` EventCallback undocumented | **[Medium]** | The `OnAction` EventCallback<WindowAction> is not documented. The docs describe per-action `OnClick` handlers on individual `<WindowAction>` components instead. |

### Behaviors / Constraints

| Gap | Severity | Details |
|-----|----------|---------|
| Overlay click always closes if Close action present | **[Low]** | `HandleOverlayClick` unconditionally closes the window when `WindowAction.Close` is in `Actions`. This behavior is not documented and conflicts with the spec's `CloseOnOverlayClick` opt-in model. |
| Close action mutates `Visible` internally | **[Low]** | `HandleAction(WindowAction.Close)` sets `Visible = false` directly before invoking `VisibleChanged`. This is correct two-way binding pattern but differs from spec examples where the consumer controls visibility in the `VisibleChanged` handler. |

---

## Recommended Changes

### Implementation Updates (Priority Order)

1. **[Critical] Refactor action model to child-component pattern.** Replace the `[Flags] enum WindowAction` parameter with `<WindowActions>` and `<WindowAction>` child components matching the spec API. This is the most fundamental architectural gap.

2. **[Critical] Implement `<WindowContent>`, `<WindowTitle>`, `<WindowFooter>` child tags.** Replace `ChildContent`/`Title`/`TitleBarTemplate` with the documented child-tag model.

3. **[Critical] Implement `State` parameter and `StateChanged` event.** Add `WindowState` enum support with actual minimize (collapse to titlebar) and maximize (fill viewport) visual behaviors.

4. **[Critical] Implement drag behavior.** Add JS interop for titlebar dragging with `TopChanged`/`LeftChanged` events and two-way binding support.

5. **[Critical] Implement resize behavior.** Add resize handles, JS interop, `Resizable` parameter, min/max dimension parameters, and `WidthChanged`/`HeightChanged` events.

6. **[High] Add `CloseOnOverlayClick` parameter.** Replace hardcoded overlay-click-to-close with opt-in behavior.

7. **[High] Add `ContainmentSelector` parameter.** Implement container-scoped rendering, dragging, and resizing.

8. **[Medium] Add animation support.** Implement `AnimationType` and `AnimationDuration` parameters.

9. **[Medium] Add remaining parameters:** `Size`, `ThemeColor`, `PersistContent`, `FooterLayoutAlign`, `Id`.

10. **[Medium] Fix accessibility:** Use `aria-labelledby` instead of `aria-label`, conditionally render `aria-modal`, add keyboard navigation (Escape to close, focus trapping for modal).

11. **[Low] Add `Refresh()` public method.**

### Documentation Updates

1. **[Medium]** If the enum-based `Actions` parameter is intentional as a simplified API, document it. Otherwise, remove it once the child-component model is implemented.

2. **[Low]** If `TitleBarTemplate` will remain as an alternative API, add documentation for it.

3. **[Low]** Document the `OnAction` event if it will coexist with per-action `OnClick` handlers.

---

## Open Questions / Ambiguities

1. **Architectural decision: child-component vs enum model.** The spec describes `<WindowActions>/<WindowAction>` child components while the implementation uses a `[Flags] enum`. Is this a deliberate simplification or an incomplete implementation? The child-component model is significantly more flexible (custom actions, per-action configuration). Resolution determines the scope of refactoring needed.

2. **Render location.** The spec states the Window renders inside `SunfishRootComponent` by default. Is this teleportation pattern planned? It requires framework-level support (e.g., a render portal mechanism).

3. **JS interop dependency.** Drag, resize, and animation features all require JavaScript interop. Is there an existing JS interop infrastructure in the project, or does this need to be built from scratch?

4. **`WindowState` enum location.** The `WindowEnums.cs` file only defines `WindowAction`. A `WindowState` enum (`Default`, `Minimized`, `Maximized`) needs to be added.

5. **Auto-centering logic.** The spec says the Window auto-centers when `Top`/`Left` are unset. Should this be CSS-based (`transform: translate(-50%, -50%)` with fixed positioning) or JS-based (calculate viewport dimensions)?

6. **`Centered` parameter.** The size spec mentions a `Centered` parameter in the `WindowState.Default` description, but it is not listed in the parameter table. Is this a planned parameter?
