using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Opaque identifier for a <see cref="MaintenanceRequest"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(MaintenanceRequestIdJsonConverter))]
public readonly record struct MaintenanceRequestId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator MaintenanceRequestId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(MaintenanceRequestId id) => id.Value;

    /// <summary>Creates a new <see cref="MaintenanceRequestId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static MaintenanceRequestId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class MaintenanceRequestIdJsonConverter : JsonConverter<MaintenanceRequestId>
{
    public override MaintenanceRequestId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("MaintenanceRequestId must be a non-null string.");
        return new MaintenanceRequestId(str);
    }

    public override void Write(Utf8JsonWriter writer, MaintenanceRequestId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
