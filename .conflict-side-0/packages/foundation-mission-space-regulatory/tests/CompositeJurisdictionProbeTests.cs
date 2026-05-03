using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Regulatory.Tests;

public sealed class CompositeJurisdictionProbeTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static DefaultCompositeJurisdictionProbe Probe() =>
        new(new FakeTime(Now));

    [Fact]
    public async Task ProbeAsync_NoSignals_ReturnsNull()
    {
        var probe = Probe();
        var result = await probe.ProbeAsync(new CompositeJurisdictionSignals());
        Assert.Null(result);
    }

    [Fact]
    public async Task ProbeAsync_AllThreeAgree_ReturnsHighConfidence()
    {
        var probe = Probe();
        var result = await probe.ProbeAsync(new CompositeJurisdictionSignals
        {
            UserDeclaredCode = "US-UT",
            TenantConfigCode = "US-UT",
            IpGeoCode = "US-UT",
        });
        Assert.NotNull(result);
        Assert.Equal("US-UT", result!.JurisdictionCode);
        Assert.Equal(Confidence.High, result.Confidence);
        Assert.Equal(3, result.SignalSources.Count);
    }

    [Theory]
    [InlineData("US-UT", "US-UT", null)]
    [InlineData("US-UT", null, "US-UT")]
    [InlineData(null, "US-UT", "US-UT")]
    public async Task ProbeAsync_TwoOfTwoAgree_ReturnsMediumConfidence(string? user, string? tenant, string? ip)
    {
        var probe = Probe();
        var result = await probe.ProbeAsync(new CompositeJurisdictionSignals
        {
            UserDeclaredCode = user,
            TenantConfigCode = tenant,
            IpGeoCode = ip,
        });
        Assert.NotNull(result);
        Assert.Equal("US-UT", result!.JurisdictionCode);
        Assert.Equal(Confidence.Medium, result.Confidence);
        Assert.Equal(2, result.SignalSources.Count);
    }

    [Theory]
    [InlineData("US-UT", null, null, "US-UT", "user-declaration")]
    [InlineData(null, "US-UT", null, "US-UT", "tenant-config")]
    [InlineData(null, null, "US-UT", "US-UT", "ip-geo")]
    public async Task ProbeAsync_OneSignalOnly_ReturnsLowConfidence(
        string? user, string? tenant, string? ip, string expected, string expectedSource)
    {
        var probe = Probe();
        var result = await probe.ProbeAsync(new CompositeJurisdictionSignals
        {
            UserDeclaredCode = user,
            TenantConfigCode = tenant,
            IpGeoCode = ip,
        });
        Assert.NotNull(result);
        Assert.Equal(expected, result!.JurisdictionCode);
        Assert.Equal(Confidence.Low, result.Confidence);
        Assert.Single(result.SignalSources);
        Assert.Equal(expectedSource, result.SignalSources[0]);
    }

    [Fact]
    public async Task ProbeAsync_AllDisagree_TieBreakerPrefersUserDeclaration()
    {
        var probe = Probe();
        var result = await probe.ProbeAsync(new CompositeJurisdictionSignals
        {
            UserDeclaredCode = "US-UT",
            TenantConfigCode = "EU-DE",
            IpGeoCode = "JP",
        });
        Assert.NotNull(result);
        Assert.Equal("US-UT", result!.JurisdictionCode);
        Assert.Equal(Confidence.Low, result.Confidence);
        Assert.Contains("user-declaration", result.SignalSources);
        Assert.DoesNotContain("tenant-config", result.SignalSources);
        Assert.DoesNotContain("ip-geo", result.SignalSources);
    }

    [Fact]
    public async Task ProbeAsync_UserMissingTenantAndIpDisagree_TieBreakerPrefersTenantConfig()
    {
        var probe = Probe();
        var result = await probe.ProbeAsync(new CompositeJurisdictionSignals
        {
            UserDeclaredCode = null,
            TenantConfigCode = "EU-DE",
            IpGeoCode = "JP",
        });
        Assert.NotNull(result);
        Assert.Equal("EU-DE", result!.JurisdictionCode);
        Assert.Equal(Confidence.Low, result.Confidence);
    }

    [Fact]
    public async Task ProbeAsync_UserAgreesWithIp_TenantDisagrees_LowConfidenceWithUserAndIp()
    {
        var probe = Probe();
        var result = await probe.ProbeAsync(new CompositeJurisdictionSignals
        {
            UserDeclaredCode = "US-UT",
            TenantConfigCode = "EU-DE",
            IpGeoCode = "US-UT",
        });
        Assert.NotNull(result);
        Assert.Equal("US-UT", result!.JurisdictionCode);
        Assert.Equal(Confidence.Low, result.Confidence);
        Assert.Equal(2, result.SignalSources.Count);
        Assert.Contains("user-declaration", result.SignalSources);
        Assert.Contains("ip-geo", result.SignalSources);
    }

    [Fact]
    public async Task ProbeAsync_PopulatesProbedAt()
    {
        var probe = Probe();
        var result = await probe.ProbeAsync(new CompositeJurisdictionSignals { UserDeclaredCode = "US-UT" });
        Assert.NotNull(result);
        Assert.Equal(Now, result!.ProbedAt);
    }

    [Fact]
    public async Task ProbeAsync_NullSignals_Throws()
    {
        var probe = Probe();
        await Assert.ThrowsAsync<ArgumentNullException>(() => probe.ProbeAsync(null!).AsTask());
    }

    [Fact]
    public async Task ProbeAsync_HonorsCancellation()
    {
        var probe = Probe();
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            probe.ProbeAsync(new CompositeJurisdictionSignals { UserDeclaredCode = "X" }, cts.Token).AsTask());
    }

    private sealed class FakeTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
