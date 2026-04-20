using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Internal.Interop;

/// <summary>
/// Observes element size changes using the browser ResizeObserver API.
/// </summary>
internal interface IResizeObserverService
{
    /// <summary>
    /// Starts observing size changes on the specified element.
    /// Returns a handle that, when disposed, stops observation.
    /// </summary>
    ValueTask<IAsyncDisposable> ObserveAsync(ElementReference element, Func<ElementRect, Task> callback, CancellationToken cancellationToken = default);
}
