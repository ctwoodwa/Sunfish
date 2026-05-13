using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.IdentityAtlas.Tests;

public class NullTeamRegistryTests
{
    private readonly ITeamRegistry _sut = new NullTeamRegistry();
    private readonly ActorId _actor = new("test-actor");

    [Fact]
    public async Task GetMembershipsAsync_ReturnsEmptyList()
    {
        var memberships = await _sut.GetMembershipsAsync(_actor);
        Assert.Empty(memberships);
    }

    [Fact]
    public async Task GetMembershipsAsync_NeverReturnsNull()
    {
        var memberships = await _sut.GetMembershipsAsync(_actor);
        Assert.NotNull(memberships);
    }
}
