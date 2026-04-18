namespace Sunfish.Foundation.Models;

/// <summary>
/// Defines a colored range segment for gauge components.
/// </summary>
public class GaugeRange
{
    /// <summary>Start value of the range.</summary>
    public double From { get; set; }

    /// <summary>End value of the range.</summary>
    public double To { get; set; }

    /// <summary>CSS color for this range (e.g., "#4caf50", "red").</summary>
    public string Color { get; set; } = "#1976d2";
}
