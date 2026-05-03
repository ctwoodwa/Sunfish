using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class SecretRotationTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private const string TenantA = "tenant-a";

    [Fact]
    public async Task ResolveAsync_BeforeAnyRotation_ReturnsEmpty()
    {
        var store = new InMemorySharedSecretStore(new FakeTimeProvider(Now));
        var lookup = await store.ResolveAsync(TenantA);
        Assert.Null(lookup.Current);
        Assert.Null(lookup.PreviousInGrace);
    }

    [Fact]
    public async Task StageRotationAsync_FirstSecret_HasNoPrevious()
    {
        var store = new InMemorySharedSecretStore(new FakeTimeProvider(Now));
        await store.StageRotationAsync(TenantA, "secret-1");
        var lookup = await store.ResolveAsync(TenantA);
        Assert.Equal("secret-1", lookup.Current);
        Assert.Null(lookup.PreviousInGrace);
    }

    [Fact]
    public async Task StageRotationAsync_SecondRotation_PreviousInGraceWindow()
    {
        var time = new FakeTimeProvider(Now);
        var store = new InMemorySharedSecretStore(time);
        await store.StageRotationAsync(TenantA, "secret-1");

        time.Advance(TimeSpan.FromDays(90)); // routine rotation cadence per A1.12.1
        await store.StageRotationAsync(TenantA, "secret-2");

        var lookup = await store.ResolveAsync(TenantA);
        Assert.Equal("secret-2", lookup.Current);
        Assert.Equal("secret-1", lookup.PreviousInGrace);
        // Both should verify within grace.
        Assert.True(lookup.Matches("secret-1"));
        Assert.True(lookup.Matches("secret-2"));
        Assert.False(lookup.Matches("secret-stale"));
    }

    [Fact]
    public async Task StageRotationAsync_PastGraceWindow_DropsPrevious()
    {
        var time = new FakeTimeProvider(Now);
        var store = new InMemorySharedSecretStore(time);
        await store.StageRotationAsync(TenantA, "secret-1");
        time.Advance(TimeSpan.FromDays(90));
        await store.StageRotationAsync(TenantA, "secret-2");

        // Move past the 24h grace.
        time.Advance(TimeSpan.FromHours(25));
        var lookup = await store.ResolveAsync(TenantA);
        Assert.Equal("secret-2", lookup.Current);
        Assert.Null(lookup.PreviousInGrace);
        Assert.False(lookup.Matches("secret-1")); // old now invalid
        Assert.True(lookup.Matches("secret-2"));
    }

    [Fact]
    public async Task StageRotationAsync_ExactlyAtGraceBoundary_DropsPrevious()
    {
        var time = new FakeTimeProvider(Now);
        var store = new InMemorySharedSecretStore(time);
        await store.StageRotationAsync(TenantA, "secret-1");
        time.Advance(TimeSpan.FromDays(1));
        await store.StageRotationAsync(TenantA, "secret-2");

        // Advance exactly 24h — drops out of the grace window.
        time.Advance(TimeSpan.FromHours(24));
        var lookup = await store.ResolveAsync(TenantA);
        Assert.Null(lookup.PreviousInGrace);
    }

    [Fact]
    public async Task ResolveAsync_DifferentTenants_AreIndependent()
    {
        var store = new InMemorySharedSecretStore(new FakeTimeProvider(Now));
        await store.StageRotationAsync(TenantA, "secret-a");
        await store.StageRotationAsync("tenant-b", "secret-b");

        var a = await store.ResolveAsync(TenantA);
        var b = await store.ResolveAsync("tenant-b");
        Assert.Equal("secret-a", a.Current);
        Assert.Equal("secret-b", b.Current);
    }

    [Fact]
    public async Task ResolveAsync_TunableGrace_AppliesToTenant()
    {
        var time = new FakeTimeProvider(Now);
        var store = new InMemorySharedSecretStore(time, graceWindow: TimeSpan.FromMinutes(15));
        await store.StageRotationAsync(TenantA, "secret-1");
        time.Advance(TimeSpan.FromMinutes(1));
        await store.StageRotationAsync(TenantA, "secret-2");

        time.Advance(TimeSpan.FromMinutes(14));
        Assert.Equal("secret-1", (await store.ResolveAsync(TenantA)).PreviousInGrace);

        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Null((await store.ResolveAsync(TenantA)).PreviousInGrace);
    }

    [Fact]
    public async Task StageRotationAsync_NullArgs_Throw()
    {
        var store = new InMemorySharedSecretStore();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.StageRotationAsync(string.Empty, "x").AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.StageRotationAsync(TenantA, string.Empty).AsTask());
    }

    [Fact]
    public async Task ResolveAsync_HonorsCancellation()
    {
        var store = new InMemorySharedSecretStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.ResolveAsync(TenantA, cts.Token).AsTask());
    }

    [Fact]
    public void SharedSecretLookup_Matches_RejectsNullCandidate()
    {
        var lookup = new SharedSecretLookup { Current = "x", PreviousInGrace = null };
        Assert.False(lookup.Matches(null));
        Assert.False(lookup.Matches(string.Empty));
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
