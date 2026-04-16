namespace Sunfish.Core.Enums;

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
    Palette
}
