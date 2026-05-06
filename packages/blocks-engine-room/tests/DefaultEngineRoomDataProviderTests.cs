using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.EngineRoom;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.EngineRoom.Tests;

public class DefaultEngineRoomDataProviderTests
{
    private static readonly TenantId TenantA = new("alpha");

    private static DefaultEngineRoomDataProvider Build(
        EngineRoomOptions? options = null,
        ISyncDaemonHealthSource? syncDaemon = null,
        ICrdtDocumentRegistry? crdtRegistry = null,
        IAuditTrail? auditTrail = null,
        IOperationSigner? signer = null,
        TimeProvider? time = null) =>
        new(
            Options.Create(options ?? new EngineRoomOptions()),
            syncDaemon,
            crdtRegistry,
            auditTrail,
            signer,
            logger: null,
            time);

    private static IOperationSigner StubSigner()
    {
        var principalId = PrincipalId.FromBytes(new byte[32]);
        var signer = Substitute.For<IOperationSigner>();
        signer.IssuerId.Returns(principalId);
        signer.SignAsync(
                Arg.Any<AuditPayload>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var payload = call.Arg<AuditPayload>();
                var occurredAt = call.Arg<DateTimeOffset>();
                var nonce = call.Arg<Guid>();
                return new ValueTask<SignedOperation<AuditPayload>>(
                    new SignedOperation<AuditPayload>(
                        Payload: payload!,
                        IssuerId: principalId,
                        IssuedAt: occurredAt,
                        Nonce: nonce,
                        Signature: Signature.FromBytes(new byte[64])));
            });
        return signer;
    }

    [Fact]
    public async Task GetHealthSummary_Returns_AllFourSubrooms()
    {
        var summary = await Build().GetHealthSummaryAsync(TenantA);

        Assert.Equal(4, summary.SubsystemHealthList.Count);
        Assert.NotNull(summary.For(EngineRoomSubsystem.MainPropulsion));
        Assert.NotNull(summary.For(EngineRoomSubsystem.Electrical));
        Assert.NotNull(summary.For(EngineRoomSubsystem.DamageControl));
        Assert.NotNull(summary.For(EngineRoomSubsystem.QaWorkshop));
    }

    [Fact]
    public async Task GetSyncDaemonHealth_NoSource_ReturnsUnavailableDefault()
    {
        var snapshot = await Build().GetSyncDaemonHealthAsync(TenantA);

        Assert.Equal(SyncDaemonStatus.Unavailable, snapshot.Status);
        Assert.Equal(0, snapshot.PeerCount);
        Assert.Equal(0d, snapshot.EventsThroughput);
    }

    /// <summary>
    /// Per W#50 P2 council Critical (W#54 P2 precedent): subsystems
    /// without a registered probe surface as
    /// <see cref="SubsystemStatus.Unknown"/>, NOT
    /// <see cref="SubsystemStatus.Operational"/>. This pins the §Trust
    /// no-misrepresentation invariant.
    /// </summary>
    [Fact]
    public async Task GetHealthSummary_NoSources_AllSubsystemsAreUnknown()
    {
        var summary = await Build().GetHealthSummaryAsync(TenantA);

        Assert.All(summary.SubsystemHealthList, s =>
            Assert.Equal(SubsystemStatus.Unknown, s.Status));
    }

    [Fact]
    public async Task GetSyncDaemonHealth_WithSource_DelegatesToInjection()
    {
        var src = Substitute.For<ISyncDaemonHealthSource>();
        src.GetCurrentAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<SyncDaemonHealth>(
                new SyncDaemonHealth(SyncDaemonStatus.Healthy, 4, 12.5, 100, DateTimeOffset.UtcNow)));

        var snapshot = await Build(syncDaemon: src).GetSyncDaemonHealthAsync(TenantA);

        Assert.Equal(SyncDaemonStatus.Healthy, snapshot.Status);
        Assert.Equal(4, snapshot.PeerCount);
    }

    [Fact]
    public async Task GetHealthSummary_DegradedSyncDaemon_MainPropulsionIsWarning()
    {
        var src = Substitute.For<ISyncDaemonHealthSource>();
        src.GetCurrentAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<SyncDaemonHealth>(
                new SyncDaemonHealth(SyncDaemonStatus.Degraded, 1, 0.5, 10, DateTimeOffset.UtcNow)));

        var summary = await Build(syncDaemon: src).GetHealthSummaryAsync(TenantA);
        var prop = summary.For(EngineRoomSubsystem.MainPropulsion);

        Assert.NotNull(prop);
        Assert.Equal(SubsystemStatus.Warning, prop!.Status);
        Assert.False(string.IsNullOrEmpty(prop.Message));
    }

    [Fact]
    public async Task GetCrdtGrowthMetrics_NoRegistry_ReturnsEmpty()
    {
        var items = new List<CrdtGrowthMetrics>();
        await foreach (var m in Build().GetCrdtGrowthMetricsAsync(TenantA))
        {
            items.Add(m);
        }
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetCrdtGrowthMetrics_WithRegistry_StreamsResults()
    {
        var registry = new FakeCrdtRegistry(
            new CrdtGrowthMetrics("doc-1", TenantA, 1024, 0, false, DateTimeOffset.UtcNow),
            new CrdtGrowthMetrics("doc-2", TenantA, 9999, 100, true, DateTimeOffset.UtcNow));

        var items = new List<CrdtGrowthMetrics>();
        await foreach (var m in Build(crdtRegistry: registry).GetCrdtGrowthMetricsAsync(TenantA))
        {
            items.Add(m);
        }

        Assert.Equal(2, items.Count);
    }

    /// <summary>
    /// Defence-in-depth — even if a buggy registry returns metrics for a
    /// different tenant, the provider's tenant filter MUST drop them.
    /// Per W#50 P2a §Trust no-cross-tenant-leakage invariant.
    /// </summary>
    [Fact]
    public async Task GetCrdtGrowthMetrics_TenantMismatch_FilteredOut()
    {
        var registry = new FakeCrdtRegistry(
            new CrdtGrowthMetrics("ours", TenantA, 1024, 0, false, DateTimeOffset.UtcNow),
            new CrdtGrowthMetrics("foreign", new TenantId("beta"), 9999, 100, true, DateTimeOffset.UtcNow));

        var items = new List<CrdtGrowthMetrics>();
        await foreach (var m in Build(crdtRegistry: registry).GetCrdtGrowthMetricsAsync(TenantA))
        {
            items.Add(m);
        }

        var single = Assert.Single(items);
        Assert.Equal("ours", single.DocumentId);
    }

    [Fact]
    public async Task SubscribeHealth_EmitsInitialSummary()
    {
        var options = new EngineRoomOptions { HeartbeatInterval = TimeSpan.Zero };
        using var cts = new CancellationTokenSource();
        var enumerator = Build(options).SubscribeHealthAsync(TenantA, cts.Token).GetAsyncEnumerator();
        try
        {
            Assert.True(await enumerator.MoveNextAsync());
            Assert.NotNull(enumerator.Current);
            Assert.False(await enumerator.MoveNextAsync());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    /// <summary>
    /// Per the W#50 P2 hand-off dedup contract: same
    /// <c>(TenantId, EngineRoomSubsystem, statusFrom, statusTo)</c>
    /// tuple within the cooldown window must emit at most one
    /// degradation audit. Different tuples fire independently.
    /// </summary>
    [Fact]
    public void EmitDegradationAudits_SameTupleWithinCooldown_FiresOnce()
    {
        var fakeTime = new TestTimeProvider(DateTimeOffset.UtcNow);
        var trail = new RecordingAuditTrail();
        var provider = Build(
            options: new EngineRoomOptions { DegradationDedupCooldown = TimeSpan.FromMinutes(1) },
            auditTrail: trail,
            signer: StubSigner(),
            time: fakeTime);

        var allOk = new EngineRoomHealthSummary(
        [
            new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.Electrical, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.DamageControl, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.QaWorkshop, SubsystemStatus.Operational, null),
        ]);
        var degraded = new EngineRoomHealthSummary(
        [
            new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, SubsystemStatus.Warning, "x"),
            new SubsystemHealth(EngineRoomSubsystem.Electrical, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.DamageControl, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.QaWorkshop, SubsystemStatus.Operational, null),
        ]);

        // First transition: should emit
        InvokeEmit(provider, TenantA, allOk, degraded);
        // Second identical transition tuple within cooldown: must NOT emit
        fakeTime.Advance(TimeSpan.FromSeconds(10));
        InvokeEmit(provider, TenantA, allOk, degraded);

        // Wait briefly to let the fire-and-forget Task complete.
        SpinWait.SpinUntil(() => trail.Records.Count >= 1, TimeSpan.FromSeconds(2));

        Assert.Single(trail.Records);
    }

    /// <summary>
    /// Different tuples (different subsystem, different transition) must
    /// fire independently within the same cooldown window.
    /// </summary>
    [Fact]
    public void EmitDegradationAudits_DifferentTuples_FireIndependently()
    {
        var fakeTime = new TestTimeProvider(DateTimeOffset.UtcNow);
        var trail = new RecordingAuditTrail();
        var provider = Build(
            options: new EngineRoomOptions { DegradationDedupCooldown = TimeSpan.FromMinutes(1) },
            auditTrail: trail,
            signer: StubSigner(),
            time: fakeTime);

        var allOk = new EngineRoomHealthSummary(
        [
            new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.Electrical, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.DamageControl, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.QaWorkshop, SubsystemStatus.Operational, null),
        ]);
        var bothDegraded = new EngineRoomHealthSummary(
        [
            new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, SubsystemStatus.Warning, "x"),
            new SubsystemHealth(EngineRoomSubsystem.Electrical, SubsystemStatus.Critical, "y"),
            new SubsystemHealth(EngineRoomSubsystem.DamageControl, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.QaWorkshop, SubsystemStatus.Operational, null),
        ]);

        InvokeEmit(provider, TenantA, allOk, bothDegraded);

        SpinWait.SpinUntil(() => trail.Records.Count >= 2, TimeSpan.FromSeconds(2));

        Assert.Equal(2, trail.Records.Count);
    }

    /// <summary>
    /// Per W#50 P2 council Critical: when no signer is registered the
    /// provider MUST NOT attempt to emit a placeholder-bytes audit
    /// record (which would throw <see cref="AuditSignatureException"/>
    /// at the <see cref="IAuditTrail"/> boundary and be silently
    /// swallowed by the catch). Skip emission entirely until Phase 2b
    /// wires the signer.
    /// </summary>
    [Fact]
    public void EmitDegradationAudits_NoSigner_DoesNotEmitEvenWithAuditTrail()
    {
        var trail = new RecordingAuditTrail();
        var provider = Build(auditTrail: trail, signer: null);

        var allOk = new EngineRoomHealthSummary(
        [
            new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.Electrical, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.DamageControl, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.QaWorkshop, SubsystemStatus.Operational, null),
        ]);
        var degraded = new EngineRoomHealthSummary(
        [
            new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, SubsystemStatus.Critical, "x"),
            new SubsystemHealth(EngineRoomSubsystem.Electrical, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.DamageControl, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.QaWorkshop, SubsystemStatus.Operational, null),
        ]);

        InvokeEmit(provider, TenantA, allOk, degraded);

        Thread.Sleep(50);
        Assert.Empty(trail.Records);
    }

    [Fact]
    public void EmitDegradationAudits_RecoveryToOperational_DoesNotEmit()
    {
        var trail = new RecordingAuditTrail();
        var provider = Build(auditTrail: trail, signer: StubSigner());

        var degraded = new EngineRoomHealthSummary(
        [
            new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, SubsystemStatus.Warning, "x"),
            new SubsystemHealth(EngineRoomSubsystem.Electrical, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.DamageControl, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.QaWorkshop, SubsystemStatus.Operational, null),
        ]);
        var recovered = new EngineRoomHealthSummary(
        [
            new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.Electrical, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.DamageControl, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.QaWorkshop, SubsystemStatus.Operational, null),
        ]);

        InvokeEmit(provider, TenantA, degraded, recovered);

        // Recoveries don't emit; trail should remain empty.
        Thread.Sleep(50);
        Assert.Empty(trail.Records);
    }

    private static void InvokeEmit(
        DefaultEngineRoomDataProvider provider,
        TenantId tenant,
        EngineRoomHealthSummary prior,
        EngineRoomHealthSummary current)
    {
        var method = typeof(DefaultEngineRoomDataProvider).GetMethod(
            "EmitDegradationAudits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(provider, [tenant, prior, current]);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public TestTimeProvider(DateTimeOffset start) { _now = start; }
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) { _now = _now.Add(delta); }
    }

    private sealed class FakeCrdtRegistry : ICrdtDocumentRegistry
    {
        private readonly CrdtGrowthMetrics[] _items;
        public FakeCrdtRegistry(params CrdtGrowthMetrics[] items) { _items = items; }

#pragma warning disable CS1998 // async lacks await — IAsyncEnumerable shape requires it
        public async IAsyncEnumerable<CrdtGrowthMetrics> StreamMetricsAsync(
            TenantId tenantId,
            CrdtGrowthQuery? query = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var m in _items)
            {
                yield return m;
            }
        }
#pragma warning restore CS1998
    }

    private sealed class RecordingAuditTrail : IAuditTrail
    {
        public List<AuditRecord> Records { get; } = new();
        private readonly object _lock = new();

        public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
        {
            lock (_lock) { Records.Add(record); }
            return ValueTask.CompletedTask;
        }

        public IAsyncEnumerable<AuditRecord> QueryAsync(
            AuditQuery query,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
