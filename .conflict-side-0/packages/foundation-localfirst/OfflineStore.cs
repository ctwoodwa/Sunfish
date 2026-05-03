using System.Collections.Concurrent;

namespace Sunfish.Foundation.LocalFirst;

/// <summary>
/// Keyed binary read / write / delete over a local store. Format-agnostic —
/// callers serialize their own payloads (JSON, MessagePack, …) into the byte
/// arrays this contract transports.
/// </summary>
public interface IOfflineStore
{
    /// <summary>Reads the value for a key. Returns null if absent.</summary>
    ValueTask<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes (or replaces) the value for a key.</summary>
    ValueTask WriteAsync(string key, byte[] value, CancellationToken cancellationToken = default);

    /// <summary>Deletes the value for a key. Returns true if a value was removed.</summary>
    ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Returns a snapshot of keys starting with the given prefix.</summary>
    ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken cancellationToken = default);
}

/// <summary>In-memory reference implementation of <see cref="IOfflineStore"/>.</summary>
public sealed class InMemoryOfflineStore : IOfflineStore
{
    private readonly ConcurrentDictionary<string, byte[]> _data = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        _data.TryGetValue(key, out var value);
        return ValueTask.FromResult(value);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string key, byte[] value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        _data[key] = value;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var removed = _data.TryRemove(key, out _);
        return ValueTask.FromResult(removed);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var snapshot = _data.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();
        return ValueTask.FromResult<IReadOnlyList<string>>(snapshot);
    }
}
