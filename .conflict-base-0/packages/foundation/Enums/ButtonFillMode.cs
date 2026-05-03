namespace Sunfish.Foundation.Enums;

/// <summary>
/// Specifies the fill mode (visual weight) of a button.
/// </summary>
public enum FillMode
{
    /// <summary>Solid background fill (default).</summary>
    Solid,

    /// <summary>Border-only with transparent background.</summary>
    Outline,

    /// <summary>No border or background; text only with hover effect.</summary>
    Flat,

    /// <summary>Styled as a hyperlink.</summary>
    Link,

    /// <summary>No visual chrome; only content is visible.</summary>
    Clear
}

/// <summary>
/// Specifies the border-radius mode of a button.
/// </summary>
public enum RoundedMode
{
    /// <summary>No border radius (square corners).</summary>
    None,

    /// <summary>Small border radius.</summary>
    Small,

    /// <summary>Medium border radius (default).</summary>
    Medium,

    /// <summary>Large border radius.</summary>
    Large,

    /// <summary>Fully rounded (pill shape).</summary>
    Full
}
