using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sunfish.Blocks.TaxReporting.Models;

namespace Sunfish.Blocks.TaxReporting.Services;

/// <summary>
/// Produces a canonical (deterministic) JSON serialization of a <see cref="TaxReportBody"/>
/// and computes a SHA-256 content hash over it.
/// </summary>
/// <remarks>
/// <para>
/// Canonical form: compact JSON (no whitespace) with properties emitted in a stable,
/// alphabetically-sorted order. Achieved via a two-pass approach: serialize with
/// <see cref="System.Text.Json"/>, then deserialize into a <see cref="SortedDictionary{TKey,TValue}"/>
/// tree, then re-serialize. This guarantees the same bytes for the same logical content
/// regardless of property declaration order in the CLR types.
/// </para>
/// <para>
/// <strong>This is NOT an Ed25519 digital signature.</strong> The <c>SignatureValue</c> field
/// on <see cref="TaxReport"/> is a SHA-256 hex string over the canonical bytes — it proves
/// content integrity but not authorship. Real digital signing (Ed25519 via Foundation's
/// <c>PrincipalId</c> + private-key facility) is a future pass.
/// TODO: integrate with Foundation signing primitives when they land.
/// </para>
/// </remarks>
public static class TaxReportCanonicalJson
{
    private static readonly JsonSerializerOptions s_writeOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Serializes <paramref name="body"/> to canonical UTF-8 JSON bytes (compact, sorted properties).
    /// The same body always produces the same bytes.
    /// </summary>
    public static byte[] Compute(TaxReportBody body)
    {
        // First pass: serialize to JSON using standard STJ (picks up the existing
        // EntityId/TaxReportId JsonConverters registered on the types).
        var firstPassJson = JsonSerializer.Serialize(body, body.GetType(), s_writeOptions);

        // Second pass: parse into a generic element tree and re-serialize with sorted keys.
        using var doc = JsonDocument.Parse(firstPassJson);
        var sortedObject = SortElement(doc.RootElement);
        return JsonSerializer.SerializeToUtf8Bytes(sortedObject, s_writeOptions);
    }

    /// <summary>
    /// Computes the SHA-256 hex string over the canonical JSON bytes of <paramref name="body"/>.
    /// </summary>
    public static string ComputeHash(TaxReportBody body)
    {
        var bytes = Compute(body);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // -----------------------------------------------------------------------
    // Internals
    // -----------------------------------------------------------------------

    /// <summary>
    /// Recursively converts a <see cref="JsonElement"/> into a plain CLR object tree
    /// with <see cref="SortedDictionary{TKey,TValue}"/> for objects (guaranteeing
    /// alphabetic property ordering) and <see cref="List{T}"/> for arrays.
    /// </summary>
    private static object? SortElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => SortObject(element),
            JsonValueKind.Array  => element.EnumerateArray()
                                           .Select(SortElement)
                                           .ToList(),
            JsonValueKind.String  => element.GetString(),
            JsonValueKind.Number  => TryGetInt64OrDouble(element),
            JsonValueKind.True    => (object)true,
            JsonValueKind.False   => (object)false,
            JsonValueKind.Null    => null,
            _ => throw new JsonException($"Unexpected JsonValueKind: {element.ValueKind}"),
        };
    }

    private static object TryGetInt64OrDouble(JsonElement element)
    {
        if (element.TryGetInt64(out var l))
            return l;
        // Decimal values are serialized as JSON numbers; preserve precision as string
        // during the sort pass so we don't lose decimal fidelity.
        // GetRawText() returns the exact string the serializer wrote (e.g. "1234.56").
        return element.GetDecimal();
    }

    private static SortedDictionary<string, object?> SortObject(JsonElement element)
    {
        var dict = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in element.EnumerateObject())
            dict[prop.Name] = SortElement(prop.Value);
        return dict;
    }
}
