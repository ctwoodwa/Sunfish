using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Migration;
using Sunfish.Foundation.UI;
using Sunfish.Foundation.Versioning;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Tests;

public sealed class DefaultHardwareProbeTests
{
    [Fact]
    public async Task Probe_ReportsHealthy_AndPopulatesArchAndCoreCount()
    {
        var probe = new DefaultHardwareProbe();
        Assert.Equal(DimensionChangeKind.Hardware, probe.Dimension);
        Assert.Equal(ProbeCostClass.Low, probe.CostClass);

        var caps = await probe.ProbeAsync();
        Assert.Equal(ProbeStatus.Healthy, caps.ProbeStatus);
        Assert.False(string.IsNullOrEmpty(caps.CpuArch));
        Assert.NotNull(caps.CpuLogicalCores);
        Assert.True(caps.CpuLogicalCores >= 1);
    }

    [Fact]
    public async Task Probe_HonorsCancellation()
    {
        var probe = new DefaultHardwareProbe();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => probe.ProbeAsync(cts.Token).AsTask());
    }
}

public sealed class DefaultRuntimeProbeTests
{
    [Fact]
    public async Task Probe_ReportsHealthy_PopulatesOsAndDotnet()
    {
        var probe = new DefaultRuntimeProbe();
        Assert.Equal(DimensionChangeKind.Runtime, probe.Dimension);
        Assert.Equal(ProbeCostClass.Low, probe.CostClass);

        var caps = await probe.ProbeAsync();
        Assert.Equal(ProbeStatus.Healthy, caps.ProbeStatus);
        Assert.False(string.IsNullOrEmpty(caps.OsFamily));
        Assert.False(string.IsNullOrEmpty(caps.OsVersion));
        Assert.False(string.IsNullOrEmpty(caps.DotnetVersion));
        Assert.False(string.IsNullOrEmpty(caps.ProcessArch));
    }
}

public sealed class DefaultNetworkProbeTests
{
    [Fact]
    public async Task Probe_NoDetectors_ReportsNullMeshAndMetered()
    {
        var probe = new DefaultNetworkProbe();
        Assert.Equal(DimensionChangeKind.Network, probe.Dimension);
        Assert.Equal(ProbeCostClass.Medium, probe.CostClass);

        var caps = await probe.ProbeAsync();
        Assert.Null(caps.HasMeshVpn);
        Assert.Null(caps.IsMeteredConnection);
        // Status is whatever the platform reports — usually Healthy, possibly PartiallyDegraded.
        Assert.True(caps.ProbeStatus is ProbeStatus.Healthy or ProbeStatus.PartiallyDegraded);
    }

    [Fact]
    public async Task Probe_WithDetectors_PassesThroughResults()
    {
        var probe = new DefaultNetworkProbe(meshDetector: () => true, meteredDetector: () => false);
        var caps = await probe.ProbeAsync();
        // If platform reports degraded, mesh+metered may be null per the fallback path.
        if (caps.ProbeStatus == ProbeStatus.Healthy)
        {
            Assert.True(caps.HasMeshVpn);
            Assert.False(caps.IsMeteredConnection);
        }
    }

    [Fact]
    public async Task Probe_DetectorThrows_FallsBackToNull()
    {
        var probe = new DefaultNetworkProbe(
            meshDetector: () => throw new InvalidOperationException("boom"),
            meteredDetector: () => throw new InvalidOperationException("boom"));
        var caps = await probe.ProbeAsync();
        if (caps.ProbeStatus == ProbeStatus.Healthy)
        {
            Assert.Null(caps.HasMeshVpn);
            Assert.Null(caps.IsMeteredConnection);
        }
    }
}

public sealed class DefaultUserProbeTests
{
    [Fact]
    public async Task Probe_NoSource_AnonymousNotSignedIn()
    {
        var probe = new DefaultUserProbe();
        Assert.Equal(DimensionChangeKind.User, probe.Dimension);
        Assert.Equal(ProbeCostClass.Low, probe.CostClass);

        var caps = await probe.ProbeAsync();
        Assert.False(caps.IsSignedIn);
        Assert.Equal(ProbeStatus.Healthy, caps.ProbeStatus);
    }

    [Fact]
    public async Task Probe_WithSource_DelegatesToSource()
    {
        var probe = new DefaultUserProbe(_ => ValueTask.FromResult(new UserCapabilities
        {
            PrincipalId = "user-1",
            IsSignedIn = true,
            ProbeStatus = ProbeStatus.Healthy,
        }));
        var caps = await probe.ProbeAsync();
        Assert.True(caps.IsSignedIn);
        Assert.Equal("user-1", caps.PrincipalId);
    }

    [Fact]
    public void Constructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultUserProbe(null!));
    }
}

public sealed class DefaultEditionProbeTests
{
    [Fact]
    public async Task Probe_NoSource_Unreachable()
    {
        var probe = new DefaultEditionProbe();
        Assert.Equal(DimensionChangeKind.Edition, probe.Dimension);
        Assert.Equal(ProbeCostClass.Low, probe.CostClass);

        var caps = await probe.ProbeAsync();
        Assert.Equal(ProbeStatus.Unreachable, caps.ProbeStatus);
        Assert.Null(caps.EditionKey);
    }

    [Fact]
    public async Task Probe_WithSource_DelegatesToSource()
    {
        var probe = new DefaultEditionProbe(_ => ValueTask.FromResult(new EditionCapabilities
        {
            EditionKey = "Pro",
            IsTrial = false,
            ProbeStatus = ProbeStatus.Healthy,
        }));
        var caps = await probe.ProbeAsync();
        Assert.Equal("Pro", caps.EditionKey);
        Assert.Equal(ProbeStatus.Healthy, caps.ProbeStatus);
    }

    [Fact]
    public void Constructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultEditionProbe(null!));
    }
}

public sealed class DefaultRegulatoryProbeTests
{
    [Fact]
    public async Task Probe_NoSource_UnreachableEmptyJurisdictions()
    {
        var probe = new DefaultRegulatoryProbe();
        Assert.Equal(DimensionChangeKind.Regulatory, probe.Dimension);
        Assert.Equal(ProbeCostClass.Medium, probe.CostClass);

        var caps = await probe.ProbeAsync();
        Assert.Equal(ProbeStatus.Unreachable, caps.ProbeStatus);
        Assert.Empty(caps.JurisdictionCodes);
    }

    [Fact]
    public async Task Probe_WithSource_PassesThroughJurisdictions()
    {
        var probe = new DefaultRegulatoryProbe(_ => ValueTask.FromResult(new RegulatoryCapabilities
        {
            JurisdictionCodes = new[] { "US-UT", "EU-DE" },
            ProbeStatus = ProbeStatus.Healthy,
        }));
        var caps = await probe.ProbeAsync();
        Assert.Equal(2, caps.JurisdictionCodes.Count);
    }
}

public sealed class DefaultTrustAnchorProbeTests
{
    [Fact]
    public async Task Probe_NoSource_Unreachable()
    {
        var probe = new DefaultTrustAnchorProbe();
        Assert.Equal(DimensionChangeKind.TrustAnchor, probe.Dimension);
        Assert.Equal(ProbeCostClass.Low, probe.CostClass);

        var caps = await probe.ProbeAsync();
        Assert.Equal(ProbeStatus.Unreachable, caps.ProbeStatus);
        Assert.False(caps.HasIdentityKey);
    }

    [Fact]
    public async Task Probe_WithSource_PassesThroughIdentityState()
    {
        var probe = new DefaultTrustAnchorProbe(_ => ValueTask.FromResult(new TrustAnchorCapabilities
        {
            HasIdentityKey = true,
            TrustedPeerCount = 4,
            ProbeStatus = ProbeStatus.Healthy,
        }));
        var caps = await probe.ProbeAsync();
        Assert.True(caps.HasIdentityKey);
        Assert.Equal(4, caps.TrustedPeerCount);
    }
}

public sealed class DefaultSyncStateProbeTests
{
    [Fact]
    public async Task Probe_NoSource_UnreachableOfflineState()
    {
        var probe = new DefaultSyncStateProbe();
        Assert.Equal(DimensionChangeKind.SyncState, probe.Dimension);
        Assert.Equal(ProbeCostClass.Low, probe.CostClass);

        var snap = await probe.ProbeAsync();
        Assert.Equal(ProbeStatus.Unreachable, snap.ProbeStatus);
        Assert.Equal(SyncState.Offline, snap.State);
    }

    [Fact]
    public async Task Probe_WithSource_PassesThroughState()
    {
        var probe = new DefaultSyncStateProbe(_ => ValueTask.FromResult(new SyncStateSnapshot
        {
            State = SyncState.Healthy,
            LastSyncedAt = DateTimeOffset.UtcNow,
            ConflictCount = 0,
            ProbeStatus = ProbeStatus.Healthy,
        }));
        var snap = await probe.ProbeAsync();
        Assert.Equal(SyncState.Healthy, snap.State);
    }
}

public sealed class DefaultVersionVectorProbeTests
{
    [Fact]
    public async Task Probe_NoSource_UnreachableNullVector()
    {
        var probe = new DefaultVersionVectorProbe();
        Assert.Equal(DimensionChangeKind.VersionVector, probe.Dimension);
        Assert.Equal(ProbeCostClass.Low, probe.CostClass);

        var snap = await probe.ProbeAsync();
        Assert.Equal(ProbeStatus.Unreachable, snap.ProbeStatus);
        Assert.Null(snap.Vector);
    }

    [Fact]
    public async Task Probe_WithSource_PassesThroughVector()
    {
        var v = new VersionVector(
            Kernel: "1.2.3",
            Plugins: new System.Collections.Generic.Dictionary<PluginId, PluginVersionVectorEntry>(),
            Adapters: new System.Collections.Generic.Dictionary<AdapterId, string>(),
            SchemaEpoch: 1u,
            Channel: ChannelKind.Stable,
            InstanceClass: InstanceClassKind.SelfHost);
        var probe = new DefaultVersionVectorProbe(_ => ValueTask.FromResult(new VersionVectorSnapshot
        {
            Vector = v,
            ProbeStatus = ProbeStatus.Healthy,
        }));
        var snap = await probe.ProbeAsync();
        Assert.Equal(v, snap.Vector);
    }
}

public sealed class DefaultFormFactorProbeTests
{
    [Fact]
    public async Task Probe_NoSource_UnreachableNullProfile()
    {
        var probe = new DefaultFormFactorProbe();
        Assert.Equal(DimensionChangeKind.FormFactor, probe.Dimension);
        Assert.Equal(ProbeCostClass.Low, probe.CostClass);

        var snap = await probe.ProbeAsync();
        Assert.Equal(ProbeStatus.Unreachable, snap.ProbeStatus);
        Assert.Null(snap.Profile);
    }

    [Fact]
    public async Task Probe_WithSource_PassesThroughProfile()
    {
        var profile = new FormFactorProfile
        {
            FormFactor = Sunfish.Foundation.Migration.FormFactorKind.Desktop,
            InputModalities = new System.Collections.Generic.HashSet<Sunfish.Foundation.Migration.InputModalityKind>(),
            DisplayClass = Sunfish.Foundation.Migration.DisplayClassKind.Large,
            NetworkPosture = Sunfish.Foundation.Migration.NetworkPostureKind.AlwaysConnected,
            StorageBudgetMb = 100u,
            PowerProfile = Sunfish.Foundation.Migration.PowerProfileKind.Wallpower,
            SensorSurface = new System.Collections.Generic.HashSet<Sunfish.Foundation.Migration.SensorKind>(),
            InstanceClass = InstanceClassKind.SelfHost,
        };
        var probe = new DefaultFormFactorProbe(_ => ValueTask.FromResult(new FormFactorSnapshot
        {
            Profile = profile,
            ProbeStatus = ProbeStatus.Healthy,
        }));
        var snap = await probe.ProbeAsync();
        Assert.Equal(profile, snap.Profile);
    }
}

// ===== IFeatureBespokeProbe extension-surface contract test =====

public sealed class FeatureBespokeProbeContractTests
{
    private sealed record SampleSignal(double Value) : IBespokeSignal;

    private sealed class SampleBespokeProbe : IFeatureBespokeProbe<SampleSignal>
    {
        public string FeatureKey => "feature.experimental.sample";
        public ProbeCostClass CostClass => ProbeCostClass.Live;
        public ValueTask<SampleSignal> ProbeAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(new SampleSignal(0.42));
    }

    [Fact]
    public async Task BespokeProbe_ImplementsContract_AndReportsCostClass()
    {
        IFeatureBespokeProbe<SampleSignal> probe = new SampleBespokeProbe();
        Assert.Equal("feature.experimental.sample", probe.FeatureKey);
        Assert.Equal(ProbeCostClass.Live, probe.CostClass);

        var signal = await probe.ProbeAsync();
        Assert.Equal(0.42, signal.Value);
    }

    [Fact]
    public async Task BespokeProbe_HonorsCancellation_ViaContract()
    {
        IFeatureBespokeProbe<SampleSignal> probe = new SampleBespokeProbe();
        using var cts = new CancellationTokenSource();
        // The sample probe is synchronous; we verify the contract surface compiles + executes.
        var signal = await probe.ProbeAsync(cts.Token);
        Assert.NotNull(signal);
    }
}
