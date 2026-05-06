using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Per-tenant store for the most-recent + historical
/// <see cref="ProviderValidationStatusEntry"/> values per ADR 0067
/// §3.13. <b>NOT a Standing Order</b> — validation status is
/// transient operational state (provider reachability + credential
/// validity), not durable configuration intent. The Atlas
/// Integration-Config surface renders the current status badge from
/// <see cref="GetCurrentAsync"/> and the trend / history from
/// <see cref="HistoryAsync"/>.
/// </summary>
/// <remarks>
/// Cycle-safe: scoped to <see cref="ProviderValidationStatus"/> +
/// <see cref="IntegrationValidationResult"/> + foundation primitives
/// only. The W#48 council-flagged <c>IDecryptCapability</c> seam
/// (Phase 1.5 PR 2) is on the future <c>IDecryptCapabilityProvider</c>
/// interface, not here.
/// </remarks>
public interface IValidationStatusStore
{
    /// <summary>
    /// Returns the most-recent validation entry for the (tenant,
    /// category, provider) tuple, or null when no validation has run.
    /// </summary>
    Task<ProviderValidationStatusEntry?> GetCurrentAsync(
        TenantId tenantId,
        IntegrationCategory category,
        string providerId,
        CancellationToken ct = default);

    /// <summary>
    /// Persist a new validation result. Implementations append to the
    /// per-tenant history + update the current pointer atomically.
    /// </summary>
    Task UpdateAsync(
        TenantId tenantId,
        IntegrationCategory category,
        string providerId,
        IntegrationValidationResult result,
        ActorId actor,
        CancellationToken ct = default);

    /// <summary>
    /// Stream the validation history (newest first) up to
    /// <paramref name="maxEntries"/> entries. Default 20 — sufficient
    /// for the UI trend pane without unbounded fetch.
    /// </summary>
    IAsyncEnumerable<ProviderValidationStatusEntry> HistoryAsync(
        TenantId tenantId,
        IntegrationCategory category,
        string providerId,
        int maxEntries = 20,
        CancellationToken ct = default);
}
