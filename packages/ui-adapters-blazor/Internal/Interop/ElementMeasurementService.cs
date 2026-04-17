using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.Components.Blazor.Internal.Interop;

internal sealed class ElementMeasurementService : IElementMeasurementService
{
    private readonly ISunfishJsModuleLoader _loader;

    public ElementMeasurementService(ISunfishJsModuleLoader loader)
    {
        _loader = loader;
    }

    public async ValueTask<ElementRect> GetBoundingClientRectAsync(ElementReference element, CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-measurement.js", cancellationToken);
        return await module.InvokeAsync<ElementRect>("getBoundingClientRect", cancellationToken, element);
    }

    public async ValueTask<ViewportRect> GetViewportAsync(CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-measurement.js", cancellationToken);
        return await module.InvokeAsync<ViewportRect>("getViewport", cancellationToken);
    }

    public async ValueTask<double[]> GetChildWidthsAsync(ElementReference element, CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-measurement.js", cancellationToken);
        return await module.InvokeAsync<double[]>("getChildWidths", cancellationToken, element);
    }
}
