using TaskStatus = Sunfish.Blocks.Tasks.Models.TaskStatus;
using Sunfish.Blocks.Tasks.Models;

namespace Sunfish.Blocks.Tasks.State;

/// <summary>
/// Status-machine state holder. Validates transitions through the canonical lifecycle:
/// Backlog → Todo → InProgress → Done (with Todo ↔ InProgress permitted).
/// Returns false and leaves state untouched for invalid transitions.
/// </summary>
public sealed class TaskBoardState
{
    public bool TryTransition(TaskItem item, TaskStatus target, out TaskItem updated)
    {
        if (!IsValid(item.Status, target))
        {
            updated = item;
            return false;
        }
        updated = item with { Status = target };
        return true;
    }

    internal static bool IsValid(TaskStatus from, TaskStatus to) => (from, to) switch
    {
        (TaskStatus.Backlog,    TaskStatus.Todo)       => true,
        (TaskStatus.Todo,       TaskStatus.InProgress) => true,
        (TaskStatus.InProgress, TaskStatus.Done)       => true,
        (TaskStatus.InProgress, TaskStatus.Todo)       => true,  // revert
        (TaskStatus.Todo,       TaskStatus.Backlog)    => true,  // revert
        _ when from == to                              => true,
        _                                              => false,
    };
}
