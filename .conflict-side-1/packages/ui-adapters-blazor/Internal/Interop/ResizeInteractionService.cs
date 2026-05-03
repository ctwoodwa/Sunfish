using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Internal.Interop;

internal sealed class ResizeInteractionService : IResizeInteractionService
{
    private readonly ISunfishJsModuleLoader _loader;

    public ResizeInteractionService(ISunfishJsModuleLoader loader)
    {
        _loader = loader;
    }

    public async ValueTask<ResizeUpdate> StartResizeAsync(
        ElementReference element,
        ResizeHandle handle,
        double startClientX,
        double startClientY,
        ResizeConstraints? constraints = null,
        Func<ResizeUpdate, Task>? onUpdate = null,
        CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-resize.js", cancellationToken);
        constraints ??= new ResizeConstraints();
        var tcs = new TaskCompletionSource<ResizeUpdate>();
        var bridge = new ResizeCallbackBridge(onUpdate, tcs);
        var dotNetRef = DotNetObjectReference.Create(bridge);

        await module.InvokeVoidAsync("startResize", cancellationToken, element, dotNetRef, new
        {
            handleFlags = (int)handle,
            clientX = startClientX,
            clientY = startClientY,
            minWidth = constraints.MinWidth,
            minHeight = constraints.MinHeight,
            maxWidth = double.IsPositiveInfinity(constraints.MaxWidth) ? 99999 : constraints.MaxWidth,
            maxHeight = double.IsPositiveInfinity(constraints.MaxHeight) ? 99999 : constraints.MaxHeight,
            clampToParent = constraints.ClampToParent,
            updateMethod = nameof(ResizeCallbackBridge.OnResizeUpdate),
            endMethod = nameof(ResizeCallbackBridge.OnResizeEnd)
        });

        var result = await tcs.Task;
        dotNetRef.Dispose();
        return result;
    }

    internal sealed class ResizeCallbackBridge
    {
        private readonly Func<ResizeUpdate, Task>? _onUpdate;
        private readonly TaskCompletionSource<ResizeUpdate> _tcs;

        public ResizeCallbackBridge(Func<ResizeUpdate, Task>? onUpdate, TaskCompletionSource<ResizeUpdate> tcs)
        {
            _onUpdate = onUpdate;
            _tcs = tcs;
        }

        [JSInvokable]
        public Task OnResizeUpdate(ResizeUpdate update) => _onUpdate?.Invoke(update) ?? Task.CompletedTask;

        [JSInvokable]
        public Task OnResizeEnd(ResizeUpdate result)
        {
            _tcs.TrySetResult(result);
            return Task.CompletedTask;
        }
    }
}
