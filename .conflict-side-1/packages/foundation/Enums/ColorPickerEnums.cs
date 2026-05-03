namespace Sunfish.Foundation.Enums;

/// <summary>
/// Specifies the color format that the ColorPicker returns to the application.
/// </summary>
public enum ColorFormat
{
    /// <summary>Colors are returned as CSS <c>rgb(r, g, b)</c> or <c>rgba(r, g, b, a)</c> strings.</summary>
    Rgb,

    /// <summary>Colors are returned as CSS hex strings, e.g. <c>#rrggbb</c> or <c>#rrggbbaa</c>.</summary>
    Hex
}

/// <summary>
/// Specifies which view is shown in the ColorPicker popup.
/// </summary>
public enum ColorPickerView
{
    /// <summary>The HSV gradient canvas with hue/opacity sliders and text inputs.</summary>
    Gradient,

    /// <summary>A palette of predefined color swatches.</summary>
    Palette,

    /// <summary>Render both the gradient canvas and a palette side-by-side in the same view.</summary>
    GradientPalette
}

/// <summary>
/// Specifies the text-input format shown inside a ColorPicker / ColorGradient.
/// </summary>
public enum ColorPickerFormat
{
    /// <summary>CSS hex strings (e.g. <c>#rrggbb</c>).</summary>
    Hex,

    /// <summary>CSS <c>rgb(r, g, b)</c> / <c>rgba(r, g, b, a)</c> strings.</summary>
    Rgb,

    /// <summary>CSS <c>hsl(h, s%, l%)</c> / <c>hsla(h, s%, l%, a)</c> strings.</summary>
    Hsl
}
