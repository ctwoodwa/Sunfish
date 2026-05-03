using Sunfish.Foundation.Enums;

namespace Sunfish.Foundation.Models;

public record DataChangeInfo
{
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public ChangeType ChangeType { get; init; }
    public string? Summary { get; init; }
    public string? ChangedByName { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public string? Module { get; init; }
    public string? AffectedEntityName { get; init; }
}
