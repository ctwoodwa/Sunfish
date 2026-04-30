using System.Text.Json.Nodes;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Kernel.Signatures.Canonicalization;

/// <summary>
/// JSON canonicalizer producing byte-stable output from arbitrary CLR
/// values, <see cref="JsonNode"/> trees, or
/// <see cref="IReadOnlyDictionary{TKey,TValue}"/> dictionaries.
/// Delegates to <see cref="CanonicalJson"/> in Foundation.Crypto, which
/// implements the alphabetically-sorted-keys / no-whitespace / UTF-8
/// rules. ADR 0054 amendment A1 pins this scheme as the canonical-bytes
/// rule for JSON documents.
/// </summary>
/// <remarks>
/// <para>
/// <b>RFC 8785 conformance note:</b> <see cref="CanonicalJson"/> is a
/// pragmatic canonicalizer that satisfies the JCS rules for object key
/// ordering + array order preservation + whitespace stripping. Number
/// serialization differences (e.g., trailing zeros, scientific
/// notation) are handled by <c>System.Text.Json</c>'s default writer
/// which is sufficient for Sunfish's signing surface; if a future use
/// case requires strict RFC 8785 number serialization, the
/// canonicalizer here is the swap point.
/// </para>
/// </remarks>
public sealed class JsonCanonicalCanonicalizer : IContentCanonicalizer
{
    /// <inheritdoc />
    public string CanonicalizationKind => "json-canonical/rfc-8785-pragmatic";

    /// <inheritdoc />
    public byte[] Canonicalize(object content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return content switch
        {
            JsonNode node => CanonicalJson.Serialize(node),
            // Anything else: route through the generic Serialize<T> which
            // covers IReadOnlyDictionary + arbitrary CLR types via runtime
            // type discovery + key sort.
            _ => CanonicalJson.Serialize(content),
        };
    }
}
