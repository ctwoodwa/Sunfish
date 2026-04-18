using Microsoft.AspNetCore.Components;

namespace Sunfish.Components.Blazor.Internal.Interop;

/// <summary>
/// Provides pointer-based resize operations on elements.
/// Used by Window (edge resize), Splitter (pane resize), ResizableContainer, DataGrid (column resize).
/// </summary>
internal interface IResizeInteractionService
{
    /// <summary>
    /// Starts a resize interaction on the specified element from the given handle direction.
    /// The <paramref name="onUpdate"/> callback fires for each pointer move.
    /// Returns a task that completes with the final size on pointer up.
    /// </summary>
    ValueTask<ResizeUpdate> StartResizeAsync(
        ElementReference element,
        ResizeHandle handle,
        double startClientX,
        double startClientY,
        ResizeConstraints? constraints = null,
        Func<ResizeUpdate, Task>? onUpdate = null,
        CancellationToken cancellationToken = default);
}
