# Paper Alignment Audit — Packages + Anchor Accelerator vs. Local-Node Architecture Paper (v10.0)

**Date:** 2026-04-22
**Auditor:** Claude Code (Opus 4.7)
**Scope:** All `packages/*` (53 packages) + `accelerators/anchor` (deliberately deferred MAUI Blazor Hybrid shell) evaluated against [`_shared/product/local-node-architecture-paper.md`](../../../_shared/product/local-node-architecture-paper.md) — *Inverting the SaaS Paradigm*, Version 10.0, April 2026.

Also touched where relevant: `accelerators/bridge` (SaaS-shell accelerator) because its current positioning creates a first-order architectural tension with the paper.

Legend: 🟢 aligned • 🟡 partial / scaffolded • 🔴 missing • ⚠ structural conflict with paper • ⚪ out of scope for current phase

---

## 1. Executive Summary

The paper specifies a **microkernel-monolith local-first node** with mandatory Phase-1 kernel primitives (sync daemon, gossip, Flease, event log, CRDT engine, role attestation, plugin registry). The repo's adapter/provider/compat layering is substantially aligned with the paper's §5.2 UI-kernel four-tier structure. **The paper's Phase-1 data/sync kernel is essentially absent — what ships as `packages/kernel` today is a type-forwarding façade over Foundation primitives, not the runtime kernel the paper mandates.** The closest in-tree approximation to Phase 1 is `packages/federation-*` (4 packages) + `foundation-localfirst`, which together sketch the sync primitives but under a different architectural model (federation, not gossip-first leaderless replication).

**Top-line findings:**

1. **UI kernel four-tier layering: substantially aligned.** Foundation / adapter / blocks / compat map onto paper §5.2 one-to-one. The new compat-vendor + compat-icon packages (this session) are exactly what §5.2 calls "Compatibility and Adapter Layer." Gap: no non-Blazor adapter yet (paper assumes React parity).

2. **Data/sync kernel: missing.** No sync daemon, no CRDT engine abstraction, no gossip, no Flease, no role-attestation key distribution, no circuit-breaker quarantine queue, no ILocalNodePlugin registry. `packages/kernel` is a façade; `packages/kernel-event-bus` and `packages/kernel-schema-registry` are InMemory stubs.

3. **Anchor aligned in intent, empty in code.** The accelerator's README explicitly positions it as the local-first counterpart to Bridge; its scope is scaffolded-only and deferred behind the ADR 0017 Web Components migration. No local-first node is actually running.

4. **Bridge creates first-order architectural tension.** Bridge is a multi-tenant SaaS shell with hosted Postgres — structurally the inverse of paper §3 ("Conventional SaaS: Cloud database is primary → Local-Node Architecture: Local node is primary"). Paper §6.1 and §17.2 offer a reconciliation path: Bridge could reposition as the **managed relay** (a peer among equals), but that reframing is not reflected in Bridge's README or ADR 0006.

5. **Implementation roadmap gap.** Paper §18 Phase 1 is ~12 kernel-core deliverables. Repo's equivalent phase (Platform Phase A/B/C/D noted in `PLATFORM_ALIGNMENT.md`) is structured very differently — Bridge-centric, post-migration, no sync-daemon-first sequencing. A re-grounded roadmap that traces Phase 1 from the paper into ICM pipeline tickets would close this gap.

---

## 2. Paper § → Repo Coverage Matrix

### 2.1 Kernel Responsibilities (Paper §5.1)

| Paper responsibility | Repo location | Status | Notes |
|---|---|---|---|
| Node lifecycle + process orchestration | — | 🔴 | No local-node host process; no OS-service-manager registration. Anchor is a MAUI app, not a persistent background service. |
| Sync daemon protocol, gossip anti-entropy, distributed lease coordination | `federation-common`, `federation-entity-sync`, `federation-capability-sync`, `federation-blob-replication` | 🟡 | Federation packages sketch the primitive (`ISyncTransport`, `InMemorySyncTransport`, `IEntitySyncer`, `ICapabilitySyncer`, RIBLT protocol, Kubo IPFS helpers). **But the model is federation (peer-to-peer with published envelopes), not the paper's gossip-daemon protocol (HELLO / CAPABILITY_NEG / DELTA_STREAM / GOSSIP_PING). No mDNS discovery, no 30-second gossip tick, no Flease quorum lease.** |
| CRDT engine abstraction + event log + snapshots + compaction | `kernel-event-bus`, `foundation-localfirst/OfflineStore.cs`, `foundation-localfirst/SyncEngine.cs` | 🟡 | `IEventBus` + `InMemoryEventBus` exists but no persistent event log, no CRDT abstraction (Yjs/Loro/Automerge). `OfflineStore` + `SyncEngine` are closer to a write-through cache than a CRDT-replicated store. |
| Schema migration infrastructure (expand/contract, lenses, epochs) | `kernel-schema-registry`, `ISchemaRegistry`, `InMemorySchemaRegistry` | 🟡 | Registry interface exists (gap G2 from prior audit). No expand/contract tooling, no bidirectional lenses (paper §7.3 Cambria-style), no epoch coordination (paper §7.4). |
| Security primitives (encryption, key management, role attestation) | — | 🔴 | No SQLCipher-equivalent encrypted-at-rest layer surfaced, no Argon2id key derivation, no per-role symmetric key distribution, no role-attestation cryptography. Paper §11.3 specifies a full key-bundle flow — not present. |
| Partial/selective sync engine (bucket definitions, stream subscriptions) | — | 🔴 | No declarative sync-bucket YAML engine (paper §10.2). Federation packages handle envelope-level sync but with no bucket eligibility semantics. |
| Plugin discovery, loading, versioning, lifecycle | — | 🔴 | No `ILocalNodePlugin` contract, no plugin registry. `packages/blocks-*` are consumed via direct ProjectReference, not discovered via a registry. |

### 2.2 UI Kernel Four-Tier (Paper §5.2) — **Substantially Aligned** 🟢

| Paper tier | Repo location | Status |
|---|---|---|
| Foundation — tokens, utilities, primitives | `packages/ui-core` + `packages/ui-adapters-blazor/Providers/*/Styles/foundation/` | 🟢 |
| Framework-Agnostic Component Layer | `packages/ui-core` contracts (`ISunfishCssProvider`, etc.) | 🟡 — contracts are framework-agnostic; **concrete components ship only as Blazor** (`ui-adapters-blazor`), so the "framework-agnostic" promise is half-kept. |
| Blocks and Modules | `packages/blocks-*` (15 packages) | 🟢 — matches paper §5.2 block examples (task-board, resource-allocation-scheduler, ledger-posting-grid, etc.) |
| Compatibility and Adapter Layer | `packages/compat-*` (13 packages, shipped in this session) | 🟢 — compat-telerik, compat-syncfusion, compat-infragistics + 9 icon-compat packages + compat-shared. **Precisely what paper §5.2 calls for.** |

**Gap:** Paper §5.2 and ADR 0014 both require **React adapter parity**. No `packages/ui-adapters-react` exists. Every compat package is Blazor-only; their React equivalents are all work not yet started.

### 2.3 Extension Point Contracts (Paper §5.3) — **Missing** 🔴

| Contract | Status |
|---|---|
| `ILocalNodePlugin` | 🔴 |
| `IStreamDefinition` | 🔴 |
| `IProjectionBuilder` | 🔴 |
| `ISchemaVersion` | 🔴 |
| `IUiBlockManifest` | 🔴 |

None of these interfaces exist. The paper is explicit: *"Kernel extension points are defined as stable, versioned interfaces. Plugins implement these interfaces; the kernel discovers and loads plugins at startup through a convention-based registry."* The repo's blocks-* packages are not discovered at startup — they're static ProjectReferences in consumer csprojs.

### 2.4 Sync Architecture (Paper §6) — **Partial / Different Model** 🟡⚠

| Paper concept | Repo | Status |
|---|---|---|
| Gossip anti-entropy (30s tick, random peer pairs) | — | 🔴 |
| mDNS peer discovery | — | 🔴 |
| Mesh VPN peer discovery (WireGuard) | — | 🔴 |
| Managed relay (optional) | `accelerators/bridge` (implicitly; not framed this way) | ⚠ — Bridge is currently framed as "SaaS shell" not "managed relay." See §5 below. |
| Sync daemon separate process + Unix socket | — | 🔴 |
| HELLO / CAPABILITY_NEG / DELTA_STREAM / GOSSIP_PING handshake | — | 🔴 |
| Stream-level subscription filtering (data minimization) | Partial in `federation-common/EnvelopeSigning.cs` | 🟡 |
| Flease distributed lease (CP-class writes) | — | 🔴 |

The repo's **federation** subsystem (4 packages) is the closest architectural analogue but implements a **different model**: signed envelope exchange over HTTP / Kubo IPFS, with a `ISyncTransport` abstraction. This is more like *eventually-consistent federated state exchange* than *leaderless gossip with quorum CP writes*. Reconciling the two would be a kernel-level architecture decision, not a swap-in.

### 2.5 Schema Migration (Paper §7) — **Skeleton Only** 🟡

| Paper primitive | Repo | Status |
|---|---|---|
| Expand/contract migration tooling | — | 🔴 |
| Event versioning + upcasters | — (paper §7.2 — no upcaster abstraction in tree) | 🔴 |
| Bidirectional schema lenses (Cambria-style) | — | 🔴 |
| Schema epoch coordination via lease quorum | — | 🔴 |
| Stream compaction (copy-transform + upcaster retirement) | — | 🔴 |

`kernel-schema-registry` is the intended home for these but ships only `ISchemaRegistry` / `InMemorySchemaRegistry`. This is Phase 3 per paper §18 — plausibly deferred, but worth an explicit task sequence.

### 2.6 Snapshots and Rehydration (Paper §8) — **Missing** 🔴

No snapshot format, no rehydration path, no epoch/schema tagging. This is a performance optimization layer; its absence is tolerable pre-scale but the code that will eventually need it is event-sourced, which doesn't exist yet.

### 2.7 CRDT Growth and GC (Paper §9) — **N/A — No CRDT Yet** 🔴

Nothing to garbage-collect because no CRDT engine is integrated. `docs/specifications/research-notes/automerge-evaluation.md` (referenced in Bridge's PLATFORM_ALIGNMENT.md) is a candidate-evaluation doc — the work itself is unstarted.

### 2.8 Partial / Selective Sync (Paper §10) — **Missing** 🔴

| Paper primitive | Repo | Status |
|---|---|---|
| Declarative sync buckets (YAML) | — | 🔴 |
| Bucket eligibility via role attestation | — | 🔴 |
| Lazy fetch + stub representation | — | 🔴 |
| Storage budgets + LRU eviction | — | 🔴 |

### 2.9 Security Architecture (Paper §11) — **Minimal** 🔴

| Defense Layer | Status |
|---|---|
| Layer 1 — Encryption at rest (SQLCipher + Argon2id + OS keystore) | 🔴 |
| Layer 2 — Field-level encryption with per-role symmetric keys | 🔴 |
| Layer 3 — Stream-level data minimization | 🟡 (envelope filtering in `federation-common`) |
| Layer 4 — Circuit breaker + quarantine queue | 🔴 — `foundation-localfirst/OfflineQueue.cs` exists but is not an append-only event-sourced quarantine; it's a write-buffer. |

Per paper §11.3 the whole "role attestations vs. key distribution" distinction isn't present — the concept that keys flow via signed administrative events in the log is unimplemented.

### 2.10 Ledger Mechanics (Paper §12) — **Partial** 🟡

| Paper primitive | Repo | Status |
|---|---|---|
| Double-entry ledger as first-class subsystem | `packages/blocks-accounting` | 🟡 — accounting is a **block**, not a kernel subsystem as paper §12.1 implies (*"financial and CP-class value records are modeled as a double-entry ledger"* — kernel-level). |
| Posting engine + idempotency | — | 🔴 |
| CQRS read models | — | 🔴 |
| Closing the books + period snapshots | — | 🔴 |

The paper is emphatic about event-sourced postings with idempotency keys and sum-to-zero invariants enforced at the kernel layer. The current `blocks-accounting` is likely CRUD-over-EF, not event-sourced postings.

### 2.11 Testing Strategy (Paper §15) — **Partial** 🟡

| Level | Repo equivalent | Status |
|---|---|---|
| Property-based tests | Limited (no evidence of FsCheck/Hedgehog usage) | 🔴 |
| Integration tests (real deps) | Existing `tests/` folders per package | 🟢 |
| Fault injection in CI | — | 🔴 |
| Deterministic simulation | — | 🔴 |
| Chaos testing in staging | — | 🔴 |

The five-level pyramid is heavily weighted toward sync-daemon / CRDT property tests that can't exist until those subsystems exist.

### 2.12 Enterprise Deployment (Paper §16) — **Missing** 🔴

| Primitive | Status |
|---|---|
| MDM-compatible installer (Intune/Jamf) | 🔴 |
| SBOM in CI | 🔴 |
| Air-gap deployment | 🔴 |
| BYOD path separation | 🔴 |

Phase 4 per paper §18. Deferred appropriately.

---

## 3. Accelerator Alignment

### 3.1 `accelerators/anchor` — **Aligned in intent, empty in code** 🟡

- ✅ **Intent correct.** README: *"Anchor exists to prove that the same component surface, bundle manifests, and Foundation primitives compose cleanly into both shapes — if something only works in the SaaS case, it isn't really local-first."* Directly channels paper §1's inversion thesis.
- ✅ **Platform choice reasonable.** MAUI Blazor Hybrid gives WebView hosting on all four desktop/mobile targets from one codebase. Paper §4 suggests Tauri; MAUI is a pragmatic .NET-stack-consistency choice with documented tradeoffs in the README.
- 🔴 **Scope deferred.** All 8 deliverables in the README checklist (LocalFirst store wiring, bundle selection UI, report catalog, audit log surface, sync toggle, auth model, platform packaging, auto-update) are unchecked. The current project is a MAUI skeleton with one placeholder page.
- ⚠ **Paper-specific gaps:**
  - No OS-service-manager registration for a persistent background container stack (paper §4).
  - No QR-code onboarding (paper §6.2 + §13.4).
  - No status-bar freshness / node-health / link-status indicators (paper §13.2).
  - No conflict list / optimistic-write button / freshness badge components (paper §5.2 "Framework-Agnostic Component Layer").

**The gap between Anchor-as-described and Anchor-as-implemented is the largest and most concentrated gap between the paper and the repo.** Anchor is the accelerator the paper was written for. Closing this gap would validate the paper experimentally.

### 3.2 `accelerators/bridge` — **Structural Conflict, Reconcilable** ⚠

Bridge is a .NET Aspire-orchestrated multi-tenant SaaS shell: Blazor Server + Postgres + Redis + RabbitMQ + Data API Builder + SignalR. The paper's §3 inversion statement (*"instead of the cloud being primary and the local device being a read-through cache, the local node is primary and the cloud is a sync peer"*) is structurally **the opposite** of what Bridge is.

Bridge's own `PLATFORM_ALIGNMENT.md` acknowledges this tension extensively:

- Entity storage — spec wants temporal tables; Bridge has audit columns.
- Version store — 🔴 Not adopted; candidate = Automerge-style change log.
- Schema registry — 🔴 Compile-time types only.
- Cryptographic ownership proofs — 🔴 `DemoTenantContext` uses tenant IDs as strings.
- Federation — 🔴 Single-server; candidate = Automerge sync protocol.

**Reconciliation path.** The paper itself offers a clean resolution via §6.1 and §17.2: Bridge can reposition as the **managed relay**. The relay provides no capabilities that self-operators can't replicate; it provides professional reliability and a human support contact. Under that framing:

- Bridge as multi-tenant SaaS = the deployment model for customers who don't run their own local nodes at all (the paper's §14 "non-technical trust gap" — these are the organizations for whom "who do I call" matters more than data residency).
- Bridge as managed relay = the paper-aligned deployment — offers peer-coordination, NAT traversal, and first-line support over a fleet of Anchor installations.

These are two different product postures. Today's Bridge is Posture 1. Paper's Bridge is Posture 2. Either (a) adopt Posture 2 and deprecate Posture 1's multi-tenancy assumptions, (b) support both postures with explicit opt-in, or (c) acknowledge Bridge-the-SaaS as a pre-paper-era decision that will be revised once Anchor matures.

**Recommendation:** Draft ADR 0006-update repositioning Bridge's future role. Do not modify Bridge's current Aspire orchestration — it's working demo-ware. Clarify that the multi-tenant-SaaS framing was pre-paper; the paper-aligned future of Bridge is the managed-relay role.

---

## 4. Critical Gaps — Ordered by Paper-Phase Priority

Every item here is in **Paper §18 Phase 1** ("Foundation — Kernel Core"). These unblock everything downstream.

| # | Missing primitive | Paper § | Repo home (implied) | Effort | Unblocks |
|---|---|---|---|---|---|
| 1 | **Sync daemon protocol sub-document** — the HELLO/CAPABILITY_NEG/DELTA_STREAM/GOSSIP_PING message-format spec | §18 *"delivered first — it unblocks all Phase 1 work"* | `docs/specifications/sync-daemon-protocol.md` (new) | 2-3 days | Everything in items 2-8 below |
| 2 | CRDT engine abstraction + library evaluation | §5.1, §9 | `packages/kernel-crdt/` (new) | ~2 weeks | Event log integration, real sync |
| 3 | Gossip anti-entropy with mDNS | §6.1 | `packages/federation-common/` (extend) | ~1 week | Multi-node testing |
| 4 | Flease distributed lease coordination | §6.3 | `packages/kernel-lease/` (new) | ~2 weeks | CP-class writes |
| 5 | Local encrypted database layer (SQLCipher + Argon2id) | §11.2 Layer 1 | `packages/foundation-localfirst/` (extend) | ~1 week | Any real on-disk data |
| 6 | Role attestation + key distribution | §11.3 | `packages/kernel-security/` (new) | ~2 weeks | Bucket-subscription eligibility |
| 7 | Circuit-breaker quarantine queue (append-only event-sourced) | §11.2 Layer 4 | `packages/kernel-quarantine/` (new) | ~1 week | Offline-write safety |
| 8 | `ILocalNodePlugin` contract + plugin registry | §5.3 | `packages/kernel/` (extend the façade; add runtime) | ~1 week | Blocks as plugins (paper §5.1) instead of static ProjectReferences |

**Total critical-path Phase 1 effort:** ~2-3 months of focused work. Paper estimates Phase 1 at Months 1–4. Aligned.

---

## 5. Partial Matches — Would Close Quickly

| # | Primitive | Current state | Needed |
|---|---|---|---|
| A | **Event log** | `kernel-event-bus/InMemoryEventBus.cs` | Persistent event-log implementation (file-backed log + snapshot format per paper §8) |
| B | **Schema registry** | `kernel-schema-registry/InMemorySchemaRegistry.cs` | Bidirectional-lens support (paper §7.3) + epoch coordination primitive |
| C | **Federation** | 4 packages with `ISyncTransport`, `IEntitySyncer`, `ICapabilitySyncer` | Architectural reconciliation: retrofit as the sync-daemon's transport layer, OR re-scope `federation-*` as a specific inter-team federation mode distinct from intra-team gossip |
| D | **Blocks-accounting** | Generic block | Refactor to event-sourced posting engine per paper §12 when kernel's event log lands |
| E | **Foundation-localfirst** | `OfflineStore` + `OfflineQueue` + `SyncEngine` | Promote `OfflineQueue` to paper's quarantine semantics (append-only, explicit-promote-or-reject). Add encryption. |

---

## 6. Structural Conflicts — Would Require ADRs

| # | Conflict | Paper position | Repo position | Resolution path |
|---|---|---|---|---|
| α | **Bridge's SaaS-shell framing** | Cloud is a peer (§3, §17.2) | Multi-tenant hosted authority (ADR 0006) | ADR 0006 update: reposition Bridge as managed relay OR document two product postures |
| β | **`packages/kernel` as type-forwarder** | Kernel is a runtime with lifecycle, plugin discovery, sync daemon (§5.1) | Kernel is a `[TypeForwardedTo]` façade for 7 primitives (`TypeForwards.cs`) | New ADR: split `packages/kernel` into `packages/kernel-primitives-facade` (current) + `packages/kernel-runtime` (new, paper-aligned) |
| γ | **Blocks as static ProjectReferences** | Blocks are discovered via `IUiBlockManifest` and `ILocalNodePlugin` at startup (§5.3) | Blocks are imported via csproj ProjectReference | Introduce plugin registry; make blocks opt-into discovery; retain static ProjectReference as fallback during transition |
| δ | **Blazor-only adapter** | React parity mandatory (§5.2; ADR 0014) | `packages/ui-adapters-blazor` only | Scaffold `packages/ui-adapters-react` per ADR 0014 — acknowledged gap |
| ε | **Ingestion subsystem scope** | Paper mentions "input modalities" abstractly; `packages/ingestion-*` (7 packages) predates paper | (pre-paper architecture) | Minor — paper is silent on ingestion specifics; current ingestion packages are compatible with the local-node model. No action required beyond documenting the mapping. |

---

## 7. Strengths — What's Already Right

1. **Compat layering matches paper §5.2 precisely.** The 13 compat packages (telerik, syncfusion, infragistics, shared + 9 icon-compats) are a textbook implementation of the paper's "Compatibility and Adapter Layer." This session's work directly executed paper guidance.
2. **Kernel primitives façade closes the spec-§3 Layer-2 gap.** `packages/kernel/TypeForwards.cs` was the right interim move to give the kernel a name without duplicating code.
3. **Microkernel-monolith intent is preserved in package structure.** Kernel → Foundation → UI-core → Adapter → Blocks → Compat → Apps is the paper's architecture topology. Code ships in the right packages.
4. **Federation packages validate the sync concept, even if the model diverges.** Signed envelopes, content-addressed distribution (via IPFS/Kubo), peer registries — these are production-shaped sync primitives. Retrofitting them as the sync-daemon transport layer is plausible.
5. **Anchor was created.** Just having `accelerators/anchor` as a first-class repo citizen (vs. a future TODO) means the paper's local-first pillar has a landing zone.

---

## 8. Recommended Next Steps

Prioritized by paper-alignment value vs. engineering cost.

### Tier 1 — Unblockers (do first)

1. **Write the sync-daemon protocol sub-document** at `docs/specifications/sync-daemon-protocol.md`. Paper §18 says "delivered first — it unblocks all Phase 1 work." Effort: 2-3 days of writing. Zero code.
2. **Draft ADR 0026 — Bridge as managed relay vs. SaaS shell.** Resolves structural conflict α. Effort: 1 day.
3. **Draft ADR 0027 — kernel runtime vs. façade split.** Resolves conflict β. Effort: 1 day.

### Tier 2 — Kernel Phase-1 implementation

4. CRDT library evaluation and choice (Yjs / Loro / Automerge — paper §9 signals Loro + Yjs as strong candidates). Effort: 1 week eval + 1 week integration spike.
5. Gossip anti-entropy on top of the existing federation transport. Retrofits current federation infra as the paper's sync-daemon transport. Effort: ~2 weeks.
6. Flease lease coordination. Effort: ~2 weeks.
7. Encrypted local store (SQLCipher / Argon2id). Effort: ~1 week.
8. `ILocalNodePlugin` + registry. Effort: ~1 week.

### Tier 3 — Anchor build-out

9. Start landing Anchor's 8 deferred deliverables, ordered to exercise each Tier-2 primitive as it ships. This is the validation that the paper's architecture assembles into working local-first software.

### Tier 4 — Deferred per paper timeline

10. Schema lenses, epoch coordination, snapshot format, CRDT GC, sync-bucket YAML engine — Phase 3 per paper §18, probably Months 9–12.
11. MDM installer, SBOM, air-gap mode, managed relay SLA — Phase 4, Months 13–18.

---

## 9. Open Questions for BDFL

1. **Bridge posture** — retain multi-tenant SaaS shell as a supported product mode, or sunset Posture 1 once Anchor + managed-relay ships?
2. **CRDT engine choice** — Yjs (most mature), Loro (paper §9 signals "compact encoding + shallow snapshots"), Automerge (research-notes evaluated it)? Affects all downstream Phase 1 work.
3. **React adapter** — which vertical needs React parity first? Or is Blazor-only acceptable through v1?
4. **Federation model reconciliation** — keep current federation packages as an inter-team federation mode, or retrofit as the intra-team sync-daemon transport?
5. **Anchor re-activation timing** — wait for ADR 0017 Web Components migration as originally gated, or de-gate now that the paper is the load-bearing spec?

---

## Cross-References

- [`_shared/product/local-node-architecture-paper.md`](../../../_shared/product/local-node-architecture-paper.md) — the paper this audit is anchored against.
- [`accelerators/anchor/README.md`](../../../accelerators/anchor/README.md) — deferred local-first accelerator.
- [`accelerators/bridge/PLATFORM_ALIGNMENT.md`](../../../accelerators/bridge/PLATFORM_ALIGNMENT.md) — prior Bridge-vs-spec gap analysis (complementary to this document).
- [`packages/kernel/README.md`](../../../packages/kernel/README.md) — current kernel-as-façade documentation.
- [`icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md`](../../01_discovery/output/sunfish-gap-analysis-2026-04-18.md) — prior spec-§3-focused gap analysis (G1-Gn). Complements this document's paper-§1-through-§20 focus.

---

*This audit is a snapshot at 2026-04-22. Re-run after any Phase-1 kernel work lands to re-evaluate coverage.*
