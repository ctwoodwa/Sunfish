using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sunfish.Foundation.LocalFirst;
using Sunfish.Kernel.Crdt.DependencyInjection;
using Sunfish.Kernel.Events.DependencyInjection;
using Sunfish.Kernel.Runtime.DependencyInjection;
using Sunfish.Kernel.Security.DependencyInjection;
using Sunfish.LocalNodeHost;

// Composition root for the Sunfish local-node host process.
//
// Paper §4 + §5.1: this is the persistent background service that owns the
// kernel runtime. It is intentionally headless — the application shell
// (Anchor, kitchen-sink, third-party UIs) connects to the already-running
// stack over the sync-daemon transport (Wave 2.1). The kernel responsibilities
// composed here map one-to-one onto paper §5.1:
//   * Plugin discovery / lifecycle → AddSunfishKernelRuntime
//   * Event log                   → AddSunfishEventLog
//   * Encrypted local store       → AddSunfishEncryptedStore
//   * Quarantine queue (§11.2 L4) → AddSunfishQuarantineQueue
//   * Security primitives         → AddSunfishKernelSecurity
//   * CRDT engine                 → AddSunfishCrdtEngine

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddSunfishKernelRuntime()       // plugin registry + INodeHost      (Wave 1.1)
    .AddSunfishEventLog()            // persistent event log             (Wave 1.3)
    .AddSunfishEncryptedStore()      // SQLCipher + Argon2id             (Wave 1.4)
    .AddSunfishQuarantineQueue()     // offline-write quarantine         (Wave 1.5)
    .AddSunfishKernelSecurity()      // Ed25519 + X25519 + role keys     (Wave 1.6)
    .AddSunfishCrdtEngine();         // CRDT abstraction                 (Wave 1.2)

// Hosted service that drives the node lifecycle. One per process.
builder.Services.AddHostedService<LocalNodeWorker>();

// Bind host-wide configuration (node id, team id, data directory).
builder.Services.Configure<LocalNodeOptions>(
    builder.Configuration.GetSection("LocalNode"));

var host = builder.Build();
await host.RunAsync();
