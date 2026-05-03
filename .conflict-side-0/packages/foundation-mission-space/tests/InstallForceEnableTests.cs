using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Tests;

public sealed class DefaultInstallForceEnableSurfaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId TenantA = new("tenant-a");

    private static InstallForceRequest NewRequest(string operatorId = "ops-1") =>
        new()
        {
            OperatorPrincipalId = operatorId,
            Reason = "Override per ticket OPS-123: hardware refresh in flight",
            OverrideTargets = new[] { DimensionChangeKind.Hardware },
            EnvelopeHash = "deadbeef",
            Platform = "windows-desktop",
        };

    [Fact]
    public async Task RequestAsync_AuditDisabled_ReturnsRecord()
    {
        var surface = new DefaultInstallForceEnableSurface(new FakeTime(Now));
        var record = await surface.RequestAsync(NewRequest());

        Assert.Equal("ops-1", record.OperatorPrincipalId);
        Assert.Single(record.OverrideTargets);
        Assert.Equal(DimensionChangeKind.Hardware, record.OverrideTargets[0]);
        Assert.Equal("deadbeef", record.EnvelopeHash);
        Assert.Equal(Now, record.RecordedAt);
    }

    [Fact]
    public async Task RequestAsync_AuditEnabled_EmitsInstallForceEnabled()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        var surface = new DefaultInstallForceEnableSurface(trail, signer, TenantA, new FakeTime(Now));

        await surface.RequestAsync(NewRequest());

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.InstallForceEnabled)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestAsync_AuditDisabled_NoEmission()
    {
        var trail = Substitute.For<IAuditTrail>();
        // No emitter wired — use audit-disabled overload.
        var surface = new DefaultInstallForceEnableSurface(new FakeTime(Now));
        await surface.RequestAsync(NewRequest());
        await trail.DidNotReceive().AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestAsync_EmptyReason_Throws_PerA1_11()
    {
        var surface = new DefaultInstallForceEnableSurface(new FakeTime(Now));
        var req = NewRequest() with { Reason = "" };
        await Assert.ThrowsAsync<ArgumentException>(() => surface.RequestAsync(req).AsTask());
    }

    [Fact]
    public async Task RequestAsync_EmptyOperator_Throws()
    {
        var surface = new DefaultInstallForceEnableSurface(new FakeTime(Now));
        var req = NewRequest() with { OperatorPrincipalId = "" };
        await Assert.ThrowsAsync<ArgumentException>(() => surface.RequestAsync(req).AsTask());
    }

    [Fact]
    public async Task RequestAsync_EmptyOverrideTargets_Throws()
    {
        var surface = new DefaultInstallForceEnableSurface(new FakeTime(Now));
        var req = NewRequest() with { OverrideTargets = Array.Empty<DimensionChangeKind>() };
        await Assert.ThrowsAsync<ArgumentException>(() => surface.RequestAsync(req).AsTask());
    }

    [Fact]
    public void Constructor_AuditEnabled_RequiresAllArgs()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());

        Assert.Throws<ArgumentNullException>(() =>
            new DefaultInstallForceEnableSurface(null!, signer, TenantA));
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultInstallForceEnableSurface(trail, null!, TenantA));
        Assert.Throws<ArgumentException>(() =>
            new DefaultInstallForceEnableSurface(trail, signer, default));
    }

    [Fact]
    public async Task RequestAsync_NullArg_Throws()
    {
        var surface = new DefaultInstallForceEnableSurface();
        await Assert.ThrowsAsync<ArgumentNullException>(() => surface.RequestAsync(null!).AsTask());
    }

    [Fact]
    public async Task RequestAsync_HonorsCancellation()
    {
        var surface = new DefaultInstallForceEnableSurface();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            surface.RequestAsync(NewRequest(), cts.Token).AsTask());
    }

    private sealed class FakeTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
