---
id: 88
title: Anchor as All-In-One Local-First Runtime
status: Proposed
date: 2026-05-16
tier: accelerator
pipeline_variant: sunfish-feature-change

concern:
  - ui
  - persistence
  - distribution
  - accessibility
  - security
  - licensing

enables:
  - small-business-os-on-light-hardware
  - corporate-deployment-without-docker-licensing
  - native-domain-implementation-via-blocks-clusters
  - clean-room-foss-pattern-leverage

composes:
  - 86   # Anchor Tauri-React Product Surface
  - 48   # Anchor multi-backend MAUI (parallel surface, retained per ADR 0086 γ)
  - 67   # Headscale substitution for Tailscale BSL
  - 32   # Anchor multi-team workspace switching
  - 31   # Bridge hybrid multi-tenant SaaS (Hosted tier)

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

amendments: []
---

# ADR 0088 — Anchor as All-In-One Local-First Runtime

**Status:** Proposed
**Date:** 2026-05-16

---

## Context

W#60 (CO UPF plan approved 2026-05-11) chose ERPNext (GPLv3, self-hosted Docker) as the property/accounting engine, with Sunfish/Anchor as the local-first sync + UI layer over it. Phases 1–3 of that plan shipped on that premise.

During W#60 P3 PASS testing on Surface Pro 7 (Intel Core i3-10100Y, x64), two related findings forced a reconsideration:

1. **Anchor's empty-state message** (*"No properties found. Create your first Property record in ERPNEXT"*) exposed ERPNext as a leaky technical dependency the user has to know about and manage. This contradicts the Inverted Stack paper's local-node philosophy (`_shared/product/local-node-architecture-paper.md` §13): the local node IS the application; the user installs one thing.
2. **The Frappe stack (ERPNext's substrate) is server-class infrastructure** — minimum ~1.5–2 GB idle RAM, multi-process, requires a container runtime (Docker Desktop or Podman). It does not fit on light-hardware target devices (Surface Pro 7 class, future Snapdragon-X tablets, modest office boxes). Forcing it locally would require raising the hardware bar in a way that contradicts the "small office on light hardware" use case.

Two paths surfaced:

- **Path I** — Bundle ERPNext as a composite runtime (Anchor + Podman + Frappe stack). Requires 16+ GB / 4-core / SSD as effective minimum hardware. Brings Docker/Podman licensing posture into the install. Excludes the device class W#60's product story targets.
- **Path II** — Anchor is the entire stack. SQLite is the primary store (not a cache); Loro CRDT handles peer-to-peer sync between local nodes; the domain (property, lease, accounting, AR/AP, reports, etc.) is implemented natively in Sunfish `blocks-*` clusters; no external engine, no container runtime, no licensing entanglement. Idle footprint ~100 MB RAM.

CO ratified Path II on 2026-05-16 over the course of an iterative design conversation. This ADR formalizes the decision and captures the implications.

## Decision

**Anchor is the all-in-one local-first runtime.** No external engine, no container runtime, no separate database server. The domain is implemented natively in Sunfish `blocks-*` clusters; the data lives in SQLite (primary, not a cache); peer-to-peer sync between local nodes goes through Loro CRDT.

Five sub-decisions structure the implementation:

### 1. Block-cluster grouping (7 clusters; storefront deferred)

The native domain is partitioned into seven block clusters, six of which are in active scope. POS / online-sales (cluster 7) is deferred until explicit demand.

| Cluster | Covers | Phase |
|---|---|---|
| `blocks-property-*` | Property + lease + tenant + inspection + deficiency + maintenance + asset lifetimes | **Phase 1** (active) — 14 existing intakes from 2026-04-28 cluster |
| `blocks-financial-*` | Chart-of-accounts + journal + AR/AP + tax + budgets + forecasting + estimates + bids | **Phase 1 core** (active) + Phase 3 advanced |
| `blocks-reports-*` | PDF rendering + statements + invoices + receipts + executive dashboards + tax reports (Schedule E etc.) | **Phase 1** (active) |
| `blocks-work-*` | Projects + work-orders + repairs + remodels + maintenance schedules + contractor mgmt + deliverables + contracts | **Phase 2** (active per CO directive 2026-05-16) |
| `blocks-docs-*` | Policies + procedures + contract templates + marketing collateral (DAM) + wiki + signing workflow | **Phase 2** (active per CO directive 2026-05-16) |
| `blocks-people-*` | Staff + scheduling + leaves + onboarding + training + contacts + customers + leads/opportunities | **Phase 3** (active per CO directive 2026-05-16) |
| `blocks-storefront-*` | POS + ancillary sales + product catalog | **Deferred** — no active workstream until explicit demand |

### 2. License posture: Sunfish output is MIT

**All Sunfish source code remains MIT-licensed.** This locks the project's licensing posture and constrains how external FOSS sources can be leveraged.

| Category | What we can do | Examples (relevant to this ADR's scope) |
|---|---|---|
| **Permissive** (MIT / Apache 2.0 / BSD / Public Domain / MPL 2.0 with file-level discipline) | Borrow code directly with attribution in source-header + entry in repo `LICENSES/` or `NOTICE` | Apache OFBiz, Mayan EDMS, Bookstack, Outline, react-pdf, WeasyPrint, Loro, Automerge, Y.js, SQLite, Tauri, Mailtrain, Kanboard, ResourceSpace, DocAssemble |
| **Copyleft** (GPLv2/3 / AGPLv3 / LGPL) | **Read for understanding only. Clean-room implement. Never paste code, never vendor.** | ERPNext, Frappe, GnuCash, Beancount, Akaunting, EspoCRM, SuiteCRM, OpenMAINT, Snipe-IT, Tryton, OrangeHRM, OpenProject, Redmine, Moodle, Chamilo, Cal.com, Documenso, Listmonk, Wiki.js, HedgeDoc |
| **Proprietary / source-available with use restrictions** (Elastic License, BSL, etc.) | Study public docs / blog posts / academic papers only; do not vendor or fork | RealPage, AppFolio, Wave, Manager, InvoiceNinja (Elastic), Bryntum, TurboTax |

### 3. Clean-room implementation discipline (mandatory)

For copyleft sources, the team uses clean-room implementation: study source for understanding, design and implement from clean-room schemas + textbook-fundamentals algorithms, cite as inspiration rather than as source. The discipline rules:

1. **License-classification gate.** Every new external source-reference is classified into permissive / copyleft / proprietary BEFORE any code work begins. Documented in the per-block design notes (typically the Stage 02 architecture doc for that cluster).
2. **Reading isolation for copyleft.** When studying GPL/AGPL/LGPL source, work in a separate git worktree or non-Sunfish directory. NEVER open copyleft source while editing Sunfish files in the same editor session.
3. **Cleansing rule.** Code from copyleft sources is never pasted — not into comments, not into temp drafts, not into commit-discarded scratch files. Clean-room means clean.
4. **Attribution for borrowed permissive code.** Source-header comment naming the upstream + version + license; entry in `LICENSES/` or `NOTICE` at repo root.
5. **Schema-mining output.** A per-cluster design doc (typically at `icm/02_architecture/blocks-{cluster}-schema-design.md`) captures field shapes, validation rules, workflow states, and citations to FOSS sources reviewed (with license noted). This is the clean-room artifact suitable for any implementer.

The clean-room pattern is the standard approach for learning from GPL projects to build non-GPL work; it has been litigated enough to be safe-default. The discipline is what makes it safe.

### 4. Tiered runtime model

Anchor ships in three tier profiles. The Light tier is the canonical local-first product; Standard adds Bridge composition; Hosted is the SaaS path per ADR 0031.

| Tier | Profile | Stack | Hardware target |
|---|---|---|---|
| **Light** | Anchor only | Tauri shell + SQLite + Loro CRDT; native domain via `blocks-*`; no container runtime | x64 or ARM64; 4 GB RAM; 2 GB free disk; Windows 10+ / macOS 12+ / Linux. Surface Pro 7 class passes. |
| **Standard** | Anchor + bundled Bridge instance | Adds local Bridge runtime (.NET) for accountant/CPA scoped access via Bridge role-account model | x64 or ARM64; 8 GB RAM; 4 GB free disk |
| **Hosted** | Anchor as client to a Sunfish Bridge tenant (Zone C per paper §20.7) | Anchor connects to remote Bridge; minimal local footprint | Any browser-capable device |

Container runtime (Podman, per the corporate-licensing-clean choice over Docker Desktop) is **only required** for the Standard tier's local Bridge runtime, and even then only as a fallback for environments where the .NET runtime isn't viable. Light tier never requires a container runtime.

### 5. Minimum hardware spec (Light tier — the canonical target)

| Resource | Minimum |
|---|---|
| Architecture | x64 or ARM64 |
| RAM | 4 GB |
| Free disk | 2 GB (1 GB Anchor install + 1 GB SQLite data growth headroom) |
| OS | Windows 10+ / macOS 12+ / Linux (recent kernel) |
| Network | Optional; Light tier works fully offline. Tailnet / LAN required for peer-to-peer Loro sync between Anchor instances. |

Surface Pro 7 (i3-10100Y, 4-8 GB RAM, 64-128 GB SSD) passes. Raspberry Pi 4 (4 GB) passes. Cheap office boxes pass.

## Consequences

### Positive

- **Local-first vision intact.** One install gives the user everything; no Docker prompt, no ERPNext URL configuration, no leaky abstraction.
- **Corporate-licensing-clean.** No Docker Desktop license question; Podman optional for Standard tier only; no GPL/AGPL code vendored into Sunfish.
- **Light hardware viable.** Surface Pro 7 class is the canonical target, not an exclusion.
- **Substrate alignment.** Sunfish's existing `blocks-*` pattern (already 14 intakes for property-ops from 2026-04-28) becomes the canonical home for the native domain. The ERPNext detour was scaffolding; the substrate was always heading here.
- **Migration path preserved.** A one-way importer reads ERPNext JSON/CSV exports (the project's existing Mac ERPNext data: 4 LLCs, leases, rent history, $7.6M Wave-history-migrated accounting) into the Anchor-native data model. No data loss; smooth cutover.

### Negative / costs

- **Implementation effort.** Native domain implementation is multi-quarter; ~5–7 weeks for the MVP Phase 1 (property + financial-core + reports) and ~6–8 weeks each for Phase 2 (work + docs) and Phase 3 (people + financial-advanced). ERPNext's other ~80% of modules (CRM-heavy, HR-heavy, manufacturing, etc.) we intentionally don't replicate — the scope is the bounded set we actually need.
- **W#60 P4 PRs 2–5 re-scope.** Original PR plan (Accountant Bridge / CPA / Tenant portal / Bank CSV) was ERPNext-backed. Needs reshape to align with native `blocks-*` data model. PR 1 (Stronghold + DPAPI) is architecture-neutral and proceeds as planned.
- **Re-discovery cost.** Domain knowledge that ERPNext encodes (e.g., tax-line-mapping for Schedule E, double-entry edge cases, lease-accounting accruals) must be re-derived from textbook fundamentals + FOSS clean-room reading. Mitigated by the FOSS source survey appendix below.
- **No fall-back to ERPNext.** Once the migration importer ships and CO cuts over, returning to ERPNext-as-engine is harder. Mitigated by the data model being independently auditable (SQLite is portable; Loro state is exportable).

## Alternatives Considered

### Path I — ERPNext-bundled (composite runtime)

Bundle ERPNext + Podman + DB + Redis into the Anchor install. Rejected because (a) effective minimum hardware spec rises to 16 GB RAM / 4-core / SSD, excluding the Surface Pro 7-class devices the product story targets; (b) brings Docker/Podman licensing posture into every install; (c) contradicts local-node-as-the-application philosophy from paper §13; (d) "Frappe Lite" doesn't exist as a supported config — the framework wants its full stack.

### Path I-lite — strip Frappe to single-process dev mode

Run Frappe with no Redis, no Nginx, no background workers (`bench start` without supervisord, SQLite instead of MariaDB). Rejected because (a) not a supported / maintained config, fragility at every Frappe upgrade; (b) saves only ~50-60% of resource cost, leaves us at ~800 MB–1 GB idle which still excludes light hardware with headroom for the app itself; (c) the production deployment story would have to be different from the dev story, which defeats local-first uniformity.

### γ option — keep ERPNext-engine for high-spec devices + Anchor-native for light

Two product profiles, two implementations, configured at install. Rejected because (a) doubles the implementation surface; (b) creates a data-model-divergence risk between profiles; (c) the value proposition of local-first is "one thing works everywhere" — bifurcating defeats it.

## Concerns

- **Implementation depth requires sustained discipline.** Clean-room implementation isn't hard in principle but requires conscious team-level discipline (license-classification gate, reading isolation, cleansing rule). Lapses in discipline create derivative-work risk. Mitigated by codifying the discipline in this ADR + per-cluster Stage 02 design docs requiring explicit FOSS-source classification.
- **Migration data integrity.** The one-way ERPNext-export → Anchor-native importer must preserve the $7.6M Wave history and 4-LLC operational data without loss. Mitigated by treating the migration importer as a first-class deliverable with its own acceptance tests.
- **CRDT-as-primary-store semantics.** Loro is well-engineered but using CRDT as the primary store (not just a sync overlay) is less battle-tested than the cache-vs-truth pattern. Mitigated by SQLite being the actual primary store and Loro layering on top for multi-device-sync semantics.
- **No fallback to a mature engine.** If a domain area (e.g., tax-line mapping) turns out to be much harder than estimated, we don't have ERPNext to fall back on. Mitigated by the FOSS source survey + textbook-fundamentals foundation; many of these domains (double-entry, AR aging, tax accruals) are 500+ year-old patterns, not someone's IP.

## Appendix A — FOSS source survey

The full FOSS source survey (organized by domain, with license posture per source) lives in `_shared/engineering/foss-source-survey-anchor-domain.md`. That document is the input to the schema-mining sprint that drives each `blocks-*` cluster's clean-room Stage 02 design doc.

Cardinal classification:
- **Permissive (MIT / Apache 2.0 / BSD / PD / MPL 2.0 per-file):** borrow with attribution
- **Copyleft (GPL / AGPL / LGPL):** read-only, clean-room implement, never paste
- **Proprietary:** study public docs only

Highest-leverage permissive sources across domains:
- **Apache OFBiz** (Apache 2.0) — comprehensive ERP/CRM/accounting/HR/property entity models; borrow with attribution across `blocks-property-*`, `blocks-financial-*`, `blocks-work-*`, `blocks-people-*`, `blocks-reports-*`
- **react-pdf** (MIT) — PDF generation for `blocks-reports-*`
- **Mayan EDMS** (Apache 2.0) — document versioning + tagging for `blocks-docs-*`
- **Bookstack** (MIT) — wiki/policies primitive for `blocks-docs-*`
- **Mailtrain** (MIT) — newsletter / marketing-blast for `blocks-people-*` and `blocks-docs-*`
- **rrule** (BSD-2) — iCal recurrence rules for `blocks-people-*` scheduling
- **ResourceSpace** (BSD-3) — DAM for `blocks-docs-*` marketing collateral
- **Kanboard** (MIT) — minimal Kanban for `blocks-work-*`
- **DocAssemble** (MIT) — document automation for `blocks-docs-*` contract templates

Highest-leverage clean-room copyleft sources (read-only):
- **ERPNext** — property + lease + accounting + invoicing
- **Beancount + ledger-cli** — cleanest double-entry data models in OSS
- **GnuCash** — desktop accounting + tax-line mapping (Schedule E)
- **OpenMAINT** — canonical inspection → deficiency → work-order workflow
- **EspoCRM** — modern PHP CRM DocType patterns
- **OrangeHRM** — comprehensive HRMS
- **OpenProject / Redmine** — project + budget-vs-actual

## Appendix B — Implementation phasing

Per CO directive 2026-05-16, Phase 1 + Phase 2 + Phase 3 are in active scope simultaneously (with parallel schema-mining sprints kicking off concurrently). Phase 4 (POS) is deferred. The phase ordering reflects implementation priority and dependency flow, not strict serial scheduling.

| Phase | Clusters | Target effort | Rationale |
|---|---|---|---|
| **Phase 1 (MVP)** | `blocks-property-*` core + `blocks-financial-*` core (chart + journal + AR/AP) + `blocks-reports-*` (basic PDF + receipts + invoices) | 5–7 weeks | Closes the immediate Wave/Rentler/Mac-ERPNext replacement loop |
| **Phase 2** | `blocks-work-*` (projects/maintenance/contractors/deliverables/contracts) + `blocks-docs-*` (policies/procedures/contract-templates/DAM/wiki/signing) | 6–8 weeks | Operational documentation + project execution; high-utility for property-ops |
| **Phase 3** | `blocks-people-*` (staff/scheduling/onboarding/training/contacts/CRM) + `blocks-financial-*` advanced (budgets/forecasting/estimates/bids) | 6–8 weeks | When operations grow beyond 1–3 employees, or when contracting work out at scale |
| **Phase 4 (deferred)** | `blocks-storefront-*` (POS), advanced LMS/training, marketing automation | TBD | Only when explicit demand surfaces |

Parallel schema-mining sprint dispatched on 2026-05-16: 5 cluster design docs (`blocks-financial-*`, `blocks-work-*`, `blocks-people-*`, `blocks-docs-*`, `blocks-reports-*`) authored concurrently via subagent dispatch; `blocks-property-*` design draws on the existing 14 intakes from 2026-04-28.
