using Xunit;

namespace Sunfish.Federation.BlobReplication.Tests;

[Collection("Kubo")]
public sealed class KuboHealthProbeTests
{
    private readonly KuboSingleNodeFixture _kubo;

    public KuboHealthProbeTests(KuboSingleNodeFixture kubo)
    {
        _kubo = kubo;
    }

    [Fact]
    public async Task GetConfigAsync_DefaultProfile_ReportsPublicNetwork()
    {
        // The default ipfs/kubo:v0.28.0 container has no swarm key — it runs on the public IPFS
        // bootstrap network. FederationStartupChecks' production-mode assertion relies on this
        // being accurately reported as "public".
        var probe = new KuboHealthProbe(_kubo.KuboClient);

        var info = await probe.GetConfigAsync(CancellationToken.None);

        Assert.Equal("public", info.NetworkProfile);
        Assert.False(string.IsNullOrWhiteSpace(info.Version));
    }
}
