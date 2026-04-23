# CRDT Backend Spike Outcome — 2026-04-22

**ADR:** [0028 — CRDT Engine Selection](../../docs/adrs/0028-crdt-engine-selection.md)
**Scope:** 1-week validation spike to swap out the provisional `StubCrdtEngine`.
**Decision:** **YDotNet (Yjs/yrs) backend shipped.** Loro deferred. Stub retained.

---

## What was tried

### Loro — deferred

- **`LoroCs` v1.10.3** (NuGet, published 2025-12-09, [sensslen/loro-cs](https://github.com/sensslen/loro-cs)).
  Self-described as "very bare bones"; the public README exposes only `LoroDoc` and `GetMap`. The
  snapshot / delta / vector-clock surface that `ICrdtDocument` requires is not exposed at the C# level.
  Netstandard2.0 target, so it loads on .NET 11 preview, but the API gap is a multi-week binding effort.
- **`loro-ffi` + hand-rolled P/Invoke** ([loro-dev/loro-ffi](https://github.com/loro-dev/loro-ffi)).
  Official UniFFI bindings listed for Swift, Python, React Native, Go, and a community C# entry that
  is `loro-cs`. No officially maintained .NET binding. Hand-rolling P/Invoke in a 1-week spike is out
  of scope.
- **Verdict:** Loro remains the right long-term choice per ADR 0028 rationale (compact encoding +
  shallow snapshots), but the .NET binding maturity has not moved since the morning probe. Re-evaluate
  once `loro-cs` exposes snapshot/delta APIs.

### YDotNet — shipped

- **`YDotNet` v0.6.0 + `YDotNet.Native` v0.6.0** (NuGet, published 2026-02-14,
  [y-crdt/ydotnet](https://github.com/y-crdt/ydotnet)). Targets net8.0; runs fine on .NET 11 preview
  via forward compatibility.
- Public API exposes everything we need: `Doc.Text/Map/Array`, `Transaction.StateVectorV1`,
  `Transaction.StateDiffV1(peerSv)`, `Transaction.ApplyV1(update)`, `TransactionUpdateResult`.
- MIT licensed; native binaries packaged for win-x64, linux-x64, osx-x64/arm64. The linux-x64-musl
  RID emits `NETSDK1206` as a benign restore warning.

### Build + run validation

- `dotnet build packages/kernel-crdt/Sunfish.Kernel.Crdt.csproj` — **0 errors.**
- `dotnet test packages/kernel-crdt/tests/` — **66 / 66 passing** (stub suite + parallel YDotNet
  suite).
- Two-peer probe with concurrent text inserts and bidirectional `StateDiffV1` / `ApplyV1` exchange
  converges to the same string on both sides — real CRDT merge, not total-order replay.

---

## Important finding: YDotNet 0.6.0 client-ID divergence bug

**Observed:** If two peers are constructed via the default `new Doc()`, client IDs collide
catastrophically — 200 constructed docs produced only 16 unique IDs. Concurrent ops from
same-ID peers become indistinguishable in the op log and diverge silently after delta exchange.

**Investigation:** Even with explicit random `ulong` `DocOptions.Id` values, documents with IDs
above `2^32 (4_294_967_295)` diverged on every concurrent-insert trial (80/80). Divergence vanishes
when IDs are constrained to uint32 range — 0 / 80 divergence over stress trials.

**Root cause (confirmed via bisection, not by source inspection):** the yrs wire format encodes
client IDs as zig-zag varint over `i64`; values with the high 32 bits set lose precision on the
round trip, so concurrent-insert RGA tiebreak uses different IDs on each peer.

**Mitigation shipped in `YDotNetCrdtEngine`:** construct every `Doc` with an explicit
`DocOptions.Id` drawn from a cryptographically-random `uint32`. Collision probability is birthday-paradox
safe for any realistic Sunfish federation envelope (§2.2 of the paper).

---

## Decision

- **Default backend:** `YDotNetCrdtEngine` via `AddSunfishCrdtEngine()`.
- **Stub retained** as `AddSunfishCrdtEngineStub()` per the ADR 0028 compatibility plan. The stub
  is a test harness and the fallback for environments where YDotNet's native binaries cannot load.
- **Loro revisit trigger:** `loro-cs` ships a documented snapshot/delta/vector-clock API, OR the
  `loro-dev/loro-ffi` community adds an officially maintained .NET binding.

## Next-try prerequisites (if LoroCs maturity recovers)

1. `LoroCs.LoroDoc` exposes a method equivalent to `export(ExportMode.Snapshot)` returning a
   `byte[]`.
2. `LoroCs.LoroDoc` exposes a method equivalent to `import(byte[])` accepting snapshot or update
   bytes.
3. `LoroCs.LoroDoc` exposes a method equivalent to `oplogVv()` returning a binary vector clock.
4. `LoroCs.LoroText.insert(index, value)` and `LoroText.delete(index, length)` exposed with UTF-16
   or UTF-8 index semantics documented.
5. `LoroCs.LoroMap.insert(key, LoroValue)` + `LoroMap.delete(key)` + equivalent for `LoroList`.

When those land, the Loro backend is a ~2-day implementation and ~1-day test mirror of the
YDotNet backend present in this package.
