using Microsoft.Maui.Storage;

namespace Sunfish.Anchor.Services;

/// <summary>
/// W#67 / ADR 0046-A6 — MAUI <see cref="SecureStorage"/>-backed
/// implementation of <see cref="IEphemeralRecoveryKeyStore"/>.
/// Production binding. The ephemeral X25519 private key is stored
/// base64url-encoded (SecureStorage values are strings; the encoding
/// is symmetric so round-trip is exact).
/// </summary>
/// <remarks>
/// Lives in MAUI-bound code (NOT compiled into the MAUI-free
/// <c>accelerators/anchor/tests/tests.csproj</c>); tests use
/// <see cref="InMemoryEphemeralRecoveryKeyStore"/>.
/// </remarks>
internal sealed class MauiSecureStorageEphemeralRecoveryKeyStore : IEphemeralRecoveryKeyStore
{
    /// <inheritdoc />
    public Task SetAsync(string keyName, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        cancellationToken.ThrowIfCancellationRequested();
        var encoded = Convert.ToBase64String(value.Span);
        return SecureStorage.Default.SetAsync(keyName, encoded);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string keyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        cancellationToken.ThrowIfCancellationRequested();
        var encoded = await SecureStorage.Default.GetAsync(keyName).ConfigureAwait(false);
        return encoded is null ? null : Convert.FromBase64String(encoded);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string keyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(keyName);
        return Task.CompletedTask;
    }
}
