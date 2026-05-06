using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Medevac state-machine service per ADR 0082 §2. Owns the six-state
/// transition table for the medevac flow + enforces the §Trust
/// four-eyes invariant on <see cref="AuthorizeAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>State-transition table (per §2):</b><br/>
/// <c>Idle → Requested</c> via <see cref="RequestAsync"/><br/>
/// <c>Requested → PendingAuthorization</c> (implementation-internal routing)<br/>
/// <c>PendingAuthorization → Authorized</c> via <see cref="AuthorizeAsync"/>
/// — REJECTS self-approval; the authorizing principal MUST NOT equal
/// the requesting principal<br/>
/// <c>Authorized → InProgress</c> (implementation-internal dispatch)<br/>
/// <c>InProgress → Complete</c> via <see cref="CompleteAsync"/><br/>
/// <c>* → Idle</c> via <see cref="CancelAsync"/> (terminal cycle reset)<br/>
/// </para>
/// <para>
/// <b>§Trust four-eyes invariant:</b>
/// <see cref="AuthorizeAsync"/> MUST emit
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.SickBayMedevacSelfApprovalRejected"/>
/// AND throw <see cref="System.InvalidOperationException"/> when the
/// authorizing principal equals the requesting principal. The dual
/// emission lets the Sick Bay timeline surface the rejection event
/// alongside the throw.
/// </para>
/// <para>
/// <b>Invalid transitions</b> throw <see cref="System.InvalidOperationException"/>
/// with an attempted-transition message of the form
/// <c>"medevac state machine: cannot transition from {current} to {target}"</c>.
/// </para>
/// </remarks>
public interface IMedevacService
{
    /// <summary>Returns the current medevac state for the tenant.</summary>
    Task<MedevacState> GetStateAsync(TenantId tenant, CancellationToken ct = default);

    /// <summary>
    /// File a medevac request. Transitions
    /// <see cref="MedevacState.Idle"/> → <see cref="MedevacState.Requested"/>;
    /// throws on invalid transitions.
    /// </summary>
    Task RequestAsync(
        TenantId tenant,
        PrincipalId requestedBy,
        string reason,
        CancellationToken ct = default);

    /// <summary>
    /// Authorize a pending medevac. Transitions
    /// <see cref="MedevacState.PendingAuthorization"/> →
    /// <see cref="MedevacState.Authorized"/>; ENFORCES the §Trust
    /// four-eyes invariant — the authorizing principal MUST NOT equal
    /// the requesting principal.
    /// </summary>
    Task AuthorizeAsync(
        TenantId tenant,
        PrincipalId authorizingPrincipal,
        CancellationToken ct = default);

    /// <summary>
    /// Cancel any non-terminal medevac state. Transitions
    /// <c>{Requested|PendingAuthorization|Authorized|InProgress}</c> →
    /// <see cref="MedevacState.Idle"/>; <see cref="MedevacState.Idle"/>
    /// and <see cref="MedevacState.Complete"/> throw.
    /// </summary>
    Task CancelAsync(
        TenantId tenant,
        PrincipalId cancellingPrincipal,
        CancellationToken ct = default);

    /// <summary>
    /// Mark the medevac complete. Transitions
    /// <see cref="MedevacState.InProgress"/> →
    /// <see cref="MedevacState.Complete"/>.
    /// <see cref="MedevacState.Complete"/> is terminal — there is no
    /// transition out without a new cycle starting from
    /// <see cref="MedevacState.Idle"/> (per W#54 P1 council Minor m1).
    /// </summary>
    Task CompleteAsync(TenantId tenant, CancellationToken ct = default);
}
