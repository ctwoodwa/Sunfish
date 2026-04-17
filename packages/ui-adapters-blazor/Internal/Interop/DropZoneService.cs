using Microsoft.JSInterop;

namespace Sunfish.Components.Blazor.Internal.Interop;

internal sealed class DropZoneService : IDropZoneService
{
    private readonly ISunfishJsModuleLoader _loader;

    public DropZoneService(ISunfishJsModuleLoader loader) => _loader = loader;

    public async ValueTask<int> RegisterAsync(string dropZoneElementId, string fileInputElementId, CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-dropzone.js", cancellationToken);
        return await module.InvokeAsync<int>("registerDropZone", cancellationToken, dropZoneElementId, fileInputElementId);
    }

    public async ValueTask UnregisterAsync(int handleId, CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-dropzone.js", cancellationToken);
        await module.InvokeVoidAsync("unregisterDropZone", cancellationToken, handleId);
    }
}
