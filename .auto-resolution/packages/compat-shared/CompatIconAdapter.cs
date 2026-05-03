using Microsoft.AspNetCore.Components;

namespace Sunfish.Compat.Shared;

/// <summary>
/// Converts vendor-shaped icon values into a Sunfish <see cref="RenderFragment"/>.
/// Most commercial vendors' <c>Icon</c> parameters accept <c>object?</c>: it may be
/// a <see cref="RenderFragment"/>, a vendor-specific SVG icon type, or a string
/// identifying a font-icon name. This adapter normalizes the input to <see cref="RenderFragment"/>.
/// </summary>
public static class CompatIconAdapter
{
    /// <summary>
    /// Converts a vendor-shaped icon value to a <see cref="RenderFragment"/>.
    /// Returns <c>null</c> for <c>null</c> input.
    /// </summary>
    public static RenderFragment? ToRenderFragment(object? icon) => icon switch
    {
        null => null,
        RenderFragment rf => rf,
        // Future: detect vendor ISvgIcon-shaped types via duck typing once a vendor ships
        // a shim that needs richer detection. For now, fall through to a string
        // representation, which is sufficient for the common "icon name" usage pattern.
        _ => (RenderFragment)(builder => builder.AddContent(0, icon.ToString()))
    };
}
