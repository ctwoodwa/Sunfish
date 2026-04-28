using System.Text.Json;
using Sunfish.Blocks.Properties.Models;
using Xunit;

namespace Sunfish.Blocks.Properties.Tests;

public class PropertyIdTests
{
    [Fact]
    public void NewId_returns_non_empty_value()
    {
        var id = PropertyId.NewId();
        Assert.False(string.IsNullOrWhiteSpace(id.Value));
    }

    [Fact]
    public void NewId_returns_unique_values()
    {
        var a = PropertyId.NewId();
        var b = PropertyId.NewId();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Implicit_string_round_trip_preserves_value()
    {
        var raw = "prop-123";
        PropertyId id = raw;
        string back = id;
        Assert.Equal(raw, back);
    }

    [Fact]
    public void JsonConverter_round_trips_as_string()
    {
        var id = new PropertyId("prop-abc");
        var json = JsonSerializer.Serialize(id);
        Assert.Equal("\"prop-abc\"", json);

        var roundTripped = JsonSerializer.Deserialize<PropertyId>(json);
        Assert.Equal(id, roundTripped);
    }

    [Fact]
    public void JsonConverter_throws_on_null()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PropertyId>("null"));
    }
}
