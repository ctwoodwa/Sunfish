namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Lifecycle state of a <see cref="RemodelPhase"/>:
/// <c>Planned → Active → (Complete | OverBudget | Cancelled)</c>. Terminal
/// states (Complete, OverBudget, Cancelled) do not transition further.
/// </summary>
public enum PhaseStatus
{
    Planned,
    Active,
    Complete,
    OverBudget,
    Cancelled,
}
