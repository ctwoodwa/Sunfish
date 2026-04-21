namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Describes how <c>SunfishPopup</c> should react when its natural position would
/// leave it partially or fully outside the visible viewport.
/// </summary>
public enum PopupCollision
{
    /// <summary>Shift the popup along the offending axis until it fits inside the viewport.</summary>
    Fit,

    /// <summary>Flip the popup to the opposite side of the anchor to stay inside the viewport.</summary>
    Flip,

    /// <summary>Do not adjust the popup's position; allow it to extend past the viewport.</summary>
    None
}
