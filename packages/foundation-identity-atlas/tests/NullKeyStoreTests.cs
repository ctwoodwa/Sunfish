using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.IdentityAtlas.Tests;

public class NullKeyStoreTests
{
    private readonly IKeyStore _sut = new NullKeyStore();
    private readonly TenantId _tenant = new("acme");
    private readonly ActorId _actor = new("test-actor");

    [Fact]
    public async Task GetIdentityProfileAsync_ReturnsNull()
    {
        var result = await _sut.GetIdentityProfileAsync(_tenant, _actor);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentKeyInfoAsync_ReturnsNull()
    {
        var result = await _sut.GetCurrentKeyInfoAsync(_tenant, _actor);
        Assert.Null(result);
    }
}
