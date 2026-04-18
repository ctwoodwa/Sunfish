namespace Sunfish.Blocks.Tasks.Models;

/// <summary>
/// Canonical task record. Intentionally thin — consumers can wrap their own domain
/// model via the TItem generic on TaskBoardBlock, or use this record directly.
/// </summary>
public sealed record TaskItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required TaskStatus Status { get; init; }
    public string? Assignee { get; init; }
    public DateTime? DueDateUtc { get; init; }
}
