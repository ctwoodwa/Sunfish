using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Sunfish.Foundation.Assets.Common;

/// <summary>
/// Produces byte-stable canonical JSON for hashing purposes.
/// </summary>
/// <remarks>
/// <para>
/// Algorithm: object keys sorted lexicographically (ordinal), arrays preserve order,
/// no whitespace, UTF-8 output. Logically equal JSON values produce identical byte
/// sequences irrespective of input key order or formatting.
/// </para>
/// <para>
/// Note: this is a pragmatic canonicalizer sufficient for Sunfish's in-repo hash chain
/// — it is not RFC 8785 JCS. A full JCS implementation is a follow-up if cross-stack
/// interoperability is later required.
/// </para>
/// </remarks>
public static class JsonCanonicalizer
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        SkipValidation = true,
    };

    /// <summary>Produces canonical UTF-8 bytes for the given JSON document.</summary>
    public static byte[] ToCanonicalBytes(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            WriteCanonical(writer, document.RootElement);
        }
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Convenience: canonical UTF-8 string for the given JSON document.</summary>
    public static string ToCanonicalString(JsonDocument document)
        => Encoding.UTF8.GetString(ToCanonicalBytes(document));

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                // Sort keys lexicographically by ordinal string comparison.
                var properties = new List<JsonProperty>();
                foreach (var property in element.EnumerateObject())
                    properties.Add(property);
                properties.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
                foreach (var property in properties)
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                // Preserve the original text form so 1, 1.0, 1e0 all round-trip as-written.
                // This matches Postgres jsonb "number preserves input" semantics closely enough
                // for Phase A hashing.
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            case JsonValueKind.Undefined:
            default:
                writer.WriteNullValue();
                break;
        }
    }
}
