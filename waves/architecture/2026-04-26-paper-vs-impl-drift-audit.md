# Paper-vs-Implementation Drift Audit

**Date:** 2026-04-26
**Auditor:** Claude Code (Opus 4.7)
**Source of truth:** [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) — *Inverting the SaaS Paradigm*, **Version 12.0**, April 2026
**Method:** Read paper end-to-end (760 lines, Sections 1–20.8); cross-walked each named architectural element against the in-tree implementation (`packages/`, `accelerators/`, `apps/`, `docs/adrs/`).
**Scope:** Architectural drift only — not coding quality, test coverage, or release-readiness (those are tracked in the parallel session debt audit at `waves/cleanup/2026-04-26-followup-debt-audit.md`).

---

## Note on paper version

CLAUDE.md and the audit-task brief both reference *Version 10.0, April 2026*. The paper file currently in the repo is stamped **Version 12.0, April 2026**. The earlier paper-alignment audit set (`icm/07_review/output/paper-alignment-audit-2026-04-22.md` … `-04-23-post-wave-6-4.md`) was performed against v10.0 and drove the kernel-runtime split (ADR 0027), the bridge-posture decisions (ADRs 0026, 0031), and the multi-team Anchor design (ADR 0032). Most of those structural gaps have since been closed by Waves 1–6.

This audit re-runs the cross-walk against v12.0 and reports the residual drift after that work. CLAUDE.md still cites v10.0; that string should be bumped to v12.0 in a follow-up.

---

## Verdict legend

- **AS-PAPER** — implementation matches the paper's named element
- **DRIFT** — implementation exists but diverges in shape, scope, or naming
- **MISSING** — paper names the element; no implementation exists
- **ADDED-NOT-IN-PAPER** — implementation exists but the paper does not name or scope it

---

## 1. Per-element verdict table

### 1a. Kernel responsibilities (Paper §5.1)

| # | Paper element | Repo location | Verdict | Notes |
|---|---|---|---|---|
| K1 | Node lifecycle + process orchestration | `packages/kernel-runtime/{NodeHost,INodeHost,NodeState}.cs`; `apps/local-node-host/{LocalNodeWorker.cs,Program.cs}` | AS-PAPER | Persistent worker host exists; runs the full kernel composition. OS-service-manager registration (systemd/launchd/Windows Service) per §4 still pending — see drift D1. |
| K2 | Sync daemon protocol + gossip + lease | `packages/kernel-sync/{Discovery,Gossip,Handshake,Protocol}/`; `packages/kernel-lease/FleaseLeaseCoordinator.cs` | AS-PAPER | HELLO / CAPABILITY_NEG / DELTA_STREAM / GOSSIP_PING messages, mDNS discovery, vector clocks, Unix-socket transport, Flease coordinator with quorum semantics — all present. Out-of-process daemon split per §6.2 not implemented (sync runs in-process today) — see drift D2. |
| K3 | CRDT engine abstraction + event log + snapshots + compaction | `packages/kernel-crdt/{ICrdtEngine,Backends/YDotNetCrdtEngine,GC,Sharding,SnapshotScheduling}/`; `packages/kernel-event-bus/FileBackedEventLog.cs` | AS-PAPER | Yjs (.NET binding) backend selected per ADR 0028. Document GC, sharding, shallow-snapshot policy, persistent file-backed event log all in place. |
| K4 | Schema migration (expand/contract, lenses, epochs) | `packages/kernel-schema-registry/{Epochs/EpochCoordinator,Lenses/{ISchemaLens,LensGraph},Migration/CopyTransformMigrator,Upcasters/{IUpcaster,UpcasterChain},Compaction/CompactionScheduler}.cs` | AS-PAPER | Bidirectional Cambria-style lenses + lens-graph traversal, epoch coordinator, copy-transform migrator, upcaster retirement after compaction — full §7.1–§7.4 coverage. |
| K5 | Security primitives (encryption, key mgmt, role attestation) | `packages/kernel-security/{Attestation,Crypto/{Ed25519Signer,X25519KeyAgreement},Keys/{RoleKeyManager,SqlCipherKeyDerivation,TeamSubkeyDerivation}}/`; `packages/foundation-localfirst/Encryption/{Argon2idKeyDerivation,SqlCipherEncryptedStore,WindowsDpapiKeystore,MacOsKeychainKeystore,LinuxLibsecretKeystore}.cs` | AS-PAPER | Layer 1 (SQLCipher + Argon2id + per-OS keystore), Layer 2 (per-role symmetric keys with X25519 wrap), Layer 3 (subscription filtering at sync boundary) all present. Layer 4 = K7. |
| K6 | Partial / selective sync engine (buckets) | `packages/kernel-buckets/{Bucket,BucketDefinition,BucketRegistry,BucketYamlLoader,LazyFetch/BucketStub,Storage/{InMemoryStorageBudgetManager,StorageBudget}}.cs` | AS-PAPER | Declarative YAML buckets, attestation-gated subscription, lazy-fetch stub representation, storage-budget manager — matches §10.2–§10.3 directly. |
| K7 | Plugin discovery + lifecycle | `packages/kernel-runtime/{PluginRegistry,IPluginRegistry,IPluginContext,ILocalNodePlugin,IStreamDefinition,IProjectionBuilder,ISchemaVersion,IUiBlockManifest}.cs` | AS-PAPER | All five §5.3 extension-point interfaces present and named exactly per the paper. `PluginCyclicDependencyException` enforces declared dependencies. |

### 1b. UI kernel four-tier (Paper §5.2)

| # | Paper element | Repo location | Verdict | Notes |
|---|---|---|---|---|
| U1 | Foundation tier (tokens, primitives, sync-state vocab) | `packages/ui-core/src/` (TS contracts + design tokens) | AS-PAPER | Sync-state tokens (`sync-healthy`, `stale`, `offline`, `conflict-pending`, `quarantine`) materialized; tooling (`tooling/Sunfish.Tooling.ColorAudit/`) audits palette consistency. |
| U2 | Framework-Agnostic Component Layer | `packages/ui-core/components/` (TS contracts); `packages/ui-core/Contracts/` (.NET interfaces) | DRIFT | Contracts are framework-agnostic, but the rendered components ship via per-framework adapters (Blazor + nascent React). The paper's Lit/Web-Components implementation route (per ADR 0017) is the chosen technical basis but not yet realized as runnable framework-agnostic components — only as TS contracts. See drift D3. |
| U3 | Blocks and Modules | `packages/blocks-{tasks,scheduling,leases,assets,maintenance,inspections,forms,workflow,businesscases,accounting,subscriptions,tax-reporting,tenant-admin,rent-collection}/` | ADDED-NOT-IN-PAPER (in scope) | 14 blocks shipped. Paper §5.2 names the *category* but only example-cites task board, scheduling, ledger grid, conflict wizard. Real-estate domain (leases, rent collection, inspections) is repo product scope, not paper scope — fine, but not paper-traceable. |
| U4 | Compatibility and Adapter Layer | `packages/compat-telerik`, `packages/compat-syncfusion`, `packages/compat-infragistics`, plus icon-pack compats (`compat-{bootstrap,fluent,font-awesome,heroicons,lucide,material,octicons,simple,tabler}-icons`, `compat-shared`) | AS-PAPER | Pattern executes paper §5.2's "Compatibility and Adapter Layer" verbatim — vendor adapters wrap real Sunfish surfaces; paper §5.3 final paragraph ("the kernel cannot distinguish a first-party plugin from a compatibility adapter") is structurally honored. |

### 1c. Sync architecture (Paper §6)

| # | Paper element | Repo location | Verdict | Notes |
|---|---|---|---|---|
| S1 | Gossip anti-entropy with vector clocks | `packages/kernel-sync/Gossip/{GossipDaemon,VectorClock}.cs` | AS-PAPER | 30-second gossip tick configurable via `GossipDaemonOptions`. |
| S2 | Tiered peer discovery (mDNS / mesh-VPN / managed relay) | `packages/kernel-sync/Discovery/{MdnsPeerDiscovery,InMemoryPeerDiscovery}.cs`; `accelerators/bridge/Sunfish.Bridge/Relay/{RelayServer,RelayWorker}.cs` | DRIFT | Tiers 1 and 3 present (mDNS + Bridge Relay). Tier 2 (mesh VPN — WireGuard etc.) is not implemented and has no ADR — see drift D4. |
| S3 | Sync-daemon out-of-process with Unix-socket transport | `packages/kernel-sync/Protocol/{UnixSocketSyncDaemonTransport,WebSocketSyncDaemonTransport,InMemorySyncDaemonTransport}.cs` | DRIFT | Transport plumbing exists; the daemon itself currently runs in-process with the rest of the kernel inside `apps/local-node-host`. The "separate process… application restarts or updates without disrupting sync" property of paper §6.2 is therefore not yet realized — see drift D2. |
| S4 | Distributed lease coordination (Flease) | `packages/kernel-lease/{FleaseLeaseCoordinator,ILeaseCoordinator,LeaseCoordinatorOptions}.cs` | AS-PAPER | 30-second default lease, ceil(N/2)+1 quorum default, `QuorumUnavailableException` blocks the write per §6.3. |

### 1d. Storage tiers (Paper §2.4)

| # | Paper element | Repo location | Verdict | Notes |
|---|---|---|---|---|
| T1 | Local encrypted database | `packages/foundation-localfirst/Encryption/SqlCipherEncryptedStore.cs` | AS-PAPER | SQLCipher per §11.2 Layer 1. |
| T2 | CRDT + event log (append-only) | `packages/kernel-event-bus/FileBackedEventLog.cs`; `packages/kernel-crdt/Backends/YDotNetCrdtEngine.cs` | AS-PAPER | CBOR-framed append log with epoch-scoped files, snapshots, fsync semantics. |
| T3 | User-controlled cloud backup (object storage) | `packages/foundation/Blobs/`; `packages/foundation-assets-postgres/` | DRIFT | Blob abstraction exists but currently targets Postgres (server-side) and Kubo (federation). No "user-pointed S3/R2/B2" disaster-recovery adapter wired specifically as the paper's tier-3 backup target — see drift D5. |
| T4 | Content-addressed distribution (optional) | `packages/federation-blob-replication/{IpfsBlobStore,Kubo/}` | AS-PAPER | Kubo/IPFS-backed CAS implementation; opt-in per the paper's "tiers 4 and 5 are opt-in." |
| T5 | Decentralized archival (optional, enterprise) | — | MISSING | No proof-of-storage / archival-tier adapter. Paper marks this as opt-in / enterprise tier; absence is acceptable but should be tracked. |

### 1e. CP/AP per record class (Paper §2.2)

| # | Paper record class | Expected position | Repo location | Verdict | Notes |
|---|---|---|---|---|---|
| C1 | Documents, notes, task descriptions → AP via CRDT | AP | `packages/kernel-crdt` (ICrdtMap/Text/List); used by `blocks-tasks`, `blocks-forms`, etc. | AS-PAPER | CRDT primitives present and shipped to blocks. |
| C2 | Team membership / permissions → AP with deferred merge + role attestation | AP-deferred | `packages/kernel-runtime/Teams/`; `packages/kernel-security/Attestation/` | AS-PAPER | Attestation bundle, issuer, verifier; team context machinery — matches §11.3 key flow. |
| C3 | Resource reservations / scheduled slots → CP via Flease | CP | `packages/kernel-lease/`; `packages/blocks-scheduling/` | DRIFT | Flease coordinator is shipped, but `blocks-scheduling/ScheduleViewBlock.razor` does not currently take a `ILeaseCoordinator` dependency for slot reservation. The CP wiring at the consumer site is missing — see drift D6. |
| C4 | Financial transactions → CP via Flease + ledger | CP | `packages/kernel-ledger/`; `packages/blocks-accounting/` | DRIFT | Ledger primitives exist (PostingEngine, BalanceProjection, PeriodCloser). Lease coordination on the posting path is not visibly wired in `kernel-ledger/PostingEngine.cs` — see drift D6. |
| C5 | Audit / governance → CP + append-only | CP-append-only | `packages/kernel-event-bus/FileBackedEventLog.cs` | AS-PAPER | Event log is structurally append-only. |

### 1f. Schema migration (Paper §7)

| # | Paper element | Repo location | Verdict | Notes |
|---|---|---|---|---|
| M1 | Expand-contract pattern documentation/tooling | `packages/kernel-schema-registry/` (mechanism); no expand-contract guide in `docs/` | DRIFT | The mechanism exists (lenses + epochs) but the *expand-contract operational pattern* (paper §7.1) is not codified as developer guidance / lint rule / template. Plugin authors have no checklist. See drift D7. |
| M2 | Event versioning + upcasters | `packages/kernel-schema-registry/Upcasters/` | AS-PAPER | `IUpcaster`, `UpcasterChain` — and chain retirement after compaction. |
| M3 | Bidirectional schema lenses | `packages/kernel-schema-registry/Lenses/{ISchemaLens,LensGraph}.cs` | AS-PAPER | Cambria-style lens graph with shortest-path traversal. |
| M4 | Epoch coordination via lease quorum | `packages/kernel-schema-registry/Epochs/{EpochCoordinator,IEpochCoordinator}.cs` | AS-PAPER | Coordinator present; quorum-driven epoch announce/cutover. |

### 1g. Snapshots and rehydration (Paper §8)

| # | Paper element | Repo location | Verdict | Notes |
|---|---|---|---|---|
| N1 | Snapshot file format (`aggregate_id, epoch_id, schema_version, last_event_seq, snapshot_payload, created_at`) | `packages/kernel-event-bus/FileBackedEventLog.cs` (`snapshot-{aggregateId}-{epochId}-{schemaVersion}-{ticks}.cbor`) | AS-PAPER | File-naming embeds the paper's named tuple; rehydration picks largest CreatedAt. |
| N2 | Rehydration with epoch/schema check + lens application | implicit in event-bus + schema-registry; no explicit `RehydrationOrchestrator` | DRIFT | Rehydration logic is split across the two packages; there is no single orchestrator implementing the §8.3 four-step recipe. Functional but undocumented. See drift D8. |

### 1h. Security architecture (Paper §11)

| # | Paper element | Repo location | Verdict | Notes |
|---|---|---|---|---|
| Q1 | Layer 1 — encryption at rest (SQLCipher + Argon2id + OS keystore) | `packages/foundation-localfirst/Encryption/` (full set) | AS-PAPER | All three OS keystores wrapped (Windows DPAPI, macOS Keychain, Linux libsecret). |
| Q2 | Layer 2 — field-level encryption per role | `packages/kernel-security/Keys/RoleKeyManager.cs` + `TeamSubkeyDerivation.cs` | AS-PAPER | Role-key wrap/unwrap mechanism; per-bucket field encryption surface implied via attestation gate. |
| Q3 | Layer 3 — stream-level data minimization | `packages/kernel-buckets/BucketRegistry.cs` + `kernel-sync` capability negotiation | AS-PAPER | Filtering enforced at sync boundary; matches §11.2 Layer 3 explicitly. |
| Q4 | Layer 4 — circuit-breaker quarantine queue (event-sourced) | `packages/foundation-localfirst/Quarantine/{EventLogBackedQuarantineQueue,InMemoryQuarantineQueue}.cs` | AS-PAPER | Event-log-backed implementation honors §2.5 append-only requirement. |

### 1i. Ledger mechanics (Paper §12)

| # | Paper element | Repo location | Verdict | Notes |
|---|---|---|---|---|
| L1 | Double-entry posting with sum-to-zero invariant | `packages/kernel-ledger/{Posting,Transaction,LedgerEvents}.cs` | AS-PAPER | |
| L2 | Posting engine with idempotency keys | `packages/kernel-ledger/{PostingEngine,IPostingEngine}.cs` | AS-PAPER | |
| L3 | CQRS read models | `packages/kernel-ledger/CQRS/{BalanceProjection,IBalanceProjection,StatementProjection,IStatementProjection}.cs` | AS-PAPER | |
| L4 | Period close + rollups | `packages/kernel-ledger/Periods/{PeriodCloser,IPeriodCloser,IPeriodCloseState}.cs` | AS-PAPER | |

### 1j. UX (Paper §13)

| # | Paper element | Repo location | Verdict | Notes |
|---|---|---|---|---|
| X1 | Three always-visible status indicators (node health, link status, data freshness) | `packages/ui-adapters-blazor/Shell/`; `packages/ui-core/components/` | DRIFT | Status-bar surface exists in shell scaffolding; the explicit three-indicator triad bound to live kernel state is not yet wired in the demo (kitchen-sink) or in Anchor's MainPage. See drift D9. |
| X2 | Bulk conflict resolution wizard | none under `blocks-*` named "conflict" | MISSING | Paper §5.2 explicitly lists "bulk conflict resolution wizard" as a Blocks-tier component. No package implements it. See drift D10. |
| X3 | Multi-device QR onboarding | `accelerators/anchor/Services/QrOnboardingService.cs`; `accelerators/anchor/Components/QrScanner.razor` | AS-PAPER | CBOR-encoded payload per paper §13.4 verbatim. |
| X4 | Optimistic UI for AP writes / freshness badges for CP | `packages/ui-core/components/` (contracts) | DRIFT | Contracts exist; the paper's exact staleness-threshold table (§13.2: 5min / 10min / 15min / 24h) is not encoded as a kernel-shipped policy. Each block sets its own thresholds ad hoc. See drift D11. |

### 1k. Testing strategy (Paper §15)

| # | Paper level | Repo location | Verdict | Notes |
|---|---|---|---|---|
| Z1 | L1 — property-based (CRDT convergence) | `packages/kernel-crdt/tests/` | AS-PAPER | |
| Z2 | L2 — integration with real dependencies | `packages/kernel-sync/tests/`; `packages/federation-pattern-c-tests/` | AS-PAPER | |
| Z3 | L3 — fault injection in CI | scattered (`Fact(Skip)` for partition cases per debt audit §1) | DRIFT | Fault-injection harness exists *ad hoc*; no named CI job runs the §15.1-L3 matrix systematically. See drift D12. |
| Z4 | L4 — deterministic simulation | none | MISSING | No FoundationDB-style deterministic-simulation harness. Paper §15.1 names it explicitly. See drift D13. |
| Z5 | L5 — chaos in staging | none | MISSING | Acceptable pre-v1; should be on a future-tier roadmap entry. |

### 1l. Enterprise / IT governance (Paper §16)

| # | Paper element | Repo location | Verdict | Notes |
|---|---|---|---|---|
| E1 | MDM-compatible silent install | `accelerators/anchor/Platforms/` (MAUI per-platform) | DRIFT | MAUI handles per-platform packaging but no MDM profile templates / Intune-Jamf-equivalent docs ship in `docs/`. |
| E2 | SBOM in CI | `tooling/sbom/` | AS-PAPER | SBOM tooling present per ADR alignment. |
| E3 | Air-gap deployment mode | — | MISSING | No air-gap docs / internal-relay mode shipped. Paper §16.2. |
| E4 | BYOD path separation (named, MDM-targetable team-data path) | `packages/kernel-runtime/Teams/TeamPaths.cs` | AS-PAPER | TeamPaths abstraction provides per-team named paths; MDM-targetability is implied by the design. |

### 1m. Sustainability / managed relay (Paper §17.2)

| # | Paper element | Repo location | Verdict | Notes |
|---|---|---|---|---|
| R1 | Managed relay as commercial SKU | `accelerators/bridge/` (Mode = Relay per `BridgeMode.cs` + ADR 0026/0031) | AS-PAPER | Bridge.Relay mode realizes paper §6.1 tier-3 + §17.2 SKU. |
| R2 | "Hosted relay as a SaaS node" — ciphertext-only, role keys on end-user devices | `accelerators/bridge/` (Mode = SaaS per ADR 0031) | DRIFT | Bridge SaaS mode currently runs Postgres-backed authoritative storage, not the paper §17.2.1 ciphertext-only model. ADR 0031 acknowledges this and elects Zone-C Hybrid as the explicit framing. The paper's "relay-as-a-peer" invariant (key holders are end users only) is not yet enforced by Bridge SaaS — see drift D14. |

### 1n. Anchor (Zone A) and Bridge (Zone C) per paper §20.7

| # | Paper element | Repo location | Verdict | Notes |
|---|---|---|---|---|
| A1 | Anchor = Zone A local-first desktop | `accelerators/anchor/` (MAUI) | AS-PAPER | Mapping codified in Anchor README + ADR 0031. |
| A2 | Bridge = Zone C hybrid hosted-node-as-SaaS | `accelerators/bridge/` | AS-PAPER (intent), DRIFT (key-bootstrap) | See R2. |
| A3 | Multi-team Anchor (workspace switcher) | `packages/kernel-runtime/Teams/` (mechanism); ADR 0032 (design) | AS-PAPER | v2 design accepted; v1 ships single-team per install per ADR 0032 decision. |

### 1o. Architecture-selection framework (Paper §20)

| # | Paper element | Repo location | Verdict |
|---|---|---|---|
| F1 | Three outcome zones (A/B/C) | `_shared/product/architecture-principles.md`; ADR 0031 | AS-PAPER |
| F2 | The five filters | `_shared/product/architecture-principles.md` | AS-PAPER (assumed; not re-verified line-by-line in this audit) |

### 1p. Items in repo NOT named by the paper (ADDED-NOT-IN-PAPER)

These are in-scope product surfaces, not architecture drift, but are tabulated for traceability so anyone reading the paper does not assume they are missing.

| Element | Repo location | Notes |
|---|---|---|
| Real-estate domain blocks (leases, rent-collection, inspections, maintenance, assets, tenant-admin, tax-reporting) | `packages/blocks-{leases,rent-collection,inspections,maintenance,assets,tenant-admin,tax-reporting}/` | Product scope. Paper §5.2 does not name domain. |
| Multi-modal ingestion pipeline (forms, spreadsheets, voice, sensors, imagery, satellite) | `packages/ingestion-{core,forms,spreadsheets,voice,sensors,imagery,satellite}/` | Six modalities; not in paper. Documented in `packages/ingestion-core/README.md` as "Spec §7 unified ingestion pipeline" — that's the *platform spec*, not the foundational paper. |
| Federation packages (envelope-signed inter-team sync) | `packages/federation-{common,entity-sync,capability-sync,blob-replication,pattern-c-tests}/` | Pre-existed kernel-sync; ADR 0029 reconciled them as a distinct inter-team mode that complements (not replaces) intra-team gossip. |
| Foundation MultiTenancy abstraction | `packages/foundation-multitenancy/` | Drives Bridge SaaS isolation, not paper-named. |
| Macaroons-based capability tokens | `packages/foundation/Macaroons/` | Auth primitive; orthogonal to paper's role-attestation flow. |
| Rule-engine event bridge | `packages/foundation-rule-engine-event-bridge/` | Workflow primitive; orthogonal to paper. |
| Analyzers package (`SUNFISH_I18N_*` etc.) | `packages/analyzers/` | Build-time hygiene; orthogonal to paper. |
| Kitchen-sink demo + apps/docs | `apps/{kitchen-sink,docs}/` | Standard product playgrounds; not paper-named. |

---

## 2. Drift catalog and recommended actions

This catalog enumerates only the **DRIFT** rows from §1, plus the **MISSING** items worth tracking. Each entry says: what the paper requires, what the repo has, the gap, and the recommended action — **fix the implementation**, **update the paper**, or **accept the drift with rationale**.

### D1 — OS-service-manager registration (paper §4)

- **Paper:** Container stack runs as a "persistent background service registered with the OS service manager (systemd / launchd / Windows Service), starting at login."
- **Repo:** `apps/local-node-host` is a worker process that runs in-process. It is not yet packaged as a systemd unit / launchd plist / Windows Service.
- **Action — fix the implementation.** Add per-OS service-manager packaging tasks to `apps/local-node-host`. Tracked under future Anchor/Bridge install work; bring forward to a named wave entry.

### D2 — Sync daemon as separate process (paper §6.2)

- **Paper:** "The sync daemon runs as a separate process from the application container, communicating via a Unix socket. This allows it to continue operating while the application restarts or updates."
- **Repo:** All transports (`UnixSocketSyncDaemonTransport`, `WebSocketSyncDaemonTransport`, `InMemorySyncDaemonTransport`) ship; the daemon class itself runs inside `apps/local-node-host` worker rather than as a separate OS process.
- **Action — fix the implementation, not blocking pre-v1.** Split sync daemon into its own process under a sibling app `apps/local-node-sync-daemon` once Wave-2 stabilizes. Document the cross-process protocol in `docs/specifications/sync-daemon-protocol.md` (already exists per Wave-0).

### D3 — Framework-agnostic component layer ships only as contracts (paper §5.2)

- **Paper:** Tier 2 is "Framework-Agnostic Component Layer — Local-first-aware primitive components."
- **Repo:** `packages/ui-core/Contracts/` and `packages/ui-core/src/` ship contracts and tokens. Real, framework-agnostic *components* (Web Components / Lit per ADR 0017) are not yet shipped — only Blazor and (nascent) React adapter implementations exist.
- **Action — accept the drift with explicit rationale.** The repo elected per-framework adapters as the practical surface and treats Tier 2 as a contract layer rather than a runnable layer. Update CLAUDE.md's tier description to reflect this so future agents do not search for non-existent FA components, OR fund a Lit/Web-Components Tier-2 sprint per ADR 0017's roadmap.

### D4 — Tier-2 mesh-VPN peer discovery (paper §6.1)

- **Paper:** Peer discovery tier 2 is "Mesh VPN (e.g., WireGuard-based)."
- **Repo:** Tier 1 (mDNS) and Tier 3 (Bridge Relay) only.
- **Action — accept the drift pre-v1.** Mesh-VPN is a deployment-time integration, not core kernel code. Add a roadmap line item for a recommended-deployment doc that names Tailscale/Headscale/etc. as the supported mesh layer. No new package needed; a `docs/deployment/mesh-vpn-discovery.md` would suffice.

### D5 — User-controlled cloud backup tier (paper §2.4 tier 3)

- **Paper:** Tier 3 = "User-controlled cloud backup (via configurable object storage adapter) — user-controlled disaster recovery."
- **Repo:** Generic blob abstractions exist (`packages/foundation/Blobs/`, `foundation-assets-postgres/`, `federation-blob-replication`/IPFS). A user-pointed S3/R2/B2 backup adapter wired specifically as the local-node DR target is missing.
- **Action — fix the implementation.** Ship a `packages/kernel-backup/` package (or under `kernel-runtime/`) that implements scheduled snapshot + upload to a configurable object-storage endpoint, with the user holding the credentials. Net-new code, ~mid-priority.

### D6 — CP wiring at consumer blocks (paper §2.2 + §6.3)

- **Paper:** Resource reservations and financial transactions are CP and require a Flease lease before write.
- **Repo:** `packages/kernel-lease` ships; `packages/blocks-scheduling/ScheduleViewBlock.razor` and `packages/kernel-ledger/PostingEngine.cs` do not visibly take a lease before their respective writes.
- **Action — fix the implementation.** Wire `ILeaseCoordinator` into `PostingEngine` (financial transactions) and into the slot-reservation path in `blocks-scheduling`. This is the highest-leverage drift in the audit because the paper explicitly says "double-booking is worse than unavailability" — and the codebase ships the coordinator without using it for the named CP cases.

### D7 — Expand-contract operational guidance (paper §7.1)

- **Paper:** Names the expand-contract pattern as the mandatory pattern for synced-record schema changes.
- **Repo:** Mechanism (lenses, epochs) is present; developer-facing checklist / template / lint is not.
- **Action — fix the documentation.** Add `docs/contributor/expand-contract-checklist.md` referenced from each plugin's `README.md`. Optionally an analyzer in `packages/analyzers/` that flags non-additive schema changes without a paired lens.

### D8 — Rehydration orchestrator (paper §8.3)

- **Paper:** Names a four-step rehydration recipe (load snapshot → epoch/schema check → replay events → apply pending upcasters).
- **Repo:** Logic is implicit and split across `kernel-event-bus` and `kernel-schema-registry`; no single named orchestrator.
- **Action — fix the implementation.** Introduce `RehydrationOrchestrator` (or document the implicit pipeline in a code comment + README anchor on the existing classes). Low-risk refactor.

### D9 — Status-bar three-indicator triad (paper §13.2)

- **Paper:** "Three always-visible status indicators (node health, link status, data freshness) appear in the status bar."
- **Repo:** Status-bar shell exists; the explicit triad bound to live kernel state is not visibly wired in `apps/kitchen-sink/` or `accelerators/anchor/MainPage.xaml`.
- **Action — fix the implementation.** Materialize the triad as a shared shell component in `packages/ui-core/components/`, consumed by both `kitchen-sink` and `anchor`. Aligns with the existing global-UX wave (`waves/global-ux/`).

### D10 — Bulk conflict resolution wizard (paper §5.2 + §13.3)

- **Paper:** Names "bulk conflict resolution wizard" as a Blocks-tier component, twice.
- **Repo:** No `packages/blocks-conflict-resolution` (or equivalent). `packages/foundation-localfirst/SyncConflict.cs` provides the data structure.
- **Action — fix the implementation.** Net-new package `packages/blocks-conflict-resolution/` shipping a wizard that consumes `IQuarantineQueue` + `SyncConflict`. Probably a mid-Wave deliverable; small surface.

### D11 — Per-class staleness thresholds policy (paper §13.2 table)

- **Paper:** Names exact staleness thresholds: 5min / 10min / 15min / 24h for each CP class.
- **Repo:** No kernel-shipped policy encoding those numbers; each block sets thresholds ad hoc (or doesn't).
- **Action — fix the implementation.** Ship a `StalenessPolicy` registry in `packages/kernel-runtime/` (or alongside `kernel-buckets`) that defaults to the paper's thresholds and lets plugins override per record class. Refer the docs at the registry to paper §13.2.

### D12 — L3 fault-injection CI matrix (paper §15.1)

- **Paper:** L3 = "Fault injection in CI — Partition, packet loss, node crash."
- **Repo:** Some scenarios live as `Fact(Skip)` placeholders (per `waves/cleanup/2026-04-26-followup-debt-audit.md` §1); no named CI job runs them as a matrix.
- **Action — fix the CI.** Add a `fault-injection.yml` workflow that exercises the §15.2 mandatory-scenario list (partition + reconnect, schema migration on offline node, Flease edge cases, security extraction, ledger sum-to-zero). Initially nightly; promote to PR gate before v1.

### D13 — L4 deterministic simulation harness (paper §15.1)

- **Paper:** L4 = "Deterministic simulation — Mixed-version nodes, epoch transitions, lease edge cases." (FoundationDB-style.)
- **Repo:** Not present.
- **Action — accept the drift pre-v1, with a roadmap line.** Deterministic simulation is a heavy investment; add to `_shared/product/roadmap-tracker.md` as a v1.1 hardening goal once the kernel surface is frozen.

### D14 — Bridge SaaS ciphertext-only invariant (paper §17.2.1 "Hosted relay as a SaaS node")

- **Paper:** "The relay stores ciphertext only; role keys remain on end-user devices so the operator of the hosted relay cannot read team data."
- **Repo:** Bridge SaaS mode currently uses Postgres-backed authoritative storage. ADR 0031 acknowledges this and elects "Zone-C Hybrid" as Bridge's explicit framing — *commercially*, this is fine; *paper-conformance-wise*, Bridge.SaaS does not match §17.2.1. Bridge.Relay does match, separately.
- **Action — accept the drift with rationale.** Bridge SaaS is intentionally a different product shape than Bridge Relay; ADR 0031 establishes a per-tenant data-plane isolation model that is the commercial answer to §17.2.1's invariant for buyers who explicitly want vendor-managed data. Document on `accelerators/bridge/README.md` that "Bridge SaaS = ADR 0031 Hybrid; Bridge Relay = paper §17.2.1 ciphertext-only" so the distinction is explicit at every entry point.

### Other MISSING items (no named drift action — track only)

| ID | Paper element | Recommended action |
|---|---|---|
| T5 | Decentralized archival tier | Roadmap entry, enterprise-tier; no immediate work. |
| Z5 | L5 chaos in staging | Roadmap entry, post-v1. |
| E3 | Air-gap deployment mode | Roadmap entry, enterprise documentation. |

---

## 3. Cross-cutting recommendations

1. **Bump the version reference.** CLAUDE.md still says "Version 10.0, April 2026"; the paper file is at v12.0. Update CLAUDE.md and any other consumer of the version string in one focused PR.

2. **Land D6 first.** The CP-block-wiring drift (lease coordinator not consumed by financial / scheduling blocks) is the only drift that breaks a paper-named correctness property at runtime. Everything else is shape, scope, or operational tooling. Single high-leverage code change.

3. **Bundle D7 + D9 + D10 + D11 into a "paper-fidelity" mini-wave.** All four are small UX/contributor-facing artifacts that close the gap between "the paper names this" and "a developer/end-user encounters it in the product."

4. **Park D2, D5, D12, D13 on the roadmap.** Each is real but not blocking and best fit for a deliberate post-v1 cycle.

5. **Document the framework-agnostic-tier intentional drift (D3).** The choice to ship contracts + per-framework adapters (rather than Lit-based runtime FA components) is defensible but invisible. A one-line CLAUDE.md edit and a README pointer prevents future agents from wasting cycles searching for a layer that does not exist by design.

6. **Keep this audit on a 2-month cadence.** The paper is at v12 in five weeks since v10 — drift accumulates quickly when the spec is iterated. A rolling re-audit every ~8 weeks against the latest paper version keeps the gap visible.

---

## 4. Audit hygiene

- **No code or CI changes in this PR.** Diff = this single new file.
- **No `.wolf/`-tracked findings logged** because no bugs were fixed and no preferences/learnings were inferred (read-only audit).
- **References checked but not modified:** `docs/adrs/0026`, `0027`, `0028`, `0029`, `0031`, `0032`; `_shared/product/paper-alignment-plan.md`; `icm/07_review/output/paper-alignment-audit-2026-04-22.md` and successors.
