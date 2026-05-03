namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Configuration surface for Bridge's per-tenant data-plane orchestration
/// (ADR 0031 Zone-C Hybrid — hosted-node-as-SaaS with per-tenant data-plane
/// isolation). Bound from <c>Bridge:Orchestration</c> in <c>appsettings.json</c>
/// by <see cref="ServiceCollectionExtensions.AddBridgeOrchestration"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rationale (ADR 0031).</b> Bridge's paper §20.7 Zone-C shape requires
/// that every tenant's team data (SQLCipher DB, event log, bucket manifests)
/// live on a tenant-private disk root and, at runtime, be served by a
/// per-tenant <c>local-node-host</c> child process. This options type is the
/// single bind-target that the supervisor (Wave 5.2.C), the lifecycle
/// coordinator (5.2.C), and the health monitor (5.2.D) all consume — it pins
/// the string conventions so the downstream tasks don't renegotiate them.
/// See <c>_shared/product/wave-5.2-decomposition.md</c> §2 and §5 "Tenant
/// Data-Dir Layout — Lock-In" for the target-state matrix.
/// </para>
/// <para>
/// <b>Numeric defaults are placeholders (decomposition plan §7 anti-pattern
/// #15 "Premature precision").</b> <see cref="MaxConcurrentTenants"/> = 50,
/// <see cref="RelayRefreshInterval"/> = 30s, <see cref="HealthPollInterval"/>
/// = 10s, and <see cref="HealthFailureStrikeCount"/> = 3 are initial values
/// chosen to unblock Wave 5.2 integration tests; they will be tuned in
/// Wave 5.5 after real load data exists. Do not treat them as SLO commitments.
/// </para>
/// </remarks>
public sealed class BridgeOrchestrationOptions
{
    /// <summary>
    /// Absolute path to the Bridge-owned root directory under which every
    /// tenant's private data plane lives — tenants at
    /// <c>{TenantDataRoot}/tenants/{TenantId:D}/</c> and cancelled-tenant
    /// ciphertext at <c>{TenantDataRoot}/graveyard/{TenantId:D}/…</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Required.</b> No default is supplied in-code. <c>Program.cs</c> binds
    /// this from <c>Bridge:Orchestration:TenantDataRoot</c>; the integration
    /// test-host (Wave 5.2.E) supplies a per-test temp directory.
    /// </para>
    /// <para>
    /// Default-by-platform values per decomposition plan §5 are:
    /// <c>%LOCALAPPDATA%\Sunfish\Bridge\tenants</c> on Windows and
    /// <c>/var/lib/sunfish/bridge/tenants</c> on POSIX. They live in
    /// <c>appsettings.json</c>, not here, so that sysadmins can override them
    /// without recompiling.
    /// </para>
    /// </remarks>
    public string TenantDataRoot { get; set; } = default!;

    /// <summary>
    /// Placeholder upper bound on the number of tenant processes Bridge will
    /// spawn concurrently. Guard-rail only — the supervisor (Wave 5.2.C)
    /// refuses to start a new tenant when the live count equals this value.
    /// </summary>
    /// <remarks>
    /// Default <c>50</c> is a placeholder per decomposition plan §7 anti-pattern
    /// #15 "Premature precision" — tuned in Wave 5.5 from real operator load
    /// data, not from a benchmark. Set per-install as capacity demands.
    /// </remarks>
    public int MaxConcurrentTenants { get; set; } = 50;

    /// <summary>
    /// Endpoint of Bridge's relay, as dialed by per-tenant
    /// <c>local-node-host</c> children for cross-node gossip. <see langword="null"/>
    /// when Wave 2.1 sync-daemon transport has not yet landed — in that state,
    /// supervisor still spawns per-tenant processes (for control-plane tests
    /// and health checks) but the children's relay dial-out is a no-op.
    /// </summary>
    /// <remarks>
    /// Nullable is deliberate per decomposition plan §8 stop-work #2: Wave 2.1
    /// is a prerequisite for meaningful gossip. Shipping the field up front
    /// lets 5.2.A + 5.2.C land without churn when 2.1 comes online.
    /// </remarks>
    public string? RelayEndpoint { get; set; }

    /// <summary>
    /// How often the tenant-lifecycle coordinator re-reads
    /// <c>TenantRegistry.ListActiveAsync</c> to rebuild the relay's
    /// <c>AllowedTeamIds</c> allowlist and reconcile live supervisor state
    /// against authoritative DB state.
    /// </summary>
    /// <remarks>
    /// Default <c>30s</c> is a placeholder per decomposition plan §7 anti-pattern
    /// #15 "Premature precision" — tuned in Wave 5.5.
    /// </remarks>
    public TimeSpan RelayRefreshInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Absolute path to the <c>local-node-host</c> executable used by the
    /// supervisor's post-boot <c>Process.Start</c> fallback path (decomposition
    /// plan §4 "Aspire Integration Decision" — hybrid).
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> when the supervisor can resolve the host from
    /// Aspire's <c>AddProject&lt;Projects.Sunfish_LocalNodeHost&gt;</c> metadata;
    /// only required when the supervisor must spawn a child post-boot (the
    /// common case, since AppHost restart per tenant signup is unacceptable).
    /// </remarks>
    public string? LocalNodeExecutablePath { get; set; }

    /// <summary>
    /// How often the tenant health monitor (Wave 5.2.D) polls each live
    /// tenant's <c>/health</c> endpoint.
    /// </summary>
    /// <remarks>
    /// Default <c>10s</c> is a placeholder per decomposition plan §7 anti-pattern
    /// #15 "Premature precision" — tuned in Wave 5.5.
    /// </remarks>
    public TimeSpan HealthPollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Consecutive failed health polls required before the monitor transitions
    /// a tenant to <see cref="TenantProcessState.Unhealthy"/>.
    /// </summary>
    /// <remarks>
    /// Default <c>3</c> is a placeholder per decomposition plan §7 anti-pattern
    /// #15 "Premature precision" — tuned in Wave 5.5 from false-positive rate
    /// observed in production.
    /// </remarks>
    public int HealthFailureStrikeCount { get; set; } = 3;
}
