namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Specifies the visual style used by <c>SunfishGauge</c> — the polymorphic
/// gauge wrapper that dispatches to the appropriate underlying gauge renderer
/// (arc, radial, circular, or linear).
/// </summary>
/// <remarks>
/// ADR reference: Tier 3 W3-2 introduces <c>SunfishGauge</c> as the canonical,
/// MVP-level entry point for gauge visuals. Existing gauge components
/// (<c>SunfishArcGauge</c>, <c>SunfishRadialGauge</c>, <c>SunfishCircularGauge</c>,
/// <c>SunfishLinearGauge</c>) remain available as low-level primitives.
/// </remarks>
public enum GaugeType
{
    /// <summary>
    /// Partial-arc gauge (default). Renders an open arc with a progress fill,
    /// typically spanning 270° from the bottom-left to bottom-right.
    /// </summary>
    Arc = 0,

    /// <summary>
    /// Radial dial gauge. Renders a circular scale with a rotating needle
    /// pointing at the current value.
    /// </summary>
    Radial = 1,

    /// <summary>
    /// Full-circle circular gauge. Renders a closed ring with a proportional
    /// fill representing the value as a percentage of the range.
    /// </summary>
    Circular = 2,

    /// <summary>
    /// Linear bar gauge. Renders a horizontal (or vertical) track with a
    /// proportional fill and optional pointer.
    /// </summary>
    Linear = 3
}
