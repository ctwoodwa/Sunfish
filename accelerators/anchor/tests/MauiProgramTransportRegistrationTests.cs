using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.CrewComms.DependencyInjection;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Transport;
using Sunfish.Foundation.Transport.DependencyInjection;
using Sunfish.Foundation.Transport.Relay;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#59 Phase 1 — Anchor MVP demo integration. Pins the contract that
/// <c>MauiProgram.cs</c> registers <see cref="ITransportSelector"/> via
/// <c>AddSunfishTransport()</c> BEFORE the <c>AddSunfishCrewComms</c> call —
/// without this ordering, <c>AddSunfishCrewComms</c>'s <c>TryAddSingleton</c>
/// noop pattern leaves <see cref="IChannelProvider"/>'s
/// <see cref="ITransportSelector"/> dependency unresolved, and crew-comms
/// silently fails at first session-open with a DI resolution exception.
/// <para>
/// We can't boot the full <c>MauiProgram.CreateMauiApp()</c> in a unit test
/// (MAUI requires a device-class graphics context), so this test mirrors the
/// relevant DI fragment from <c>MauiProgram.cs</c> and verifies the
/// composed graph resolves both services. Drift detection only — runtime
/// behavior of mDNS / Bridge tier selection is out of scope here.
/// </para>
/// </summary>
public sealed class MauiProgramTransportRegistrationTests
{
    private static readonly BridgeRelayOptions PlaceholderRelay = new()
    {
        // DefaultTransportSelector requires at least one TransportTier.ManagedRelay
        // transport (Tier 3 always-tried fallback per ADR 0061). For the
        // LAN-mDNS demo this URL is registered but not exercised. URL must
        // parse — placeholder.invalid is reserved per RFC 6761 so resolution
        // failures are unambiguous.
        RelayUrl = new Uri("wss://placeholder.invalid/crew-comms"),
    };

    [Fact]
    public async Task TransportSelector_ResolvesBeforeCrewCommsRegistration_PerW59Phase1()
    {
        var services = new ServiceCollection();

        // Mirror MauiProgram.cs §W#59 P1 ordering: BridgeRelay (Tier 3
        // always-fallback) + transport selector + crew-comms (which TryAdds
        // its NativeChannelProvider; depends on ITransportSelector).
        services.AddBridgeRelay(PlaceholderRelay);
        services.AddSunfishTransport();
        services.AddSunfishCrewComms(roster =>
            roster.AddInMemory(System.Array.Empty<CrewMember>()));

        // NativeChannelProvider is IAsyncDisposable-only — `using var` would
        // throw on container Dispose(). Use `await using` for async cleanup.
        await using var provider = services.BuildServiceProvider();

        // Both services must resolve from the composed graph.
        var transportSelector = provider.GetService<ITransportSelector>();
        Assert.NotNull(transportSelector);

        var channelProvider = provider.GetService<IChannelProvider>();
        Assert.NotNull(channelProvider);
    }

    [Fact]
    public async Task CrewComms_RegisteredWithoutTransport_DoesNotResolveChannelProvider()
    {
        // Negative-existence test pinning the contract: if a future refactor
        // drops the AddSunfishTransport() call from MauiProgram, IChannelProvider
        // resolution must fail (loud) so CI catches the regression rather
        // than ship a silently-broken Anchor.
        var services = new ServiceCollection();

        services.AddSunfishCrewComms(roster =>
            roster.AddInMemory(System.Array.Empty<CrewMember>()));

        await using var provider = services.BuildServiceProvider();

        // GetRequiredService throws when IChannelProvider's transitive
        // ITransportSelector dependency is missing — the constructor of
        // NativeChannelProvider fails to satisfy. This is the assertion the
        // positive test relies on for its meaningful coverage.
        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IChannelProvider>());
    }

    [Fact]
    public async Task Transport_RegisteredWithoutBridgeRelay_FailsLoudOnSelectorResolution()
    {
        // ADR 0061 / DefaultTransportSelector contract: at least one
        // TransportTier.ManagedRelay transport MUST be registered before
        // the selector resolves. Pin this so a future MauiProgram refactor
        // that drops AddBridgeRelay surfaces at CI time as a loud
        // ArgumentException rather than at runtime when the user opens a
        // chat session.
        var services = new ServiceCollection();
        services.AddSunfishTransport();

        await using var provider = services.BuildServiceProvider();

        Assert.Throws<ArgumentException>(
            () => provider.GetRequiredService<ITransportSelector>());
    }
}
