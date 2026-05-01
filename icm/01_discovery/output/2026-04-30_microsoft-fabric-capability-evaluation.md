# Microsoft Fabric → Sunfish Capability Evaluation

**Stage:** 01_discovery research note
**Date:** 2026-04-30
**Author:** XO (research session)
**Status:** Draft for CO review
**Decision authority:** None — this is evaluative reconnaissance, not an ADR

---

## 1. Executive summary

CO asked: *"What Fabric-like features can we incorporate into the Sunfish platform while still staying true to the local-first mandate?"*

Microsoft Fabric's workload surface decomposes into ~17 distinct capabilities. Mapping each against Sunfish's existing zone framework (Zone A local-first node = Anchor; Zone C hybrid hosted-node = Bridge; Zone B traditional SaaS = explicitly out-of-scope per paper §20.7) produces this verdict table:

| # | Fabric capability | Primary verdict | Trust model | Confidence |
|---|---|---|---|---|
| A.1 | OneLake (unified data lake) | A+C hybrid | Tenant-owned target; Bridge as opt-in storage | Med |
| A.2 | Mirroring (CDC to lake) | A-native | Local-node exporter plugin | High |
| A.3 | Shortcuts (cross-source virtual refs) | C-hosted (cross-tenant) | Federation per ADR 0029 | Med |
| B.4 | Data Factory (pipelines) | A-native (+C) | In-process orchestrator; Bridge per-tenant runner | High |
| B.5 | Synapse Data Engineering (Spark) | A-native (notebooks) / B-only (cluster) | Local DuckDB+notebooks; cluster is Zone B | High |
| B.6 | Synapse Data Science (ML) | A-native (local) / B-only (centralized training) | Local ML.NET; cross-tenant centralized = reject | Med |
| C.7 | Lakehouse query | A-native | DuckDB-as-plugin | High |
| C.8 | Warehouse (T-SQL over lake) | A-native | DuckDB or SQLite as warehouse | High |
| C.9 | Real-Time Intelligence (KQL) | A-native | KQL-style query over event log | Med |
| C.10 | Direct Lake (BI semantic model) | A-native (+C) | Local dashboards over local data; Bridge per-tenant model | Med |
| D.11 | Real-time Hub (event ingest/route) | A-native (+C) | In-process events; Bridge-relayed cross-device | Med |
| D.12 | Data Activator (reactive triggers) | A-native | When-X-then-Y plugin over event log | High |
| E.13 | Power BI (semantic models, dashboards) | A-native (+C) | Local dashboards; Bridge per-tenant (plaintext, opt-in) | High |
| E.14 | Copilot (AI-assisted analytics) | A-native (+C) / B-only (training) | Local LLM; Bridge LLM proxy; centralized training = reject | Med |
| E.15 | Notebooks (collaborative) | A-native (+C) | Polyglot Notebooks + Yjs; Bridge-hosted execution opt-in | High |
| F.16 | Purview (lineage, sensitivity, DLP) | A-native (local) / B-only (cross-tenant) | Schema-registry extends to lineage; central Purview = reject | High |
| F.17 | Fabric Admin (workspace/capacity) | Already-covered | Bridge control plane = per-tenant Fabric Admin analog | High |

**Headline:** 14 of 17 capabilities have a Zone A (local-first plugin) path; 9 also have a Bridge (Zone C) complement when cross-device or hosted serving is wanted; only 4 have a B-only sub-aspect that should be flagged as out-of-scope (cross-tenant Spark cluster, centralized ML training on plaintext, cross-tenant Purview, cross-tenant capacity admin). The local-first mandate does **not** preclude most of Fabric's value — the substrate (kernel-ledger, kernel-sync, kernel-schema-registry, foundation-localfirst, blocks-tax-reporting) is closer to Fabric than the marketing vocabulary suggests.

**Top-3 recommended next intakes** (detail in §7):

1. **`blocks-local-analytics-duckdb`** — DuckDB-embedded plugin covering C.7, C.8, C.10, and most of E.13/E.15. Single biggest unlock; subsumes "DataFrame, OLAP, dashboards over local data."
2. **`blocks-reactive-triggers`** — Data Activator analog (D.12); composes with kernel-ledger event log; biggest functional gap not already on the property-ops roadmap.
3. **`blocks-snapshot-exporter`** — User-controlled-target exporter (Parquet/Delta) for A.1, A.2 outbound; preserves user data ownership; positions Sunfish to interoperate with any external lakehouse the user chooses without Sunfish operating one.

---

## 2. Research question (as posed by CO)

Verbatim: **"What Fabric-like features can we incorporate into the Sunfish platform while still staying true to the local-first mandate?"**

Scope clarifications from CO (collected via AskUserQuestion 2026-04-30):

- **Not changing architecture layering.** The kernel/plugin/UI tier framework stays exactly as it is. The eval is about *adding features that compose with the existing layering*, not restructuring it.
- **Anchor and Bridge are the answer key.** Some Fabric features are Sunfish-native (Zone A in-process plugin); some genuinely require a host (Zone C — that's why Bridge exists per ADR 0031); some overextend into multi-tenant SaaS-only territory (Zone B per paper §20.7) and should be flagged as out-of-scope.
- **Bridge is the designated home for hosted features.** ADR 0031 establishes Bridge as Zone C Hybrid: shared control plane + per-tenant data plane (one `local-node-host` process per tenant) + shared stateless relay tier. Hosted Fabric-like features should target Bridge first.
- **Per-capability menu evaluation.** Each Fabric capability gets its own verdict; no all-or-nothing commitment.
- **Dual interpretation where ambiguous.** Where a Fabric capability could land Zone A (in-process) or Zone C (hosted), evaluate both paths.

---

## 3. Method

### What "Fabric-like" means here

This eval uses Microsoft Fabric's own canonical workload taxonomy (Storage, Data Engineering, Data Science, Data Warehousing, Real-Time Intelligence, Data Factory, Power BI, Data Activator, Copilot, Purview integration, Fabric Admin) as the source for the 17-capability menu. The list is current as of public Microsoft Fabric documentation in late-2025/early-2026; capabilities Microsoft adds later may need addendum evaluation.

This is not a Microsoft endorsement — Fabric is used here as the dominant industry vocabulary for "unified analytics platform" against which to measure Sunfish's capability gaps.

### Zone-fit classification scheme

Each capability is tagged with a primary verdict and (where applicable) trust-model sub-classification:

| Tag | Meaning |
|---|---|
| **A-native** | Implementable as in-process plugin in `apps/local-node-host/` (Anchor or single-tenant Bridge). Local-first preserved. No new architectural commitment beyond `ILocalNodePlugin` (paper §5.3). |
| **C-hosted (ciphertext)** | Runs on Bridge over ciphertext only — operator can't read user data. Maps to ADR 0031's "Relay-only" tenant trust level. |
| **C-hosted (plaintext, opt-in)** | Runs on Bridge over plaintext that a *single tenant* has explicitly authorized hosting. Maps to ADR 0031's "Attested hosted peer" trust level. Tenant retains revocation. |
| **C-hosted (cross-tenant aggregation)** | Requires multiple tenants to authorize cross-tenant aggregation. Federated model — Bridge orchestrates but each tenant remains sovereign. Tracked in ADR 0029 federation. |
| **B-only (reject)** | Fundamentally requires multi-tenant centralized plaintext that tenants can't grant safely. Out of scope per paper §20.7. SaaS-only feature; Sunfish does not pursue. |
| **A+C hybrid** | Genuinely benefits from a split: in-process for individual workloads, hosted for cross-device/cross-user surfaces. |
| **Already-covered** | Existing Sunfish primitive substantially fulfills this; Fabric framing adds vocabulary, not capability. |

### Out of scope

- Microsoft licensing / Fabric capacity pricing / vendor cost analysis
- Power BI service-tier comparisons
- Authorship of full intakes (this eval *recommends* intakes; it does not write them)
- Code samples or implementation sketches beyond plugin shape
- Vendor selection within each plugin family (e.g., DuckDB vs. Apache DataFusion vs. ClickHouse-embedded — that is a Stage-02 architecture decision, not Stage-01 discovery)

---

## 4. Sunfish substrate (one-page recap)

### Local-first kernel — substantively built

| Layer | Package(s) | Function |
|---|---|---|
| Storage | `packages/foundation-localfirst/` | SQLCipher store, Argon2id KDF, OS keystores (macOS Keychain / Windows DPAPI / Linux libsecret) |
| CRDT | `packages/kernel-crdt/` | Yjs binding, ICrdtEngine + convergence tests |
| Ledger | `packages/kernel-ledger/` | Event-sourced posting engine, double-entry CQRS projections |
| Sync | `packages/kernel-sync/` | Gossip daemon (30s tick), VectorClock, mDNS discovery, Unix socket / named pipe transport |
| Lease (CP) | `packages/kernel-lease/` | Flease coordinator, ceil(N/2)+1 quorum |
| Buckets | `packages/kernel-buckets/` | Declarative selective sync, role-attestation eligibility |
| Runtime | `packages/kernel-runtime/` | `ILocalNodePlugin`, `PluginRegistry`, `TeamContext` multi-team scoping |
| Schema | `packages/kernel-schema-registry/` | Bidirectional lenses, upcaster chains, `CompactionScheduler` |
| Security | `packages/kernel-security/` | Ed25519 role attestation, per-role key wrapping |
| Host | `apps/local-node-host/` | Single-process BackgroundService orchestrating per-team kernel scopes |

Existing reporting / scheduling / domain primitives:

- **ADR 0021** — `Sunfish.Foundation.Reporting` contract-and-adapter model: `IPdfExportWriter`, `IXlsxExportWriter`, `IDocxExportWriter`, `IPptxExportWriter`, `ICsvExportWriter`. Default adapters: PDFsharp+MigraDoc (PDF), ClosedXML (XLSX), NPOI (DOCX/PPTX), CsvHelper (CSV). All MIT/Apache-2.0.
- **`packages/blocks-tax-reporting/`** — concrete tax-prep export bundle (depreciation schedule, 1099-NEC, annual exports).
- **ADR 0051** — Quartz behind a shim for monthly statement reconciliation (narrow scope).

### Accelerator zones — paper-aligned

| Accelerator | Zone | Paper § | ADR |
|---|---|---|---|
| `accelerators/anchor/` | A — Local-First Node | §5 (kernel), §13 (UX), §20.7 Zone A | 0032 |
| `accelerators/bridge/` | C — Hybrid | §17.2 (hosted-relay-as-SaaS), §20.7 Zone C | 0031 |

**ADR 0031 Bridge model:**
- **Control plane** (shared): Aspire orchestration, Postgres, signup, billing, support — holds no team data.
- **Data plane** (per-tenant): One `apps/local-node-host` process per tenant, dedicated SQLCipher DB at per-tenant path, dedicated subdomain (`{tenant}.sunfish.example.com`).
- **Relay tier** (shared, stateless): Sync-daemon transport fan-out per team_id.
- **Three trust levels** at signup: *Relay-only* (operator sees ciphertext only — default) / *Attested hosted peer* (operator can decrypt for backup verification — opt-in) / *No hosted peer* (self-host).

### Plaintext-only-on-explicit-authorization invariant

Paper §17.2: *"the relay stores ciphertext only; role keys remain on end-user devices so the operator of the hosted relay cannot read team data."*

This is the load-bearing invariant for every C-hosted verdict in §5: hosted features either (a) operate over ciphertext only, (b) operate over plaintext that a single tenant has explicitly authorized hosting, or (c) federate across tenants where each tenant remains sovereign. Multi-tenant centralized plaintext aggregation (Zone B per paper §20.7) is explicitly out-of-scope.

Paper §5.1 also constrains *how* features land: *"a small, stable core ... with well-defined extension points that domain plugins implement ... all running in-process to avoid inter-process communication overhead."* New features land as `ILocalNodePlugin` implementations, not sidecar processes (per ADR 0028, which rejects sidecars: *"sidecar process is operational complexity; directly contradicts paper §5.1 'all running in-process to avoid IPC overhead.'"*).

---

## 5. Per-capability evaluation

### 5.1 OneLake (unified data lake) — Verdict: A+C hybrid

**What Fabric provides:** A unified namespace over object storage (Delta tables + files), shared across all Fabric workloads. "One copy, many engines."

**Sunfish/Bridge analog:** Sunfish's local node is the source of truth for transactional data (event log + CRDT state). A "OneLake-style" capability would be an **exporter plugin** that writes append-only snapshots to a user-configured object-storage target (S3-compatible / Azure Blob / local filesystem) in an open columnar format (Parquet, optionally Delta). Bridge can offer per-tenant managed object storage as an **opt-in hosted target** for tenants who don't want to operate their own bucket — but the data written there is the tenant's, not a shared lake.

**Pros:** Unlocks interop with any external analytical tool the user trusts (their own Spark cluster, their own DuckDB/Trino instance, their own dbt project). Preserves data ownership: the user picks the target, holds the keys. Doesn't require Sunfish to operate analytical infrastructure.

**Cons:** Schema drift between local CRDT shape and Parquet snapshot needs explicit management (paper §7 lenses help). Snapshot cadence is a new operational concern. If Bridge offers managed storage, encryption-at-rest semantics must be carefully scoped (ciphertext only by default per ADR 0031 Relay-only tier).

**Existing Sunfish coverage:** Snapshots (paper §8) for local rehydration; no exporter to open table format.

**Verdict + confidence:** A+C hybrid, **medium** confidence. Local-side exporter is straightforward; Bridge-managed storage requires a managed-storage product decision that's likely Phase 3+.

---

### 5.2 Mirroring (CDC from operational DB to lake) — Verdict: A-native

**What Fabric provides:** Change-data-capture from operational databases (Cosmos DB, SQL DB, Snowflake) into OneLake as Delta tables, kept current via continuous replication.

**Sunfish/Bridge analog:** Sunfish's event log *is* its change feed — every state mutation is an immutable posting in `kernel-ledger`. A mirroring plugin would project the event log to a user-controlled lake target on a configurable schedule (continuous, hourly, daily). Bridge variant: mirror the **ciphertext** event log to operator-owned archival storage as defense-in-depth, indistinguishable from off-site backup.

**Pros:** Zero new substrate — composes with existing event log. User-controlled target preserves data sovereignty. Bridge ciphertext-mirror is a natural backup product.

**Cons:** Defining the canonical projection shape (per-aggregate Delta tables? per-stream tables? per-team partition?) is a design decision that needs an ADR. CDC continuity (resume-from-offset semantics) extends the kernel snapshot/compaction story.

**Existing Sunfish coverage:** Event log via `kernel-ledger`; snapshot infrastructure (paper §8); no exporter.

**Verdict + confidence:** A-native (with C-hosted ciphertext variant), **high** confidence.

---

### 5.3 Shortcuts (cross-source virtual references) — Verdict: C-hosted (cross-tenant aggregation)

**What Fabric provides:** Virtual references to data in other lakes/sources without copying — "shortcut" appears in your workspace but reads from elsewhere.

**Sunfish/Bridge analog:** The local-first analog is cross-team or cross-tenant data references via federation. Two teams in different tenants can reference each other's data only if both authorize — exactly the model ADR 0029 federation reserves.

**Pros:** Aligned with existing federation roadmap. Preserves per-tenant sovereignty.

**Cons:** Cross-tenant collaboration is explicitly deferred per ADR 0031 §5 ("Cross-tenant collaboration: deferred. Intra-tenant only in v1."). No work to do here until ADR 0029 federation lands.

**Existing Sunfish coverage:** ADR 0029 federation-* packages (deferred).

**Verdict + confidence:** C-hosted (cross-tenant aggregation), **medium** confidence. Track-as-deferred, revisit when ADR 0029 federation reaches design.

---

### 5.4 Data Factory (pipelines / dataflows) — Verdict: A-native (with C complement)

**What Fabric provides:** Visual ETL/ELT pipeline orchestration with hundreds of connectors, scheduled and event-triggered runs.

**Sunfish/Bridge analog:** A general-purpose pipeline orchestrator plugin that runs in-process within `local-node-host`. Composes scheduled jobs (existing Quartz shim per ADR 0051), triggered actions (Data Activator analog from §5.12), and the exporter plugin (§5.1, §5.2). Bridge complement: per-tenant pipeline runner for jobs that need to fire when no user device is online.

**Pros:** Generalizes the narrow-scope Quartz usage (currently monthly statements only) into a first-class plugin. Composes naturally with mirroring (§5.2) and reactive triggers (§5.12). Bridge variant gives users "scheduled jobs that don't require my laptop to be on."

**Cons:** Risk of feature creep into "low-code visual pipeline builder" territory — that's a UI surface, not a kernel feature. Scope tightly to "schedule + run + log" in v1.

**Existing Sunfish coverage:** ADR 0051 Quartz shim (narrow scope: monthly statement reconciliation only).

**Verdict + confidence:** A-native (with C-hosted plaintext complement when run on Bridge), **high** confidence. Likely a high-priority intake.

---

### 5.5 Synapse Data Engineering (Spark notebooks, lakehouse engineering) — Verdict: A-native (notebooks) / B-only (Spark cluster)

**What Fabric provides:** Spark-based distributed compute over OneLake, with collaborative notebooks for engineering large-scale transformations.

**Sunfish/Bridge analog:** Spark itself is over-spec for a local node — it assumes a cluster. The local-first analog is **Polyglot Notebooks / .NET Interactive** running queries against local DuckDB or local SQLite (covers most "small data" engineering needs — and a single property-management tenant's data is, by Fabric standards, very small data). Spark cluster for cross-tenant aggregation is **Zone B** — Sunfish doesn't pursue.

**Pros:** Notebooks-against-local-data covers the realistic use case for Sunfish tenants. Composes with §5.7 local OLAP plugin.

**Cons:** "We don't run Spark" may surprise enterprise buyers used to Fabric framing. Mitigation: position as "we run the queries you'd run in Fabric, locally — without needing a cluster you don't actually need."

**Existing Sunfish coverage:** None.

**Verdict + confidence:** A-native for notebooks; B-only (reject) for cluster, **high** confidence on the split.

---

### 5.6 Synapse Data Science (ML notebooks, MLflow, model deployment) — Verdict: A-native (local) / B-only (centralized training)

**What Fabric provides:** Distributed ML training, experiment tracking via MLflow, model registry, and inference deployment.

**Sunfish/Bridge analog:** Local model training over local data using ML.NET or ONNX runtime, executed inside notebooks (§5.15) or batch pipelines (§5.4). Cross-tenant federated learning would require explicit per-tenant authorization (rare; track as future). Centralized training on plaintext aggregated across tenants is **Zone B** — direct E2EE violation.

**Pros:** Local model training preserves training-data privacy by construction (data never leaves the device). Strong differentiator vs. Fabric for regulated workloads.

**Cons:** Local training has hardware ceiling — can't train large transformer models on a laptop. Acceptable: most LOB tenant ML needs (anomaly detection, simple classifiers, time-series forecasting) fit local hardware comfortably.

**Existing Sunfish coverage:** None.

**Verdict + confidence:** A-native for local ML; B-only (reject) for cross-tenant centralized training, **medium** confidence (depends on actual tenant ML use cases, which Phase-2 hasn't surfaced yet).

---

### 5.7 Lakehouse query (Spark + SQL over Delta tables) — Verdict: A-native

**What Fabric provides:** Spark and T-SQL query interfaces over Delta tables in OneLake.

**Sunfish/Bridge analog:** **DuckDB embedded as an `ILocalNodePlugin`** — runs in-process, queries local SQLite tables and local Parquet snapshots (the output of §5.2 mirroring). Single largest unlock for analytics on local data; subsumes most of the §5.8 / §5.10 / parts of §5.13 / §5.15 surface area.

**Pros:** DuckDB is MIT-licensed, pure-managed-friendly via .NET binding, uses very little memory, runs anywhere. Composes with kernel-ledger projections (paper §12.3 already provides CQRS read models — DuckDB queries the SQLite tables those projections write). Naturally satisfies paper §5.1 in-process mandate.

**Cons:** Adds a non-trivial native dependency (DuckDB ships native binaries per platform). Must verify it doesn't conflict with SQLCipher's `SQLitePCLRaw.bundle_e_sqlcipher` provider (DuckDB is independent of SQLite, so no direct conflict, but coexistence in the same process needs verification at Stage-02).

**Existing Sunfish coverage:** Ledger CQRS projections (paper §12.3); no arbitrary OLAP / DataFrame surface.

**Verdict + confidence:** A-native, **high** confidence. **Top-1 recommended next intake.**

---

### 5.8 Warehouse (T-SQL over OneLake) — Verdict: A-native

**What Fabric provides:** T-SQL warehouse interface over OneLake, separate from Spark-based Lakehouse query but querying the same data.

**Sunfish/Bridge analog:** Same answer as §5.7 — DuckDB-as-plugin provides SQL over local data. DuckDB's SQL dialect is PostgreSQL-flavored rather than T-SQL, but it's standard SQL; bundle authors writing reports don't care about T-SQL specifically.

**Pros / Cons / Coverage:** See §5.7.

**Verdict + confidence:** A-native, **high** confidence. Subsumed by the §5.7 intake.

---

### 5.9 Real-Time Intelligence (eventstreams + KQL eventhouses) — Verdict: A-native

**What Fabric provides:** Event ingestion + KQL (Kusto Query Language) over time-series and event data; Eventhouses as the storage tier.

**Sunfish/Bridge analog:** Sunfish's gossip daemon (paper §6.1) is the eventstream backbone — every CRDT operation is an event, gossiped to peers. A "KQL-style" query layer is a query plugin over the event log, supporting time-windowed aggregations, sessionization, etc. DuckDB itself supports time-series workloads adequately for v1 — a dedicated KQL layer is likely not needed unless a tenant explicitly asks.

**Pros:** Event log is already there. Adding query primitives over it is a plugin, not a kernel change.

**Cons:** Inventing a KQL dialect is unnecessary; reuse DuckDB SQL with time-series extensions. Real-time push (vs. polling) is a separate feature (see §5.11 Real-time Hub).

**Existing Sunfish coverage:** kernel-sync gossip daemon (event distribution); no query layer over event log.

**Verdict + confidence:** A-native, **medium** confidence. Likely composes with §5.7 DuckDB plugin rather than warranting its own intake.

---

### 5.10 Direct Lake (Power BI semantic model querying lake directly) — Verdict: A-native (with C complement)

**What Fabric provides:** Power BI semantic models query OneLake Delta tables directly without import — fast, fresh, no copy.

**Sunfish/Bridge analog:** Local dashboards (semantic-model-style) query local DuckDB / SQLite directly via the §5.7 plugin. Bridge complement: per-tenant hosted semantic model serving dashboards over tenant plaintext (Attested hosted peer trust level per ADR 0031).

**Pros:** Direct query semantics fit local-first naturally — no separate "import" step.

**Cons:** Bridge-hosted semantic model requires the tenant to authorize plaintext hosting; not the default trust level. Documentation must make this explicit.

**Existing Sunfish coverage:** ADR 0021 covers static report generation; no interactive semantic model layer.

**Verdict + confidence:** A-native (local) with C-hosted (plaintext, opt-in) complement, **medium** confidence.

---

### 5.11 Real-time Hub (event ingestion + routing) — Verdict: A-native (with C complement)

**What Fabric provides:** Centralized ingestion of events from many sources, routed to consumers (Eventhouses, notebooks, Activator).

**Sunfish/Bridge analog:** In-process event handling via kernel-sync. Cross-device push: Bridge relay tier already does this for CRDT ops; extending it to **named webhook endpoints** (operator-configured URLs that receive event payloads on tenant authorization) is a natural Bridge feature.

**Pros:** Composes with existing relay; webhook endpoints unlock integration with external systems (Zapier-style).

**Cons:** Webhook delivery semantics (retry, dead-letter, ordering) are a non-trivial design.

**Existing Sunfish coverage:** kernel-sync gossip daemon (in-process events); Bridge relay (cross-device, ciphertext only).

**Verdict + confidence:** A-native (in-process) with C-hosted (ciphertext) for relayed webhooks, **medium** confidence.

---

### 5.12 Data Activator (reactive triggers — when X then Y) — Verdict: A-native

**What Fabric provides:** "When condition X holds in your data, then take action Y" — a reactive trigger engine bridging analytics and automation.

**Sunfish/Bridge analog:** A reactive-trigger plugin that subscribes to event log streams, evaluates per-trigger predicates, and fires configured actions (notification, webhook, Bridge push, scheduled-job kick). Composes with §5.4 pipelines for the action side.

**Pros:** Highest-impact missing capability not already on the property-ops roadmap. Natural local-first plugin (predicate evaluation is local; action dispatch can be local or via Bridge). Composes with kernel-ledger event log naturally.

**Cons:** Predicate language design is a non-trivial decision (raw SQL? DSL? CRDT-aware?). Risk of feature-flag-style overuse.

**Existing Sunfish coverage:** None. Quartz handles scheduled jobs but not reactive triggers.

**Verdict + confidence:** A-native, **high** confidence. **Top-2 recommended next intake.**

---

### 5.13 Power BI (semantic models, reports, dashboards) — Verdict: A-native (with C complement)

**What Fabric provides:** Semantic models, interactive reports, dashboards, paginated reports.

**Sunfish/Bridge analog:** Local interactive dashboards via §5.7 DuckDB + Sunfish UI kernel (paper §5.2 four-tier UI, framework-agnostic primitives). Bridge complement: per-tenant hosted dashboards via Browser Shell (ADR 0031 §5.3 Wave 5.3 "Browser shell v1") — the existing roadmap item already supports this pattern.

**Pros:** Dashboard rendering composes with existing Sunfish UI primitives; no new framework. Static reports (PDF/XLSX) already covered by ADR 0021. Browser-shell-rendered interactive dashboards reuse Wave 5.3 work.

**Cons:** Interactive dashboard authoring (drag-drop) is a UI surface that needs design; not in Phase-2 scope per `phase-2-commercial-mvp-intake-2026-04-27.md` (BI/sales-readiness API is Phase 4+).

**Existing Sunfish coverage:** ADR 0021 (static reports — partial); property-owner-cockpit intake plans Phase 2.2 dashboards + Phase 2.3 reporting; ADR 0031 §5.3 Browser shell (rendering substrate).

**Verdict + confidence:** A-native + C-hosted (plaintext, opt-in for browser-shell), **high** confidence.

---

### 5.14 Copilot in Fabric (AI-assisted analytics) — Verdict: A-native (local) / C-hosted (Bridge proxy) / B-only (centralized training)

**What Fabric provides:** AI-assisted query authoring, summarization, drafting — embedded in every Fabric workload.

**Sunfish/Bridge analog:**
- **A-native:** Local LLM inference (Ollama-served small models, ONNX-runtime served quantized models) over local data. The model runs on the device; the data never leaves.
- **C-hosted (plaintext, opt-in):** Bridge-hosted LLM proxy that forwards tenant-authorized plaintext queries to a hosted inference endpoint (operator-configured: OpenAI / Anthropic / Azure OpenAI / self-hosted vLLM). Tenant chooses whether to authorize plaintext egress; default is no.
- **B-only (reject):** Cross-tenant model training on aggregated tenant plaintext. Direct E2EE violation; out-of-scope.

**Pros:** Local LLM is a strong differentiator vs. Fabric (which assumes Microsoft-hosted models). Bridge proxy lets tenants who *want* hosted models opt in without Sunfish operating an inference platform. The "no centralized training on tenant data" stance is marketable.

**Cons:** Local LLM hardware requirements vary widely; setting expectations matters. Bridge proxy needs careful authorization UX (tenant must understand plaintext is leaving their device).

**Existing Sunfish coverage:** None.

**Verdict + confidence:** A-native + C-hosted (plaintext, opt-in) + B-only (reject) for centralized training, **medium** confidence (depends on tenant LLM appetite, currently unknown).

---

### 5.15 Notebooks (collaborative analyst exploration) — Verdict: A-native (with C complement)

**What Fabric provides:** Collaborative notebooks (Jupyter-style) for analyst-facing exploration over OneLake data.

**Sunfish/Bridge analog:**
- **A-native:** Polyglot Notebooks / .NET Interactive over local data via §5.7 DuckDB plugin. Single-user exploration, runs in `local-node-host` or alongside it.
- **A-native real-time collaboration:** Notebook documents stored as CRDT documents (Yjs already in `kernel-crdt`); multiple users on the same team co-edit a notebook in real time, just like any other CRDT-backed document.
- **C-hosted (plaintext, opt-in):** Bridge-hosted notebook execution for tenants who want long-running queries to run server-side.

**Pros:** Real-time collaborative notebooks via CRDT is a genuine local-first differentiator (Fabric notebooks are server-collaborative, single-cell-locking). Polyglot Notebooks is .NET-native.

**Cons:** Notebook UI surface is non-trivial — likely a separate `blocks-notebooks` package + cooperation with apps/docs.

**Existing Sunfish coverage:** None.

**Verdict + confidence:** A-native + C-hosted (plaintext, opt-in for hosted execution), **high** confidence. Likely composes with §5.7 DuckDB intake.

---

### 5.16 Purview integration (lineage, sensitivity labels, DLP) — Verdict: A-native (local) / B-only (cross-tenant)

**What Fabric provides:** Data governance — lineage tracking, sensitivity classification, data loss prevention, glossary, catalog.

**Sunfish/Bridge analog:**
- **A-native:** Per-record schema-version lineage already partial via `packages/kernel-schema-registry/` (lenses + upcasters track structural derivations). Sensitivity labels per CRDT field extend paper §11.2 Layer 2 (field-level encryption with per-role keys) by adding a classification metadata layer. DLP (refusing to sync sensitive fields to ineligible peers) is already enforced by `kernel-buckets` role-attestation eligibility.
- **B-only (reject):** Cross-tenant central governance dashboard ("see all your tenants' data classifications in one place") — direct E2EE violation; Sunfish operator can't see tenant data. Out-of-scope.

**Pros:** Sunfish has strong governance primitives already; Purview vocabulary surfaces what's there. Local-first is *stronger* than Purview's "data sovereignty" claim — Sunfish doesn't require taking the operator's word for it.

**Cons:** Per-tenant governance UI (label editor, lineage viewer) is a UI surface to design.

**Existing Sunfish coverage:** kernel-schema-registry (lineage primitives); paper §11.2 Layer 2 (field encryption — no labels yet); kernel-buckets (DLP enforcement — no policy DSL yet); paper §11.3 (role attestation).

**Verdict + confidence:** A-native (local) / B-only (cross-tenant), **high** confidence.

---

### 5.17 Fabric Admin (workspace + capacity management) — Verdict: Already-covered (Bridge control plane)

**What Fabric provides:** Workspace management, capacity allocation, monitoring, audit logs — operator-facing admin surface.

**Sunfish/Bridge analog:** Bridge already has a per-tenant control plane per ADR 0031 (§"Default: Option C — Zone-C Hybrid multi-tenant"): "Aspire orchestration, Postgres, DAB, SignalR, Wolverine ... signup, billing, subscription tier enforcement, admin backoffice, support tickets, system status." Per-tenant capacity (one `local-node-host` process per tenant) maps to Fabric's per-workspace capacity model. Cross-tenant capacity sharing (Fabric's capacity-sharing model) doesn't exist in Sunfish — and shouldn't, because Bridge's per-tenant data plane is the isolation boundary.

**Pros:** Already exists; Fabric vocabulary surfaces it.

**Cons:** Bridge's admin surface is currently sparse (Wave 5.1-5.5 in ADR 0031 §"Work sequencing"); Fabric Admin framing may motivate filling gaps.

**Existing Sunfish coverage:** ADR 0031 Bridge control plane.

**Verdict + confidence:** Already-covered for per-tenant; B-only (reject) for cross-tenant capacity sharing, **high** confidence.

---

## 6. Cross-cutting observations

### 6.1 The "plaintext on Bridge" trust gradient is the load-bearing distinction

ADR 0031 already names three trust levels at signup: **Relay-only** (operator sees ciphertext only — default) / **Attested hosted peer** (operator can decrypt for backup verification — opt-in) / **No hosted peer** (self-host). Every C-hosted verdict in §5 lands on one of these levels. Future hosted-feature ADRs should make the trust-level mapping explicit in their decision text.

### 6.2 Sunfish has primitives Fabric calls by other names

| Fabric vocabulary | Sunfish equivalent |
|---|---|
| OneLake | User-controlled object storage as exporter target (§5.1) |
| Mirroring (CDC) | Event log + exporter (§5.2 — same primitive, different framing) |
| Eventstreams | Gossip daemon (paper §6.1) |
| Real-time Hub | Bridge relay tier (ADR 0031) — extended with named webhooks |
| Direct Lake | Local DuckDB queries against local SQLite/Parquet (§5.7) |
| Purview lineage | kernel-schema-registry (lenses + upcasters) |
| Purview DLP | kernel-buckets role-attestation eligibility (paper §10.2) |
| Purview sensitivity labels | Paper §11.2 Layer 2 field-level encryption (extend with classification metadata) |
| Fabric Admin | Bridge control plane (ADR 0031) |
| Fabric workspace | Bridge per-tenant data plane (ADR 0031) |

The vocabulary translation alone is valuable: it lets future roadmap conversations reference Fabric concepts without implying we're going to operate Fabric.

### 6.3 The local-first mandate is a *stronger* guarantee than Fabric's data sovereignty

Microsoft Fabric's data-sovereignty story is "data stays in the region you select; Microsoft doesn't read your data." Sunfish's local-first mandate is "data stays on devices and infrastructure *you operate*; the operator (us, or the Bridge tenant's contracted host) holds ciphertext only by default." Sunfish's claim is verifiable by the customer (they hold the keys); Fabric's requires trusting Microsoft's policy enforcement.

This is a marketing differentiator for regulated industries (legal, healthcare, finance, government). The eval should not understate it.

### 6.4 Most B-only sub-aspects cluster on "centralized aggregation across tenants without explicit per-tenant authorization"

Of the 4 capabilities with B-only sub-aspects (5.5 cross-tenant Spark cluster, 5.6 centralized ML training, 5.16 cross-tenant Purview, 5.17 cross-tenant capacity sharing), all 4 fail for the same reason: they assume the operator can read tenant plaintext at will and aggregate across tenants. ADR 0031's "ciphertext-as-defense-in-depth" + paper §17.2's "operator holds ciphertext only" rule them out by construction.

This is not a gap in Sunfish; it's a feature of the architecture. Future Fabric-vocabulary requests should be filtered through this lens before being treated as gaps.

### 6.5 What Sunfish has that Fabric doesn't (worth keeping in scope)

- **CRDT-based collaborative documents** — every CRDT document supports concurrent multi-user editing via Yjs (`kernel-crdt`). Fabric's collaboration model is server-locked single-cell editing. Sunfish notebooks (§5.15), dashboards (§5.13), and pipelines (§5.4) can all be collaborative-by-construction.
- **Signed-attestation lineage** — schema lineage in Sunfish can carry Ed25519 attestations from the team admin (kernel-security primitives). Fabric's lineage is operator-asserted. Sunfish's can be cryptographically verified.
- **Append-only event log** as both source-of-truth and audit trail — paper §12.1 makes the ledger first-class. Fabric treats audit logs as a separate stream; Sunfish unifies them.

These differences should appear in the marketing positioning, not as Fabric gaps.

---

## 7. Recommended next intakes

In priority order:

### 7.1 `blocks-local-analytics-duckdb` (covers §5.7, §5.8, §5.10, parts of §5.13, §5.15)

**ICM pipeline variant:** `sunfish-feature-change`
**Package(s):** `Sunfish.Blocks.LocalAnalytics.DuckDb` (new) + `Sunfish.Foundation.Analytics` (new contracts package) + `Sunfish.Plugins.LocalAnalytics.DuckDb` (the `ILocalNodePlugin` registration)
**Composes with:** `kernel-ledger` (queries CQRS projections), `kernel-schema-registry` (column-name stability across schema epochs), `foundation-localfirst` (queries SQLCipher-stored tables — verify provider coexistence)
**Gating decision:** DuckDB.NET binding maturity vs. alternatives (Apache DataFusion via FFI, ClickHouse-embedded, raw SQLite extensions) — Stage-02 architecture decision.
**Why first:** Single largest unlock; subsumes Lakehouse query, Warehouse, Direct Lake, parts of Power BI and Notebooks. Composes with mirroring (§5.2) for the export side.

### 7.2 `blocks-reactive-triggers` (covers §5.12, parts of §5.4 and §5.11)

**ICM pipeline variant:** `sunfish-feature-change`
**Package(s):** `Sunfish.Blocks.ReactiveTriggers` (new) + `Sunfish.Foundation.Triggers` (new contracts) + `Sunfish.Plugins.ReactiveTriggers` (registration)
**Composes with:** `kernel-ledger` event log (subscribed-to stream), `kernel-runtime` (action dispatch via `IActionRegistry` — likely new), §7.3 exporter (one possible action), Bridge relay (cross-device action dispatch)
**Gating decision:** Predicate language design (raw SQL via §7.1 / lightweight DSL / CRDT-aware structural matchers) — Stage-02 architecture decision.
**Why second:** Highest-impact missing capability not already on the property-ops roadmap. Unlocks workflow automation that property-ops Phase 2 needs (lease-renewal triggers, work-order escalation, payment-overdue notifications) but doesn't currently have a substrate for.

### 7.3 `blocks-snapshot-exporter` (covers §5.1, §5.2 outbound, parts of §5.16)

**ICM pipeline variant:** `sunfish-feature-change`
**Package(s):** `Sunfish.Blocks.SnapshotExporter` (new) + `Sunfish.Foundation.Snapshots` (extend existing snapshot infra) + `Sunfish.Plugins.SnapshotExporter.S3` / `Sunfish.Plugins.SnapshotExporter.AzureBlob` / `Sunfish.Plugins.SnapshotExporter.LocalFs` (target adapters per ADR 0021 contract-and-adapter pattern)
**Composes with:** kernel-ledger (event log source), kernel-schema-registry (canonical projection shape), ADR 0021 contract-and-adapter precedent, Bridge per-tenant ciphertext archival
**Gating decision:** Output table format (Parquet / Delta / Iceberg) — Stage-02 architecture decision; default likely Parquet for v1, Delta optional via adapter.
**Why third:** Preserves data ownership when tenants want external analytics. Positions Sunfish to interoperate with any external lakehouse (Microsoft Fabric, Databricks, BigQuery, Snowflake, the user's own DuckDB) without Sunfish operating one. Unblocks "we want our data in *our* lake" enterprise asks.

### Capabilities NOT recommended for next intakes

- **§5.3 Shortcuts** — wait for ADR 0029 federation; deferred per ADR 0031 §5.
- **§5.5/§5.6 Spark/ML at scale** — local notebooks suffice for v1; no tenant has asked.
- **§5.9 KQL eventhouses** — DuckDB SQL covers; revisit if a tenant explicitly asks.
- **§5.11 Real-time Hub webhooks** — defer until §7.2 reactive triggers ship; webhooks are one action type.
- **§5.14 Copilot** — wait for tenant signal; either local LLM or Bridge proxy is non-trivial.
- **§5.17 Fabric Admin** — already covered by Bridge control plane; gaps surface during Wave 5.1-5.5 build.

---

## 8. Out of scope / explicitly deferred

| Capability sub-aspect | Why out of scope | Trigger to revisit |
|---|---|---|
| Cross-tenant Spark cluster (§5.5) | Multi-tenant centralized compute on plaintext = E2EE violation | None; Zone B per paper §20.7 |
| Centralized ML training on tenant plaintext (§5.6) | Same as above | None; Zone B |
| Cross-tenant Purview governance dashboard (§5.16) | Operator can't see tenant data; cross-tenant aggregation impossible | None; Zone B |
| Cross-tenant capacity sharing (§5.17) | Per-tenant data plane is isolation boundary by design | None; would require ADR 0031 amendment |
| Fabric-style "free tier with telemetry" | Sunfish is OSS / E2EE; telemetry is opt-in only | None; conflicts with paper §11 |
| Fabric capacity-pricing model | Bridge is per-tenant subscription per ADR 0031 | None; pricing model is decided |
| Cross-tenant Shortcuts (§5.3) | Cross-tenant collaboration deferred per ADR 0031 §5 | When ADR 0029 federation reaches design |
| Spark-cluster-equivalent local compute | Over-spec for local hardware | If tenant explicitly asks with hardware budget |

---

## 9. Open questions for CO

1. **Single-tenant Bridge mode for solo BDFL property-ops use:** ADR 0031 assumes Bridge is multi-tenant from day 1. Is there a "Bridge-of-one" mode for the BDFL's own property-management deployment, or does the BDFL run Anchor + self-hosted relay? (Affects §5.10, §5.13 Bridge-hosted dashboard deployment options.)

2. **Local LLM appetite (§5.14):** Is local LLM inference a Phase-2 commercial differentiator the BDFL wants to lead with, or a Phase 3+ "if tenants ask"? (Affects whether §5.14 graduates to a recommended next intake.)

3. **Mirroring target preference (§5.2, §7.3):** Does the BDFL have a preferred external lakehouse target (Microsoft Fabric for legal/insurance integration? Databricks? Snowflake? Open-format-only?), or should the exporter be format-only / target-agnostic? (Affects §7.3 priority and the per-target adapter list.)

4. **Reactive trigger language (§5.12, §7.2):** Is there an existing predicate language convention in Sunfish (CRDT-aware patterns? raw SQL? DSL?) that §7.2 should adopt, or is this a fresh design decision? (Stage-02 question, but worth flagging now.)

5. **Bridge admin UI scope:** ADR 0031 §"Work sequencing" Wave 5.1 narrows control-plane scope. Does Fabric Admin framing (§5.17) suggest expanding the admin UI ambitions, or staying narrow until tenants ask?

6. **Marketing positioning:** Does the BDFL want this Fabric-mapping vocabulary to leak into external messaging ("Sunfish does Fabric-like analytics, locally"), or stay internal as a roadmap clarification tool? (Shapes whether the eval becomes a public-facing white paper.)

---

## 10. References

### ADRs cited

- [ADR 0021 — Document and Report Generation Pipeline](../../../docs/adrs/0021-reporting-pipeline-policy.md)
- [ADR 0028 — CRDT Engine Selection](../../../docs/adrs/0028-crdt-engine-selection.md) (sidecar rejection language)
- [ADR 0029 — Federation Reconciliation](../../../docs/adrs/0029-federation-reconciliation.md) (cross-tenant)
- [ADR 0031 — Bridge as Hybrid Multi-Tenant SaaS](../../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md)
- [ADR 0032 — Multi-Team Anchor Workspace Switching](../../../docs/adrs/0032-multi-team-anchor-workspace-switching.md)
- [ADR 0051 — Foundation Integrations Payments](../../../docs/adrs/0051-foundation-integrations-payments.md) (Quartz baseline)

### Foundational paper sections cited

`_shared/product/local-node-architecture-paper.md`:

- §5.1 Kernel and Plugin Model — in-process plugin mandate
- §5.2 The UI Kernel — four-tier UI substrate
- §5.3 Extension Point Contracts — `ILocalNodePlugin` etc.
- §6.1 Gossip Anti-Entropy — eventstream backbone
- §8 Snapshots and Rehydration — exporter substrate
- §10.2 Declarative Sync Buckets — DLP enforcement primitive
- §11 Security Architecture (full) — E2EE invariants
- §12 Ledger Mechanics — event log as audit trail + source of truth
- §17.2 Managed Relay as Sustainable Revenue — ciphertext-only operator
- §20.7 The Three Outcome Zones — A / B / C framework

### Code paths cited

- `packages/foundation-localfirst/` — SQLCipher store, OS keystores
- `packages/kernel-crdt/` — Yjs binding
- `packages/kernel-ledger/` — event-sourced posting engine
- `packages/kernel-sync/` — gossip daemon, mDNS, transport
- `packages/kernel-lease/` — Flease CP coordinator
- `packages/kernel-buckets/` — selective sync, DLP enforcement
- `packages/kernel-runtime/` — `ILocalNodePlugin`, `TeamContext`
- `packages/kernel-schema-registry/` — lenses, upcasters, lineage
- `packages/kernel-security/` — Ed25519 role attestation
- `packages/blocks-tax-reporting/` — existing export pattern
- `apps/local-node-host/` — Anchor + per-tenant Bridge host process

### Intakes cited

- [`icm/00_intake/output/property-owner-cockpit-intake-2026-04-28.md`](../../00_intake/output/property-owner-cockpit-intake-2026-04-28.md) — Phase 2.2 dashboards, Phase 2.3 reporting
- [`icm/00_intake/output/phase-2-commercial-mvp-intake-2026-04-27.md`](../../00_intake/output/phase-2-commercial-mvp-intake-2026-04-27.md) — Phase 4+ BI deferred
- [`icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md`](../../00_intake/output/property-ops-INDEX-intake-2026-04-28.md) — property-ops cluster

### Microsoft Fabric documentation

Microsoft Fabric public documentation overview (workload taxonomy as of late-2025/early-2026): <https://learn.microsoft.com/en-us/fabric/>. URLs not version-pinned; capability list reflects the canonical Microsoft workload set at time of writing.

---

*End of evaluation. Decision authority: CO. Next step (if accepted): scope §7.1 / §7.2 / §7.3 as Stage 00_intake entries.*
