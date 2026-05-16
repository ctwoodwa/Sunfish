# FOSS Source Survey — Anchor Domain

**Status:** Active reference document
**Authored:** 2026-05-16
**Authoritative companion to:** [ADR 0088 — Anchor as All-In-One Local-First Runtime](../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md)

## Purpose

This document is the consolidated FOSS source survey for the Sunfish Anchor domain. It exists to support clean-room implementation of Anchor's native `blocks-*` clusters per Path II (ADR 0088). For each domain area the Anchor product covers, this document catalogs the FOSS projects worth studying, their license posture, and the use guidance that follows from the license.

**How to use this doc:**

1. Before any cluster's Stage 02 design begins, identify the relevant domain section(s) below
2. Apply the license-classification gate (§Discipline below)
3. For permissive sources: borrow code/schemas/templates with attribution
4. For copyleft sources: read for understanding, clean-room implement, never paste code
5. For proprietary sources: study public documentation only
6. Capture FOSS-source citations + license + what each informed in the cluster's Stage 02 design doc

This document is the single point of truth for the FOSS landscape Anchor's domain touches. When a new source is added or evaluated for any cluster, update this document.

## License-classification policy

Sunfish output is **MIT-licensed** (per ADR 0088 §2). This constrains how external sources can be vendored.

| Category | License examples | What we can do |
|---|---|---|
| **Permissive** | MIT, Apache 2.0, BSD-2/3, Public Domain, MPL 2.0 (file-level discipline) | **Borrow code directly** with attribution in source-header comment + entry in repo `LICENSES/` or `NOTICE`. Can mix with MIT codebase freely. |
| **Copyleft** | GPLv2, GPLv3, AGPLv3, LGPL | **Read for understanding only.** Clean-room implement. **Never paste code**, never vendor, never fork. Designs derived from clean-room reading + textbook fundamentals are MIT-licensable. |
| **Proprietary** | Elastic License, BSL, Commercial-only, Custom-restrictive | Study **public documentation / blog posts / API references / academic papers only**. No code reading, no decompilation, no design extraction. |

**AGPLv3 special note:** even though Anchor is local-first (and the AGPL "network distribution" trigger normally wouldn't apply), the Standard tier composing a local Bridge runtime + the Hosted tier exposes us to AGPL's source-distribution clause if we ever vendored AGPL code. **Conservative posture: treat AGPLv3 the same as GPL** — study only, never vendor.

## Discipline rules (mandatory)

Per ADR 0088 §3:

1. **License-classification gate** — every external source-reference is classified into permissive / copyleft / proprietary BEFORE any code work begins. Documented in the cluster's Stage 02 design doc.
2. **Reading isolation for copyleft** — when studying GPL/AGPL/LGPL source, work in a separate git worktree or non-Sunfish directory. NEVER open copyleft source while editing Sunfish files in the same editor session.
3. **Cleansing rule** — code from copyleft sources is never pasted — not into comments, not into temp drafts, not into commit-discarded scratch files.
4. **Attribution for borrowed permissive code** — source-header comment naming the upstream + version + license; entry in `LICENSES/` or `NOTICE` at repo root.
5. **Schema-mining output** — a per-cluster design doc (typically at `icm/02_architecture/blocks-{cluster}-schema-design.md`) captures field shapes, validation rules, workflow states, and citations to FOSS sources reviewed (with license noted).

## Domain survey

Organized by Anchor block cluster (the 7-cluster grouping per ADR 0088). For each cluster, the sources are listed in study-priority order — the most-relevant or highest-leverage first.

### `blocks-property-*` — Property + lease + tenant + inspection + maintenance + asset lifetimes

| Source | License | Posture | Notes |
|---|---|---|---|
| ERPNext property + rental DocTypes | GPLv3 | Clean-room | Primary reference for property data shape — field defs, validation, workflows |
| Apache OFBiz `facilities` + `rental` modules | Apache 2.0 | **Borrow** | Property entity model + lease workflows; direct port to TS types |
| Tryton `account_rental` | GPLv3 | Clean-room | Cross-check on property accounting flows |
| OpenMAINT (`facility` + `assets` + `inspections`) | AGPLv3 | Study only | **Canonical reference** for building + asset + inspection patterns; workflow: schedule inspection → record findings → generate deficiencies → schedule remediation |
| Snipe-IT (`assets` + `audits` + `consumables`) | AGPLv3 | Study only | Asset lifecycle: check-in/check-out, status states, audit trails, custom fields per asset class |
| Apache OFBiz `asset` + `fixedasset` modules | Apache 2.0 | **Borrow** | Fixed-asset lifecycle, depreciation methods, maintenance schedule entities |
| ERPNext Asset (`asset`, `asset_maintenance`, `asset_repair`) | GPLv3 | Clean-room | Depreciation calc + service-life prediction + repair-against-asset linking |
| iTop (CMDB + asset relationships) | AGPLv3 | Study only | Strong relational asset modeling — useful when asset deficiencies cascade |

**Highest-leverage:** OpenMAINT's inspection → deficiency → work-order workflow (AGPL — clean-room) + Apache OFBiz's fixedasset entity model (Apache 2.0 — borrow).

### `blocks-financial-*` — Chart-of-accounts + journal + AR/AP + tax + budgets + forecasting + estimates + bids

| Source | License | Posture | Notes |
|---|---|---|---|
| Apache OFBiz `accounting` module | Apache 2.0 | **Borrow** | Entity model + chart-of-accounts + journal patterns. **Highest-leverage permissive source for this cluster.** |
| Beancount + ledger-cli ecosystem | GPLv2 | Clean-room | **Cleanest double-entry data models in OSS** — plain-text journal + chart-of-accounts; the patterns will outlive any specific syntax |
| GnuCash | GPLv2 | Clean-room | Mature desktop accounting; AR aging + reconciliation patterns + Schedule E tax-line mapping |
| Akaunting | GPLv3 | Clean-room | Modern PHP small-business AR/AP patterns |
| ERPNext (`accounts`, `tax_template`, `budget`) | GPLv3 | Clean-room | Property-business-specific patterns |
| Tryton (accounting modules) | GPLv3 | Clean-room | Modular Python ERP; alternative accounting reference |
| IRS Publication 527 + Schedule E + Form 1040 + 1099 instructions | Public domain | **Direct reference** | US rental-property tax mapping; authoritative line-by-line spec |
| OpenProject (budgets + cost-types) | GPLv3 | Clean-room | Strong budget-vs-actual on projects |
| GanttProject | GPLv3 | Clean-room | Schedule + cost build-up patterns (estimates) |
| InvoiceNinja | Elastic License | **Avoid** | Not OSI-approved; license use restrictions |

**Highest-leverage:** Apache OFBiz `accounting` module (Apache 2.0 — direct borrow) for entity model + Beancount/ledger-cli (GPL — clean-room) for the cleanest double-entry data model.

### `blocks-reports-*` — Statements, invoices, quotes, bills, receipts, executive dashboards, tax reports

| Source | License | Posture | Notes |
|---|---|---|---|
| react-pdf (`@react-pdf/renderer`) | MIT | **Direct dep** | React components → PDF; drop-in for Anchor's React stack. **Single largest permissive-borrow opportunity for this cluster.** |
| WeasyPrint (HTML/CSS → PDF) | BSD-3 | **Borrow / direct dep** | If a server-side renderer is needed (e.g., Bridge tier hosting tenant statements) |
| wkhtmltopdf | LGPLv3 | Command-line invocation only | No static link; useful for ad-hoc PDF gen |
| Apache OFBiz `accounting/template/*` invoice + statement templates | Apache 2.0 | **Borrow** | Real-world invoice / statement / quote templates |
| Apache Superset (BI dashboards) | Apache 2.0 | **Borrow** | KPI cards, time-series charts, drill-down patterns |
| Metabase | AGPLv3 | Study only | Open-source BI reference |
| Akaunting (modern invoicing) | GPLv3 | Clean-room | Invoice / quote / bill workflow + line-item composition |
| Beancount (period statement queries) | GPLv2 | Clean-room | Period balance statements; canonical double-entry-as-report query patterns |
| GnuCash (tax reports) | GPLv2 | Clean-room | Schedule-E-friendly reports; US tax-line mapping |
| ERPNext Reports (rent_roll, P&L by property, customer_statement, GST returns) | GPLv3 | Clean-room | Property-specific report shapes; tax-report templates |
| InvoicePlane | AGPLv3 (verify) | Study only | Invoice-focused; simpler than ERPNext |
| IRS Publication 527 + Schedule E + Form 1040 + 1099 instructions | Public domain | **Direct reference** | Tax form structure + line-item mapping |

**Highest-leverage:** react-pdf (MIT — direct dep) + Apache OFBiz templates (Apache 2.0 — borrow) + IRS public-domain forms.

### `blocks-work-*` — Projects + work-orders + repairs + remodels + maintenance schedules + contractors + deliverables + contracts

| Source | License | Posture | Notes |
|---|---|---|---|
| Apache OFBiz `workeffort` + `agreement` modules | Apache 2.0 | **Borrow** | WorkEffort + WorkEffortAssoc + WorkEffortFixedAsset + Agreement + AgreementItem + AgreementTerm — **canonical contract → deliverables model**. The single highest-value permissive borrow for this cluster. |
| OpenProject (work-packages + budgets) | GPLv3 | Clean-room | Mature; best reference for budget-vs-actual on remodels |
| Redmine (issues + projects + time-tracking) | GPLv2 | Clean-room | Mature, simpler than OpenProject; closer to "small remodel as project" sweet spot |
| Kanboard (minimal Kanban) | MIT | **Borrow** | Lightweight task-state machine; suitable for "repair-as-card" UI |
| Taiga (agile-flexible PM) | Mozilla MPL 2.0 | **Borrow per-file with attribution** | MPL is file-level copyleft — separate files stay clean |
| ERPNext Projects (`project`, `task`, `timesheet`, `activity_cost`) | GPLv3 | Clean-room | Property-aware project linking |
| GanttProject | GPLv3 | Clean-room | Schedule/cost build-up patterns |
| Documenso | GPLv3 + Enterprise | Clean-room for OSS parts | Modern e-signing UX |
| OpenSign | AGPLv3 | Study only | DocuSign-alternative signing workflow reference |
| DocAssemble | MIT | **Borrow** | Document automation / contract template generation |
| OpenMAINT (building maintenance + asset-deficiency → work-order) | AGPLv3 | Study only | Crossover with property/maintenance domain |

**Highest-leverage:** Apache OFBiz `workeffort` + `agreement` (Apache 2.0 — direct borrow) — its WorkEffort+WorkEffortFixedAsset pattern already encodes "project that operates on a specific asset" which is exactly the repair/remodel/maintenance shape.

### `blocks-people-*` — Staff + scheduling + leaves + onboarding + training + contacts + customers + leads/opportunities

| Source | License | Posture | Notes |
|---|---|---|---|
| Apache OFBiz `humanres` + `marketing` + `party` modules | Apache 2.0 | **Borrow** | Party + Position + Employment + PartyRole + PartyRelationship; SalesOpportunity + Campaign. **The single most important permissive source for this cluster** — Party model becomes the architectural anchor for the whole cluster. |
| OrangeHRM Community | GPLv3 | Clean-room | Full HRMS: employee + leave + recruitment + performance |
| Sentrifugo | GPLv2 | Clean-room | Simpler HRMS; closer to small-business shape |
| Frappe HR (`onboarding_template`, employee module) | GPLv3 | Clean-room | Step-based onboarding workflow; modern DocType structure |
| EspoCRM | GPLv3 | Clean-room | Modern PHP CRM; **cleanest DocType model in OSS CRM space** |
| SuiteCRM | AGPLv3 | Study only | Salesforce-like; feature-comprehensive reference |
| Mautic | GPLv3 + commercial | Clean-room | Marketing automation, lead scoring, drip campaigns |
| CiviCRM | AGPLv3 | Study only | Non-profit-flavored CRM |
| Cal.com | AGPLv3 + commercial | Study only | Modern scheduling stack reference |
| Easy!Appointments | GPLv3 | Clean-room | Appointment booking patterns |
| rrule (iCal recurrence rules) | BSD-2 | **Direct dep** | Industry-standard JS/TS port available |
| Mailtrain | MIT | **Borrow** | Newsletter for marketing-blast |
| Listmonk | AGPLv3 | Study only | Newsletter + email marketing; modern stack |

**Highest-leverage:** Apache OFBiz `humanres` + `party` (Apache 2.0 — borrow); EspoCRM (GPL — clean-room); rrule (BSD-2 — direct dep).

### `blocks-docs-*` — Policies + procedures + contract templates + marketing collateral (DAM) + wiki + signing workflow

| Source | License | Posture | Notes |
|---|---|---|---|
| Bookstack | MIT | **Borrow / direct candidate** | Books → chapters → pages, WYSIWYG + Markdown. **Excellent fit for policies/procedures wiki.** |
| Outline | BSD-3 | **Borrow** | Modern team wiki patterns |
| Mayan EDMS | Apache 2.0 | **Borrow** | Doc versioning + retention + tagging + OCR patterns |
| Apache OFBiz `content` module | Apache 2.0 | **Borrow** | Content + DataResource + ContentAssoc entities |
| DocAssemble | MIT | **Borrow** | Document automation / template + variable rendering |
| ResourceSpace (DAM) | BSD-3 | **Borrow** | Digital asset management for marketing/brand assets |
| Razuna (DAM) | GPLv3 | Clean-room | Alternative DAM reference |
| Documenso (e-signing) | GPLv3 + Enterprise | Clean-room for OSS parts | Modern e-signing UX |
| OpenSign | AGPLv3 | Study only | DocuSign-alternative signing workflow |
| Wiki.js | AGPLv3 | Study only | — |
| HedgeDoc | AGPLv3 | Study only | Collaborative markdown |

**Highest-leverage:** Bookstack (MIT — borrow/direct) for wiki/policies + Mayan EDMS (Apache 2.0 — borrow) for doc versioning + ResourceSpace (BSD-3 — borrow) for DAM.

### `blocks-storefront-*` — POS, ancillary sales, product catalog (DEFERRED per ADR 0088 §1)

| Source | License | Posture | Notes |
|---|---|---|---|
| ERPNext POS | GPLv3 | Clean-room | Touch-friendly POS integrated to accounting |
| Floreant POS | GPLv2 | Clean-room | Restaurant-focused but transferable |
| Odoo POS (community) | LGPLv3 | Clean-room | — |
| Stripe Terminal API | Proprietary | Study public API docs | Hardware integration reference |
| Square API | Proprietary | Study public API docs | — |

**Status:** No active workstream. Pick up when explicit demand surfaces.

### Sync / CRDT / Local-first substrate (cross-cluster)

Already chosen per ADR 0086 + ADR 0088. Listed for reference + future evaluation only.

| Source | License | Posture | Notes |
|---|---|---|---|
| Loro | Apache 2.0 | **Already chosen** | Per ADR 0086 (P3); chosen for peer-to-peer multi-device sync |
| SQLite | Public domain | **Already chosen** | Per ADR 0086 (P3); primary store under Path II |
| Tauri | MIT/Apache 2.0 | **Already chosen** | Per ADR 0086 (P3); application shell |
| tauri-plugin-stronghold | Apache 2.0 | **Direct dep** | Per W#60 P4 PR 1 plan; auth credential storage |
| iota_stronghold | Apache 2.0 | **Direct dep** | Underneath tauri-plugin-stronghold |
| Automerge | MIT | Comparison reference | Alternative CRDT |
| Y.js | MIT | Comparison reference | Alternative CRDT |
| DiffSync | Apache 2.0 | Comparison reference | — |
| Replicache | Commercial | Comparison reference only | Not OSI |

## Cross-cutting "highest-leverage permissive" sources

Across all clusters, three sources stand out as broad-application permissive borrows:

1. **Apache OFBiz** (Apache 2.0) — comprehensive ERP/CRM/accounting/HR/property/work entity models; appears in 5 of 7 cluster surveys. Direct port of entity definitions to TypeScript/SQLite with attribution is the canonical pattern.
2. **Apache 2.0 ecosystem generally** — Mayan EDMS, Apache Superset, Apache OFBiz all share the permissive license + mature entity modeling.
3. **MIT-licensed niche tools** — Bookstack (wiki), Kanboard (Kanban), DocAssemble (templates), rrule (recurrence), Mailtrain (newsletter), react-pdf (PDF rendering) — each fits a specific subsystem precisely and is directly usable as a dep or borrow.

## Cross-cutting "study only" copyleft references

These are the deepest knowledge sources but require strict clean-room discipline:

1. **ERPNext** (GPLv3) — comprehensive property/accounting/CRM/HR; informs nearly every cluster
2. **Apache OFBiz** (Apache 2.0) — note: OFBiz is ALSO Apache 2.0 so it's actually a borrow target, not just study (listed in both places intentionally — its breadth makes it relevant for clean-room study of design patterns AND direct code borrow)
3. **Beancount + ledger-cli** (GPL) — cleanest double-entry data models in OSS
4. **GnuCash** (GPLv2) — mature desktop accounting + tax mapping
5. **OpenMAINT** (AGPLv3) — canonical inspection → deficiency → work-order workflow
6. **OrangeHRM** (GPLv3) — comprehensive HRMS
7. **EspoCRM** (GPLv3) — modern CRM DocType patterns
8. **OpenProject / Redmine** (GPL) — mature project management

## Authoritative external references (public domain — direct reference)

1. **IRS Publication 527** — Residential Rental Property tax guidance
2. **IRS Schedule E** — Supplemental Income/Loss (rental real estate)
3. **IRS Form 1040** instructions
4. **IRS Form 1099-NEC + 1099-MISC** instructions — contractor payment reporting
5. **IRS Publication 925** — Passive Activity and At-Risk Rules
6. **IRS Publication 946** — How to Depreciate Property (MACRS)
7. **FinCEN BOI / Corporate Transparency Act** filing requirements
8. **NACHA Operating Rules** — ACH payment processing (membership required for full text; public summaries available)
9. **VA Code Title 55.1** — Virginia Residential Landlord and Tenant Act (VRLTA) (rental property law for the project's primary jurisdiction)
10. **Fair Housing Act** (42 USC §3601 et seq.) — federal protected classes + accommodation rules

## How this doc evolves

- When a new cluster is designed, add a section for that cluster
- When a new source is evaluated for any cluster, add a row to the relevant cluster section AND update the cross-cutting summary if appropriate
- When a source's license status changes upstream (rare but possible — some projects re-license over time), update the posture column AND audit any places we may have borrowed
- This doc is the authoritative single source of truth for the FOSS landscape Anchor touches; cluster Stage 02 design docs cite back to it
