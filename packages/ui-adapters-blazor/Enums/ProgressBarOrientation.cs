namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Specifies the orientation of a <c>SunfishProgressBar</c> (standard or chunked).
/// Mirrors the Telerik <c>ProgressBarOrientation</c> spec so spec samples port without edits.
/// </summary>
public enum ProgressBarOrientation
{
    /// <summary>Progress fills from left to right (default).</summary>
    Horizontal,

    /// <summary>Progress fills from bottom to top.</summary>
    Vertical,
}
