namespace Sunfish.Compat.Syncfusion;

/// <summary>
/// Syncfusion-shaped toast lifecycle event arguments (SfToast.BeforeOpen). Mirrors
/// <c>Syncfusion.Blazor.Notifications.ToastBeforeOpenArgs</c>.
/// </summary>
public class ToastBeforeOpenArgs
{
    /// <summary>The toast title.</summary>
    public string? Title { get; init; }

    /// <summary>The toast body content.</summary>
    public string? Content { get; init; }

    /// <summary>Cancel the open.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// Syncfusion-shaped toast-opened event arguments. Mirrors
/// <c>Syncfusion.Blazor.Notifications.ToastOpenArgs</c>.
/// </summary>
public class ToastOpenArgs
{
    /// <summary>The toast title.</summary>
    public string? Title { get; init; }

    /// <summary>The toast body content.</summary>
    public string? Content { get; init; }
}

/// <summary>
/// Syncfusion-shaped toast-closed event arguments. Mirrors
/// <c>Syncfusion.Blazor.Notifications.ToastCloseArgs</c>.
/// </summary>
public class ToastCloseArgs
{
    /// <summary>The toast title.</summary>
    public string? Title { get; init; }

    /// <summary>True when the close originated from a user click on the × button.</summary>
    public bool IsInteracted { get; init; }
}

/// <summary>
/// Syncfusion-shaped toast-click event arguments. Mirrors
/// <c>Syncfusion.Blazor.Notifications.ToastClickEventArgs</c>.
/// </summary>
public class ToastClickEventArgs
{
    /// <summary>The toast title.</summary>
    public string? Title { get; init; }

    /// <summary>True when the click originated from a user interaction (vs. programmatic).</summary>
    public bool IsInteracted { get; init; }

    /// <summary>Cancel the default click behavior (auto-close).</summary>
    public bool Cancel { get; set; }
}
