using Sunfish.Blocks.Inspections.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Inspections.Services;

/// <summary>
/// Payload for scheduling a new <see cref="Inspection"/> via
/// <see cref="IInspectionsService.ScheduleAsync"/>.
/// </summary>
public sealed record ScheduleInspectionRequest
{
    /// <summary>The template that defines the checklist for this inspection.</summary>
    public required InspectionTemplateId TemplateId { get; init; }

    /// <summary>The unit to be inspected.</summary>
    public required EntityId UnitId { get; init; }

    /// <summary>Display name of the person who will conduct the inspection.</summary>
    public required string InspectorName { get; init; }

    /// <summary>The calendar date on which the inspection is scheduled.</summary>
    public required DateOnly ScheduledDate { get; init; }
}
