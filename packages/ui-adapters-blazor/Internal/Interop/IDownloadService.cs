namespace Sunfish.Components.Blazor.Internal.Interop;

/// <summary>
/// Triggers browser file downloads from in-memory content.
/// </summary>
internal interface IDownloadService
{
    /// <summary>Triggers a file download in the browser.</summary>
    ValueTask DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default);
}
