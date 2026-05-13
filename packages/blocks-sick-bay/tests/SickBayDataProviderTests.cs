using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.SickBay;
using Sunfish.Foundation.UI;
using Xunit;

namespace Sunfish.Blocks.SickBay.Tests;

public class SickBayDataProviderTests
{
    private static SickBayDataProvider Build(
        SickBayOptions? options = null,
        IMissionEnvelopeProvider? envelopeProvider = null) =>
        new SickBayDataProvider(
            Options.Create(options ?? new SickBayOptions()),
            envelopeProvider);

    [Fact]
    public async Task GetSnapshotAsync_EmptyOptions_ReturnsEmptyPharmacyAndIdleMedevac()
    {
        var snapshot = await Build().GetSnapshotAsync(new TenantId("alpha"));

        Assert.Empty(snapshot.Pharmacy);
        Assert.Empty(snapshot.Lab);
        Assert.Equal(MedevacState.Idle, snapshot.MedevacState);
        Assert.Equal(AtmosphereHealth.Unknown, snapshot.Atmosphere.OverallHealth);
        Assert.False(snapshot.Atmosphere.ForceEnableActive);
    }

    [Fact]
    public async Task GetSnapshotAsync_RegisteredPurposes_ProjectsToPharmacyRows()
    {
        var options = new SickBayOptions()
            .RegisterPurpose("recovery-key", "Recovery Key")
            .RegisterPurpose("vendor-tin", "Vendor TIN");

        var snapshot = await Build(options).GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(2, snapshot.Pharmacy.Count);
        var purposes = snapshot.Pharmacy.Select(p => p.FieldPurpose).ToHashSet();
        Assert.Contains("recovery-key", purposes);
        Assert.Contains("vendor-tin", purposes);
    }

    [Fact]
    public async Task GetSnapshotAsync_PharmacyEntries_UseSuppressedRecordCountByDefault()
    {
        var options = new SickBayOptions().RegisterPurpose("recovery-key", "Recovery Key");

        var snapshot = await Build(options).GetSnapshotAsync(new TenantId("alpha"));

        Assert.All(snapshot.Pharmacy, entry =>
        {
            Assert.True(entry.RecordCount.IsSuppressed);
            Assert.Equal(RotationHealth.Current, entry.RotationStatus);
            Assert.False(entry.HasCompromiseFlag);
        });
    }

    [Fact]
    public async Task GetSnapshotAsync_FriendlyName_IsTakenFromOptionsValue()
    {
        var options = new SickBayOptions()
            .RegisterPurpose("recovery-key", "Recovery Key (operator)");

        var snapshot = await Build(options).GetSnapshotAsync(new TenantId("alpha"));

        var entry = Assert.Single(snapshot.Pharmacy);
        Assert.Equal("Recovery Key (operator)", entry.FriendlyName);
    }

    [Fact]
    public async Task GetSnapshotAsync_Cancellation_ThrowsOperationCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Build().GetSnapshotAsync(new TenantId("alpha"), cts.Token));
    }

    [Fact]
    public async Task SubscribeSnapshotAsync_EmitsInitialSnapshot()
    {
        var options = new SickBayOptions { FallbackPollingInterval = TimeSpan.Zero };
        using var cts = new CancellationTokenSource();
        var enumerator = Build(options).SubscribeSnapshotAsync(new TenantId("alpha"), cts.Token).GetAsyncEnumerator();
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
    /// W#54 Phase 2 H4 (load-bearing) — <see cref="SickBayDataProvider"/>
    /// MUST NOT depend on <c>Sunfish.Foundation.Recovery.IFieldDecryptor</c>.
    /// Per ADR 0046-A2 §4 + ADR 0082 §Trust impact.
    /// </summary>
    [Fact]
    public void SickBayDataProvider_DoesNotReference_IFieldDecryptor()
    {
        const string ForbiddenName = "IFieldDecryptor";
        var assembly = typeof(SickBayDataProvider).Assembly;

        var referencedAssemblies = assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();
        Assert.DoesNotContain("Sunfish.Foundation.Recovery", referencedAssemblies);

        var providerType = typeof(SickBayDataProvider);
        const BindingFlags AllMembers =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        foreach (var field in providerType.GetFields(AllMembers))
            AssertNotForbidden(field.FieldType, $"field {field.Name}", ForbiddenName);

        foreach (var ctor in providerType.GetConstructors(AllMembers))
        foreach (var p in ctor.GetParameters())
            AssertNotForbidden(p.ParameterType, $"ctor parameter {p.Name}", ForbiddenName);

        foreach (var method in providerType.GetMethods(AllMembers))
        {
            AssertNotForbidden(method.ReturnType, $"return of {method.Name}", ForbiddenName);
            foreach (var p in method.GetParameters())
                AssertNotForbidden(p.ParameterType, $"parameter {p.Name} of {method.Name}", ForbiddenName);
            var body = method.GetMethodBody();
            if (body is null) continue;
            foreach (var local in body.LocalVariables)
                AssertNotForbidden(local.LocalType, $"local in {method.Name}", ForbiddenName);
        }
    }

    private static void AssertNotForbidden(Type type, string site, string forbiddenName)
    {
        Assert.DoesNotContain(forbiddenName, type.FullName ?? string.Empty);
        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
                AssertNotForbidden(arg, site + " (generic arg)", forbiddenName);
        }
    }

    // ── Phase 2b — AtmosphereHealth enum sentinel ─────────────────────────────

    [Fact]
    public void AtmosphereHealth_Unknown_is_ordinal_zero()
    {
        // ADR 0082-A1.3: default(AtmosphereHealth) MUST be Unknown so that
        // zero-initialized structs never silently present as Green.
        Assert.Equal(0, (int)AtmosphereHealth.Unknown);
        Assert.Equal(AtmosphereHealth.Unknown, default(AtmosphereHealth));
    }

    // ── Phase 2b — Mission Envelope atmosphere projection ─────────────────────

    private static MissionEnvelope AllHealthy() => new()
    {
        Hardware      = new() { ProbeStatus = ProbeStatus.Healthy },
        User          = new() { ProbeStatus = ProbeStatus.Healthy, IsSignedIn = true },
        Regulatory    = new() { ProbeStatus = ProbeStatus.Healthy },
        Runtime       = new() { ProbeStatus = ProbeStatus.Healthy },
        FormFactor    = new() { ProbeStatus = ProbeStatus.Healthy },
        Edition       = new() { ProbeStatus = ProbeStatus.Healthy, EditionKey = "anchor-self-host" },
        Network       = new() { ProbeStatus = ProbeStatus.Healthy, IsOnline = true },
        TrustAnchor   = new() { ProbeStatus = ProbeStatus.Healthy, HasIdentityKey = true },
        SyncState     = new() { ProbeStatus = ProbeStatus.Healthy, State = SyncState.Healthy },
        VersionVector = new() { ProbeStatus = ProbeStatus.Healthy },
        SnapshotAt    = DateTimeOffset.UtcNow,
    };

    private static IMissionEnvelopeProvider ProviderFor(MissionEnvelope envelope)
    {
        var provider = Substitute.For<IMissionEnvelopeProvider>();
        provider.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(envelope));
        return provider;
    }

    [Fact]
    public async Task GetSnapshotAsync_WithAllHealthyProbes_ReturnsGreen()
    {
        var snapshot = await Build(envelopeProvider: ProviderFor(AllHealthy()))
            .GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(AtmosphereHealth.Green, snapshot.Atmosphere.OverallHealth);
        Assert.Equal(0, snapshot.Atmosphere.WarningProbeCount);
        Assert.Equal(0, snapshot.Atmosphere.CriticalProbeCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithOneStaleProbe_ReturnsYellow()
    {
        var envelope = AllHealthy() with
        {
            Hardware = new() { ProbeStatus = ProbeStatus.Stale },
        };
        var snapshot = await Build(envelopeProvider: ProviderFor(envelope))
            .GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(AtmosphereHealth.Yellow, snapshot.Atmosphere.OverallHealth);
        Assert.Equal(1, snapshot.Atmosphere.WarningProbeCount);
        Assert.Equal(0, snapshot.Atmosphere.CriticalProbeCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithOnePartiallyDegradedProbe_ReturnsYellow()
    {
        var envelope = AllHealthy() with
        {
            Regulatory = new() { ProbeStatus = ProbeStatus.PartiallyDegraded },
        };
        var snapshot = await Build(envelopeProvider: ProviderFor(envelope))
            .GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(AtmosphereHealth.Yellow, snapshot.Atmosphere.OverallHealth);
        Assert.Equal(1, snapshot.Atmosphere.WarningProbeCount);
        Assert.Equal(0, snapshot.Atmosphere.CriticalProbeCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithOneFailedProbe_ReturnsOrange()
    {
        var envelope = AllHealthy() with
        {
            Network = new() { ProbeStatus = ProbeStatus.Failed, IsOnline = false },
        };
        var snapshot = await Build(envelopeProvider: ProviderFor(envelope))
            .GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(AtmosphereHealth.Orange, snapshot.Atmosphere.OverallHealth);
        Assert.Equal(0, snapshot.Atmosphere.WarningProbeCount);
        Assert.Equal(1, snapshot.Atmosphere.CriticalProbeCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithOneUnreachableProbe_ReturnsOrange()
    {
        var envelope = AllHealthy() with
        {
            TrustAnchor = new() { ProbeStatus = ProbeStatus.Unreachable, HasIdentityKey = false },
        };
        var snapshot = await Build(envelopeProvider: ProviderFor(envelope))
            .GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(AtmosphereHealth.Orange, snapshot.Atmosphere.OverallHealth);
        Assert.Equal(0, snapshot.Atmosphere.WarningProbeCount);
        Assert.Equal(1, snapshot.Atmosphere.CriticalProbeCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithTwoCriticalProbes_ReturnsOrange()
    {
        // ADR 0082-A1.2.2: CriticalProbeCount 1–2 → Orange; 3+ → Red.
        var envelope = AllHealthy() with
        {
            Network     = new() { ProbeStatus = ProbeStatus.Unreachable, IsOnline = false },
            TrustAnchor = new() { ProbeStatus = ProbeStatus.Failed, HasIdentityKey = false },
        };
        var snapshot = await Build(envelopeProvider: ProviderFor(envelope))
            .GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(AtmosphereHealth.Orange, snapshot.Atmosphere.OverallHealth);
        Assert.Equal(0, snapshot.Atmosphere.WarningProbeCount);
        Assert.Equal(2, snapshot.Atmosphere.CriticalProbeCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithThreeCriticalProbes_ReturnsRed()
    {
        // ADR 0082-A1.2.2: CriticalProbeCount >= 3 → Red.
        var envelope = AllHealthy() with
        {
            Network       = new() { ProbeStatus = ProbeStatus.Failed, IsOnline = false },
            TrustAnchor   = new() { ProbeStatus = ProbeStatus.Unreachable, HasIdentityKey = false },
            SyncState     = new() { ProbeStatus = ProbeStatus.Failed, State = SyncState.Healthy },
        };
        var snapshot = await Build(envelopeProvider: ProviderFor(envelope))
            .GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(AtmosphereHealth.Red, snapshot.Atmosphere.OverallHealth);
        Assert.Equal(0, snapshot.Atmosphere.WarningProbeCount);
        Assert.Equal(3, snapshot.Atmosphere.CriticalProbeCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithNullProvider_ReturnsUnknown()
    {
        var snapshot = await Build(envelopeProvider: null)
            .GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(AtmosphereHealth.Unknown, snapshot.Atmosphere.OverallHealth);
        Assert.Equal(0, snapshot.Atmosphere.WarningProbeCount);
        Assert.Equal(0, snapshot.Atmosphere.CriticalProbeCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithWarningAndOneCritical_ReturnsOrange()
    {
        var envelope = AllHealthy() with
        {
            Hardware = new() { ProbeStatus = ProbeStatus.Stale },
            Network  = new() { ProbeStatus = ProbeStatus.Failed, IsOnline = false },
        };
        var snapshot = await Build(envelopeProvider: ProviderFor(envelope))
            .GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(AtmosphereHealth.Orange, snapshot.Atmosphere.OverallHealth);
        Assert.Equal(1, snapshot.Atmosphere.WarningProbeCount);
        Assert.Equal(1, snapshot.Atmosphere.CriticalProbeCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithWarningsAndCriticals_PrefersCriticalClassification()
    {
        // ADR 0082-A1.2.2: critical count drives classification when both exist;
        // 3+ criticals → Red regardless of how many warnings are also present.
        var envelope = AllHealthy() with
        {
            Hardware    = new() { ProbeStatus = ProbeStatus.Stale },
            Regulatory  = new() { ProbeStatus = ProbeStatus.PartiallyDegraded },
            Network     = new() { ProbeStatus = ProbeStatus.Failed, IsOnline = false },
            TrustAnchor = new() { ProbeStatus = ProbeStatus.Unreachable, HasIdentityKey = false },
            SyncState   = new() { ProbeStatus = ProbeStatus.Failed, State = SyncState.Healthy },
        };
        var snapshot = await Build(envelopeProvider: ProviderFor(envelope))
            .GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(AtmosphereHealth.Red, snapshot.Atmosphere.OverallHealth);
        Assert.Equal(2, snapshot.Atmosphere.WarningProbeCount);
        Assert.Equal(3, snapshot.Atmosphere.CriticalProbeCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithThrowingProvider_ReturnsUnknown()
    {
        var provider = Substitute.For<IMissionEnvelopeProvider>();
        provider.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns<ValueTask<MissionEnvelope>>(_ => throw new InvalidOperationException("provider fault"));

        var snapshot = await Build(envelopeProvider: provider)
            .GetSnapshotAsync(new TenantId("alpha"));

        Assert.Equal(AtmosphereHealth.Unknown, snapshot.Atmosphere.OverallHealth);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithThrowingProvider_OperationCancelledPropagates()
    {
        var provider = Substitute.For<IMissionEnvelopeProvider>();
        provider.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns<ValueTask<MissionEnvelope>>(_ => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Build(envelopeProvider: provider).GetSnapshotAsync(new TenantId("alpha")));
    }

    [Fact]
    public async Task GetSnapshotAsync_UsesSingleEnvelopeReadPerInvocation()
    {
        // Pins the no-flicker invariant: when the ValueTask resolves synchronously
        // the snapshot carries derived health; Unknown is not emitted transiently.
        var provider = Substitute.For<IMissionEnvelopeProvider>();
        provider.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(AllHealthy()));

        var snapshot = await Build(envelopeProvider: provider)
            .GetSnapshotAsync(new TenantId("alpha"));

        // Exactly one read per invocation (not cached across calls from Phase 2 polling).
        await provider.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        Assert.Equal(AtmosphereHealth.Green, snapshot.Atmosphere.OverallHealth);
    }

    // ── Phase 2b — Observer-driven SubscribeSnapshotAsync ─────────────────────

    private static EnvelopeChange MakeChange() => new EnvelopeChange
    {
        Current = AllHealthy(),
        ChangedDimensions = new[] { DimensionChangeKind.Network },
        Severity = EnvelopeChangeSeverity.Warning,
    };

    [Fact]
    public async Task SubscribeSnapshotAsync_ReEmitsOnObserverChange()
    {
        var provider = Substitute.For<IMissionEnvelopeProvider>();
        provider.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(AllHealthy()));

        // TCS-based capture: deterministic, no arbitrary sleep.
        var observerRegistered = new TaskCompletionSource<IMissionEnvelopeObserver>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        provider.When(p => p.Subscribe(Arg.Any<IMissionEnvelopeObserver>()))
            .Do(ci => observerRegistered.TrySetResult(ci.Arg<IMissionEnvelopeObserver>()!));

        var opts = new SickBayOptions { FallbackPollingInterval = TimeSpan.FromMinutes(5) };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sut = Build(opts, provider);
        var received = new List<SickBaySnapshot>();

        var enumerateTask = Task.Run(async () =>
        {
            await foreach (var snap in sut.SubscribeSnapshotAsync(new TenantId("alpha"), cts.Token))
            {
                received.Add(snap);
                if (received.Count >= 2) cts.Cancel();
            }
        }, CancellationToken.None);

        // Wait for Subscribe call — fires before first yield (B2 fix).
        var capturedObserver = await observerRegistered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Fire the observer — should trigger a second snapshot.
        await capturedObserver.OnChangedAsync(MakeChange(), CancellationToken.None);

        await enumerateTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(received.Count >= 2, $"Expected ≥2 snapshots, got {received.Count}");
    }

    [Fact]
    public async Task SubscribeSnapshotAsync_UnsubscribesOnCancellation()
    {
        var provider = Substitute.For<IMissionEnvelopeProvider>();
        provider.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(AllHealthy()));

        var opts = new SickBayOptions { FallbackPollingInterval = TimeSpan.FromMinutes(5) };
        using var cts = new CancellationTokenSource();
        var sut = Build(opts, provider);
        var enumerator = sut.SubscribeSnapshotAsync(new TenantId("alpha"), cts.Token)
            .GetAsyncEnumerator(CancellationToken.None);

        // Consume initial snapshot — Subscribe is already wired before first yield (B2 fix).
        Assert.True(await enumerator.MoveNextAsync());
        provider.Received().Subscribe(Arg.Any<IMissionEnvelopeObserver>());

        // Cancel — should trigger Unsubscribe via finally.
        cts.Cancel();
        try { await enumerator.MoveNextAsync(); } catch (OperationCanceledException) { }
        await enumerator.DisposeAsync();

        provider.Received().Unsubscribe(Arg.Any<IMissionEnvelopeObserver>());
    }
}
