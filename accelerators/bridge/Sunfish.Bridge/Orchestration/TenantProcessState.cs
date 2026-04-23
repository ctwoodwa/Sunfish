namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Runtime lifecycle of a per-tenant <c>local-node-host</c> child process
/// as seen by <see cref="ITenantProcessSupervisor"/>. Distinct from
/// <c>Sunfish.Bridge.Data.Entities.TenantStatus</c>, which tracks the
/// control-plane billing/authorization state in Postgres — this enum tracks
/// only the in-memory process handle's observable status.
/// </summary>
/// <remarks>
/// Per <c>_shared/product/wave-5.2-decomposition.md</c> §2 "Target State per
/// Lifecycle Operation". Transitions are driven by supervisor operations
/// (5.2.C) and by the health monitor (5.2.D); Wave 5.2.A only pins the enum
/// shape so downstream tasks don't renegotiate it.
/// </remarks>
public enum TenantProcessState
{
    /// <summary>
    /// Supervisor has no record of the tenant, or the supervisor was just
    /// restarted and has not yet polled the tenant's status. Per decomposition
    /// plan §7 anti-pattern #6 "Missing Resume Protocol", AppHost restart
    /// rebuilds state by reading <c>TenantRegistry.ListActiveAsync()</c>; any
    /// tenant not yet reconciled sits in <see cref="Unknown"/>.
    /// </summary>
    Unknown,

    /// <summary>
    /// <see cref="ITenantProcessSupervisor.StartAsync"/> or
    /// <see cref="ITenantProcessSupervisor.ResumeAsync"/> has been invoked
    /// and the child process has been spawned, but no health probe has yet
    /// confirmed it is live. Corresponds to decomposition plan §2.1
    /// "Create tenant" between <c>Process.Start</c> and first successful
    /// <c>/health</c> probe.
    /// </summary>
    Starting,

    /// <summary>
    /// Child process is live and returning HTTP 200 on <c>/health</c> with
    /// both <c>IActiveTeamAccessor.Active is not null</c> and
    /// <c>IGossipDaemon.State == Started</c>. The "green" steady state per
    /// decomposition plan §6 "Health + Monitoring".
    /// </summary>
    Running,

    /// <summary>
    /// Health monitor observed <see cref="BridgeOrchestrationOptions.HealthFailureStrikeCount"/>
    /// consecutive failed polls (HTTP 5xx, timeout &gt;2s, or unreachable).
    /// Per decomposition plan §6 the supervisor does NOT automatically
    /// restart the child — Wave 5.2 leaves recovery to operator action, with
    /// auto-restart a Wave 5.5 candidate.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Child was stopped by <see cref="ITenantProcessSupervisor.PauseAsync"/>
    /// — billing failure or operator-initiated pause. Disk is retained
    /// untouched. Transitions to <see cref="Starting"/> on
    /// <see cref="ITenantProcessSupervisor.ResumeAsync"/>. Per decomposition
    /// plan §2.2 "Pause tenant".
    /// </summary>
    Paused,

    /// <summary>
    /// Terminal. Child was stopped by
    /// <see cref="ITenantProcessSupervisor.StopAndEraseAsync"/>; disk is
    /// either in the graveyard (<see cref="DeleteMode.RetainCiphertext"/>)
    /// or recursively deleted (<see cref="DeleteMode.SecureWipe"/>). No
    /// further transitions; the supervisor's handle is removed on the next
    /// reconciliation pass. Per decomposition plan §2.4 "Delete tenant".
    /// </summary>
    Cancelled,

    /// <summary>
    /// Terminal for this process lifetime. Child exited unexpectedly (non-zero
    /// exit code or spawn error); supervisor retains the handle so the operator
    /// can query <see cref="ITenantProcessSupervisor.GetStateAsync"/> and decide
    /// whether to <see cref="ITenantProcessSupervisor.ResumeAsync"/> (which
    /// transitions back to <see cref="Starting"/>).
    /// </summary>
    Failed,
}
