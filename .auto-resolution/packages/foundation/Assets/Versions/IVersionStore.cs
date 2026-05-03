using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Versions;

/// <summary>
/// Read surface over the append-only version log.
/// </summary>
/// <remarks>
/// Spec §3.2. Phase A surfaces linear history only; <see cref="BranchAsync"/> and
/// <see cref="MergeAsync"/> throw <see cref="NotImplementedException"/> (plan D-CRDT-ROUTE).
/// </remarks>
public interface IVersionStore
{
    /// <summary>Returns the version with the given id, or <c>null</c> if unknown.</summary>
    Task<Version?> GetVersionAsync(VersionId id, CancellationToken ct = default);

    /// <summary>Streams the full version history of an entity ordered by sequence ascending.</summary>
    IAsyncEnumerable<Version> GetHistoryAsync(EntityId entity, CancellationToken ct = default);

    /// <summary>
    /// Returns the version whose validity range contains <paramref name="at"/>, or <c>null</c>
    /// if no such version exists (i.e. <paramref name="at"/> predates the first version).
    /// </summary>
    Task<Version?> GetAsOfAsync(EntityId entity, DateTimeOffset at, CancellationToken ct = default);

    /// <summary>Phase A: <see cref="NotImplementedException"/>.</summary>
    Task<VersionId> BranchAsync(VersionId from, BranchOptions options, CancellationToken ct = default);

    /// <summary>Phase A: <see cref="NotImplementedException"/>.</summary>
    Task<VersionId> MergeAsync(VersionId left, VersionId right, MergeOptions options, CancellationToken ct = default);
}
