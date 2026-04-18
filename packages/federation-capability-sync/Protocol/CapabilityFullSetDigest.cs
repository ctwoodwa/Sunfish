namespace Sunfish.Federation.CapabilitySync.Protocol;

/// <summary>
/// Full-set fallback payload — when RIBLT decoding cannot converge within the agreed symbol budget,
/// the sender enumerates every op nonce it holds. The receiver computes the plain set difference
/// and requests the missing ops individually.
/// </summary>
/// <param name="NonceIds">Every op nonce the sender holds.</param>
public sealed record CapabilityFullSetDigest(IReadOnlyList<Guid> NonceIds);
