namespace Sunfish.Foundation.Enums;

/// <summary>
/// Controls the flyout alignment of the SignalR connection status popup
/// relative to its trigger button.
/// </summary>
public enum ConnectionPopupPlacement
{
    /// <summary>Below the trigger, aligned to the start (left in LTR).</summary>
    BottomStart,

    /// <summary>Below the trigger, aligned to the end (right in LTR).</summary>
    BottomEnd,

    /// <summary>Above the trigger, aligned to the start (left in LTR).</summary>
    TopStart,

    /// <summary>Above the trigger, aligned to the end (right in LTR).</summary>
    TopEnd
}
