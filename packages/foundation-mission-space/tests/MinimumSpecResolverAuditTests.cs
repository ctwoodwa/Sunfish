using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Migration;
using Sunfish.Foundation.UI;
using Sunfish.Foundation.Versioning;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Tests;

public sealed class MinimumSpecResolverAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId TenantA = new("tenant-a");

    private static MissionEnvelope NewEnvelope(ulong? ramMb = 16384)
    {
        return new MissionEnvelope
        {
            Hardware = new HardwareCapabilities
            {
                CpuArch = "X64",
                CpuLogicalCores = 8,
                RamTotalMb = ramMb,
                ProbeStatus = ProbeStatus.Healthy,
            },
            User = new UserCapabilities { IsSignedIn = true, ProbeStatus = ProbeStatus.Healthy },
            Regulatory = new RegulatoryCapabilities { JurisdictionCodes = new[] { "US-UT" }, ProbeStatus = ProbeStatus.Healthy },
            Runtime = new RuntimeCapabilities { OsFamily = "Windows", OsVersion = "11.0", DotnetVersion = "11.0", ProbeStatus = ProbeStatus.Healthy },
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
            Edition = new EditionCapabilities { EditionKey = "Pro", IsTrial = false, ProbeStatus = ProbeStatus.Healthy },
            Network = new NetworkCapabilities { IsOnline = true, ProbeStatus = ProbeStatus.Healthy },
            TrustAnchor = new TrustAnchorCapabilities { HasIdentityKey = true, TrustedPeerCount = 4, ProbeStatus = ProbeStatus.Healthy },
            SyncState = new SyncStateSnapshot { State = SyncState.Healthy, ProbeStatus = ProbeStatus.Healthy },
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

    private static (DefaultMinimumSpecResolver resolver, IAuditTrail trail) NewAuditEnabledResolver(FakeTime? time = null)
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        var resolver = new DefaultMinimumSpecResolver(trail, signer, TenantA, time ?? new FakeTime(Now));
        return (resolver, trail);
    }

    [Fact]
    public async Task Evaluate_AuditEnabled_AllPass_EmitsMinimumSpecEvaluated()
    {
        var (resolver, trail) = NewAuditEnabledResolver();
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 8L * 1024 * 1024 * 1024 },
        };

        await resolver.EvaluateAsync(spec, NewEnvelope());

        await trail.Received().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.MinimumSpecEvaluated)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_AuditEnabled_RequiredFails_EmitsInstallBlocked()
    {
        var (resolver, trail) = NewAuditEnabledResolver();
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 32L * 1024 * 1024 * 1024 },
        };

        await resolver.EvaluateAsync(spec, NewEnvelope(ramMb: 16384));

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.InstallBlocked)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_AuditEnabled_RecommendedFails_EmitsInstallWarned()
    {
        var (resolver, trail) = NewAuditEnabledResolver();
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Recommended,
            Hardware = new HardwareSpec { MinMemoryBytes = 32L * 1024 * 1024 * 1024 },
        };

        await resolver.EvaluateAsync(spec, NewEnvelope(ramMb: 16384));

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.InstallWarned)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_AuditEnabled_PassToFailTransition_EmitsPostInstallSpecRegression()
    {
        var time = new FakeTime(Now);
        var (resolver, trail) = NewAuditEnabledResolver(time);
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 8L * 1024 * 1024 * 1024 },
        };

        // Round 1: Pass (16 GB env, 8 GB spec).
        await resolver.EvaluateAsync(spec, NewEnvelope(ramMb: 16384));

        // Advance past the cache TTL so the next evaluation is fresh.
        time.Advance(TimeSpan.FromSeconds(31));

        // Round 2: Fail (4 GB env, same spec) → regression.
        await resolver.EvaluateAsync(spec, NewEnvelope(ramMb: 4096));

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.PostInstallSpecRegression)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_AuditEnabled_RepeatedEvaluation_EvaluatedDeduped5Min()
    {
        var time = new FakeTime(Now);
        var (resolver, trail) = NewAuditEnabledResolver(time);
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 8L * 1024 * 1024 * 1024 },
        };

        // First evaluation fires MinimumSpecEvaluated.
        await resolver.EvaluateAsync(spec, NewEnvelope());
        // Advance past the resolver's cache TTL but stay within the 5-min dedup window.
        time.Advance(TimeSpan.FromSeconds(60));
        resolver.InvalidateCache();
        await resolver.EvaluateAsync(spec, NewEnvelope());

        // Should only see ONE MinimumSpecEvaluated emission within 5 min.
        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.MinimumSpecEvaluated)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_AuditDisabled_DoesNotEmit()
    {
        var resolver = new DefaultMinimumSpecResolver(new FakeTime(Now));
        // Build the audit-disabled resolver; no emission should occur regardless of verdict.
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 32L * 1024 * 1024 * 1024 },
        };
        var result = await resolver.EvaluateAsync(spec, NewEnvelope(ramMb: 16384));
        Assert.Equal(OverallVerdict.Block, result.Overall);
        // No exception, no audit trail wired.
    }

    [Fact]
    public void Constructor_AuditEnabled_RequiresAllArgs()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());

        Assert.Throws<ArgumentNullException>(() => new DefaultMinimumSpecResolver(null!, signer, TenantA));
        Assert.Throws<ArgumentNullException>(() => new DefaultMinimumSpecResolver(trail, null!, TenantA));
        Assert.Throws<ArgumentException>(() => new DefaultMinimumSpecResolver(trail, signer, default));
    }

    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
