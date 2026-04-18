using Microsoft.AspNetCore.Components;

namespace Sunfish.Components.Blazor.Internal.Interop;

/// <summary>
/// Observes element intersection with the viewport or a root element using the browser IntersectionObserver API.
/// </summary>
internal interface IIntersectionObserverService
{
    /// <summary>
    /// Starts observing intersection changes on the specified element.
    /// Returns a handle that, when disposed, stops observation.
    /// </summary>
    ValueTask<IAsyncDisposable> ObserveAsync(
        ElementReference element,
        Func<IntersectionState, Task> callback,
        double[]? thresholds = null,
        CancellationToken cancellationToken = default);
}
