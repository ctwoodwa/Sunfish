using Microsoft.AspNetCore.Components;

namespace Sunfish.Components.Blazor.Internal.Interop;

/// <summary>
/// Provides browser graphics interop for chart/diagram/map rendering.
/// Abstracts canvas and SVG measurement operations that require browser APIs.
/// </summary>
internal interface IGraphicsInteropService
{
    /// <summary>Measures the rendered text size using a canvas 2D context.</summary>
    ValueTask<(double Width, double Height)> MeasureTextAsync(
        string text,
        string font,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the device pixel ratio for high-DPI rendering.</summary>
    ValueTask<double> GetDevicePixelRatioAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the computed dimensions of an SVG or canvas element.</summary>
    ValueTask<ElementRect> GetRenderedSizeAsync(
        ElementReference element,
        CancellationToken cancellationToken = default);
}
