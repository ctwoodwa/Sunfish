using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SickBay;
using Xunit;

namespace Sunfish.Blocks.SickBay.Tests;

public class DefaultStretcherBearerPolicyTests
{
    [Fact]
    public async Task GetEligibleRespondersAsync_ReturnsAllFourStretcherBearerRoleValues()
    {
        var policy = new DefaultStretcherBearerPolicy();

        var responders = await policy.GetEligibleRespondersAsync(new TenantId("alpha"));

        Assert.Equal(4, responders.Count);
        Assert.Contains(StretcherBearerRole.DCA, responders);
        Assert.Contains(StretcherBearerRole.MPA, responders);
        Assert.Contains(StretcherBearerRole.CommsOfficer, responders);
        Assert.Contains(StretcherBearerRole.SonarOfficer, responders);
    }

    [Fact]
    public async Task GetEligibleRespondersAsync_Cancellation_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var policy = new DefaultStretcherBearerPolicy();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => policy.GetEligibleRespondersAsync(new TenantId("alpha"), cts.Token));
    }
}
