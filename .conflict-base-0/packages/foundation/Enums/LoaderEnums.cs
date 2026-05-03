namespace Sunfish.Foundation.Enums;

/// <summary>
/// Specifies the visual style of a loading indicator.
/// </summary>
public enum LoaderType
{
    /// <summary>A classic rotating spinner — the default for most loading states.</summary>
    Spinner,

    /// <summary>A pulsing animation style.</summary>
    Pulsing,

    /// <summary>An infinitely spinning animation.</summary>
    InfiniteSpinner,

    /// <summary>A converging spinner animation.</summary>
    ConvergingSpinner
}
