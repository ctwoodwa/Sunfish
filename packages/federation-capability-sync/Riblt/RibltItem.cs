namespace Sunfish.Federation.CapabilitySync.Riblt;

/// <summary>
/// A single item that participates in RIBLT set reconciliation. <see cref="Hash"/> is the
/// identity-equality key (used by the inclusion function to decide which symbols to touch) and
/// <see cref="Checksum"/> is a deterministic companion hash derived from <see cref="Hash"/>.
/// </summary>
/// <remarks>
/// The invariant <c>Checksum == Probe(Hash)</c> gives the decoder a ghost-peel guard: when a
/// symbol collapses to <c>count = ±1</c>, the decoder verifies <c>symbol.ChecksumXor ==
/// Probe(symbol.HashXor)</c> before treating it as a real item. A multi-item combination that
/// happens to sum to a net count of ±1 will almost certainly fail this probe check and be
/// rejected as "not a single item yet."
/// </remarks>
/// <param name="Hash">A 64-bit identity-equality key for the item.</param>
/// <param name="Checksum">The companion probe <c>Probe(Hash)</c>, used for single-item validation during peeling.</param>
public readonly record struct RibltItem(ulong Hash, ulong Checksum)
{
    /// <summary>
    /// Derives a <see cref="RibltItem"/> from an identity digest (e.g. folded op nonce XOR payload digest).
    /// The companion checksum is computed deterministically via <see cref="Probe"/>.
    /// </summary>
    /// <param name="identity">A 64-bit identity digest for the item.</param>
    public static RibltItem FromIdentity(ulong identity)
        => new(identity, Probe(identity));

    /// <summary>
    /// Derives a <see cref="RibltItem"/> from a GUID nonce and a pre-computed payload digest.
    /// Both contribute to the identity hash; the checksum is derived from that hash.
    /// </summary>
    public static RibltItem From(Guid nonce, ulong payloadDigest)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!nonce.TryWriteBytes(bytes))
            throw new InvalidOperationException("Failed to write Guid bytes.");
        var low = BitConverter.ToUInt64(bytes[..8]);
        var high = BitConverter.ToUInt64(bytes.Slice(8, 8));
        var identity = low ^ high ^ payloadDigest;
        return new RibltItem(identity, Probe(identity));
    }

    /// <summary>
    /// Deterministic companion hash used as the ghost-peel probe. Any stable 64-bit
    /// well-mixed function of <paramref name="hash"/> distinct from the identity works;
    /// we use a SplitMix64-style mixer with a different constant than the inclusion function.
    /// </summary>
    public static ulong Probe(ulong hash)
    {
        unchecked
        {
            ulong mix = hash + 0xC2B2AE3D27D4EB4FUL;
            mix = (mix ^ (mix >> 33)) * 0xFF51AFD7ED558CCDUL;
            mix = (mix ^ (mix >> 33)) * 0xC4CEB9FE1A85EC53UL;
            mix ^= mix >> 33;
            return mix;
        }
    }
}
