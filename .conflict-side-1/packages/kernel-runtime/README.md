# Sunfish.Kernel.Runtime — The Paper's Kernel

`Sunfish.Kernel.Runtime` is the runtime kernel described in the foundational
paper (`_shared/product/local-node-architecture-paper.md`), sections **§5.1**
(kernel responsibilities) and **§5.3** (extension-point contracts).

It sits **alongside** `Sunfish.Kernel` (the type-forwarding facade), per
[ADR 0027 — Kernel Runtime Split](../../docs/adrs/0027-kernel-runtime-split.md).

## Why two packages?

Sunfish has two things both honestly called "kernel":

| Package | Responsibility | Shape |
|---|---|---|
| `Sunfish.Kernel` | Spec §3 primitive **contracts** (entity store, version store, audit log, permission evaluator, blob store, schema registry, event bus) | Type-forwarding facade over `Sunfish.Foundation.*` |
| `Sunfish.Kernel.Runtime` | Paper §5.1 **runtime** (node lifecycle, plugin registry, stream topology, projection scheduler, UI-block surfacing) | Live services with state |

The facade gives Layer 2 a single nameable entry point (closing gap G1).
The runtime gives the paper's §5.1 kernel a dedicated home so it doesn't
get tangled with `[TypeForwardedTo]` infrastructure. See ADR 0027 for the
decision history.

## What ships in Wave 1.1

- **Plugin contract:** `ILocalNodePlugin` (id, version, deps, load/unload).
- **Extension points (paper §5.3):**
  - `IStreamDefinition` — CRDT stream declaration + bucket contributions.
  - `IProjectionBuilder` — read-model projection, rebuildable from source.
  - `ISchemaVersion` — event-type upcaster (paper §7.2).
  - `IUiBlockManifest` — UI block + required streams + required attestations.
- **Registry:** `IPluginRegistry` / `PluginRegistry` — topological load,
  reverse-order unload, cycle + missing-dep detection, deterministic ordering
  for equal-depth nodes (sorted by plugin ID).
- **Host shell:** `INodeHost` / `NodeHost` — `Stopped → Starting → Running
  → Stopping → Stopped` state machine with `Faulted` on unrecoverable error.
- **DI:** `AddSunfishKernelRuntime()` wires the registry + host as singletons.

## What is intentionally **not** here yet

- Sync-daemon orchestration — **Wave 2.1**.
- CRDT engine hooks — **Wave 1.2** (needs ADR 0028 resolution).
- Persistent event-log binding — **Wave 1.3**.
- Projection scheduler — **Wave 2.x**.
- Stream topology wiring — **Wave 2.x**.

This Wave-1.1 package is the **shape** the paper's kernel will grow into, not
the full runtime. Downstream plugins can already implement `ILocalNodePlugin`
and depend on extension-point interfaces without waiting for the orchestration
waves.

## Using it

```csharp
using Sunfish.Kernel.Runtime;
using Sunfish.Kernel.Runtime.DependencyInjection;

var services = new ServiceCollection();
services.AddSunfishKernelRuntime();
// (your plugin registrations, foundation services, etc.)
var sp = services.BuildServiceProvider();

var host = sp.GetRequiredService<INodeHost>();
await host.StartAsync(ct);

await host.Plugins.LoadAllAsync(new ILocalNodePlugin[]
{
    new AccountingPlugin(),
    new SchedulingPlugin(),
}, ct);
```

A plugin looks like:

```csharp
public sealed class AccountingPlugin : ILocalNodePlugin
{
    public string Id => "com.sunfish.blocks.accounting";
    public string Version => "0.1.0";
    public IReadOnlyCollection<string> Dependencies => Array.Empty<string>();

    public Task OnLoadAsync(IPluginContext ctx, CancellationToken ct)
    {
        ctx.RegisterStream(new LedgerStream());
        ctx.RegisterProjection(new AccountBalancesProjection());
        ctx.RegisterUiBlock(new LedgerBlockManifest());
        return Task.CompletedTask;
    }

    public Task OnUnloadAsync(CancellationToken ct) => Task.CompletedTask;
}
```

## Links

- Paper §5.1 Kernel and Plugin Model
- Paper §5.3 Extension Point Contracts
- [ADR 0027](../../docs/adrs/0027-kernel-runtime-split.md)
- [`Sunfish.Kernel` (facade)](../kernel/README.md)
- [Paper alignment plan — Wave 1.1](../../_shared/product/paper-alignment-plan.md)
