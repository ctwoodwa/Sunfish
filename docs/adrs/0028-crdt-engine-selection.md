# ADR 0028 — CRDT Engine Selection

**Status:** Proposed
**Date:** 2026-04-22
**Resolves:** Paper §9 mandates a CRDT engine with production-grade compaction, GC behavior, and compact binary encoding. Paper §19 names three candidates — Yjs, Loro, and an Automerge-inspired native-.NET implementation. This ADR picks one.

---

## Context

The local-node architecture paper (§2.4, §6.1, §9) treats the CRDT engine as kernel infrastructure: it backs AP-class data (§2.2), feeds the event log that sync exchanges over gossip (§6.1), and carries the majority of document state that domain plugins produce. Paper §9 is explicit that CRDT document growth — tombstones, insert/delete history, list and rich-text internals — is the single most dangerous operational risk in the architecture, and that **"library selection should treat compaction behavior as a first-class evaluation criterion alongside correctness and performance."**

The Sunfish runtime environment is .NET 11 preview. Per paper §5.1 the kernel is a **microkernel monolith** with domain plugins running in-process "to avoid inter-process communication overhead." That rules out architectures that push CRDT execution into a sidecar as the steady-state shape.

A prior evaluation — [`docs/specifications/research-notes/automerge-evaluation.md`](../specifications/research-notes/automerge-evaluation.md) (2026-04-17) — surveyed Automerge, Automerge-Repo, and Keyhive and concluded that Automerge is an excellent **design reference** but not a drop-in .NET dependency. Four mismatches were identified, with the lack of a first-class .NET binding at the top. That evaluation deferred the engine choice to a later ADR. This is that ADR.

---

## Requirements

- **.NET interop** — library must be callable from .NET 11 without a sidecar process in steady state (paper §5.1).
- **Rich-text + list + map CRDT types** — all three are used by domain plugins (paper §2.2 document/note/task data, §9 rich-text and list growth).
- **Compaction / GC** — production-grade, required per paper §9. Not a research prototype.
- **Compact binary encoding** — bandwidth-efficient gossip exchange per paper §6.1.
- **Active maintenance** — the library must stay alive for at least the 18-month implementation roadmap per paper §18.
- **Permissive license** — MIT, Apache 2.0, or similar. CC-BY-SA or strong copyleft is disqualifying for the Sunfish open-source strategy (paper §17.1).

---

## Candidates

### Option A — Yjs + yrs (Rust port) via .NET bindings

- Most mature CRDT library; widely deployed (Figma, Notion, others).
- yrs is the Rust port; .NET binding via P/Invoke or a generated C# wrapper over the C FFI.
- Pro: proven in production at scale; excellent documentation; well-understood tombstone GC; rich-text + list + map first-class.
- Con: .NET binding is an extra layer we maintain; marshaling cost; the binding's maintenance is an ongoing concern.
- License: MIT.

### Option B — Loro

- Newer Rust-native CRDT library. Paper §19 explicitly flags **"compact encoding + shallow snapshots"** as its strength.
- .NET binding via the same P/Invoke approach as yrs.
- Pro: designed with compaction as a primary feature; smaller runtime memory footprint in published benchmarks; rich-text + list + map first-class; shallow-snapshot support maps directly onto paper §9 mitigation 3.
- Con: less battle-tested than Yjs; smaller ecosystem; smaller pool of debugging war-stories to learn from.
- License: MIT.

### Option C — Native .NET implementation inspired by Automerge/Yjs

- Ship a Sunfish-authored CRDT library in C#.
- Pro: no binding layer; full control; aligns with "everything is .NET" ergonomics; matches the path the Automerge evaluation §4 sketched for "adopt the patterns, not the library."
- Con: reinventing the wheel on subtle correctness work; library-level compaction alone is years of engineering; maintenance is ours alone; contradicts paper §19's "production-validated" framing.
- License: ours (MIT per paper §17.1).

### Option D — Automerge via sidecar process

- Keep Automerge as-is (Rust); expose via IPC.
- Pro: use Automerge's correctness guarantees directly.
- Con: sidecar process is operational complexity; directly contradicts paper §5.1 "all running in-process to avoid IPC overhead." The Automerge evaluation §3.1 listed this path as "plausible, painful."
- License: MIT.

---

## Decision (recommended)

**Adopt Option B — Loro.**

Rationale:

- Paper §9 explicitly cites Loro's compact encoding + shallow snapshots as the production-validated answer to CRDT growth. Paper §19's row for "CRDT compaction" names Yjs (internal GC) and Loro (compact encoding + shallow snapshots) side-by-side; Loro's design advantage is specifically the growth-management axis, which paper §9 elevates to "first-class evaluation criterion."
- Loro's design treats compaction as a primary concern rather than an optimization retrofit. That framing matches paper §9's requirements better than Yjs's emergent-GC approach.
- Smaller memory footprint suits the desktop + mobile deployment envelope implied by paper §4.
- .NET binding effort is comparable to yrs; we pay it once either way. Starting with Loro means we do not retrofit later when Yjs's edge-case tombstone behavior becomes the binding work we have to redo.
- License (MIT) is compatible with paper §17.1 open-source strategy.

Fallback: if Loro's .NET binding maturity or .NET 11 compatibility proves intractable, we fall back to **Option A (Yjs/yrs)**. A small evaluation spike validates before committing. Option C and Option D remain rejected for the reasons listed; neither is the fallback.

---

## Decision consequences

### Positive

- Compaction story is "use Loro's compaction" — paper §9's first mitigation strategy is library-provided rather than homegrown.
- Application-level document sharding (paper §9 mitigation 2) becomes a kernel-API concern, not a runtime-CRDT concern.
- Shallow-snapshot support (paper §9 mitigation 3) is a library feature rather than an application-authored capability.
- Small, well-scoped binding package to maintain.
- Community momentum: Loro is gaining adoption in modern local-first apps; ecosystem growth is expected over the 18-month roadmap.

### Negative

- Less battle-tested than Yjs; we may hit issues Yjs users have already debugged. Mitigated by: initial 1-week validation spike, fallback to Option A defined, property-based test harness at paper §15 Level 1 covering convergence/idempotency/commutativity/monotonicity.
- Binding maintenance is a Sunfish-owned responsibility. Mitigated by: the binding is a thin wrapper; we are not forking Loro itself.

---

## Compatibility plan

- Wrap Loro behind a Sunfish CRDT contract (`ICrdtDocument`, `ICrdtText`, `ICrdtMap`, `ICrdtList`) so that a future library swap (e.g., fallback to Yjs) does not ripple through application code.
- The binding package lives in `packages/kernel-crdt/` per Wave 1.2 of the paper-alignment plan.
- The `ICrdtDocument` contract also isolates domain plugins from Loro's wire format — the event log (paper §5.1, §6.1) serializes through kernel-controlled encoding, so an engine swap does not require a schema epoch bump (paper §7.4) by itself.

---

## Implementation checklist

- [ ] 1-week validation spike: build a trivial `ICrdtDocument` wrapper around Loro in .NET; prove P/Invoke works on Windows / macOS / Linux; measure marshaling cost; test rich-text insert/delete + list append + map set.
- [ ] If spike passes: scaffold `packages/kernel-crdt/` with `Loro.NET` or equivalent dependency.
- [ ] If spike fails: re-run with yrs (Option A); update this ADR's recommendation to Option A and move to Accepted.
- [ ] Property-based test harness (paper §15 Level 1) using the `ICrdtDocument` contract — CRDT convergence, idempotency, commutativity, monotonicity.
- [ ] Stress test for CRDT growth under high-churn documents (paper §15.1 explicit requirement) — verify library-level compaction keeps document size bounded.
- [ ] Document application-level sharding pattern as an application concern per paper §9 mitigation 2.
- [ ] Document shallow-snapshot policy per paper §9 mitigation 3 — opt-in per document type, reserved for well-understood cases.

---

## References

- `_shared/product/local-node-architecture-paper.md` §2.4, §5.1, §6.1, §9, §15.1, §17.1, §18, §19
- [`docs/specifications/research-notes/automerge-evaluation.md`](../specifications/research-notes/automerge-evaluation.md) — prior Automerge evaluation (2026-04-17)
- [Loro GitHub](https://github.com/loro-dev/loro)
- [Yjs](https://github.com/yjs/yjs) and [yrs (y-crdt)](https://github.com/y-crdt/y-crdt)
- [ADR 0023](0023-dialog-provider-slot-methods.md), [ADR 0024](0024-button-variant-enum-expansion.md), [ADR 0025](0025-css-class-prefix-policy.md) — ADR format references
