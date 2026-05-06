using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Write-side command service for the Ship's Office surface per ADR 0083
/// §2. Two operations: publish a document, archive a document. Per §5
/// audit-emission ordering: implementations check permission FIRST, emit
/// the pre-op audit record SECOND, execute the operation THIRD; on
/// permission-rejected publish, emit
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.ShipsOfficePublishRejected"/>
/// rather than throwing.
/// </summary>
public interface IShipsOfficeCommandService
{
    /// <summary>
    /// Publish a draft document. Per §4 + §5: requires
    /// <see cref="ShipAction.PublishShipsOfficeDocument"/> at MainDeck,
    /// minimum role <c>XO</c>. On permission denial, the implementation
    /// MUST emit
    /// <see cref="Sunfish.Kernel.Audit.AuditEventType.ShipsOfficePublishRejected"/>
    /// and return <see cref="PublishOutcome.Rejected"/> WITHOUT throwing — the
    /// rejection is informational and the audit trail is the durable record.
    /// On success returns <see cref="PublishOutcome.Published"/>.
    /// </summary>
    /// <remarks>
    /// W#55 P1 pre-merge council 2026-05-06 (Major SI-1): the explicit
    /// outcome enum prevents callers from interpreting the absence of an
    /// exception as a confirmation of publication. Callers MUST branch
    /// on the outcome value to determine whether to surface a success or
    /// rejection affordance to the user.
    /// </remarks>
    Task<PublishOutcome> PublishAsync(
        TenantId tenant,
        ShipsOfficeDocumentId id,
        CancellationToken ct = default);

    /// <summary>
    /// Archive a published document. Per §4 + §5: requires
    /// <see cref="ShipAction.ArchiveShipsOfficeDocument"/> at MainDeck,
    /// minimum role <c>XO</c>. On permission denial, the implementation
    /// THROWS (<c>UnauthorizedAccessException</c> is the cohort
    /// convention) — archive is the rare path and a thrown exception is
    /// the right signal for the caller's audit-noise policy.
    /// </summary>
    Task ArchiveAsync(
        TenantId tenant,
        ShipsOfficeDocumentId id,
        CancellationToken ct = default);
}
