using System.Text.Json;
using Sunfish.Blocks.PropertyAssets.Models;
using Xunit;

namespace Sunfish.Blocks.PropertyAssets.Tests;

public class AssetIdTests
{
    [Fact]
    public void NewId_returns_non_empty_value()
    {
        var id = AssetId.NewId();
        Assert.False(string.IsNullOrWhiteSpace(id.Value));
    }

    [Fact]
    public void NewId_returns_unique_values()
    {
        Assert.NotEqual(AssetId.NewId(), AssetId.NewId());
    }

    [Fact]
    public void Implicit_string_round_trip_preserves_value()
    {
        var raw = "asset-123";
        AssetId id = raw;
        string back = id;
        Assert.Equal(raw, back);
    }

    [Fact]
    public void JsonConverter_round_trips_as_string()
    {
        var id = new AssetId("asset-abc");
        var json = JsonSerializer.Serialize(id);
        Assert.Equal("\"asset-abc\"", json);
        Assert.Equal(id, JsonSerializer.Deserialize<AssetId>(json));
    }

    [Fact]
    public void JsonConverter_throws_on_null()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AssetId>("null"));
    }
}
