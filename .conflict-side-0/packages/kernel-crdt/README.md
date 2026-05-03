# Sunfish.Kernel.Crdt

CRDT engine abstraction for the Sunfish local-node architecture. Wave 1.2 of the
paper-alignment plan. Paper §2.2 (AP-class data, leaderless CRDT merge) and §9
(CRDT growth and GC). ADR 0028 (CRDT engine selection).

## Status

**YDotNet (Yjs/yrs) backend shipped as default.** The 1-week validation spike mandated by
[ADR 0028](../../docs/adrs/0028-crdt-engine-selection.md) was re-run on 2026-04-22. Full write-up:
[`SPIKE-OUTCOME.md`](SPIKE-OUTCOME.md).

## Spike outcome (2026-04-22, revisited)

| Candidate | Decision | Rationale |
|---|---|---|
| Loro via `LoroCs` (NuGet v1.10.3, 2025-12-09) | Deferred | Self-described as "very bare bones"; public API exposes `LoroDoc.GetMap` only; no snapshot / delta / vector-clock surface exposed at the C# level. Netstandard2.0 target works on .NET 11, but the API gap is a multi-week binding effort. |
| Loro via `loro-ffi` + hand-rolled P/Invoke | Deferred | Larger binding effort than a 1-week spike can close responsibly. No officially maintained .NET binding in `loro-dev/loro-ffi`. |
| Yjs/yrs via `YDotNet` (NuGet v0.6.0, 2026-02-14) | **Adopted as default backend** | Mature; MIT-licensed; targets net8.0 and runs fine on .NET 11 preview. Exposes the full `StateVectorV1` / `StateDiffV1` / `ApplyV1` surface. Documented client-ID-truncation bug mitigated in the wrapper (see below). |
| Provisional in-memory stub | Retained as test harness / escape hatch | See `Backends/StubCrdtEngine.cs` banner. |

### Client-ID uniqueness workaround

YDotNet 0.6.0's default `new Doc()` produces non-unique client IDs (200 docs → 16 unique IDs in our
probe). Even with explicit random `ulong` IDs, values above `2^32` cause concurrent-insertion
divergence because the yrs wire format encodes client IDs with only 32 bits of precision in practice.
`YDotNetCrdtEngine` constructs every `Doc` with an explicit `DocOptions.Id` drawn from a
cryptographically-random `uint32`; this eliminates divergence (0 / 80 stress-trial failures).
See [`SPIKE-OUTCOME.md`](SPIKE-OUTCOME.md) for the full investigation.

## What's here

| File | Role |
|---|---|
| `ICrdtDocument.cs` | Root document contract (snapshot, delta, vector clock). |
| `ICrdtText.cs` | Rich-text container contract. |
| `ICrdtMap.cs` | Key-value container contract. |
| `ICrdtList.cs` | Ordered-list container contract. |
| `ICrdtEngine.cs` | Factory/registry surface. |
| `Backends/YDotNetCrdtEngine.cs` | Default production backend — wraps YDotNet 0.6.0 (Yjs/yrs). |
| `Backends/StubCrdtEngine.cs` | Test-harness / escape-hatch stub backend (heavily flagged). |
| `DependencyInjection/ServiceCollectionExtensions.cs` | `AddSunfishCrdtEngine()` / `AddSunfishCrdtEngineYDotNet()` / `AddSunfishCrdtEngineStub()`. |
| `tests/` | xUnit + FsCheck property-based harness — runs against both backends. |
| `SPIKE-OUTCOME.md` | Full 2026-04-22 spike write-up. |

## Using it

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Kernel.Crdt;
using Sunfish.Kernel.Crdt.DependencyInjection;

var services = new ServiceCollection();
services.AddSunfishCrdtEngine();   // YDotNet (Yjs/yrs) backend by default

var engine = services.BuildServiceProvider().GetRequiredService<ICrdtEngine>();

await using var doc = engine.CreateDocument("note/2026-04-22/lunch");
var text = doc.GetText("body");
text.Insert(0, "CRDTs keep concurrent writers consistent.");

var snapshot = doc.ToSnapshot();          // send to peer via federation layer
await using var peer = engine.OpenDocument("note/2026-04-22/lunch", snapshot);
```

See `tests/CrdtTextTests.cs` for the full convergence exchange pattern (two peers,
concurrent edits, delta exchange, assert identical `Value`). See
`tests/YDotNetCrdtEngineTests.cs` for property-based convergence + idempotence
coverage against the real Yjs/yrs backend (exercises concurrent-insert RGA
tiebreak, map LWW, array fractional positions — things the stub's total-order
replay cannot honestly test).

## Backend selection

Three DI entry points:

| Extension | Backend | When to use |
|---|---|---|
| `AddSunfishCrdtEngine()` | YDotNet (default) | Production and almost every test. |
| `AddSunfishCrdtEngineYDotNet()` | YDotNet (explicit) | When you want the host wiring to be unambiguous. |
| `AddSunfishCrdtEngineStub()` | Stub | Environments where native binaries cannot load, or for deterministic unit tests that do not care about real CRDT merge semantics. |

All three use `TryAddSingleton`, so calling `AddSunfishCrdtEngineStub()` **before**
`AddSunfishCrdtEngine()` wins.

## Swapping to a Loro backend later

The contracts in this package stay the same. To add a Loro backend once
`loro-cs` exposes the snapshot/delta/vector-clock surface (see
[`SPIKE-OUTCOME.md`](SPIKE-OUTCOME.md) next-try prerequisites):

1. Add `Backends/LoroCrdtEngine.cs` implementing `ICrdtEngine`.
2. Add an `AddSunfishCrdtEngineLoro()` extension following the same shape as
   the YDotNet one.
3. Point `tests/LoroCrdtEngineTests.cs` at the same property-based harness.
4. Run the paper §15.1 growth stress test (see
   [paper-alignment-plan Wave 4.4](../../_shared/product/paper-alignment-plan.md#wave-4---extended-phase-2--phase-3-backlog)).

The YDotNet backend is not removed when Loro lands — both remain available,
and the default picks the best-supported one.

## What's *not* here

- **Tombstone GC / shallow-snapshot compaction.** Paper §9 Wave 4.4 deliverable.
  YDotNet's `Doc` auto-compacts tombstones via Yjs's internal GC on a default
  policy, but shallow snapshots (paper §9 mitigation 3) are not yet wired.
- **Wire-format stability guarantees.** YDotNet emits lib0 v1 encoded binary,
  which is a stable and documented Yjs format. The stub's JSON envelope is
  versionless and MUST NOT be persisted across a backend swap.
- **Integration with `IEventLog`.** Wave 1.2 ships the contract; the event-log
  hook lands alongside Wave 1.3's persistent event-log work.
- **Loro backend.** Deferred — see [`SPIKE-OUTCOME.md`](SPIKE-OUTCOME.md) for
  the re-evaluation triggers.

## References

- Paper §2.2 — AP-class data
- Paper §6.1 — gossip-based delta sync
- Paper §9 — CRDT growth and GC
- Paper §15 Level 1 — property-based testing for convergence / idempotency /
  commutativity / monotonicity
- [ADR 0028](../../docs/adrs/0028-crdt-engine-selection.md)
- [Wave 1.2 deliverable](../../_shared/product/paper-alignment-plan.md#wave-1---phase-1-kernel-primitives)
