namespace Sunfish.Foundation.Recovery;

/// <summary>
/// Default <see cref="IRecoveryClock"/> backed by
/// <see cref="DateTimeOffset.UtcNow"/>.
/// </summary>
public sealed class SystemRecoveryClock : IRecoveryClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;
}
