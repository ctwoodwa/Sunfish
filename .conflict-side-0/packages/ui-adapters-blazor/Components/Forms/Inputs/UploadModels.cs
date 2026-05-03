namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

// ── Enums ────────────────────────────────────────────────────────────────────

/// <summary>
/// Represents the status of a file in the upload process.
/// </summary>
public enum UploadFileStatus
{
    /// <summary>File has been selected but not yet uploaded.</summary>
    Selected,

    /// <summary>File is currently being uploaded.</summary>
    Uploading,

    /// <summary>File upload has been paused (chunk upload only).</summary>
    Paused,

    /// <summary>File has been successfully uploaded.</summary>
    Uploaded,

    /// <summary>File upload failed.</summary>
    Failed,

    /// <summary>File upload was cancelled.</summary>
    Cancelled
}

/// <summary>
/// Distinguishes whether an Upload event relates to an upload or a remove operation.
/// </summary>
public enum UploadOperationType
{
    /// <summary>The event is for an upload (save) operation.</summary>
    Upload,

    /// <summary>The event is for a remove (delete) operation.</summary>
    Remove
}

// ── Upload file info ─────────────────────────────────────────────────────────

/// <summary>
/// Represents information about a file being managed by SunfishUpload.
/// </summary>
public class UploadFileInfo
{
    /// <summary>Unique GUID identifier for this file entry.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>The display name of the file (including extension).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The file size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>The file extension (including the dot, e.g. ".pdf").</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>The current upload status.</summary>
    public UploadFileStatus Status { get; set; }

    /// <summary>The upload progress percentage (0-100).</summary>
    public int Progress { get; set; }

    /// <summary>True if the file extension violates AllowedExtensions.</summary>
    public bool InvalidExtension { get; set; }

    /// <summary>True if the file size exceeds MaxFileSize.</summary>
    public bool InvalidMaxFileSize { get; set; }

    /// <summary>True if the file size is below MinFileSize.</summary>
    public bool InvalidMinFileSize { get; set; }

    /// <summary>True if any validation check failed.</summary>
    public bool IsInvalid => InvalidExtension || InvalidMaxFileSize || InvalidMinFileSize;

    /// <summary>Internal: reference to the underlying IBrowserFile (null for initial/server-only entries).</summary>
    internal Microsoft.AspNetCore.Components.Forms.IBrowserFile? BrowserFile { get; set; }

    /// <summary>Internal: cancellation token source for in-flight uploads.</summary>
    internal System.Threading.CancellationTokenSource? CancellationTokenSource { get; set; }

    /// <summary>Internal: number of bytes successfully uploaded so far (used for chunk resume).</summary>
    internal long UploadedBytes { get; set; }

    /// <summary>Internal: number of auto-retries attempted for the current chunk.</summary>
    internal int ChunkRetryCount { get; set; }
}

// ── FileSelect file info ─────────────────────────────────────────────────────

/// <summary>
/// Represents a file entry surfaced by SunfishFileUpload (FileSelect) events.
/// </summary>
public class FileSelectFileInfo
{
    /// <summary>Unique identifier for this file entry.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>The display name of the file (including extension).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The file size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>The file extension (including the dot, e.g. ".pdf").</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>True if the file extension violates AllowedExtensions.</summary>
    public bool InvalidExtension { get; set; }

    /// <summary>True if the file size exceeds MaxFileSize.</summary>
    public bool InvalidMaxFileSize { get; set; }

    /// <summary>True if the file size is below MinFileSize.</summary>
    public bool InvalidMinFileSize { get; set; }

    /// <summary>True if any validation check failed.</summary>
    public bool IsInvalid => InvalidExtension || InvalidMaxFileSize || InvalidMinFileSize;

    /// <summary>A stream for reading the selected file contents asynchronously.</summary>
    public System.IO.Stream? Stream { get; set; }

    /// <summary>Internal: reference to the underlying IBrowserFile.</summary>
    internal Microsoft.AspNetCore.Components.Forms.IBrowserFile? BrowserFile { get; set; }
}

// ── HTTP response info ───────────────────────────────────────────────────────

/// <summary>
/// Contains information about the server response to an upload or remove request.
/// </summary>
public class UploadHttpRequest
{
    /// <summary>The HTTP response status code.</summary>
    public int Status { get; set; }

    /// <summary>The HTTP response status reason phrase.</summary>
    public string StatusText { get; set; } = string.Empty;

    /// <summary>The response content type.</summary>
    public string ResponseType { get; set; } = string.Empty;

    /// <summary>The response body text.</summary>
    public string ResponseText { get; set; } = string.Empty;
}

// ── Upload event args ────────────────────────────────────────────────────────

/// <summary>
/// Base event arguments for Upload events that carry a file list and request data/headers.
/// </summary>
public class UploadEventArgs
{
    /// <summary>The files involved in this event.</summary>
    public List<UploadFileInfo> Files { get; set; } = [];

    /// <summary>Set to true to cancel the event and suppress the default action.</summary>
    public bool IsCancelled { get; set; }

    /// <summary>Additional form data key-value pairs to send with the request.</summary>
    public Dictionary<string, object> RequestData { get; set; } = [];

    /// <summary>Additional HTTP header key-value pairs to send with the request.</summary>
    public Dictionary<string, object> RequestHeaders { get; set; } = [];
}

/// <summary>
/// Event arguments for the OnSelect event.
/// </summary>
public class UploadSelectEventArgs
{
    /// <summary>The files that were selected.</summary>
    public List<UploadFileInfo> Files { get; set; } = [];

    /// <summary>Set to true to cancel the event (files will not be added to the list).</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Event arguments for the OnError event.
/// </summary>
public class UploadErrorEventArgs
{
    /// <summary>The file associated with the error.</summary>
    public List<UploadFileInfo> Files { get; set; } = [];

    /// <summary>The type of operation that failed.</summary>
    public UploadOperationType Operation { get; set; }

    /// <summary>Information about the server response.</summary>
    public UploadHttpRequest Request { get; set; } = new();

    // Legacy: single-file error message (used by existing callers)
    internal string? ErrorMessage { get; set; }
}

/// <summary>
/// Event arguments for the OnSuccess event.
/// </summary>
public class UploadSuccessEventArgs
{
    /// <summary>The file that was successfully uploaded or removed.</summary>
    public List<UploadFileInfo> Files { get; set; } = [];

    /// <summary>The type of operation that succeeded.</summary>
    public UploadOperationType Operation { get; set; }

    /// <summary>Information about the server response.</summary>
    public UploadHttpRequest Request { get; set; } = new();
}

/// <summary>
/// Event arguments for the OnProgress event.
/// </summary>
public class UploadProgressEventArgs
{
    /// <summary>The file that is progressing.</summary>
    public List<UploadFileInfo> Files { get; set; } = [];

    /// <summary>The current upload progress percentage (0-100).</summary>
    public int Progress { get; set; }
}

/// <summary>
/// Event arguments for the OnCancel event.
/// </summary>
public class UploadCancelEventArgs
{
    /// <summary>The file whose upload was cancelled.</summary>
    public List<UploadFileInfo> Files { get; set; } = [];

    /// <summary>Set to true to prevent the cancellation (upload will continue).</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Event arguments for the OnClear event.
/// </summary>
public class UploadClearEventArgs
{
    /// <summary>The files that will be cleared.</summary>
    public List<UploadFileInfo> Files { get; set; } = [];

    /// <summary>Set to true to prevent the clear action.</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Event arguments for the OnPause event (chunk upload).
/// </summary>
public class UploadPauseEventArgs
{
    /// <summary>The file being paused.</summary>
    public List<UploadFileInfo> Files { get; set; } = [];

    /// <summary>Set to true to prevent pausing.</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Event arguments for the OnResume event (chunk upload).
/// </summary>
public class UploadResumeEventArgs
{
    /// <summary>The file being resumed.</summary>
    public List<UploadFileInfo> Files { get; set; } = [];

    /// <summary>Set to true to prevent resuming.</summary>
    public bool IsCancelled { get; set; }
}

// ── FileUpload template context ──────────────────────────────────────────────

/// <summary>
/// Context object passed to <see cref="SunfishFileUpload.FileTemplate"/> and
/// <see cref="SunfishFileUpload.FileInfoTemplate"/>. Wraps the raw file info with
/// convenience validation accessors for template authors.
/// </summary>
public class FileUploadTemplateContext
{
    /// <summary>The underlying file entry.</summary>
    public FileSelectFileInfo File { get; init; } = new();

    /// <summary>True if any validation rule was violated.</summary>
    public bool IsInvalid => File.IsInvalid;

    /// <summary>Human-readable summary of validation failures (empty string when valid).</summary>
    public string ValidationMessage { get; init; } = string.Empty;
}

// ── FileSelect event args ────────────────────────────────────────────────────

/// <summary>
/// Event arguments for SunfishFileUpload (FileSelect) OnSelect and OnRemove events.
/// </summary>
public class FileSelectEventArgs
{
    /// <summary>The files involved in this event.</summary>
    public List<FileSelectFileInfo> Files { get; set; } = [];

    /// <summary>Set to true to cancel the event and suppress the default action.</summary>
    public bool IsCancelled { get; set; }
}
