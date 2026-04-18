using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.Components;

namespace Sunfish.Components.Blazor.Components.Forms.Inputs;

/// <summary>
/// Configures the ColorPicker to include the palette (color swatch grid) view.
/// Exposes a subset of <see cref="SunfishColorPalette"/> parameters for customization.
/// </summary>
public class ColorPickerPaletteView : ColorPickerViewBase
{
    public override ColorPickerView ViewType => ColorPickerView.Palette;

    /// <summary>The number of columns in the palette grid.</summary>
    [Parameter] public int Columns { get; set; } = 5;

    /// <summary>The collection of colors to display. Defaults to <see cref="ColorPalettePresets.Basic"/>.</summary>
    [Parameter] public IEnumerable<string>? Colors { get; set; }

    /// <summary>The CSS width of each palette tile (e.g. "24px", "2rem").</summary>
    [Parameter] public string TileWidth { get; set; } = "24px";

    /// <summary>The CSS height of each palette tile (e.g. "24px", "2rem").</summary>
    [Parameter] public string TileHeight { get; set; } = "24px";
}
