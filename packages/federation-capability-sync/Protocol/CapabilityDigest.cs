using Sunfish.Federation.CapabilitySync.Riblt;

namespace Sunfish.Federation.CapabilitySync.Protocol;

/// <summary>
/// Initial sync payload — announces the local op-count and the first batch of RIBLT coded symbols
/// so the receiver can attempt a fast-path reconciliation.
/// </summary>
/// <remarks>
/// Wire-format serialization is deferred to a follow-up task (D-4-http). These records define the
/// shape of the protocol; the in-memory syncer passes them by reference and never serializes.
/// </remarks>
/// <param name="LocalOpCount">The number of signed capability ops held by the sender.</param>
/// <param name="InitialSymbols">The first batch of coded symbols encoding the sender's op set.</param>
/// <param name="SymbolBatchSize">The symbol-batch size the sender will use for subsequent batches.</param>
public sealed record CapabilityDigest(
    int LocalOpCount,
    IReadOnlyList<CodedSymbol> InitialSymbols,
    int SymbolBatchSize);
