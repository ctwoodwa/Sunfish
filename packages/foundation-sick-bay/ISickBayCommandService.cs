using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Write-side command service for Sick Bay key-rotation operations per
/// ADR 0082 §2. Phase 1 ships the contract; Phase 2 wires the
/// <see cref="IKeyRotationScheduler"/> implementation behind it.
/// </summary>
/// <remarks>
/// W#54 P1: <see cref="TriggerKeyRotationAsync"/> takes
/// <c>string triggerReason</c>. Phase 2 amendment swaps to a typed
/// <c>KeyRotationTrigger</c> enum (gated on H3 — ADR 0068 reaching
/// <c>Status: Accepted</c>). Phase 1 ships the string surface so
/// downstream Phase 2 / Phase 3b consumers can build against the
/// stable contract regardless of the H3 timeline.
/// </remarks>
public interface ISickBayCommandService
{
    /// <summary>
    /// Trigger a manual key rotation for <paramref name="fieldPurpose"/>.
    /// Caller MUST pre-authorize via
    /// <c>IPermissionResolver.ResolveAsync(tenant, ..., ShipAction.TriggerKeyRotation, ...)</c>.
    /// Implementations MUST emit
    /// <see cref="Sunfish.Kernel.Audit.AuditEventType.SickBayKeyRotationTriggered"/>
    /// pre-op per §6 audit-emission ordering.
    /// </summary>
    /// <param name="tenant">Owning tenant.</param>
    /// <param name="fieldPurpose">Wayfinder field-purpose key (e.g., <c>"recovery-key"</c>).</param>
    /// <param name="triggerReason">Free-form rationale; written into the audit payload. Phase 2 amendment swaps to typed <c>KeyRotationTrigger</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task TriggerKeyRotationAsync(
        TenantId tenant,
        string fieldPurpose,
        string triggerReason,
        CancellationToken ct = default);
}
