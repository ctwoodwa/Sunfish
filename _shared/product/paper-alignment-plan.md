# Paper-Alignment Plan — Execution Plan to Align the Repo with the Foundational Paper

**Date:** 2026-04-22
**Status:** Active
**Source of truth:** [`_shared/product/local-node-architecture-paper.md`](./local-node-architecture-paper.md) (*Inverting the SaaS Paradigm*, v10.0)
**Audit basis:** [`icm/07_review/output/paper-alignment-audit-2026-04-22.md`](../../icm/07_review/output/paper-alignment-audit-2026-04-22.md)

---

## Purpose

The paper is now the source of truth. This plan sequences the work required to close the gap between the paper's architecture and the current repo. The audit identified ~20 critical gaps; this plan buckets them into five waves that respect (a) the paper's own Phase-1-first sequencing directive, (b) dependency chains between primitives, and (c) opportunities for parallel subagent fan-out.

**Execution mode:** dynamic `/loop` with parallel subagent dispatch. Each wave completes before the next wave's dependencies unlock. Wave 0 is docs-only (parallel-safe, no build contention); Waves 1–3 mix docs and code with per-wave agent caps.

**Pre-release posture reminder:** Sunfish is pre-v1. Breaking changes are approved. Third-party provider compatibility is relaxed until first public release. The plan below accepts aggressive architectural changes where they clearly land the paper's intent.

---

## Wave 0 — Foundational Specs and ADRs (docs-only, parallel-safe)

**Goal:** Produce the design documents that every downstream code wave reads. All Wave-0 agents write to distinct `docs/` paths — zero file-collision, zero build contention. Safe to fan out.

| # | Deliverable | Path | Paper § |
|---|---|---|---|
| 0.1 | **Sync daemon protocol specification** — HELLO / CAPABILITY_NEG / ACK / DELTA_STREAM / GOSSIP_PING wire format, Unix-socket transport spec, capability-negotiation semantics, stream-subscription filtering. Paper §18 names this as *"delivered first — it unblocks all Phase 1 work."* | `docs/specifications/sync-daemon-protocol.md` | §6.2, §18 |
| 0.2 | **ADR 0026 — Bridge posture (SaaS shell vs. managed relay).** Reconcile Bridge's current multi-tenant-authority shape with paper §3's inversion. Options: deprecate SaaS mode / repose as managed relay / support both postures via explicit opt-in. | `docs/adrs/0026-bridge-posture.md` | §3, §17.2 |
| 0.3 | **ADR 0027 — Kernel runtime split.** Today's `packages/kernel` is a type-forwarding façade; paper's kernel is a runtime with lifecycle + plugin discovery + sync daemon. Decide: keep façade, add `packages/kernel-runtime` alongside, OR replace façade. | `docs/adrs/0027-kernel-runtime-split.md` | §5.1, §5.3 |
| 0.4 | **ADR 0028 — CRDT engine selection.** Choose between Yjs, Loro, and Automerge-inspired native .NET implementation. Paper §9 signals Loro (compact encoding + shallow snapshots) and Yjs (mature internal GC). Cites existing `docs/specifications/research-notes/automerge-evaluation.md`. | `docs/adrs/0028-crdt-engine-selection.md` | §9, §18 |
| 0.5 | **ADR 0029 — Federation reconciliation.** Current `packages/federation-*` (4 packages) implements signed-envelope federation; paper mandates gossip anti-entropy. Decide: retrofit federation as the gossip transport layer, OR keep federation as a distinct inter-team mode while adding a new intra-team gossip daemon. | `docs/adrs/0029-federation-reconciliation.md` | §6.1, §6.2 |
| 0.6 | **ADR 0030 — React adapter scaffolding.** Paper §5.2 + ADR 0014 require React parity; no `packages/ui-adapters-react` exists. Scope the scaffolding work (component surface, tests, build tooling) without implementing components yet. | `docs/adrs/0030-react-adapter-scaffolding.md` | §5.2 |

**Dispatch strategy:** All six agents in parallel. Each produces a self-contained markdown file. No agent modifies any other file. Safe concurrency = 6.

**Exit criterion:** Six files land on `main`. Each ADR includes a recommendation (not just a question); the user reviews and ratifies via a follow-up commit that flips status to Accepted.

---

## Wave 1 — Kernel Primitives Scaffolding (code + tests, moderate parallelism)

**Goal:** Stand up empty-but-typed contracts + minimal implementations for the Phase-1 primitives the paper names. Each primitive lands as a new package or extension of an existing one.

| # | Deliverable | Package | Paper § | Depends on |
|---|---|---|---|---|
| 1.1 | **`ILocalNodePlugin` contract + plugin registry.** Discovery via convention-based registration. Extension-point interfaces (`IStreamDefinition`, `IProjectionBuilder`, `ISchemaVersion`, `IUiBlockManifest`). | `packages/kernel-runtime/` (new, per ADR 0027) | §5.1, §5.3 | ADR 0027 |
| 1.2 | **CRDT engine abstraction layer.** Wrap chosen library (per ADR 0028); expose Sunfish-flavored `ICrdtDocument`, `ICrdtText`, `ICrdtMap`, `ICrdtList` contracts; integrate with event log. | `packages/kernel-crdt/` (new) | §2.2, §9 | ADR 0028 |
| 1.3 | **Persistent event log.** Promote `packages/kernel-event-bus/InMemoryEventBus` to a file-backed append-only log with snapshot support per paper §8. Keep the in-memory impl as a test harness. | `packages/kernel-event-bus/` (extend) | §2.5, §8 | — |
| 1.4 | **Encrypted local store.** Extend `foundation-localfirst` with SQLCipher at rest + Argon2id key derivation + OS-keystore integration (DPAPI / Keychain / libsecret). | `packages/foundation-localfirst/` (extend) | §11.2 Layer 1 | — |
| 1.5 | **Circuit-breaker quarantine queue (append-only, event-sourced).** Promote `foundation-localfirst/OfflineQueue.cs` to paper's quarantine semantics (append-only, explicit-promote-or-reject, reason-recorded). | `packages/foundation-localfirst/` (extend) | §11.2 Layer 4 | 1.3 |
| 1.6 | **Role attestation + key distribution.** Signed attestation bundles (Ed25519); per-role symmetric keys wrapped per-member with public-key crypto; administrative events in the log carry key bundles. | `packages/kernel-security/` (new) | §11.3 | — |
| 1.7 | **Schema registry v2 — bidirectional lenses.** Extend `kernel-schema-registry` from in-memory stub to Cambria-style declarative lenses + epoch tracking. | `packages/kernel-schema-registry/` (extend) | §7.3, §7.4 | — |

**Dispatch strategy:** Waves 1A and 1B, three agents each. 1A: 1.1, 1.3, 1.4 (independent; each touches a different package). 1B: 1.2, 1.5, 1.6 (1.2 needs ADR 0028; 1.5 needs 1.3 to be merged; 1.6 is independent). 1.7 can start any time but is lowest-priority — last agent in the wave.

**Caution:** Concurrent `dotnet build` contention on shared dependencies (`foundation`, `ui-core`, `ui-adapters-blazor`). Keep per-wave concurrency ≤ 3. Each agent instructed NOT to touch `Sunfish.slnx` — batched at wave end.

**Exit criterion:** All contracts compile; all in-wave tests pass; `Sunfish.slnx` updated in one batch commit per wave. Build time per agent ≤ 20 min expected.

---

## Wave 2 — Sync Transport and Node Host (code + integration)

**Goal:** Make the kernel primitives talk to each other over the wire.

| # | Deliverable | Package | Paper § | Depends on |
|---|---|---|---|---|
| 2.1 | **Gossip anti-entropy daemon.** 30-second tick, random peer selection, delta exchange. Either (a) retrofits `federation-common` as the transport, or (b) new daemon per ADR 0029. | `packages/kernel-sync/` (new) or `federation-common` (extend) | §6.1, §6.2 | ADR 0029, 1.2, 1.3 |
| 2.2 | **mDNS peer discovery.** Tiered fallback: mDNS → mesh-VPN / WireGuard → managed relay. | `packages/kernel-sync/` | §6.1 | 2.1 |
| 2.3 | **Flease distributed lease coordination.** CP-class record writes. Quorum-negotiated leases, 30s default, auto-expire. | `packages/kernel-lease/` (new) | §6.3 | 2.1 |
| 2.4 | **Declarative sync-bucket engine (YAML).** Bucket definitions, role-attestation eligibility evaluation at capability-negotiation time, lazy-fetch stub representation, storage-budget LRU eviction. | `packages/kernel-buckets/` (new) | §10.2, §10.3 | 1.6, 2.1 |
| 2.5 | **Local-node host process.** Container-stack orchestration. Persistent background service registered with systemd / launchd / Windows Service. Starts at login. Shell-connects-to-running-stack model. | `apps/local-node-host/` (new) or `accelerators/anchor/` extension | §4, §5.1 | 1.1 |

**Dispatch strategy:** 2.1 → 2.2, 2.3, 2.4 in parallel once 2.1 lands; 2.5 last. This wave needs actual multi-node testing so per-agent scope is bigger than Wave 1.

**Exit criterion:** Two local-node instances discover each other via mDNS, exchange gossip deltas, negotiate a lease, and survive a simulated partition-and-reconnect test.

---

## Wave 3 — UI Kernel Completion and Anchor Re-Activation (code + UX)

**Goal:** Make the local-first node visible through the UI as paper §13 requires, and light up Anchor as the first end-to-end local-first deliverable.

| # | Deliverable | Package | Paper § | Depends on |
|---|---|---|---|---|
| 3.1 | **Sync/freshness state tokens.** Extend `ui-core` design tokens with: sync-healthy, stale, offline, conflict-pending, quarantine. All three provider skins (BS5, Fluent, M3) implement. | `packages/ui-core`, `packages/ui-adapters-blazor/Providers/*` | §5.2, §13.2 | — |
| 3.2 | **Paper-specific components.** Freshness badge (bound to per-record staleness); node-health / link-status status-bar indicators; conflict-list subscribed to kernel's conflict inbox; optimistic-write button (pending/confirmed/failed states). | `packages/ui-adapters-blazor/Components/LocalFirst/` (new subfolder) | §5.2, §13.1, §13.2 | 3.1, 1.3 |
| 3.3 | **Anchor re-activation.** Land the 8 deferred deliverables from `accelerators/anchor/README.md`: LocalFirst wiring, bundle selection UI, report catalog, audit log surface, sync toggle, auth model, platform packaging, auto-update. De-gate from ADR 0017 Web Components migration since the paper supersedes it. | `accelerators/anchor/` | §4, §13 | 1.4, 2.5, 3.2 |
| 3.4 | **QR-code onboarding.** Scan to transfer role attestation bundle + initial CRDT snapshot. Three-step install flow per paper §13.4. | `accelerators/anchor/` | §13.4 | 1.6, 3.3 |
| 3.5 | **React adapter scaffolding.** Per ADR 0030. Not full parity — structural shell + build pipeline + first 3 components as proof-of-concept. | `packages/ui-adapters-react/` (new) | §5.2, ADR 0014 | ADR 0030 |

**Dispatch strategy:** 3.1 first (small, unblocking); 3.2 + 3.5 in parallel; 3.3 + 3.4 last (largest scope).

**Exit criterion:** Anchor installed on two machines discovers peer, renders a shared CRDT document, exits gracefully to offline mode when network disconnected, reconnects without data loss.

---

## Wave 4 — Ledger, Bridge Reposition, Compaction (scoped polish)

**Goal:** Close the paper's Phase-3 and Phase-4 items that aren't blocking.

| # | Deliverable | Path | Paper § | Depends on |
|---|---|---|---|---|
| 4.1 | **Event-sourced ledger kernel subsystem.** Refactor `blocks-accounting` to the paper's posting-engine + CQRS-read-models architecture. Promote financial primitives from block to kernel subsystem per §12.1. | `packages/kernel-ledger/` (new) + `packages/blocks-accounting/` (refactor) | §12 | 1.3, 2.3 |
| 4.2 | **Bridge reposition per ADR 0026.** Execute the ADR's decided direction (deprecate / reposition / dual-posture). | `accelerators/bridge/` | §17.2 | ADR 0026 |
| 4.3 | **Stream compaction.** Periodic copy-transform job + upcaster retirement per paper §7.2. | `packages/kernel-schema-registry/` (extend) | §7.2 | 1.7 |
| 4.4 | **CRDT GC + sharding strategy.** Application-level document sharding + shallow snapshots per paper §9. | `packages/kernel-crdt/` (extend) | §9 | 1.2 |
| 4.5 | **MDM-compatible installer + SBOM + air-gap deployment.** Phase-4 enterprise primitives per paper §16. | various | §16 | 3.3 |

**Dispatch strategy:** Lower priority; execute opportunistically once Waves 0–3 have landed.

---

## Meta — Testing and Governance

Parallel to every code wave:

- **Property-based test harness** for CRDT convergence / idempotency / commutativity / monotonicity (paper §15 Level 1). Start in Wave 1 when `kernel-crdt` lands.
- **Deterministic simulation test harness** for mixed-version nodes, epoch transitions, lease edge cases (paper §15 Level 4). Start in Wave 2.
- **Sunfish.Analyzers** expansion — the vendor-usings analyzer that shipped this session is a pattern; add analyzers for: deprecated `DialogClass(bool)` (if any survivor callers), plugin-registration correctness, stream-subscription correctness.

---

## Non-Goals (explicitly out of scope)

1. **Rewriting the UI-adapter-Blazor components to be "framework-agnostic."** The paper's framework-agnostic tier is honored via `ui-core` contracts + compat packages. Blazor stays as a concrete adapter; React joins it per Wave 3.5.
2. **Full CRDT library implementation from scratch.** Wave 1.2 wraps an existing library per ADR 0028.
3. **Full decentralized archival tier (paper §2.4 Tier 5).** Phase 4, explicitly opt-in per paper.
4. **mar-\* cleanup across non-DataGrid components.** Housekeeping backlog from ADR 0025 Phase 1; orthogonal to paper alignment. Tracked separately.

---

## Execution Protocol

1. **Each wave gets a `/loop tiers until completed`** invocation. Agents fan out within the wave's parallelism cap.
2. **`Sunfish.slnx` updates batched per wave.** Each agent instructed not to touch slnx; I (or whoever runs the wave) batches slnx entries at wave close.
3. **Each agent's output reviewed before next wave starts.** Follow-up PRs allowed during the next wave for minor corrections.
4. **ADRs stay in "Proposed" until ratified.** Wave 0 ships six Proposed ADRs; the user/BDFL accepts-or-redirects en bloc; accepted ADRs unlock their downstream waves.
5. **Audit re-run at each wave completion.** [`icm/07_review/output/paper-alignment-audit-*`](../../icm/07_review/output/) gets a new dated file showing the deltas.

---

## Cross-References

- [`_shared/product/local-node-architecture-paper.md`](./local-node-architecture-paper.md) — the paper this plan implements.
- [`icm/07_review/output/paper-alignment-audit-2026-04-22.md`](../../icm/07_review/output/paper-alignment-audit-2026-04-22.md) — the gap analysis this plan resolves.
- [`accelerators/anchor/README.md`](../../accelerators/anchor/README.md) — the accelerator Wave 3.3 re-activates.
- [`accelerators/bridge/PLATFORM_ALIGNMENT.md`](../../accelerators/bridge/PLATFORM_ALIGNMENT.md) — the parallel Bridge-specific alignment doc.
- `.claude/rules/universal-planning.md` — the UPF this plan structure follows.

---

*This plan is a working document. Expect updates after each wave closes as the repo's real state informs the next wave's scoping.*
