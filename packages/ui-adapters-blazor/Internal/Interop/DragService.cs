using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.Components.Blazor.Internal.Interop;

internal sealed class DragService : IDragService
{
    private readonly ISunfishJsModuleLoader _loader;

    public DragService(ISunfishJsModuleLoader loader)
    {
        _loader = loader;
    }

    public async ValueTask<DragResult> StartDragAsync(
        ElementReference element,
        DragStartOptions options,
        Func<DragUpdate, Task>? onUpdate = null,
        CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-dragdrop.js", cancellationToken);
        var tcs = new TaskCompletionSource<DragResult>();
        var bridge = new DragCallbackBridge(onUpdate, tcs);
        var dotNetRef = DotNetObjectReference.Create(bridge);

        await module.InvokeVoidAsync("startDrag", cancellationToken, element, dotNetRef, new
        {
            clientX = options.ClientX,
            clientY = options.ClientY,
            containmentSelector = options.ContainmentSelector,
            disableTextSelection = options.DisableTextSelection,
            updateMethod = nameof(DragCallbackBridge.OnDragUpdate),
            endMethod = nameof(DragCallbackBridge.OnDragEnd)
        });

        var result = await tcs.Task;
        dotNetRef.Dispose();
        return result;
    }

    internal sealed class DragCallbackBridge
    {
        private readonly Func<DragUpdate, Task>? _onUpdate;
        private readonly TaskCompletionSource<DragResult> _tcs;

        public DragCallbackBridge(Func<DragUpdate, Task>? onUpdate, TaskCompletionSource<DragResult> tcs)
        {
            _onUpdate = onUpdate;
            _tcs = tcs;
        }

        [JSInvokable]
        public Task OnDragUpdate(DragUpdate update) => _onUpdate?.Invoke(update) ?? Task.CompletedTask;

        [JSInvokable]
        public Task OnDragEnd(DragResult result)
        {
            _tcs.TrySetResult(result);
            return Task.CompletedTask;
        }
    }
}
