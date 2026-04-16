using Sunfish.Foundation.Enums;

namespace Sunfish.Foundation.Configuration;

/// <summary>
/// Configuration options for custom icon providers registered via
/// <c>AddSunfishIconsCustom</c>.
/// </summary>
public sealed class IconOptions
{
    /// <summary>Custom sprite URL override. Required for <see cref="IconRenderMode.SvgSprite"/> providers.</summary>
    public string? SpriteUrl { get; set; }

    /// <summary>CSS class prefix for font-based icon sets (e.g. "bi" for Bootstrap Icons, "fa" for Font Awesome).</summary>
    public string? CssClassPrefix { get; set; }

    /// <summary>Optional display name for the library (used in diagnostics).</summary>
    public string LibraryName { get; set; } = "Custom";

    /// <summary>Render mode for this provider. Defaults to <see cref="IconRenderMode.SvgSprite"/>.</summary>
    public IconRenderMode RenderMode { get; set; } = IconRenderMode.SvgSprite;
}
