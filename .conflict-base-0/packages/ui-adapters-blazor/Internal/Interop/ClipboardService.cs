using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Internal.Interop;

internal sealed class ClipboardService : IClipboardService
{
    private readonly ISunfishJsModuleLoader _loader;

    public ClipboardService(ISunfishJsModuleLoader loader)
    {
        _loader = loader;
    }

    public async ValueTask WriteAsync(ClipboardWriteRequest request, CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-clipboard-download.js", cancellationToken);
        await module.InvokeVoidAsync("writeClipboard", cancellationToken, request);
    }

    public async ValueTask<string> ReadTextAsync(CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-clipboard-download.js", cancellationToken);
        return await module.InvokeAsync<string>("readText", cancellationToken);
    }
}
