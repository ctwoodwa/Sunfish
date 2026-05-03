using System.Collections.Concurrent;

namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// In-process registry mapping per-tenant GUIDs to the <see cref="Uri"/> on
/// which their <c>local-node-host</c> child exposes the Wave 5.2.D
/// <c>/health</c> endpoint (and, in later waves, any other HTTP surface the
/// child serves). Owned by Bridge, populated by
/// <see cref="ITenantProcessSupervisor"/> (Wave 5.2.C) immediately after
/// spawn, consumed by <see cref="TenantHealthMonitor"/> (Wave 5.2.D).
/// </summary>
/// <remarks>
/// <para>
/// Per <c>_shared/product/wave-5.2-decomposition.md</c> §8 stop-work #5,
/// endpoint routing persistence across AppHost restart is a Wave 5.3
/// concern; Wave 5.2 ships with an in-memory implementation. The supervisor's
/// boot-time reconciliation re-populates the registry from
/// <c>ITenantRegistry.ListActiveAsync</c> (Wave 5.2.C).
/// </para>
/// <para>
/// Registered as a singleton — concurrent reads and writes are expected
/// (supervisor writes on spawn; monitor reads on every poll tick; admin UI
/// reads on every page render).
/// </para>
/// </remarks>
public interface ITenantEndpointRegistry
{
    /// <summary>
    /// Record or replace the endpoint for <paramref name="tenantId"/>.
    /// Idempotent — a subsequent call with the same URI is a no-op; with a
    /// different URI it replaces the mapping.
    /// </summary>
    void Register(Guid tenantId, Uri endpoint);

    /// <summary>
    /// Remove <paramref name="tenantId"/>'s mapping if present. Idempotent.
    /// Called by <see cref="ITenantProcessSupervisor.StopAndEraseAsync"/> on
    /// cancellation, and by <see cref="ITenantProcessSupervisor.PauseAsync"/>
    /// on pause.
    /// </summary>
    void Unregister(Guid tenantId);

    /// <summary>
    /// Look up the endpoint for <paramref name="tenantId"/>. Returns
    /// <see langword="false"/> when no mapping exists — e.g. the tenant is
    /// paused, cancelled, or never-spawned.
    /// </summary>
    bool TryGet(Guid tenantId, out Uri? endpoint);

    /// <summary>
    /// Snapshot of every currently-registered mapping. Safe to enumerate
    /// across concurrent mutations — each call returns a fresh collection.
    /// </summary>
    IReadOnlyDictionary<Guid, Uri> Snapshot();
}

/// <summary>
/// Default in-process implementation of <see cref="ITenantEndpointRegistry"/>.
/// Thread-safe; backed by <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class InMemoryTenantEndpointRegistry : ITenantEndpointRegistry
{
    private readonly ConcurrentDictionary<Guid, Uri> _endpoints = new();

    /// <inheritdoc />
    public void Register(Guid tenantId, Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        _endpoints[tenantId] = endpoint;
    }

    /// <inheritdoc />
    public void Unregister(Guid tenantId) => _endpoints.TryRemove(tenantId, out _);

    /// <inheritdoc />
    public bool TryGet(Guid tenantId, out Uri? endpoint)
    {
        if (_endpoints.TryGetValue(tenantId, out var value))
        {
            endpoint = value;
            return true;
        }
        endpoint = null;
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Guid, Uri> Snapshot()
        => new Dictionary<Guid, Uri>(_endpoints);
}
