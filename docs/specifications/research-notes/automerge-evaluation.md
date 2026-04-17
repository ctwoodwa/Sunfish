# Automerge as a Candidate Implementation for Sunfish Decentralization

**Document type:** Research evaluation note
**Audience:** Architects, Phase 9 implementers, future decentralization-track planners
**Date:** 2026-04-17
**Status:** v0.1 — initial evaluation after surveying Automerge, Automerge-Repo, Keyhive, Beelay

---

## TL;DR

Automerge (with Automerge-Repo and the emerging Keyhive auth layer) is an **excellent design reference** for Sunfish's decentralization primitives (spec §2, §8, §10) but is **not a drop-in implementation** for the current Sunfish repo. Four material mismatches:

1. **No official .NET binding.** Bindings exist for JS/TS, Rust, Swift, Python, Java, R, and C FFI. Sunfish is .NET-first.
2. **Keyhive uses group-based capability membership; spec §10 described Macaroon-style delegation.** Both are capability systems but model authority differently. Spec needs reconciliation.
3. **Bridge is server-hosted Blazor Server, not local-first.** Automerge-Repo's sweet spot is local-first clients (IndexedDB, offline-first). Bridge's architecture is shaped against a central PostgreSQL + DAB + SignalR backplane.
4. **No runtime schema registry in Automerge.** Spec §3 calls for a schema registry primitive; Automerge sidesteps this — all docs are JSON-like and schemaless at the library layer.

**Recommendation:** Treat Automerge as **design reference / inspiration** for the initial Sunfish decentralization primitives, not as an integrated dependency. Adopt its CRDT semantics, its sync protocol shape, and Keyhive's group-membership model (likely replacing the spec's Macaroon story). Defer any actual integration with automerge-rs/automerge-c until a later phase where the cost-benefit becomes clear.

For Phase 9 specifically: **no immediate change**. Bridge as shipped does not need Automerge. The `PLATFORM_ALIGNMENT.md` inventory (Task 9-10) should note Automerge as the candidate implementation for the decentralization rows that are currently 🔴 not-adopted.

---

## 1. What Automerge provides

### 1.1 Core library (automerge-rs / automerge-js)

- **JSON-like CRDT document type.** You make edits to a local document; Automerge produces a compact binary patch; peers merge patches deterministically. No conflicts in the traditional sense — the CRDT semantics define the merge.
- **Version history built in.** Every change is a node in a Merkle-like change graph. You can inspect history, branch, rebase, reconstruct any past state.
- **Compact binary wire format.** Designed to be efficient over slow networks and Bluetooth.
- **Sync protocol.** Delta-sync between two peers who each know a document's state — they negotiate what each has and exchange only the missing changes. Works over any reliable in-order transport (WebSocket, WebRTC, Bluetooth).

### 1.2 Automerge-Repo (the "batteries-included" toolkit)

- **Storage adapters:** IndexedDB, filesystem, Redis — persist documents under the repo layer.
- **Network adapters:** WebSocket, Bluetooth, WebRTC, BroadcastChannel.
- **UI integrations:** React, Svelte, Vue.
- **Sync server:** `automerge-repo-sync-server` is a simple Express (Node.js) app for relaying sync messages between peers who can't connect directly.
- **Status:** Beta. Performance degrades above ~60k edits per document; busy sync servers struggle.

### 1.3 Keyhive (the new auth/sync layer from Ink & Switch)

- **Capability via group membership**, not signed tokens. Every principal (device, person, organization) is either an individual (Ed25519 public key) or a group (a mutable collection of other principals).
- **Documents are groups.** Sharing a doc with a team = making the team-group a member of the doc-group. Inherits nested-group semantics automatically.
- **BeeKEM** — continuous group key agreement. Peers derive document decryption keys from membership without a central key server.
- **Encrypted commit graphs.** The sync server sees only ciphertext. Only principals in the doc-group can decrypt.
- **Revocation is a graph operation.** Adding/removing members is itself a CRDT operation that peers sync.
- **RIBLT (Rateless Invertible Bloom Lookup Tables)** for set reconciliation of the membership graph.

### 1.4 Beelay (the new multi-doc sync protocol)

- The original Automerge sync protocol is per-document. Real apps have thousands of docs.
- Beelay is the new protocol being designed alongside Keyhive to sync many docs efficiently between peers — relevant to Sunfish where a property-management org might have millions of entities.

---

## 2. Mapping to Sunfish spec deliverables

| Spec section | Automerge contribution | Fit |
|---|---|---|
| §2 Reference Architecture — decentralized, federated, crypto-owned | Keyhive's Ed25519 + BeeKEM + encrypted commit graphs are the reference design | **Excellent** — adopt the model |
| §3 Core Kernel — Entity storage (multi-versioned) | CRDT change graph gives versioning for free | **Excellent** — direct fit |
| §3 Core Kernel — Audit trail | Immutable change graph = audit trail | **Excellent** — direct fit |
| §3 Core Kernel — Event bus | Sync protocol ≈ event stream between peers | **Good** — sync = events, but external consumers need an adapter |
| §3 Core Kernel — Schema registry | Automerge is schemaless at library layer | **No fit** — build separately |
| §3 Core Kernel — Permission evaluator | Keyhive provides enforcement; no policy DSL | **Partial** — Keyhive is a primitive layer, a PolicyL-style DSL (per §5) would sit on top |
| §8 Asset Evolution & Versioning (split, merge, re-parent, temporal) | CRDT semantics handle merge; branching is native; temporal queries are "doc at change N" | **Excellent** — direct fit |
| §10 Federation (peer-to-peer, multi-jurisdiction) | Automerge-Repo sync protocol is federation | **Excellent** — direct fit |
| §10 Time-bound delegation (Macaroon-style) | **Keyhive doesn't use Macaroons** — it uses group membership with revocation | **Mismatch** — see reconciliation below |

---

## 3. The four material mismatches

### 3.1 No .NET binding

**Reality:** The Rust core is wrapped for JS (WASM), Swift, Python, Java, R, and raw C (FFI). There is no first-class `automerge-dotnet` package as of this writing (April 2026).

**Integration paths if we want real Automerge in Sunfish:**

| Path | Effort | Maintenance | Verdict |
|---|---|---|---|
| **P/Invoke over automerge-c** | Medium initial + platform-specific native asset shipping | High (platform-specific binaries, P/Invoke shims) | Plausible, painful |
| **Sidecar service** — Automerge runs as a Node or Rust process; .NET talks over gRPC/HTTP | Low initial | Low ongoing (two processes, clear boundary) | **Recommended if we integrate** |
| **Write a .NET CRDT from scratch**, inspired by Automerge's algorithms | Very high initial | Low ongoing but requires CRDT expertise | Likely overkill; consider only if Automerge semantics don't match |
| **Don't use Automerge, take the ideas** | Zero integration | N/A | **Recommended for now** |

**Recommendation:** Adopt Automerge as design reference. Build Sunfish's version-store primitive with a CRDT-adjacent design (append-only change log + merkle DAG + deterministic conflict resolution). Plan for a future phase to evaluate whether swapping in real Automerge via a sidecar is worth the operational cost.

### 3.2 Keyhive vs Macaroons

**Spec §10 currently describes Macaroon-style delegation.** Macaroons (Google, 2014) are cryptographically-signed bearer tokens with attached caveats — time bounds, scopes, third-party verifiers. Landlord issues inspector a "valid 7 days, read-only on property P" macaroon.

**Keyhive's alternative:** every principal is either an individual (one Ed25519 keypair, typically a device) or a group (mutable list of member principals). Access to a document is membership in the document's group. Revocation is a graph operation. No bearer tokens — possession of membership is proven by signing with your Ed25519 key.

**Comparison:**

| Property | Macaroons | Keyhive |
|---|---|---|
| Delegation | Attenuate existing token | Add sub-group membership |
| Revocation | Very hard (tokens are bearer, can't be invalidated individually — need blocklists or short expiry) | Graph operation, syncs to all peers |
| Offline enforcement | Yes (caveats verified locally) | Yes (group membership verified locally via signatures) |
| Time bounds | Native (time caveats) | Needs layering — a group can hold a time-bounded child group |
| Third-party caveats | Native (verifier URL) | Not a built-in primitive |
| Key rotation | Mints a new token | Rotate device key → graph op adds new key, revokes old |
| Peer-to-peer | Works (token is self-contained) | Works (membership graph is CRDT, syncs peer-to-peer) |

**Judgment call.** Both are capability systems. Keyhive is **better matched to Sunfish's model** because:

- Sunfish is already CRDT-shaped (spec §3 entity versioning). Layering Keyhive on top of CRDT entities is natural.
- Revocation is a first-class concern for property management (fire a contractor, re-sync all devices — must not require short-expiry workarounds).
- Offline peer-to-peer enforcement is the goal; Keyhive was designed for exactly this.
- Third-party caveats (macaroon's big trick) are less relevant — code-enforcement federation is a Keyhive cross-group membership, not a third-party URL dance.

**Macaroon win cases** that Keyhive doesn't cover as neatly:

- "Inspector, go to all 50 of my properties, but only for the next 48 hours" — Macaroon's time-caveat is one-liner; Keyhive requires a time-bounded group with automatic expiry (doable but more moving parts).
- "Anyone in my team can delegate further to their agents, but not beyond one hop" — Macaroon attenuation rules are expressive; Keyhive's group-graph needs care.

**Recommendation:** **Replace the Macaroon model in spec §10 with a Keyhive-inspired group-membership model as the primary.** Retain Macaroons as a documented **alternative** for time-bound scenarios where group management is overkill — both are capability systems and they can coexist (e.g., Keyhive for durable membership, a Macaroon-like ephemeral token for short-lived inspector access). Document the decision + rationale in spec §10.

### 3.3 Bridge is server-hosted, not local-first

**Reality:** Bridge is a Blazor Server app with a PostgreSQL + DAB + Wolverine + SignalR backend. State lives on the server. Clients are browser UIs that see only the server's view.

**Automerge's sweet spot:** local-first clients that hold their own copy of state, work offline, and sync when network permits. IndexedDB persistence, WebSocket or WebRTC sync.

**Mismatch implication:** If Sunfish wants Automerge benefits (offline-first inspections, contractor editing on-site without signal, peer-to-peer sync), **the architecture needs to shift** — Bridge's Blazor Server rendering model is specifically the anti-pattern (server holds state, clients are dumb UIs).

**Reconciliation paths:**

1. **Keep Bridge Blazor Server + add Automerge as backend-only storage.** PostgreSQL stays as the projection (read model); Automerge change log is the source of truth. Servers sync via the Automerge protocol with peer servers in other jurisdictions. Works for federation (§10) but doesn't give offline-first clients.
2. **Pivot Bridge to Blazor WASM** (offline-capable) + Automerge-JS running in the browser. Server becomes a sync relay + projection store. Offline-first, peer-capable. Significant re-architecture; would throw away the current Blazor Server composition.
3. **Don't integrate Automerge into Bridge at all.** Build a second accelerator — `accelerators/bridge-offline/` or a Phase-10 inspection-app — as the local-first reference. Bridge stays as the server-authoritative reference.

**Recommendation:** Path 3 for the immediate roadmap. Ship Bridge as-is (spec §10 Bridge is explicitly described as centralized-reference). Build a separate **local-first inspection app accelerator** in a future phase that demonstrates Automerge integration — that's the natural showcase for peer-to-peer, offline-first. Both accelerators co-exist; consumers pick the architecture that fits their deployment.

### 3.4 No schema registry

**Spec §3 kernel primitive:** runtime schema registry with JSON-Schema-style contracts and migration scripts.

**Automerge:** documents are untyped JSON-like structures. Schema is enforced by application code, not the library.

**Impact:** Low. This is an additive primitive that sits **beside** Automerge, not on top. We'd build `Sunfish.Schema` regardless.

**Recommendation:** No change — the schema registry was always going to be custom Sunfish code.

---

## 4. What adopting Automerge patterns (without adopting Automerge) gives us

Even without integrating the library, adopting Automerge's design choices gives Sunfish specific advantages:

### 4.1 Entity changes as an append-only Merkle DAG

Current Sunfish Foundation uses EF Core rows with `CreatedAt`/`UpdatedAt` audit columns. A Merkle-DAG version store (spec §3) would store entity state as a sequence of signed change operations, with each change hash-linked to its predecessors. Merging two divergent histories is deterministic. Every peer arrives at the same state given the same set of changes.

This is a real design shift for the kernel — not a small one — but it unlocks most of spec §8 (asset evolution: splits, merges, re-parent, temporal queries) and §10 (federation) with a single consistent primitive.

### 4.2 Keyhive-style capability enforcement

Adopting Keyhive's model in Sunfish means:

- Every principal (user, device, organization, jurisdiction) has an Ed25519 keypair in Foundation
- Every entity belongs to a "group" (ACL graph node)
- Group membership is itself a CRDT that syncs between peers
- Mutations to an entity require a signed operation by a member principal
- Revocation is adding a "remove X" operation to the group

The `Sunfish.Crypto` + `Sunfish.Authorization` packages that spec §2 called for would be built around this model.

### 4.3 Sync protocol shape

If we build a custom version-store, we can copy Automerge's sync protocol shape:

1. Peer A says "I have heads X, Y, Z"
2. Peer B compares against its state, says "I need these changes; here's what I have that you don't"
3. Both sides exchange only the missing changes
4. Each side verifies signatures and merges

Conn-agnostic — works over WebSocket, SignalR, gRPC, carrier-pigeon. This matches spec §10's "works over any transport" aspiration.

---

## 5. Impact on Phase 9 (Bridge accelerator)

### 5.1 No immediate changes to Phase 9 tasks

The Phase 9 plan ships Bridge as a server-authoritative PM reference. Automerge isn't a prerequisite for this. Tasks 9-1 through 9-9 are unchanged.

### 5.2 Update Task 9-10's PLATFORM_ALIGNMENT.md

The alignment inventory from Task 9-10 should cite Automerge/Keyhive as the candidate implementation for each decentralization row:

```markdown
| Cryptographic ownership proofs | 🔴 | Candidate: Keyhive (Ed25519 + BeeKEM). See docs/specifications/research-notes/automerge-evaluation.md |
| Time-bound / delegation | 🔴 | Candidate: Keyhive group graphs (primary) + Macaroon-style ephemeral tokens (supplement) |
| Federation (peer-to-peer sync) | 🔴 | Candidate: Automerge-style sync protocol adapted for .NET |
| Asset versioning (CRDT) | 🟡 | EF audit columns today; candidate: Automerge-inspired Merkle DAG version store |
```

### 5.3 Add a "Decentralization Track" entry to the next-steps section

The bottom of `PLATFORM_ALIGNMENT.md` already lists future platform phases. Add an explicit note:

> **Platform Phase B (decentralization):** Design decision driven by evaluation in `docs/specifications/research-notes/automerge-evaluation.md`. Plan is to adopt Automerge's **semantics** (Merkle DAG change log, CRDT merge rules, sync protocol shape) and Keyhive's **capability model** (group membership over Ed25519 keys) rather than integrate the Automerge library directly. Integration is deferred pending a cost-benefit evaluation in a future phase; initial implementation is a .NET-native version store + crypto + sync. See evaluation doc for mismatches (no .NET binding, Bridge is not local-first, Keyhive vs Macaroons reconciliation).

---

## 6. Impact on the platform specification

### 6.1 Spec §10 reconciliation

Replace the primary delegation model from Macaroon-centric to Keyhive-inspired. Explicitly note:

- Group membership as primary capability mechanism
- Ed25519 signed operations for all writes
- Revocation as a first-class graph operation
- Macaroon-style ephemeral tokens as a supplementary primitive for short-lived scenarios

### 6.2 Spec §8 ratification

The asset evolution section aligns well with CRDT semantics. Add a note citing Automerge's merge rules as the reference for split/merge/re-parent operations.

### 6.3 Spec §4 roadmap — add Phase B

The 5-phase roadmap should be clear that decentralization (crypto, Keyhive-style auth, federation) is its own track that runs parallel-or-after the vertical accelerators. Today's repo state has foundation + UI + Blazor adapter; it does NOT have any kernel crypto. This phase is a design/implementation effort probably equal in scope to the entire UI migration.

### 6.4 Spec §12 risk register update

Add:

- **Risk:** Adopting Automerge's semantics without the library means we're rebuilding CRDT primitives in .NET. CRDT correctness is subtle.
- **Mitigation:** Write extensive conformance tests against Automerge reference implementations; consider a sidecar integration for cross-verification during development.

---

## 7. Open questions

1. **Does Sunfish actually need local-first?** The spec implies yes (offline inspections, drone data ingestion disconnected, rural properties with poor signal), but Bridge doesn't exercise it. We should decide whether local-first is a **day-one** property or a **track-2** capability.
2. **What's the smallest viable decentralization demo?** A Bridge-adjacent "contractor mobile" local-first accelerator using real Automerge + Keyhive in JS/TS on the client, talking to a .NET server for projection, would test the full story end-to-end without rebuilding the library in .NET.
3. **Keyhive maturity.** Keyhive is active Ink & Switch research as of April 2026 — not yet a 1.0 deliverable. Building Sunfish's crypto layer on a shifting target has risk. Does Sunfish wait, or does it adopt an earlier stable approximation?
4. **Licensing.** Automerge is MIT. Keyhive license is TBD per current Ink & Switch output. Confirm before any code integration or derivation.
5. **Schema + CRDT.** Sunfish wants a schema registry (§3); Automerge is schemaless. How do we reconcile at the entity level — schema as a projection layer on top of the CRDT, or schema enforced at write time with versioned migrations?

---

## 8. Sources

- [Automerge documentation — Welcome](https://automerge.org/docs/hello/)
- [Automerge Repo is a wrapper for the Automerge CRDT library](https://automerge.org/automerge-repo)
- [Automerge Repo: A "batteries-included" toolkit for building local-first applications](https://automerge.org/blog/automerge-repo/)
- [Keyhive notebook 05 — Syncing](https://www.inkandswitch.com/keyhive/notebook/05/)
- [Automerge sync protocol (Rust docs)](https://automerge.org/automerge/automerge/sync/index.html)
- [automerge/automerge (GitHub — core repo)](https://github.com/automerge/automerge)
- [automerge/automerge-repo-sync-server (GitHub — reference sync server)](https://github.com/automerge/automerge-repo-sync-server)
- Martin Kleppmann, "Automerge: Real-time data sync between edge devices" (MobiUK abstract)
- Peer review / set reconciliation: Automerge sync protocol paper (arxiv.org/abs/2012.00472)
