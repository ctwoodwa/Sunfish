using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sunfish.Kernel.Runtime.DependencyInjection;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.DependencyInjection;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Sync.Identity;
using Sunfish.Kernel.Sync.Protocol;
using Sunfish.LocalNodeHost;
using Sunfish.LocalNodeHost.Health;

// Composition root for the Sunfish local-node host process.
//
// Paper §4 + §5.1: this is the persistent background service that owns the
// kernel runtime. It is intentionally headless — the application shell
// (Anchor, kitchen-sink, third-party UIs) connects to the already-running
// stack over the sync-daemon transport. Wave 6.3.E.2 reshaped the composition
// root: per-team services (event log, encrypted store, quarantine queue,
// gossip, lease coordinator, bucket registry) live inside each team's
// TeamContext.Services. The install-level surface here owns only:
//
//   * Plugin discovery / lifecycle   → AddSunfishKernelRuntime           (Wave 1.1)
//   * Security primitives + KDFs     → AddSunfishKernelSecurity          (Wave 1.6)
//   * Keystore-backed root seed      → AddSunfishRootSeedProvider        (Wave 6.7.A)
//   * Per-tick gossip cap            → AddSunfishResourceGovernor        (Wave 6.4, ADR 0032)
//   * Per-team service registrar     → AddSunfishDefaultTeamRegistrar    (Wave 6.3.E)
//   * Per-team store activator       → AddSunfishTeamStoreActivator      (Wave 6.3.E.1)
//   * Team bootstrap hosted service  → MultiTeamBootstrapHostedService   (Wave 6.3.E.2)
//   * Node-host lifecycle            → LocalNodeWorker                   (Wave 1.1 + Wave 6.3.E.2)

var builder = Host.CreateApplicationBuilder(args);

// Bind host-wide configuration (node id, team id, data directory, multi-team
// bootstrap). We need the DataDirectory + MultiTeam section available NOW —
// before service registration — so the default team registrar closure can
// capture it.
builder.Services.Configure<LocalNodeOptions>(
    builder.Configuration.GetSection("LocalNode"));

var localNodeOptions = new LocalNodeOptions();
builder.Configuration.GetSection("LocalNode").Bind(localNodeOptions);

// Root seed: either an injected per-tenant seed (Bridge supervisor path) or a
// keystore-backed IRootSeedProvider (direct-install path).
//
// W5.2 stop-work #1: when Bridge spawns a tenant child, it HKDF-derives a
// per-tenant 32-byte seed from its own install-level root seed and passes the
// hex string via LocalNode__RootSeedHex. The child honors it and skips the
// keystore lookup entirely — two tenants on one Bridge host therefore derive
// cryptographically independent Ed25519 + SQLCipher keys.
//
// Direct installs (Anchor, standalone dotnet run) leave RootSeedHex null and
// fall back to the keystore path: the provider generates a 32-byte RNG seed
// on first launch and persists it to the platform keystore (Windows DPAPI
// today; mac/Linux Wave-2 stubs). Blocking on GetAwaiter().GetResult() is
// acceptable here — no ambient SynchronizationContext, single-threaded
// bootstrap, well before any hosted service begins its StartAsync.
byte[] rootSeed;
if (!string.IsNullOrWhiteSpace(localNodeOptions.RootSeedHex))
{
    // Parse + validate the injected hex seed. Convert.FromHexString throws
    // FormatException on malformed input — we catch and rethrow with a clearer
    // message so misconfigured environments fail the host fast.
    try
    {
        rootSeed = Convert.FromHexString(localNodeOptions.RootSeedHex);
    }
    catch (FormatException ex)
    {
        throw new InvalidOperationException(
            "LocalNode:RootSeedHex is set but is not valid hex. Expected a 64-character hex string (32 bytes).",
            ex);
    }
    if (rootSeed.Length != 32)
    {
        throw new InvalidOperationException(
            $"LocalNode:RootSeedHex decoded to {rootSeed.Length} bytes; expected exactly 32.");
    }

    Console.WriteLine(
        $"[local-node-host] Using injected root seed (length={rootSeed.Length}B) — keystore bypass enabled.");
}
else
{
    using var bootstrapServices = new ServiceCollection()
        .AddSunfishRootSeedProvider()
        .BuildServiceProvider();
    var seedProvider = bootstrapServices.GetRequiredService<IRootSeedProvider>();
    rootSeed = seedProvider.GetRootSeedAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult().ToArray();
}

// Derive the root Ed25519 identity from the seed. Kernel-security's
// IEd25519Signer is stateless, so we instantiate it directly here rather
// than spin up a mini-provider to resolve it.
{
    var signer = new Ed25519Signer();
    var (rootPublicKey, rootPrivateKey) = signer.GenerateFromSeed(rootSeed);
    var rootIdentity = new NodeIdentity(
        NodeId: Convert.ToHexString(rootPublicKey.AsSpan(0, 16)).ToLowerInvariant(),
        PublicKey: rootPublicKey,
        PrivateKey: rootPrivateKey);

    // Pure, side-effect-free KDF utilities. Constructed directly so the default
    // registrar closure captures the exact derivation instance the activator also
    // observes later.
    var subkeyDerivation = new TeamSubkeyDerivation(signer);
    var sqlCipherKeyDerivation = new SqlCipherKeyDerivation();

    builder.Services
        .AddSunfishKernelRuntime()              // plugin registry + INodeHost          (Wave 1.1)
        .AddSunfishKernelSecurity()             // Ed25519 + X25519 + KDFs              (Wave 1.6)
        .AddSunfishRootSeedProvider()           // keystore-backed install seed         (Wave 6.7.A)
        .AddSunfishResourceGovernor()           // per-tick gossip cap                  (Wave 6.4)
        .AddSunfishDefaultTeamRegistrar(        // per-team service wiring              (Wave 6.3.E)
            dataDirectory: localNodeOptions.DataDirectory,
            rootIdentity: rootIdentity,
            subkeyDerivation: subkeyDerivation,
            sqlCipherKeyDerivation: sqlCipherKeyDerivation)
        .AddSunfishTeamStoreActivator(rootSeed); // per-team encrypted-store opener    (Wave 6.3.E.1)
}

// Wave 5.2.D health surface. Registered before LocalNodeWorker so the
// endpoint is bound as soon as possible after team bootstrap — Bridge's
// TenantHealthMonitor begins polling once the child process is spawned and
// will see "active team not yet materialized" (Unhealthy) during the
// bootstrap window until MultiTeamBootstrapHostedService completes.
builder.Services.AddHealthChecks()
    .AddCheck<LocalNodeHealthCheck>("local-node");

// Wave 5.3.C shared Kestrel-backed WebApplication. Singleton so
// HostedHealthEndpoint and HostedWebSocketEndpoint can register their paths
// on the same listener. Registered as a singleton FIRST so its constructor
// (which builds the WebApplication) runs before anything else resolves it.
builder.Services.AddSingleton<SharedHostedWebApp>();

// Wave 5.3.C sync-daemon accept surface. LoggingSyncDaemonAcceptor is a
// stub that logs "received WS connection, CBOR-reading not yet wired" and
// closes the WebSocket cleanly; Wave 5.3.D replaces it with the real
// session pipeline.
builder.Services.AddSingleton<ISyncDaemonAcceptor, LoggingSyncDaemonAcceptor>();

// Multi-team bootstrap runs first so the node-host worker sees a materialized
// active team on StartAsync. Registration order matters — the .NET generic
// host starts hosted services in registration order.
builder.Services.AddHostedService<MultiTeamBootstrapHostedService>();
builder.Services.AddHostedService<LocalNodeWorker>();
// Wave 5.3.C: HostedHealthEndpoint + HostedWebSocketEndpoint register their
// paths on the shared app during their StartAsync; the shared app itself is
// registered LAST so it starts Kestrel after every path has been mapped.
builder.Services.AddHostedService<HostedHealthEndpoint>();
builder.Services.AddHostedService<HostedWebSocketEndpoint>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SharedHostedWebApp>());

var host = builder.Build();
await host.RunAsync();
