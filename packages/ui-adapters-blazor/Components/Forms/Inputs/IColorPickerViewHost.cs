namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// Interface that allows <see cref="ColorPickerViewBase"/> components to register
/// with their parent <see cref="SunfishColorPicker"/> via cascading parameter.
/// </summary>
internal interface IColorPickerViewHost
{
    void RegisterView(ColorPickerViewBase view);
    void UnregisterView(ColorPickerViewBase view);
}
