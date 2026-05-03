using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>Payload for submitting a new <see cref="MaintenanceRequest"/>.</summary>
public sealed record SubmitMaintenanceRequest
{
    /// <summary>The property to which this request belongs.</summary>
    public required EntityId PropertyId { get; init; }

    /// <summary>Display name of the person submitting the request.</summary>
    public required string RequestedByDisplayName { get; init; }

    /// <summary>Human-readable description of the issue or work needed.</summary>
    public required string Description { get; init; }

    /// <summary>Urgency level for this request.</summary>
    public required MaintenancePriority Priority { get; init; }

    /// <summary>The date on which the request is submitted.</summary>
    public required DateOnly RequestedDate { get; init; }

    /// <summary>
    /// Optional opaque reference to a deficiency in blocks-inspections.
    /// Consumer code translates between a real <c>DeficiencyId</c> and this string.
    /// </summary>
    public string? DeficiencyReference { get; init; }
}
