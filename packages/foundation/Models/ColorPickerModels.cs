namespace Sunfish.Foundation.Models;

/// <summary>
/// Event arguments for the <c>OnOpen</c> event of a <c>SunfishColorPicker</c>.
/// Set <see cref="IsCancelled"/> to <c>true</c> to prevent the popup from opening.
/// </summary>
public class ColorPickerOpenEventArgs
{
    /// <summary>Set to <c>true</c> to cancel the popup opening.</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Event arguments for the <c>OnClose</c> event of a <c>SunfishColorPicker</c>.
/// Set <see cref="IsCancelled"/> to <c>true</c> to prevent the popup from closing.
/// </summary>
public class ColorPickerCloseEventArgs
{
    /// <summary>Set to <c>true</c> to cancel the popup closing.</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Provides preset color palette collections for the <c>SunfishColorPicker</c> palette view.
/// </summary>
public static class ColorPalettePresets
{
    /// <summary>
    /// The standard Office color palette (70 colors, 10 columns).
    /// </summary>
    public static readonly IEnumerable<string> Office = new[]
    {
        "#ffffff", "#000000", "#e7e6e6", "#44546a", "#4472c4", "#ed7d31", "#a5a5a5", "#ffc000", "#5b9bd5", "#70ad47",
        "#f2f2f2", "#808080", "#d0cece", "#d6dce4", "#d9e2f3", "#fbe5d6", "#ededed", "#fff2cc", "#deebf7", "#e2efda",
        "#d9d9d9", "#595959", "#aeaaaa", "#adb9ca", "#b4c7e7", "#f8cbad", "#dbdbdb", "#ffe699", "#bdd7ee", "#c5e0b4",
        "#bfbfbf", "#404040", "#757171", "#8497b0", "#8faadc", "#f4b183", "#c9c9c9", "#ffd966", "#9cc3e5", "#a9d18e",
        "#a6a6a6", "#262626", "#3b3838", "#333f4f", "#2f5597", "#c55a11", "#7b7b7b", "#bf9000", "#2e75b6", "#548235",
        "#808080", "#0d0d0d", "#161616", "#222b35", "#1f3864", "#833c0b", "#525252", "#7f6000", "#1f4e79", "#375623",
        "#c00000", "#ff0000", "#ffc000", "#ffff00", "#92d050", "#00b050", "#00b0f0", "#0070c0", "#002060", "#7030a0"
    };

    /// <summary>
    /// A basic palette of 20 common colors.
    /// </summary>
    public static readonly IEnumerable<string> Basic = new[]
    {
        "#ffffff", "#c0c0c0", "#808080", "#000000",
        "#ff0000", "#800000", "#ffff00", "#808000",
        "#00ff00", "#008000", "#00ffff", "#008080",
        "#0000ff", "#000080", "#ff00ff", "#800080",
        "#ff8040", "#804000", "#ff80c0", "#804060"
    };

    /// <summary>
    /// An extended palette of 40 colors covering a broad spectrum.
    /// </summary>
    public static readonly IEnumerable<string> Extended = new[]
    {
        "#ffffff", "#e0e0e0", "#c0c0c0", "#a0a0a0", "#808080", "#404040", "#202020", "#000000",
        "#ffcccc", "#ff8080", "#ff0000", "#cc0000", "#800000", "#400000",
        "#ffddbb", "#ffaa55", "#ff8000", "#cc6600", "#804000",
        "#ffff99", "#ffff00", "#cccc00", "#808000", "#404000",
        "#ccffcc", "#80ff80", "#00ff00", "#00cc00", "#008000", "#004000",
        "#ccffff", "#80ffff", "#00ffff", "#00cccc", "#008080", "#004040",
        "#ccccff", "#8080ff", "#0000ff", "#0000cc", "#000080", "#000040"
    };

    /// <summary>
    /// A flat design-inspired palette of material-style colors.
    /// </summary>
    public static readonly IEnumerable<string> Flat = new[]
    {
        "#e74c3c", "#c0392b", "#e67e22", "#d35400", "#f1c40f", "#f39c12",
        "#2ecc71", "#27ae60", "#1abc9c", "#16a085", "#3498db", "#2980b9",
        "#9b59b6", "#8e44ad", "#34495e", "#2c3e50", "#95a5a6", "#7f8c8d",
        "#ecf0f1", "#bdc3c7"
    };
}
