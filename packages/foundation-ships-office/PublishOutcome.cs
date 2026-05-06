using System.Text.Json.Serialization;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Outcome of <see cref="IShipsOfficeCommandService.PublishAsync"/> per
/// ADR 0083 §5 + W#55 P1 pre-merge council 2026-05-06 (Major SI-1).
/// PublishAsync is silent on permission denial (it emits
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.ShipsOfficePublishRejected"/>
/// rather than throwing) — making the success/rejection distinction
/// explicit on the return type prevents callers from interpreting the
/// absence of an exception as a confirmation of publication.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PublishOutcome
{
    /// <summary>The document moved to <see cref="DocumentStatus.Published"/>.</summary>
    Published,

    /// <summary>The publish attempt was denied at the permission gate; the rejection has been audited.</summary>
    Rejected,
}
