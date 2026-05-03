namespace Sunfish.Foundation.LocalFirst;

/// <summary>
/// Describes a conflict between a local and remote version of the same key.
/// Callers supply <see cref="CommonAncestor"/> when available for three-way
/// merge strategies.
/// </summary>
public sealed record SyncConflict
{
    /// <summary>Store key being synced.</summary>
    public required string Key { get; init; }

    /// <summary>Local version payload.</summary>
    public required byte[] LocalVersion { get; init; }

    /// <summary>Remote version payload.</summary>
    public required byte[] RemoteVersion { get; init; }

    /// <summary>Optional common ancestor payload for three-way merge.</summary>
    public byte[]? CommonAncestor { get; init; }

    /// <summary>Local modification timestamp, when known.</summary>
    public DateTimeOffset? LocalModifiedAt { get; init; }

    /// <summary>Remote modification timestamp, when known.</summary>
    public DateTimeOffset? RemoteModifiedAt { get; init; }
}

/// <summary>
/// Strategy for merging conflicting versions into a single resolved payload.
/// Modules register their own resolver when last-writer-wins is not safe
/// (for example, richer CRDT-style merges).
/// </summary>
public interface ISyncConflictResolver
{
    /// <summary>Resolves a conflict and returns the merged payload bytes.</summary>
    ValueTask<byte[]> ResolveAsync(SyncConflict conflict, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default resolver: picks the version with the later modification timestamp,
/// preferring remote when timestamps are missing or equal. Suitable as a
/// baseline; modules with richer data should register their own.
/// </summary>
public sealed class LastWriterWinsConflictResolver : ISyncConflictResolver
{
    /// <inheritdoc />
    public ValueTask<byte[]> ResolveAsync(SyncConflict conflict, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        var winner = (conflict.LocalModifiedAt, conflict.RemoteModifiedAt) switch
        {
            (null, null) => conflict.RemoteVersion,
            (null, _) => conflict.RemoteVersion,
            (_, null) => conflict.LocalVersion,
            var (l, r) when l > r => conflict.LocalVersion,
            _ => conflict.RemoteVersion,
        };

        return ValueTask.FromResult(winner);
    }
}
