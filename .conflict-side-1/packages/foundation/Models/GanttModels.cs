namespace Sunfish.Foundation.Models;

/// <summary>
/// Represents a task in the SunfishGantt chart.
/// </summary>
public class GanttTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public double PercentComplete { get; set; }
    public bool IsMilestone { get; set; }
    public string? Color { get; set; }
    public int Level { get; set; }
    public string? ParentId { get; set; }
    public List<string>? DependsOn { get; set; }
}
