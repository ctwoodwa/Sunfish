using Sunfish.Foundation.Enums;
using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// Configures the ColorPicker to include the gradient (HSVA canvas) view.
/// Exposes a subset of <see cref="SunfishColorGradient"/> parameters for customization.
/// </summary>
public class ColorPickerGradientView : ColorPickerViewBase
{
    public override ColorPickerView ViewType => ColorPickerView.Gradient;

    /// <summary>The color format displayed in the text inputs (Hex or Rgb).</summary>
    [Parameter] public ColorFormat? Format { get; set; }

    /// <summary>The available formats the user can toggle between. When null, both Hex and Rgb are available.</summary>
    [Parameter] public IEnumerable<ColorFormat>? Formats { get; set; }

    /// <summary>Whether to show the opacity (alpha) slider and input. Default is true.</summary>
    [Parameter] public bool ShowOpacityEditor { get; set; } = true;
}
