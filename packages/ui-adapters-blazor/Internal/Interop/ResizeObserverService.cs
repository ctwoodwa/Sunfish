using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.Components.Blazor.Internal.Interop;

internal sealed class ResizeObserverService : IResizeObserverService
{
    private readonly ISunfishJsModuleLoader _loader;

    public ResizeObserverService(ISunfishJsModuleLoader loader)
    {
        _loader = loader;
    }

    public async ValueTask<IAsyncDisposable> ObserveAsync(ElementReference element, Func<ElementRect, Task> callback, CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-observers.js", cancellationToken);
        var bridge = new ResizeCallbackBridge(callback);
        var dotNetRef = DotNetObjectReference.Create(bridge);
        var handleId = await module.InvokeAsync<int>("observeResize", cancellationToken, element, dotNetRef, nameof(ResizeCallbackBridge.OnResize));
        return new ObserverHandle(module, handleId, dotNetRef, "unobserveResize");
    }

    internal sealed class ResizeCallbackBridge
    {
        private readonly Func<ElementRect, Task> _callback;

        public ResizeCallbackBridge(Func<ElementRect, Task> callback)
        {
            _callback = callback;
        }

        [JSInvokable]
        public Task OnResize(ElementRect rect) => _callback(rect);
    }

    private sealed class ObserverHandle : IAsyncDisposable
    {
        private readonly IJSObjectReference _module;
        private readonly int _handleId;
        private readonly IDisposable _dotNetRef;
        private readonly string _unobserveMethod;
        private bool _disposed;

        public ObserverHandle(IJSObjectReference module, int handleId, IDisposable dotNetRef, string unobserveMethod)
        {
            _module = module;
            _handleId = handleId;
            _dotNetRef = dotNetRef;
            _unobserveMethod = unobserveMethod;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                await _module.InvokeVoidAsync(_unobserveMethod, _handleId);
            }
            catch (JSDisconnectedException) { }

            _dotNetRef.Dispose();
        }
    }
}
