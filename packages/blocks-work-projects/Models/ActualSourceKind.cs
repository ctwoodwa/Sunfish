namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Classification of the originating event that produced a
/// <see cref="ProjectActual"/> row per Stage 02 §2.22. Drives
/// budget-vs-actual reconciliation reporting (e.g. "show me all
/// labour-side actuals on Project X").
/// </summary>
public enum ActualSourceKind
{
    WorkOrderLine,
    TimeEntry,
    JournalEntry,
    Invoice,
    Manual,
}
