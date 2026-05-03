---
uid: troubleshooting-js-errors
title: JavaScript Errors
description: Common JavaScript errors and how to resolve them.
---

# JavaScript Errors

Sunfish uses ES module-based JavaScript interop for components that require browser APIs (drag-and-drop, resize handles, dialogs, etc.). This article covers the most common JavaScript errors and their resolutions.

## "Could not find 'X' in module"

**Full error example:**

```
Microsoft.JSInterop.JSException: Could not find 'initDropZone' in module '_content/Sunfish.Core/js/sunfish-core.js'.
```

**Causes and solutions:**

1. **JS module not loaded.** The browser has not yet loaded the module that exports the expected function. This can happen when the component's `OnAfterRenderAsync` fires before the module import completes. Ensure the component awaits the module import before calling its functions:

   ```csharp
   protected override async Task OnAfterRenderAsync(bool firstRender)
   {
       if (firstRender)
       {
           _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
               "import", "_content/Sunfish.Core/js/sunfish-core.js");
           await _module.InvokeVoidAsync("initDropZone", _elementRef);
       }
   }
   ```

2. **Incorrect `_content/` path.** Module paths for Razor Class Libraries follow the pattern `_content/{AssemblyName}/{path}`. Verify the assembly name matches the package name (e.g., `Sunfish.Core`, `Sunfish.Providers.FluentUI`). A typo or casing difference will produce a 404 for the module file, and the subsequent invocation will report a missing export.

3. **Provider JS not initialized.** Some providers include a companion JavaScript module that must be imported before provider-specific interop functions are available. If you switched providers without updating the JS import path, the old module may be loaded instead of the new one.

## "Cannot Read Properties of Null"

**Full error example:**

```
TypeError: Cannot read properties of null (reading 'addEventListener')
```

**Causes and solutions:**

1. **Element reference lost after re-render.** If a component stores an `ElementReference` and the element is conditionally rendered (e.g., inside an `@if` block that becomes `false`), the reference becomes invalid. Subsequent JS calls using the stale reference will receive a `null` element in JavaScript. To avoid this:
   - Do not store `ElementReference` values across renders where the element may be removed.
   - Dispose JS listeners before the element is removed by calling a cleanup function in `DisposeAsync`.

2. **Component disposed during async operation.** If a component is disposed while an `await` is in progress, the `ElementReference` may be invalidated before the awaited JS call executes. Guard against this with a `CancellationToken` or a `_disposed` flag:

   ```csharp
   private bool _disposed;

   protected override async Task OnAfterRenderAsync(bool firstRender)
   {
       if (_disposed) return;
       await _module.InvokeVoidAsync("attachListeners", _elementRef);
   }

   public async ValueTask DisposeAsync()
   {
       _disposed = true;
       if (_module is not null)
       {
           await _module.InvokeVoidAsync("detachListeners", _elementRef);
           await _module.DisposeAsync();
       }
   }
   ```

## Dialog / Modal Not Opening

**Symptoms:** Calling `ShowModalAsync()` or clicking the trigger button does nothing; no dialog element appears in the DOM.

**Causes and solutions:**

1. **JS interop not initialized.** `SunfishDialog` and `SunfishModal` use the browser's `<dialog>` element and call `dialog.showModal()` via JS interop. If the component's interop module has not been imported yet, the call is silently dropped. Ensure the dialog is not programmatically opened in `OnInitialized` or `OnParametersSet` — these lifecycle hooks run before `OnAfterRenderAsync`, at which point the JS module is not yet available.

2. **`ShowModalAsync` called before `OnAfterRenderAsync`.** The JS module is imported in `OnAfterRenderAsync(firstRender: true)`. Calls to `ShowModalAsync` before this lifecycle hook completes will fail because the interop reference is `null`. If you need to open a dialog immediately on page load, call `ShowModalAsync` inside `OnAfterRenderAsync`:

   ```csharp
   protected override async Task OnAfterRenderAsync(bool firstRender)
   {
       if (firstRender)
       {
           await _dialog.ShowModalAsync();
       }
   }
   ```

3. **Dialog rendered inside a conditional block.** If the `<SunfishDialog>` component is inside an `@if` block that is initially `false`, the component does not exist in the DOM when `ShowModalAsync` is called. Ensure the dialog is rendered (even if invisible) before calling show, or use the `Visible` parameter instead of conditional rendering.

## Drag / Resize Not Working

**Symptoms:** Column resize handles, splitter drag, or drag-and-drop interactions do not respond to pointer events.

**Causes and solutions:**

1. **Missing JS interop initialization.** Resize and drag features are activated by calling an initialization function from `OnAfterRenderAsync`. If the component re-renders (e.g., due to a parameter change) and re-initialization is not called, the event listeners are not re-attached. Check that the component calls its JS init function after each render that modifies the DOM structure (not only on `firstRender`).

2. **Pointer events blocked by an overlay element.** An absolute-positioned overlay (e.g., a loading spinner, a tooltip, or a custom overlay) with `pointer-events: auto` layered above the drag handle will intercept pointer events before they reach the handle. Inspect the element in DevTools and look for an overlay covering the handle. Set `pointer-events: none` on the overlay if it does not need to capture input.

3. **`touch-action` not set.** On touch devices, the browser's default touch action (scrolling) takes precedence over pointer events for drag operations unless `touch-action: none` is set on the drag target. Sunfish applies this automatically on its drag handles, but a global CSS reset that overrides `touch-action` can prevent drag from working on touch screens.

4. **Component not visible at init time.** JS interop that measures element dimensions (e.g., for splitter initial position) will return zero values if the component is hidden via `display: none` at initialization. Ensure the component is visible in the DOM before first render, or call the JS re-initialization function after the component becomes visible.

## See Also

- [General Issues](xref:troubleshooting-general)
- [Security Overview](xref:security-overview)
