namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Specifies which side of a <c>SunfishSlider</c> / <c>SunfishRangeSlider</c> track
/// renders tick marks. Applies to both horizontal and vertical orientations.
/// Mirrors the Telerik <c>SliderTickPosition</c> spec.
/// </summary>
public enum SliderTickPosition
{
    /// <summary>No ticks are rendered on either side of the track.</summary>
    None,

    /// <summary>Ticks render on the leading side (top for horizontal, left for vertical).</summary>
    Before,

    /// <summary>Ticks render on the trailing side (bottom for horizontal, right for vertical).</summary>
    After,

    /// <summary>Ticks render on both sides of the track (default).</summary>
    Both
}
