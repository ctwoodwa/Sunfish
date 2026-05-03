# Inverting the SaaS Paradigm: A Local-Node Architecture for Collaborative Software

**Version 12.0 — April 2026**

*A vendor-neutral architecture specification for building enterprise-grade collaborative software where the workstation is the server, the cloud is a peer, and the user owns their data unconditionally.*

> **Note:** This is the foundational research paper that Sunfish implements. The rest of the repository — kernel, plugins, UI tiers, compat packages, ICM pipeline artifacts — flows from the decisions and architecture described here. If you're new to the repo, read this paper first; it is the "why" that makes the codebase's structure legible.
>
> Published under Creative Commons CC-BY 4.0. Feedback, critiques, and implementation reports are welcomed.

---

## Abstract

This paper describes a distributed systems architecture for collaborative software that inverts the conventional SaaS deployment model. Rather than centralizing data and compute on vendor-controlled infrastructure, every user workstation runs a complete, self-contained application node. Collaboration occurs through peer-to-peer state synchronization using conflict-free replicated data types and a lightweight sync daemon. Cloud infrastructure, where present, acts as a peer among equals rather than an authority.

The architecture is grounded in four decades of distributed systems research — leaderless replication, CAP theorem positioning, event sourcing, unbundled databases, double-entry ledgers, and CRDT compaction theory — and validated against production systems that implement each subsystem independently. The goal is a system that behaves like a cloud application during normal operation, functions fully offline without degradation, and remains operational indefinitely regardless of any vendor relationship.

---

## 1. Executive Summary

Modern SaaS bundles three desirable properties — real-time collaboration, multi-device access, and zero-maintenance infrastructure — with three properties users would prefer to avoid: data residency on vendor infrastructure, pricing at the vendor's discretion, and service continuity contingent on the vendor's survival. Users accept this bundle because the collaborative features have historically been inseparable from the centralized infrastructure that enables them.

This paper demonstrates that the separation is now technically achievable. The architectural thesis is simple: invert the primary/secondary relationship between local and cloud.

**Conventional SaaS:** Cloud database is primary → local device caches and renders.

**Local-Node Architecture:** Local node is primary → cloud relay is an optional sync peer.

Every workstation runs a complete containerized application stack. The user's authoritative data copy lives in a local encrypted database. When peers are reachable, nodes exchange state through a gossip protocol. When no peers are reachable, the node operates with full fidelity. There is no degraded mode because there is no dependency on any remote service for core function.

---

## 2. Theoretical Foundations

### 2.1 Reliability, Scalability, Maintainability

Kleppmann's *Designing Data-Intensive Applications* defines the three axes of system quality as reliability (doing the right thing when things go wrong), scalability (handling growth gracefully), and maintainability (enabling future engineers to work productively). Cloud-first SaaS achieves all three by centralizing control. The local-node model achieves the same properties through different mechanisms:

- **Reliability** through eliminating the single point of failure — a node that loses connectivity continues operating at full fidelity.
- **Scalability** through distributing compute to the edge — each workstation handles its own query load; the central relay handles only coordination messages, not application traffic.
- **Maintainability** through strict interface contracts between subsystems, enabling each to evolve independently.

### 2.2 The CAP Theorem and Deliberate Consistency Choices

A distributed system can guarantee at most two of Consistency, Availability, and Partition tolerance. Network partitions are physical reality, so the design choice is whether to prioritize Consistency (CP) or Availability (AP) during a partition. This architecture makes an explicit, per-record-class choice:

| Record Class | CAP Position | Mechanism | Rationale |
|---|---|---|---|
| Documents, notes, task descriptions | AP | CRDT (text/map/list) | Divergence tolerable; merge deterministic |
| Team membership, permissions | AP with deferred merge | CRDT + role attestation | Identity facts converge after reconnect |
| Resource reservations, scheduled slots | CP | Distributed lease | Double-booking worse than unavailability |
| Financial transactions | CP | Distributed lease + ledger | Audit integrity requires strict ordering |
| Audit and governance records | CP + append-only | Distributed lease + event log | Immutability is first-class requirement |

Nodes operating offline are in AP mode. The circuit breaker quarantine queue validates AP writes against CP constraints upon reconnect before promoting them to the shared log.

### 2.3 Leaderless Replication and Quorum Coordination

For AP-class data, this architecture operates as a **leaderless replicated system**: any node can accept writes, and convergence is guaranteed by CRDT merge semantics. This eliminates leader failover problems for the majority of application data.

For CP-class data, a minimal quorum layer (modeled on Flease — Failure-aware Lease management) provides distributed lease coordination without a dedicated leader process. Nodes negotiate leases through message-passing; a lease is granted only when a quorum of reachable peers agree. This is the narrowest possible CP footprint: only records that genuinely require coordination bear that cost.

For teams with fewer members than quorum requires, CP-class operations require either a managed relay node as an additional quorum participant, or a configuration downgrade to AP with conflict detection and post-hoc resolution. Both options are surfaced at installation time.

### 2.4 Unbundled Databases and the Five-Layer Storage Architecture

Rather than relying on a single database system, the architecture composes five specialized storage tiers:

1. **Local encrypted database** — primary operational store; all reads and writes hit this layer first.
2. **CRDT and event log** — append-only log of all CRDT operations and domain events; source of truth for sync and audit.
3. **User-controlled cloud backup** (via configurable object storage adapter) — user-controlled disaster recovery.
4. **Content-addressed distribution** (optional) — integrity-verified asset distribution and deduplication.
5. **Decentralized archival** (optional, enterprise tier) — cryptographic proof-of-storage for regulated industries.

Tiers 4 and 5 are opt-in. The core system is fully functional on tiers 1–3 alone.

### 2.5 Event Sourcing and Append-Only Correctness

The CRDT operation log, domain event log, and circuit breaker quarantine queue are designed as **event-sourced, append-only structures**. An event-sourced log never modifies past records — it only appends new events. Current aggregate state is derived by replaying the event log from a known snapshot.

Benefits: corruption resistance (a partially written entry does not corrupt prior entries), point-in-time recovery, and semantic soundness of the quarantine queue (quarantined writes are held pending validation and either promoted or explicitly rejected with a recorded reason).

---

## 3. Reframing the SaaS Contract

The SaaS business model bundles desirable properties (collaboration, multi-device access, maintenance-free operation) with undesirable ones (vendor data custody, vendor-controlled pricing, vendor-dependent continuity). Users accept this because the desirable properties have historically required centralized infrastructure. That dependency is now removable.

The inversion: instead of the cloud being primary and the local device being a read-through cache, the local node is primary and the cloud is a sync peer. The local node holds the authoritative copy. Cloud infrastructure, where it exists, is one peer among many — not the authority.

The practical consequence: software that behaves like a cloud application when online, functions perfectly when offline, and continues to operate after any vendor relationship ends. The user's data is theirs in the strongest sense — not because of a contract, but because of where the bits live.

---

## 4. Why Modern Hardware Makes This Viable

A current mid-range workstation (16GB RAM, 8-core CPU, 500GB NVMe) has more compute than the average cloud VM serving a ten-user team five years ago. A complete three-service containerized stack — API server, sync daemon, local database — runs comfortably under 1GB of RAM at idle.

The cold-start problem is real: a multi-service container stack can take 15–30 seconds from cold. "Installed software" users expect sub-second perceived launch. The solution is to run the container stack as a **persistent background service** registered with the OS service manager (systemd on Linux, launchd on macOS, Windows Service on Windows), starting at login and running quietly at idle. The application shell connects to the already-running local stack; perceived launch time is the shell's initialization time, typically under two seconds. This is architecturally identical to how existing local agent software (VPN clients, password managers, container runtimes) operates.

Storage management follows the model established by cloud storage tiering: active working data lives fully local; archival and reference data is stored in user-controlled backup and fetched on demand; content-addressed caching provides deduplication across peers.

---

## 5. The Local-Node Kernel Architecture

### 5.1 Kernel and Plugin Model

The local node is organized as a **microkernel monolith**: a small, stable core responsible for infrastructure concerns, with well-defined extension points that domain plugins implement. This is the Linux modular kernel model applied to application software — a solid core plus loadable modules under strict contracts, all running in-process to avoid inter-process communication overhead.

**Kernel responsibilities (stable, versioned slowly):**

- Node lifecycle and process orchestration
- Sync daemon protocol, gossip anti-entropy, and distributed lease coordination
- CRDT engine abstraction, event log, snapshots, and compaction
- Schema migration infrastructure (expand/contract, lenses, epochs)
- Security primitives (encryption, key management, role attestation)
- Partial/selective sync engine (bucket definitions, stream subscriptions)
- Plugin discovery, loading, versioning, and lifecycle management

**Plugin responsibilities (domain-specific, versioned independently):**

- Domain aggregates, commands, and events (e.g., project management, scheduling, finance)
- Domain-specific CRDT document types and stream subscriptions
- Read-model projections and materialized views
- UI surface definitions (see Section 5.2)

This separation gives three practical benefits:

1. **Independent evolution:** Domain plugins can add aggregates, events, and UI surfaces without touching kernel code.
2. **Tenant-specific bundles:** Deployments can enable or disable modules based on organizational need.
3. **Safe extensibility:** Third parties and enterprise customers can develop their own modules against the stable kernel contract without forking the core.

### 5.2 The UI Kernel

Parallel to the data/sync kernel, a **UI kernel** provides a stable presentation foundation that all domain plugin interfaces share. This layer is organized in four tiers:

**Foundation** — Design tokens, utilities, and primitives shared across all modules:
- Semantic tokens encoding node states (sync-healthy, stale, offline, conflict-pending, quarantine) into visual vocabulary
- Typography, spacing, elevation, and layout primitives
- Icon and iconography sets covering local-first status metaphors

**Framework-Agnostic Component Layer** — Local-first-aware primitive components:
- Status indicators bound to kernel sync state (node health, link status, data freshness)
- Optimistic-write button components that reflect pending/confirmed/failed states
- Conflict list component subscribed to the kernel's conflict inbox
- Freshness badge component bound to per-record staleness metadata

**Blocks and Modules** — Higher-level, domain-aligned building blocks:
- Each domain plugin exposes its UI through a corresponding set of blocks
- Blocks encapsulate data contracts (what they need from the kernel plugin API) and interaction patterns (how they handle offline, stale, conflict, and AP/CP states)
- Examples: task board, resource allocation scheduler, ledger posting grid, account statement viewer, bulk conflict resolution wizard

**Compatibility and Adapter Layer** — Optional packages that mirror the public API shape of other component libraries or external systems:
- Allows prototyping against the local-first component contracts and switching rendering engines with minimal code changes
- Applies equally to UI adapters (alternative component libraries) and integration adapters (alternative auth providers, storage backends)

The UI kernel and data/sync kernel are aligned: every kernel state visible to the data layer has a corresponding token and component in the UI layer. A developer implementing a new domain plugin produces both a backend plugin (aggregates, events, projections) and a matching set of blocks that consume kernel-standard components.

### 5.3 Extension Point Contracts

Kernel extension points are defined as stable, versioned interfaces. Plugins implement these interfaces; the kernel discovers and loads plugins at startup through a convention-based registry. Required extension points:

- `ILocalNodePlugin` — registration, lifecycle, dependency declaration
- `IStreamDefinition` — declares CRDT streams, event types, and sync bucket contributions
- `IProjectionBuilder` — registers read-model projections rebuilt from the event log
- `ISchemaVersion` — declares supported schema versions and provides upcasters for older versions
- `IUiBlockManifest` — registers the plugin's blocks and modules with the UI kernel

Compatibility adapter plugins implement the same interfaces as their target and expose a wrapper that routes through to the alternative provider. The kernel cannot distinguish a first-party plugin from a compatibility adapter — both satisfy the same contract.

---

## 6. The Sync Architecture

### 6.1 Gossip Anti-Entropy

Peer discovery and state synchronization use a gossip protocol. Each node maintains a membership list of known peers with associated vector clocks. Periodically (default: 30 seconds), each node selects two random peers and exchanges a delta — the operations each node holds that the other lacks.

Peer discovery follows a tiered approach:
1. **mDNS** — zero-configuration, LAN-only; peers on the same network segment discover each other automatically.
2. **Mesh VPN** (e.g., WireGuard-based) — peers across networks connect with automatic NAT traversal; no port forwarding required.
3. **Managed relay** (optional) — a lightweight relay for teams where direct peer connectivity is not viable.

### 6.2 Sync Daemon Protocol

The sync daemon runs as a separate process from the application container, communicating via a Unix socket. This allows it to continue operating while the application restarts or updates.

**Handshake sequence:**

```
1. HELLO        {node_id, schema_version, supported_versions[]}
2. CAPABILITY_NEG {crdt_streams[], cp_leases[], bucket_subscriptions[]}
3. ACK          {granted_subscriptions[]}
4. DELTA_STREAM (continuous)
5. GOSSIP_PING  (every 30s)
```

**Data minimization invariant:** The sync daemon enforces subscription filtering at the stream level, not the application layer. A node receives CRDT operations and events only for streams it is subscribed to. Subscription eligibility is determined by role attestation during capability negotiation. A node that lacks the required attestation never receives the operations — receiving data and hiding it in the UI is not a security control.

### 6.3 Distributed Lease Coordination (CP Mode)

For CP-class records, a node must acquire a distributed lease before writing. The lease is granted when a quorum of peers acknowledge the request. If quorum is unreachable, the write is blocked and the UI surfaces a clear indicator. Leases expire automatically; a node that goes offline releases its lease at expiry.

Default lease duration: 30 seconds. Default quorum: ceil(N/2) + 1.

---

## 7. Schema Migration

Schema migration is the hardest operational problem in a local-node architecture. Nodes update independently; a team may run versions spread across two or three releases simultaneously.

### 7.1 Expand-Contract Pattern

All schema modifications affecting synced record types follow the **expand-contract** (parallel change) pattern:

- **Expand phase:** New fields are additive and optional. The application dual-writes to both old and new fields. Older nodes ignore unknown fields; newer nodes prefer new fields and fall back to old ones. This phase remains active for at least one major version release cycle.
- **Contract phase:** Once all active peers have moved past the compatibility window, old fields are removed. This is a breaking change requiring a **schema epoch bump** — a version gate that rejects sync connections from nodes below the minimum supported epoch.

### 7.2 Event Versioning and Upcasters

Events are immutable. Schema evolution uses explicit versioning:

- Additive changes (new optional fields, new event variants) are handled in-place.
- Non-additive changes introduce new event types (e.g., `RecordUpdatedV2`), leaving old types intact.
- An **upcaster layer** on the read path transforms older event versions into the current in-memory shape. Upcasters are pure functions.

**Long-term maintenance:** Upcaster chains accumulate over time and become brittle. This architecture treats upcasters as a short- to medium-term tool and mandates **periodic stream compaction**: a background copy-transform job replays the original stream, applies all lenses and upcasters, and writes a compact current-version stream. Old upcasters are retired after compaction; the old stream is archived for deep audit.

### 7.3 Bidirectional Schema Lenses

For structural changes not handled by additive upcasters, the architecture uses **bidirectional lenses**: declarative, composable transformation functions between schema versions stored in the CRDT document. Lenses form a version graph; migrations between distant versions traverse the shortest path. This generalizes upcasters and handles field renames, type changes, and structural reorganizations.

### 7.4 Epoch Coordination and Copy-Transform Migration

For truly breaking changes, the architecture uses **schema epochs** coordinated by distributed lease quorum:

1. A new epoch is announced and agreed by quorum.
2. A background copy-transform job reads the existing log, applies lenses and upcasters, and writes to a new epoch stream.
3. Nodes cut over to the new epoch as they upgrade.
4. Once all active nodes have cut over, the old epoch is marked read-only.

Nodes returning from long offline periods that cannot interpret the current epoch download a fresh snapshot before resuming incremental sync.

---

## 8. Snapshots and Rehydration

### 8.1 Purpose and Source of Truth

Snapshots exist to avoid replaying thousands of events on every aggregate load. The source of truth remains the ordered event log; snapshots are a performance optimization only.

### 8.2 Snapshot Format

```
{
  aggregate_id:      string
  epoch_id:          string
  schema_version:    string
  last_event_seq:    uint64
  snapshot_payload:  bytes (serialized state)
  created_at:        timestamp
}
```

Snapshots are stored separately from the main event log to keep domain history clean. They can be safely deleted and regenerated without affecting correctness.

### 8.3 Rehydration

1. Load most recent snapshot for the aggregate.
2. Verify it matches current epoch and schema; if not, discard and regenerate.
3. Replay events after `last_event_seq` from the log.
4. Apply any pending upcasters to events from older schema versions.

### 8.4 Interaction with Schema Migration

Snapshots are epoch- and schema-scoped. After a breaking migration, old snapshots are discarded; the system rehydrates from the older snapshot, applies lenses to reach the new schema shape, and writes a new snapshot tagged with the current epoch and schema version.

---

## 9. CRDT Growth and Garbage Collection

CRDT documents — particularly rich-text and list structures — grow monotonically. Every insert and delete is represented in internal state; tombstones and historical operations accumulate. Long-lived, high-churn documents can grow large even when visible content is modest.

Three mitigation strategies are used in combination:

**1. Library-level compaction**
Modern CRDT libraries perform internal garbage collection and use compact binary encodings that keep document growth bounded under normal usage. Library selection should treat compaction behavior as a first-class evaluation criterion alongside correctness and performance.

**2. Application-level document sharding**
Large logical documents are split into sub-documents under a map key. Retiring or archiving a section becomes a key deletion, allowing the CRDT engine to garbage-collect that section's content without affecting the rest of the document.

**3. Periodic shallow snapshots**
For extreme cases (programmatically generated content, high-churn logs), the system periodically creates a shallow snapshot of the CRDT state and discards old history. This relies on recent version vectors plus application-level invariants to preserve mergeability for active peers. Shallow snapshots are reserved for well-understood document types where long-term mergeability is less critical than bounded storage.

The default policy is conservative: full history is retained, relying on library-level compaction. Application-level purging and shallow snapshots are opt-in per document type.

---

## 10. Partial and Selective Sync

### 10.1 Why Full Replication Fails

Full replication to every node becomes a storage problem (multi-gigabyte local databases) or a security problem (nodes holding data they are not authorized to use, protected only by application-layer access control) at any meaningful scale.

### 10.2 Declarative Sync Buckets

**Sync buckets** are named, declaratively specified subsets of the team dataset associated with role membership:

```yaml
buckets:
  - name: team_core
    record_types: [projects, tasks, members, comments]
    filter: record.team_id = peer.team_id
    replication: eager
    required_attestation: team_member

  - name: financial_records
    record_types: [invoices, payments, budgets]
    filter: record.team_id = peer.team_id
    replication: eager
    required_attestation: financial_role

  - name: archived_projects
    record_types: [projects, tasks]
    filter: project.archived = true
    replication: lazy
    required_attestation: team_member
    max_local_age_days: 90
```

Bucket eligibility is evaluated at capability negotiation. The sync daemon constructs a minimal subscription set from peer attestations. Non-eligible nodes never receive bucket events.

### 10.3 Lazy Fetch and Storage Budgets

Lazy-replicated buckets use demand-driven fetch. Records are represented locally as stubs (identifier, metadata, last-modified timestamp, content hash). On access, stubs trigger full-content fetch from peers or backup.

Nodes enforce a configurable local storage budget (default: 10GB). Near the limit, least-recently-used records in lazy buckets are evicted; stubs are retained. Content hashes verify re-fetched records.

---

## 11. Security Architecture

### 11.1 Threat Model

Distributing data to endpoints does not eliminate the honeypot problem — it distributes it to the weakest endpoint. A cloud database is a single high-value target behind enterprise controls. A fleet of workstations is a larger attack surface with heterogeneous posture. Defense-in-depth is required.

### 11.2 Four Defensive Layers

**Layer 1 — Encryption at rest:** All local databases are encrypted (e.g., SQLCipher). Keys are derived from user credentials (Argon2id) and stored in OS-native keystores. Physical storage extraction without credentials yields no plaintext.

**Layer 2 — Field-level encryption:** Records in high-sensitivity buckets (financial, PII, health) are encrypted at the field level using per-role symmetric keys. Role keys are generated by the team administrator, wrapped with each member's public key, and distributed as special administrative events in the log.

**Layer 3 — Stream-level data minimization:** The sync daemon enforces subscription filtering before any data leaves the node. Non-subscribed nodes never receive events regardless of application configuration.

**Layer 4 — Circuit breaker and quarantine:** Offline writes are quarantined pending validation against current team state and policy. Quarantined writes are held (not discarded), reviewed, and either promoted or explicitly rejected with a recorded reason.

### 11.3 Role Attestation vs. Key Distribution

Role attestations (cryptographically signed claims) prove role membership but do not generate encryption keys. The key management flow is separate:

1. Administrator generates per-role symmetric keys.
2. Keys are wrapped with each qualifying member's public key (asymmetric encryption).
3. Wrapped key bundles are distributed as administrative events in the log.
4. Each node decrypts its role key bundle using its private key and stores keys in the OS keystore.
5. Attestations are presented during sync capability negotiation to prove eligibility; the sync daemon's subscription decision is independent of key possession.

Key rotation: the administrator generates new role keys, re-wraps for current authorized members, and publishes new bundles. Nodes no longer authorized do not receive new bundles and cannot decrypt future records.

---

## 12. Ledger Mechanics

### 12.1 Double-Entry Ledger as a First-Class Subsystem

Financial and CP-class value records are modeled as a **double-entry ledger** — not as mutable balance fields. Every financial change is represented as immutable posting events. Invariants:

- Each transaction produces at least two postings.
- The sum of all posting amounts per transaction is always zero.
- Postings are immutable; corrections use compensating entries.

### 12.2 Posting Engine and Idempotency

A **posting engine** converts domain events into ledger postings under distributed lease coordination. Events carry idempotency keys; the engine guarantees that processing the same domain event multiple times produces at most one set of ledger postings.

### 12.3 CQRS Read Models

Querying balances from the raw posting stream is impractical at scale. The architecture uses a CQRS write/read split:

- **Write side:** Immutable posting event stream (source of truth).
- **Read side:** Materialized projections (balance tables, statements, aging reports) updated asynchronously from the event stream.

Projections are rebuilt from the event stream if needed. Business rule aggregates never depend on projections for critical decisions; they rely solely on the event-sourced write side.

### 12.4 Closing the Books and Period Snapshots

At period close, the projection engine computes rollup snapshots (account balances, P&L summaries) stored as closing events. Subsequent postings affecting closed periods are directed to adjustment accounts in the next open period. These rollups act as ledger-specific snapshots: fast historical queries without replaying full posting history.

---

## 13. UX Design Philosophy

### 13.1 The Complexity Hiding Standard

The architecture's success as a user-facing product is measured by how invisible its distributed nature is. The standard: **a non-technical user should be unable to determine, from normal use, whether the application is local-first or cloud-first.** The only visible difference should be that it works when internet is unavailable.

### 13.2 AP/CP Visibility

AP-class data uses **optimistic UI**: writes are applied locally and synced asynchronously. CP-class data is constrained by freshness requirements:

| Data Class | Staleness Threshold | UX Treatment |
|---|---|---|
| Resource availability | 5 minutes | Amber indicator; booking blocked if offline |
| Financial balances | 15 minutes | "As of [timestamp]" label; writes require online |
| Scheduled appointments | 10 minutes | Calendar freshness badge; conflicts surfaced on reconnect |
| Team membership | 24 hours | Silent; surfaced only at role-dependent action |

Three always-visible status indicators (node health, link status, data freshness) appear in the status bar: non-intrusive under normal conditions, informative under degraded ones.

### 13.3 Bulk Conflict Resolution

Conflicts are grouped by record type and cause. Predefined auto-resolution rules (configurable per record type) handle the majority automatically. "Resolve all similar" affordances speed manual review. All decisions are logged as events for audit.

### 13.4 Multi-Device Onboarding

Adding a new node to a team requires three steps:

1. **Install** — the application installs the container runtime and stack silently.
2. **Authenticate** — the user scans a QR code from an existing team member's device, transferring the role attestation bundle and initial CRDT snapshot.
3. **Sync** — the new node begins gossip; initial sync of eager buckets completes in the background.

---

## 14. The Non-Technical Trust Gap

### 14.1 Why Architecture Doesn't Sell Itself

The local-node model's value proposition is most compelling for small teams with minimal IT: legal firms, medical practices, architecture studios, consultancies. These are also the organizations least likely to evaluate software on architecture diagrams. Research consistently shows that SMB software adoption barriers are predominantly non-technical: trust, perceived complexity, and uncertainty about support.

### 14.2 Non-Technical Framing

The product narrative for non-technical buyers must avoid infrastructure vocabulary:

- "Your data lives on your computers, in your office."
- "It keeps working when your internet is out."
- "If we disappear, your software keeps running."
- "Setup is one install and scanning a QR code."

### 14.3 Change Management

Successful adoption requires:

- **A champion:** One technically-inclined team member who understands the model and supports colleagues.
- **A comparison:** "Like a cloud app, but running on your own computer."
- **A fallback story:** "You can export everything; it's your data."
- **A support path:** The managed relay offering provides a support contact satisfying the "who do I call" requirement.

---

## 15. Testing Strategy

### 15.1 The Five-Level Testing Pyramid

| Level | Technique | Scenarios |
|---|---|---|
| 1 | Property-based tests | CRDT convergence, idempotency, commutativity, monotonicity |
| 2 | Integration tests (real dependencies) | Sync handshake, data path correctness |
| 3 | Fault injection in CI | Partition, packet loss, node crash |
| 4 | Deterministic simulation | Mixed-version nodes, epoch transitions, lease edge cases |
| 5 | Chaos testing in staging | Unknown failure modes under production-representative load |

CRDT growth tests are also required: stress tests simulate high-churn documents under programmatic edit load to verify that the combination of library compaction and application-level sharding keeps document size within bounds.

### 15.2 Mandatory Scenarios Before First Production Release

**Partition and reconnect:**
- Two nodes diverge offline for 30 days and reconnect → all AP-class data merges correctly, no data loss.
- Three-node team loses quorum for a CP write → write blocked, surfaced to user, not silently dropped.
- Node returns with 1000+ queued operations → anti-entropy completes without timeout.

**Schema migration:**
- Node on schema N-1 syncs with node on schema N → lenses translate operations correctly in both directions.
- Epoch transition while one node is offline → returning node downloads epoch snapshot and resumes correctly.
- "Couch device" (offline for 3+ major versions) → capability negotiation rejects with clear error, user directed to update.

**Flease edge cases:**
- Lease holder goes offline mid-write → lease expires, another node acquires, prior partial write is quarantined.
- Network partition during lease negotiation → both sides correctly identify no-quorum state.

**Security:**
- Node storage extracted without credentials → encryption prevents plaintext access.
- Role key rotated, former member reconnects → cannot decrypt records written after rotation.

**Ledger:**
- Sum-to-zero invariant holds under retries and failures.
- Duplicate domain events produce exactly one set of postings.

---

## 16. IT Governance and Enterprise Deployment

### 16.1 MDM-Compatible Installation

The installation package is designed for silent, policy-driven deployment:

- Installable via standard package managers with no user interaction required.
- Container runtime deployed as a managed dependency.
- Application configuration pre-seeded via MDM configuration profile.
- Software Bill of Materials (SBOM) published with each release for security review.

### 16.2 Managed Endpoint Integration

Deployment documentation includes approved exclusion lists for common endpoint detection and response platforms, network proxy configuration for relay-only environments, and an air-gap deployment mode with an internal update server and internal relay.

### 16.3 BYOD Separation

All team data is stored in a named, policy-configurable path that MDM can target for enterprise wipe. Personal application data (UI preferences, local-only drafts) is stored in a separate, policy-excluded path.

---

## 17. Open Source Strategy and Sustainability

### 17.1 Licensing

This architecture is designed for permissive open-source licensing. The rationale: locally installed software has no license server to protect proprietary features. Rather than treating this as a vulnerability, it is embraced as a strategic choice. The full capability of the software is available to any installer.

The open-source model eliminates the vendor lock-in concern that prevents security-conscious organizations from evaluating the software, creates adoption flywheel effects that proprietary models cannot replicate, and produces durable infrastructure — as demonstrated by the most widely deployed software in existence.

### 17.2 Managed Relay as Sustainable Revenue

The sustainable revenue model is a managed relay service: operationally hardened, SLA-backed relay infrastructure that teams subscribe to for guaranteed peer coordination, NAT traversal, and first-line support. The relay provides no capabilities that self-operators cannot replicate; it provides them with professional reliability and a human support contact.

Infrastructure cost analysis: a single relay node on commodity infrastructure handles approximately 500 concurrent team connections at minimal hosting cost. At a modest per-team subscription fee, the service becomes cash-flow positive well before reaching meaningful scale. Surplus funds core library maintenance and community infrastructure.

#### Hosted relay as a SaaS node

Operating the relay as a full replicated node effectively yields a cloud-hosted, browser-accessible deployment that is indistinguishable from SaaS from the user's perspective. The architecture treats this as one valid deployment among many, not a separate product model.

The key invariant is that the relay stores ciphertext only; role keys remain on end-user devices so the operator of the hosted relay cannot read team data. Because the hosted relay is just another peer in the gossip network, teams can migrate between vendor-hosted and self-hosted relays (or add workstation nodes) without data model translation or lock-in, as the same sync protocol governs all nodes.

### 17.3 Governance

Long-term project governance follows the model of foundation-backed open-source projects: a small core team with a defined decision-making process, funded by organizational sponsors, with clear intellectual property assignment that prevents any single sponsor from controlling the project's direction.

---

## 18. Implementation Roadmap

### Phase 1 — Foundation (Months 1–4): Kernel Core

- Sync daemon protocol specification (sub-document, delivered first — it unblocks all Phase 1 work)
- CRDT engine abstraction and library evaluation with property-based test suite
- Gossip anti-entropy with mDNS peer discovery
- Distributed lease coordination (CP mode)
- Local encrypted database layer
- Role attestation integration for stream subscription
- Circuit breaker quarantine queue (append-only, event-sourced)
- Plugin registry and `ILocalNodePlugin` contract

### Phase 2 — Application Shell (Months 5–8): Node Host and UI Kernel

- Container stack (API server, sync daemon, local database)
- Desktop application shell (Tauri or equivalent)
- OS service manager registration (silent background service)
- QR code onboarding (attestation bundle + CRDT snapshot transfer)
- UI kernel: Foundation tokens (including sync/freshness state tokens), framework-agnostic component layer
- Status bar, conflict list, freshness badge components
- Staleness indicators and optimistic UI framework

### Phase 3 — Domain Plugins and Sync Completeness (Months 9–12)

- Declarative sync bucket engine (YAML format + evaluation)
- Lazy fetch with stub representation and storage budget enforcement
- CRDT GC/sharding strategy and shallow snapshot support
- Bidirectional schema lens integration
- Schema epoch coordination protocol
- Expand-contract migration tooling
- Stream compaction (copy-transform + upcaster retirement)
- Ledger subsystem: posting engine, CQRS projections, closing-the-books rollups
- Domain plugin bundles (projects, scheduling, ledger surface areas)
- Corresponding Blocks and Modules for each domain plugin

### Phase 4 — Enterprise and Distribution (Months 13–18)

- MDM-compatible installer (Intune/Jamf/equivalent)
- SBOM generation in CI
- Air-gap deployment mode
- Managed relay service deployment (multi-region, SLA-backed)
- Optional decentralized archival tier integration
- Compatibility and adapter layer (UI adapters, auth provider adapters)
- Foundation governance application

---

## 19. Analogues and Validation

Each component has production validation in existing systems:

| Component | Production Analogue | What It Proves |
|---|---|---|
| Local container stack as installed software | Docker Desktop, Rancher Desktop, Tailscale | Container runtimes are consumer-installable; silent background service is normal |
| Gossip anti-entropy | Amazon DynamoDB, Apache Cassandra | Leaderless replication works at scale |
| CRDT collaboration | Figma, Linear, Notion | CRDTs are production-ready for collaborative SaaS |
| Distributed lease coordination | Xtreemfs (Flease) | Failure-aware leases work without dedicated coordinators |
| Desktop shell + local server | VS Code + language servers, 1Password local agent | Native shell + local HTTP is a validated pattern |
| Declarative partial sync | PowerSync, ElectricSQL | Sync buckets solve the selective replication problem |
| OSS + managed service revenue | Grafana, GitLab, Nextcloud | OSS core + hosted service is commercially sustainable |
| Schema epoch coordination | DXOS ECHO | Decentralized schema migration is a solved (if hard) problem |
| Bidirectional schema lenses | Ink & Switch Cambria | CRDT + lenses = schema evolution without a coordinator |
| Event-sourced ledger | Multiple FinTech event-sourcing implementations | Double-entry + event log produces auditable, correct financial state |
| CQRS read models | Standard CQRS implementations | Separate read/write paths are necessary for ledger performance at scale |
| CRDT compaction | Yjs (internal GC), Loro (compact encoding + shallow snapshots) | CRDT document growth is manageable with the right library and sharding policy |
| Microkernel monolith | Linux loadable kernel modules, IDE plugin systems | Stable kernel + pluggable modules under strict contracts is proven at scale |

---

## 20. Conclusion

The local-node inversion is not a regression to installed software. It is a redistribution of where infrastructure lives. The collaborative features that made SaaS compelling — real-time sync, multi-device access, seamless onboarding — are preserved. The properties that made SaaS tolerable despite its costs — vendor dependency, data residency, pricing risk — are eliminated.

The enabling technologies are mature and individually production-validated. The theoretical foundations — leaderless replication, event sourcing, CRDT theory, double-entry ledger design, microkernel modularity — are well understood. The remaining work is engineering: assembling known components into a coherent, deployable system with the UX polish that makes distributed complexity invisible to the people who should never have to think about it.

The sync daemon protocol sub-document is the first and most critical deliverable. It defines the peer handshake, capability negotiation, stream subscription filtering, and lease coordination message formats. Everything in Phase 1 is unblocked by it and blocked without it. Once the sync daemon contract is specified, the remainder of the implementation follows a clear, validated path.

This architecture is ready for implementation.

---

*This paper is published under Creative Commons CC-BY 4.0. Feedback, critiques, and implementation reports are welcomed.*


---

## 20. Architecture Selection Framework

Not every software problem is best solved with a local-node architecture. This framework provides a structured method for determining when the local-node model is the right choice, when traditional centralized SaaS is correct, and when a hybrid approach is appropriate.

### 20.1 The Core Question

Before evaluating any other factor, answer one question:

> **Is the primary value of this software derived from the user's own data, or from aggregating data across many users?**

- **User's own data** → local-first is the correct default.
- **Aggregated data across users** → centralized infrastructure is structurally required.

Everything that follows is nuance around that axis.

---

### 20.2 Filter 1: Consistency Requirement (Hard Stop)

Work through these filters in order. The first filter that produces a hard answer terminates the evaluation.

| Question | Answer → Model |
|---|---|
| Does a transaction need to be atomic across multiple users *simultaneously*? | **Centralized only** |
| Is stale data dangerous (payments, inventory, reservations)? | **Centralized only** |
| Does every user need the exact same truth at the exact same millisecond? | **Centralized only** |
| Can users tolerate eventual consistency (minutes to hours for cross-peer sync)? | **Local-first viable** |

If any row returns **Centralized only**, stop. Do not force the local-node architecture onto financial ledgers, seat reservation systems, or real-time trading platforms. The CAP theorem is not a negotiating position.

---

### 20.3 Filter 2: Data Ownership Profile

| Profile | Model |
|---|---|
| User creates data; data describes the user or their work | Local-first |
| Vendor aggregates anonymous user behavior as the product | Centralized |
| Regulatory custodian must hold the authoritative copy | Centralized |
| User owns data but wants *optional* sharing or sync | Local-first + relay |
| Data has value only when pooled (market prices, rankings, recommendations) | Centralized |

---

### 20.4 Filter 3: Connectivity and Operational Environment

| Environment | Model |
|---|---|
| Field workers, air-gapped facilities, rural or mobile-poor connectivity | Local-first mandatory |
| Always-online, browser-only, no install friction tolerated | Traditional SaaS |
| Enterprise with MDM, IT governance, or BYOC storage policy | Local-first node |
| Anonymous public access required, no persistent identity | Traditional website |
| Regulated data residency requirements (GDPR, HIPAA, FedRAMP, ITAR) | Local-first or on-premises |

---

### 20.5 Filter 4: Business Model Alignment

| Situation | Implication |
|---|---|
| Revenue from recurring access to a hosted service | Traditional SaaS viable, but exposed to vendor-lock-in backlash |
| Revenue from support, extensions, or managed relay | Local-first strongly viable |
| Network effects require all users on the same platform | Centralized required |
| Enterprise sales with security review requirements | Local-first node (easier to pass vendor risk review) |
| Open-source sustainability model | Local-first strongly preferred |

---

### 20.6 Filter 5: Team Capability and Timeline

This filter determines *when* and *how*, not *whether*:

| Constraint | Implication |
|---|---|
| Need to ship in under 3 months | Start traditional SaaS; architect for future local-node migration |
| Team has no CRDT or sync experience | Budget 3–6 additional months |
| Existing hosted product with historical data | Hybrid: retain cloud as sync relay, add local node capability incrementally |
| Greenfield project with time to architect correctly | Local-first node from day one |

---

### 20.7 The Three Outcome Zones

Running all five filters produces one of three outcomes:

**Zone A — Local-First Node (this architecture)**

All five filters clear without a hard stop. Applies to:
- Single-tenant or team-scoped productivity and business software
- Offline or regulated operational environments
- Software whose core value exists before any other user joins
- Professional or enterprise users willing to install software

*Representative examples: project management, professional CRM, ERP, design tools, field operations, legal and healthcare records management.*

**Zone B — Traditional SaaS or Website**

Filter 1 or Filter 2 produces a hard "Centralized only" result. Applies to:
- Multi-tenant aggregation as the core value proposition
- Anonymous access with no persistent identity
- Millisecond global consistency as a hard requirement
- Pure content delivery (marketing, news, e-commerce catalog)

*Representative examples: social platforms, trading systems, global search engines, marketing and editorial websites.*

**Zone C — Hybrid**

Filters pass for user-scoped data but fail for specific coordination features. This is the most common real-world outcome for enterprise software. Applies to:
- Local node handles all user-owned data and day-to-day compute
- Cloud relay handles sync, cross-organization collaboration, payments, and compliance reporting
- Traditional web layer handles public-facing surfaces (landing page, documentation, status page)

*Representative examples: Linear, Notion, Obsidian Sync, Figma — all converging on this model.*

---

### 20.8 A Practical Shortcut

If all three of the following are true, the local-node architecture is the right default:

1. The software could function if the vendor disappeared tomorrow, provided the user retains their local data.
2. The software's core value exists before any other user joins — it is not dependent on network effects.
3. An enterprise IT department would prefer that the data never leave their network.

If any answer is **no**, identify which filter captures that constraint and evaluate whether a hybrid or centralized model addresses it, or whether the constraint is a fundamental property of the problem versus an inherited design assumption.
