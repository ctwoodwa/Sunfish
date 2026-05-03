namespace Sunfish.Foundation.LocalFirst.Encryption;

/// <summary>
/// macOS Keychain-backed keystore. Stub — the actual Security.framework P/Invoke
/// integration is scheduled for Wave 2 (mobile / desktop packaging). Until then,
/// all operations throw <see cref="PlatformNotSupportedException"/>.
/// </summary>
public sealed class MacOsKeychainKeystore : IKeystore
{
    private const string NotImplementedMessage =
        "Mac keychain integration scheduled for Wave 2 mobile / desktop packaging.";

    /// <inheritdoc />
    public Task<ReadOnlyMemory<byte>?> GetKeyAsync(string name, CancellationToken ct)
        => throw new PlatformNotSupportedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task SetKeyAsync(string name, ReadOnlyMemory<byte> key, CancellationToken ct)
        => throw new PlatformNotSupportedException(NotImplementedMessage);

    /// <inheritdoc />
    public Task DeleteKeyAsync(string name, CancellationToken ct)
        => throw new PlatformNotSupportedException(NotImplementedMessage);
}
