using System.Collections.Concurrent;
using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Internal.Interop;

/// <summary>
/// Lazily imports and caches JS ES modules from the Sunfish.Components static content path.
/// </summary>
internal sealed class SunfishJsModuleLoader : ISunfishJsModuleLoader, IDisposable
{
    private const string ContentPrefix = "./_content/Sunfish.Components/";
    private readonly IJSRuntime _jsRuntime;
    private readonly ConcurrentDictionary<string, Task<IJSObjectReference>> _modules = new();
    private bool _disposed;

    public SunfishJsModuleLoader(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async ValueTask<IJSObjectReference> ImportAsync(string modulePath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var fullPath = ContentPrefix + modulePath;
        var task = _modules.GetOrAdd(fullPath, path =>
            _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, path).AsTask());

        return await task;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _modules.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kvp in _modules)
        {
            try
            {
                if (kvp.Value.IsCompletedSuccessfully)
                {
                    var module = kvp.Value.Result;
                    await module.DisposeAsync();
                }
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected; safe to ignore.
            }
        }

        _modules.Clear();
    }
}
