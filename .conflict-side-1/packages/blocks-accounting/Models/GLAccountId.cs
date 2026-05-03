using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Accounting.Models;

/// <summary>Opaque identifier for a <see cref="GLAccount"/>.</summary>
[JsonConverter(typeof(GLAccountIdJsonConverter))]
public readonly record struct GLAccountId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator GLAccountId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(GLAccountId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static GLAccountId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class GLAccountIdJsonConverter : JsonConverter<GLAccountId>
{
    public override GLAccountId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("GLAccountId must be a non-null string.");
        return new GLAccountId(str);
    }

    public override void Write(Utf8JsonWriter writer, GLAccountId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
