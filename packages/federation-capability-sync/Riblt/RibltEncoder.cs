namespace Sunfish.Federation.CapabilitySync.Riblt;

/// <summary>
/// Encodes a set of <see cref="RibltItem"/> values into a sequence of <see cref="CodedSymbol"/>s.
/// The symbol stream is rateless: callers may request as many symbols as they need, and the
/// inclusion function deterministically selects which items contribute to each index.
/// </summary>
public sealed class RibltEncoder
{
    private readonly IReadOnlyList<RibltItem> _items;

    /// <summary>Constructs an encoder over the given item set (the sequence is materialised once).</summary>
    public RibltEncoder(IEnumerable<RibltItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToList();
    }

    /// <summary>Returns the single coded symbol at <paramref name="index"/>.</summary>
    public CodedSymbol Symbol(int index)
    {
        ulong hashXor = 0, checksumXor = 0;
        int count = 0;
        foreach (var item in _items)
        {
            if (IsIncluded(item.Hash, index))
            {
                hashXor ^= item.Hash;
                checksumXor ^= item.Checksum;
                count++;
            }
        }
        return new CodedSymbol(hashXor, count, checksumXor);
    }

    /// <summary>Returns <paramref name="count"/> contiguous symbols starting at <paramref name="startIndex"/>.</summary>
    public IReadOnlyList<CodedSymbol> Batch(int startIndex, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        var arr = new CodedSymbol[count];
        for (int i = 0; i < count; i++) arr[i] = Symbol(startIndex + i);
        return arr;
    }

    /// <summary>
    /// Deterministic inclusion function — given an item hash and a symbol index, returns whether
    /// that item contributes to that symbol. The density schedule starts dense (every item
    /// touches index 0) and thins out with higher indices so the decoder can peel outlier
    /// items as the symbol budget grows.
    /// </summary>
    internal static bool IsIncluded(ulong itemHash, int symbolIndex)
    {
        unchecked
        {
            ulong mix = itemHash ^ ((ulong)symbolIndex + 0x9E3779B97F4A7C15UL);
            mix = (mix ^ (mix >> 30)) * 0xBF58476D1CE4E5B9UL;
            mix = (mix ^ (mix >> 27)) * 0x94D049BB133111EBUL;
            mix ^= mix >> 31;
            int density = 1 + (symbolIndex / 4);
            return (mix % (ulong)density) == 0;
        }
    }
}
