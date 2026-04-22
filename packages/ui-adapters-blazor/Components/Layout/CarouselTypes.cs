namespace Sunfish.UIAdapters.Blazor.Components.Layout;

/// <summary>
/// Event arguments for <see cref="SunfishCarousel"/> slide-change events. Supports
/// cancellation via <see cref="IsCancelled"/> so handlers can veto transitions
/// (e.g., until a gated action completes).
/// </summary>
public class CarouselSlideChangeEventArgs
{
    /// <summary>Zero-based index of the slide the carousel is leaving.</summary>
    public int PreviousSlideIndex { get; set; }

    /// <summary>Zero-based index of the slide the carousel is moving to.</summary>
    public int CurrentSlideIndex { get; set; }

    /// <summary>Set to <c>true</c> to cancel the slide change.</summary>
    public bool IsCancelled { get; set; }
}
