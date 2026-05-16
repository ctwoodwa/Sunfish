using System.Collections.Concurrent;

namespace Sunfish.Anchor.Services;

/// <summary>
/// W#67 / ADR 0046-A6 — in-memory <see cref="IEphemeralRecoveryKeyStore"/>
/// fallback. Used by the Anchor tests project (which deliberately does
/// not pull in MAUI's <c>SecureStorage</c>) and as a dev-mode default
/// when the MAUI implementation is unavailable. Production binds the
/// MAUI <c>SecureStorage</c>-backed implementation in
/// <c>MauiProgram.cs</c>.
/// </summary>
/// <remarks>
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>. Holds
/// secret material in plaintext process memory — by design only for
/// tests and dev-mode bootstrap; production must use a platform-secure
/// store.
/// </remarks>
public sealed class InMemoryEphemeralRecoveryKeyStore : IEphemeralRecoveryKeyStore
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task SetAsync(string keyName, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        cancellationToken.ThrowIfCancellationRequested();
        _store[keyName] = value.ToArray();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<byte[]?> GetAsync(string keyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.TryGetValue(keyName, out var v) ? v : (byte[]?)null);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string keyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        cancellationToken.ThrowIfCancellationRequested();
        _store.TryRemove(keyName, out _);
        return Task.CompletedTask;
    }
}
