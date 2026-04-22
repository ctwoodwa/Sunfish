namespace Sunfish.Compat.Syncfusion;

/// <summary>
/// Syncfusion-shaped pre-open event arguments for SfDialog. Mirrors
/// <c>Syncfusion.Blazor.Popups.BeforeOpenEventArgs</c>.
/// </summary>
public class BeforeOpenEventArgs
{
    /// <summary>Cancel the open.</summary>
    public bool Cancel { get; set; }

    /// <summary>When set, overrides the max-height of the dialog body.</summary>
    public double? MaxHeight { get; set; }
}

/// <summary>
/// Syncfusion-shaped pre-close event arguments for SfDialog. Mirrors
/// <c>Syncfusion.Blazor.Popups.BeforeCloseEventArgs</c>.
/// </summary>
public class BeforeCloseEventArgs
{
    /// <summary>Cancel the close.</summary>
    public bool Cancel { get; set; }

    /// <summary>True when the close originated from user interaction (e.g. ESC, × button).</summary>
    public bool IsInteracted { get; init; }
}
