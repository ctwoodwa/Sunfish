namespace Sunfish.Federation.CapabilitySync.Protocol;

/// <summary>
/// Response to a <see cref="CapabilityFullSetDigest"/> — the receiver names the specific op nonces
/// it wants the sender to forward in a follow-up fetch.
/// </summary>
/// <param name="OpsWanted">The subset of nonces the receiver lacks and wishes to pull.</param>
public sealed record CapabilityFullSetDiff(IReadOnlyList<Guid> OpsWanted);
