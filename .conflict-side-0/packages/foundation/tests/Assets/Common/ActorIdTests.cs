using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Tests.Assets.Common;

public sealed class ActorIdTests
{
    [Fact]
    public void System_Sentinel_HasValueSystem()
    {
        Assert.Equal("system", ActorId.System.Value);
    }

    [Fact]
    public void Sunfish_Sentinel_HasValueSunfish()
    {
        Assert.Equal("sunfish", ActorId.Sunfish.Value);
    }

    [Fact]
    public void Sunfish_And_System_AreDistinct()
    {
        Assert.NotEqual(ActorId.System, ActorId.Sunfish);
    }
}
