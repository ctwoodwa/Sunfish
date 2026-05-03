namespace Sunfish.UIAdapters.Blazor.Components.Utility;

/// <summary>
/// A lightweight descriptor of a file dropped onto a <see cref="SunfishDropZone"/>.
/// </summary>
/// <param name="Name">The file name as reported by the browser.</param>
/// <param name="Size">The file size in bytes as reported by the browser (may be 0 if unknown).</param>
/// <param name="ContentType">The MIME type as reported by the browser (may be empty).</param>
public sealed record DropZoneFileInfo(string Name, long Size, string ContentType);

/// <summary>
/// Event payload raised when a user drops one or more files onto a <see cref="SunfishDropZone"/>.
/// </summary>
public sealed class DropZoneDropEventArgs
{
    /// <summary>
    /// The list of files dropped onto the zone. Empty when the drop did not include any files
    /// (for example, a drag-text payload).
    /// </summary>
    public IReadOnlyList<DropZoneFileInfo> Files { get; init; } = Array.Empty<DropZoneFileInfo>();
}
