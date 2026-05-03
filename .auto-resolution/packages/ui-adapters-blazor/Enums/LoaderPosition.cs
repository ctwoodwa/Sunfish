namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Specifies where the loader indicator sits within a loader-container overlay
/// (used by <c>SunfishSpinner</c> when in loader-container mode and by
/// <c>SunfishLoaderContainer</c>). Mirrors the Telerik <c>LoaderPosition</c> spec.
/// </summary>
public enum LoaderPosition
{
    /// <summary>Loader is anchored to the leading edge of the overlay.</summary>
    Start,

    /// <summary>Loader is centered within the overlay (default).</summary>
    Center,

    /// <summary>Loader is anchored to the trailing edge of the overlay.</summary>
    End,
}

/// <summary>
/// Specifies the backdrop panel rendered behind a loader-container spinner overlay.
/// </summary>
public enum LoaderPanel
{
    /// <summary>No backdrop is rendered; the spinner floats over the content.</summary>
    None,

    /// <summary>An opaque backdrop hides the content beneath the overlay (default).</summary>
    Solid,

    /// <summary>A translucent backdrop dims the content beneath the overlay.</summary>
    Transparent,
}
