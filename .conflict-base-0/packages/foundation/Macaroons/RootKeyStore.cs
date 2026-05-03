using System.Collections.Concurrent;

namespace Sunfish.Foundation.Macaroons;

/// <summary>
/// Abstraction over the issuer-side storage of macaroon root keys, keyed by
/// <see cref="Macaroon.Location"/>. Root keys are high-value secrets: treat them like
/// signing keys and rotate on a schedule.
/// </summary>
public interface IRootKeyStore
{
    /// <summary>
    /// Returns the root key for the given <paramref name="location"/>, or <c>null</c> if no
    /// key is registered. Returns the stored bytes directly — callers must not mutate.
    /// </summary>
    ValueTask<byte[]?> GetRootKeyAsync(string location, CancellationToken ct = default);
}

/// <summary>
/// An in-memory <see cref="IRootKeyStore"/> suitable for tests, dev, and single-process
/// deployments. Not persistent; not clustered.
/// </summary>
public sealed class InMemoryRootKeyStore : IRootKeyStore
{
    private readonly ConcurrentDictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers (or replaces) the root key for <paramref name="location"/>. The key is
    /// defensively copied so later mutations to <paramref name="key"/> do not leak in.
    /// </summary>
    public void Set(string location, ReadOnlySpan<byte> key)
    {
        ArgumentNullException.ThrowIfNull(location);
        _keys[location] = key.ToArray();
    }

    /// <inheritdoc />
    public ValueTask<byte[]?> GetRootKeyAsync(string location, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        return ValueTask.FromResult(_keys.TryGetValue(location, out var key) ? key : null);
    }
}
