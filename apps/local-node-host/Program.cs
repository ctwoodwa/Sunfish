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

// Root seed: Wave 6.3.E.2 carve-out. Real keystore-backed loading lands in
// Wave 6.7. Development environments use a deterministic zero seed; other
// environments throw.
var rootSeed = LocalNodeRootSeedReader.Read(builder.Environment);

// Derive the root Ed25519 identity from the seed. Kernel-security's
// IEd25519Signer is stateless, so we instantiate it directly here rather than
// spin up a mini-provider to resolve it.
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
    .AddSunfishResourceGovernor()           // per-tick gossip cap                  (Wave 6.4)
    .AddSunfishDefaultTeamRegistrar(        // per-team service wiring              (Wave 6.3.E)
        dataDirectory: localNodeOptions.DataDirectory,
        rootIdentity: rootIdentity,
        subkeyDerivation: subkeyDerivation,
        sqlCipherKeyDerivation: sqlCipherKeyDerivation)
    .AddSunfishTeamStoreActivator(rootSeed); // per-team encrypted-store opener    (Wave 6.3.E.1)

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
