namespace Sunfish.Kernel.Runtime.Scheduling;

/// <summary>
/// Tunable knobs for <see cref="ResourceGovernor"/>. Bound via
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>.
/// </summary>
/// <remarks>
/// Default <see cref="MaxActiveRoundsPerTick"/> is 2 per ADR 0032 — high
/// enough that active teams make progress each tick, low enough that a user
/// in 4+ teams doesn't stampede the network and CPU every 30 seconds.
/// Override per deployment shape (Anchor single-user desktop vs. Bridge
/// hosted-node) when the topology justifies a different cap.
/// </remarks>
public sealed record ResourceGovernorOptions
{
    /// <summary>
    /// Maximum number of gossip rounds that may run concurrently across all
    /// teams at any instant. Must be &#x2265; 1.
    /// </summary>
    public int MaxActiveRoundsPerTick { get; init; } = 2;
}
