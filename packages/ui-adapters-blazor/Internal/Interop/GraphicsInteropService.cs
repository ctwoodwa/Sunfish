using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.Components.Blazor.Internal.Interop;

internal sealed class GraphicsInteropService : IGraphicsInteropService
{
    private readonly ISunfishJsModuleLoader _loader;

    public GraphicsInteropService(ISunfishJsModuleLoader loader)
    {
        _loader = loader;
    }

    public async ValueTask<(double Width, double Height)> MeasureTextAsync(
        string text,
        string font,
        CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-graphics.js", cancellationToken);
        var result = await module.InvokeAsync<TextMeasurement>("measureText", cancellationToken, text, font);
        return (result.Width, result.Height);
    }

    public async ValueTask<double> GetDevicePixelRatioAsync(CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-graphics.js", cancellationToken);
        return await module.InvokeAsync<double>("getDevicePixelRatio", cancellationToken);
    }

    public async ValueTask<ElementRect> GetRenderedSizeAsync(
        ElementReference element,
        CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-graphics.js", cancellationToken);
        return await module.InvokeAsync<ElementRect>("getRenderedSize", cancellationToken, element);
    }

    private record TextMeasurement(double Width, double Height);
}
