---
type: advisory
workstream-or-chapter: W#52 Phase 2b — DefaultTacticalRuleEngine (pre-review)
last-pr: "702"
---

Pre-review advisory for DefaultTacticalRuleEngine.cs before opening the PR.
Two issues flagged; the first is security-council-class. Raise now to avoid
a council round-trip.

---

## Issue 1 — `ConcurrentDictionary.GetOrAdd` factory runs side-effects: CONCURRENCY BUG

**File:** `packages/foundation-tactical/DefaultTacticalRuleEngine.cs`  
**Method:** `EvaluateStreamAsync`

The factory lambda passed to `GetOrAdd` starts a `Task.Run` AND writes to
`tenantTasks[tid]`. `ConcurrentDictionary.GetOrAdd` can invoke the factory
**twice** if two threads race on the same key — only one value is stored
in `tenantWriters`, but **both tasks start**. The losing task's
`Channel<TacticalSignal>` reader blocks indefinitely on `ReadAllAsync`
(no signals ever arrive — the winner's writer is stored). The `tenantTasks[tid]`
assignment from both runs races; the "winner" of the task-dict write may be
the *losing* channel task, so `Task.WhenAll(tenantTasks.Values)` awaits the
wrong task; the winning reader task becomes unobserved; and when the winning
task completes, `output.Writer` may never receive `TryComplete`, leaving
`ReadAllAsync` blocked forever.

**Fix — move side-effects outside the factory:**

```csharp
var ch = Channel.CreateUnbounded<TacticalSignal>(
    new UnboundedChannelOptions { SingleReader = true });

var added = false;
var writer = tenantWriters.GetOrAdd(signal.TenantId, _ =>
{
    added = true;
    return ch.Writer;
});

if (added)
{
    var task = Task.Run(async () =>
    {
        await foreach (var s in ch.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            foreach (var alert in Evaluate(s))
                await output.Writer.WriteAsync(alert, ct).ConfigureAwait(false);
        }
    }, ct);
    tenantTasks.TryAdd(signal.TenantId, task);
}
```

Note: `added` is still technically racy across concurrent invocations of the
same outer `await foreach` body. For correct once-only initialisation, either
use a `Lazy<(ChannelWriter<TacticalSignal>, Task)>` value type in the dict,
or serialise per-tenant-init through a `SemaphoreSlim` keyed by `TenantId`.
The `Lazy<T>` approach keeps the ConcurrentDictionary pattern:

```csharp
var tenantLazies = new ConcurrentDictionary<TenantId,
    Lazy<(ChannelWriter<TacticalSignal>, Task)>>();

var lazy = tenantLazies.GetOrAdd(signal.TenantId, tid =>
    new Lazy<(ChannelWriter<TacticalSignal>, Task)>(() =>
    {
        var ch = Channel.CreateUnbounded<TacticalSignal>(...);
        var task = Task.Run(async () => { /* reader */ }, ct);
        return (ch.Writer, task);
    }, LazyThreadSafetyMode.ExecutionAndPublication));

var (writer, _) = lazy.Value;
await writer.WriteAsync(signal, ct).ConfigureAwait(false);
```

Then `await Task.WhenAll(tenantLazies.Values.Select(l => l.Value.Item2))`.

---

## Issue 2 — Unbounded channels: advisory

Both `Channel.CreateUnbounded<TacticalAlert>` (output) and
`Channel.CreateUnbounded<TacticalSignal>` (per-tenant) are unbounded.
Under a sustained signal burst, memory grows without bound. Consider:

```csharp
Channel.CreateBounded<TacticalSignal>(new BoundedChannelOptions(1024)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleReader = true
})
```

Per-tenant capacity 1024 is a reasonable default; configurable via
`TacticalOptions.PerTenantSignalChannelCapacity`. The output channel
can remain unbounded since alerts are consumed by the caller directly.
If council deems this advisory (not blocking), log as minor finding.
