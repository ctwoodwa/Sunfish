namespace Sunfish.Components.Blazor.Internal.Interop;

/// <summary>
/// Provides clipboard read/write operations via the browser Clipboard API.
/// </summary>
internal interface IClipboardService
{
    /// <summary>Writes text and/or HTML to the system clipboard.</summary>
    ValueTask WriteAsync(ClipboardWriteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads plain text from the system clipboard.</summary>
    ValueTask<string> ReadTextAsync(CancellationToken cancellationToken = default);
}
