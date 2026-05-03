using Sunfish.Blocks.Inspections.Models;

namespace Sunfish.Blocks.Inspections.Services;

/// <summary>
/// Payload for creating a new <see cref="InspectionTemplate"/> via
/// <see cref="IInspectionsService.CreateTemplateAsync"/>.
/// </summary>
public sealed record CreateTemplateRequest
{
    /// <summary>Human-readable template name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional description explaining the purpose or scope of the template.</summary>
    public string? Description { get; init; }

    /// <summary>Ordered list of checklist items for this template.</summary>
    public required IReadOnlyList<InspectionChecklistItem> Items { get; init; }
}
