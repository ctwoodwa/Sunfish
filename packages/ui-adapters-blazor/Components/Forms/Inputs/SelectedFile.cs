using Microsoft.AspNetCore.Components.Forms;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// A lightweight, provider-agnostic descriptor for a file the user has picked in
/// <see cref="SunfishFileUpload"/>. Emitted via the <c>FilesChanged</c> callback
/// and via <see cref="FileUploadSelectEventArgs"/> so callers can react to selection
/// without depending on Blazor's <see cref="IBrowserFile"/> abstraction.
/// </summary>
public class SelectedFile
{
    /// <summary>Unique identifier assigned by the component for this selection entry.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>The display name of the file (including extension).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The file size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>The file extension including the leading dot (e.g. <c>.pdf</c>).</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>The browser-reported MIME type (may be empty).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>True when the extension violates <c>AllowedExtensions</c>.</summary>
    public bool InvalidExtension { get; set; }

    /// <summary>True when the file exceeds <c>MaxFileSize</c>.</summary>
    public bool InvalidMaxFileSize { get; set; }

    /// <summary>True when the file is smaller than <c>MinFileSize</c>.</summary>
    public bool InvalidMinFileSize { get; set; }

    /// <summary>True when any validation rule was violated.</summary>
    public bool IsInvalid => InvalidExtension || InvalidMaxFileSize || InvalidMinFileSize;

    /// <summary>
    /// The current upload status. Always <see cref="UploadFileStatus.Selected"/> in
    /// FileSelect (no-upload) mode; transitions through Uploading -> Uploaded/Failed
    /// when a <c>SaveUrl</c> is configured.
    /// </summary>
    public UploadFileStatus Status { get; set; } = UploadFileStatus.Selected;

    /// <summary>Upload progress in percent (0-100). Only meaningful during upload.</summary>
    public int Progress { get; set; }

    /// <summary>Reference to the underlying Blazor file for reading bytes on demand.</summary>
    public IBrowserFile? BrowserFile { get; internal set; }

    /// <summary>Number of bytes already transmitted (used by resumable uploads).</summary>
    internal long UploadedBytes { get; set; }

    /// <summary>Token source that lets the component cancel an in-flight request.</summary>
    internal System.Threading.CancellationTokenSource? CancellationTokenSource { get; set; }
}

/// <summary>
/// Event arguments for <see cref="SunfishFileUpload.OnSelect"/>. Fires when the
/// user has picked one or more files (pre-upload). Set <see cref="IsCancelled"/> to
/// <c>true</c> to veto the selection -- files are discarded and no upload begins.
/// </summary>
public class FileUploadSelectEventArgs
{
    /// <summary>The files the user just selected.</summary>
    public IList<SelectedFile> Files { get; set; } = new List<SelectedFile>();

    /// <summary>Set to true to reject the selection (files are dropped from the queue).</summary>
    public bool IsCancelled { get; set; }
}
