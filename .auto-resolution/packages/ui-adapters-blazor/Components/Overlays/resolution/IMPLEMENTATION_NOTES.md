# Implementation Notes: SunfishWindow (Overlays)

## Design Decisions

### 1. Dual Action API (Enum + Child Components)
The spec describes a `<WindowActions>`/`<WindowAction>` child component model, but the existing implementation used a `[Flags] enum WindowAction`. Rather than a breaking change, both APIs are supported:

- **Enum API** (`Actions="WindowAction.Close | WindowAction.Minimize"`): Simple, declarative, covers common cases.
- **Child component API** (`<WindowActions><WindowActionButton Name="Close" OnClick="..." />`): Full flexibility with custom actions, per-action handlers, custom icons.

When `WindowActions` child is present, it takes precedence over the enum-based buttons.

### 2. JS Interop Pattern
Follows the established project pattern (SunfishSplitter, SunfishTooltip, SunfishMediaQuery):
- Inline JS via `JS.InvokeAsync<IJSObjectReference>("eval", GetScript())`
- `DotNetObjectReference<T>` for C# callbacks from JS
- Document-level `pointermove`/`pointerup` listeners for drag/resize (not element-level, to handle fast mouse movement)
- `[JSInvokable]` methods: `OnDragEndFromJs(top, left)`, `OnResizeEndFromJs(top, left, width, height)`

### 3. Auto-centering
CSS-based: `top:50%; left:50%; transform:translate(-50%,-50%)` when `Top`/`Left` are not set.
On first drag, JS removes the transform to switch to absolute pixel positioning.

### 4. Maximize/Restore State
Before maximizing, the component saves `Top`, `Left`, `Width`, `Height` to private fields. On restore (Maximized → Normal), these values are written back. The `mar-window--maximized` CSS class handles the full-viewport visual via CSS.

### 5. Resize Handles
Eight directional handles: n, e, s, w, ne, se, sw, nw. Rendered only when `Resizable=true` and `State=Normal`. Minimum enforced size: 200x100px.

### 6. Containment
`ContainmentSelector` passed to JS module. If set, drag positions are clamped within the container's bounding rect. Resize containment uses the same bounds.

## Approach

### Pass 1 (Current): Core functionality
- JS interop for drag and resize
- Child component architecture
- State management (minimize/maximize visual behavior)
- Two-way binding for position and size
- Keyboard navigation (Escape to close)
- Auto-centering
- All missing parameters (CloseOnOverlayClick, ContainmentSelector, Size, ThemeColor, AnimationType, etc.)

### Pass 2 (Future): Polish
- Modal focus trapping (Tab key cycling)
- Portal rendering via SunfishRootComponent
- Multi-window z-index management service
- Animation CSS implementations
- Accessibility audit (axe-core)

## Code Notes

### File Structure
```
Overlays/
├── SunfishWindow.razor          — Main window component (single file, razor + @code)
├── WindowContent.razor         — Child component for body content
├── WindowTitle.razor           — Child component for title (supports HTML)
├── WindowFooter.razor          — Child component for footer
├── WindowActions.razor         — Container for action buttons
├── WindowActionButton.razor    — Individual action button
├── GAP_ANALYSIS.md             — Original gap analysis
└── resolution/
    ├── RESOLUTION_STATUS.md
    ├── IMPLEMENTATION_NOTES.md
    ├── RESEARCH_LOG.md
    ├── TEST_PLAN.md
    └── PROTOTYPE_NOTES.md
```

### Modified External Files
- `Sunfish.Core/Enums/WindowEnums.cs` — Added `WindowAnimationType`, `WindowSize`, `WindowFooterLayoutAlign` enums
- `Sunfish.Core/Contracts/ISunfishCssProvider.cs` — Added `WindowFooterClass()` method
- `Sunfish.Providers.FluentUI/FluentUICssProvider.cs` — Implemented `WindowFooterClass()`
- `Sunfish.Providers.Bootstrap/BootstrapCssProvider.cs` — Implemented `WindowFooterClass()`

### Child Component Communication
Child components (`WindowContent`, `WindowTitle`, `WindowFooter`, `WindowActions`) receive the parent `SunfishWindow` via `CascadingValue`. On `OnInitialized`, they call `internal` setters on the parent to register their content fragments. The parent renders these fragments in the appropriate locations.

`WindowActionButton` registers itself with the parent via `RegisterActionButton()` and delegates click handling to `internal` methods on `SunfishWindow` (`HandleCloseFromAction`, `HandleMinimizeFromAction`, `HandleMaximizeRestoreFromAction`) when no custom `OnClick` is bound.
