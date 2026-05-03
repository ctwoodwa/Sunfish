using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Sunfish.Foundation.Recovery;

/// <summary>
/// Phase 1 G6 sub-pattern <b>#48c (paper-key fallback)</b> per ADR 0046 —
/// deterministic conversion between Sunfish's 32-byte root seed and a 24-word
/// human-readable phrase using the standard BIP-39 English wordlist.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this is.</b> The Sunfish keystore stores a 32-byte root seed
/// (paper §11.2; <c>KeystoreRootSeedProvider</c>) from which all per-team
/// SqlCipher keys, role keys, and the install's Ed25519 identity are
/// derived. Losing access to the keystore means losing access to every
/// team's data — unless the user has previously printed a paper-key copy
/// of the root seed in a portable format. The BIP-39 24-word phrase is
/// that portable format.
/// </para>
/// <para>
/// <b>Why BIP-39.</b> ADR 0046 selected BIP-39 (Bitcoin Improvement
/// Proposal 39) because the format is well-known, lossy hand-copying
/// errors are detectable via the built-in checksum byte, and the
/// 2048-word English list is unambiguous when written down (no two
/// words share a four-letter prefix per the BIP-39 design constraint).
/// 256-bit entropy yields exactly 24 words — the canonical BIP-39 length
/// for a "fully secure" mnemonic.
/// </para>
/// <para>
/// <b>What this is NOT.</b> This is purely a 32-byte ↔ 24-word codec.
/// It does not derive a passphrase-protected wallet (BIP-39's optional
/// passphrase + PBKDF2 step) — Sunfish's keystore handles passphrase
/// hardening separately via Argon2id (<c>SqlCipherKeyDerivation</c>),
/// so the root seed inside the keystore is the right unit to encode
/// here. It does not interact with the trustee/social-recovery flow
/// (#48a + #48e) — that's a separate code path with its own audit
/// trail (#48f). Paper-key recovery skips trustees entirely; the holder
/// of the paper key IS the legitimate owner per ADR 0046's threat model.
/// </para>
/// <para>
/// <b>Algorithm (BIP-39 spec, §"Generating the mnemonic").</b>
/// <list type="number">
///   <item>Take 32 bytes (256 bits) of entropy = the root seed.</item>
///   <item>Compute SHA-256 of the entropy.</item>
///   <item>Take the first (256/32) = 8 bits of the SHA-256 as checksum.</item>
///   <item>Concatenate entropy ‖ checksum = 264 bits.</item>
///   <item>Split into 24 groups of 11 bits; each group is an index into the wordlist.</item>
/// </list>
/// Recovery reverses these steps and validates the checksum byte.
/// </para>
/// </remarks>
public static class PaperKeyDerivation
{
    /// <summary>BIP-39 wordlist size (2^11).</summary>
    public const int WordlistSize = 2048;

    /// <summary>Number of words for a 256-bit (32-byte) entropy.</summary>
    public const int MnemonicWordCount = 24;

    /// <summary>Required entropy length in bytes for <see cref="MnemonicWordCount"/> words.</summary>
    public const int EntropyByteLength = 32;

    private static readonly Lazy<string[]> EnglishWordlist = new(LoadEnglishWordlist);
    private static readonly Lazy<Dictionary<string, int>> EnglishWordIndex = new(BuildWordIndex);

    /// <summary>
    /// Encode a 32-byte root seed as a 24-word BIP-39 mnemonic phrase.
    /// </summary>
    /// <param name="rootSeed">Sunfish root seed (32 bytes). Must not be empty.</param>
    /// <returns>Space-separated 24-word mnemonic, all lowercase.</returns>
    /// <exception cref="ArgumentException">If <paramref name="rootSeed"/> is not exactly 32 bytes.</exception>
    public static string ToMnemonic(ReadOnlySpan<byte> rootSeed)
    {
        if (rootSeed.Length != EntropyByteLength)
        {
            throw new ArgumentException(
                $"Sunfish root seed must be exactly {EntropyByteLength} bytes; got {rootSeed.Length}.",
                nameof(rootSeed));
        }

        // Build a 264-bit working buffer: 256 bits entropy + 8 bits checksum.
        Span<byte> working = stackalloc byte[EntropyByteLength + 1];
        rootSeed.CopyTo(working);

        // BIP-39 checksum = first 8 bits (entropy_bits / 32) of SHA-256(entropy).
        Span<byte> checksum = stackalloc byte[32];
        SHA256.HashData(rootSeed, checksum);
        working[EntropyByteLength] = checksum[0];

        // Split into 24 11-bit groups. Each group indexes the wordlist.
        var wordlist = EnglishWordlist.Value;
        var sb = new StringBuilder(capacity: MnemonicWordCount * 8);
        for (var wordIndex = 0; wordIndex < MnemonicWordCount; wordIndex++)
        {
            var bitOffset = wordIndex * 11;
            var index = ExtractElevenBits(working, bitOffset);
            if (wordIndex > 0) sb.Append(' ');
            sb.Append(wordlist[index]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Decode a 24-word BIP-39 mnemonic phrase back to its 32-byte root seed,
    /// validating the checksum byte.
    /// </summary>
    /// <param name="mnemonic">Space-separated 24-word phrase. Whitespace and case are normalized.</param>
    /// <returns>Recovered 32-byte root seed.</returns>
    /// <exception cref="ArgumentException">If <paramref name="mnemonic"/> is null/empty.</exception>
    /// <exception cref="FormatException">If the phrase has wrong word count, contains an unknown word,
    /// or the checksum byte does not match the entropy's SHA-256 prefix.</exception>
    public static byte[] FromMnemonic(string mnemonic)
    {
        if (string.IsNullOrWhiteSpace(mnemonic))
        {
            throw new ArgumentException("Mnemonic must be a non-empty 24-word phrase.", nameof(mnemonic));
        }

        var words = mnemonic
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.ToLowerInvariant())
            .ToArray();

        if (words.Length != MnemonicWordCount)
        {
            throw new FormatException(
                $"Sunfish paper-key mnemonic must be exactly {MnemonicWordCount} words; got {words.Length}.");
        }

        var index = EnglishWordIndex.Value;
        var working = new byte[EntropyByteLength + 1];
        for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
        {
            if (!index.TryGetValue(words[wordIndex], out var wordValue))
            {
                throw new FormatException(
                    $"Mnemonic word {wordIndex + 1} ('{words[wordIndex]}') is not in the BIP-39 English wordlist.");
            }
            WriteElevenBits(working, wordIndex * 11, wordValue);
        }

        var entropy = new byte[EntropyByteLength];
        Array.Copy(working, 0, entropy, 0, EntropyByteLength);

        Span<byte> expectedChecksum = stackalloc byte[32];
        SHA256.HashData(entropy, expectedChecksum);
        if (expectedChecksum[0] != working[EntropyByteLength])
        {
            throw new FormatException(
                "Mnemonic checksum byte does not match the entropy's SHA-256 prefix. " +
                "The phrase is either mistyped or not a valid Sunfish paper key.");
        }

        return entropy;
    }

    /// <summary>
    /// Extract 11 bits from <paramref name="buffer"/> starting at <paramref name="bitOffset"/>,
    /// big-endian. Returns the bits packed into the low 11 bits of a 32-bit int.
    /// </summary>
    private static int ExtractElevenBits(ReadOnlySpan<byte> buffer, int bitOffset)
    {
        // 11 bits straddle at most two bytes. Compute the byte index of the first bit
        // and shift to align so the 11 bits land in the low 11 of a 24-bit working value.
        var byteIndex = bitOffset / 8;
        var bitOffsetInByte = bitOffset % 8;
        // Read up to 3 bytes to cover the 11-bit window.
        int b0 = buffer[byteIndex];
        int b1 = byteIndex + 1 < buffer.Length ? buffer[byteIndex + 1] : 0;
        int b2 = byteIndex + 2 < buffer.Length ? buffer[byteIndex + 2] : 0;
        var combined = (b0 << 16) | (b1 << 8) | b2;
        // The 11-bit group occupies bits [bitOffsetInByte .. bitOffsetInByte+10] of the combined 24-bit value
        // counting from the high end. Shift right to align low, then mask.
        var shift = 24 - bitOffsetInByte - 11;
        return (combined >> shift) & 0x7FF;
    }

    /// <summary>
    /// Write the low 11 bits of <paramref name="value"/> into <paramref name="buffer"/>
    /// starting at <paramref name="bitOffset"/>, big-endian. Bits are OR-ed in;
    /// caller ensures the buffer is initially zero so writes don't collide.
    /// </summary>
    private static void WriteElevenBits(byte[] buffer, int bitOffset, int value)
    {
        var byteIndex = bitOffset / 8;
        var bitOffsetInByte = bitOffset % 8;
        // Place the 11-bit value into a 24-bit working int, shifted so its high bit lands at bitOffsetInByte
        // of the high byte.
        var shift = 24 - bitOffsetInByte - 11;
        var combined = (value & 0x7FF) << shift;
        buffer[byteIndex] |= (byte)((combined >> 16) & 0xFF);
        if (byteIndex + 1 < buffer.Length)
        {
            buffer[byteIndex + 1] |= (byte)((combined >> 8) & 0xFF);
        }
        if (byteIndex + 2 < buffer.Length)
        {
            buffer[byteIndex + 2] |= (byte)(combined & 0xFF);
        }
    }

    private static string[] LoadEnglishWordlist()
    {
        var assembly = typeof(PaperKeyDerivation).Assembly;
        // Embedded as Sunfish.Foundation.Recovery.bip39-english.txt
        var resourceName = $"{typeof(PaperKeyDerivation).Namespace}.bip39-english.txt";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded BIP-39 wordlist resource '{resourceName}' not found in assembly. " +
                "Verify the .csproj includes the resource as <EmbeddedResource>.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var words = new List<string>(WordlistSize);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0) words.Add(trimmed);
        }
        if (words.Count != WordlistSize)
        {
            throw new InvalidOperationException(
                $"Embedded BIP-39 wordlist has {words.Count} words; expected {WordlistSize}.");
        }
        return words.ToArray();
    }

    private static Dictionary<string, int> BuildWordIndex()
    {
        var list = EnglishWordlist.Value;
        var dict = new Dictionary<string, int>(WordlistSize, StringComparer.Ordinal);
        for (var i = 0; i < list.Length; i++)
        {
            dict[list[i]] = i;
        }
        return dict;
    }
}
