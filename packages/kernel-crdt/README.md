# Sunfish.Kernel.Crdt

CRDT engine abstraction for the Sunfish local-node architecture. Wave 1.2 of the
paper-alignment plan. Paper §2.2 (AP-class data, leaderless CRDT merge) and §9
(CRDT growth and GC). ADR 0028 (CRDT engine selection).

## Status

**Provisional stub backend.** The 1-week validation spike mandated by
[ADR 0028](../../docs/adrs/0028-crdt-engine-selection.md) ran on 2026-04-22. The
outcome is documented below and in the banner of
[`Backends/StubCrdtEngine.cs`](Backends/StubCrdtEngine.cs).

## Spike outcome (2026-04-22)

| Candidate | Decision | Rationale |
|---|---|---|
| Loro via `LoroCs` (NuGet v1.10.3, 2025-12-09) | Deferred | Self-described as "very bare bones"; lists .NET Standard 2.0 through .NET 10 (no .NET 11 listed); required snapshot / delta / vector-clock surface not publicly documented. |
| Loro via `loro-ffi` + hand-rolled P/Invoke | Deferred | Larger binding effort than a 1-week spike can close responsibly. |
| Yjs/yrs via `YDotNet` (NuGet v0.6.0, 2026-02-14) | Documented fallback | Mature; supports rich-text + list + map; MIT-licensed. Targets net8.0–net10.0; .NET 11 compatibility must be validated before swap. |
| Provisional in-memory stub | **Adopted** | Unblocks Wave 1.2 dependents (event-bus integration, paper §15 property-based harness, federation sync) while the Loro binding matures. |

The stub ships the full `ICrdtDocument` / `ICrdtText` / `ICrdtMap` / `ICrdtList`
contract surface. It converges by **total-order replay of an op log** sorted by
`(lamport, actor)`. This is deterministic and correct for the convergence
property tested in `tests/` but is **not** a production CRDT: it does not
implement tombstone GC or shallow-snapshot compaction as paper §9 requires.

## What's here

| File | Role |
|---|---|
| `ICrdtDocument.cs` | Root document contract (snapshot, delta, vector clock). |
| `ICrdtText.cs` | Rich-text container contract. |
| `ICrdtMap.cs` | Key-value container contract. |
| `ICrdtList.cs` | Ordered-list container contract. |
| `ICrdtEngine.cs` | Factory/registry surface. |
| `Backends/StubCrdtEngine.cs` | Provisional stub backend (single file, heavily flagged). |
| `DependencyInjection/ServiceCollectionExtensions.cs` | `AddSunfishCrdtEngine()`. |
| `tests/` | xUnit + FsCheck property-based harness. |

## Using it

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Kernel.Crdt;
using Sunfish.Kernel.Crdt.DependencyInjection;

var services = new ServiceCollection();
services.AddSunfishCrdtEngine();

var engine = services.BuildServiceProvider().GetRequiredService<ICrdtEngine>();

await using var doc = engine.CreateDocument("note/2026-04-22/lunch");
var text = doc.GetText("body");
text.Insert(0, "CRDTs keep concurrent writers consistent.");

var snapshot = doc.ToSnapshot();          // send to peer via federation layer
await using var peer = engine.OpenDocument("note/2026-04-22/lunch", snapshot);
```

See `tests/CrdtTextTests.cs` for the full convergence exchange pattern (two peers,
concurrent edits, delta exchange, assert identical `Value`).

## Swapping the backend

The contracts in this package are designed so the backend is replaceable per
ADR 0028's compatibility plan. To swap:

1. Add a new `Backends/LoroCrdtEngine.cs` (or `Backends/YrsCrdtEngine.cs`)
   implementing `ICrdtEngine`.
2. Change the `TryAddSingleton<ICrdtEngine, StubCrdtEngine>()` call in
   `DependencyInjection/ServiceCollectionExtensions.cs` to the new backend
   (or add an `AddSunfishCrdtEngine(CrdtBackend backend)` overload).
3. Run the `tests/` harness against the new backend — the convergence
   property-test is backend-agnostic by design.
4. Run the paper §15.1 growth stress test (see
   [paper-alignment-plan Wave 4.4](../../_shared/product/paper-alignment-plan.md#wave-4---extended-phase-2--phase-3-backlog)).

## What's *not* here

- **Production CRDT merge semantics.** See the stub banner and ADR 0028.
- **Tombstone GC / shallow-snapshot compaction.** Paper §9 Wave 4.4 deliverable.
- **Wire-format stability guarantees.** The stub's JSON envelope is versionless
  and MUST NOT be persisted across a backend swap. Production backends will
  ship their own stable wire format.
- **Integration with `IEventLog`.** Wave 1.2 ships the contract; the event-log
  hook lands alongside Wave 1.3's persistent event-log work.

## References

- Paper §2.2 — AP-class data
- Paper §6.1 — gossip-based delta sync
- Paper §9 — CRDT growth and GC
- Paper §15 Level 1 — property-based testing for convergence / idempotency /
  commutativity / monotonicity
- [ADR 0028](../../docs/adrs/0028-crdt-engine-selection.md)
- [Wave 1.2 deliverable](../../_shared/product/paper-alignment-plan.md#wave-1---phase-1-kernel-primitives)
