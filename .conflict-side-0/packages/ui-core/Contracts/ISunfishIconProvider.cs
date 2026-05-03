using Sunfish.Foundation.Enums;

namespace Sunfish.UICore.Contracts;

/// <summary>
/// Provides design-system-specific icon rendering. Implementations resolve icon
/// names to the appropriate markup (inline SVG, sprite reference, icon font, etc.).
/// </summary>
/// <remarks>
/// Returns pre-rendered HTML strings. Callers must treat the returned value as raw
/// markup and must not HTML-encode it. In Blazor, wrap as
/// <c>new MarkupString(provider.GetIcon(name, size))</c>.
/// </remarks>
public interface ISunfishIconProvider
{
    /// <summary>
    /// Returns the pre-rendered HTML markup for the requested icon at the given size.
    /// </summary>
    /// <param name="name">The logical icon name (e.g., "home", "settings").</param>
    /// <param name="size">The desired icon size.</param>
    /// <returns>A pre-rendered HTML string containing the icon markup. Do not HTML-encode.</returns>
    string GetIcon(string name, IconSize size = IconSize.Medium);

    /// <summary>
    /// Gets the URL of the SVG sprite sheet used by this icon provider.
    /// </summary>
    /// <returns>A relative or absolute URL pointing to the sprite SVG file.
    /// Returns <see cref="string.Empty"/> for <see cref="IconRenderMode.CssClass"/> providers.</returns>
    string GetIconSpriteUrl();

    /// <summary>
    /// Gets the render mode used by this icon provider (sprite, inline SVG, or CSS class).
    /// </summary>
    IconRenderMode RenderMode => IconRenderMode.SvgSprite;

    /// <summary>
    /// Gets the display name of the icon library (e.g., "Tabler", "Lucide", "Custom").
    /// Used for diagnostics and tooling.
    /// </summary>
    string LibraryName => "Unknown";
}
