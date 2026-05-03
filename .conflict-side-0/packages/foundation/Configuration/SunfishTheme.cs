namespace Sunfish.Foundation.Configuration;

/// <summary>
/// Immutable snapshot of a Sunfish theme, including color palette, typography scale,
/// shape tokens, and layout direction. Passed to <c>ISunfishThemeService</c>
/// (see <c>Sunfish.Foundation.Services</c>) to apply a new visual appearance at runtime.
/// </summary>
public record SunfishTheme
{
    /// <summary>
    /// Gets the color palette (primary, secondary, surface, semantic colors, etc.).
    /// </summary>
    public SunfishColorPalette Colors { get; init; } = new();

    /// <summary>
    /// Gets the typography scale (font families, sizes, weights, line heights).
    /// </summary>
    public SunfishTypographyScale Typography { get; init; } = new();

    /// <summary>
    /// Gets the shape tokens (border radius values, elevation levels).
    /// </summary>
    public SunfishShape Shape { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether the theme uses right-to-left layout direction.
    /// </summary>
    public bool IsRtl { get; init; }
}
