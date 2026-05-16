using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// Opaque identifier for a user-defined classification (project /
/// department / fund / cost-pool / etc.) — second dimensional tag on
/// <see cref="JournalEntryLine.ClassId"/>. The classification entity
/// itself ships in <c>blocks-financial-periods</c> (or a future
/// <c>blocks-financial-classifications</c>); this ID type is hosted
/// here so the journal-line FK does not create a reverse cluster
/// dependency.
/// </summary>
/// <remarks>
/// Hand-off names this `ClassId` short-form; schema design uses the
/// fully-qualified `ClassificationId`. Following the schema spec to
/// reduce ambiguity.
/// </remarks>
[JsonConverter(typeof(ClassificationIdJsonConverter))]
public readonly record struct ClassificationId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator ClassificationId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(ClassificationId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static ClassificationId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class ClassificationIdJsonConverter : JsonConverter<ClassificationId>
{
    public override ClassificationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ClassificationId must be a non-null string.");
        return new ClassificationId(str);
    }

    public override void Write(Utf8JsonWriter writer, ClassificationId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
