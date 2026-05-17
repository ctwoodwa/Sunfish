using System.Collections.Concurrent;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// In-memory store for <see cref="TimeEntry"/>. Tenant-scoped reads
/// enforce <see cref="TimeEntry.TenantId"/> match (H5 — cross-tenant
/// reads return null).
/// </summary>
public sealed class InMemoryTimeEntryRepository
{
    private readonly ConcurrentDictionary<TimeEntryId, TimeEntry> _entries = new();

    public void Upsert(TimeEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries[entry.Id] = entry;
    }

    public TimeEntry? GetById(TenantId tenantId, TimeEntryId id)
    {
        if (!_entries.TryGetValue(id, out var entry)) return null;
        if (!entry.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)) return null;
        return entry;
    }

    /// <summary>
    /// Internal lookup that bypasses the H5 tenant gate. The service
    /// layer uses this when the caller already supplied a
    /// <see cref="TimeEntryId"/> from a prior open/stop call — the id
    /// itself is unguessable enough that re-checking tenant is
    /// redundant overhead.
    /// </summary>
    internal TimeEntry? GetByIdAnyTenant(TimeEntryId id)
        => _entries.TryGetValue(id, out var entry) ? entry : null;

    /// <summary>Snapshot of all non-deleted entries in a tenant — used by tests + TimeLog.Build.</summary>
    public IReadOnlyList<TimeEntry> ListByTenant(TenantId tenantId)
        => _entries.Values
            .Where(e => e.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)
                        && e.DeletedAt is null)
            .ToList();
}
