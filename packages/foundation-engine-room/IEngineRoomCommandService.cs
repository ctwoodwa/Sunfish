using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Write-side command service for the Engine Room Damage Control surface
/// per ADR 0079 §2. Three operations: quarantine, release, compact.
/// Implementations MUST verify <c>ShipAction.QuarantineDocument</c>
/// (resp. <c>ReleaseQuarantine</c> / <c>CompactDocument</c>) via
/// <c>IPermissionResolver</c> BEFORE invoking the persistence layer; on
/// denial they MUST throw <see cref="EngineRoomUnauthorizedException"/>
/// AND emit
/// <c>AuditEventType.DamageControlAuthorizationDenied</c> per §Trust.
/// </summary>
/// <remarks>
/// <para>
/// W#50 P1 substrate: the contract requires throwing on denial (rather
/// than returning a structured outcome) because Damage Control
/// operations are §Trust-elevated and the cohort precedent (W#55
/// PublishOutcome) is for non-elevated operations. Throwing prevents the
/// silent-rejection foot-gun for write-class destructive actions.
/// </para>
/// <para>
/// <b>Audit-emission ordering (per §5; W#50 P1 council Minor m1):</b>
/// the pre-op <c>*Requested</c> audit-event MUST be appended BEFORE
/// invoking <c>IPermissionResolver.ResolveAsync</c>, so denied requests
/// still leave a forensic trail. On denial: emit
/// <c>DamageControlAuthorizationDenied</c> + throw
/// <see cref="EngineRoomUnauthorizedException"/>. On success: emit the
/// post-op <c>*ed</c> audit-event AFTER the persistence layer accepts.
/// </para>
/// </remarks>
public interface IEngineRoomCommandService
{
    /// <summary>
    /// Quarantine <paramref name="documentId"/> for <paramref name="tenantId"/>.
    /// Caller MUST pre-authorize via
    /// <c>IPermissionResolver.ResolveAsync(tenantId, ..., ShipAction.QuarantineDocument, ...)</c>;
    /// the implementation re-verifies and throws
    /// <see cref="EngineRoomUnauthorizedException"/> on denial. Emits
    /// <c>AuditEventType.DocumentQuarantineRequested</c> pre-op and
    /// <c>AuditEventType.DocumentQuarantined</c> post-op per §5
    /// audit-emission ordering.
    /// </summary>
    ValueTask<QuarantineResult> QuarantineDocumentAsync(
        string documentId,
        TenantId tenantId,
        ActorId requestedBy,
        string reason,
        CancellationToken ct = default);

    /// <summary>
    /// Release <paramref name="documentId"/> from quarantine. Caller MUST
    /// pre-authorize via <c>ShipAction.ReleaseQuarantine</c>; the
    /// implementation re-verifies and throws
    /// <see cref="EngineRoomUnauthorizedException"/> on denial. Emits
    /// <c>AuditEventType.DocumentQuarantineReleaseRequested</c> pre-op
    /// and <c>AuditEventType.DocumentQuarantineReleased</c> post-op.
    /// </summary>
    ValueTask<ReleaseResult> ReleaseQuarantineAsync(
        string documentId,
        TenantId tenantId,
        ActorId requestedBy,
        string reason,
        CancellationToken ct = default);

    /// <summary>
    /// Compact <paramref name="documentId"/>'s CRDT representation. Caller
    /// MUST pre-authorize via <c>ShipAction.CompactDocument</c>. Throws
    /// <see cref="System.InvalidOperationException"/> (NOT
    /// <see cref="EngineRoomUnauthorizedException"/>) when the document
    /// is not eligible for compaction — eligibility is a state check, not
    /// an authority check. Emits
    /// <c>AuditEventType.ManualCompactionInitiated</c> pre-op and
    /// <c>AuditEventType.ManualCompactionCompleted</c> post-op.
    /// </summary>
    ValueTask<CompactionResult> CompactDocumentAsync(
        string documentId,
        TenantId tenantId,
        ActorId requestedBy,
        CancellationToken ct = default);
}
