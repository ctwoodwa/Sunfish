namespace Sunfish.Federation.CapabilitySync.Riblt;

/// <summary>
/// Outcome of a <see cref="RibltDecoder.TryDecode"/> attempt.
/// </summary>
public enum RibltDecodeOutcome
{
    /// <summary>Decoding succeeded and the remote-only / local-only sets are exact.</summary>
    Success,

    /// <summary>Decoder made partial progress but could not peel every symbol; caller should request more symbols.</summary>
    NeedMoreSymbols,

    /// <summary>Residual symbols cannot be peeled and the set state is inconsistent — caller must fall back.</summary>
    Inconsistent,
}

/// <summary>
/// Result of a decode attempt: overall outcome plus (when successful) the set differences.
/// </summary>
/// <param name="Outcome">The decode outcome.</param>
/// <param name="RemoteOnly">Items present on the remote side but missing locally.</param>
/// <param name="LocalOnly">Items present locally but missing on the remote side.</param>
public sealed record RibltDecodeResult(
    RibltDecodeOutcome Outcome,
    IReadOnlyList<RibltItem> RemoteOnly,
    IReadOnlyList<RibltItem> LocalOnly);

/// <summary>
/// Peeling decoder for RIBLT-encoded symbol streams. The caller receives <see cref="CodedSymbol"/>s
/// produced by a remote <see cref="RibltEncoder"/>; the decoder subtracts every local item's
/// contribution, then repeatedly peels any symbol that collapses to a count of ±1 until no
/// progress is possible.
/// </summary>
public static class RibltDecoder
{
    /// <summary>
    /// Attempts to decode the remote symbol set against the local item set. Returns
    /// <see cref="RibltDecodeOutcome.Success"/> when all residual symbols are fully cancelled,
    /// <see cref="RibltDecodeOutcome.NeedMoreSymbols"/> when peeling stalled with positive-count
    /// residuals, and <see cref="RibltDecodeOutcome.Inconsistent"/> only in unusual cases where
    /// zero-count residuals still carry non-zero XORs.
    /// </summary>
    public static RibltDecodeResult TryDecode(
        IReadOnlyList<CodedSymbol> remoteSymbols,
        IReadOnlyCollection<RibltItem> localItems)
    {
        ArgumentNullException.ThrowIfNull(remoteSymbols);
        ArgumentNullException.ThrowIfNull(localItems);

        var work = new CodedSymbol[remoteSymbols.Count];
        for (int i = 0; i < remoteSymbols.Count; i++) work[i] = remoteSymbols[i];

        // Subtract local items from every symbol they touch.
        foreach (var local in localItems)
        {
            for (int i = 0; i < work.Length; i++)
            {
                if (RibltEncoder.IsIncluded(local.Hash, i))
                {
                    var s = work[i];
                    work[i] = new CodedSymbol(s.HashXor ^ local.Hash, s.Count - 1, s.ChecksumXor ^ local.Checksum);
                }
            }
        }

        var remoteOnly = new List<RibltItem>();
        var localOnly = new List<RibltItem>();
        var seenHashes = new HashSet<ulong>();
        bool progress = true;
        int iter = 0;
        const int MaxIter = 4096;

        while (progress && iter++ < MaxIter)
        {
            progress = false;
            for (int i = 0; i < work.Length; i++)
            {
                var s = work[i];
                if (s.Count is not (1 or -1)) continue;

                // Ghost-peel guard: a real single-item symbol must satisfy
                // ChecksumXor == Probe(HashXor). Multi-item combinations that happen to
                // net-count ±1 will almost always fail this check.
                if (s.ChecksumXor != RibltItem.Probe(s.HashXor)) continue;

                // Skip duplicates — a peeled item can appear in multiple symbols.
                if (!seenHashes.Add(s.HashXor)) continue;

                var item = new RibltItem(s.HashXor, s.ChecksumXor);
                if (s.Count == 1) remoteOnly.Add(item); else localOnly.Add(item);
                int delta = s.Count;
                for (int j = 0; j < work.Length; j++)
                {
                    if (RibltEncoder.IsIncluded(item.Hash, j))
                    {
                        var sj = work[j];
                        work[j] = new CodedSymbol(sj.HashXor ^ item.Hash, sj.Count - delta, sj.ChecksumXor ^ item.Checksum);
                    }
                }
                progress = true;
            }
        }

        bool allZero = work.All(s => s.Count == 0 && s.HashXor == 0 && s.ChecksumXor == 0);
        if (allZero) return new RibltDecodeResult(RibltDecodeOutcome.Success, remoteOnly, localOnly);
        bool anyResidual = work.Any(s => s.Count != 0 || s.HashXor != 0 || s.ChecksumXor != 0);
        if (anyResidual) return new RibltDecodeResult(RibltDecodeOutcome.NeedMoreSymbols, remoteOnly, localOnly);
        return new RibltDecodeResult(RibltDecodeOutcome.Inconsistent, Array.Empty<RibltItem>(), Array.Empty<RibltItem>());
    }
}
