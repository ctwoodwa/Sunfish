using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Tests;

public sealed class ForceEnablePolicyResolverTests
{
    [Theory]
    [InlineData(DimensionChangeKind.Hardware, ForceEnablePolicy.NotOverridable)]
    [InlineData(DimensionChangeKind.Runtime, ForceEnablePolicy.NotOverridable)]
    [InlineData(DimensionChangeKind.Regulatory, ForceEnablePolicy.OverridableWithCaveat)]
    [InlineData(DimensionChangeKind.Edition, ForceEnablePolicy.OverridableWithCaveat)]
    [InlineData(DimensionChangeKind.User, ForceEnablePolicy.Overridable)]
    [InlineData(DimensionChangeKind.Network, ForceEnablePolicy.Overridable)]
    [InlineData(DimensionChangeKind.TrustAnchor, ForceEnablePolicy.Overridable)]
    [InlineData(DimensionChangeKind.SyncState, ForceEnablePolicy.Overridable)]
    [InlineData(DimensionChangeKind.VersionVector, ForceEnablePolicy.Overridable)]
    [InlineData(DimensionChangeKind.FormFactor, ForceEnablePolicy.Overridable)]
    public void ResolveFor_AllTenDimensions_PerA1_9(DimensionChangeKind dimension, ForceEnablePolicy expected)
    {
        Assert.Equal(expected, ForceEnablePolicyResolver.ResolveFor(dimension));
    }

    [Fact]
    public void IsForceEnablePermitted_NotOverridableDimensions_ReturnsFalse()
    {
        Assert.False(ForceEnablePolicyResolver.IsForceEnablePermitted(DimensionChangeKind.Hardware));
        Assert.False(ForceEnablePolicyResolver.IsForceEnablePermitted(DimensionChangeKind.Runtime));
    }

    [Fact]
    public void IsForceEnablePermitted_OtherDimensions_ReturnsTrue()
    {
        foreach (var d in new[]
        {
            DimensionChangeKind.User, DimensionChangeKind.Regulatory,
            DimensionChangeKind.Edition, DimensionChangeKind.Network,
            DimensionChangeKind.TrustAnchor, DimensionChangeKind.SyncState,
            DimensionChangeKind.VersionVector, DimensionChangeKind.FormFactor,
        })
        {
            Assert.True(ForceEnablePolicyResolver.IsForceEnablePermitted(d));
        }
    }

    [Fact]
    public void ResolveFor_UnknownEnumValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForceEnablePolicyResolver.ResolveFor((DimensionChangeKind)999));
    }
}

public sealed class DefaultFeatureForceEnableSurfaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId TenantA = new("tenant-a");

    private static FeatureForceEnableRequest NewRequest(DimensionChangeKind dim = DimensionChangeKind.Network) =>
        new()
        {
            FeatureKey = "feature.x",
            Dimension = dim,
            OperatorPrincipalId = "operator-1",
            ExpiresAt = null,
            Reason = "Manual override for migration",
        };

    [Fact]
    public async Task RequestAsync_OverridableDimension_ReturnsRecord()
    {
        var surface = new DefaultFeatureForceEnableSurface(new FakeTime(Now));
        var record = await surface.RequestAsync(NewRequest());
        Assert.Equal("feature.x", record.FeatureKey);
        Assert.Equal(DimensionChangeKind.Network, record.Dimension);
        Assert.Equal(Now, record.RequestedAt);
    }

    [Fact]
    public async Task RequestAsync_NotOverridableDimension_ThrowsAndDoesNotPersist()
    {
        var surface = new DefaultFeatureForceEnableSurface(new FakeTime(Now));
        await Assert.ThrowsAsync<ForceEnableNotPermittedException>(() =>
            surface.RequestAsync(NewRequest(DimensionChangeKind.Hardware)).AsTask());

        var resolved = await surface.ResolveAsync("feature.x", DimensionChangeKind.Hardware);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task RequestAsync_RuntimeDimension_AlsoNotOverridable()
    {
        var surface = new DefaultFeatureForceEnableSurface(new FakeTime(Now));
        await Assert.ThrowsAsync<ForceEnableNotPermittedException>(() =>
            surface.RequestAsync(NewRequest(DimensionChangeKind.Runtime)).AsTask());
    }

    [Theory]
    [InlineData(DimensionChangeKind.Regulatory)]
    [InlineData(DimensionChangeKind.Edition)]
    public async Task RequestAsync_OverridableWithCaveat_PersistsRecord(DimensionChangeKind dim)
    {
        var surface = new DefaultFeatureForceEnableSurface(new FakeTime(Now));
        var record = await surface.RequestAsync(NewRequest(dim));
        Assert.Equal(dim, record.Dimension);
    }

    [Fact]
    public async Task ResolveAsync_AfterRequest_ReturnsLastRecord()
    {
        var surface = new DefaultFeatureForceEnableSurface(new FakeTime(Now));
        await surface.RequestAsync(NewRequest());
        var resolved = await surface.ResolveAsync("feature.x", DimensionChangeKind.Network);
        Assert.NotNull(resolved);
        Assert.Equal("operator-1", resolved!.OperatorPrincipalId);
    }

    [Fact]
    public async Task ResolveAsync_UnknownFeatureKey_ReturnsNull()
    {
        var surface = new DefaultFeatureForceEnableSurface(new FakeTime(Now));
        var resolved = await surface.ResolveAsync("never-registered", DimensionChangeKind.Network);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveAsync_ExpiredRecord_ReturnsNullAndEvicts()
    {
        var time = new FakeTime(Now);
        var surface = new DefaultFeatureForceEnableSurface(time);
        await surface.RequestAsync(NewRequest() with { ExpiresAt = Now.AddMinutes(5) });

        time.Advance(TimeSpan.FromMinutes(6));
        var first = await surface.ResolveAsync("feature.x", DimensionChangeKind.Network);
        var second = await surface.ResolveAsync("feature.x", DimensionChangeKind.Network);

        Assert.Null(first);
        Assert.Null(second); // eviction stuck
    }

    [Fact]
    public async Task RevokeAsync_RemovesRecord()
    {
        var surface = new DefaultFeatureForceEnableSurface(new FakeTime(Now));
        await surface.RequestAsync(NewRequest());
        await surface.RevokeAsync("feature.x", DimensionChangeKind.Network);
        Assert.Null(await surface.ResolveAsync("feature.x", DimensionChangeKind.Network));
    }

    [Fact]
    public async Task RevokeAsync_UnknownKey_DoesNotThrow()
    {
        var surface = new DefaultFeatureForceEnableSurface(new FakeTime(Now));
        await surface.RevokeAsync("never-registered", DimensionChangeKind.Network);
        // No exception → success.
    }

    // === Audit emission ===

    [Fact]
    public async Task RequestAsync_AuditEnabled_EmitsFeatureForceEnabled()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        var surface = new DefaultFeatureForceEnableSurface(trail, signer, TenantA, new FakeTime(Now));

        await surface.RequestAsync(NewRequest());

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.FeatureForceEnabled)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestAsync_NotOverridable_EmitsFeatureForceEnableRejected()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        var surface = new DefaultFeatureForceEnableSurface(trail, signer, TenantA, new FakeTime(Now));

        await Assert.ThrowsAsync<ForceEnableNotPermittedException>(() =>
            surface.RequestAsync(NewRequest(DimensionChangeKind.Hardware)).AsTask());

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.FeatureForceEnableRejected)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_AuditEnabled_EmitsFeatureForceRevoked()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        var surface = new DefaultFeatureForceEnableSurface(trail, signer, TenantA, new FakeTime(Now));

        await surface.RequestAsync(NewRequest());
        trail.ClearReceivedCalls();
        await surface.RevokeAsync("feature.x", DimensionChangeKind.Network);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.FeatureForceRevoked)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_UnknownKey_DoesNotEmit()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        var surface = new DefaultFeatureForceEnableSurface(trail, signer, TenantA, new FakeTime(Now));

        await surface.RevokeAsync("never-registered", DimensionChangeKind.Network);

        await trail.DidNotReceive().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.FeatureForceRevoked)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestAsync_AuditDisabled_DoesNotEmit()
    {
        var surface = new DefaultFeatureForceEnableSurface(new FakeTime(Now));
        // Just exercises the no-op audit path; no exception, no audit trail.
        await surface.RequestAsync(NewRequest());
        await Assert.ThrowsAsync<ForceEnableNotPermittedException>(() =>
            surface.RequestAsync(NewRequest(DimensionChangeKind.Hardware)).AsTask());
    }

    [Fact]
    public void Constructor_AuditEnabled_RequiresAllArgs()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());

        Assert.Throws<ArgumentNullException>(() =>
            new DefaultFeatureForceEnableSurface(null!, signer, TenantA));
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultFeatureForceEnableSurface(trail, null!, TenantA));
        Assert.Throws<ArgumentException>(() =>
            new DefaultFeatureForceEnableSurface(trail, signer, default));
    }

    [Fact]
    public async Task RequestAsync_NullArg_Throws()
    {
        var surface = new DefaultFeatureForceEnableSurface();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            surface.RequestAsync(null!).AsTask());
    }

    [Fact]
    public async Task RequestAsync_HonorsCancellation()
    {
        var surface = new DefaultFeatureForceEnableSurface();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            surface.RequestAsync(NewRequest(), cts.Token).AsTask());
    }

    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
