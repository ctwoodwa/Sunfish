namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Spawns, stops, and interrogates the per-tenant <c>local-node-host</c>
/// child processes that serve each Bridge tenant's data plane (ADR 0031
/// Zone-C Hybrid). Implemented in Wave 5.2.C
/// (<c>TenantProcessSupervisor.cs</c>); this interface is the contract every
/// caller — the lifecycle coordinator (5.2.C), the AppHost integration
/// (5.2.E), and the Blazor admin UI (Wave 5.3) — consumes.
/// </summary>
/// <remarks>
/// <para>
/// Wave 5.2.A ships the interface only — no behaviour. The decomposition
/// plan §7 anti-pattern #7 ("Delegation without contracts") calls out that
/// pinning the contract before 5.2.B and 5.2.C dispatch avoids re-negotiation
/// churn; that is why the interface lives in the contracts scaffold rather
/// than alongside its implementation.
/// </para>
/// <para>
/// <b>Thread-safety.</b> Implementations MUST be safe for concurrent
/// invocation — the admin UI, the lifecycle coordinator, and the health
/// monitor will all call into this interface from independent threads.
/// </para>
/// <para>
/// <b>Idempotency.</b> All mutating methods MUST be idempotent:
/// <see cref="StartAsync"/> on an already-running tenant is a no-op,
/// <see cref="PauseAsync"/> on an already-paused tenant is a no-op, and
/// <see cref="StopAndEraseAsync"/> on an already-cancelled tenant is a no-op.
/// See decomposition plan §2.2–§2.4.
/// </para>
/// </remarks>
public interface ITenantProcessSupervisor
{
    /// <summary>
    /// Spawns the per-tenant child process (via Aspire <c>AddProject</c> or
    /// <c>Process.Start</c> fallback — see decomposition plan §4) and
    /// transitions the tenant to <see cref="TenantProcessState.Starting"/>,
    /// then <see cref="TenantProcessState.Running"/> once the first health
    /// probe succeeds. Idempotent.
    /// </summary>
    ValueTask StartAsync(Guid tenantId, CancellationToken ct);

    /// <summary>
    /// Gracefully stops the per-tenant child and transitions the tenant to
    /// <see cref="TenantProcessState.Paused"/>. Disk is retained untouched
    /// per decomposition plan §2.2. Idempotent.
    /// </summary>
    ValueTask PauseAsync(Guid tenantId, CancellationToken ct);

    /// <summary>
    /// Re-spawns a previously-paused tenant's child with the same spawn
    /// spec + env vars, transitioning back through
    /// <see cref="TenantProcessState.Starting"/> to
    /// <see cref="TenantProcessState.Running"/>. Fails on tenants in
    /// <see cref="TenantProcessState.Cancelled"/>. Idempotent against a
    /// tenant already in <see cref="TenantProcessState.Running"/>.
    /// </summary>
    ValueTask ResumeAsync(Guid tenantId, CancellationToken ct);

    /// <summary>
    /// Stops the tenant's child and disposes the tenant's disk per
    /// <paramref name="mode"/>, then transitions the tenant to
    /// <see cref="TenantProcessState.Cancelled"/>. Idempotent. Per
    /// decomposition plan §2.4, callers are expected to have already
    /// transitioned the control-plane <c>TenantStatus</c> to
    /// <c>Cancelled</c> before invoking this.
    /// </summary>
    /// <param name="tenantId">Tenant to erase.</param>
    /// <param name="mode">Disk-disposal policy — graveyard move vs. secure wipe.</param>
    /// <param name="ct">Cancellation token for the stop/erase I/O.</param>
    ValueTask StopAndEraseAsync(Guid tenantId, DeleteMode mode, CancellationToken ct);

    /// <summary>
    /// Returns the supervisor's current view of the tenant's process state.
    /// Returns <see cref="TenantProcessState.Unknown"/> for tenants the
    /// supervisor has never been asked to manage.
    /// </summary>
    ValueTask<TenantProcessState> GetStateAsync(Guid tenantId, CancellationToken ct);

    /// <summary>
    /// Raised whenever a tenant's state transitions. See
    /// <see cref="TenantProcessEvent"/> for payload shape. Handlers MUST be
    /// non-blocking — the supervisor fires synchronously inside its own
    /// transition lock, so a slow handler delays subsequent transitions for
    /// the same tenant.
    /// </summary>
    event EventHandler<TenantProcessEvent>? StateChanged;
}
