---
title: ResizableContainer
page_title: SunfishResizableContainer Component
description: A wrapper component that provides drag-to-resize functionality for hosted child content.
slug: resizable-container-overview
tags: resizable-container, layout, resize, container, wrapper
published: true
position: 1
---

# SunfishResizableContainer Overview

The `SunfishResizableContainer` is a layout primitive that wraps arbitrary child content and provides user-controlled resize handles. It supports configurable edge/corner handles, min/max constraints, keyboard resizing, ResizeObserver integration, and optional size persistence.

## Basic Usage

```razor
<SunfishResizableContainer Width="400px" Height="300px"
                          MinWidth="200px" MinHeight="150px">
    <p>Drag the handle to resize this container.</p>
</SunfishResizableContainer>
```

## When to Use

- Resizing a single hosted component like a grid, scheduler, chart, editor, or dashboard tile
- Giving users control over a content area's dimensions
- Persisting user-chosen panel sizes across page loads

## When NOT to Use

- **Multi-pane layouts** where adjacent panels resize against each other — use `SunfishSplitter` instead
- **IDE-style layouts** requiring splitter groups for file explorers and tool panels
- **Highly virtualized content** that needs bespoke resize orchestration beyond container observation

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| ChildContent | RenderFragment? | null | Content inside the container |
| Width | string | "100%" | Width as CSS value |
| Height | string | "320px" | Height as CSS value |
| MinWidth | string? | null | Minimum width constraint |
| MinHeight | string? | null | Minimum height constraint |
| MaxWidth | string? | null | Maximum width constraint |
| MaxHeight | string? | null | Maximum height constraint |
| Enabled | bool | true | Whether resizing is enabled |
| ShowHandle | bool | true | Whether to show resize handles |
| ResizeEdges | SunfishResizeEdges | BottomRight | Which edges/corners have handles |
| ObserveSizeChanges | bool | true | Use ResizeObserver for size detection |
| PersistSize | bool | false | Persist dimensions to localStorage |
| PersistKey | string? | null | Storage key for persistence |
| UseGhostOutline | bool | false | Show ghost outline during drag |
| ClampToParent | bool | false | Constrain within parent bounds |
| DisableTextSelection | bool | true | Disable text selection during drag |
| KeyboardResizeEnabled | bool | true | Allow keyboard arrow-key resizing |
| HandleAriaLabel | string? | null | Accessible label for handles |
| HandleClass | string? | null | Extra CSS class for handles |
| HandleStyle | string? | null | Extra inline style for handles |
| Class | string? | null | Extra CSS class for root |
| Style | string? | null | Extra inline style for root |

### Two-Way Binding Callbacks

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| WidthChanged | EventCallback\<string\> | — | Two-way binding callback. Fires with the new CSS width string after drag, keyboard resize, `SetSizeAsync`, or persisted-size restore. Use `@bind-Width` rather than wiring this directly. |
| HeightChanged | EventCallback\<string\> | — | Two-way binding callback. Fires with the new CSS height string. Use `@bind-Height` rather than wiring this directly. |

## Two-Way Binding

Width and Height support Blazor two-way binding:

```razor
<SunfishResizableContainer @bind-Width="_width" @bind-Height="_height">
    <p>Current size: @_width x @_height</p>
</SunfishResizableContainer>
```

## Public Methods

| Method | Description |
|--------|-------------|
| SetSizeAsync(string width, string height) | Set dimensions programmatically |
| ResetSizeAsync() | Reset to initial Width/Height |
| FocusHandleAsync() | Focus the primary handle |

## Integration Guidance

For hosted components (DataGrid, AllocationScheduler, Scheduler, Gantt, Charts):

1. Accept `Width`/`Height` parameters or respond to parent container sizing
2. Observe container changes or rerender on parent resize
3. Do NOT embed resize handles directly in the hosted component — let the wrapper handle resizing

## Performance Considerations

- ResizeObserver fires on any size change (layout reflows, CSS transitions, user drag)
- Throttle expensive operations in `OnObservedSizeChanged` if the hosted component has heavy render logic
- Ghost outline mode (`UseGhostOutline`) avoids live reflow during drag for complex content
- Pointer events use `setPointerCapture` for reliable cross-browser drag tracking

## Implementation Notes

- Uses a small JS interop module with `pointerdown`/`pointermove`/`pointerup` events
- `ResizeObserver` monitors the container element for all size changes
- Handle elements are `<button>` for native focus and keyboard support
- CSS `resize: both` is NOT used as the primary mechanism — full control via JS
