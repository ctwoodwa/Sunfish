# Shared JS Interop Infrastructure — Implementation Notes

**Date:** 2026-04-09
**Scope:** Scaffold-only — no component features implemented

---

## What Was Scaffolded

A shared, reusable JS interop layer for complex Sunfish components. This provides typed C# service abstractions, ES module JS implementations, and DI registration so that future component work (Window, Popover, DataGrid, Chart, etc.) can consume shared browser interop instead of each component reinventing its own.

### Services Created

| Interface                       | Implementation                | JS Module                      | Purpose                                              |
| ------------------------------- | ----------------------------- | ------------------------------ | ---------------------------------------------------- |
| `ISunfishJsModuleLoader`        | `SunfishJsModuleLoader`       | (orchestrator)                 | Lazy-loads and caches JS ES modules                  |
| `IElementMeasurementService`   | `ElementMeasurementService`  | `marilo-measurement.js`        | getBoundingClientRect, viewport measurement          |
| `IResizeObserverService`       | `ResizeObserverService`      | `marilo-observers.js`          | ResizeObserver wrapper with handle-based disposal    |
| `IIntersectionObserverService` | `IntersectionObserverService`| `marilo-observers.js`          | IntersectionObserver wrapper with handle-based disposal |
| `IPopupPositionService`        | `PopupPositionService`       | `marilo-positioning.js`        | Anchor-relative popup positioning with flip          |
| `IDragService`                 | `DragService`                | `marilo-dragdrop.js`           | Pointer-based drag operations                        |
| `IResizeInteractionService`    | `ResizeInteractionService`   | `marilo-resize.js`             | Pointer-based element resize operations              |
| `IClipboardService`            | `ClipboardService`           | `marilo-clipboard-download.js` | Clipboard read/write via Clipboard API               |
| `IDownloadService`             | `DownloadService`            | `marilo-clipboard-download.js` | In-memory file download triggering                   |
| `IGraphicsInteropService`      | `GraphicsInteropService`     | `marilo-graphics.js`           | Text measurement, DPI, element size for rendering    |

### DTOs and Enums

All defined in `InteropModels.cs`:
- `ElementRect`, `ViewportRect`
- `DragStartOptions`, `DragUpdate`, `DragResult`
- `ResizeHandle` (flags enum), `ResizeConstraints`, `ResizeUpdate`
- `PopupAnchorOptions`, `PopupPlacement` (enum), `PopupPositionResult`
- `IntersectionState`
- `DownloadRequest`, `ClipboardWriteRequest`

---

## Where It Lives

```
src/Sunfish.Components.Blazor/
├── Internal/Interop/           ← C# interfaces, implementations, DTOs, DI registration
│   ├── I*.cs                   ← Service interfaces (internal)
│   ├── *.cs                    ← Service implementations (internal)
│   ├── InteropModels.cs        ← DTOs and enums (internal)
│   ├── InteropServiceExtensions.cs ← DI registration (public extension on SunfishBuilder)
│   └── SHARED_INTEROP_NOTES.md ← This file
└── wwwroot/js/
    ├── marilo-measurement.js
    ├── marilo-observers.js
    ├── marilo-positioning.js
    ├── marilo-dragdrop.js
    ├── marilo-resize.js
    ├── marilo-clipboard-download.js
    └── marilo-graphics.js

tests/Sunfish.Tests/Interop/
├── SharedInteropServiceTests.cs  ← DI registration, construction, disposal tests
├── InteropModelTests.cs          ← DTO/enum validation tests
└── JsModulePathTests.cs          ← JS file existence and export verification
```

---

## Which Components Should Consume Which Services

| Component         | Services                                                    |
| ----------------- | ----------------------------------------------------------- |
| **Window**        | `IDragService`, `IResizeInteractionService`, `IElementMeasurementService` |
| **Popover**       | `IPopupPositionService`, `IElementMeasurementService`       |
| **Splitter**      | `IResizeInteractionService`, `IElementMeasurementService`   |
| **DataGrid**      | `IResizeInteractionService`, `IElementMeasurementService`, `IIntersectionObserverService` (virtual scroll) |
| **TreeList**      | `IIntersectionObserverService` (virtual scroll)             |
| **PivotGrid**     | `IResizeInteractionService`, `IElementMeasurementService`   |
| **Scheduler**     | `IDragService`, `IResizeInteractionService`, `IElementMeasurementService` |
| **Gantt**         | `IDragService`, `IResizeInteractionService`, `IElementMeasurementService` |
| **Chart**         | `IGraphicsInteropService`, `IElementMeasurementService`, `IResizeObserverService` |
| **Diagram**       | `IGraphicsInteropService`, `IDragService`, `IElementMeasurementService` |
| **Map**           | `IGraphicsInteropService`, `IResizeObserverService`         |
| **Editor**        | `IClipboardService`, `IElementMeasurementService`           |
| **FileManager**   | `IClipboardService`, `IDownloadService`                     |
| **ResizableContainer** | `IResizeInteractionService`, `IResizeObserverService`  |

---

## What Was Intentionally Deferred

1. **Full feature wiring** — Services are injected into SunfishWindow as a seam but not yet wired to replace the inline JS.
2. **Virtual scrolling service** — Deferred until DataGrid virtual scroll pass; may extend `IIntersectionObserverService` or be a separate service.
3. **Focus management service** — May be needed for modal trap, Popover focus; deferred until Popover/Dialog focus pass.
4. **DotNetObjectReference pooling** — The observer services create per-observation DotNetObjectReferences. Pooling may improve perf for high-frequency observe/unobserve.
5. **SSR/prerendering guards** — JS interop is not available during static SSR. Components already guard via `OnAfterRenderAsync`; the shared services rely on the same pattern.
6. **Keyboard resize/drag** — The shared drag/resize modules handle pointer events only. Keyboard-based resize (already in ResizableContainer's per-component JS) may be added later.

---

## Architecture Decisions

1. **Placement in `Internal/Interop`** rather than `Sunfish.Foundation`: The services depend on `IJSRuntime` and `IJSObjectReference` which are Blazor-specific. They also depend on the JS module files shipped with `Sunfish.Components.Blazor`. Keeping them in Components ensures the JS files and C# wrappers ship together.

2. **Internal visibility**: All interfaces, DTOs, and implementations are `internal`. Only the `AddMariloInteropServices()` extension method is `public` (on `SunfishBuilder`). Components inject services via `[Inject]` which bypasses access modifiers.

3. **Module loader pattern**: `SunfishJsModuleLoader` centralizes all JS module imports with caching and disposal. This replaces the pattern where each component calls `JS.InvokeAsync<IJSObjectReference>("import", ...)` independently.

4. **Observer handle pattern**: `IResizeObserverService` and `IIntersectionObserverService` return `IAsyncDisposable` handles. Disposing the handle stops the observation. This prevents leaked observers.

5. **DI registration as explicit opt-in**: `AddMariloInteropServices()` is a separate call chained after `AddMariloCoreServices()` because providers don't reference `Sunfish.Components.Blazor` (to avoid circular dependencies).

---

## Open Design Questions

1. Should `ISunfishJsModuleLoader` be promoted to `Sunfish.Foundation` as an abstraction to allow testing without JS? Currently it's internal to Components.
2. Should the Window, Splitter, etc. migration from inline JS to shared modules happen incrementally (one service at a time) or as a batch?
3. Do we need a `IScrollService` for programmatic scroll (e.g., DataGrid scroll-to-row)?

---

## DI Registration

```csharp
// In Program.cs or startup:
builder.Services.AddMarilo()
    .AddMariloCoreServices()
    .AddMariloInteropServices();  // ← New
```

---

## Integration Seam

`SunfishWindow.razor` now injects three shared services:
- `IElementMeasurementService`
- `IDragService`
- `IResizeInteractionService`

These are injected but **not yet wired** — the existing inline JS continues to handle drag/resize. The next pass will migrate Window to use these shared services.
