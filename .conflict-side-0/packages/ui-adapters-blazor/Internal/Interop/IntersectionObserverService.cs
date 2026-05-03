using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Internal.Interop;

internal sealed class IntersectionObserverService : IIntersectionObserverService
{
    private readonly ISunfishJsModuleLoader _loader;

    public IntersectionObserverService(ISunfishJsModuleLoader loader)
    {
        _loader = loader;
    }

    public async ValueTask<IAsyncDisposable> ObserveAsync(
        ElementReference element,
        Func<IntersectionState, Task> callback,
        double[]? thresholds = null,
        CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-observers.js", cancellationToken);
        var bridge = new IntersectionCallbackBridge(callback);
        var dotNetRef = DotNetObjectReference.Create(bridge);
        var handleId = await module.InvokeAsync<int>("observeIntersection", cancellationToken,
            element, dotNetRef, nameof(IntersectionCallbackBridge.OnIntersection), thresholds);
        return new ObserverHandle(module, handleId, dotNetRef);
    }

    internal sealed class IntersectionCallbackBridge
    {
        private readonly Func<IntersectionState, Task> _callback;

        public IntersectionCallbackBridge(Func<IntersectionState, Task> callback)
        {
            _callback = callback;
        }

        [JSInvokable]
        public Task OnIntersection(IntersectionState state) => _callback(state);
    }

    private sealed class ObserverHandle : IAsyncDisposable
    {
        private readonly IJSObjectReference _module;
        private readonly int _handleId;
        private readonly IDisposable _dotNetRef;
        private bool _disposed;

        public ObserverHandle(IJSObjectReference module, int handleId, IDisposable dotNetRef)
        {
            _module = module;
            _handleId = handleId;
            _dotNetRef = dotNetRef;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                await _module.InvokeVoidAsync("unobserveIntersection", _handleId);
            }
            catch (JSDisconnectedException) { }

            _dotNetRef.Dispose();
        }
    }
}
