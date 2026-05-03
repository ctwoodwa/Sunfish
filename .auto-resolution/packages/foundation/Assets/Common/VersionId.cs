using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Assets.Common;

/// <summary>
/// Identifies a single version of an entity.
/// </summary>
/// <remarks>
/// <para>
/// Composed of <see cref="Entity"/>, an append-only <see cref="Sequence"/> (starting at 1),
/// and a <see cref="Hash"/> that chains to the previous version's hash. The hash is
/// deterministic over (parent-hash || canonical(body) || ISO-8601 valid-from), using
/// SHA-256; see spec §3.2 and plan D-CRDT-ROUTE.
/// </para>
/// <para>
/// <c>ToString()</c> renders a short form suitable for logs:
/// <c>"{entity}@{sequence}:{hash[..12]}"</c>.
/// </para>
/// <para>
/// JSON wire form (via <see cref="VersionIdJsonConverter"/>):
/// <c>"{scheme}:{authority}/{localPart}@{sequence}:{fullHash}"</c> — the full hash is preserved
/// for round-trip correctness.
/// </para>
/// </remarks>
[JsonConverter(typeof(VersionIdJsonConverter))]
public readonly record struct VersionId(EntityId Entity, int Sequence, string Hash)
{
    /// <inheritdoc />
    public override string ToString()
    {
        var shortHash = Hash.Length >= 12 ? Hash[..12] : Hash;
        return $"{Entity}@{Sequence}:{shortHash}";
    }

    /// <summary>
    /// Full canonical wire form: <c>"{entity}@{sequence}:{fullHash}"</c>.
    /// Unlike <see cref="ToString"/>, the hash is never truncated.
    /// </summary>
    public string ToWireString() => $"{Entity}@{Sequence}:{Hash}";

    /// <summary>Parses the canonical wire form produced by <see cref="ToWireString"/>.</summary>
    /// <exception cref="FormatException">Thrown when the value is malformed.</exception>
    public static VersionId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        // Format: {scheme}:{authority}/{localPart}@{sequence}:{fullHash}
        // Find the last '@' to split entity from sequence:hash
        var atIndex = value.LastIndexOf('@');
        if (atIndex <= 0 || atIndex == value.Length - 1)
            throw new FormatException($"VersionId '{value}' must be of form entity@sequence:hash.");

        var entityPart = value[..atIndex];
        var rest = value[(atIndex + 1)..];

        var colonIndex = rest.IndexOf(':');
        if (colonIndex <= 0 || colonIndex == rest.Length - 1)
            throw new FormatException($"VersionId '{value}' must be of form entity@sequence:hash.");

        if (!int.TryParse(rest[..colonIndex], out var sequence))
            throw new FormatException($"VersionId '{value}' sequence '{rest[..colonIndex]}' is not a valid integer.");

        var hash = rest[(colonIndex + 1)..];
        var entity = EntityId.Parse(entityPart);
        return new VersionId(entity, sequence, hash);
    }
}

internal sealed class VersionIdJsonConverter : JsonConverter<VersionId>
{
    public override VersionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("VersionId must be a non-null string.");
        try { return VersionId.Parse(str); }
        catch (FormatException ex) { throw new JsonException(ex.Message, ex); }
    }

    public override void Write(Utf8JsonWriter writer, VersionId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToWireString());
    }
}
