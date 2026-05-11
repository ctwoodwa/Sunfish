# Intake — Sunfish-Node: multi-stack reference cohort + portable protocol specs

**Date:** 2026-05-11 (kickoff)
**Requestor:** CO (per the *The Inverted Stack* book's framework-agnostic claim and the Galley editorial-tool reality)
**Request:** Promote Sunfish from a single-stack (.NET) implementation to a true multi-stack platform by extracting language-neutral protocol specs and producing a Node.js / TypeScript reference implementation alongside the existing .NET / Blazor MAUI reference.
**Pipeline variant:** `sunfish-feature-change` (large; multi-cohort) — may need to be decomposed into a cohort family during 01_discovery
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

Sunfish currently claims to be "framework-agnostic" in three places — the foundational paper (`local-node-architecture-paper.md`), the README, and ADR 0014 (Adapter Parity Policy). In practice, "framework-agnostic" is true for the **UI layer** (Blazor + React via `ui-core` contracts + `ui-adapters-blazor` / `ui-adapters-react`) but **false for everything else**:

- `kernel-sync` daemon is C#
- `kernel-audit` ledger is C# (signed-event chain in `Sunfish.Kernel.Audit`)
- `kernel-security` recovery + key handling is C#
- `foundation-channels` + `blocks-crew-comms` (W#45) is C#
- All the `foundation-*` packages are `.csproj`
- The reference Anchor accelerator is .NET MAUI
- The reference Bridge accelerator is ASP.NET Core

For a reader of *The Inverted Stack* who wants to build an Anchor-shaped app in Node, Python, Swift, or Rust, the current answer is: **rewrite Sunfish from scratch in your stack, or interop only over Bridge HTTP**. That undercuts the architecture's portability claim — one of the book's central propositions.

**Galley** is the immediate driver: it's a Zone-A Anchor-shaped app (creator's local-first editorial workshop for the book itself) but built in React + Node + Express because TypeScript was the right stack for the editor surface. Galley is currently borrowing Sunfish *patterns* (audit log, alignment-driven sentence model) without being able to import Sunfish *packages*. CO's call: galley should be a first-class **Sunfish-Node reference implementation**, not a one-off, and Sunfish itself should publish portable protocol specs that any stack can implement.

This is not a request to port Sunfish to Node. It is a request to **decompose Sunfish into language-neutral protocol specs + per-stack reference implementations**, where .NET is one stack and Node/TS is the second. Future apps in Swift / Rust / Python implement the same specs.

## Predecessors

- **The Inverted Stack** (book) — argues for the architecture, not for any stack. Multi-stack support closes the credibility gap between claim and implementation.
- **ADR 0014 — Adapter Parity Policy** — establishes the principle for the UI layer; this intake extends the principle to the runtime layer.
- **`Sunfish.Kernel.Sync.Protocol.WireProtocol`** — the sync-daemon wire protocol is already specified at the byte level (Appendix A of the book). It's the *cleanest* existing example of a portable spec — implementations in any language can interop. We need to do for the rest of Sunfish what the daemon-protocol already did for sync.
- **Galley** at `/Users/christopherwood/Projects/SunfishSoftware/galley` — a working Zone-A app in React + Node, currently consuming the book repo's chunked alignment data + remote GPU TTS/STT/Image/Music API. Borrows Sunfish patterns conceptually; would adopt Sunfish-Node references as they ship.

## Industry prior-art

- **Protocol Buffers + gRPC** — schema-defined wire formats, code-gen per stack. Closest analog to the model here.
- **JSON-RPC 2.0 + JSON-Schema** — language-neutral RPC spec; multiple impls per stack. Lighter-weight than protobuf, fits markdown+JSON-shaped Sunfish artifacts.
- **OpenAPI 3.x** — schema specifies HTTP surface; tooling generates clients per stack. Already used by the inference server galley talks to.
- **Yjs + Loro v0** — already-portable CRDT specs with implementations in multiple languages (JS, Rust, Python). Sunfish's CRDT layer would adopt one of these or co-publish a v0 spec.
- **Matrix protocol** — federation/sync protocol with reference servers in multiple languages (Rust, Node, Python). Architectural sibling to Sunfish's three-tier transport.
- **OAuth 2 + OIDC** — language-neutral identity protocol, libraries in every stack. Sunfish-equivalent for `kernel-security`'s identity envelope.

## Scope (proposed)

The cohort decomposes into ~5 sub-workstreams. 01_discovery will refine the boundaries; Stage-02 architecture decides whether to ship them as one ADR family or many.

### W-A. Protocol-spec extraction (foundation work; spec-only, no code)

Promote Sunfish's existing wire-format-defined primitives to first-class portable specs. Each gets a markdown spec + JSON-Schema (or equivalent) under a new `_shared/protocols/` directory or sibling repo. **No code changes to existing .NET impls — only documentation.**

Candidate protocols:

| Protocol | Current location | Spec deliverable |
|---|---|---|
| **Audit-event ledger** | `Sunfish.Kernel.Audit` types | `_shared/protocols/audit-event-v1.md` + `audit-event.schema.json` |
| **Sync-daemon wire protocol** | Appendix A of the book + `Sunfish.Kernel.Sync.Protocol` | already exists in book; extract to `_shared/protocols/sync-wire-v1.md` |
| **Manifest schemas** (chapter / chunk / alignment / bundle) | `audiobook.py` output + `BusinessCaseBundleManifest` | `_shared/protocols/{chapter,chunk,alignment,bundle}-manifest-v1.md` |
| **CRDT wire format** | `Sunfish.Foundation.Loro` (or future) | adopt Yjs/Loro v0 public spec by reference; document galley's binding |
| **Identity envelope** (Ed25519 signed-payload) | `Sunfish.Kernel.Security` | `_shared/protocols/identity-envelope-v1.md` |
| **Three-tier transport contract** (mDNS / mesh-VPN / Bridge-relay) | ADR 0061 + `foundation-transport` | `_shared/protocols/peer-transport-v1.md` |
| **Channel session protocol** (W#45 Crew Comms) | ADR 0076 + amendments | already speced; extract from ADR to `_shared/protocols/channels-v1.md` |

Each protocol spec is **canonical**; the .NET implementation becomes a *consumer* of the spec, not its definition.

### W-B. Sunfish-Node reference: minimal kernel surface

A new `sunfish-node/` sibling repo (or sub-repo) implementing the protocols above in TypeScript + Node. Ship in dependency order:

1. `@sunfish/audit-log` — append-only JSONL ledger matching audit-event-v1
2. `@sunfish/identity` — Ed25519 envelope sign/verify (`@noble/ed25519`)
3. `@sunfish/manifest` — TS types + Zod schemas for the 4 manifest shapes
4. `@sunfish/crdt` — wraps `yjs` (or `loro-crdt`) with the Sunfish-binding adapter
5. `@sunfish/sync-protocol` — wire-protocol codec (encode/decode the daemon frames)
6. `@sunfish/transport` — three-tier `IPeerTransport` equivalent (mDNS via `mdns`, relay via `ws`)
7. `@sunfish/channels` — Crew Comms session protocol (consumer of `transport` + `identity`)

Each package is small (~200-800 LOC). They mirror the C# `Sunfish.*` packages 1:1 at the contract level but live in a separate npm scope.

### W-C. Galley adopts the Sunfish-Node references

Galley today is bespoke. As `sunfish-node` packages ship, galley refactors:

- `services/book-server/audit-log.jsonl` (planned for galley anyway) → backed by `@sunfish/audit-log`
- Galley's chapter / chunk / alignment data structures → typed via `@sunfish/manifest`
- (Future) Galley's inline editor → `@sunfish/crdt` + `yjs` for prose state
- (Future) Live editor↔author collab → `@sunfish/channels`

Galley becomes the working proof-of-life for Sunfish-Node and a published reference for "how to build an Anchor-shaped app in TS+Node."

### W-D. Conformance test vectors (per protocol)

Each protocol spec ships with **test vectors** in `_shared/protocols/<proto>/test-vectors.json`. Both the .NET impl and the Node impl run the same vectors and must agree. This is how we keep the two stacks honest — the spec is the source of truth, not either implementation.

Pattern already established for ADR 0076-A3 conformance test vectors (W#45). Generalize.

### W-E. Documentation: "Implementing Sunfish in your stack"

A new `apps/docs/contributing/implementing-in-other-stacks.md` page that walks a contributor through the protocol specs in the order needed to build a minimal Sunfish-shaped runtime. The book itself gets a sidebar (Part IV / Playbook) referencing this. Closes the credibility gap publicly.

## Out of scope (for this cohort)

- **Porting Anchor-MAUI to Node Electron** — that's a separate "Anchor-Node" accelerator workstream. This cohort defines the substrate; the Anchor-Node accelerator is a downstream consumer.
- **Browser-only target** — sunfish-node is server-side TypeScript. Browser-side CRDT consumption (galley's frontend) goes via book-server, not directly.
- **Rust / Swift / Python references** — same model applies; future cohorts.
- **Replacing the .NET reference** — the .NET reference stays canonical for the existing accelerators and for any consumer that prefers .NET. This cohort makes Node a *peer*, not a *replacement*.

## Affected Sunfish Packages

Touched at the **spec** level (mostly docs):
- `_shared/` — new `protocols/` subtree
- `docs/adrs/` — new ADRs and possibly amendments to ADRs 0014, 0061, 0076 to acknowledge multi-stack
- `apps/docs/` — new contributor guide

Spec-extracted (no code change):
- `kernel-audit`, `kernel-sync`, `kernel-security`, `foundation-channels`, `foundation-transport`, `foundation-multitenancy` (TenantId envelope)

Affected outside this repo:
- `sunfish-node/` — new repo (or `services/sunfish-node-reference/` if monorepo decision)
- Galley (`/Users/christopherwood/Projects/SunfishSoftware/galley/`) — adopts packages as they ship
- The book repo (`the-inverted-stack`) — gains sidebar pointing at the multi-stack story

## Dependencies and Constraints

**Hard prerequisites (verified on origin/main 2026-05-08):**
- ADR 0061 (Three-Tier Peer Transport Model) Accepted ✓
- ADR 0076 (Crew Comms) Accepted ✓; A1+A2+A3 amendments landed
- W#45 substrate built ✓ — the Sunfish-Node channels package binds to W#45's protocol
- W#1 (Wave-1 security follow-ups) merged ✓

**Soft prerequisites:**
- The book's Appendix A (sync-protocol wire spec) exists ✓ — already a portable spec, just needs promotion to `_shared/protocols/`
- ADR 0014 (Adapter Parity Policy) — extends to runtime; ADR amendment may be appropriate

**Sequencing concerns:**
- W-A (spec extraction) must complete before W-B (Node refs) starts — implementations need a frozen spec target.
- W-D (test vectors) blocks any cross-stack interop claims — both impls must pass vectors before we say they conform.
- W-C (galley adoption) is iterative; can start ad-hoc but should formalize once W-B's first packages ship.

**Effort estimate (rough; refine in 01_discovery):**
- W-A: ~20-25h XO research + spec authoring
- W-B: ~40-60h sunfish-PM build (Node TS implementations)
- W-C: ~10-15h sunfish-PM in galley repo
- W-D: ~10-15h shared (vector generation + .NET-side runners)
- W-E: ~5h XO docs

Total: ~90-120h. Realistic delivery: **3-4 weeks** at one full-time researcher + part-time builder, or 6-8 weeks at half-capacity.

## Risks

1. **Spec drift between stacks.** Mitigation: test vectors per protocol; CI gate that runs the same vector against both impls.
2. **The .NET impl is the de-facto spec, not the markdown.** Common in protocol projects. Mitigation: every protocol-affecting change must update the markdown spec FIRST, then both impls. ADR-style: change the contract, then the consumers.
3. **Adoption friction in galley.** Galley's React+Node stack already has working primitives (e.g., its own typed clients). Wholesale replacement risks regressions. Mitigation: drop-in adoption — each `@sunfish/*` package replaces an exact piece of galley code, one at a time.
4. **The CRDT choice (Yjs vs Loro)** is itself a meaningful decision and affects Sunfish broadly. The .NET side currently uses YDotNet (= Yjs) per recent commits; Loro is the stated aspirational target. May warrant a separate pre-cohort ADR amendment to lock the choice for the Node references.
5. **Scope creep into "real Anchor-Node accelerator."** Resist. This cohort delivers substrate + reference packages + galley adoption. Building a desktop app (Electron / Tauri / Capacitor) on top of those packages is a follow-on cohort.

## Selected Pipeline Variant

- [x] **`sunfish-feature-change`** (large; cohort family) — protocol specs + new package family + galley adoption + conformance vectors + docs

## Next Steps

1. **CO promotes intake to active** (assigns priority and decides cohort vs separate workstreams).
2. **01_discovery** (XO research):
   - Confirm CRDT choice (Yjs vs Loro) — ADR amendment if needed
   - Decide repo layout: `sunfish-node/` sibling repo vs `services/sunfish-node-reference/` in this repo
   - Decompose W-A into per-protocol intakes if helpful (one ADR per protocol vs one cohort ADR with sub-sections)
   - Identify which 2-3 protocols ship first (audit-event + manifest are the safest; CRDT and channels are biggest)
3. **02_architecture**: ADR(s) for the protocol-spec model and per-package contracts.
4. **03_package-design**: per-package surface design (TS interfaces; mirror C# contracts).
5. **04_scaffolding** through **08_release** as standard.

## Open questions for CO

- **Repo layout.** Sibling `sunfish-node/` (cleanest separation, matches `the-inverted-stack/`) or sub-repo? Lean: sibling.
- **Distribution.** npm publish under `@sunfish/*` scope — is the org already registered? If not, alternate scope.
- **Yjs vs Loro for v1.** The .NET side currently has YDotNet on origin/main; the architecture paper names Loro as aspirational. The Node side starting with Yjs is lowest-friction; pinning to Loro requires the Rust core path. CO call required.
- **Book sidebar timing.** Should *The Inverted Stack* book wait for Sunfish-Node to ship before claiming multi-stack, or land a "in-flight" sidebar now?
- **Token-budget framing.** This cohort is ~90-120h XO+sunfish-PM. Realistic delivery: 3-4 weeks dedicated, or 6-8 weeks at half-capacity. Land galley's audio-first editor first, then this. Confirm sequencing.

---

*Authored by XO at CO request 2026-05-08; saved for Monday-morning 2026-05-11 kickoff. CO promotion required before XO begins 01_discovery.*
