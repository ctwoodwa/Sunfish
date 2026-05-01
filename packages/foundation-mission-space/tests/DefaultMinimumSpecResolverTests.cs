using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Migration;
using Sunfish.Foundation.UI;
using Sunfish.Foundation.Versioning;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Tests;

public sealed class DefaultMinimumSpecResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static MissionEnvelope NewEnvelope(
        ulong? ramMb = 16384, // 16 GB
        int? cores = 8,
        bool isOnline = true,
        bool isSignedIn = true,
        SyncState syncState = SyncState.Healthy,
        bool hasIdentityKey = true)
    {
        return new MissionEnvelope
        {
            Hardware = new HardwareCapabilities
            {
                CpuArch = "X64",
                CpuLogicalCores = cores,
                RamTotalMb = ramMb,
                StorageAvailableMb = 100_000,
                HasGpu = true,
                ProbeStatus = ProbeStatus.Healthy,
            },
            User = new UserCapabilities
            {
                PrincipalId = "user-1",
                Roles = new[] { "admin" },
                IsSignedIn = isSignedIn,
                ProbeStatus = ProbeStatus.Healthy,
            },
            Regulatory = new RegulatoryCapabilities
            {
                JurisdictionCodes = new[] { "US-UT" },
                ProbeStatus = ProbeStatus.Healthy,
            },
            Runtime = new RuntimeCapabilities
            {
                ProcessArch = "X64",
                OsFamily = "Windows",
                OsVersion = "11.0",
                DotnetVersion = "11.0",
                ProbeStatus = ProbeStatus.Healthy,
            },
            FormFactor = new FormFactorSnapshot
            {
                Profile = new FormFactorProfile
                {
                    FormFactor = FormFactorKind.Desktop,
                    InputModalities = new HashSet<InputModalityKind>(),
                    DisplayClass = DisplayClassKind.Large,
                    NetworkPosture = NetworkPostureKind.AlwaysConnected,
                    StorageBudgetMb = 100_000u,
                    PowerProfile = PowerProfileKind.Wallpower,
                    SensorSurface = new HashSet<SensorKind>(),
                    InstanceClass = InstanceClassKind.SelfHost,
                },
                ProbeStatus = ProbeStatus.Healthy,
            },
            Edition = new EditionCapabilities
            {
                EditionKey = "Pro",
                IsTrial = false,
                ProbeStatus = ProbeStatus.Healthy,
            },
            Network = new NetworkCapabilities
            {
                IsOnline = isOnline,
                HasMeshVpn = false,
                IsMeteredConnection = false,
                ProbeStatus = ProbeStatus.Healthy,
            },
            TrustAnchor = new TrustAnchorCapabilities
            {
                HasIdentityKey = hasIdentityKey,
                TrustedPeerCount = 4,
                ProbeStatus = ProbeStatus.Healthy,
            },
            SyncState = new SyncStateSnapshot
            {
                State = syncState,
                LastSyncedAt = Now,
                ConflictCount = 0,
                ProbeStatus = ProbeStatus.Healthy,
            },
            VersionVector = new VersionVectorSnapshot
            {
                Vector = new VersionVector(
                    Kernel: "1.5.0",
                    Plugins: new Dictionary<PluginId, PluginVersionVectorEntry>(),
                    Adapters: new Dictionary<AdapterId, string>(),
                    SchemaEpoch: 3u,
                    Channel: ChannelKind.Stable,
                    InstanceClass: InstanceClassKind.SelfHost),
                ProbeStatus = ProbeStatus.Healthy,
            },
            SnapshotAt = Now,
        }.WithComputedHash();
    }

    [Fact]
    public async Task Evaluate_AllPass_ReturnsPassVerdict()
    {
        var resolver = new DefaultMinimumSpecResolver(new FakeTime(Now));
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 8L * 1024 * 1024 * 1024, MinCpuLogicalCores = 4 },
            Runtime = new RuntimeSpec { RequiredOsFamilies = new HashSet<string> { "Windows", "MacOS" } },
        };

        var result = await resolver.EvaluateAsync(spec, NewEnvelope());

        Assert.Equal(OverallVerdict.Pass, result.Overall);
    }

    [Fact]
    public async Task Evaluate_RequiredFails_ReturnsBlockVerdict()
    {
        var resolver = new DefaultMinimumSpecResolver(new FakeTime(Now));
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 32L * 1024 * 1024 * 1024 }, // 32 GB > 16 GB env
        };

        var result = await resolver.EvaluateAsync(spec, NewEnvelope(ramMb: 16384));

        Assert.Equal(OverallVerdict.Block, result.Overall);
    }

    [Fact]
    public async Task Evaluate_RecommendedFails_ReturnsWarnOnlyVerdict()
    {
        var resolver = new DefaultMinimumSpecResolver(new FakeTime(Now));
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Recommended,
            Hardware = new HardwareSpec { MinMemoryBytes = 32L * 1024 * 1024 * 1024 },
        };

        var result = await resolver.EvaluateAsync(spec, NewEnvelope(ramMb: 16384));

        Assert.Equal(OverallVerdict.WarnOnly, result.Overall);
    }

    [Fact]
    public async Task Evaluate_InformationalFails_StillReturnsPassVerdict_PerA1_8()
    {
        // Per A1.8 explicit Informational rule — Informational dimensions are surfaced
        // but never gate the verdict.
        var resolver = new DefaultMinimumSpecResolver(new FakeTime(Now));
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Informational,
            Hardware = new HardwareSpec { MinMemoryBytes = 32L * 1024 * 1024 * 1024 }, // would fail if Required
        };

        var result = await resolver.EvaluateAsync(spec, NewEnvelope(ramMb: 16384));

        Assert.Equal(OverallVerdict.Pass, result.Overall);
        var hw = result.Dimensions.Single(d => d.Dimension == DimensionChangeKind.Hardware);
        Assert.Equal(DimensionPolicyKind.Informational, hw.Policy);
        Assert.Equal(DimensionPassFail.Fail, hw.Outcome);
    }

    [Fact]
    public async Task Evaluate_NoSpecForDimension_MarkedUnevaluated()
    {
        var resolver = new DefaultMinimumSpecResolver(new FakeTime(Now));
        var spec = new MinimumSpec { Policy = SpecPolicy.Required };

        var result = await resolver.EvaluateAsync(spec, NewEnvelope());

        Assert.Equal(OverallVerdict.Pass, result.Overall);
        Assert.All(result.Dimensions, d =>
        {
            Assert.Equal(DimensionPolicyKind.Unevaluated, d.Policy);
            Assert.Equal(DimensionPassFail.Unevaluated, d.Outcome);
        });
        Assert.Equal(10, result.Dimensions.Count);
    }

    [Fact]
    public async Task Evaluate_PerPlatformOverride_ComposesNotReplaces()
    {
        // Per A1.7 COMPOSE rule. Baseline declares MinMemoryBytes; iOS override
        // adds RequiresIdentityKey on Trust dimension. The merged spec should
        // evaluate BOTH dimensions.
        var resolver = new DefaultMinimumSpecResolver(new FakeTime(Now));
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 8L * 1024 * 1024 * 1024 },
            PerPlatform = new[]
            {
                new PerPlatformSpec
                {
                    Platform = "ios",
                    Trust = new TrustSpec { RequiresIdentityKey = true },
                },
            },
        };

        var result = await resolver.EvaluateAsync(spec, NewEnvelope(hasIdentityKey: true), platform: "ios");

        Assert.Equal(OverallVerdict.Pass, result.Overall);
        var hw = result.Dimensions.Single(d => d.Dimension == DimensionChangeKind.Hardware);
        var trust = result.Dimensions.Single(d => d.Dimension == DimensionChangeKind.TrustAnchor);
        Assert.Equal(DimensionPolicyKind.Required, hw.Policy);  // baseline applied
        Assert.Equal(DimensionPolicyKind.Required, trust.Policy); // override added
        Assert.Equal(DimensionPassFail.Pass, hw.Outcome);
        Assert.Equal(DimensionPassFail.Pass, trust.Outcome);
    }

    [Fact]
    public async Task Evaluate_PerPlatformOverride_OverrideValueWinsOverBaseline()
    {
        // Per A1.7: iOS override sets MinMemoryBytes lower than baseline;
        // override's value wins for that dimension.
        var resolver = new DefaultMinimumSpecResolver(new FakeTime(Now));
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 32L * 1024 * 1024 * 1024 }, // 32 GB baseline
            PerPlatform = new[]
            {
                new PerPlatformSpec
                {
                    Platform = "ios",
                    Hardware = new HardwareSpec { MinMemoryBytes = 4L * 1024 * 1024 * 1024 }, // 4 GB iOS
                },
            },
        };

        // Env has 16 GB. Without override → Block (16 < 32). With override → Pass (16 > 4).
        var withOverride = await resolver.EvaluateAsync(spec, NewEnvelope(ramMb: 16384), platform: "ios");
        Assert.Equal(OverallVerdict.Pass, withOverride.Overall);

        var resolverFresh = new DefaultMinimumSpecResolver(new FakeTime(Now));
        var withoutOverride = await resolverFresh.EvaluateAsync(spec, NewEnvelope(ramMb: 16384));
        Assert.Equal(OverallVerdict.Block, withoutOverride.Overall);
    }

    [Fact]
    public async Task Evaluate_UnknownPlatform_BaselineApplies()
    {
        var resolver = new DefaultMinimumSpecResolver(new FakeTime(Now));
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 8L * 1024 * 1024 * 1024 },
            PerPlatform = new[]
            {
                new PerPlatformSpec
                {
                    Platform = "ios",
                    Hardware = new HardwareSpec { MinMemoryBytes = 32L * 1024 * 1024 * 1024 },
                },
            },
        };

        // Platform "android" not declared → baseline applies (8 GB) and 16 GB env passes.
        var result = await resolver.EvaluateAsync(spec, NewEnvelope(ramMb: 16384), platform: "android");
        Assert.Equal(OverallVerdict.Pass, result.Overall);
    }

    [Fact]
    public async Task Evaluate_CacheTtl_ReturnsCachedResultWithinWindow()
    {
        var time = new FakeTime(Now);
        var resolver = new DefaultMinimumSpecResolver(time);
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 8L * 1024 * 1024 * 1024 },
        };
        var env = NewEnvelope();

        var first = await resolver.EvaluateAsync(spec, env);
        time.Advance(TimeSpan.FromSeconds(10)); // within TTL
        var second = await resolver.EvaluateAsync(spec, env);

        Assert.Same(first, second); // cached ref equality
    }

    [Fact]
    public async Task Evaluate_CacheTtlExpired_ReturnsFreshResult()
    {
        var time = new FakeTime(Now);
        var resolver = new DefaultMinimumSpecResolver(time);
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 8L * 1024 * 1024 * 1024 },
        };
        var env = NewEnvelope();

        var first = await resolver.EvaluateAsync(spec, env);
        time.Advance(TimeSpan.FromSeconds(31)); // past TTL
        var second = await resolver.EvaluateAsync(spec, env);

        Assert.NotSame(first, second);
        Assert.Equal(first.Overall, second.Overall);
    }

    [Fact]
    public async Task InvalidateCache_ForcesFreshEvaluation()
    {
        var time = new FakeTime(Now);
        var resolver = new DefaultMinimumSpecResolver(time);
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 8L * 1024 * 1024 * 1024 },
        };
        var env = NewEnvelope();

        var first = await resolver.EvaluateAsync(spec, env);
        resolver.InvalidateCache();
        var second = await resolver.EvaluateAsync(spec, env);

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task Evaluate_NullArgs_Throws()
    {
        var resolver = new DefaultMinimumSpecResolver();
        var spec = new MinimumSpec();
        var env = NewEnvelope();
        await Assert.ThrowsAsync<ArgumentNullException>(() => resolver.EvaluateAsync(null!, env).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => resolver.EvaluateAsync(spec, null!).AsTask());
    }

    [Fact]
    public async Task Evaluate_HonorsCancellation()
    {
        var resolver = new DefaultMinimumSpecResolver();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            resolver.EvaluateAsync(new MinimumSpec(), NewEnvelope(), null, cts.Token).AsTask());
    }

    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
