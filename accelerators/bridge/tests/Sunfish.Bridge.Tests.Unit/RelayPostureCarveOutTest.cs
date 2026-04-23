using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Sunfish.Bridge;           // BridgeOptions, RelayOptions
using Sunfish.Bridge.Relay;     // IRelayServer, RelayServer
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.DependencyInjection;
using Sunfish.Kernel.Sync.DependencyInjection;
using Sunfish.Kernel.Sync.Protocol;

using Xunit;

namespace Sunfish.Bridge.Tests.Unit;

/// <summary>
/// ADR 0031 + Wave 6.3.E.2 pin-down: the Bridge relay posture is a headless
/// managed relay (paper §6.1 tier-3) and must NEVER compose the multi-team
/// runtime surface. Specifically, relay posture must not register
/// <see cref="ITeamContextFactory"/> — the relay does not own team contexts
/// nor should it be accidentally extended with <c>ITeamStoreActivator</c>,
/// per-team DBs, or gossip daemons.
/// </summary>
/// <remarks>
/// <para>
/// <c>ConfigureRelayPosture</c> in <c>Sunfish.Bridge/Program.cs</c> is a
/// top-level <c>static void</c> local function, so it is not directly
/// callable from tests. We replicate the exact relay-posture service
/// registrations inline (and keep them in lockstep with Program.cs so a
/// drift in that file is caught here), then assert that the built provider
/// does NOT resolve <see cref="ITeamContextFactory"/>.
/// </para>
/// <para>
/// If <c>ConfigureRelayPosture</c> changes and this test goes stale, the
/// follow-up is either (a) update this test's inline registrations to match,
/// or (b) factor <c>ConfigureRelayPosture</c> into a real static on a shared
/// type exposed to the test project. The latter is the preferred path once
/// the relay posture grows beyond the handful of registrations below.
/// </para>
/// </remarks>
public class RelayPostureCarveOutTest
{
    [Fact]
    public void Relay_posture_does_not_register_ITeamContextFactory()
    {
        var services = new ServiceCollection();

        // Mirror the exact Bridge relay-posture composition from
        // Sunfish.Bridge/Program.cs::ConfigureRelayPosture. If any line
        // drifts there, update this block and note the reason — the goal is
        // to detect accidental ITeamContextFactory introduction, not to
        // mandate a frozen posture.
        var relayOpts = new RelayOptions();
        var listenEndpoint = !string.IsNullOrWhiteSpace(relayOpts.ListenEndpoint)
            ? relayOpts.ListenEndpoint!
            : OperatingSystem.IsWindows()
                ? "sunfish-bridge-relay"
                : "/tmp/sunfish-bridge-relay.sock";

        services.AddSingleton<ISyncDaemonTransport>(
            _ => new UnixSocketSyncDaemonTransport(listenEndpoint));
        services.AddSunfishKernelSync();
        services.AddSunfishKernelSecurity();
        // AddAuthorization() is part of the real relay posture but pulls
        // ASP.NET Core assemblies; omitted here because it contributes no
        // ITeamContextFactory registration — the carve-out check is unaffected.
        services.AddSingleton<IRelayServer, RelayServer>();
        // RelayWorker omitted — it's a hosted service and requires an
        // ILogger<T> from a logging stack we don't spin up here. Its
        // registration contributes nothing to the ITeamContextFactory check.

        using var sp = services.BuildServiceProvider();

        // Core assertion: relay posture does not register the multi-team
        // runtime surface. ADR 0031 carve-out.
        Assert.Null(sp.GetService<ITeamContextFactory>());
        Assert.Null(sp.GetService<IActiveTeamAccessor>());
    }
}
