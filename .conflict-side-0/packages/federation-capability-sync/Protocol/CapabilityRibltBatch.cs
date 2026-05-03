using Sunfish.Federation.CapabilitySync.Riblt;

namespace Sunfish.Federation.CapabilitySync.Protocol;

/// <summary>
/// A follow-up RIBLT symbol batch requested by the receiver when decoding stalled with
/// <see cref="RibltDecodeOutcome.NeedMoreSymbols"/>. Batches are contiguous in the rateless symbol stream.
/// </summary>
/// <param name="StartIndex">The index of the first symbol in this batch within the overall stream.</param>
/// <param name="Symbols">The coded symbols for indices <c>[StartIndex, StartIndex + Symbols.Count)</c>.</param>
public sealed record CapabilityRibltBatch(
    int StartIndex,
    IReadOnlyList<CodedSymbol> Symbols);
