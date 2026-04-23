using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sunfish.Kernel.Runtime.DependencyInjection;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.DependencyInjection;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Sync.Identity;
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

// Root seed: keystore-backed IRootSeedProvider. On first launch the provider
// generates a 32-byte RNG seed and persists it to the platform keystore
// (Windows DPAPI today; mac/Linux Wave-2 stubs throw PlatformNotSupportedException
// on first use). Subsequent launches on the same machine read the same seed
// back, preserving continuity of Ed25519 + SQLCipher derivations.
//
// We build a minimal bootstrap provider here so the remaining static
// registration (registrar closure, store activator) can observe the resolved
// seed. Blocking on GetAwaiter().GetResult() is acceptable at process
// startup — no ambient SynchronizationContext, single-threaded bootstrap,
// well before any hosted service begins its StartAsync.
using (var bootstrapServices = new ServiceCollection()
    .AddSunfishRootSeedProvider()
    .BuildServiceProvider())
{
    var seedProvider = bootstrapServices.GetRequiredService<IRootSeedProvider>();
    var rootSeed = seedProvider.GetRootSeedAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult().ToArray();

    // Derive the root Ed25519 identity from the seed. Kernel-security's
    // IEd25519Signer is stateless, so we instantiate it directly here rather
    // than spin up a mini-provider to resolve it.
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

// Multi-team bootstrap runs first so the node-host worker sees a materialized
// active team on StartAsync. Registration order matters — the .NET generic
// host starts hosted services in registration order.
builder.Services.AddHostedService<MultiTeamBootstrapHostedService>();
builder.Services.AddHostedService<LocalNodeWorker>();
builder.Services.AddHostedService<HostedHealthEndpoint>();

var host = builder.Build();
await host.RunAsync();
