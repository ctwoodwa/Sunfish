using Sunfish.Foundation.Enums;

namespace Sunfish.Foundation.Models;

public record AlertItem
{
    public required string Title { get; init; }
    public string? Detail { get; init; }
    public string? Module { get; init; }
    public AlertSeverity Severity { get; init; } = AlertSeverity.Info;
}
