namespace Sunfish.Foundation.LocalFirst.Encryption;

/// <summary>
/// Linux libsecret-backed keystore. Stub — the actual libsecret / DBus
/// integration is scheduled for Wave 2 (mobile / desktop packaging). Until
/// then, all operations throw <see cref="PlatformNotSupportedException"/>.
/// </summary>
public sealed class LinuxLibsecretKeystore : IKeystore
{
    private const string NotImplementedMessage =
        "Linux libsecret integration scheduled for Wave 2 mobile / desktop packaging.";

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
