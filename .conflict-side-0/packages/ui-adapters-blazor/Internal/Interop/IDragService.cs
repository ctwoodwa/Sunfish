using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Internal.Interop;

/// <summary>
/// Provides pointer-based drag operations for moving elements.
/// Used by Window (title-bar drag), Scheduler (event drag), etc.
/// </summary>
internal interface IDragService
{
    /// <summary>
    /// Starts a drag operation on the specified element.
    /// The <paramref name="onUpdate"/> callback fires for each pointer move.
    /// Returns a task that completes with the final drag result on pointer up.
    /// </summary>
    ValueTask<DragResult> StartDragAsync(
        ElementReference element,
        DragStartOptions options,
        Func<DragUpdate, Task>? onUpdate = null,
        CancellationToken cancellationToken = default);
}
