using Sunfish.UIAdapters.Blazor.Internal.Interop;
using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.Foundation.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// An upload component that sends files to a remote endpoint asynchronously.
/// Supports auto-upload or deferred upload, drag-and-drop, chunk upload, initial files,
/// pause/resume/cancel per file, and full event model.
/// </summary>
public partial class SunfishUpload : SunfishComponentBase, IUploadChunkSettingsSink
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IDropZoneService DropZoneService { get; set; } = default!;

    // ── Parameters: Endpoints ────────────────────────────────────────────────

    /// <summary>The URL that receives uploaded files via HTTP POST.</summary>
    [Parameter] public string SaveUrl { get; set; } = string.Empty;

    /// <summary>The FormData key name for the file in the save request (default "files").</summary>
    [Parameter] public string SaveField { get; set; } = "files";

    /// <summary>The URL that receives file names for deletion via HTTP POST.</summary>
    [Parameter] public string? RemoveUrl { get; set; }

    /// <summary>The FormData key name for the file name in the remove request (default "files").</summary>
    [Parameter] public string RemoveField { get; set; } = "files";

    // ── Parameters: Behaviour ────────────────────────────────────────────────

    /// <summary>When true, upload starts automatically after file selection.</summary>
    [Parameter] public bool AutoUpload { get; set; } = true;

    /// <summary>When false, disables file selection and upload.</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>When true, allows multiple files to be selected at once.</summary>
    [Parameter] public bool Multiple { get; set; } = true;

    /// <summary>Controls if the upload sends credentials for cross-site requests.</summary>
    [Parameter] public bool WithCredentials { get; set; }

    // ── Parameters: Validation ───────────────────────────────────────────────

    /// <summary>The accept attribute of the underlying file input.</summary>
    [Parameter] public string? Accept { get; set; }

    /// <summary>List of allowed file extensions (e.g. ".pdf", ".jpg").</summary>
    [Parameter] public List<string>? AllowedExtensions { get; set; }

    /// <summary>Maximum file size in bytes (client-side validation only).</summary>
    [Parameter] public long? MaxFileSize { get; set; }

    /// <summary>Minimum file size in bytes (client-side validation only).</summary>
    [Parameter] public long? MinFileSize { get; set; }

    // ── Parameters: Accessibility / Identity ────────────────────────────────

    /// <summary>The capture attribute for the file input (e.g. "user", "environment").</summary>
    [Parameter] public string? Capture { get; set; }

    /// <summary>An id attribute rendered on the file input element.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>The id of an external drop zone to connect to.</summary>
    [Parameter] public string? DropZoneId { get; set; }

    // ── Parameters: Chunk upload ─────────────────────────────────────────────

    /// <summary>Size of each chunk in bytes. Default 1 MB. Set to 0 to disable chunking.</summary>
    [Parameter] public long ChunkSize { get; set; } = 1_048_576;

    // ── Parameters: Initial files ────────────────────────────────────────────

    /// <summary>Pre-populated files displayed in the list on load.</summary>
    [Parameter] public IEnumerable<UploadFileInfo>? Files { get; set; }

    // ── Parameters: Templates ────────────────────────────────────────────────

    /// <summary>Custom template replacing the entire drop zone content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Custom template for the file selection button content.</summary>
    [Parameter] public RenderFragment? SelectFilesButtonTemplate { get; set; }

    /// <summary>Custom template for each file in the file list.</summary>
    [Parameter] public RenderFragment<UploadFileInfo>? FileTemplate { get; set; }

    /// <summary>Custom template for the file info section (name, size, error) within each file entry.</summary>
    [Parameter] public RenderFragment<UploadFileInfo>? FileInfoTemplate { get; set; }

    // ── Parameters: Events ───────────────────────────────────────────────────

    /// <summary>Fires when files are selected. Cancel to prevent listing/uploading.</summary>
    [Parameter] public EventCallback<UploadSelectEventArgs> OnSelect { get; set; }

    /// <summary>Fires when files are about to be uploaded. Cancel to stop the upload.</summary>
    [Parameter] public EventCallback<UploadEventArgs> OnUpload { get; set; }

    /// <summary>Fires when an upload or remove request succeeds.</summary>
    [Parameter] public EventCallback<UploadSuccessEventArgs> OnSuccess { get; set; }

    /// <summary>Fires when an upload or remove request fails.</summary>
    [Parameter] public EventCallback<UploadErrorEventArgs> OnError { get; set; }

    /// <summary>Fires when upload progress is reported for a file.</summary>
    [Parameter] public EventCallback<UploadProgressEventArgs> OnProgress { get; set; }

    /// <summary>Fires when a file remove is initiated. Cancel to suppress the request.</summary>
    [Parameter] public EventCallback<UploadEventArgs> OnRemove { get; set; }

    /// <summary>Fires when the user cancels an in-flight upload.</summary>
    [Parameter] public EventCallback<UploadCancelEventArgs> OnCancel { get; set; }

    /// <summary>Fires when the user clears the file list (AutoUpload=false only).</summary>
    [Parameter] public EventCallback<UploadClearEventArgs> OnClear { get; set; }

    /// <summary>Fires when chunk upload is paused.</summary>
    [Parameter] public EventCallback<UploadPauseEventArgs> OnPause { get; set; }

    /// <summary>Fires when chunk upload is resumed.</summary>
    [Parameter] public EventCallback<UploadResumeEventArgs> OnResume { get; set; }

    /// <summary>Specifies the adaptive rendering mode for the popup on mobile devices.</summary>
    [Parameter] public AdaptiveMode AdaptiveMode { get; set; } = AdaptiveMode.None;

    // ── IUploadChunkSettingsSink ──────────────────────────────────────────────

    private SunfishUploadChunkSettings? _chunkSettings;

    void IUploadChunkSettingsSink.RegisterChunkSettings(SunfishUploadChunkSettings settings)
    {
        _chunkSettings = settings;
        InvokeAsync(StateHasChanged);
    }

    void IUploadChunkSettingsSink.UnregisterChunkSettings(SunfishUploadChunkSettings settings)
    {
        if (ReferenceEquals(_chunkSettings, settings))
        {
            _chunkSettings = null;
            InvokeAsync(StateHasChanged);
        }
    }

    // ── Effective chunk settings (child overrides flat parameters) ────────────

    private long EffectiveChunkSize => _chunkSettings?.ChunkSize ?? ChunkSize;
    private int? EffectiveAutoRetryAfter => _chunkSettings?.AutoRetryAfter;
    private int EffectiveMaxAutoRetries => _chunkSettings?.MaxAutoRetries ?? 0;
    private string? EffectiveMetadataField => _chunkSettings?.MetadataField;
    private bool EffectiveResumable => _chunkSettings?.Resumable ?? true;

    // ── Internal state ────────────────────────────────────────────────────────

    private readonly List<UploadFileInfo> _files = [];
    private bool _isDragOver;
    private bool _filesParamInitialized;
    private string InputId => Id ?? $"mar-upload-{_componentId}";
    private readonly string _componentId = Guid.NewGuid().ToString("N")[..8];
    private int _dropZoneHandleId = -1;
    private string? _previousDropZoneId;
    private bool _dropZoneRegistrationPending;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (!_filesParamInitialized && Files != null)
        {
            foreach (var f in Files)
            {
                if (!_files.Any(x => x.Id == f.Id))
                {
                    // Pre-loaded files are considered already uploaded
                    if (f.Status == UploadFileStatus.Selected)
                        f.Status = UploadFileStatus.Uploaded;
                    _files.Add(f);
                }
            }
            _filesParamInitialized = true;
        }

        // Track DropZoneId changes for re-registration
        if (DropZoneId != _previousDropZoneId)
        {
            _dropZoneRegistrationPending = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !string.IsNullOrEmpty(DropZoneId))
        {
            _previousDropZoneId = DropZoneId;
            _dropZoneHandleId = await DropZoneService.RegisterAsync(DropZoneId, InputId);
            _dropZoneRegistrationPending = false;
        }
        else if (_dropZoneRegistrationPending)
        {
            // Unregister old drop zone if active
            if (_dropZoneHandleId >= 0)
            {
                try { await DropZoneService.UnregisterAsync(_dropZoneHandleId); }
                catch (JSDisconnectedException) { }
                _dropZoneHandleId = -1;
            }

            // Register new drop zone if set
            if (!string.IsNullOrEmpty(DropZoneId))
            {
                _dropZoneHandleId = await DropZoneService.RegisterAsync(DropZoneId, InputId);
            }

            _previousDropZoneId = DropZoneId;
            _dropZoneRegistrationPending = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dropZoneHandleId >= 0)
        {
            try { await DropZoneService.UnregisterAsync(_dropZoneHandleId); }
            catch (JSDisconnectedException) { }
            _dropZoneHandleId = -1;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Clears all files from the list.</summary>
    public async Task ClearFiles()
    {
        var args = new UploadClearEventArgs { Files = [.. _files] };
        await OnClear.InvokeAsync(args);
        if (!args.IsCancelled)
        {
            // Cancel any in-flight uploads
            foreach (var f in _files.Where(f => f.CancellationTokenSource != null))
                f.CancellationTokenSource!.Cancel();
            _files.Clear();
            StateHasChanged();
        }
    }

    /// <summary>Uploads all valid selected files that have not yet been uploaded.</summary>
    public async Task UploadFiles()
    {
        var toUpload = _files
            .Where(f => f.Status == UploadFileStatus.Selected && !f.IsInvalid && f.BrowserFile != null)
            .ToList();

        if (!toUpload.Any()) return;

        var uploadArgs = new UploadEventArgs
        {
            Files = toUpload,
            RequestData = [],
            RequestHeaders = []
        };
        await OnUpload.InvokeAsync(uploadArgs);
        if (uploadArgs.IsCancelled) return;

        foreach (var f in toUpload)
            await UploadFileAsync(f, uploadArgs.RequestData, uploadArgs.RequestHeaders);
    }

    /// <summary>Cancels the upload of the file with the given id.</summary>
    public async Task CancelFile(string fileId)
    {
        var file = _files.FirstOrDefault(f => f.Id == fileId);
        if (file == null) return;

        var args = new UploadCancelEventArgs { Files = [file] };
        await OnCancel.InvokeAsync(args);
        if (!args.IsCancelled)
        {
            file.CancellationTokenSource?.Cancel();
            file.Status = UploadFileStatus.Cancelled;
            _files.Remove(file);
            StateHasChanged();
        }
    }

    /// <summary>Pauses the chunk upload of the file with the given id.</summary>
    public async Task PauseFile(string fileId)
    {
        var file = _files.FirstOrDefault(f => f.Id == fileId && f.Status == UploadFileStatus.Uploading);
        if (file == null) return;

        var args = new UploadPauseEventArgs { Files = [file] };
        await OnPause.InvokeAsync(args);
        if (!args.IsCancelled)
        {
            file.CancellationTokenSource?.Cancel();
            file.Status = UploadFileStatus.Paused;
            StateHasChanged();
        }
    }

    /// <summary>Resumes a paused chunk upload for the file with the given id.</summary>
    public async Task ResumeFile(string fileId)
    {
        var file = _files.FirstOrDefault(f => f.Id == fileId && f.Status == UploadFileStatus.Paused);
        if (file == null || file.BrowserFile == null) return;

        var args = new UploadResumeEventArgs { Files = [file] };
        await OnResume.InvokeAsync(args);
        if (!args.IsCancelled)
        {
            if (!EffectiveResumable)
                file.UploadedBytes = 0; // non-resumable: restart from byte 0
            file.CancellationTokenSource = new System.Threading.CancellationTokenSource();
            file.Status = UploadFileStatus.Uploading;
            await UploadFileAsync(file, [], []);
        }
    }

    /// <summary>Retries a failed upload for the file with the given id.</summary>
    public async Task RetryFile(string fileId)
    {
        var file = _files.FirstOrDefault(f => f.Id == fileId && f.Status == UploadFileStatus.Failed);
        if (file == null || file.BrowserFile == null) return;

        file.CancellationTokenSource = new System.Threading.CancellationTokenSource();
        file.Status = UploadFileStatus.Selected;
        file.UploadedBytes = 0;
        await UploadFileAsync(file, [], []);
    }

    /// <summary>Removes the file with the given id from the list.</summary>
    public async Task RemoveFile(string fileId)
    {
        var file = _files.FirstOrDefault(f => f.Id == fileId);
        if (file != null)
            await RemoveFileInternalAsync(file);
    }

    /// <summary>
    /// Opens the browser file selection dialog programmatically.
    /// Note: Does not work in Safari due to browser security restrictions.
    /// </summary>
    public async Task OpenSelectFilesDialog()
    {
        await JS.InvokeVoidAsync("eval", $"document.getElementById('{InputId}')?.click()");
    }

    // ── Selection handler ─────────────────────────────────────────────────────

    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        if (!Enabled) return;

        const int maxFiles = 1000;
        var browserFiles = Multiple
            ? e.GetMultipleFiles(maxFiles).ToList()
            : [e.File];

        var newEntries = new List<UploadFileInfo>();
        foreach (var bf in browserFiles)
        {
            var ext = System.IO.Path.GetExtension(bf.Name)?.ToLowerInvariant() ?? string.Empty;
            var entry = new UploadFileInfo
            {
                Name = bf.Name,
                Size = bf.Size,
                Extension = ext,
                Status = UploadFileStatus.Selected,
                BrowserFile = bf,
                InvalidExtension = IsInvalidExtension(ext),
                InvalidMaxFileSize = MaxFileSize.HasValue && bf.Size > MaxFileSize.Value,
                InvalidMinFileSize = MinFileSize.HasValue && bf.Size < MinFileSize.Value
            };

            _files.Add(entry);
            newEntries.Add(entry);
        }

        var selectArgs = new UploadSelectEventArgs { Files = newEntries };
        await OnSelect.InvokeAsync(selectArgs);

        if (selectArgs.IsCancelled)
        {
            foreach (var e2 in newEntries)
                _files.Remove(e2);
            return;
        }

        if (AutoUpload)
        {
            // Valid files only
            var validFiles = newEntries.Where(f => !f.IsInvalid).ToList();
            if (!validFiles.Any()) return;

            var uploadArgs = new UploadEventArgs
            {
                Files = validFiles,
                RequestData = [],
                RequestHeaders = []
            };
            await OnUpload.InvokeAsync(uploadArgs);
            if (uploadArgs.IsCancelled) return;

            foreach (var f in validFiles)
                await UploadFileAsync(f, uploadArgs.RequestData, uploadArgs.RequestHeaders);
        }
    }

    // ── Upload logic ──────────────────────────────────────────────────────────

    private async Task UploadFileAsync(
        UploadFileInfo info,
        Dictionary<string, object> requestData,
        Dictionary<string, object> requestHeaders)
    {
        if (info.BrowserFile == null || string.IsNullOrEmpty(SaveUrl)) return;

        var isResume = info.UploadedBytes > 0;
        info.CancellationTokenSource = new System.Threading.CancellationTokenSource();
        info.Status = UploadFileStatus.Uploading;
        if (!isResume)
            info.Progress = 0;
        StateHasChanged();

        try
        {
            var maxReadSize = MaxFileSize ?? 2L * 1024 * 1024 * 1024;

            if (EffectiveChunkSize > 0 && info.BrowserFile.Size > EffectiveChunkSize)
            {
                await UploadChunkedAsync(info, requestData, requestHeaders, maxReadSize);
            }
            else
            {
                await UploadWholeAsync(info, requestData, requestHeaders, maxReadSize);
            }
        }
        catch (OperationCanceledException)
        {
            // Paused or cancelled — leave status as-is (set by CancelFile/PauseFile)
            if (info.Status == UploadFileStatus.Uploading)
                info.Status = UploadFileStatus.Cancelled;
        }
        catch (Exception ex)
        {
            info.Status = UploadFileStatus.Failed;
            await OnError.InvokeAsync(new UploadErrorEventArgs
            {
                Files = [info],
                Operation = UploadOperationType.Upload,
                Request = new UploadHttpRequest { StatusText = ex.Message }
            });
        }
        finally
        {
            info.CancellationTokenSource?.Dispose();
            info.CancellationTokenSource = null;
        }

        StateHasChanged();
    }

    private async Task UploadWholeAsync(
        UploadFileInfo info,
        Dictionary<string, object> requestData,
        Dictionary<string, object> requestHeaders,
        long maxReadSize)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(info.BrowserFile!.OpenReadStream(maxReadSize));
        content.Add(streamContent, SaveField, info.Name);

        foreach (var kv in requestData)
            content.Add(new StringContent(kv.Value?.ToString() ?? ""), kv.Key);

        using var request = new HttpRequestMessage(HttpMethod.Post, SaveUrl) { Content = content };
        if (WithCredentials)
            request.Options.Set(new HttpRequestOptionsKey<bool>("WebAssemblyFetchCredentials"), true);
        foreach (var kv in requestHeaders)
            request.Headers.TryAddWithoutValidation(kv.Key, kv.Value?.ToString());

        var response = await Http.SendAsync(request, info.CancellationTokenSource!.Token);
        var responseText = await response.Content.ReadAsStringAsync();
        var httpReq = new UploadHttpRequest
        {
            Status = (int)response.StatusCode,
            StatusText = response.ReasonPhrase ?? "",
            ResponseType = response.Content.Headers.ContentType?.MediaType ?? "",
            ResponseText = responseText
        };

        if (response.IsSuccessStatusCode)
        {
            info.Status = UploadFileStatus.Uploaded;
            info.Progress = 100;
            await OnSuccess.InvokeAsync(new UploadSuccessEventArgs
            {
                Files = [info],
                Operation = UploadOperationType.Upload,
                Request = httpReq
            });
        }
        else
        {
            info.Status = UploadFileStatus.Failed;
            await OnError.InvokeAsync(new UploadErrorEventArgs
            {
                Files = [info],
                Operation = UploadOperationType.Upload,
                Request = httpReq
            });
        }
    }

    private async Task UploadChunkedAsync(
        UploadFileInfo info,
        Dictionary<string, object> requestData,
        Dictionary<string, object> requestHeaders,
        long maxReadSize)
    {
        var chunkSize = EffectiveChunkSize;
        var totalChunks = (int)Math.Ceiling((double)info.BrowserFile!.Size / chunkSize);
        var buffer = new byte[chunkSize];
        var startChunk = (int)(info.UploadedBytes / chunkSize);

        await using var stream = info.BrowserFile.OpenReadStream(maxReadSize);

        // Skip past already-uploaded bytes when resuming
        if (info.UploadedBytes > 0 && startChunk > 0)
        {
            var bytesToSkip = info.UploadedBytes;
            var skipBuffer = new byte[Math.Min(81920, bytesToSkip)];
            while (bytesToSkip > 0)
            {
                var toRead = (int)Math.Min(skipBuffer.Length, bytesToSkip);
                var read = await stream.ReadAsync(skipBuffer.AsMemory(0, toRead), info.CancellationTokenSource!.Token);
                if (read == 0) break;
                bytesToSkip -= read;
            }
        }

        for (var chunkIndex = startChunk; chunkIndex < totalChunks; chunkIndex++)
        {
            // Check for cancellation/pause between chunks
            info.CancellationTokenSource!.Token.ThrowIfCancellationRequested();

            var bytesToRead = (int)Math.Min(chunkSize, info.BrowserFile.Size - (long)chunkIndex * chunkSize);
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bytesToRead), info.CancellationTokenSource.Token);

            using var chunkContent = new MultipartFormDataContent();
            using var byteContent = new ByteArrayContent(buffer, 0, bytesRead);
            chunkContent.Add(byteContent, SaveField, info.Name);

            // Standard chunk metadata
            chunkContent.Add(new StringContent(chunkIndex.ToString()), "chunkIndex");
            chunkContent.Add(new StringContent(totalChunks.ToString()), "totalChunks");
            chunkContent.Add(new StringContent(info.Name), "fileName");
            chunkContent.Add(new StringContent(info.Size.ToString()), "totalSize");

            // Optional metadata field from chunk settings
            if (!string.IsNullOrEmpty(EffectiveMetadataField))
                chunkContent.Add(new StringContent(info.Id), EffectiveMetadataField);

            foreach (var kv in requestData)
                chunkContent.Add(new StringContent(kv.Value?.ToString() ?? ""), kv.Key);

            using var request = new HttpRequestMessage(HttpMethod.Post, SaveUrl) { Content = chunkContent };
            if (WithCredentials)
                request.Options.Set(new HttpRequestOptionsKey<bool>("WebAssemblyFetchCredentials"), true);
            foreach (var kv in requestHeaders)
                request.Headers.TryAddWithoutValidation(kv.Key, kv.Value?.ToString());

            var response = await Http.SendAsync(request, info.CancellationTokenSource.Token);

            if (!response.IsSuccessStatusCode)
            {
                // Auto-retry logic when EffectiveAutoRetryAfter is configured
                if (EffectiveAutoRetryAfter.HasValue && info.ChunkRetryCount < EffectiveMaxAutoRetries)
                {
                    info.ChunkRetryCount++;
                    await Task.Delay(EffectiveAutoRetryAfter.Value, info.CancellationTokenSource.Token);
                    chunkIndex--; // retry same chunk
                    continue;
                }

                var responseText = await response.Content.ReadAsStringAsync();
                info.Status = UploadFileStatus.Failed;
                info.ChunkRetryCount = 0;
                await OnError.InvokeAsync(new UploadErrorEventArgs
                {
                    Files = [info],
                    Operation = UploadOperationType.Upload,
                    Request = new UploadHttpRequest
                    {
                        Status = (int)response.StatusCode,
                        StatusText = response.ReasonPhrase ?? "",
                        ResponseText = responseText
                    }
                });
                return;
            }

            info.ChunkRetryCount = 0; // reset retry counter on success
            info.UploadedBytes = (long)(chunkIndex + 1) * chunkSize;
            if (info.UploadedBytes > info.BrowserFile.Size)
                info.UploadedBytes = info.BrowserFile.Size;

            var newProgress = (int)((chunkIndex + 1) * 100.0 / totalChunks);
            info.Progress = newProgress;

            await OnProgress.InvokeAsync(new UploadProgressEventArgs
            {
                Files = [info],
                Progress = newProgress
            });

            StateHasChanged();
        }

        info.Status = UploadFileStatus.Uploaded;
        info.Progress = 100;

        var finalResponse = new UploadHttpRequest { Status = 200, StatusText = "OK" };
        await OnSuccess.InvokeAsync(new UploadSuccessEventArgs
        {
            Files = [info],
            Operation = UploadOperationType.Upload,
            Request = finalResponse
        });
    }

    // ── Remove handler ────────────────────────────────────────────────────────

    private async Task RemoveFileInternalAsync(UploadFileInfo file)
    {
        var args = new UploadEventArgs
        {
            Files = [file],
            RequestData = [],
            RequestHeaders = []
        };
        await OnRemove.InvokeAsync(args);
        if (args.IsCancelled) return;

        // If file is uploading, cancel first
        if (file.Status == UploadFileStatus.Uploading || file.Status == UploadFileStatus.Paused)
        {
            file.CancellationTokenSource?.Cancel();
        }

        if (!string.IsNullOrEmpty(RemoveUrl) && file.Status == UploadFileStatus.Uploaded)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(file.Name), RemoveField);

                foreach (var kv in args.RequestData)
                    content.Add(new StringContent(kv.Value?.ToString() ?? ""), kv.Key);

                using var request = new HttpRequestMessage(HttpMethod.Post, RemoveUrl) { Content = content };
                if (WithCredentials)
                    request.Options.Set(new HttpRequestOptionsKey<bool>("WebAssemblyFetchCredentials"), true);
                foreach (var kv in args.RequestHeaders)
                    request.Headers.TryAddWithoutValidation(kv.Key, kv.Value?.ToString());

                var response = await Http.SendAsync(request);
                var responseText = await response.Content.ReadAsStringAsync();
                var httpReq = new UploadHttpRequest
                {
                    Status = (int)response.StatusCode,
                    StatusText = response.ReasonPhrase ?? "",
                    ResponseText = responseText
                };

                if (response.IsSuccessStatusCode)
                {
                    _files.Remove(file);
                    await OnSuccess.InvokeAsync(new UploadSuccessEventArgs
                    {
                        Files = [file],
                        Operation = UploadOperationType.Remove,
                        Request = httpReq
                    });
                }
                else
                {
                    await OnError.InvokeAsync(new UploadErrorEventArgs
                    {
                        Files = [file],
                        Operation = UploadOperationType.Remove,
                        Request = httpReq
                    });
                }
            }
            catch (Exception ex)
            {
                await OnError.InvokeAsync(new UploadErrorEventArgs
                {
                    Files = [file],
                    Operation = UploadOperationType.Remove,
                    Request = new UploadHttpRequest { StatusText = ex.Message }
                });
            }
        }
        else
        {
            _files.Remove(file);
        }

        StateHasChanged();
    }

    // ── Drag handlers ─────────────────────────────────────────────────────────

    private void HandleDragEnter(Microsoft.AspNetCore.Components.Web.DragEventArgs e) => _isDragOver = true;
    private void HandleDragOver(Microsoft.AspNetCore.Components.Web.DragEventArgs e) { }
    private void HandleDragLeave(Microsoft.AspNetCore.Components.Web.DragEventArgs e) => _isDragOver = false;
    private void HandleDrop(Microsoft.AspNetCore.Components.Web.DragEventArgs e) => _isDragOver = false;

    // ── Validation helpers ────────────────────────────────────────────────────

    private bool IsInvalidExtension(string ext)
    {
        if (AllowedExtensions == null || !AllowedExtensions.Any()) return false;
        return !AllowedExtensions.Select(e => e.ToLowerInvariant()).Contains(ext);
    }

    // ── CSS helpers ───────────────────────────────────────────────────────────

    private string DropZoneCssClass()
    {
        var cls = CssProvider.UploadDropZoneClass(_isDragOver);
        if (!Enabled) cls += " mar-upload__drop-zone--disabled";
        return cls;
    }

    // ── Format helpers ────────────────────────────────────────────────────────

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }

    private static string StatusLabel(UploadFileStatus status) => status switch
    {
        UploadFileStatus.Selected => "Ready",
        UploadFileStatus.Uploading => "Uploading",
        UploadFileStatus.Paused => "Paused",
        UploadFileStatus.Uploaded => "Done",
        UploadFileStatus.Failed => "Failed",
        UploadFileStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    // ── Computed view predicates ──────────────────────────────────────────────

    private bool CanUploadManually => !AutoUpload && _files.Any(f => f.Status == UploadFileStatus.Selected && !f.IsInvalid);
    private bool CanClear => !AutoUpload && _files.Any();
}
