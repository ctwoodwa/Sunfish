using System.Runtime.CompilerServices;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Versions;

/// <summary>
/// Zero-dependency in-memory <see cref="IVersionStore"/> sharing storage with
/// <see cref="Sunfish.Foundation.Assets.Entities.InMemoryEntityStore"/>.
/// </summary>
public sealed class InMemoryVersionStore : IVersionStore
{
    private readonly InMemoryAssetStorage _storage;

    /// <summary>Creates an in-memory version store backed by the given shared storage.</summary>
    public InMemoryVersionStore(InMemoryAssetStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <inheritdoc />
    public Task<Version?> GetVersionAsync(VersionId id, CancellationToken ct = default)
    {
        if (!_storage.Versions.TryGetValue(id.Entity, out var history))
            return Task.FromResult<Version?>(null);
        var hit = history.FirstOrDefault(v => v.Id.Sequence == id.Sequence && v.Id.Hash == id.Hash);
        return Task.FromResult<Version?>(hit);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Version> GetHistoryAsync(EntityId entity, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_storage.Versions.TryGetValue(entity, out var history))
            yield break;
        foreach (var version in history.ToArray())
        {
            ct.ThrowIfCancellationRequested();
            yield return version;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public Task<Version?> GetAsOfAsync(EntityId entity, DateTimeOffset at, CancellationToken ct = default)
    {
        if (!_storage.Versions.TryGetValue(entity, out var history))
            return Task.FromResult<Version?>(null);
        var hit = history
            .Where(v => v.ValidFrom <= at && (v.ValidTo is null || at < v.ValidTo))
            .OrderByDescending(v => v.Id.Sequence)
            .FirstOrDefault();
        return Task.FromResult<Version?>(hit);
    }

    /// <inheritdoc />
    public Task<VersionId> BranchAsync(VersionId from, BranchOptions options, CancellationToken ct = default)
        => throw new NotImplementedException(
            "Phase A ships linear history only. Branch/merge lands in Platform Phase B; see plan D-CRDT-ROUTE.");

    /// <inheritdoc />
    public Task<VersionId> MergeAsync(VersionId left, VersionId right, MergeOptions options, CancellationToken ct = default)
        => throw new NotImplementedException(
            "Phase A ships linear history only. Branch/merge lands in Platform Phase B; see plan D-CRDT-ROUTE.");
}
