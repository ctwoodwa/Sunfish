using System.Text.Json;
using Sunfish.Blocks.PropertyEquipment.Models;
using Xunit;

namespace Sunfish.Blocks.PropertyEquipment.Tests;

public class EquipmentIdTests
{
    [Fact]
    public void NewId_returns_non_empty_value()
    {
        var id = EquipmentId.NewId();
        Assert.False(string.IsNullOrWhiteSpace(id.Value));
    }

    [Fact]
    public void NewId_returns_unique_values()
    {
        Assert.NotEqual(EquipmentId.NewId(), EquipmentId.NewId());
    }

    [Fact]
    public void Implicit_string_round_trip_preserves_value()
    {
        var raw = "equipment-123";
        EquipmentId id = raw;
        string back = id;
        Assert.Equal(raw, back);
    }

    [Fact]
    public void JsonConverter_round_trips_as_string()
    {
        var id = new EquipmentId("equipment-abc");
        var json = JsonSerializer.Serialize(id);
        Assert.Equal("\"equipment-abc\"", json);
        Assert.Equal(id, JsonSerializer.Deserialize<EquipmentId>(json));
    }

    [Fact]
    public void JsonConverter_throws_on_null()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EquipmentId>("null"));
    }
}
