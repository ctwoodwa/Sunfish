namespace Sunfish.Blocks.Tasks.Models;

/// <summary>
/// Canonical task lifecycle states. Hard-coded for the canonical board.
/// Follow-up work: make this extensible via a registry or allow consumer-defined enums.
/// </summary>
public enum TaskStatus
{
    Backlog,
    Todo,
    InProgress,
    Done
}
