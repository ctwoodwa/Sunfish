using System.Collections.Concurrent;

namespace Sunfish.Foundation.LocalFirst.Encryption;

/// <summary>
/// In-memory, dictionary-backed <see cref="IKeystore"/> for tests. Not suitable
/// for production use — keys live only for the lifetime of the process.
/// </summary>
public sealed class InMemoryKeystore : IKeystore
{
    private readonly ConcurrentDictionary<string, byte[]> _data = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<ReadOnlyMemory<byte>?> GetKeyAsync(string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_data.TryGetValue(name, out var value))
        {
            return Task.FromResult<ReadOnlyMemory<byte>?>(value);
        }

        return Task.FromResult<ReadOnlyMemory<byte>?>(null);
    }

    /// <inheritdoc />
    public Task SetKeyAsync(string name, ReadOnlyMemory<byte> key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _data[name] = key.ToArray();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteKeyAsync(string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _data.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}
