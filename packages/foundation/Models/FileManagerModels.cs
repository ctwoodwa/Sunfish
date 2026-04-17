namespace Sunfish.Foundation.Models;

/// <summary>
/// Represents a file or folder entry in the SunfishFileManager.
/// Use as <c>TItem</c> when you don't need a custom data model.
/// </summary>
public class FileManagerEntry
{
    public string Id { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public bool HasDirectories { get; set; }
    public long Size { get; set; }
    public DateTime? DateModified { get; set; }
    public DateTime? DateModifiedUtc { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateCreatedUtc { get; set; }
    public string? Extension { get; set; }
    public IEnumerable<FileManagerEntry>? Directories { get; set; }
    public IEnumerable<FileManagerEntry>? Items { get; set; }
}

/// <summary>
/// Specifies the view type for the file manager. Use <see cref="FileManagerViewType"/>.
/// </summary>
[Obsolete("Use FileManagerViewType instead. FileManagerViewMode will be removed in a future version.")]
public enum FileManagerViewMode
{
    /// <summary>List view with details.</summary>
    List,

    /// <summary>Grid/thumbnail view.</summary>
    Grid
}

/// <summary>
/// Specifies the view type for the file manager.
/// </summary>
public enum FileManagerViewType
{
    /// <summary>List view with details columns.</summary>
    ListView,

    /// <summary>Grid/thumbnail view.</summary>
    Grid
}

// ── Upload Settings ──────────────────────────────────────────────────────────

/// <summary>
/// Configures upload behaviour for <c>SunfishFileManager&lt;TItem&gt;</c>.
/// Assign to the <c>UploadSettings</c> parameter to enable the Upload button.
/// </summary>
public class FileManagerUploadSettings
{
    /// <summary>The server URL that accepts uploaded files (e.g. "/api/upload").</summary>
    public string? SaveUrl { get; set; }

    /// <summary>
    /// Allowed file extensions (e.g. <c>new[] { ".jpg", ".png" }</c>).
    /// When null or empty, all extensions are accepted.
    /// </summary>
    public string[]? AllowedExtensions { get; set; }

    /// <summary>Maximum file size in bytes. Zero means no limit.</summary>
    public long MaxFileSize { get; set; }

    /// <summary>When true (default), the file input accepts multiple files.</summary>
    public bool Multiple { get; set; } = true;
}

// ── EventArgs ────────────────────────────────────────────────────────────────

/// <summary>
/// Arguments for the <c>OnRead</c> event. Assign <see cref="Data"/> before returning.
/// </summary>
public class FileManagerReadEventArgs
{
    /// <summary>The path being loaded.</summary>
    public string Path { get; init; } = "/";

    /// <summary>
    /// The loaded items. The handler must populate this collection
    /// before the task completes.
    /// </summary>
    public IEnumerable<object>? Data { get; set; }

    /// <summary>Optional cancellation token passed by the component.</summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Arguments for the <c>OnCreate</c> event.
/// </summary>
/// <typeparam name="TItem">The data model type.</typeparam>
public class FileManagerCreateEventArgs<TItem>
{
    /// <summary>The item representing the newly created resource (e.g. folder).</summary>
    public TItem? Item { get; init; }
}

/// <summary>
/// Arguments for the <c>OnDelete</c> event.
/// </summary>
/// <typeparam name="TItem">The data model type.</typeparam>
public class FileManagerDeleteEventArgs<TItem>
{
    /// <summary>The item to be deleted.</summary>
    public TItem? Item { get; init; }
}

/// <summary>
/// Arguments for the <c>OnEdit</c> event (stub – Phase B).
/// </summary>
/// <typeparam name="TItem">The data model type.</typeparam>
public class FileManagerEditEventArgs<TItem>
{
    /// <summary>The item being edited.</summary>
    public TItem? Item { get; init; }
}

/// <summary>
/// Arguments for the <c>OnUpdate</c> event (stub – Phase B).
/// </summary>
/// <typeparam name="TItem">The data model type.</typeparam>
public class FileManagerUpdateEventArgs<TItem>
{
    /// <summary>The item after update.</summary>
    public TItem? Item { get; init; }
}

/// <summary>
/// Arguments for the <c>OnDownload</c> event (stub – Phase B).
/// </summary>
/// <typeparam name="TItem">The data model type.</typeparam>
public class FileManagerDownloadEventArgs<TItem>
{
    /// <summary>The item to download.</summary>
    public TItem? Item { get; init; }

    /// <summary>The stream containing file content.</summary>
    public Stream? Stream { get; init; }

    /// <summary>The MIME type of the file.</summary>
    public string? MimeType { get; init; }

    /// <summary>The download file name.</summary>
    public string? FileName { get; init; }

    /// <summary>Set to true in the handler to cancel the download.</summary>
    public bool IsCancelled { get; set; }
}
