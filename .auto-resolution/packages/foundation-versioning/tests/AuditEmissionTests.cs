using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Versioning.Audit;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Versioning.Tests;

internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;
    public FakeTimeProvider(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}

public sealed class AuditEmissionTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static (InMemoryVersionVectorIncompatibility svc, IAuditTrail trail, FakeTimeProvider time) NewAuditEnabled()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        var time = new FakeTimeProvider(Now);
        var svc = new InMemoryVersionVectorIncompatibility(trail, signer, TenantA, time);
        return (svc, trail, time);
    }

    [Fact]
    public async Task RecordRejectionAsync_AuditEnabled_EmitsRecord()
    {
        var (svc, trail, _) = NewAuditEnabled();
        var verdict = new VersionVectorVerdict(VerdictKind.Incompatible, FailedRule.SchemaEpochMismatch, "epoch 7 vs 9");

        await svc.RecordRejectionAsync("node-A", verdict);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.VersionVectorIncompatibilityRejected) && r.TenantId.Equals(TenantA)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordLegacyReconnectAsync_AuditEnabled_EmitsRecord()
    {
        var (svc, trail, _) = NewAuditEnabled();

        await svc.RecordLegacyReconnectAsync("node-B", "1.3.0", kernelMinorLag: 3);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.LegacyDeviceReconnected)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordRejectionAsync_AuditDisabled_DoesNotEmit()
    {
        var time = new FakeTimeProvider(Now);
        var svc = new InMemoryVersionVectorIncompatibility(time);
        var verdict = new VersionVectorVerdict(VerdictKind.Incompatible, FailedRule.ChannelOrdering, "stable vs nightly");

        // Should complete without throwing — and no audit emitter was registered.
        await svc.RecordRejectionAsync("node-A", verdict);
    }

    [Fact]
    public async Task RecordRejectionAsync_DedupsWithin1HourWindow()
    {
        var (svc, trail, time) = NewAuditEnabled();
        var verdict = new VersionVectorVerdict(VerdictKind.Incompatible, FailedRule.SchemaEpochMismatch, "epoch 7 vs 9");

        // First emission at T=0.
        await svc.RecordRejectionAsync("node-A", verdict);
        // Storm: 5 retries within 30 minutes — all deduped.
        for (var i = 0; i < 5; i++)
        {
            time.Advance(TimeSpan.FromMinutes(5));
            await svc.RecordRejectionAsync("node-A", verdict);
        }

        await trail.Received(1).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordRejectionAsync_RefiresAfter1HourWindow()
    {
        var (svc, trail, time) = NewAuditEnabled();
        var verdict = new VersionVectorVerdict(VerdictKind.Incompatible, FailedRule.SchemaEpochMismatch, "epoch 7 vs 9");

        await svc.RecordRejectionAsync("node-A", verdict);
        time.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));
        await svc.RecordRejectionAsync("node-A", verdict);

        await trail.Received(2).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordRejectionAsync_DifferentRuleEmitsSeparately()
    {
        var (svc, trail, _) = NewAuditEnabled();
        var v1 = new VersionVectorVerdict(VerdictKind.Incompatible, FailedRule.SchemaEpochMismatch, "epoch 7 vs 9");
        var v2 = new VersionVectorVerdict(VerdictKind.Incompatible, FailedRule.ChannelOrdering, "stable vs nightly");

        await svc.RecordRejectionAsync("node-A", v1);
        await svc.RecordRejectionAsync("node-A", v2);

        await trail.Received(2).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordRejectionAsync_DifferentNodeEmitsSeparately()
    {
        var (svc, trail, _) = NewAuditEnabled();
        var verdict = new VersionVectorVerdict(VerdictKind.Incompatible, FailedRule.SchemaEpochMismatch, "epoch 7 vs 9");

        await svc.RecordRejectionAsync("node-A", verdict);
        await svc.RecordRejectionAsync("node-B", verdict);

        await trail.Received(2).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordLegacyReconnectAsync_DedupsWithin24HourWindow()
    {
        var (svc, trail, time) = NewAuditEnabled();

        await svc.RecordLegacyReconnectAsync("node-A", "1.3.0", kernelMinorLag: 3);
        for (var i = 0; i < 10; i++)
        {
            time.Advance(TimeSpan.FromHours(2));
            await svc.RecordLegacyReconnectAsync("node-A", "1.3.0", kernelMinorLag: 3);
        }

        await trail.Received(1).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordLegacyReconnectAsync_RefiresAfter24HourWindow()
    {
        var (svc, trail, time) = NewAuditEnabled();

        await svc.RecordLegacyReconnectAsync("node-A", "1.3.0", kernelMinorLag: 3);
        time.Advance(TimeSpan.FromHours(24).Add(TimeSpan.FromSeconds(1)));
        await svc.RecordLegacyReconnectAsync("node-A", "1.3.0", kernelMinorLag: 3);

        await trail.Received(2).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordLegacyReconnectAsync_DifferentLagEmitsSeparately()
    {
        var (svc, trail, _) = NewAuditEnabled();

        await svc.RecordLegacyReconnectAsync("node-A", "1.3.0", kernelMinorLag: 3);
        await svc.RecordLegacyReconnectAsync("node-A", "1.2.0", kernelMinorLag: 4);

        await trail.Received(2).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordRejectionAsync_RejectsCompatibleVerdict()
    {
        var (svc, _, _) = NewAuditEnabled();
        var compat = new VersionVectorVerdict(VerdictKind.Compatible, null, null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.RecordRejectionAsync("node-A", compat).AsTask());
    }

    [Fact]
    public async Task RecordRejectionAsync_RejectsEmptyRemoteNodeId()
    {
        var (svc, _, _) = NewAuditEnabled();
        var verdict = new VersionVectorVerdict(VerdictKind.Incompatible, FailedRule.SchemaEpochMismatch, "x");

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.RecordRejectionAsync(string.Empty, verdict).AsTask());
    }

    [Fact]
    public void Constructor_AuditEnabled_RequiresAllArgs()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());

        Assert.Throws<ArgumentNullException>(() => new InMemoryVersionVectorIncompatibility(null!, signer, TenantA));
        Assert.Throws<ArgumentNullException>(() => new InMemoryVersionVectorIncompatibility(trail, null!, TenantA));
        Assert.Throws<ArgumentException>(() => new InMemoryVersionVectorIncompatibility(trail, signer, default));
    }

    [Fact]
    public void AuditPayloads_IncompatibilityRejected_ShapeIsAlphabetized()
    {
        var payload = VersionVectorAuditPayloads.IncompatibilityRejected("node-A", FailedRule.SchemaEpochMismatch, "epoch 7 vs 9");

        Assert.Equal("SchemaEpochMismatch", payload.Body["failed_rule"]);
        Assert.Equal("epoch 7 vs 9", payload.Body["failed_rule_detail"]);
        Assert.Equal("node-A", payload.Body["remote_node_id"]);
        Assert.Equal(3, payload.Body.Count);
    }

    [Fact]
    public void AuditPayloads_LegacyReconnected_ShapeIsAlphabetized()
    {
        var payload = VersionVectorAuditPayloads.LegacyReconnected("node-A", "1.3.0", 3);

        Assert.Equal(3, payload.Body["kernel_minor_lag"]);
        Assert.Equal("1.3.0", payload.Body["remote_kernel"]);
        Assert.Equal("node-A", payload.Body["remote_node_id"]);
        Assert.Equal(3, payload.Body.Count);
    }
}
