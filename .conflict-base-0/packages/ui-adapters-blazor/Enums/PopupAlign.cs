namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Horizontal alignment of the anchor point used by <c>SunfishPopup</c>.
/// Selects which horizontal edge of the anchor element the popup aligns to.
/// </summary>
public enum PopupAnchorHorizontalAlign
{
    /// <summary>Align to the anchor's left edge (default).</summary>
    Left,

    /// <summary>Align to the anchor's horizontal center.</summary>
    Center,

    /// <summary>Align to the anchor's right edge.</summary>
    Right
}

/// <summary>
/// Vertical alignment of the anchor point used by <c>SunfishPopup</c>.
/// Selects which vertical edge of the anchor element the popup aligns to.
/// </summary>
public enum PopupAnchorVerticalAlign
{
    /// <summary>Align to the anchor's top edge.</summary>
    Top,

    /// <summary>Align to the anchor's vertical middle.</summary>
    Middle,

    /// <summary>Align to the anchor's bottom edge (default).</summary>
    Bottom
}

/// <summary>
/// Horizontal alignment of the popup itself relative to the anchor point.
/// Selects which horizontal edge of the popup aligns to the anchor point.
/// </summary>
public enum PopupHorizontalAlign
{
    /// <summary>Align the popup's left edge to the anchor point (default).</summary>
    Left,

    /// <summary>Align the popup's horizontal center to the anchor point.</summary>
    Center,

    /// <summary>Align the popup's right edge to the anchor point.</summary>
    Right
}

/// <summary>
/// Vertical alignment of the popup itself relative to the anchor point.
/// Selects which vertical edge of the popup aligns to the anchor point.
/// </summary>
public enum PopupVerticalAlign
{
    /// <summary>Align the popup's top edge to the anchor point (default).</summary>
    Top,

    /// <summary>Align the popup's vertical middle to the anchor point.</summary>
    Middle,

    /// <summary>Align the popup's bottom edge to the anchor point.</summary>
    Bottom
}
