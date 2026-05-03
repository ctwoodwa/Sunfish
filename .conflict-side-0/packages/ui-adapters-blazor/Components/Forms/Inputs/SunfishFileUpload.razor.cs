using Sunfish.UIAdapters.Blazor.Internal.Interop;
using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// A unified file input component that covers both the FileSelect (selection-only) and
/// Upload (server-backed) specs. When <see cref="SaveUrl"/> is <c>null</c> or empty, the
/// component behaves as a FileSelect: it exposes picked files via <see cref="FilesChanged"/>
/// and does not issue HTTP requests. When <see cref="SaveUrl"/> is set, files POST to the
/// endpoint with progress reporting, optional chunking, and resumable transfers.
/// </summary>
/// <remarks>
/// Chunked uploads use an <see cref="HttpClient"/> injected from DI (a scoped instance, not
/// <c>IHttpClientFactory</c>). Each chunk is posted as <c>multipart/form-data</c> with a
/// <c>Content-Range: bytes {start}-{end}/{total}</c> header and the file bytes bound to
/// the <see cref="SaveField"/> key. Headers from <see cref="HttpHeaders"/> and from
/// <see cref="OnUpload"/>'s <c>RequestHeaders</c> are merged into every request.
/// </remarks>
public partial class SunfishFileUpload : SunfishComponentBase, IAsyncDisposable
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IDropZoneService DropZoneService { get; set; } = default!;

    // -- Parameters: Mode / endpoints ---------------------------------------

    /// <summary>HTTP POST endpoint that receives uploaded files. When null/empty the
    /// component behaves as FileSelect (no HTTP traffic; use <see cref="FilesChanged"/>).</summary>
    [Parameter] public string? SaveUrl { get; set; }

    /// <summary>The multipart/form-data key name for the file payload (default "files").</summary>
    [Parameter] public string SaveField { get; set; } = "files";

    /// <summary>HTTP endpoint invoked when the user removes an uploaded file.
    /// A POST is issued with <see cref="RemoveField"/> bound to the file name.</summary>
    [Parameter] public string? RemoveUrl { get; set; }

    /// <summary>The multipart/form-data key name for the remove request (default "files").</summary>
    [Parameter] public string RemoveField { get; set; } = "files";

    // -- Parameters: Behaviour ----------------------------------------------

    /// <summary>When true (default), uploads start automatically after selection.
    /// When false, the component renders an explicit "Upload" button.</summary>
    [Parameter] public bool AutoUpload { get; set; } = true;

    /// <summary>Allow picking more than one file. Defaults to true.</summary>
    [Parameter] public bool Multiple { get; set; } = true;

    /// <summary>Show the selected-files list. Defaults to true.</summary>
    [Parameter] public bool ShowFileList { get; set; } = true;

    /// <summary>
    /// When true, uploads every valid selected file in a single batched request
    /// (a single multipart POST with repeated <see cref="SaveField"/> parts).
    /// Default is false - each file posts separately, matching the Sunfish spec.
    /// </summary>
    [Parameter] public bool Batch { get; set; }

    /// <summary>Send cookies and auth headers for cross-origin uploads
    /// (sets the WebAssemblyFetchCredentials request option).</summary>
    [Parameter] public bool WithCredentials { get; set; }

    /// <summary>Extra HTTP headers merged into every upload/remove request.</summary>
    [Parameter] public IDictionary<string, string>? HttpHeaders { get; set; }

    /// <summary>
    /// Chunk size in bytes. When greater than zero and smaller than the file size, the upload
    /// is streamed as sequential chunk POSTs with a Content-Range header. Set to 0 (default)
    /// to upload each file in a single POST.
    /// </summary>
    [Parameter] public long ChunkSize { get; set; }

    /// <summary>
    /// When true, <see cref="SunfishFileUpload"/> remembers the byte offset of a
    /// paused/failed chunk upload and resumes from that offset on retry. Defaults
    /// to true; only applies when <see cref="ChunkSize"/> is greater than zero.
    /// </summary>
    [Parameter] public bool ResumableUploads { get; set; } = true;

    /// <summary>When false, disables file selection and any in-flight uploads.</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>Disables the component (maps to <see cref="Enabled"/> = false).</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>When true, renders the component in read-only mode (no selection, list stays).</summary>
    [Parameter] public bool ReadOnly { get; set; }

    // -- Parameters: Validation ---------------------------------------------

    /// <summary>The accept attribute passed to the underlying file input.</summary>
    [Parameter] public string? Accept { get; set; }

    /// <summary>List of allowed file extensions (for example .pdf, .jpg).</summary>
    [Parameter] public IList<string>? AllowedExtensions { get; set; }

    /// <summary>Minimum allowed file size in bytes. <c>null</c> disables the check.</summary>
    [Parameter] public long? MinFileSize { get; set; }

    /// <summary>Maximum allowed file size in bytes. <c>null</c> disables the check.</summary>
    [Parameter] public long? MaxFileSize { get; set; }

    // -- Parameters: Accessibility / Identity -------------------------------

    /// <summary>The capture attribute (for example "user", "environment").</summary>
    [Parameter] public string? Capture { get; set; }

    /// <summary>An id attribute rendered on the underlying file input.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Id of an external drop-zone to bind via the <see cref="IDropZoneService"/>.</summary>
    [Parameter] public string? DropZoneId { get; set; }

    /// <summary>
    /// A selector (by id) for an external drop-zone element. Takes precedence
    /// over <see cref="DropZoneId"/> when both are supplied.
    /// </summary>
    [Parameter] public string? DropZoneElement { get; set; }

    // -- Parameters: Initial / Bound state ----------------------------------

    /// <summary>Files pre-populated into the list on first render (treated as already uploaded).</summary>
    [Parameter] public IEnumerable<FileSelectFileInfo>? Files { get; set; }

    // -- Parameters: Templates ----------------------------------------------

    /// <summary>Custom content for the select-files button region.</summary>
    [Parameter] public RenderFragment? SelectFilesButtonTemplate { get; set; }

    /// <summary>Per-file item template. Replaces the built-in layout entirely.</summary>
    [Parameter] public RenderFragment<FileUploadTemplateContext>? FileTemplate { get; set; }

    /// <summary>Per-file info template (name + size + error); keeps the built-in actions.</summary>
    [Parameter] public RenderFragment<FileUploadTemplateContext>? FileInfoTemplate { get; set; }

    // -- Parameters: Events -------------------------------------------------

    /// <summary>Fires when files are picked; set args.IsCancelled=true to veto.</summary>
    [Parameter] public EventCallback<FileUploadSelectEventArgs> OnSelect { get; set; }

    /// <summary>Fires before each upload POST; exposes RequestData / RequestHeaders.</summary>
    [Parameter] public EventCallback<UploadEventArgs> OnUpload { get; set; }

    /// <summary>Fires after a chunk or per-file progress update.</summary>
    [Parameter] public EventCallback<UploadProgressEventArgs> OnProgress { get; set; }

    /// <summary>Fires after an upload or remove returns a 2xx status.</summary>
    [Parameter] public EventCallback<UploadSuccessEventArgs> OnSuccess { get; set; }

    /// <summary>Fires after an upload or remove fails with a non-2xx / network error.</summary>
    [Parameter] public EventCallback<UploadErrorEventArgs> OnError { get; set; }

    /// <summary>Fires when the user cancels an in-flight upload.</summary>
    [Parameter] public EventCallback<UploadCancelEventArgs> OnCancel { get; set; }

    /// <summary>Fires when the user removes a file from the queue.</summary>
    [Parameter] public EventCallback<UploadEventArgs> OnRemove { get; set; }

    /// <summary>Fires when the user clears the entire file list (manual mode).</summary>
    [Parameter] public EventCallback<UploadClearEventArgs> OnClear { get; set; }

    /// <summary>Fires whenever the selected-files list changes (for FileSelect-mode binding).</summary>
    [Parameter] public EventCallback<IList<SelectedFile>> FilesChanged { get; set; }

    /// <summary>Specifies the adaptive rendering mode for the popup on mobile devices.</summary>
    [Parameter] public AdaptiveMode AdaptiveMode { get; set; } = AdaptiveMode.None;

    // -- Internal state -----------------------------------------------------

    private readonly List<SelectedFile> _files = [];
    private bool _isDragOver;
    private bool _filesParamInitialized;
    private string InputId => Id ?? $"mar-fileupload-{_componentId}";
    private readonly string _componentId = Guid.NewGuid().ToString("N")[..8];
    private int _dropZoneHandleId = -1;
    private string? _previousDropZoneId;
    private bool _dropZoneRegistrationPending;

    private bool IsUploadMode => !string.IsNullOrEmpty(SaveUrl);
    private bool IsInteractive => Enabled && !Disabled && !ReadOnly;

    // -- Lifecycle ----------------------------------------------------------

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!_filesParamInitialized && Files != null)
        {
            foreach (var f in Files)
            {
                if (_files.Any(x => x.Id == f.Id)) continue;

                _files.Add(new SelectedFile
                {
                    Id = f.Id,
                    Name = f.Name,
                    Size = f.Size,
                    Extension = f.Extension,
                    InvalidExtension = f.InvalidExtension,
                    InvalidMaxFileSize = f.InvalidMaxFileSize,
                    InvalidMinFileSize = f.InvalidMinFileSize,
                    Status = UploadFileStatus.Uploaded,
                });
            }
            _filesParamInitialized = true;
        }

        var resolvedDropZone = DropZoneElement ?? DropZoneId;
        if (resolvedDropZone != _previousDropZoneId)
        {
            _dropZoneRegistrationPending = true;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var resolvedDropZone = DropZoneElement ?? DropZoneId;

        if (firstRender && !string.IsNullOrEmpty(resolvedDropZone))
        {
            _previousDropZoneId = resolvedDropZone;
            _dropZoneHandleId = await DropZoneService.RegisterAsync(resolvedDropZone, InputId);
            _dropZoneRegistrationPending = false;
        }
        else if (_dropZoneRegistrationPending)
        {
            if (_dropZoneHandleId >= 0)
            {
                try { await DropZoneService.UnregisterAsync(_dropZoneHandleId); }
                catch (JSDisconnectedException) { }
                _dropZoneHandleId = -1;
            }

            if (!string.IsNullOrEmpty(resolvedDropZone))
            {
                _dropZoneHandleId = await DropZoneService.RegisterAsync(resolvedDropZone, InputId);
            }

            _previousDropZoneId = resolvedDropZone;
            _dropZoneRegistrationPending = false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var f in _files)
        {
            try { f.CancellationTokenSource?.Cancel(); } catch { /* best effort */ }
            f.CancellationTokenSource?.Dispose();
            f.CancellationTokenSource = null;
        }

        if (_dropZoneHandleId >= 0)
        {
            try { await DropZoneService.UnregisterAsync(_dropZoneHandleId); }
            catch (JSDisconnectedException) { }
            _dropZoneHandleId = -1;
        }
    }

    // -- Public API (programmatic methods) ----------------------------------

    /// <summary>Clears every file from the list (honours <see cref="OnClear"/> veto in manual mode).</summary>
    public async Task ClearFiles()
    {
        var snapshot = _files.ToList();
        var args = new UploadClearEventArgs { Files = snapshot.Select(ToUploadFileInfo).ToList() };
        await OnClear.InvokeAsync(args);
        if (args.IsCancelled) return;

        foreach (var f in _files.Where(f => f.CancellationTokenSource != null))
            f.CancellationTokenSource!.Cancel();
        _files.Clear();
        await NotifyFilesChangedAsync();
        StateHasChanged();
    }

    /// <summary>Removes the file with the given id from the queue.</summary>
    public async Task RemoveFileAsync(string fileId)
    {
        var file = _files.FirstOrDefault(f => f.Id == fileId);
        if (file != null) await RemoveFileInternalAsync(file);
    }

    /// <summary>Alias kept for parity with the Upload spec API.</summary>
    public Task RemoveFile(string fileId) => RemoveFileAsync(fileId);

    /// <summary>Uploads every valid, not-yet-uploaded file (manual-upload path).</summary>
    public async Task UploadFiles()
    {
        if (!IsUploadMode) return;

        var toUpload = _files
            .Where(f => f.Status == UploadFileStatus.Selected && !f.IsInvalid && f.BrowserFile != null)
            .ToList();
        if (toUpload.Count == 0) return;

        var uploadArgs = new UploadEventArgs
        {
            Files = toUpload.Select(ToUploadFileInfo).ToList(),
            RequestData = [],
            RequestHeaders = [],
        };
        await OnUpload.InvokeAsync(uploadArgs);
        if (uploadArgs.IsCancelled) return;

        if (Batch)
        {
            await UploadBatchAsync(toUpload, uploadArgs.RequestData, uploadArgs.RequestHeaders);
        }
        else
        {
            foreach (var f in toUpload)
                await UploadOneAsync(f, uploadArgs.RequestData, uploadArgs.RequestHeaders);
        }
    }

    /// <summary>Cancels the upload of the file with the specified id.</summary>
    public async Task CancelFile(string fileId)
    {
        var file = _files.FirstOrDefault(f => f.Id == fileId);
        if (file == null) return;

        var args = new UploadCancelEventArgs { Files = [ToUploadFileInfo(file)] };
        await OnCancel.InvokeAsync(args);
        if (args.IsCancelled) return;

        file.CancellationTokenSource?.Cancel();
        file.Status = UploadFileStatus.Cancelled;
        _files.Remove(file);
        await NotifyFilesChangedAsync();
        StateHasChanged();
    }

    /// <summary>Opens the native file picker. Safari blocks programmatic clicks; no-ops there.</summary>
    public async Task OpenSelectFilesDialog()
    {
        await JS.InvokeVoidAsync("eval", $"document.getElementById('{InputId}')?.click()");
    }

    // -- Event handlers -----------------------------------------------------

    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        if (!IsInteractive) return;

        const int maxFiles = 1000;
        var browserFiles = Multiple ? e.GetMultipleFiles(maxFiles).ToList() : [e.File];

        var newEntries = new List<SelectedFile>();
        foreach (var bf in browserFiles)
        {
            var ext = System.IO.Path.GetExtension(bf.Name)?.ToLowerInvariant() ?? string.Empty;
            var entry = new SelectedFile
            {
                Name = bf.Name,
                Size = bf.Size,
                Extension = ext,
                ContentType = bf.ContentType ?? string.Empty,
                BrowserFile = bf,
                Status = UploadFileStatus.Selected,
                InvalidExtension = IsInvalidExtension(ext),
                InvalidMaxFileSize = MaxFileSize.HasValue && bf.Size > MaxFileSize.Value,
                InvalidMinFileSize = MinFileSize.HasValue && bf.Size < MinFileSize.Value,
            };
            _files.Add(entry);
            newEntries.Add(entry);
        }

        var selectArgs = new FileUploadSelectEventArgs { Files = newEntries };
        await OnSelect.InvokeAsync(selectArgs);
        if (selectArgs.IsCancelled)
        {
            foreach (var entry in newEntries) _files.Remove(entry);
            StateHasChanged();
            return;
        }

        await NotifyFilesChangedAsync();

        if (IsUploadMode && AutoUpload)
        {
            var valid = newEntries.Where(f => !f.IsInvalid).ToList();
            if (valid.Count == 0) return;

            var uploadArgs = new UploadEventArgs
            {
                Files = valid.Select(ToUploadFileInfo).ToList(),
                RequestData = [],
                RequestHeaders = [],
            };
            await OnUpload.InvokeAsync(uploadArgs);
            if (uploadArgs.IsCancelled) return;

            if (Batch)
            {
                await UploadBatchAsync(valid, uploadArgs.RequestData, uploadArgs.RequestHeaders);
            }
            else
            {
                foreach (var f in valid)
                    await UploadOneAsync(f, uploadArgs.RequestData, uploadArgs.RequestHeaders);
            }
        }
    }

    private void HandleDragEnter(Microsoft.AspNetCore.Components.Web.DragEventArgs e) => _isDragOver = true;
    private void HandleDragOver(Microsoft.AspNetCore.Components.Web.DragEventArgs e) { }
    private void HandleDragLeave(Microsoft.AspNetCore.Components.Web.DragEventArgs e) => _isDragOver = false;
    private void HandleDrop(Microsoft.AspNetCore.Components.Web.DragEventArgs e) => _isDragOver = false;

    private async Task RemoveFileInternalAsync(SelectedFile file)
    {
        var args = new UploadEventArgs
        {
            Files = [ToUploadFileInfo(file)],
            RequestData = [],
            RequestHeaders = [],
        };
        await OnRemove.InvokeAsync(args);
        if (args.IsCancelled) return;

        if (file.Status is UploadFileStatus.Uploading or UploadFileStatus.Paused)
        {
            file.CancellationTokenSource?.Cancel();
        }

        if (!string.IsNullOrEmpty(RemoveUrl)
            && file.Status == UploadFileStatus.Uploaded
            && IsUploadMode)
        {
            await SendRemoveRequestAsync(file, args);
        }
        else
        {
            _files.Remove(file);
        }

        await NotifyFilesChangedAsync();
        StateHasChanged();
    }

    private async Task SendRemoveRequestAsync(SelectedFile file, UploadEventArgs args)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(file.Name), RemoveField);
            foreach (var kv in args.RequestData)
                content.Add(new StringContent(kv.Value?.ToString() ?? string.Empty), kv.Key);

            using var request = new HttpRequestMessage(HttpMethod.Post, RemoveUrl!) { Content = content };
            ApplyHeaders(request, args.RequestHeaders);

            var response = await Http.SendAsync(request);
            var httpReq = await ReadHttpResponseAsync(response);

            if (response.IsSuccessStatusCode)
            {
                _files.Remove(file);
                await OnSuccess.InvokeAsync(new UploadSuccessEventArgs
                {
                    Files = [ToUploadFileInfo(file)],
                    Operation = UploadOperationType.Remove,
                    Request = httpReq,
                });
            }
            else
            {
                await OnError.InvokeAsync(new UploadErrorEventArgs
                {
                    Files = [ToUploadFileInfo(file)],
                    Operation = UploadOperationType.Remove,
                    Request = httpReq,
                });
            }
        }
        catch (Exception ex)
        {
            await OnError.InvokeAsync(new UploadErrorEventArgs
            {
                Files = [ToUploadFileInfo(file)],
                Operation = UploadOperationType.Remove,
                Request = new UploadHttpRequest { StatusText = ex.Message },
            });
        }
    }

    // -- Upload logic -------------------------------------------------------

    private async Task UploadOneAsync(
        SelectedFile file,
        Dictionary<string, object> requestData,
        Dictionary<string, object> requestHeaders)
    {
        if (file.BrowserFile == null || string.IsNullOrEmpty(SaveUrl)) return;

        file.CancellationTokenSource ??= new System.Threading.CancellationTokenSource();
        file.Status = UploadFileStatus.Uploading;
        if (file.UploadedBytes == 0) file.Progress = 0;
        StateHasChanged();

        try
        {
            var maxReadSize = MaxFileSize ?? 2L * 1024 * 1024 * 1024;
            if (ChunkSize > 0 && file.BrowserFile.Size > ChunkSize)
            {
                await UploadChunkedAsync(file, requestData, requestHeaders, maxReadSize);
            }
            else
            {
                await UploadWholeAsync(file, requestData, requestHeaders, maxReadSize);
            }
        }
        catch (OperationCanceledException)
        {
            if (file.Status == UploadFileStatus.Uploading)
                file.Status = UploadFileStatus.Cancelled;
        }
        catch (Exception ex)
        {
            file.Status = UploadFileStatus.Failed;
            await OnError.InvokeAsync(new UploadErrorEventArgs
            {
                Files = [ToUploadFileInfo(file)],
                Operation = UploadOperationType.Upload,
                Request = new UploadHttpRequest { StatusText = ex.Message },
            });
        }
        finally
        {
            file.CancellationTokenSource?.Dispose();
            file.CancellationTokenSource = null;
            StateHasChanged();
        }
    }

    private async Task UploadWholeAsync(
        SelectedFile file,
        Dictionary<string, object> requestData,
        Dictionary<string, object> requestHeaders,
        long maxReadSize)
    {
        using var content = new MultipartFormDataContent();
        using var stream = new StreamContent(file.BrowserFile!.OpenReadStream(maxReadSize));
        if (!string.IsNullOrEmpty(file.ContentType))
            stream.Headers.TryAddWithoutValidation("Content-Type", file.ContentType);
        content.Add(stream, SaveField, file.Name);
        foreach (var kv in requestData)
            content.Add(new StringContent(kv.Value?.ToString() ?? string.Empty), kv.Key);

        using var request = new HttpRequestMessage(HttpMethod.Post, SaveUrl!) { Content = content };
        ApplyHeaders(request, requestHeaders);

        var response = await Http.SendAsync(request, file.CancellationTokenSource!.Token);
        var httpReq = await ReadHttpResponseAsync(response);

        if (response.IsSuccessStatusCode)
        {
            file.Status = UploadFileStatus.Uploaded;
            file.Progress = 100;
            await OnProgress.InvokeAsync(new UploadProgressEventArgs
            {
                Files = [ToUploadFileInfo(file)],
                Progress = 100,
            });
            await OnSuccess.InvokeAsync(new UploadSuccessEventArgs
            {
                Files = [ToUploadFileInfo(file)],
                Operation = UploadOperationType.Upload,
                Request = httpReq,
            });
        }
        else
        {
            file.Status = UploadFileStatus.Failed;
            await OnError.InvokeAsync(new UploadErrorEventArgs
            {
                Files = [ToUploadFileInfo(file)],
                Operation = UploadOperationType.Upload,
                Request = httpReq,
            });
        }
    }

    private async Task UploadChunkedAsync(
        SelectedFile file,
        Dictionary<string, object> requestData,
        Dictionary<string, object> requestHeaders,
        long maxReadSize)
    {
        var chunkSize = ChunkSize;
        var total = file.BrowserFile!.Size;
        var totalChunks = (int)Math.Ceiling((double)total / chunkSize);
        var buffer = new byte[chunkSize];
        var startChunk = ResumableUploads ? (int)(file.UploadedBytes / chunkSize) : 0;
        if (!ResumableUploads) file.UploadedBytes = 0;

        await using var stream = file.BrowserFile.OpenReadStream(maxReadSize);

        if (file.UploadedBytes > 0 && startChunk > 0)
        {
            var bytesToSkip = file.UploadedBytes;
            var skipBuffer = new byte[Math.Min(81920, bytesToSkip)];
            while (bytesToSkip > 0)
            {
                var toRead = (int)Math.Min(skipBuffer.Length, bytesToSkip);
                var read = await stream.ReadAsync(skipBuffer.AsMemory(0, toRead), file.CancellationTokenSource!.Token);
                if (read == 0) break;
                bytesToSkip -= read;
            }
        }

        for (var chunkIndex = startChunk; chunkIndex < totalChunks; chunkIndex++)
        {
            file.CancellationTokenSource!.Token.ThrowIfCancellationRequested();

            var chunkStart = (long)chunkIndex * chunkSize;
            var bytesToRead = (int)Math.Min(chunkSize, total - chunkStart);
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bytesToRead), file.CancellationTokenSource.Token);
            var chunkEnd = chunkStart + bytesRead - 1;

            using var chunkContent = new MultipartFormDataContent();
            using var byteContent = new ByteArrayContent(buffer, 0, bytesRead);
            if (!string.IsNullOrEmpty(file.ContentType))
                byteContent.Headers.TryAddWithoutValidation("Content-Type", file.ContentType);
            chunkContent.Add(byteContent, SaveField, file.Name);
            chunkContent.Add(new StringContent(chunkIndex.ToString()), "chunkIndex");
            chunkContent.Add(new StringContent(totalChunks.ToString()), "totalChunks");
            chunkContent.Add(new StringContent(total.ToString()), "totalSize");
            chunkContent.Add(new StringContent(file.Name), "fileName");
            foreach (var kv in requestData)
                chunkContent.Add(new StringContent(kv.Value?.ToString() ?? string.Empty), kv.Key);

            using var request = new HttpRequestMessage(HttpMethod.Post, SaveUrl!) { Content = chunkContent };
            ApplyHeaders(request, requestHeaders);
            // Default header documented in remarks: Content-Range: bytes {start}-{end}/{total}
            request.Content!.Headers.TryAddWithoutValidation(
                "Content-Range",
                $"bytes {chunkStart}-{chunkEnd}/{total}");

            var response = await Http.SendAsync(request, file.CancellationTokenSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                var errReq = await ReadHttpResponseAsync(response);
                file.Status = UploadFileStatus.Failed;
                await OnError.InvokeAsync(new UploadErrorEventArgs
                {
                    Files = [ToUploadFileInfo(file)],
                    Operation = UploadOperationType.Upload,
                    Request = errReq,
                });
                return;
            }

            file.UploadedBytes = Math.Min(chunkEnd + 1, total);
            file.Progress = (int)((chunkIndex + 1) * 100.0 / totalChunks);
            await OnProgress.InvokeAsync(new UploadProgressEventArgs
            {
                Files = [ToUploadFileInfo(file)],
                Progress = file.Progress,
            });
            StateHasChanged();
        }

        file.Status = UploadFileStatus.Uploaded;
        file.Progress = 100;
        await OnSuccess.InvokeAsync(new UploadSuccessEventArgs
        {
            Files = [ToUploadFileInfo(file)],
            Operation = UploadOperationType.Upload,
            Request = new UploadHttpRequest { Status = 200, StatusText = "OK" },
        });
    }

    private async Task UploadBatchAsync(
        IList<SelectedFile> files,
        Dictionary<string, object> requestData,
        Dictionary<string, object> requestHeaders)
    {
        foreach (var f in files)
        {
            f.CancellationTokenSource ??= new System.Threading.CancellationTokenSource();
            f.Status = UploadFileStatus.Uploading;
            f.Progress = 0;
        }
        StateHasChanged();

        var streams = new List<System.IO.Stream>();
        try
        {
            var maxReadSize = MaxFileSize ?? 2L * 1024 * 1024 * 1024;
            using var content = new MultipartFormDataContent();
            foreach (var f in files)
            {
                if (f.BrowserFile == null) continue;
                var s = f.BrowserFile.OpenReadStream(maxReadSize);
                streams.Add(s);
                var sc = new StreamContent(s);
                if (!string.IsNullOrEmpty(f.ContentType))
                    sc.Headers.TryAddWithoutValidation("Content-Type", f.ContentType);
                content.Add(sc, SaveField, f.Name);
            }
            foreach (var kv in requestData)
                content.Add(new StringContent(kv.Value?.ToString() ?? string.Empty), kv.Key);

            using var request = new HttpRequestMessage(HttpMethod.Post, SaveUrl!) { Content = content };
            ApplyHeaders(request, requestHeaders);

            var response = await Http.SendAsync(request);
            var httpReq = await ReadHttpResponseAsync(response);

            if (response.IsSuccessStatusCode)
            {
                foreach (var f in files)
                {
                    f.Status = UploadFileStatus.Uploaded;
                    f.Progress = 100;
                }
                await OnSuccess.InvokeAsync(new UploadSuccessEventArgs
                {
                    Files = files.Select(ToUploadFileInfo).ToList(),
                    Operation = UploadOperationType.Upload,
                    Request = httpReq,
                });
            }
            else
            {
                foreach (var f in files) f.Status = UploadFileStatus.Failed;
                await OnError.InvokeAsync(new UploadErrorEventArgs
                {
                    Files = files.Select(ToUploadFileInfo).ToList(),
                    Operation = UploadOperationType.Upload,
                    Request = httpReq,
                });
            }
        }
        catch (Exception ex)
        {
            foreach (var f in files) f.Status = UploadFileStatus.Failed;
            await OnError.InvokeAsync(new UploadErrorEventArgs
            {
                Files = files.Select(ToUploadFileInfo).ToList(),
                Operation = UploadOperationType.Upload,
                Request = new UploadHttpRequest { StatusText = ex.Message },
            });
        }
        finally
        {
            foreach (var s in streams) s.Dispose();
            foreach (var f in files)
            {
                f.CancellationTokenSource?.Dispose();
                f.CancellationTokenSource = null;
            }
            StateHasChanged();
        }
    }

    // -- HTTP helpers -------------------------------------------------------

    private void ApplyHeaders(HttpRequestMessage request, IDictionary<string, object> dynamicHeaders)
    {
        if (WithCredentials)
        {
            request.Options.Set(new HttpRequestOptionsKey<bool>("WebAssemblyFetchCredentials"), true);
        }
        if (HttpHeaders != null)
        {
            foreach (var kv in HttpHeaders)
                request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        }
        foreach (var kv in dynamicHeaders)
            request.Headers.TryAddWithoutValidation(kv.Key, kv.Value?.ToString());
    }

    private static async Task<UploadHttpRequest> ReadHttpResponseAsync(HttpResponseMessage response)
    {
        string body;
        try { body = await response.Content.ReadAsStringAsync(); }
        catch { body = string.Empty; }
        return new UploadHttpRequest
        {
            Status = (int)response.StatusCode,
            StatusText = response.ReasonPhrase ?? string.Empty,
            ResponseType = response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            ResponseText = body,
        };
    }

    // -- Bridge helpers between SelectedFile and UploadFileInfo -------------

    private static UploadFileInfo ToUploadFileInfo(SelectedFile file) => new()
    {
        Id = file.Id,
        Name = file.Name,
        Size = file.Size,
        Extension = file.Extension,
        Status = file.Status,
        Progress = file.Progress,
        InvalidExtension = file.InvalidExtension,
        InvalidMaxFileSize = file.InvalidMaxFileSize,
        InvalidMinFileSize = file.InvalidMinFileSize,
        BrowserFile = file.BrowserFile,
    };

    private Task NotifyFilesChangedAsync()
    {
        if (!FilesChanged.HasDelegate) return Task.CompletedTask;
        return FilesChanged.InvokeAsync(_files.ToList());
    }

    // -- Validation ---------------------------------------------------------

    private bool IsInvalidExtension(string ext)
    {
        if (AllowedExtensions == null || AllowedExtensions.Count == 0) return false;
        return !AllowedExtensions.Select(e => e.ToLowerInvariant()).Contains(ext);
    }

    // -- Formatting + template helpers --------------------------------------

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }

    private static string ValidationSummary(SelectedFile f)
    {
        var msgs = new List<string>();
        if (f.InvalidExtension) msgs.Add("invalid file type");
        if (f.InvalidMaxFileSize) msgs.Add("file too large");
        if (f.InvalidMinFileSize) msgs.Add("file too small");
        return string.Join(", ", msgs);
    }

    private FileUploadTemplateContext BuildTemplateContext(SelectedFile file) => new()
    {
        File = new FileSelectFileInfo
        {
            Id = file.Id,
            Name = file.Name,
            Size = file.Size,
            Extension = file.Extension,
            InvalidExtension = file.InvalidExtension,
            InvalidMaxFileSize = file.InvalidMaxFileSize,
            InvalidMinFileSize = file.InvalidMinFileSize,
            BrowserFile = file.BrowserFile,
        },
        ValidationMessage = ValidationSummary(file),
    };

    private string DropZoneCssClass() =>
        CssProvider.FileUploadDropZoneClass(_isDragOver, !IsInteractive);

    private bool CanUploadManually =>
        IsUploadMode && !AutoUpload && _files.Any(f => f.Status == UploadFileStatus.Selected && !f.IsInvalid);

    private bool CanClear =>
        (IsUploadMode && !AutoUpload && _files.Count > 0) ||
        (!IsUploadMode && _files.Count > 0);
}
