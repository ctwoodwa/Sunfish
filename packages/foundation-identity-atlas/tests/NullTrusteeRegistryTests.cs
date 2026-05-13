using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.IdentityAtlas.Tests;

public class NullTrusteeRegistryTests
{
    private readonly ITrusteeRegistry _sut = new NullTrusteeRegistry();
    private readonly TenantId _tenant = new("acme");
    private readonly ActorId _actor = new("test-actor");

    [Fact]
    public async Task GetPolicyAsync_ReturnsZeroMaxPolicy()
    {
        var policy = await _sut.GetPolicyAsync(_tenant);
        Assert.Equal(0, policy.MaxTrustees);
    }

    [Fact]
    public async Task GetTrusteesAsync_ReturnsEmptyList()
    {
        var trustees = await _sut.GetTrusteesAsync(_tenant, _actor);
        Assert.Empty(trustees);
    }

    [Fact]
    public async Task GetPolicyAsync_NeverReturnsNull()
    {
        var policy = await _sut.GetPolicyAsync(_tenant);
        Assert.NotNull(policy);
    }
}
