# Aspire 13.2 Runtime Resource Mutation — Research Findings

**Date:** 2026-04-23
**Question:** Does .NET Aspire 13.2 support adding/removing resources from the
`DistributedApplication` resource graph at runtime, after `DistributedApplication.Run()` has
started?
**Context:** Resolves W5.2 decomposition stop-work #3 (see
`_shared/product/wave-5.2-decomposition.md`). The Bridge AppHost
(`accelerators/bridge/Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj`) pins
`Aspire.AppHost.Sdk` **13.2.1** with package references to `Aspire.Hosting.AppHost`,
`Aspire.Hosting.PostgreSQL`, `Aspire.Hosting.Redis`, and `Aspire.Hosting.RabbitMQ`.

---

## Verdict

**Answer: No (with a blessed workaround: pre-allocated `WithExplicitStart` slots).**

Aspire 13.2 does **not** expose any API for adding new resources to a running
`DistributedApplication`. The resource graph is sealed at `builder.Build()`; the last
author-controlled mutation point is the `BeforeStartEvent`, which fires **during**
`DistributedApplication.StartAsync` — before `Run()` returns control, not after. Once the
DCP orchestrator is running, the supported lifecycle surface is limited to
**starting, stopping, and restarting resources that already exist in the graph** (via
dashboard commands, `aspire resource <name> <command>` CLI, or `ResourceNotificationService`).
The officially tracked feature request for dynamic post-boot resource addition
(`dotnet/aspire` issue #5851, "On Demand Startup of Resources") was **closed into
milestone 9.1 and resolved by shipping `WithExplicitStart` instead** — i.e., pre-declare the
resource at build time, defer its start, and trigger it on demand. True post-boot
`AddProject<T>` is not supported and there is no evidence Microsoft intends to ship it.

---

## Evidence

### Microsoft Learn sources

**1. `BeforeStartEvent` is the last graph-mutation window — and it fires *before* `Run()` unblocks.**

From `BeforeStartEvent` class docs
(<https://learn.microsoft.com/dotnet/api/aspire.hosting.applicationmodel.beforestartevent?view=dotnet-aspire-13.0>):

> "This event is published before the application starts. […] Subscribing to this event
> is analogous to implementing the `BeforeStartAsync(DistributedApplicationModel,
> CancellationToken)` method."
>
> ```csharp
> builder.Eventing.Subscribe<BeforeStartEvent>(async (@event, cancellationToken) => {
>   var appModel = @event.ServiceProvider.GetRequiredService<DistributedApplicationModel>();
>   // Mutate the distributed application model.
> });
> ```

Corroborated by the GitHub issue #9146 ("Tightening the resource lifecycle") fetched verbatim:

> "The `DistributedApplication.Services` collection is built by the time this event is
> published. This event is the last chance to add or remove resources from the
> `DistributedApplicationModel` before they're processed by consumers."

All subsequent events in the lifecycle (`InitializeResourceEvent`,
`ResourceEndpointsAllocatedEvent`, `AfterResourcesCreatedEvent`, `ResourceReadyEvent`,
`ResourceStoppedEvent`) are *observational* with respect to the graph — they yield an
`IResource` reference or the model, but no documented path to add new resources post-boot.

**2. `DistributedApplicationModel` exposes a read-only `Resources` collection.**

From
<https://learn.microsoft.com/dotnet/api/aspire.hosting.applicationmodel.distributedapplicationmodel?view=dotnet-aspire-13.0>:

> "Represents a distributed application."
>
> Properties: `Resources` — "Gets the collection of resources associated with the
> distributed application."

The public surface is a getter only; constructors take `IEnumerable<IResource>` /
`IResourceCollection` at construction time. There is no `Add`/`Remove` method, no
`Mutate`/`Commit` pattern, and no event for post-startup graph changes.

**3. `WithExplicitStart` is the officially sanctioned "on-demand resource" pattern.**

From
<https://learn.microsoft.com/dotnet/api/aspire.hosting.resourcebuilderextensions.withexplicitstart?view=dotnet-aspire-13.0>:

> "Adds a `ExplicitStartupAnnotation` annotation to the resource so it doesn't
> automatically start with the app host startup."
>
> Example: "The database clean up tool project isn't started with the app host. The
> resource start command can be used to run it ondemand later."
>
> ```csharp
> var builder = DistributedApplication.CreateBuilder(args);
> var pgsql = builder.AddPostgres("postgres");
> builder.AddProject<Projects.CleanUpDatabase>("dbcleanuptool")
>        .WithReference(pgsql)
>        .WithExplicitStart();
> ```

**4. Aspire 13.2 release notes mention no runtime-graph-mutation APIs.**

From <https://aspire.dev/whats-new/aspire-13-2/> (fetched 2026-04-23): the 13.2 lifecycle
surface area additions are limited to `Logger` on `IDistributedApplicationResourceEvent`,
the `BeforeResourceStartedEvent` "only fires when actually starting a resource" fix,
`ResourceNotificationService.WaitFor` improvements, `aspire resource <name> <command>`
CLI reorganization (start/stop/restart), a dashboard parameter-setting UI, and a `rebuild`
command for project resources. No new API for adding/removing resources at runtime.

### Context7 sources

Context7 MCP returned "Monthly quota exceeded" on `resolve-library-id` for
`Aspire.Hosting`. **No Context7 evidence was gathered for this question.**

### GitHub issues / discussions

- **`dotnet/aspire` #5851 — "On Demand Startup of Resources"** — *Closed*, milestone 9.1.
  <https://github.com/dotnet/aspire/issues/5851>
  The author proposed three use cases (optional local-dev dependencies, nightly CronJob
  equivalents, optional secondary instances) and offered two alternatives. The one
  directly relevant to Wave 5.2.C.2, quoted verbatim:
  > "An alternative approach would be to provide a way for commands to dynamically add new
  > resources, which would allow you to use a command to spawn up a new resource."
  The resolution was to ship `WithExplicitStart` instead (PR #7324, author JamesNK) —
  i.e., the *other* proposal from the same issue. Dynamic runtime-add was not implemented.

- **`dotnet/aspire` #9146 — "Tightening the resource lifecycle"** — documents that
  `BeforeStartEvent` is "the last chance to add or remove resources from the
  `DistributedApplicationModel` before they're processed by consumers."
  <https://github.com/dotnet/aspire/issues/9146>

- **`dotnet/aspire` #7043 — "Allow Resources to not be started automatically with the
  AppHost"** — the companion feature request to #5851, also resolved by `WithExplicitStart`.
  <https://github.com/microsoft/aspire/issues/7043>

- **`dotnet/aspire` #2155 — "Execute arbitrary code once a resource is up and running"** —
  notes that `IDistributedApplicationLifecycleHook` "only allows modifying the app model
  before starting resources" — confirming the post-start immutability boundary.
  <https://github.com/dotnet/aspire/issues/2155>

- **`dotnet/aspire` #7324 — PR "Add `WithExplicitStart` to conditionally not start some
  resources"** — the actual shipping vehicle for #5851.
  <https://github.com/dotnet/aspire/pull/7324>

---

## Implications for Wave 5.2.C.2

**Do NOT dispatch 5.2.C.2 with the `AddProject` post-boot path — it is not an available
capability in Aspire 13.2.** The stop-work resolves by choosing between three concrete
paths, weighted below.

### Option A — Collapse to `Process.Start`-only (status quo from 5.2.C.1)

- **Pros:** Already shipped (`5ccdd46`). Zero additional framework risk. Unbounded tenant
  count — no upper ceiling. Simplest supervisor.
- **Cons:** Post-boot tenants never appear in the Aspire dashboard; lose structured logs,
  OTLP traces, resource-state UI, and the `aspire resource <name> stop` CLI for those
  tenants. Two distinct monitoring models (boot-time vs. runtime tenants).

### Option B — Pre-allocated `WithExplicitStart` slot pool (RECOMMENDED)

Pre-declare N `Sunfish.LocalNodeHost` project resources at AppHost build time, each marked
`.WithExplicitStart()`. The supervisor maps a new tenant signup to the next free slot and
calls the start command (via `ResourceCommandService` / `aspire resource <slot> start` /
programmatic equivalent). Slots are dashboard-visible from boot in `NotStarted` state; the
start/stop lifecycle is fully first-class.

- **Pros:** Full dashboard integration — identical to boot-time tenants. Uses the exact
  pattern Microsoft shipped to answer issue #5851. No framework hacks. Clean logical model
  (tenants bind to slots; slot identity is a string name).
- **Cons:** Hard ceiling on tenants per AppHost = N. Requires capacity planning (what is
  N?). When the pool fills, new signups must either (a) queue, (b) rotate onto the
  `Process.Start` fallback, or (c) trigger an AppHost restart with a larger N. Config/env
  coupling: each slot needs a tenant-parameterized config at start-time (achievable via
  `ResourceNotificationService` and per-resource environment-variable updates, but
  non-trivial to design).
- **Open question:** can `WithExplicitStart` project resources have their command-line
  args / env vars *rebound* per-start? `dotnet/aspire` discussion #8502 ("Custom Resource
  Commands - Dynamic Command-Line Arguments") is the relevant thread — confirms the
  community has asked; landing status should be re-verified before committing to B.

### Option C — AppHost-restart-per-tenant-signup with batch throttling

Accumulate signups, restart the AppHost periodically (e.g., every N minutes or every M
signups, whichever first), rebuild the graph with the new resource set.

- **Pros:** Dashboard integration is full. No pool ceiling.
- **Cons:** Every restart tears down all tenants' local nodes briefly — unacceptable for
  a hybrid supervisor that has in-flight traffic. Timing windows are user-hostile.
  Effectively a nonstarter for a SaaS-shaped Zone-C accelerator (Bridge §20.7, ADR 0031).

### Recommendation

**Replace Wave 5.2.C.2's `AddProject`-post-boot assumption with Option B (pre-allocated
slot pool), with Option A as the overflow path when the pool is exhausted.** This gives:

1. The first N tenants (for some N sized from tenant-per-node projections — see
   Bridge §20.7 capacity modeling) get full dashboard integration.
2. Overflow tenants fall back to `Process.Start` (the already-shipped 5.2.C.1 path) and
   are visibly marked as "detached" in the supervisor's own admin UI.
3. A later wave can restart the AppHost with a larger N during a maintenance window to
   rebalance detached tenants into pool slots.

Before dispatching the revised 5.2.C.2, confirm in a short spike (≤ 1 day) that:

- `WithExplicitStart` project resources can accept per-start environment variables
  (required to inject the tenant-scoped config at start time, not build time).
- `ResourceCommandService` or equivalent is callable from a hosted service inside the
  AppHost process (i.e., the supervisor can trigger starts programmatically, not only
  via the dashboard/CLI).

If either spike fails, **collapse to Option A** and accept the dashboard-integration
loss permanently.

---

## Notes for the supervisor implementation

Since (a) `AddProject<T>` post-`Build()` is not supported, these are the practical APIs the
supervisor should touch instead:

### Pre-allocation at AppHost build time (`Program.cs`)

```csharp
var builder = DistributedApplication.CreateBuilder(args);

const int TenantSlotCount = 32; // capacity-planned per Bridge §20.7

for (var i = 0; i < TenantSlotCount; i++)
{
    builder.AddProject<Projects.Sunfish_LocalNodeHost>($"tenant-slot-{i:D3}")
           .WithExplicitStart()
           .WithReference(postgres)
           .WithReference(redis);
           // Per-start env binding TBD — see spike above.
}

builder.Services.AddHostedService<TenantSlotSupervisor>();
builder.Build().Run();
```

### On-demand start from a hosted service

The supervisor resolves `DistributedApplicationModel` + `ResourceCommandService` (or the
runtime equivalent — verify exact DI type in 13.2) from DI, finds the next free
`tenant-slot-NNN` resource, and invokes the `start` command. This is conceptually what
the dashboard's "Start" button does; the programmatic surface is what the spike must
confirm.

### Fallback (overflow)

When all slots are in use (queryable via `ResourceNotificationService` by filtering for
`KnownResourceStates.Running`), route new signups to the existing `Process.Start` path
from 5.2.C.1 (commit `5ccdd46`). Emit a structured log event so operators can reconcile
later.

### What explicitly does NOT work in 13.2

- Calling `IDistributedApplicationBuilder.AddProject<T>` on the captured builder
  reference after `builder.Build()` returns — the builder is a build-time concept and
  the `DistributedApplicationModel` it produced is sealed.
- Mutating `DistributedApplicationModel.Resources` directly — the collection is exposed
  read-only on the public API (`IResourceCollection`), and even if reflection-bypassed,
  the DCP orchestrator would not pick up the addition (no re-scan event exists).
- Relying on `BeforeStartEvent` for runtime additions — it fires during
  `StartAsync`, before `Run()` unblocks. It is the *build-time-tail* mutation window,
  not a runtime one.
