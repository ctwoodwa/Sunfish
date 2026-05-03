using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Sunfish.Foundation.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// Abstract base class for ColorPicker view configuration components (Gradient, Palette).
/// These are configuration-only components that produce no rendered output.
/// They register with the parent <see cref="SunfishColorPicker"/> via <see cref="IColorPickerViewHost"/>.
/// </summary>
public abstract class ColorPickerViewBase : ComponentBase, IDisposable
{
    [CascadingParameter] private IColorPickerViewHost? ViewHost { get; set; }

    /// <summary>The view type this component represents.</summary>
    public abstract ColorPickerView ViewType { get; }

    protected override void OnInitialized()
    {
        ViewHost?.RegisterView(this);
    }

    /// <summary>No rendered output — configuration-only component.</summary>
    protected override void BuildRenderTree(RenderTreeBuilder builder) { }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) ViewHost?.UnregisterView(this);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
