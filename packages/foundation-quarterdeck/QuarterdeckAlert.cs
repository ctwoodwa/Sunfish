using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// A single alert entry surfaced on the Quarterdeck ticker per
/// ADR 0080 §2.3. Sourced from any registered
/// <see cref="IQuarterdeckAlertSource"/>; emitted to the snapshot after
/// permission filtering per <see cref="AlertVisibilityPolicy"/>.
/// </summary>
/// <param name="AlertId">
/// Stable identifier for the alert; durable across snapshot emits so
/// the ticker can correlate acknowledgements across heartbeats.
/// MUST be unique across all <see cref="IQuarterdeckAlertSource"/>
/// implementations within a tenant; the data provider drops collisions
/// during aggregation. Format: <c>"{SourceName}:{source-local-id}"</c>;
/// characters MUST match the regex
/// <c>^[A-Za-z0-9_\-:]{1,128}$</c> per ADR 0080 §1. Recommended
/// source-local-id form is a ULID for stable lexicographic ordering.
/// </param>
/// <param name="TenantId">
/// Tenant the alert belongs to. Set even for cross-tenant administrative
/// sources (the alert source is responsible for restricting which
/// tenants see which alerts).
/// </param>
/// <param name="Severity">Severity bucket; drives sort order + politeness.</param>
/// <param name="Title">
/// Short headline for the alert. The data provider does NOT localize
/// here; alert sources emit pre-localized text or localization keys per
/// their own contract.
/// </param>
/// <param name="Summary">
/// Optional one-sentence body. Null when the title alone conveys the
/// alert.
/// </param>
/// <param name="IssuedAt">
/// Wall-clock timestamp when the alert was issued. Drives secondary
/// sort within a severity bucket.
/// </param>
/// <param name="ExpiresAt">
/// Optional expiry. When set + the timestamp has elapsed + the alert
/// does not require acknowledgement, the data provider omits the alert
/// from the snapshot per §2.3 rule 8.
/// </param>
/// <param name="RequiresAcknowledgement">
/// Whether the alert demands explicit acknowledgement before it can
/// drop off the ticker. Acknowledgement-required alerts persist past
/// <see cref="ExpiresAt"/>.
/// </param>
/// <param name="IsAcknowledged">
/// Whether the alert has been acknowledged. Acknowledged alerts may
/// still surface in a recently-acknowledged tail per UI policy.
/// </param>
/// <param name="AcknowledgedBy">
/// Display name of the actor who acknowledged the alert; null when
/// <paramref name="IsAcknowledged"/> is false.
/// </param>
/// <param name="AcknowledgedAt">
/// Wall-clock timestamp of acknowledgement; null when
/// <paramref name="IsAcknowledged"/> is false.
/// </param>
/// <param name="SourceName">
/// Registered name of the originating <see cref="IQuarterdeckAlertSource"/>
/// per §5.3. The data provider validates uniqueness at startup and
/// stamps each alert with its source for downstream traceability. The
/// prefix <c>"sunfish.*"</c> is reserved for first-party sources;
/// third-party alerts MUST use a non-reserved prefix.
/// </param>
/// <param name="VisibilityPolicy">
/// Per-alert disclosure policy per ADR 0080 §1 + §2.3 rule 5. Defaults
/// to <see cref="AlertVisibilityPolicy.OmitForDeniedActors"/>; set to
/// <see cref="AlertVisibilityPolicy.ShowAll"/> for ship-wide broadcast
/// alerts where suppression would defeat the alert's safety purpose.
/// The policy is point-in-time alert data — once issued, an alert
/// keeps its disclosure policy across all snapshot emits.
/// </param>
/// <remarks>
/// <b>Acknowledgement-state invariant:</b> the tuple
/// <c>(IsAcknowledged, AcknowledgedBy, AcknowledgedAt)</c> has two
/// legal states only — <c>(false, null, null)</c> or
/// <c>(true, name, timestamp)</c>. Phase 2 providers MUST NOT emit
/// the contradictory state <c>(true, null, _)</c>;
/// <see cref="IQuarterdeckCommandService.AcknowledgeAlertAsync"/>
/// implementations satisfy this by writing all three fields together
/// when they transition the alert to acknowledged.
/// </remarks>
public sealed record QuarterdeckAlert(
    string AlertId,
    TenantId TenantId,
    AlertSeverity Severity,
    string Title,
    string? Summary,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    bool RequiresAcknowledgement,
    bool IsAcknowledged,
    string? AcknowledgedBy,
    DateTimeOffset? AcknowledgedAt,
    string SourceName,
    AlertVisibilityPolicy VisibilityPolicy = AlertVisibilityPolicy.OmitForDeniedActors);
