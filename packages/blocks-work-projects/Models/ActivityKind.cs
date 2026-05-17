namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>Classification of a <see cref="TimeEntry"/>'s activity per Stage 02 §2.20.</summary>
public enum ActivityKind
{
    Labor,
    Travel,
    Consultation,
    Inspection,
    Admin,
    Callout,
    Overtime,
}
