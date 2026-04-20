using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Internal.Interop;

internal sealed class DownloadService : IDownloadService
{
    private readonly ISunfishJsModuleLoader _loader;

    public DownloadService(ISunfishJsModuleLoader loader)
    {
        _loader = loader;
    }

    public async ValueTask DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-clipboard-download.js", cancellationToken);
        await module.InvokeVoidAsync("download", cancellationToken, request);
    }
}
