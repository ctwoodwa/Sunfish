using System.Text.Json;
using Sunfish.Blocks.PropertyEquipment.Models;
using Xunit;

namespace Sunfish.Blocks.PropertyEquipment.Tests;

public class WarrantyMetadataTests
{
    [Fact]
    public void Json_round_trip_preserves_required_and_optional_fields()
    {
        var w = new WarrantyMetadata
        {
            StartsAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2030, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Provider = "Best Buy Geek Squad",
            PolicyNumber = "GS-12345",
            CoverageNotes = "Parts and labour",
        };

        var json = JsonSerializer.Serialize(w);
        Assert.Equal(w, JsonSerializer.Deserialize<WarrantyMetadata>(json));
    }
}
