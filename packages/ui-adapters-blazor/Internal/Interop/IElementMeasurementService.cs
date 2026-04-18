using Microsoft.AspNetCore.Components;

namespace Sunfish.Components.Blazor.Internal.Interop;

/// <summary>
/// Measures element bounding rectangles and viewport dimensions via JS interop.
/// </summary>
internal interface IElementMeasurementService
{
    /// <summary>Gets the bounding client rect of the given element.</summary>
    ValueTask<ElementRect> GetBoundingClientRectAsync(ElementReference element, CancellationToken cancellationToken = default);

    /// <summary>Gets the current viewport dimensions and scroll offsets.</summary>
    ValueTask<ViewportRect> GetViewportAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the widths of all direct children of the given element.</summary>
    ValueTask<double[]> GetChildWidthsAsync(ElementReference element, CancellationToken cancellationToken = default);
}
