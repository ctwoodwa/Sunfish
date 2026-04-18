using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Produces byte-stable canonical JSON for signing. Object keys are sorted alphabetically
/// (ordinal), array order is preserved, no whitespace, UTF-8.
/// </summary>
/// <remarks>
/// <para>This is a pragmatic canonicalizer sufficient for <see cref="SignedOperation{T}"/> —
/// it is not a full RFC 8785 JCS. Logically-equal objects with different key orderings produce
/// identical byte sequences.</para>
/// <para>Related but distinct: <see cref="Sunfish.Foundation.Assets.Common.JsonCanonicalizer"/>
/// operates on <c>JsonDocument</c> for the Assets hash chain; this class operates on arbitrary
/// CLR objects via <see cref="JsonSerializer"/> + <see cref="JsonNode"/> and adds the
/// <see cref="SerializeSignable"/> envelope helper. Both share the same canonicalization
/// rules (alphabetical key sort, no whitespace, UTF-8).</para>
/// </remarks>
public static class CanonicalJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        SkipValidation = true,
    };

    /// <summary>Serializes an arbitrary CLR value to canonical-JSON UTF-8 bytes.</summary>
    public static byte[] Serialize<T>(T value)
    {
        var node = JsonSerializer.SerializeToNode(value, SerializerOptions);
        var sorted = SortKeys(node);
        return NodeToBytes(sorted);
    }

    /// <summary>
    /// Builds the signable envelope <c>{issuedAt, issuerId, nonce, payload}</c>, sorts all keys,
    /// and returns the canonical UTF-8 bytes that an Ed25519 signature covers.
    /// </summary>
    public static byte[] SerializeSignable<T>(
        T payload,
        PrincipalId issuerId,
        DateTimeOffset issuedAt,
        Guid nonce)
    {
        var payloadNode = JsonSerializer.SerializeToNode(payload, SerializerOptions);

        var envelope = new JsonObject
        {
            ["issuedAt"] = JsonValue.Create(issuedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
            ["issuerId"] = JsonValue.Create(issuerId.ToBase64Url()),
            ["nonce"] = JsonValue.Create(nonce.ToString("D")),
            ["payload"] = payloadNode,
        };

        var sorted = SortKeys(envelope);
        return NodeToBytes(sorted);
    }

    /// <summary>
    /// Recursively produces a new node tree with object keys sorted alphabetically (ordinal).
    /// Array order is preserved. Scalars are returned as deep clones.
    /// </summary>
    internal static JsonNode? SortKeys(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;

            case JsonObject obj:
                {
                    var sorted = new JsonObject();
                    foreach (var kvp in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        // DeepClone detaches the child from its current parent so we can re-parent it.
                        sorted[kvp.Key] = SortKeys(kvp.Value?.DeepClone());
                    }
                    return sorted;
                }

            case JsonArray arr:
                {
                    var sortedArr = new JsonArray();
                    foreach (var item in arr)
                        sortedArr.Add(SortKeys(item?.DeepClone()));
                    return sortedArr;
                }

            default:
                // Value node — clone to detach from any parent.
                return node.DeepClone();
        }
    }

    private static byte[] NodeToBytes(JsonNode? node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            if (node is null)
                writer.WriteNullValue();
            else
                node.WriteTo(writer);
        }
        return stream.ToArray();
    }
}
