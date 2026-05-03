using System.Collections.Concurrent;
using Sunfish.Blocks.BusinessCases.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.BusinessCases.Services;

/// <summary>
/// In-memory backing store for <see cref="BundleActivationRecord"/> rows keyed by
/// (tenant, bundle). Shared between <see cref="InMemoryBusinessCaseService"/> and
/// <see cref="InMemoryBundleProvisioningService"/> so reads see writes.
/// Not intended for production use.
/// </summary>
public sealed class InMemoryBundleActivationStore
{
    private readonly ConcurrentDictionary<(TenantId TenantId, string BundleKey), BundleActivationRecord> _records = new();

    /// <summary>Adds or replaces the activation record for the (tenant, bundle) pair.</summary>
    public void Upsert(BundleActivationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[(record.TenantId, record.BundleKey)] = record;
    }

    /// <summary>Removes the activation record for the (tenant, bundle) pair, if any.</summary>
    public bool Remove(TenantId tenantId, string bundleKey)
    {
        return _records.TryRemove((tenantId, bundleKey), out _);
    }

    /// <summary>Tries to fetch the activation record for the (tenant, bundle) pair.</summary>
    public bool TryGet(TenantId tenantId, string bundleKey, out BundleActivationRecord? record)
    {
        if (_records.TryGetValue((tenantId, bundleKey), out var value))
        {
            record = value;
            return true;
        }

        record = null;
        return false;
    }

    /// <summary>Returns the first active (not-deactivated) record for the tenant, or null.</summary>
    public BundleActivationRecord? GetFirstActive(TenantId tenantId)
    {
        foreach (var pair in _records)
        {
            if (pair.Key.TenantId.Equals(tenantId) && pair.Value.DeactivatedAt is null)
            {
                return pair.Value;
            }
        }

        return null;
    }
}
