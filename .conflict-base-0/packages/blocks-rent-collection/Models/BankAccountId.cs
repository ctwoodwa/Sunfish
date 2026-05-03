using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>Opaque identifier for a <see cref="BankAccount"/>.</summary>
[JsonConverter(typeof(BankAccountIdJsonConverter))]
public readonly record struct BankAccountId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator BankAccountId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(BankAccountId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static BankAccountId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class BankAccountIdJsonConverter : JsonConverter<BankAccountId>
{
    public override BankAccountId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("BankAccountId must be a non-null string.");
        return new BankAccountId(str);
    }

    public override void Write(Utf8JsonWriter writer, BankAccountId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
