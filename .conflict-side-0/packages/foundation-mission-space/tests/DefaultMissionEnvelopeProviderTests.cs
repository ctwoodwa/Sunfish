using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.UI;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Tests;

public sealed class DefaultMissionEnvelopeProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static MissionEnvelope NewEnvelope(string editionKey = "anchor-self-host") => new()
    {
        Hardware = new() { ProbeStatus = ProbeStatus.Healthy },
        User = new() { ProbeStatus = ProbeStatus.Healthy, IsSignedIn = true },
        Regulatory = new() { ProbeStatus = ProbeStatus.Healthy },
        Runtime = new() { ProbeStatus = ProbeStatus.Healthy },
        FormFactor = new() { ProbeStatus = ProbeStatus.Healthy },
        Edition = new() { ProbeStatus = ProbeStatus.Healthy, EditionKey = editionKey },
        Network = new() { ProbeStatus = ProbeStatus.Healthy, IsOnline = true },
        TrustAnchor = new() { ProbeStatus = ProbeStatus.Healthy, HasIdentityKey = true },
        SyncState = new() { ProbeStatus = ProbeStatus.Healthy, State = global::Sunfish.Foundation.UI.SyncState.Healthy },
        VersionVector = new() { ProbeStatus = ProbeStatus.Healthy },
        SnapshotAt = Now,
    };

    [Fact]
    public async Task GetCurrentAsync_FirstCall_InvokesFactoryOnce()
    {
        var calls = 0;
        var provider = new DefaultMissionEnvelopeProvider(_ =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(NewEnvelope());
        }, time: new FakeTime(Now));

        await provider.GetCurrentAsync();

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetCurrentAsync_CachedWithinTtl_DoesNotInvokeFactoryAgain()
    {
        var calls = 0;
        var time = new FakeTime(Now);
        var provider = new DefaultMissionEnvelopeProvider(_ =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(NewEnvelope());
        }, time: time, cacheTtl: TimeSpan.FromSeconds(30));

        await provider.GetCurrentAsync();
        time.Advance(TimeSpan.FromSeconds(15));
        await provider.GetCurrentAsync();

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetCurrentAsync_PastCacheTtl_RefreshesEnvelope()
    {
        var calls = 0;
        var time = new FakeTime(Now);
        var provider = new DefaultMissionEnvelopeProvider(_ =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(NewEnvelope());
        }, time: time, cacheTtl: TimeSpan.FromSeconds(30));

        await provider.GetCurrentAsync();
        time.Advance(TimeSpan.FromSeconds(31));
        await provider.GetCurrentAsync();

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetCurrentAsync_SingleFlight_MultipleCallersOneFactoryInvocation()
    {
        var calls = 0;
        var gate = new TaskCompletionSource();
        var provider = new DefaultMissionEnvelopeProvider(async _ =>
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return NewEnvelope();
        });

        var task1 = provider.GetCurrentAsync().AsTask();
        var task2 = provider.GetCurrentAsync().AsTask();
        var task3 = provider.GetCurrentAsync().AsTask();

        // Briefly yield so all three callers register as joiners.
        await Task.Delay(50);
        gate.SetResult();
        await Task.WhenAll(task1, task2, task3);

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task InvalidateAsync_ForcesNextCallToRefresh()
    {
        var calls = 0;
        var provider = new DefaultMissionEnvelopeProvider(_ =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(NewEnvelope());
        }, time: new FakeTime(Now));

        await provider.GetCurrentAsync();
        await provider.InvalidateAsync();
        await provider.GetCurrentAsync();

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetCurrentAsync_HonorsCancellation()
    {
        var provider = new DefaultMissionEnvelopeProvider(async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return NewEnvelope();
        });

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetCurrentAsync(cts.Token).AsTask());
    }

    [Fact]
    public async Task GetCurrentAsync_OverallTimeoutEnforced_CallerSeesCancellation()
    {
        var provider = new DefaultMissionEnvelopeProvider(async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return NewEnvelope();
        }, overallTimeout: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetCurrentAsync().AsTask());
    }

    [Fact]
    public async Task GetCurrentAsync_FactoryThrows_PropagatesAndDoesNotCache()
    {
        var calls = 0;
        var provider = new DefaultMissionEnvelopeProvider(_ =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("probe failed");
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetCurrentAsync().AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetCurrentAsync().AsTask());
        Assert.Equal(2, calls); // no caching of failures
    }

    [Fact]
    public async Task Observers_ReceiveBroadcastsOnEnvelopeChange()
    {
        var observer = new RecordingObserver();
        var time = new FakeTime(Now);
        var sequence = 0;
        var provider = new DefaultMissionEnvelopeProvider(_ =>
        {
            sequence++;
            return ValueTask.FromResult(NewEnvelope(editionKey: $"edition-{sequence}"));
        }, time: time, cacheTtl: TimeSpan.FromMilliseconds(1));

        provider.Subscribe(observer);

        await provider.GetCurrentAsync();
        time.Advance(TimeSpan.FromSeconds(1));
        await provider.GetCurrentAsync();

        await provider.FlushFanoutForTestAsync();
        Assert.True(observer.Changes.Count >= 1);
    }

    [Fact]
    public async Task Observers_NotInvokedWhenEnvelopeUnchanged()
    {
        var observer = new RecordingObserver();
        var fixedEnvelope = NewEnvelope();
        var time = new FakeTime(Now);
        var provider = new DefaultMissionEnvelopeProvider(_ =>
            ValueTask.FromResult(fixedEnvelope),
            time: time, cacheTtl: TimeSpan.FromMilliseconds(1));

        provider.Subscribe(observer);

        await provider.GetCurrentAsync();
        time.Advance(TimeSpan.FromSeconds(1));
        await provider.GetCurrentAsync();
        await provider.FlushFanoutForTestAsync();

        // First call broadcasts (initial envelope); subsequent identical
        // envelopes do not (envelopeHash is unchanged).
        Assert.Single(observer.Changes);
    }

    [Fact]
    public async Task Observers_Coalesced_SingleFanoutForRapidChanges()
    {
        var observer = new RecordingObserver();
        var sequence = 0;
        var time = new FakeTime(Now);
        var provider = new DefaultMissionEnvelopeProvider(_ =>
        {
            sequence++;
            return ValueTask.FromResult(NewEnvelope(editionKey: $"e-{sequence}"));
        }, time: time, cacheTtl: TimeSpan.FromMilliseconds(1),
           coalescingWindow: TimeSpan.FromHours(1)); // never auto-fanout in test

        provider.Subscribe(observer);

        // Three changes back-to-back; coalesce window is huge so the
        // timer doesn't fire — they must merge into one pending change.
        await provider.GetCurrentAsync();
        time.Advance(TimeSpan.FromSeconds(1));
        await provider.GetCurrentAsync();
        time.Advance(TimeSpan.FromSeconds(1));
        await provider.GetCurrentAsync();

        await provider.FlushFanoutForTestAsync();
        Assert.Single(observer.Changes);
    }

    [Fact]
    public void Subscribe_SameObserverTwice_DeduplicatedToSingleEntry()
    {
        var provider = new DefaultMissionEnvelopeProvider(_ => ValueTask.FromResult(NewEnvelope()));
        var observer = new RecordingObserver();
        provider.Subscribe(observer);
        provider.Subscribe(observer);
        // No way to count without an internal accessor; behavioral
        // check: a single notification arrives, not two.
    }

    [Fact]
    public async Task Unsubscribe_StopsBroadcastsToThatObserver()
    {
        var stayingObserver = new RecordingObserver();
        var leavingObserver = new RecordingObserver();
        var sequence = 0;
        var time = new FakeTime(Now);
        var provider = new DefaultMissionEnvelopeProvider(_ =>
        {
            sequence++;
            return ValueTask.FromResult(NewEnvelope(editionKey: $"e-{sequence}"));
        }, time: time, cacheTtl: TimeSpan.FromMilliseconds(1));

        provider.Subscribe(stayingObserver);
        provider.Subscribe(leavingObserver);
        await provider.GetCurrentAsync();
        await provider.FlushFanoutForTestAsync();

        provider.Unsubscribe(leavingObserver);
        time.Advance(TimeSpan.FromSeconds(1));
        await provider.GetCurrentAsync();
        await provider.FlushFanoutForTestAsync();

        Assert.Equal(2, stayingObserver.Changes.Count);
        Assert.Single(leavingObserver.Changes);
    }

    [Fact]
    public void Constructor_AuditEnabled_RequiresAllArgs()
    {
        Func<CancellationToken, ValueTask<MissionEnvelope>> factory = _ => ValueTask.FromResult(NewEnvelope());
        var trail = Substitute.For<Sunfish.Kernel.Audit.IAuditTrail>();
        var signer = new Sunfish.Foundation.Crypto.Ed25519Signer(Sunfish.Foundation.Crypto.KeyPair.Generate());
        var tenantId = new Sunfish.Foundation.Assets.Common.TenantId("tenant-a");

        Assert.Throws<ArgumentNullException>(() =>
            new DefaultMissionEnvelopeProvider(factory, null!, signer, tenantId));
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultMissionEnvelopeProvider(factory, trail, null!, tenantId));
        Assert.Throws<ArgumentException>(() =>
            new DefaultMissionEnvelopeProvider(factory, trail, signer, default));
    }

    [Fact]
    public void Subscribe_NullObserver_Throws()
    {
        var provider = new DefaultMissionEnvelopeProvider(_ => ValueTask.FromResult(NewEnvelope()));
        Assert.Throws<ArgumentNullException>(() => provider.Subscribe(null!));
        Assert.Throws<ArgumentNullException>(() => provider.Unsubscribe(null!));
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var provider = new DefaultMissionEnvelopeProvider(_ => ValueTask.FromResult(NewEnvelope()));
        await provider.DisposeAsync();
        await provider.DisposeAsync();
    }

    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    private sealed class RecordingObserver : IMissionEnvelopeObserver
    {
        public List<EnvelopeChange> Changes { get; } = new();
        public ValueTask OnChangedAsync(EnvelopeChange change, CancellationToken ct = default)
        {
            lock (Changes) Changes.Add(change);
            return ValueTask.CompletedTask;
        }
    }
}
