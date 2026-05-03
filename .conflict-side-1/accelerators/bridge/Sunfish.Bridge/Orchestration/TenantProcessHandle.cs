namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Per-tenant mutable state tracked by <see cref="TenantProcessSupervisor"/>
/// (Wave 5.2.C.1). Bundles the OS process handle, the endpoint it listens on,
/// the observable <see cref="TenantProcessState"/>, and boot-time diagnostics.
/// </summary>
/// <remarks>
/// Internal — the supervisor never exposes this type outward; consumers read
/// only <see cref="TenantProcessState"/> via
/// <see cref="ITenantProcessSupervisor.GetStateAsync"/>. Mutability is protected
/// by the supervisor's per-tenant lock.
/// </remarks>
internal sealed class TenantProcessHandle
{
    public Guid TenantId { get; init; }

    /// <summary>Null when the tenant is <see cref="TenantProcessState.Paused"/>
    /// or <see cref="TenantProcessState.Cancelled"/> — the underlying OS
    /// process has been killed and the reference released.</summary>
    public IProcessHandle? Process { get; set; }

    /// <summary>HTTP endpoint the child's health surface listens on.</summary>
    public Uri HealthEndpoint { get; set; } = default!;

    /// <summary>Observable lifecycle state. Mirrors what
    /// <see cref="ITenantProcessSupervisor.GetStateAsync"/> returns.</summary>
    public TenantProcessState State { get; set; } = TenantProcessState.Unknown;

    /// <summary>UTC instant the process was most recently started.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Per-tenant gate for serializing transitions — held by the
    /// supervisor during Start / Pause / Resume / StopAndErase so a
    /// concurrent caller can't see a half-built handle.</summary>
    public readonly object Gate = new();
}
