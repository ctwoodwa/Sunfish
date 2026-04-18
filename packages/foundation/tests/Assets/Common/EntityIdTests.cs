using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Tests.Assets.Common;

public sealed class EntityIdTests
{
    [Fact]
    public void Parse_RoundTripsToString()
    {
        var id = new EntityId("property", "acme-rentals", "42");
        Assert.Equal("property:acme-rentals/42", id.ToString());
        var parsed = EntityId.Parse("property:acme-rentals/42");
        Assert.Equal(id, parsed);
    }

    [Fact]
    public void Parse_RejectsMalformedInput()
    {
        Assert.Throws<FormatException>(() => EntityId.Parse("no-colon"));
        Assert.Throws<FormatException>(() => EntityId.Parse(":missing-scheme/local"));
        Assert.Throws<FormatException>(() => EntityId.Parse("scheme:auth/"));
        Assert.Throws<FormatException>(() => EntityId.Parse("scheme:/local"));
    }

    [Fact]
    public void TryParse_ReturnsFalse_OnMalformed()
    {
        Assert.False(EntityId.TryParse("garbage", out _));
        Assert.True(EntityId.TryParse("s:a/l", out var good));
        Assert.Equal("s:a/l", good.ToString());
    }
}
