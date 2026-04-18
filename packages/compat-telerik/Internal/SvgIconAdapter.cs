using Microsoft.AspNetCore.Components;

namespace Sunfish.Compat.Telerik.Internal;

/// <summary>
/// Converts Telerik-shaped icon values into a Sunfish <see cref="RenderFragment"/>.
/// Telerik's <c>Icon</c> parameter on most components accepts <c>object?</c>: it may be
/// a <see cref="RenderFragment"/>, an <c>ISvgIcon</c>-shaped value (Telerik's SvgIcon
/// type), or a string identifying a font-icon name. This adapter normalizes the input
/// to <see cref="RenderFragment"/>.
/// </summary>
internal static class SvgIconAdapter
{
    /// <summary>
    /// Converts a Telerik-shaped icon value to a <see cref="RenderFragment"/>.
    /// Returns <c>null</c> for <c>null</c> input.
    /// </summary>
    public static RenderFragment? ToRenderFragment(object? icon) => icon switch
    {
        null => null,
        RenderFragment rf => rf,
        // Future: detect ISvgIcon-shaped types via duck typing once compat-telerik ships
        // a shim for Telerik.FontIcon / ISvgIcon. Phase 6 falls through to a string
        // representation, which is sufficient for the common "icon name" usage pattern.
        _ => (RenderFragment)(builder => builder.AddContent(0, icon.ToString()))
    };
}
