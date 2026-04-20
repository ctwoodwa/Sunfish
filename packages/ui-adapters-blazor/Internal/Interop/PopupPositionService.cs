using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Internal.Interop;

internal sealed class PopupPositionService : IPopupPositionService
{
    private readonly ISunfishJsModuleLoader _loader;

    public PopupPositionService(ISunfishJsModuleLoader loader)
    {
        _loader = loader;
    }

    public async ValueTask<PopupPositionResult> ComputePositionAsync(
        ElementReference anchor,
        ElementReference popup,
        PopupAnchorOptions options,
        CancellationToken cancellationToken = default)
    {
        var module = await _loader.ImportAsync("js/marilo-positioning.js", cancellationToken);
        return await module.InvokeAsync<PopupPositionResult>("computePosition", cancellationToken,
            anchor, popup, new
            {
                placement = PlacementToJs(options.Placement),
                offset = options.Offset,
                autoFlip = options.AutoFlip,
                viewportMargin = options.ViewportMargin
            });
    }

    private static string PlacementToJs(PopupPlacement placement) => placement switch
    {
        PopupPlacement.Top => "top",
        PopupPlacement.TopStart => "top-start",
        PopupPlacement.TopEnd => "top-end",
        PopupPlacement.Bottom => "bottom",
        PopupPlacement.BottomStart => "bottom-start",
        PopupPlacement.BottomEnd => "bottom-end",
        PopupPlacement.Left => "left",
        PopupPlacement.LeftStart => "left-start",
        PopupPlacement.LeftEnd => "left-end",
        PopupPlacement.Right => "right",
        PopupPlacement.RightStart => "right-start",
        PopupPlacement.RightEnd => "right-end",
        _ => "bottom"
    };
}
