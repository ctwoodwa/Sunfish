# Sunfish Platform Gap Analysis — 2026-04-18

**ICM stage:** 01_discovery
**Pipeline variant:** `sunfish-gap-analysis`
**Author:** Claude (Opus 4.7 1M), on behalf of Chris Wood
**Audience:** Architecture + product; feeds Stage 02 remediation design

---

## Executive Summary

This document enumerates every identifiable divergence between what the Sunfish platform
specification (v0.4) describes and what has landed on `origin/main` as of 2026-04-18. The
scope is the whole stack: kernel primitives (§3), package architecture (§2.3), multi-platform
host strategy (§4.5 / Appendix E), Property Management MVP (§6), federation first-wave (§10.4),
ingestion pipeline (§7), deferred open questions (Appendix C), research-note follow-ups,
and engineering hygiene concerns surfaced during the consolidation session.

Methodology: we compared the spec's structured promises against (a) the `packages/` tree as
it exists on `main`, (b) the two explicit parking-lot documents (Phase C — 14 items,
Phase D — 11 items), (c) Appendix C open questions, (d) the four research notes under
`docs/specifications/research-notes/`, (e) open GitHub issues (#12), and (f) the recent
`git log --oneline origin/main` history. Every gap below has an actual spec citation (or
plan-document citation) and an actual code (or absence-of-code) citation.

**Thirty-six gaps identified.** Priority breakdown: **4 P0**, **15 P1**, **13 P2**, **4 P3**.

**Top-five findings (recommended execution order):**

1. **G1** — the kernel package does not exist. `packages/foundation/` ships five of the seven
   kernel primitives (Assets, Crypto, Capabilities, Macaroons, PolicyEvaluator, Blobs), but
   there is no `packages/kernel/` that binds them to spec §3's namespace, no
   `ISchemaRegistry`, and no `IEventBus`. This blocks every downstream consumer that wants
   to code against spec §3 contracts directly.
2. **G2 + G3** — Schema Registry and Event Bus (kernel primitives 4 and 6) are entirely
   absent. No contracts, no reference implementations, no tests. Multiple other gaps
   (G19, G22, G28) depend on these two.
3. **G5** — the Bridge accelerator remains the PmDemo-heritage Blazor shell, not the PM MVP
   defined in §6 (leases, rent, inspections, maintenance, vendors, accounting, tax). This is
   the headline revenue-adjacent gap.
4. **G6 + G7** — Phase 2.5 multi-platform hosts (MAUI / Photino) are absent, and the Iced
   lessons-learned contracts in `ui-core` are all placeholder-absent as well. v0.4 locked the
   strategy but no shipping code matches yet.
5. **G8** — IPFS + IPFS-Cluster federation (Phase D-6 Raft-pinned replication with 24-hour
   attestation) remains parked because Docker/Podman was not available in the implementer's
   session. Everything in Phase D Pattern-A/Pattern-B depends on this.

**Recommended execution order at a glance:** stabilize foundation (G32, G33, G34, G35 — fast
hygiene wins) in parallel with creating the kernel package facade (G1), then cut ISchemaRegistry
(G2) and IEventBus (G3), then tackle either the Phase 2.5 host family (G6/G7) or the Phase D
blob-replication wave (G8/G9/G10) depending on whether the next business-critical demo is
mobile-first (host family wins) or cross-jurisdiction federation-first (Phase D wave wins).
Phase 4 PM vertical (G5 + G14–G18) is the longest-pole critical path and should start in
parallel no later than the moment G1 lands.

---

## Methodology

### What was reviewed

- **Spec v0.4** (`docs/specifications/sunfish-platform-specification.md`, 2643 lines) in full,
  with close reads of §2.3 package layers, §3 kernel primitives, §4.3–4.8 phased roadmap,
  §4.10 tensions list, §6 PM MVP, §7 ingestion modalities, §10.4 federation patterns,
  Appendix C open questions, Appendix D v0.3 revisions, Appendix E v0.4 host pivot.
- **Phase plans** — Platform Phase A/B/B-blobs/C/D plans under `docs/superpowers/plans/`
  (nine plan documents).
- **Parking lot documents** — `2026-04-18-platform-phase-C-parking-lot.md` (14 items),
  `2026-04-18-platform-phase-D-parking-lot.md` (11 items).
- **Research notes** — `automerge-evaluation.md`, `ipfs-evaluation.md`,
  `external-references.md`, `multi-platform-host-evaluation.md`.
- **Open issues** — `gh issue list --state open` returned one: #12 (Phase C parking lot).
- **Closed issues** — #9, #10, #11, #13 (all closed by commits on `main` between 2026-04-17
  and 2026-04-18). These are explicitly excluded from the gap list below.
- **Code base** — spot-checked `packages/foundation/`, `packages/ui-core/`,
  `packages/federation-*/`, `packages/ingestion-*/`, `packages/blocks-*/`,
  `accelerators/bridge/`, and `apps/` to verify presence/absence of named types.
- **Git history** — `git log --oneline origin/main -20`.

### Priority heuristic

- **P0 (Critical)** — blocks a demonstrated use case (PM vertical, federation patterns,
  multi-platform host strategy); no acceptable workaround; high user-facing impact.
- **P1 (Major)** — substantial spec divergence; workarounds exist but are fragile or
  generate maintenance cost if deferred; moderate user-facing impact.
- **P2 (Minor)** — nice-to-have; low user impact today; polish / cleanup; spec alignment
  nicety.
- **P3 (Future)** — speculative or research-grade; legitimate parking-lot status; probably
  not in scope before v1.0.

Effort notation: **S** = 1–3 days; **M** = 1–2 weeks; **L** = 3–4 weeks; **XL** = 6+ weeks.

### Explicitly out of scope

- **Closed v0.3 tensions (Appendix D, T1–T18 + T-X).** These are now canonical shipped shapes
  and not gaps. If a future revision changes the spec _away_ from the shipped shape, that
  would re-open a gap; today the spec has been amended to match.
- **Dropped strategic decisions.** Per Appendix E.1 and Appendix C #5/#6, React adapter,
  React parity tests, and GPU-native Blazor rendering are **not** gaps — they are deliberate
  de-scopings.
- **Issues already closed by `origin/main`:** #9 Phase A Postgres backend, #10 Phase D-5
  IpfsBlobStore (landed via #17), #11 Phase D patterns (partially — Pattern C landed via
  #16; A and B remain as G9/G10), #13 spec v0.3 revisions (closed via #14).
- **`compat-telerik` parity gaps** — compat-telerik is explicitly best-effort by charter
  (CLAUDE.md §compat-telerik Policy). Unless a specific mapping becomes a business
  requirement, deltas are approved gaps.
- **Speculative schemas** — lease / invoice / deficiency etc. referenced in §6 are counted
  as PM-vertical gaps (G14–G18) but not double-counted as schema-registry gaps.

---

## Gap Inventory

Gaps are numbered `G1…G36`. Priorities, categories, and effort are normalized for cross-gap
comparison in the execution order section.

---

### G1 — No `packages/kernel` namespace exists

**Priority:** P0
**Category:** Kernel
**Spec references:** §2.3 (Layer 2), §3 (all subsections), §4.3 remaining scope, §4.10 item 1

**Gap Description**
Spec §2.3 lists `packages/kernel` as Layer 2 of the layered architecture, and §3 describes
seven kernel primitives. The repo has five of those primitives shipped under
`packages/foundation/` (Assets as entity+version+audit, Crypto as signing, Capabilities,
Macaroons, PolicyEvaluator as ReBAC, Blobs as the CID blob store) — but no
`packages/kernel/` directory exists, no types live in a `Sunfish.Kernel.*` namespace, and
no façade binds the foundation subfolders to §3.1-§3.7's declared interfaces. Consumers
that follow the spec verbatim (`IEntityStore`, `IAuditLog`, etc.) will not find those
interfaces — they must discover the foundation-namespaced equivalents (`IAssetStorage`,
`IAuditLog` in `Sunfish.Foundation.Assets.Audit`, etc.).

**Impact Assessment**
- Affected users: every downstream consumer reading the spec; every future block author;
  external reviewers evaluating whether the repo implements the spec.
- Severity: **major** — it doesn't block shipping (types exist, just not at the spec-named
  path), but the optical gap between spec-says-`packages/kernel` and code-says-
  `packages/foundation/Assets` is the single largest spec-vs-repo discrepancy identified.
- Workarounds: documentation call-out explaining the foundation-vs-kernel naming; consumers
  can find the primitives by searching for contract names.

**Root Cause**
The foundation package was established during migration Phase 1 before the spec's §3 named
the kernel. When the kernel primitives shipped in Phase A/B, they were placed under
`foundation/` to keep them close to the code already there, not re-homed into a new kernel
package. No code-reorganization PR has repatriated them, and no façade has been added.

**Options**

#### Option A: Physical re-home — create `packages/kernel/`, move primitives
- Pros: Matches spec verbatim; clean dependency graph (`foundation` below `kernel` as spec
  shows); consumer discovery works as spec claims.
- Cons: Huge churn — every `using Sunfish.Foundation.Assets.*` line across the repo moves.
  Breaking change for any early downstream consumer. Breaks all the `HasNoBlazorDependencyTests`
  and dependency-guard tests. Extra PRs across all accelerators and blocks.
- Risk: High — regression-prone across ~150 files.
- Effort: **L** (3–4 weeks including consumer sweep).

#### Option B: Virtual façade — create `packages/kernel/` that type-forwards into foundation
- Pros: Zero consumer churn; spec-named types exist; foundation's current consumers
  unchanged. Kernel can evolve to add `IEventBus` and `ISchemaRegistry` (G2, G3) at the
  right namespace from day one.
- Cons: Two public names for the same type (forwarding + native); some developer confusion.
  Deprecation of the foundation names would be a separate later cycle.
- Risk: Low.
- Effort: **M** (4–7 days to stand up the façade + XML docs pointing to foundation origins).

#### Option C: Rename foundation → kernel only (no move), update spec to match
- Pros: One-shot rename across the repo; no dual names.
- Cons: `foundation` is also an architectural layer name and not a synonym for kernel;
  renaming loses the foundation-vs-kernel distinction that §2.3 deliberately draws. Also
  breaks every consumer at once.
- Risk: Medium — pushes the naming problem into the spec.
- Effort: **M** (but risks future re-split).

**Recommended Path**
**Option B — virtual façade.** Stand up `packages/kernel/` with type-forwards (or
namespace-aliased re-exports) for the primitives that already ship, plus stub interfaces
for G2 and G3, plus a `README.md` saying "the kernel contracts live here; implementations
live in `foundation/`". This lets the spec's §2.3 be true without a destructive move, and
makes G2/G3 one-commit additions rather than two-commit (create-namespace + add-contract).
Option A can be revisited later as a v2.0 cleanup if the indirection proves annoying.

**Effort Estimate**
**M** (4–7 days).

**Dependencies**
None blocking. Closes opportunistically alongside G2, G3 (which create their contracts at
the `Sunfish.Kernel.*` namespace from the start).

---

### G2 — Schema Registry primitive (§3.4) not shipped

**Priority:** P0
**Category:** Kernel
**Spec references:** §3.4, §4.10 item 4, Appendix D open-question #2

**Gap Description**
Spec §3.4 declares `ISchemaRegistry` with `GetAsync / RegisterAsync / ValidateAsync /
PlanMigrationAsync / MigrateAsync`, a `Schema` record carrying a `JsonSchema Definition`,
`ParentSchemas`, `Migrations`, and `Tags`. The spec requires schemas to be JSON Schema
draft 2020-12, content-addressed by CID for federated verifiability, with `jsonata` as the
default migration engine.

None of this ships. Entity schemas in the repo are C# classes. There is no `ISchemaRegistry`
interface, no runtime-registered JSON Schema, no migration pipeline, no content-addressed
schema storage. Every `IngestedEntity.SchemaId` today is a string that no registry actually
resolves; `Sunfish.Foundation.Assets.Common.SchemaId` is just a `record struct Wrap(string Id)`.

**Impact Assessment**
- Affected users: anyone wanting runtime schema registration (every federated consumer),
  schema migration (every evolving deployment), JSON-Schema-based form authoring
  (Phase 5 blocks-forms AI authoring flow — G22), per-schema blob-boundary override
  (Phase C parking-lot #12 — G20), schema-aware validation in ingestion (every modality
  today bypasses schema validation).
- Severity: **major** — fundamental kernel primitive; blocks several downstream gaps.
- Workarounds: hardcoded C# models (currently in use); brittle and non-federated.

**Root Cause**
Phase 1 focused on Blazor UI; Phase A focused on asset/version/audit/hierarchy; Phase B
focused on crypto/capabilities; Phase C focused on ingestion wiring; Phase D focused on
federation transport. No phase has taken schema-registry as its named deliverable. It is
identified in the spec's "deferred to v0.4+" list (Appendix D open-question #2) without an
owning phase.

**Options**

#### Option A: Build from scratch on `JsonSchema.Net` (Greg Dennis, MIT)
- Shape: `Sunfish.Kernel.SchemaRegistry` package with `InMemorySchemaRegistry` (dev) and
  `PostgresSchemaRegistry` (prod). Content-addressed schema storage via the existing
  `IBlobStore` (a schema's CID is its `FromBytes` of canonical-JSON). `IMigrationEngine`
  abstraction with `jsonata` reference implementation (`Jsonata.Net.Native`).
- Pros: Uses the recommended parser from spec §3.4; clean fit with spec naming.
- Cons: ~3 weeks to ship a credible MVP (registry + validation + migration).
- Risk: Medium — jsonata-on-.NET (`Jsonata.Net.Native`) is less battle-tested than the
  JS reference; migration correctness needs heavy tests.
- Effort: **L** (3 weeks).

#### Option B: Ship validation-only now, migration-later
- Shape: Register schemas, validate entity bodies; stub `PlanMigrationAsync` /
  `MigrateAsync` with `NotSupportedException`.
- Pros: Faster to ship (~1 week); unlocks G20 (per-schema blob-boundary) and G22
  (AI form authoring) validation path.
- Cons: Migration is the harder half; deferring it just pushes the problem.
- Risk: Low.
- Effort: **M** (1 week for the validation half alone).

#### Option C: Wrap an existing registry (Confluent Schema Registry, Apicurio)
- Pros: Offloads registry implementation to a proven OSS service; matches Kafka/event-bus
  ecosystems.
- Cons: Confluent/Apicurio are Kafka/Avro-centric; JSON Schema support exists but is
  secondary. Adds a service dependency for what should be an in-process primitive.
- Risk: Medium-high — doesn't match the "kernel runs in-process" design.
- Effort: **L** (integration + operational runbook).

**Recommended Path**
**Option B** now (ship validation path, stub migration) to unlock G20 and G22 quickly,
then **Option A**'s migration half in a follow-up (targeting v0.6 of this spec). Use
`JsonSchema.Net` for validation and content-address schemas via `IBlobStore`; that path
is forward-compatible with the migration engine landing later.

**Effort Estimate**
**M** (1 week) for Option B; then **M** (1–2 weeks) for the migration half in a follow-up.
Combined: L.

**Dependencies**
G1 (kernel namespace) ideally lands first so `ISchemaRegistry` can live at
`Sunfish.Kernel.SchemaRegistry` from commit 0.

---

### G3 — Event Bus primitive (§3.6) not shipped

**Priority:** P0
**Category:** Kernel
**Spec references:** §3.6, §2.7, §4.3 remaining scope, §4.10 item 1

**Gap Description**
Spec §3.6 declares `IEventBus` with `PublishAsync / SubscribeAsync / GetCheckpointAsync /
AdvanceCheckpointAsync`, per-entity ordering, idempotent delivery, Ed25519-signed events,
and a transport-neutral design that can back to in-proc channels, MassTransit, Wolverine,
Kafka, or libp2p pubsub. Spec §2.7 frames it as the substrate for cross-block
communication.

Grep across `packages/` for `IEventBus` returns zero matches. There is no
`Sunfish.Kernel.EventBus` package, no in-proc reference implementation, no subscription
machinery, no checkpoint store. Phase D's `ISyncTransport` covers federation peer-to-peer
envelopes; that is not the kernel event bus — it is one possible backend for it.

**Impact Assessment**
- Affected users: any block that needs to react to entity events (inspections creating
  deficiencies, maintenance spawning work-orders — §6.3/§6.4), any federation consumer
  that wants to forward kernel events to peers, the BusinessRuleEngine (G4) that needs
  event-driven triggering, the ingestion pipeline (post-ingest handlers today bypass the
  event bus because there isn't one).
- Severity: **major** — second-largest architectural gap after schema registry.
- Workarounds: direct method calls between blocks (fragile, couples them at compile-time);
  `IPostIngestHandler<T>` (ingestion-scoped only).

**Root Cause**
Same as G2 — no phase has owned event-bus as its deliverable. Phase D built a federation
transport but explicitly documented that kernel event-bus is a separate concern.

**Options**

#### Option A: In-proc `Channel<Event>` reference implementation + MassTransit adapter
- Shape: `Sunfish.Kernel.EventBus` with `InMemoryEventBus` default, `MassTransitEventBus`
  for RabbitMQ/ASB/SQS deployments. Per-entity-ID ordering via a keyed actor pattern
  (one channel per entity or a consistent-hash partition).
- Pros: Spec-faithful; MassTransit is the most-adopted .NET bus; covers both dev and prod
  deployments immediately.
- Cons: Two backends to test; MassTransit has its own saga story (which could conflict
  with durable workflows — G13).
- Risk: Medium.
- Effort: **L** (2–3 weeks including conformance tests).

#### Option B: Ship in-proc only; defer distributed backends
- Shape: `InMemoryEventBus` backed by `System.Threading.Channels.Channel<Event>`; per-entity
  ordering via `ConcurrentDictionary<EntityId, Channel<Event>>`.
- Pros: Small shipping surface; easy to test; kitchen-sink demo runnable on day 1.
- Cons: Doesn't cover production-scale multi-process deployments; adds migration cost
  later.
- Risk: Low.
- Effort: **M** (1 week).

#### Option C: Adopt Wolverine directly as kernel event bus
- Shape: `Sunfish.Kernel.EventBus.Wolverine` as the only implementation.
- Pros: Bridge accelerator already uses Wolverine; tighter JasperFx/Marten integration;
  durable-inbox story built in.
- Cons: Forces every kernel consumer to pull Wolverine as a transitive dep; conflicts
  with the "transport-neutral" framing of spec §3.6; narrows backend choice.
- Risk: Medium — architectural lock-in.
- Effort: **M** (2 weeks).

**Recommended Path**
**Option B now, Option A next.** Ship `InMemoryEventBus` + contracts + conformance tests
first; add `MassTransitEventBus` + integration tests in a follow-up. Retain the spec's
transport-neutral framing; do not bet on Wolverine at the kernel layer. Kernel contract
is stable once signed-event shape is locked.

**Effort Estimate**
**M** (1 week) for Option B ship; then **M** (2 weeks) for MassTransit follow-up.

**Dependencies**
G1 (kernel namespace); interacts with G13 (durable workflow execution engine — Wolverine
or Temporal — which sits on top of event bus, not underneath it).

---

### G4 — `BusinessRuleEngine` not integrated with kernel event bus

**Priority:** P2
**Category:** Kernel
**Spec references:** §4.10 item 6, §4.6 Phase 3 workflow orchestration

**Gap Description**
`packages/foundation/BusinessLogic/BusinessRuleEngine/` exists as a scaffold but is
orphaned — it isn't wired to any event source, doesn't subscribe to the (non-existent —
see G3) kernel event bus, and produces no side effects in any running demo.

**Impact Assessment**
- Affected users: any consumer expecting event-driven rule evaluation (e.g., "when rent is
  30 days late, fire the late-fee rule" — §6.2).
- Severity: **minor** — the scaffold is clearly marked as "starting point" and no feature
  has been advertised around it yet.
- Workarounds: direct callers invoke rule methods; the rule engine works as a library but
  not as a reactive substrate.

**Root Cause**
Migration Phase 1 ported the rule engine from Marilo intact, but no subsequent phase has
wired it to a reactive source because the event bus (G3) doesn't exist.

**Options**

#### Option A: Wire `BusinessRuleEngine` to kernel event bus once G3 lands
- Pros: Natural next step; small change if G3 ships first.
- Cons: Depends on G3.
- Risk: Low.
- Effort: **S** (2–3 days after G3).

#### Option B: Deprecate the scaffold, replace with a workflow-block subsumption
- Pros: One less orphan; aligns with §4.6 Phase 3 workflow block.
- Cons: Throws away investment; current consumers (if any) break.
- Risk: Low — scaffold is scaffold.
- Effort: **S** (1–2 days to deprecate).

#### Option C: Leave as-is; document as "manual rules library"
- Pros: Zero work.
- Cons: Spec tension item 6 stays open indefinitely; user confusion.
- Risk: Low.
- Effort: **S** (hours for doc).

**Recommended Path**
**Option A** after G3 lands. Document the scaffold's role in the meantime.

**Effort Estimate**
**S** (2–3 days after G3).

**Dependencies**
G3.

---

### G5 — Bridge accelerator is a PmDemo port, not a PM MVP

**Priority:** P0
**Category:** Governance / vertical
**Spec references:** §6 (all subsections), §4.7 Phase 4, §4.10 item 5

**Gap Description**
`accelerators/bridge/` is the PmDemo Blazor shell migrated into the Sunfish repo: 14 screens
(boards, tasks, timelines, admin screens) plus Features / Handlers / Messages folders.
Spec §6's MVP requires: Leases (§6.1), Rent Collection (§6.2), Inspections (§6.3),
Maintenance Workflows (§6.4), Vendor Quotes (§6.5), Accounting (§6.6 — light), Tax
Reporting (§6.7), Audit Trail (§6.8). Bridge has none of the domain entities
(`Lease`, `RentSchedule`, `Invoice`, `Payment`, `Inspection`, `Deficiency`, `WorkOrder`,
`Vendor`, `Quote`, `RFQ`, `JournalEntry`, `DepreciationSchedule`, `TaxReport`) wired to
any kernel backend. No DocuSign integration, no Plaid integration, no QuickBooks export.

**Impact Assessment**
- Affected users: every prospective PM-vertical customer (the explicit primary commercial
  wedge per §13.1), every external stakeholder evaluating "does Sunfish actually deliver
  property management."
- Severity: **major** — this is the single most-visible commercial gap. §13.1 targets
  "small-to-midsize property managers (50–2000 units)" as the initial segment and Bridge
  is the only path to that segment.
- Workarounds: none — no other PM product ships on Sunfish.

**Root Cause**
Migration Phase 9 scoped "port PmDemo into Bridge" as a tactical rebrand, not a ground-up
§6 build. The spec's Phase 4 is called out as "partial" for exactly this reason. The spec
and migration plan diverged here intentionally: migration is tactical, spec is strategic.

**Options**

#### Option A: Full MVP build (all §6.1–§6.8)
- Pros: Hits the spec's exit criterion "a small property manager can operate Bridge as their
  system of record end-to-end" directly.
- Cons: XL effort; touches every kernel primitive, every integration (DocuSign, Plaid,
  QuickBooks), every block.
- Risk: High — requires kernel (G1–G4) + schemas (G2) + workflows (G13) to land first or
  in parallel.
- Effort: **XL** (4–6 months, per spec §4.7 own estimate).

#### Option B: Vertical slice — Inspections end-to-end, other features stubbed
- Pros: Demonstrates the end-to-end story for one §6 feature (most compelling for
  federation-enabled inspection with code-enforcement peer); keeps scope bounded.
- Cons: Leaves 6 of 8 features unshipped; "PM MVP" framing is only partly true.
- Risk: Medium.
- Effort: **L** (2 months) — inspections depend on forms (shipped), workflow (G13 partial),
  blob store (shipped), schemas (G2), federation (shipped as first-wave).

#### Option C: Reframe Bridge as "reference vertical shell," ship §6 features as blocks
- Pros: Separates the vertical app-shell problem from the feature-block problem; blocks
  reusable across verticals.
- Cons: Longest timeline to a complete Bridge; risks Bridge feeling perpetually incomplete.
- Risk: Low.
- Effort: **XL** aggregated.

**Recommended Path**
**Option B** (inspections vertical slice) to prove the §6 pattern, then fold in remaining
features as blocks per Option C's strategy. Demo path: a lease-less, inspection-focused
Bridge + a code-enforcement peer, federating via the shipped Phase D transport.

**Effort Estimate**
**L** (2 months) for Option B. **XL** (4–6 months) for the complete Option A version.

**Dependencies**
G1, G2, G3 (kernel), plus individual block gaps G14 (leases), G15 (rent), G16
(maintenance/vendors), G17 (accounting), G18 (tax) for feature completeness.

---

### G6 — Multi-platform hosts (MAUI / Photino) not shipped

**Priority:** P1
**Category:** Infrastructure
**Spec references:** §4.5 Phase 2.5, Appendix E.2, §2.3 Layer 8

**Gap Description**
Spec v0.4 §4.5 and Appendix E.2 codify Phase 2.5: five host packages
(`hosts-web`, `hosts-desktop-maui`, `hosts-desktop-photino`, `hosts-mobile-maui`,
`hosts-native-maui` optional). `ls packages/` shows zero host packages. The existing
Blazor Server / WASM host lives under `apps/kitchen-sink/` and is not renamed to
`hosts-web` yet. No MAUI project file, no Photino dependency, no iOS or Android
entry point exists in the repo.

**Impact Assessment**
- Affected users: anyone wanting to ship Bridge (or any Sunfish app) as a Windows/macOS
  desktop, iOS / Android mobile, or offline-first mobile app. The spec §4.5 exit criterion
  "Bridge's PM inspection workflow works end-to-end on an iPad + a Windows laptop + a web
  browser" is blocked entirely.
- Severity: **major** — codified strategy without execution.
- Workarounds: web-only deployments; PWA installability on mobile (degraded UX).

**Root Cause**
v0.4 locked the strategy 2026-04-18 (literally one day before this analysis) but no
implementation plan has been executed yet.

**Options**

#### Option A: Full Phase 2.5 per the spec plan
- Shape: rename `apps/kitchen-sink/` host half to `packages/hosts-web/`, create
  `hosts-desktop-maui/`, `hosts-desktop-photino/`, `hosts-mobile-maui/` with MAUI
  Blazor Hybrid entry points (~100 LOC each). Axe-core-blazor CI; app-store
  packaging pipelines.
- Pros: Closes the spec exit criterion directly.
- Cons: Mobile app-store compliance alone is L-scale (App Store review loop, Android
  Play Store, code signing). Windows MSIX signing and macOS notarization each add days.
- Risk: Medium-high — app-store review can surface MAUI platform bugs that take weeks
  to route around.
- Effort: **XL** (3–4 months per spec §4.5 own estimate).

#### Option B: Desktop-only Phase 2.5 — ship `hosts-desktop-maui` + `hosts-desktop-photino`
- Pros: Covers Windows + macOS, drops the mobile-app-store work.
- Cons: Mobile is the more strategically valuable surface (inspectors in the field).
- Risk: Low-medium.
- Effort: **L** (4–6 weeks).

#### Option C: Mobile-only Phase 2.5 via MAUI only
- Pros: Targets the highest-value surface first.
- Cons: Desktop deferred indefinitely; leaves a Photino gap too.
- Risk: Medium.
- Effort: **L** (6 weeks).

**Recommended Path**
**Option B first** (desktop) to unblock power-user workflows and prove the MAUI Hybrid
rendering works; **Option C next** (mobile) for the Bridge inspector workflow. Option A
then folds both together at the deferred native-MAUI + app-store-packaging level.

**Effort Estimate**
**XL** (3–4 months) aggregated; sequenceable as L + L.

**Dependencies**
G7 (Iced-lessons ui-core contracts, though the contracts are additive so hosts can ship
against the existing 228 components before G7 lands).

---

### G7 — Iced lessons-learned ui-core contracts absent

**Priority:** P1
**Category:** UI
**Spec references:** §4.5 Phase 2.5 in scope item 6, Appendix E.3 table (L1–L6)

**Gap Description**
Appendix E.3 enumerates seven additive `ui-core` contracts derived from Iced (Rust GUI)
lessons: `ISunfishRenderer`, `IClientTask<TMessage>`, `IClientSubscription<TMessage>`,
per-widget `Style` records, `StateMachineComponent<TState, TMessage>`, `ISunfishOperation`,
`SunfishElement<TMessage>`. Grep in `packages/ui-core/Contracts/` shows three files:
`ISunfishCssProvider.cs`, `ISunfishIconProvider.cs`, `ISunfishJsInterop.cs`. None of the
seven new contracts ship.

**Impact Assessment**
- Affected users: complex flow authors (inspection wizards, multi-step forms, real-time
  sensor dashboards); future renderer implementers (MAUI-native, Avalonia).
- Severity: **moderate** — the 228 components work today without these; the contracts
  unlock future complexity rather than solving current pain.
- Workarounds: ad-hoc `async void`, direct `@ref`, CSS-class-string building. The current
  approach works; it just doesn't scale.

**Root Cause**
Research note `multi-platform-host-evaluation.md` landed 2026-04-18; implementation not
started.

**Options**

#### Option A: Ship all seven contracts as empty interfaces + default implementations
- Pros: Matches spec surface exactly; unblocks future consumers; low code footprint.
- Cons: Empty interfaces without real-world validation feel speculative.
- Risk: Low.
- Effort: **M** (1–2 weeks for contracts + unit tests + one worked example per contract).

#### Option B: Ship the two HIGH-ROI contracts first (L1 renderer, L2 task/subscription)
- Pros: Targets the contracts with actual shipping need (renderer unlocks G6 Option A's
  native MAUI future; task/subscription unlocks real-time sensor UIs); less speculative.
- Cons: Other five contracts remain a gap.
- Risk: Low.
- Effort: **M** (1 week for two contracts).

#### Option C: Defer entirely until Phase 2.5 hosts surface concrete needs
- Pros: Zero speculative code; spec stays aspirational until validated.
- Cons: Hosts gap (G6) ships without the renderer abstraction the spec envisions,
  creating retro-fit risk.
- Risk: Medium — hosts couple to the wrong API if contracts aren't there.
- Effort: **S** (hours for doc).

**Recommended Path**
**Option B.** Ship `ISunfishRenderer` and `IClientTask<T>` / `IClientSubscription<T>`
first. They have the clearest application (renderer unlocks native rendering; task/sub
unlocks real-time ingestion UIs). Defer L3–L6 until G6 Option B or C needs them.

**Effort Estimate**
**M** (1 week for Option B; 1–2 weeks for Option A).

**Dependencies**
None blocking; should land before G6 hosts if possible.

---

### G8 — IPFS-Cluster Raft pinning + 24h attestation (Phase D-6)

**Priority:** P1
**Category:** Federation
**Spec references:** §3.7, §10.4, Phase D parking-lot #2

**Gap Description**
Phase D-5 (IpfsBlobStore via Kubo HTTP RPC) landed via PR #17. Phase D-6 (IPFS-Cluster with
Raft consensus at replication factor 3, plus 24-hour signed attestation per spec §10.4) is
parked due to Docker/Podman unavailability during the Phase D implementer's session. No
`Sunfish.Federation.BlobReplication.Cluster` package exists.

**Impact Assessment**
- Affected users: every federated deployment that needs guaranteed cross-peer blob
  replication (PM + city code enforcement, multi-command military bases, cross-jurisdiction
  inspection data). Blob availability is single-point-of-failure without cluster pinning.
- Severity: **major** — the Phase D worked-example patterns A and B (G9, G10) both depend
  on this.
- Workarounds: single-IPFS-node mode (already shipped); doesn't guarantee replication.

**Root Cause**
Infrastructure-gated (Docker/Podman requirement). The implementer could not run the
required IPFS-Cluster Testcontainer in their session. Not a design or spec problem.

**Options**

#### Option A: Set up local Docker/Podman + ship D-6 directly
- Pros: Unblocks this entire arm of the federation story; straightforward implementation
  against existing research.
- Cons: Requires local container runtime (Windows WSL2 or native Linux). Not a one-hour
  setup.
- Risk: Low if infra available.
- Effort: **M** (2 weeks implementation) + S (infra setup).

#### Option B: Adopt GitHub Codespaces / remote dev container
- Pros: Avoids local-machine infra; CI can run the container tests.
- Cons: Slow dev loop; still blocks any follow-up work that needs the container locally.
- Risk: Low.
- Effort: **M** (2–3 weeks with remote-dev friction tax).

#### Option C: Mock IPFS-Cluster surface, ship contract-only
- Pros: Doesn't require runtime; allows G9/G10 to partially proceed.
- Cons: Doesn't prove the integration works; shipping unused adapter code.
- Risk: High — mock-only code that has never touched real IPFS-Cluster is a footgun.
- Effort: **M** (1 week).

**Recommended Path**
**Option A.** Container infra must be stood up; it is foundational for all future
federation work. One-time cost, repeat dividend.

**Effort Estimate**
**M** (2 weeks after Docker/Podman setup).

**Dependencies**
Infrastructure (Docker/Podman). Enables G9 and G10.

---

### G9 — Pattern A worked example (PM + city code enforcement)

**Priority:** P1
**Category:** Federation
**Spec references:** §10.4, Phase D parking-lot #3

**Gap Description**
Phase D-7 worked example: two PM companies push inspection entities + attached drone
imagery blobs to a municipal code-enforcement agency; all three peers run independent
Sunfish nodes; federation routes via signed envelopes over HTTP. Entity-sync +
capability-sync legs are individually shippable but the composed scenario is parked
because the blob-replication leg needs G8.

**Impact Assessment**
- Affected users: demo/sales narrative for cross-jurisdiction use cases; validation that
  all Phase D primitives compose.
- Severity: **major** — key demo asset for the cross-jurisdictional federation story.
- Workarounds: partial scenarios that elide blob replication.

**Root Cause**
Depends on G8's blob-replication primitive.

**Options**

#### Option A: Full scenario after G8 lands
- Pros: Complete story; hits spec exit criterion exactly.
- Cons: Sequenced behind G8.
- Risk: Low once G8 is in.
- Effort: **M** (1–2 weeks building the three-peer demo + tests).

#### Option B: Entity-sync + capability-sync only; skip blobs
- Pros: Can ship before G8.
- Cons: Incomplete story; drone-imagery part of the PM vertical is the most visually
  compelling piece.
- Risk: Low.
- Effort: **M** (1 week).

**Recommended Path**
**Option A** — fold into G8 wave.

**Effort Estimate**
**M** (1–2 weeks).

**Dependencies**
G8.

---

### G10 — Pattern B worked example (base command + air-gapped sneakernet)

**Priority:** P2
**Category:** Federation
**Spec references:** §2.5 sneakernet fallback, §10.4, Phase D parking-lot #4

**Gap Description**
Phase D-8 worked example: base command + air-gapped child bases via sneakernet. Envelopes
serialized to USB, carried across the air gap, replayed at the other end. Signature
verification + nonce deduplication + CRDT merge all hold across the gap. Currently parked
behind G8 (blob replication — sneakernet needs to carry blobs too).

**Impact Assessment**
- Affected users: military-base accelerator (Phase 5 secondary vertical); any air-gapped
  operational environment.
- Severity: **moderate** — drives the Base accelerator design but is a Phase 5 concern.
- Workarounds: online-only federation (which is exactly the problem).

**Root Cause**
Depends on G8 for the blob half.

**Options**

#### Option A: Full sneakernet after G8
- Pros: Complete; validates the fallback federation topology the spec promises.
- Cons: Lower-urgency than Pattern A.
- Risk: Low.
- Effort: **M** (2 weeks).

#### Option B: Entity + capability sneakernet only
- Pros: Can ship before G8.
- Cons: Incomplete story.
- Risk: Low.
- Effort: **M** (1 week).

**Recommended Path**
**Option A.** Sequence after G9.

**Effort Estimate**
**M** (2 weeks).

**Dependencies**
G8.

---

### G11 — Real-time streaming sensor ingestion (MQTT / IoT Hub / Kinesis)

**Priority:** P2
**Category:** Ingestion
**Spec references:** §7.4, Phase C parking-lot #3, Issue #12 sub-item 1

**Gap Description**
Current `Sunfish.Ingestion.Sensors` handler is batch-only (stream in, entity out). Spec
§7.4 envisions persistent MQTT / IoT Hub / Kinesis subscriptions with per-reading event
emission. Requires hosted-service lifecycle and per-tenant quota machinery that Phase C
explicitly scoped out.

**Impact Assessment**
- Affected users: Transit accelerator (predictive-maintenance sensor streams); any IoT-
  heavy PM deployment; Phase D real-time federation scenarios.
- Severity: **moderate** — Phase 5 vertical blocker, not a Phase 1 blocker.
- Workarounds: batch polling at the sensor side (degraded latency).

**Root Cause**
Phase C shipped the batch shape deliberately; streaming requires hosted-service + quota
infrastructure.

**Options**

#### Option A: MQTT first, IoT Hub + Kinesis as follow-ups
- Pros: MQTT is the most-federated protocol; MQTTnet library is mature.
- Cons: Hosted-service lifecycle management is non-trivial.
- Risk: Medium — backpressure + reconnect semantics need care.
- Effort: **L** (3 weeks).

#### Option B: Ship quota middleware (G13-adjacent) and streaming together
- Pros: Two parking-lot items addressed together.
- Cons: Larger change footprint.
- Risk: Medium.
- Effort: **L** (4 weeks).

**Recommended Path**
**Option A** first; quota middleware folds in as a separate gap (G28).

**Effort Estimate**
**L** (3 weeks).

**Dependencies**
G3 (event bus) — streaming readings must publish events; without an event bus, readings
pile up with no listeners.

---

### G12 — ML inference hooks (crack detection, anomaly detection, LLM mutation synthesis)

**Priority:** P3
**Category:** Ingestion
**Spec references:** §7.3, §7.5, Phase C parking-lot #2 / #4, Issue #12 sub-item 2

**Gap Description**
`IPostIngestHandler<T>` exists as the consumer-owned extension slot, but no default ML
adapters ship. Specifically the parking lot calls out: crack detection on drone images,
anomaly detection on sensor batches, LLM-based voice mutation synthesis.

**Impact Assessment**
- Affected users: any consumer wanting off-the-shelf inference; otherwise DIY.
- Severity: **low** — the contract slot exists; building actual models is out-of-scope for
  Sunfish-the-framework by design (models are domain-specific).
- Workarounds: consumers write their own `IPostIngestHandler<T>` implementations.

**Root Cause**
Intentionally deferred — spec positions ML as consumer-owned.

**Options**

#### Option A: Ship reference sample handlers in `apps/kitchen-sink/`
- Pros: Demonstrates integration patterns; no kernel/foundation changes.
- Cons: Sample code is maintenance burden.
- Risk: Low.
- Effort: **M** (1 week per sample).

#### Option B: Approve gap permanently
- Pros: Zero work; aligns with "models are consumer-owned" design.
- Cons: Leaves parking-lot item unaddressed.
- Risk: None.
- Effort: **S** (hours for doc).

**Recommended Path**
**Option B** (approved gap), with optional Option A samples if Bridge surfaces a concrete
use case (e.g., move-in photo auto-tagging).

**Effort Estimate**
**S**.

**Dependencies**
None.

---

### G13 — AI-assisted form authoring (Typeform-AI style)

**Priority:** P2
**Category:** Ingestion / UI
**Spec references:** §6 design references, Phase C parking-lot #1, Issue #12 sub-item 3

**Gap Description**
Spec §6 cites Typeform-AI as the baseline 2026 UX expectation; Phase C parking lot names
a future `Sunfish.Ingestion.Forms.Ai` package with `NoOp` default + pluggable OpenAI /
Anthropic / Azure OpenAI via `HttpClient`. Nothing ships.

**Impact Assessment**
- Affected users: PM operators authoring inspection templates, maintenance request forms,
  lease addenda.
- Severity: **moderate** — optional sugar, but the spec positions it as baseline UX.
- Workarounds: hand-authored JSON Schema.

**Root Cause**
Phase C intentionally scoped out; no successor phase has picked it up.

**Options**

#### Option A: Ship the package with pluggable LLM adapters (OpenAI + Anthropic)
- Pros: Matches spec; no vendor lock-in.
- Cons: LLM output quality for schema generation is uneven; needs post-edit UX.
- Risk: Medium — prompt engineering is an ongoing cost center.
- Effort: **L** (3 weeks with decent UX).

#### Option B: Defer until Bridge forms block has stabilized
- Pros: Fewer moving parts at once.
- Cons: Leaves parking-lot item open.
- Risk: Low.
- Effort: **S**.

**Recommended Path**
**Option B** — defer. Re-evaluate after G5 inspections vertical slice proves the form
authoring UX pain is real.

**Effort Estimate**
**L** if/when shipped.

**Dependencies**
G2 (schema registry — can't generate a schema without a registry to register it into).

---

### G14 — Domain block: `blocks-leases` (§6.1)

**Priority:** P1
**Category:** Blocks / PM vertical
**Spec references:** §6.1, §4.7

**Gap Description**
Spec §6.1 enumerates `Lease`, `Unit`, `Party`, `Document` entities; draft / signature /
execute / renewal / termination workflows; DocuSign integration; acceptance criteria
"under 3 minutes to create a lease." `packages/blocks-leases/` does not exist.

**Impact Assessment**
- Affected users: every PM-vertical customer.
- Severity: **major** — core §6 feature.
- Workarounds: none.

**Root Cause**
Phase 9 migrated PmDemo screens; did not build a lease block.

**Options**

#### Option A: Full block with DocuSign integration
- Pros: Covers §6.1 fully.
- Cons: DocuSign integration alone is L-scale.
- Risk: Medium.
- Effort: **L** (3 weeks).

#### Option B: Lease entity + block UI, defer DocuSign
- Pros: Unblocks inspection-vertical slice (G5 Option B).
- Cons: Incomplete.
- Risk: Low.
- Effort: **M** (1–2 weeks).

**Recommended Path**
**Option B** first; DocuSign integration as a follow-up.

**Effort Estimate**
**M**.

**Dependencies**
G1, G2, G3, G5 scope decision.

---

### G15 — Domain block: `blocks-rent-collection` (§6.2)

**Priority:** P1
**Category:** Blocks / PM vertical
**Spec references:** §6.2, §4.7

**Gap Description**
`RentSchedule`, `Invoice`, `Payment`, `BankAccount`, `LateFeePolicy` entities; Plaid ACH
and Stripe card integrations; idempotent late-fee workflows; tenant-ledger reconciliation
acceptance criterion. None shipped.

**Impact Assessment**
- Affected users: every PM-vertical customer.
- Severity: **major**.
- Workarounds: none.

**Root Cause**
Same as G14.

**Options**

#### Option A: Full block with Plaid + Stripe integrations
- Pros: Covers §6.2 fully.
- Cons: Two external integrations; decimal-arithmetic discipline requires care.
- Risk: Medium — financial correctness risk.
- Effort: **L** (4 weeks).

#### Option B: Entities + ledger block only; defer Plaid/Stripe
- Pros: Unblocks manual PM workflows.
- Cons: Incomplete.
- Risk: Low.
- Effort: **M** (2 weeks).

**Recommended Path**
**Option B** first; integrations as follow-ups.

**Effort Estimate**
**M**.

**Dependencies**
G1, G2, G14 (rent depends on lease).

---

### G16 — Domain block: `blocks-inspections` + `blocks-maintenance` (§6.3–§6.4)

**Priority:** P1
**Category:** Blocks / PM vertical
**Spec references:** §6.3, §6.4, §4.7

**Gap Description**
`InspectionTemplate`, `Inspection`, `Deficiency`, `InspectionReport`, `MaintenanceRequest`,
`WorkOrder`, `Vendor`, `Quote`, `RFQ` entities; parent/child case pattern (Pega-inspired);
offline mobile capture; photo/voice attachment; deficiency → work-order rollup. None
shipped.

**Impact Assessment**
- Affected users: every PM-vertical customer.
- Severity: **major** — the most visually compelling §6 feature and the natural pairing
  with federation (code-enforcement peer).
- Workarounds: none.

**Root Cause**
Same as G14.

**Options**

#### Option A: Both blocks together (inspection + maintenance share vendor/quote)
- Pros: Natural cohesion.
- Cons: Larger scope.
- Risk: Medium.
- Effort: **XL** (6–8 weeks).

#### Option B: Inspections first, maintenance follow-up
- Pros: Discrete ship; inspections aligns with G9 Pattern A federation demo.
- Cons: Work-order loop needs maintenance to close.
- Risk: Low.
- Effort: **L** (4 weeks inspections; 4 weeks maintenance).

**Recommended Path**
**Option B** — inspections first per G5 Option B, maintenance next. Anchors G9 demo.

**Effort Estimate**
**L** each.

**Dependencies**
G1, G2, G3 (event bus for deficiency → work-order spawning).

---

### G17 — Domain block: `blocks-accounting` (§6.6)

**Priority:** P2
**Category:** Blocks / PM vertical
**Spec references:** §6.6, §4.7

**Gap Description**
`GLAccount`, `JournalEntry`, `DepreciationSchedule` entities; automatic JE generation on
payment; QuickBooks / Xero export. §6.6 explicitly marks this as "light" — a system of
record for property-level GL, not a full ERP.

**Impact Assessment**
- Affected users: PM-vertical customers who do accounting in Sunfish vs externally.
- Severity: **moderate** — most PM operators use external accounting.
- Workarounds: export-only without inline accounting.

**Root Cause**
Same as G14.

**Options**

#### Option A: Full block + QuickBooks export
- Pros: Covers §6.6.
- Cons: Accounting correctness risk.
- Risk: Medium.
- Effort: **L** (4 weeks).

#### Option B: Export-only (JE shape + QuickBooks formatter, no ledger UI)
- Pros: Faster; leverages external accounting as the primary UI.
- Cons: Incomplete.
- Risk: Low.
- Effort: **M** (2 weeks).

**Recommended Path**
**Option B** — spec §6.6 explicitly says "for full accounting, the typical integration is
QBO / Xero," matching Option B.

**Effort Estimate**
**M**.

**Dependencies**
G1, G2, G15.

---

### G18 — Domain block: `blocks-tax-reporting` (§6.7)

**Priority:** P2
**Category:** Blocks / PM vertical
**Spec references:** §6.7, §4.7

**Gap Description**
`TaxReport` entity; annual Schedule E generation; 1099-NEC generation; state-level personal-
property templates; immutable signed reports. Depends on G17 for JE ledger source.

**Impact Assessment**
- Affected users: PM-vertical customers filing taxes (i.e., all of them).
- Severity: **moderate** — seasonal; external CPAs often handle.
- Workarounds: export + manual.

**Root Cause**
Same as G14.

**Options**

#### Option A: Full implementation covering Schedule E + 1099-NEC
- Pros: Closes §6.7.
- Cons: Jurisdictional tax templates are a maintenance tail.
- Risk: Medium.
- Effort: **L** (3 weeks).

#### Option B: Templates + signed-export only, defer per-jurisdiction state forms
- Pros: Covers the most common case.
- Cons: Partial.
- Risk: Low.
- Effort: **M** (2 weeks).

**Recommended Path**
**Option B**.

**Effort Estimate**
**M**.

**Dependencies**
G1, G2, G15, G17.

---

### G19 — Phase 3 workflow orchestration (`blocks-workflow`)

**Priority:** P1
**Category:** Blocks
**Spec references:** §4.6, §4.10 item 6

**Gap Description**
Spec §4.6 calls for `packages/blocks-workflow` with declarative state machines + BPMN
subset; event-driven rules; integration with kernel event bus; durable workflow execution
(Temporal / Dapr / Elsa / DurableTask candidates per §3.6). `blocks-tasks` and
`blocks-scheduling` exist as scaffolds but neither is a durable workflow engine.

**Impact Assessment**
- Affected users: PM vertical workflows (§6.4 maintenance state machine); Phase 5
  verticals generally.
- Severity: **major** — durable execution is the foundation for the §6.4 maintenance
  workflow acceptance criterion.
- Workarounds: in-memory workflows; no crash recovery.

**Root Cause**
Phase 3 of the spec roadmap is explicitly "future."

**Options**

#### Option A: Adopt Temporal via `Temporalio.Sdk`
- Pros: Most mature durable-execution .NET option; rich tooling.
- Cons: External service dependency (Temporal server); operational complexity.
- Risk: Medium.
- Effort: **XL** (6 weeks to integrate + author reference workflows).

#### Option B: Adopt Dapr Workflows (DurableTask underneath)
- Pros: Tighter .NET ecosystem; Dapr is Microsoft-aligned.
- Cons: Dapr itself is an infra layer with its own ops.
- Risk: Medium.
- Effort: **XL** (6 weeks).

#### Option C: Adopt Elsa Workflows 3
- Pros: .NET-native; designer UI included; lighter ops than Temporal.
- Cons: Smaller community; fewer battle-tested production deployments.
- Risk: Medium.
- Effort: **L** (4 weeks).

**Recommended Path**
**Option C (Elsa Workflows 3)** for Sunfish's open-source-first model. Revisit Temporal
when a customer's scale demands it. Spec §3.6 explicitly calls Elsa a candidate alongside
Temporal.

**Effort Estimate**
**L** — **XL**.

**Dependencies**
G3 (event bus for triggering workflows).

---

### G20 — Per-schema blob-boundary override

**Priority:** P2
**Category:** Ingestion / Kernel
**Spec references:** Phase C parking-lot #12, Appendix D open-question #2, §3.4

**Gap Description**
Phase C ships a global 64 KiB blob-boundary (`D-BLOB-BOUNDARY`). Spec §3.4 envisions a
per-schema `blobThreshold` descriptor on the schema registry. Blocked by G2 (no schema
registry exists to host the descriptor).

**Impact Assessment**
- Affected users: schemas whose typical payloads don't match the 64 KiB heuristic.
- Severity: **minor** — 64 KiB is a reasonable default.
- Workarounds: accept the default.

**Root Cause**
Depends on G2.

**Options**

#### Option A: Add `x-sunfish.blobThreshold` to schema descriptor; wire ingestion to read it
- Pros: Clean spec-aligned solution.
- Cons: Blocked by G2.
- Risk: Low.
- Effort: **S** (2–3 days after G2).

**Recommended Path**
**Option A** once G2 lands.

**Effort Estimate**
**S**.

**Dependencies**
G2.

---

### G21 — True atomic batch ingestion for spreadsheets

**Priority:** P2
**Category:** Ingestion / Kernel
**Spec references:** §7.2, Phase C parking-lot #13, T14

**Gap Description**
Phase C's `SpreadsheetIngestionPipeline` produces a single `IngestedEntity` with N per-row
events sharing a `CorrelationId`. True atomic batch (§7.2's "1,200 units atomically") needs
a kernel `entity.create-batch` API. Kernel doesn't have batch write today.

**Impact Assessment**
- Affected users: initial-data-load scenarios (bulk unit import, bulk lease backfill).
- Severity: **minor** — correlation-id-gated transactions at the consumer layer are an
  acceptable workaround for the first wave.
- Workarounds: correlation-id filtering.

**Root Cause**
Kernel missing batch-create primitive.

**Options**

#### Option A: Add `CreateBatchAsync` to `IEntityStore`
- Pros: Clean spec match.
- Cons: Transactional semantics + rollback across Postgres + federation need care.
- Risk: Medium.
- Effort: **M** (1–2 weeks).

**Recommended Path**
**Option A** when the kernel package lands (G1). Add to the contract on day 0.

**Effort Estimate**
**M**.

**Dependencies**
G1.

---

### G22 — Streaming blob-write path

**Priority:** P2
**Category:** Kernel / Ingestion / Federation
**Spec references:** Phase C parking-lot #8, Phase D parking-lot #8, Issue #12 sub-item 7

**Gap Description**
`IBlobStore.PutAsync(ReadOnlyMemory<byte>)` buffers in memory. Multi-GB blobs hit a
practical 2 GB limit (drone tiles, satellite scenes, BIM exports). Need
`PutStreamingAsync(Stream)`.

**Impact Assessment**
- Affected users: drone-imagery, satellite-imagery, BIM, archive-sensor workflows.
- Severity: **moderate** — hard blocker for the workloads it affects.
- Workarounds: pre-chunking at the ingestion layer (increases complexity).

**Root Cause**
Phase B-blobs shipped the minimal contract; streaming was explicitly deferred.

**Options**

#### Option A: Add `PutStreamingAsync(Stream)` to `IBlobStore` and all three backends
(`FileSystemBlobStore`, future S3 backend, `IpfsBlobStore`)
- Pros: Closes parking-lot item; unblocks large-blob workflows.
- Cons: Each backend has different streaming ergonomics; IPFS streaming via Kubo RPC is
  non-trivial.
- Risk: Medium.
- Effort: **M** (2 weeks for contract + FS backend + 1 other).

**Recommended Path**
**Option A**. Test case: the parking-lot-skipped
`Ingest_100MbStream_StreamsWithoutMemoryExplosion` test in `ingestion-imagery`.

**Effort Estimate**
**M**.

**Dependencies**
None.

---

### G23 — Virus-scanning middleware

**Priority:** P3
**Category:** Ingestion
**Spec references:** Phase C parking-lot #11, Issue #12 sub-item 9

**Gap Description**
`IIngestionMiddleware<TInput>` slot exists; `IngestOutcome.Quarantined` discriminator
exists; no default AV integration (ClamAV is the named candidate). Consumer-wired today.

**Impact Assessment**
- Affected users: any consumer accepting file uploads.
- Severity: **low** — gated behind middleware slot, so consumers can wire their own.
- Workarounds: consumer wires own AV.

**Root Cause**
Intentionally deferred — AV integration is deployment-specific.

**Options**

#### Option A: Ship ClamAV adapter as optional package
- Pros: One-line registration for the common case.
- Cons: ClamAV ops (signature updates, daemon management) is consumer-owned anyway.
- Risk: Low.
- Effort: **M** (1 week).

#### Option B: Approve gap; document the middleware slot
- Pros: Zero code.
- Cons: Parking-lot item stays open.
- Risk: None.
- Effort: **S**.

**Recommended Path**
**Option B** — approved gap unless a customer surfaces a concrete AV need.

**Effort Estimate**
**S** / **M**.

**Dependencies**
None.

---

### G24 — Satellite provider implementations (Planet / Maxar / Sentinel Hub / Airbus)

**Priority:** P3
**Category:** Ingestion
**Spec references:** Phase C parking-lot #10, Issue #12 sub-item 8

**Gap Description**
`ISatelliteImageryProvider` contract ships with a `NoOpSatelliteImageryProvider` default.
Commercial provider SDKs (Planet Labs, Maxar, Sentinel Hub, Airbus OneAtlas) not integrated;
they live in downstream packages per the parking lot.

**Impact Assessment**
- Affected users: satellite-dependent accelerators (Transit, Base, large-scale asset
  monitoring).
- Severity: **low** — ecosystem expansion, not a platform gap.
- Workarounds: consumer implements `ISatelliteImageryProvider`.

**Root Cause**
Provider SDKs are heavy, typically closed-source, and need commercial credentials.

**Options**

#### Option A: Ship Sentinel Hub (open-ish API) as the reference implementation
- Pros: Validates the contract; Sentinel Hub has an accessible tier.
- Cons: Still a commercial API.
- Risk: Low.
- Effort: **M** (1–2 weeks).

#### Option B: Approved gap; publish contract + NoOp only
- Pros: Zero work.
- Cons: First-time consumers have no reference.
- Risk: Low.
- Effort: **S**.

**Recommended Path**
**Option B** until a specific accelerator surfaces the need.

**Effort Estimate**
**S**.

**Dependencies**
None.

---

### G25 — BIM / CAD imports (`Sunfish.Ingestion.Bim`)

**Priority:** P3
**Category:** Ingestion
**Spec references:** §7.6, §9, Phase C parking-lot #7, Issue #12 sub-item 6

**Gap Description**
Spec §9 codifies BIM-as-enrichment, IFC 4.3.2 as canonical format, two-way sync. Xbim
Toolkit is the named parser. No `Sunfish.Ingestion.Bim` package exists.

**Impact Assessment**
- Affected users: construction, large-building-operations verticals.
- Severity: **low** — dedicated phase per the parking lot; not in any critical path
  today.
- Workarounds: consumer implements.

**Root Cause**
IFC 4.3.2 surface is large; Phase C intentionally scoped it out.

**Options**

#### Option A: Ship `Sunfish.Ingestion.Bim` with Xbim-backed IFC import
- Pros: Unlocks construction / large-building verticals.
- Cons: Xbim itself is a substantial dep.
- Risk: Medium.
- Effort: **L** (4 weeks).

#### Option B: Approved gap until an accelerator surfaces the need
- Pros: Zero work.
- Cons: Parking-lot stays open.
- Risk: Low.
- Effort: **S**.

**Recommended Path**
**Option B**.

**Effort Estimate**
**S**.

**Dependencies**
G22 (streaming blob-write — BIM files are multi-GB).

---

### G26 — AI-assisted voice mutation synthesis (§7.3 LLM orchestrator)

**Priority:** P3
**Category:** Ingestion
**Spec references:** §7.3 end-to-end example, Phase C parking-lot #2

**Gap Description**
`IPostIngestHandler<TranscriptionResult>` implementation that runs a transcript through an
LLM to propose kernel `(entity, event[])` mutations, gated by §7.7 confirmation. Needs the
kernel mutation-proposal API, which doesn't exist.

**Impact Assessment**
- Affected users: voice-first inspection flows.
- Severity: **low** — niche; consumer-owned handlers work today.
- Workarounds: consumer implements.

**Root Cause**
Depends on kernel mutation-proposal API (unspecified).

**Options**

#### Option A: Define mutation-proposal API, ship reference LLM adapter
- Pros: Closes the spec loop.
- Cons: Speculative API with no validated consumer.
- Risk: Medium — API design without consumer feedback is fragile.
- Effort: **L**.

#### Option B: Approved gap; document the `IPostIngestHandler` pattern
- Pros: Zero code; preserves optionality.
- Cons: Parking lot stays open.
- Risk: None.
- Effort: **S**.

**Recommended Path**
**Option B**.

**Effort Estimate**
**S**.

**Dependencies**
G1, G3.

---

### G27 — Multi-tenant quotas / rate limits middleware

**Priority:** P2
**Category:** Ingestion
**Spec references:** Phase C parking-lot #5, Issue #12 sub-item 4

**Gap Description**
`IIngestionMiddleware<TInput>` slot exists; no `QuotaMiddleware` ships. Needs
`IIngestionQuotaStore` contract and per-tenant configuration surface.

**Impact Assessment**
- Affected users: multi-tenant SaaS deployments (the commercial target per §13.3).
- Severity: **moderate** — SaaS ops blocker if a tenant floods ingestion.
- Workarounds: external rate-limit layer (nginx, API gateway).

**Root Cause**
Middleware slot shipped; quota-store wasn't on Phase C's critical path.

**Options**

#### Option A: Define `IIngestionQuotaStore` + ship Redis-backed default
- Pros: Production-grade; SaaS-ready.
- Cons: Redis dep; rate-limit algorithm needs care (token bucket vs sliding window).
- Risk: Medium.
- Effort: **M** (2 weeks).

#### Option B: Ship in-memory quota store; defer Redis
- Pros: Fast to ship.
- Cons: Doesn't cover multi-process deployments.
- Risk: Low.
- Effort: **M** (1 week).

**Recommended Path**
**Option B** first, Redis follow-up.

**Effort Estimate**
**M**.

**Dependencies**
None.

---

### G28 — MessagePack sensor-batch decoder

**Priority:** P3
**Category:** Ingestion
**Spec references:** Phase C parking-lot #6, Issue #12 sub-item 5

**Gap Description**
`NoOpMessagePackDecoder` ships returning `IngestOutcome.UnsupportedFormat`. Full
`MessagePack-CSharp` adapter deferred.

**Impact Assessment**
- Affected users: MessagePack-emitting sensors (niche in Sunfish's early target).
- Severity: **low**.
- Workarounds: JSON / NDJSON (already supported).

**Root Cause**
`MessagePack-CSharp` dep would be non-trivial for a niche workload.

**Options**

#### Option A: Ship `MessagePackSensorBatchDecoder` as optional package
- Pros: Closes parking-lot item.
- Cons: Adds a rarely-used dep.
- Risk: Low.
- Effort: **S** (2–3 days).

#### Option B: Approved gap
- Pros: Zero work.
- Cons: Parking lot stays open.
- Risk: None.
- Effort: **S**.

**Recommended Path**
**Option A** — it is small and closes a concrete parking-lot item.

**Effort Estimate**
**S**.

**Dependencies**
None.

---

### G29 — Integration tests against real external APIs

**Priority:** P2
**Category:** Ingestion / Test
**Spec references:** Phase C parking-lot #14, Issue #12 sub-item 12

**Gap Description**
All Phase C tests use mocked `HttpClient`. Opt-in `*.IntegrationTests` projects that hit
real Whisper / Azure Speech endpoints (with tenant-provided credentials) are parked.

**Impact Assessment**
- Affected users: anyone who needs confidence that the HTTP adapters actually work end-to-
  end against live APIs.
- Severity: **moderate** — stub-only tests are a known reliability risk.
- Workarounds: manual verification at deployment time.

**Root Cause**
Phase C explicitly scoped out real-API tests to keep CI key-free.

**Options**

#### Option A: Ship `*.IntegrationTests` opt-in projects; document credential setup
- Pros: Closes parking-lot item.
- Cons: Requires test accounts + credential management in CI.
- Risk: Low if opt-in (skipped by default).
- Effort: **M** (1 week per modality).

#### Option B: Approved gap with documented manual-verification runbook
- Pros: Zero code.
- Cons: No automated regression.
- Risk: Medium — silent API-break risk.
- Effort: **S**.

**Recommended Path**
**Option A** for voice modalities (Whisper / Azure Speech) first; other modalities
follow-up.

**Effort Estimate**
**M**.

**Dependencies**
None.

---

### G30 — DAB MCP DML tools live-Aspire verification

**Priority:** P2
**Category:** Infrastructure
**Spec references:** PR #19 status

**Gap Description**
The `feat/dab-mcp-dml-tools` PR (still open; branch exists but unmerged as of this
analysis) wires DAB 1.7.90 MCP SQL server with 6 DML tools (Create, Update, Delete, Read
with filters, Execute with stored procs, etc.). End-to-end verification against a live
Aspire run is deferred to local-dev.

**Impact Assessment**
- Affected users: any Bridge user who would use DAB via MCP.
- Severity: **moderate** — unmerged work; acceptance pending verification.
- Workarounds: manual DAB usage.

**Root Cause**
PR in flight; verification requires a live Aspire run that the author couldn't complete
in session.

**Options**

#### Option A: Complete live verification, merge PR
- Pros: Closes the open PR.
- Cons: Requires a live Aspire run.
- Risk: Low.
- Effort: **S** (1–2 days).

**Recommended Path**
**Option A** — should be finished before this gap-analysis PR merges or immediately after.

**Effort Estimate**
**S**.

**Dependencies**
Aspire host availability.

---

### G31 — Appendix C open question: schema-registry governance model

**Priority:** P2
**Category:** Spec / Governance
**Spec references:** Appendix C #1
**Resolution:** ADR-resolved — see `docs/adrs/0001-schema-registry-governance.md`

**Gap Description**
"Canonical schema registry governance model — is it a foundation, a company, a W3C working
group?" Unanswered.

**Impact Assessment**
- Affected users: ecosystem partners, schema-authoring organizations.
- Severity: **moderate** — affects commercial strategy (§13.3 OSS vs commercial split) and
  schema-authoring incentive structure.
- Workarounds: in-repo schemas only (single-party) until answered.

**Root Cause**
Open product / ecosystem question.

**Options**

#### Option A: Propose a governance RFC for review
- Pros: Gets stakeholder input on the answer.
- Cons: Time-boxed but unpredictable.
- Risk: Low.
- Effort: **S** (draft) + weeks (review).

#### Option B: Defer explicitly to pre-v1.0 milestone
- Pros: Zero work now.
- Cons: Stays open.
- Risk: Low.
- Effort: **S**.

**Recommended Path**
**Option A** once G2 ships — governance is more grounded once the technical primitive
exists.

**Effort Estimate**
**S** for RFC draft.

**Dependencies**
G2.

---

### G32 — Appendix C open question: kernel module format

**Priority:** P2
**Category:** Spec
**Spec references:** Appendix C #2
**Resolution:** ADR-resolved — see `docs/adrs/0002-kernel-module-format.md`

**Gap Description**
"Kernel module format — is it an Assembly + manifest (like ASP.NET), an OCI artifact, or
a plain NuGet package?" Unanswered.

**Impact Assessment**
- Affected users: plugin authors, deployment topology designers.
- Severity: **moderate** — affects how compliance packs and third-party schemas/blocks
  are distributed.
- Workarounds: NuGet is the implicit default today.

**Root Cause**
Open architecture question.

**Options**

#### Option A: Codify "NuGet default + optional OCI overlay" in spec
- Pros: Matches shipping reality.
- Cons: Forecloses options.
- Risk: Low.
- Effort: **S**.

#### Option B: RFC for stakeholder review
- Pros: Captures use cases.
- Cons: Slower.
- Risk: Low.
- Effort: **S** draft + weeks review.

**Recommended Path**
**Option A** — the repo's reality is NuGet; spec should acknowledge this.

**Effort Estimate**
**S**.

**Dependencies**
None.

---

### G33 — Appendix C open question: event-bus distribution semantics

**Priority:** P2
**Category:** Spec / Kernel
**Spec references:** Appendix C #3
**Resolution:** ADR-resolved — see `docs/adrs/0003-event-bus-distribution-semantics.md`

**Gap Description**
"Event bus distribution semantics — Sunfish default (at-least-once) or opt-in stronger
(exactly-once via Kafka transactions)?" Spec §3.6 declares at-least-once + idempotent
subscribers; the opt-in exactly-once path is unspecified.

**Impact Assessment**
- Affected users: deployments with strong ordering / exactly-once needs.
- Severity: **moderate** — affects backend selection (G3 Option A).
- Workarounds: idempotent design (already required).

**Root Cause**
Unanswered.

**Options**

#### Option A: Codify at-least-once default; ship Kafka-transactions path as future
- Pros: Matches spec §3.6.
- Cons: Kafka path stays unspecified.
- Risk: Low.
- Effort: **S**.

**Recommended Path**
**Option A** once G3 lands with the MassTransit backend.

**Effort Estimate**
**S**.

**Dependencies**
G3.

---

### G34 — Appendix C open question: post-quantum signature migration plan

**Priority:** P3
**Category:** Spec / Crypto
**Spec references:** Appendix C #4
**Resolution:** ADR-resolved — see `docs/adrs/0004-post-quantum-signature-migration.md`

**Gap Description**
"Post-quantum signature algorithm migration plan." Reserved extension point in spec §3.3
but no migration plan documented.

**Impact Assessment**
- Affected users: long-horizon deployments (government, military, 10+ year audit retention).
- Severity: **low-moderate** — PQ timeline is 2030+.
- Workarounds: Ed25519 today; monitor NIST PQ finalists.

**Root Cause**
PQ landscape not yet stable (NIST Round 4 still in flux per 2026 timeline).

**Options**

#### Option A: Document migration strategy (algorithm-agility in signatures + dual-sign window)
- Pros: Positions Sunfish for future PQ adoption without forcing choice.
- Cons: Speculative.
- Risk: Low.
- Effort: **S** (doc).

#### Option B: Approved deferral until PQ finalists stabilize
- Pros: Zero work.
- Cons: Open question.
- Risk: Low.
- Effort: **S**.

**Recommended Path**
**Option A** — even a half-page "algorithm-agility is a first-class design concern"
section closes the question adequately for now.

**Effort Estimate**
**S**.

**Dependencies**
None.

---

### G35 — Appendix C open question: Rust kernel crate + edge/embedded UX

**Priority:** P3
**Category:** Spec
**Spec references:** Appendix C #8, Appendix E.5, Appendix E.6 #1

**Gap Description**
Two intertwined open questions: (a) Rust kernel crate for mobile-native + browser-WASM
canonical-JSON / crypto / CID / policy parity (E.6 #1 explicitly lists this); (b) edge /
embedded-device UX strategy for rugged scanners, kiosks, small-screen IoT gateways.
Deferred until an accelerator surfaces concrete requirements.

**Impact Assessment**
- Affected users: Base accelerator (field conditions), Transit accelerator (trackside
  devices), eventual SDK consumers wanting true-native mobile primitives.
- Severity: **low** — genuinely future work.
- Workarounds: C# across the board today; adequate for Phase 2.5 mobile.

**Root Cause**
Correctly parked; no accelerator demands it yet.

**Options**

#### Option A: Prototype spike (~2 weeks) to size the cost
- Pros: Grounds future effort.
- Cons: Spikes without product validation are low ROI.
- Risk: Low.
- Effort: **M**.

#### Option B: Approved gap until an accelerator requires it
- Pros: Zero work.
- Cons: Question stays open.
- Risk: None.
- Effort: **S**.

**Recommended Path**
**Option B** — this is a textbook legitimate parking lot.

**Effort Estimate**
**S**.

**Dependencies**
None.

---

### G36 — Engineering hygiene bundle (JsonConverters, transitive dep guard, CODEOWNERS, GitButler)

**Priority:** P1 (bundled — individual items would be P2)
**Category:** Infrastructure / Governance
**Spec references:** T-X (for converters), code-reviewer-agent verdict on PR #8,
consolidation session observations

**Gap Description**
Four distinct but inexpensive hygiene gaps that naturally close in the same PR:

1. **JsonConverter polish (ex-G32 in the input list).** `Cid`, `EntityId`, `VersionId`,
   `SchemaId`, `ActorId`, `TenantId`, `Instant`, and related positional-record structs in
   `packages/foundation/` serialize as `{"value":"..."}` (or plain object form) rather than
   as flat strings. For eight to twelve identity types, the canonical fix is the same
   `[JsonConverter]` pattern commit `a2cda39` applied to `PrincipalId` and `Signature` in
   Phase D. See T-X in Appendix D for the precedent.

2. **`HasNoBlazorDependencyTests` transitive-closure hardening.** The existing test
   (`packages/foundation/tests/Crypto/HasNoBlazorDependencyTests.cs`) only walks direct
   `GetReferencedAssemblies()` — it doesn't follow transitive references. If a transitive
   package adds an AspNetCore.Components dep, the test won't catch it. Verified: one-level
   only.

3. **CODEOWNERS single-maintainer bottleneck.** `.github/CODEOWNERS` lists `@ctwoodwa` for
   every path. No redundancy, no domain-expert fan-out. Onboarding new maintainers is a
   governance gap.

4. **GitButler file-race during multi-subagent runs.** Consolidation session observed
   subagents hitting file-write race conditions that traced to GitButler interference.
   Either disable GitButler for multi-subagent work by convention, or document a known-
   safe workflow in `CONTRIBUTING.md` / `CLAUDE.md`.

**Impact Assessment**
- Affected users:
  (1) canonical-JSON signing correctness for any identity-wrapped payload (cross-phase
  correctness concern);
  (2) guards against regression into Blazor-in-foundation;
  (3) future collaborators;
  (4) anyone running multi-subagent sessions (Claude Code or agentic pipelines).
- Severity: bundled **major** — per-item these are moderate; together they are a
  pre-v0.5 hygiene batch.
- Workarounds: manual vigilance.

**Root Cause**
Items 1 and 2 are discovered-during-this-analysis regressions relative to Phase D
standards. 3 is a solo-maintainer pattern that needs to relax as contributors onboard.
4 is a local-tooling observation from the consolidation session.

**Options**

#### Option A: Bundled PR closing all four
- Pros: One review cycle; each fix is S-scale; forcing-function to not leave items
  forgotten.
- Cons: Mixed-concern PR (could be split).
- Risk: Low.
- Effort: **M** (4–6 days):
  - Converters: **S** (1–2 days).
  - Transitive dep test: **S** (1 day — walk `Assembly.GetReferencedAssemblies()`
    recursively with seen-set guard).
  - CODEOWNERS: **S** (hours — but real decision needs a second maintainer to exist).
  - GitButler note: **S** (1 hour — add to `CLAUDE.md`).

#### Option B: Four separate PRs
- Pros: Cleaner review diffs; independent revert-ability.
- Cons: Four PR cycles.
- Risk: Low.
- Effort: Same total; more overhead.

#### Option C: Ship (1) and (2) now, defer (3) and (4)
- Pros: Converters + transitive guard are the correctness items.
- Cons: Governance items stay open.
- Risk: Low.
- Effort: **S**.

**Recommended Path**
**Option A** bundled PR. Four small fixes that belong together — collectively they close
"polish" gaps that individually would never get prioritized.

**Effort Estimate**
**M** total (S each, bundled).

**Dependencies**
None.

---

### G37 — SunfishDataGrid feature coverage (component-level)

**Priority:** P1
**Category:** UI
**Spec references:** `docs/component-specs/grid/` (78 files). Component-level tracker at `packages/ui-adapters-blazor/Components/DataDisplay/DataGrid/GAP_ANALYSIS.md` (134 tasks across 4 phases, created 2026-03-30, last refreshed 2026-04-01).

**Gap Description**
`SunfishDataGrid` — the flagship data-display component — has an estimated **55-60% API coverage** versus its own component spec. A pre-existing component-level gap analysis tracks **134 tasks across 4 phases** (A: 49 pure-C#, B: 35 JS-interop, C: 29 advanced features, D: 21 future). Two pre-phase passes in March 2026 landed the core (public GridState, CRUD, filter menu, multi-sort, virtualization, footer, detail templates) but zero of the 134 planned-phase tasks have started.

Representative missing capabilities:
- **Phase A** — Grouping (with recursive GroupByMany + aggregates), AutoGenerateColumns, global SearchBox, NoDataTemplate, RowTemplate, column Editable/HeaderClass/Id/ShowColumnMenu/VisibleInColumnChooser, GridState enrichment (EditItem, ExpandedItems, ColumnStates, SearchFilter), Size enum, HighlightedItems, CSV export.
- **Phase B** — Full JS-interop layer (`marilo-datagrid.js` ES module): keyboard navigation, column resize ⚡, column reorder ⚡, row drag-and-drop, frozen/locked columns. Community consensus flags column resize + reorder as "table stakes" for any enterprise grid.
- **Phase C** — Excel export (ClosedXML), PDF export (QuestPDF), column menu, column chooser, CheckBoxList filter mode, multi-column headers, cell selection, editing validation (EditForm + DataAnnotations).
- **Phase D** — AI features (AI Column / Highlight / Search / Smart Box), popup-form/buttons templates, pager template, toolbar built-in tools, virtual/checkbox columns, AdaptiveMode (card layout on narrow viewports).

**Impact Assessment**
- Affected users: every Bridge accelerator screen that uses grids (timeline, kanban, board, task list, inspection lists, budget-line tables) and every future PM-vertical view.
- Severity: P1 — core operations work (filter, sort, page, select, edit, detail expand) but enterprise-expected capabilities (column resize, keyboard nav, Excel export, grouping) are absent. Users coming from Radzen / Syncfusion / AG Grid will experience the gap immediately.
- Workarounds: consumers can wrap external grid libraries (MudBlazor DataGrid, Radzen DataGrid, Blazorise DataGrid) per G36's OSS gap-filler catalog — but that undercuts Sunfish's "one adapter, consistent surface" value proposition.
- Competitive risk: data grid is the single most-used component in business apps; a weak grid makes Sunfish look incomplete regardless of how strong the kernel is.

**Root Cause**
Component-spec size (78 spec files) vs available time. The DataGrid spec was carried forward from Marilo and is comprehensive; the migration shipped a functional core but deferred the long tail. Pre-phase passes prioritized correctness over feature surface.

**Options**

#### Option A: Execute the existing 134-task tracker in phase order (A → B → C → D)
- **Pros:** Plan already scoped; tasks are discrete; RESEARCH_LOG.md captured reference patterns from Radzen / QuickGrid / MudBlazor / Tabulator / AG Grid.
- **Cons:** 134 tasks is multi-quarter work for one component. Phase A alone (49 tasks) is ~4-6 weeks.
- **Risk:** Low — spec is stable; tasks are well-scoped.
- **Effort:** XL — Phase A (M/L), Phase B (L), Phase C (L), Phase D (M). Total 4-6 months at steady state.

#### Option B: Ship Phase A + Phase B only (table-stakes parity); defer C + D
- **Pros:** Covers the 80% that enterprise users expect. Column resize (B2) + column reorder (B3) + keyboard nav (B1) alone close most of the "feels incomplete" perception gap. Frees 50 tasks (C+D) into a later tracker.
- **Cons:** No Excel/PDF export (C1/C2) or AI features (D1). Consumers that need export route through OSS wrappers.
- **Risk:** Low.
- **Effort:** L — Phase A + B = 84 tasks, ~8-12 weeks.

#### Option C: Adopt MudBlazor.DataGrid / Radzen.Blazor.DataGrid as a provider; deprecate the Sunfish impl
- **Pros:** Instant feature-parity with a mature grid. Reduces Sunfish maintenance burden. Consistent with G36's OSS-as-provider pattern.
- **Cons:** Fragments the SunfishDataGrid surface — existing consumers would need to migrate. Loses the custom GridState shape. Contradicts the "one Sunfish surface" principle.
- **Risk:** Medium — existing consumers in `apps/kitchen-sink` + `accelerators/bridge` would need rework.
- **Effort:** M initial integration + XL migration tail.

#### Option D: Prioritize by consumer demand — instrument current usage, execute the 134-task list in demand order
- **Pros:** Aligns effort with real pain; avoids speculative work on features nobody uses.
- **Cons:** Requires telemetry / customer interviews that don't exist yet; delays table-stakes features (keyboard, resize, grouping) whose demand is obvious.
- **Risk:** Medium — "what's really used" bias toward current Bridge usage which is PmDemo-derived.
- **Effort:** L — same surface area as A/B, but reordered.

**Recommended Path**

**Option B** — ship Phase A + Phase B only (84 tasks). This closes the "feels unfinished" gap at the most visible level (column resize, reorder, keyboard nav, grouping, CSV export, SearchBox) without over-committing to the XL full-spec surface. Phases C + D remain in the tracker as follow-up work, picked up opportunistically once the Sunfish PM vertical surfaces specific demand for Excel/PDF export or AI features.

Execute in the existing tracker's phase order (A before B because A is pure-C# and derisks the rendering / state model; B's JS-interop layer depends on the stabilized column model). Phase A and Phase B can be done by a dedicated DataGrid workstream in parallel with platform-tier work (G1-G5).

**Effort Estimate**

**L** — ~8-12 weeks for Phase A + B. Tracker is at `packages/ui-adapters-blazor/Components/DataDisplay/DataGrid/GAP_ANALYSIS.md`; update its progress table as tasks complete, one session per 3-5 tasks.

**Dependencies**

None at the platform level. The tracker itself notes internal dependencies (B0 JS-infra before B1-B5; A1 grouping before A8 CSV-export of grouped data).

**Note on integration with this platform gap analysis**

This entry exists so the DataGrid work is *visible* at the platform tier. The 134-task tracker remains the authoritative plan-of-record at the component tier; don't duplicate sub-task state here. Reference the file whenever this gap is scheduled or its priority is revisited.

---

## Prioritized Execution Order

The order below is a directed plan, not a sorted list. Gaps on the same tier can proceed
in parallel; gaps on later tiers depend (directly or by shared resource) on earlier tiers.

### Tier 1 — Foundation hygiene (parallel, ~1 week)

These are small, independent, and unblock or derisk everything downstream.

- **G36 (bundled hygiene)** — JsonConverters + transitive dep guard + CODEOWNERS + GitButler.
- **G32 (kernel module format)** — codify NuGet in spec.
- **G34 (post-quantum migration plan)** — half-page doc.
- **G30 (DAB MCP live-Aspire verification)** — finish PR #19.

### Tier 2 — Kernel bootstrap (parallel, ~3 weeks)

Core architecture work. Must land before PM vertical (Tier 4) can proceed.

- **G1 (kernel namespace façade)** — creates the home where Tier 2 contracts live.
- **G2 (schema registry — validation half)** — ISchemaRegistry + InMemorySchemaRegistry
  + PostgresSchemaRegistry, JsonSchema.Net validation, content-addressed via IBlobStore.
- **G3 (event bus — InMemory)** — InMemoryEventBus + contract conformance tests.
- **G4 (BusinessRuleEngine wiring)** — depends on G3 landing.
- **G33 (event-bus distribution semantics doc)** — codify at-least-once default.

### Tier 3 — Host family (parallel with Tier 4, ~6–8 weeks)

Unblocks multi-platform delivery; independent of kernel work.

- **G7 (Iced-lessons contracts, HIGH-ROI subset)** — ISunfishRenderer, IClientTask,
  IClientSubscription.
- **G6 (hosts — desktop first)** — hosts-web rename + hosts-desktop-maui +
  hosts-desktop-photino.
- **G6 (hosts — mobile follow-up)** — hosts-mobile-maui + app-store packaging.

### Tier 4 — PM vertical (~3–6 months, sequenced)

Anchors the commercial wedge. Should begin in parallel with Tier 3.

- **G5 (Bridge reshape to inspections-vertical slice)** — frames the ship.
- **G14 (blocks-leases)** — entities + UI, defer DocuSign.
- **G15 (blocks-rent-collection)** — entities + ledger UI, defer Plaid/Stripe.
- **G16 (blocks-inspections + blocks-maintenance)** — inspections first (anchors G9).
- **G17 (blocks-accounting — export-only)**.
- **G18 (blocks-tax-reporting — templates + signed-export)**.
- **G19 (blocks-workflow via Elsa 3)** — crosses Tier 3/4 boundary; enables G16's state
  machine.

### Tier 5 — Federation first-wave close-out (parallel with Tier 4, ~2 months)

Infrastructure-gated on Docker/Podman availability.

- **G8 (IPFS-Cluster Raft pinning + attestation)**.
- **G9 (Pattern A worked example)** — demo of G8 + G16 inspections vertical composed.
- **G10 (Pattern B worked example)**.

### Tier 6 — Ingestion parking-lot close-out (parallel, ~2 months)

Targeted closure of specific parking-lot items.

- **G22 (streaming blob-write)**.
- **G27 (quota middleware)**.
- **G21 (atomic batch ingestion)** — folds into G1+G2 wave.
- **G20 (per-schema blob-boundary override)** — after G2.
- **G28 (MessagePack decoder)** — opportunistic.
- **G29 (real-API integration tests for voice)**.
- **G11 (real-time streaming sensor ingestion)** — after G3.

### Tier 7 — Governance and forward-looking (RFC + spec edits, ~1 month)

- **G31 (schema-registry governance RFC)** — after G2 ships.

### Tier 8 — Approved gaps (docs-only)

No code. Document the deferral in release notes / spec.

- **G12 (ML inference hooks)** — consumer-owned.
- **G23 (virus scanning)** — middleware slot only.
- **G24 (satellite providers)** — contract + NoOp only.
- **G25 (BIM imports)** — dedicated future phase.
- **G26 (voice mutation synthesis)** — consumer-owned.
- **G35 (Rust kernel crate + edge UX)** — parked until an accelerator surfaces demand.

---

## What is explicitly NOT a gap

The following are superficially-spec-vs-repo divergences that are **not** gaps; they are
deliberate v0.4 product decisions:

- **No React adapter.** Dropped in v0.4 (Appendix E.1). Blazor MAUI Hybrid covers every
  platform. Not a gap.
- **No React parity tests.** Dropped with the adapter.
- **No pure-MAUI-XAML component ports of the 228 Blazor components.** `hosts-native-maui`
  is explicitly optional (§4.5 non-goals) and reserved for performance-critical surfaces
  only.
- **No GPU-native Blazor rendering.** Explicitly deferred to `hosts-native-maui` surfaces
  (§4.5 non-goals).
- **No Rust UI adapter parallel to Blazor.** v0.4 Appendix E.5 is explicit: a Rust kernel
  crate is a separate research track; a Rust UI adapter is not warranted.
- **No single-key-rotation KMS/HSM implementation in the repo.** `IRootKeyStore` and
  `IOperationSigner` are consumer-owned production integrations (§T11).
- **No durable nonce-replay-protection store (Phase D parking-lot #11).** Phase B's
  in-memory tracking is sufficient for current single-process and test scenarios.
- **No libp2p transport (Phase D parking-lot #6).** HTTP + TLS is deliberate Phase D first
  pass.
- **No BeeKEM group key agreement (Phase D parking-lot #7).** Confidentiality is a
  separate future phase per Appendix D.
- **Phase D RIBLT conformance tests vs reference JS Keyhive (Phase D parking-lot #10).**
  Sunfish's RIBLT passes its own round-trip tests and delivers convergence; cross-
  implementation conformance is approved as a later concern.
- **Cross-jurisdiction policy overlay (Phase D parking-lot #9).** Semantics not nailed
  down in spec; Pattern A (G9) implementation will surface the concrete requirements.
- **Full Automerge library integration.** Research note is explicit: adopt the model, not
  the library. Shipped approach (`ChangeRecord` opaque-diff envelope) preserves optionality.
- **T1–T18 and T-X from Appendix D.** Shipped shapes are now spec-canonical; not gaps.
- **`compat-telerik` deltas vs Telerik.** Best-effort by charter.

---

## Open questions for stakeholder review

Flagged as needing product / architecture direction before implementation:

1. **G1 Option A vs B.** Physical re-home (destructive, spec-clean) vs virtual façade
   (non-destructive, dual names). Recommend B but the call is reasonable either way; affects
   downstream consumer migration cost.
2. **G3 transport priority.** Ship MassTransit or Wolverine first after InMemory? The
   Bridge accelerator uses Wolverine; the broader .NET ecosystem uses MassTransit more.
   Spec §3.6 lists both as candidates; the tiebreaker is which backend Phase 4 PM vertical
   needs first.
3. **G5 Bridge reshape.** Inspections-vertical slice (recommended) or full §6 MVP at once?
   Inspections slice is two months vs 4–6 months for full; trade-off is demonstrability
   vs completeness.
4. **G6 platform priority.** Desktop-first (power users, Windows/macOS) or mobile-first
   (inspectors in the field)? Recommend desktop-first because MAUI Hybrid rendering needs
   validation before app-store review loops; mobile follow-up is then lower-risk.
5. **G19 workflow engine.** Elsa 3 (recommended, lighter ops, .NET-native) vs Temporal
   (battle-tested, cross-language) vs Dapr Workflows (Microsoft-aligned). Selection
   affects Phase 3 for life.
6. **G31 schema-registry governance.** Foundation vs company vs W3C WG is a commercial-
   strategy question as much as a technical one; §13.3 OSS/commercial split is implicated.
7. **Phase 4 PM-vertical integration targets.** DocuSign is default for lease signing;
   Plaid + Stripe for rent; QuickBooks + Xero for accounting. Confirm these as v1.0
   targets vs leaving them consumer-owned.
8. **Mobile app-store accounts.** Apple Developer + Google Play + Microsoft Partner Center
   accounts needed for G6 mobile Phase 2.5 delivery. Commercial decision.

---

*End of gap analysis. Consumed by Stage 02 (Architecture) — each P0/P1 gap expects a
remediation design artifact. P2/P3 gaps may be deferred to a second-wave Stage 02 or
accepted as parking lot with sign-off.*
