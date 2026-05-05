using Sunfish.Blocks.CrewComms.Signaling;
using Sunfish.Federation.Common;
using Xunit;

namespace Sunfish.Blocks.CrewComms.Tests;

public class GlareResolverTests
{
    [Fact]
    public void IsLocalYielder_LowerOrdinalYields()
    {
        var aaa = new PeerId("aaa");
        var bbb = new PeerId("bbb");
        Assert.True(GlareResolver.IsLocalYielder(aaa, bbb));
        Assert.False(GlareResolver.IsLocalYielder(bbb, aaa));
    }

    [Fact]
    public void IsLocalYielder_BothPeersAgreeWithoutCoordination()
    {
        // Symmetry property: both endpoints, running independently, MUST
        // agree on which is the initiator.
        var p1 = new PeerId("xJ7Lq3w==");
        var p2 = new PeerId("aZ9k0Pv==");
        var p1Yields = GlareResolver.IsLocalYielder(p1, p2);
        var p2Yields = GlareResolver.IsLocalYielder(p2, p1);
        Assert.NotEqual(p1Yields, p2Yields);
    }

    [Fact]
    public void IsLocalYielder_EqualPeerIdReturnsFalse()
    {
        var same = new PeerId("self");
        Assert.False(GlareResolver.IsLocalYielder(same, same));
    }
}
