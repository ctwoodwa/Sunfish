namespace Sunfish.UIAdapters.Blazor.Internal.Interop;

/// <summary>
/// Manages external drop-zone registration for file upload components.
/// Forwards files dropped onto an external DOM element to a hidden file input.
/// </summary>
internal interface IDropZoneService
{
    /// <summary>Registers an external element as a drop zone forwarding to the given file input.</summary>
    /// <returns>A handle ID for cleanup, or -1 if elements were not found.</returns>
    ValueTask<int> RegisterAsync(string dropZoneElementId, string fileInputElementId, CancellationToken cancellationToken = default);

    /// <summary>Removes all event listeners for the given handle.</summary>
    ValueTask UnregisterAsync(int handleId, CancellationToken cancellationToken = default);
}
