using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// A request for maintenance work submitted by a tenant or property manager.
/// </summary>
/// <param name="Id">Unique identifier for this maintenance request.</param>
/// <param name="PropertyId">The property to which this request belongs.</param>
/// <param name="RequestedByDisplayName">Display name of the person who submitted the request.</param>
/// <param name="Description">Human-readable description of the issue or work needed.</param>
/// <param name="Priority">Urgency level assigned to this request.</param>
/// <param name="Status">Current lifecycle status of this request.</param>
/// <param name="RequestedDate">The calendar date on which the request was submitted.</param>
/// <param name="DeficiencyReference">
/// Optional opaque reference to a deficiency in <c>blocks-inspections</c>.
/// This is a plain string so that <c>blocks-maintenance</c> has no compile-time dependency on
/// <c>Sunfish.Blocks.Inspections</c>; consumer code is responsible for translating between
/// a real <c>DeficiencyId</c> and this string. See independence note in the package README.
/// </param>
/// <param name="CreatedAtUtc">The instant this record was first persisted.</param>
public sealed record MaintenanceRequest(
    MaintenanceRequestId Id,
    EntityId PropertyId,
    string RequestedByDisplayName,
    string Description,
    MaintenancePriority Priority,
    MaintenanceRequestStatus Status,
    DateOnly RequestedDate,
    string? DeficiencyReference,
    Instant CreatedAtUtc);
