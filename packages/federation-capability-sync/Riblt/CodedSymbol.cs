namespace Sunfish.Federation.CapabilitySync.Riblt;

/// <summary>
/// A single Rateless Invertible Bloom Lookup Table (RIBLT) coded symbol. Each symbol aggregates
/// a XOR-sum of item hashes, a signed item count, and a XOR-sum of item checksums across the
/// subset of items that the (deterministic) inclusion function maps to this symbol index.
/// </summary>
/// <remarks>
/// Symbols are combined by the encoder via XOR of per-item contributions. The decoder peels
/// symbols whose count collapses to ±1 (i.e. a unique item leaked into that symbol alone).
/// </remarks>
/// <param name="HashXor">XOR of item hashes currently aggregated in this symbol.</param>
/// <param name="Count">Net count of items (positive = remote contributions, negative = local contributions after subtraction).</param>
/// <param name="ChecksumXor">XOR of item checksums currently aggregated in this symbol.</param>
public readonly record struct CodedSymbol(ulong HashXor, int Count, ulong ChecksumXor);
