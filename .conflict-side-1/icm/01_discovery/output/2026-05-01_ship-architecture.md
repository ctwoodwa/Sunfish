# Sunfish Ship Architecture — Consolidated Operator/Admin/Dev Experience Discovery

**Stage:** 01 Discovery
**Pipeline:** `sunfish-gap-analysis` (exit: **Approved Gap**)
**Date:** 2026-05-01
**Author:** XO research session
**Status:** Approved Gap — CO approved 2026-05-01
**Companion plan:** `~/.claude/plans/sunfish-ship-architecture-research.md` (UPF v1.2 Grade A; meta-UPF spot-check executed 2026-05-01)
**Intake:** `icm/00_intake/output/2026-05-01_ship-architecture-intake.md`
**Active workstream:** W#35 in `icm/_state/active-workstreams.md`

---

## 1. Executive Summary

Sunfish has an **Aspire-shaped consolidation gap**: configuration UX (W#34 Wayfinder), observability surfaces (logs / traces / metrics / health from `Microsoft.Extensions.Logging` + ADR 0049 audit), recovery + identity (ADR 0046 + W#32), per-tenant aggregation (W#29 Owner Web Cockpit), and provider-integration config all currently live as separate, partially-specified surfaces with no shared IA, no shared role/permission tuple, no shared design system, and no consistent navigation.

This discovery specifies the **Sunfish Ship Architecture** — a two-layer model (locations × responsibilities × deck-depth) composing as a permission tuple `(role, location, deck, action)`. It surveys cross-platform consolidated-dashboard precedents (Aspire / Grafana / Heroku / Supabase / Kubernetes / Hashicorp), maps each v1 location and each v1 role to current Sunfish coverage, identifies gaps, and queues 3–6 follow-on ADR intakes. The matrix is a *map*, not a *specification* — each follow-on ADR specifies the actual contract.

**Naming locked**: 7 v1 locations (Quarterdeck / Wayfinder / Engine Room / Tactical / Sick Bay / Ship's Office / Supply Office) + 2 v2 (Wardroom / Brig); 8 v1 responsibilities (Captain/XO + Department Heads ENG/NAV/TAC + Division Officers + Specialized Staff IDC/Scribe/SUPPO) + cross-cutting watch OOD/EOOW. **IDC framing** for Sick Bay (Pharmacy / Lab / Atmosphere monitor + Medevac to Bridge accelerator) replaces earlier "Surgeon" naming and matches Sunfish's local-first-with-structured-escalation operating pattern. **OOD pattern** as Sunfish primitive (currently-on-watch admin distinct from role assignment) promoted to its own follow-on ADR.

**Strategic value**: this discovery is the *substrate ADR* for every operator/admin/dev UI in Sunfish going forward. Without it, each downstream UI ADR re-derives navigation, permission, audit-tagging, role taxonomy, accessibility baseline, and cross-tenant composition rules ad-hoc — and the cohort metric (11+ substrate amendments needing council fixes per the W#33/W#34 lessons) compounds. With it, downstream ADRs inherit a coherent ship metaphor + permission tuple + design system baseline + watch-rotation primitive + first-aid contextual help; the only per-ADR work is the location-specific surface. The matrix is therefore *upstream of every Aspire-shaped surface Sunfish will ever ship* — it pays for itself across the 5–6 follow-on ADRs alone, and each future location ADR after that.

### Verdict table — Locations

| # | Location | Coverage | Confidence | Recommended next step |
|---|---|---|---|---|
| 5.1 | **Quarterdeck** | **Gap** | Medium | New ADR — entry-point + executive-summary surface; OOD-watch UI |
| 5.2 | **Wayfinder** *(W#34)* | **Specified** | High | Reference only — W#34 covers comprehensively |
| 5.3 | **Engine Room** | **Partial** | Medium-High | New ADR — Aspire-shaped observability surface composing on existing infrastructure |
| 5.4 | **Tactical** | **Gap** | Medium | New ADR — anomaly detection + threat-trigger UX |
| 5.5 | **Sick Bay** | **Partial** | Medium-High | New ADR — Sick Bay aggregation UI composing on ADR 0046 + W#32 substrate |
| 5.6 | **Ship's Office** | **Partial** | Medium-High | New ADR — content/document aggregation surface |
| 5.7 | **Supply Office** *(deferred)* | **Gap** | Low | Deferred to Phase 2 commercial work; defer ADR |

### Verdict table — Responsibilities

| # | Role | Coverage | Confidence | Recommended next step |
|---|---|---|---|---|
| 6.1 | **Captain / XO** | Partial | Medium-High | Reference only — ADR 0046 + 0032 cover identity primitives; role taxonomy emerges via shared-design-system ADR |
| 6.2 | **Department Heads (ENG / NAV / TAC)** | Gap | Medium | Folded into shared-design-system ADR (role taxonomy) |
| 6.3 | **Division Officers (rotating)** | Gap | Medium | Folded into shared-design-system ADR (role taxonomy + rotation pattern) |
| 6.4 | **IDC ("Doc")** | Partial | Medium-High | New ADR — Sick Bay aggregation includes IDC role definition (overlap with §5.5 recommendation) |
| 6.5 | **Scribe** | Gap | Medium | Folded into shared-design-system ADR + Ship's Office ADR |
| 6.6 | **SUPPO** *(deferred)* | Gap | Low | Phase 2 commercial work |
| 6.7 | **OOD / EOOW** *(cross-cutting watch)* | Gap | Medium | **New ADR — OOD pattern + Watch rotation** (confirmed for promotion) |

### Verdict — Cross-axis primitives

| § | Primitive | Coverage | Recommended next step |
|---|---|---|---|
| 7.1 | Permission tuple `(role, location, deck, action)` | Gap | Folded into shared-design-system ADR + W#34 ~ADR 0068 |
| 7.2 | Watch rotation (OOD/EOOW) | Gap | New ADR — OOD pattern + Watch rotation (overlaps §6.7) |
| 7.3 | Stretcher-bearer pattern | Gap | Documented here; emerges concretely as cross-department on-call escalation matures |
| 7.4 | First-aid baseline (universal contextual help) | Gap | Folded into shared-design-system ADR |

**Summary statistic**: 1 Specified location + 3 Partial + 3 Gap (location side); 1 Partial role + 5 Gap + 2 deferred (role side). 4 cross-axis primitives, all Gap. Net: **3 confirmed follow-on ADRs** (Quarterdeck entry-point + Engine Room observability + Tactical anomaly-detection) **+ 2 cross-cutting ADRs** (OOD pattern + Watch rotation as one bundled intake; Shared design system) **+ amendments** to ADR 0046 (Sick Bay aggregation) + ADR 0036 (Quarterdeck pulse) + ADR 0049 (OOD-tagging). Total: 5–6 follow-on intakes per Phase 4.

**Stage 1.5 hardening note**: §7 (shared design system) + per-location §5 surfaces received a WCAG / a11y adversarial pass. Findings folded in: WCAG 2.2 AA conformance baseline mandatory at the design-system level; native platform a11y APIs (UIA / NSAccessibility / UIAccessibility / AccessibilityNodeInfo) per cross-platform OS; EN 301 549 procurement compliance for Bridge EU tenants.

---

## 2. Research Question

CO directive (paraphrased from 2026-05-01 brief): *"What if we consolidated all the operator/admin/dev experiences — like Aspire — where logs, telemetry, traces, configurations, settings live in one place, or in multiple coordinated experiences with shared styling at minimum? Plus: scaling access by complexity (executive summary → engine-room detail) with permission-gated departments. Plus naming via naval / submarine vocabulary cohesive with the existing org chart."*

The matrix produced here answers: "for any Sunfish operator/admin/dev concern, what location does it live in, who has authority over it, and what's the depth of detail accessible at each role tier?" It does not specify the actual UI contracts — those become downstream ADRs (Quarterdeck entry-point ADR, Engine Room observability ADR, OOD ADR, Shared Design System ADR, etc.).

---

## 3. Method

### 3.1 Verdict-tag scheme

| Tag | Meaning | What's required |
|---|---|---|
| **Specified** | Predecessor (ADR or prior research) covers the location/role substantively, including UX surface where applicable | Citation + one-line definition + "reference only" recommendation |
| **Partial** | Infrastructure / data model is specified; UX surface or role-specifics missing | Citation of what IS covered + named gap + recommended new ADR or amendment |
| **Gap** | No current artifact covers the concern | Industry prior-art (1–3 references) + sketch of contract shape + new ADR recommendation |

### 3.2 Per-location §5 schema (6-field)

Every location's §5 subsection follows: **Coverage tag** / **Confidence** / **Recommended next step** header, then **Gate definition** / **Examples** / **Current coverage** / **What's missing** / **Recommendation**.

### 3.3 Per-role §6 schema (6-field, symmetric per meta-UPF)

Every role's §6 subsection follows: **Coverage tag** / **Confidence** / **Recommended next step** header, then **Role definition** / **Primary location** / **Responsibilities + authority gradient** / **Current coverage + what's missing** / **Recommendation**.

### 3.4 Cross-axis primitives §7

The four cross-cutting primitives (permission tuple / watch rotation / stretcher-bearer / first-aid baseline) get tighter treatment — each ~250-350 words covering definition + examples + composition with existing Sunfish substrate + recommendation.

### 3.5 What's in scope

The two-layer architecture (locations × responsibilities); the v1 location + role inventory; the cross-axis primitives; the consolidated-dashboard appendix. Industry prior-art surveys for ~5 cross-platform consolidated dashboards.

### 3.6 What's out of scope

- Concrete UI contracts (page layouts, component specs) — deferred to per-location ADRs
- Concrete role/permission DSL — deferred to W#34 ~ADR 0068 + the shared-design-system ADR
- Per-feature UI assignments — per-block module work
- W#29 Owner Web Cockpit reframing — acknowledged but full integration deferred to a future amendment
- Phase 2 commercial Supply Office UX — deferred until Phase 2 commercial workstream matures

---

## 4. Sunfish Substrate Recap

### 4.1 Two-layer architecture

The user's load-bearing insight: **locations and responsibilities are orthogonal axes** that compose with deck-depth to define permissions:

> **Where** (location) × **Who** (responsibility) × **How deep** (deck) = the cell of the experience a user can access.

A location is *where* features live (the IA / UI structure); a responsibility is *who* operates them (the role / accountability model). Permissions compose as a tuple: `(role, location, deck, action)`.

**Decks** *(within each location)*:
- **Top deck** — executive summary; status indicators; KPI cards. Default for casual users.
- **Main deck** — operational surfaces. Functional read/write.
- **Engineering deck** — internals; logs; raw events; rarely-touched config.
- **Below-the-waterline** *(optional)* — destructive/irreversible operations; always permission-gated; loud audit.

### 4.2 Locked v1 vocabulary

**Locations** (7 v1 + 2 v2):

| # | Location | Sub-rooms |
|---|---|---|
| 1 | **Quarterdeck** | (flat) |
| 2 | **Wayfinder** *(W#34)* | Helm, Atlas, Standing Orders log, Radio Room, Periscope |
| 3 | **Engine Room** | Main Propulsion, Electrical, Damage Control, QA Workshop |
| 4 | **Tactical** | Sonar Room, Lookout, Fire Control |
| 5 | **Sick Bay** | Pharmacy, Lab, Atmosphere monitor |
| 6 | **Ship's Office** | (flat) |
| 7 | **Supply Office** *(Phase 2 deferred)* | (flat) |
| 8 | **Wardroom** *(v2)* | (flat) |
| 9 | **Brig** *(v2)* | (flat) |

**Responsibilities** (8 v1 + cross-cutting watch): Captain / XO at the tenant-ownership tier; Department Heads ENG (Engine Room) / NAV (Wayfinder) / TAC (Tactical); Division Officers MPA / DCA / Comms / Sonar / Electrical / QA — rotating through assignments; Specialized Staff IDC ("Doc," lives in Sick Bay) / Scribe (Ship's Office) / SUPPO (Supply Office, Phase 2); cross-cutting watch OOD (Quarterdeck, current shift) / EOOW (Engine Room, current shift).

### 4.3 Boundary disambiguation: W#33 / W#34 / W#35

This is load-bearing — three workstreams use overlapping vocabulary:

| Workstream | Concept | Scope |
|---|---|---|
| **W#33 Mission Space** | The *envelope* — "what your device can do given hardware × user × jurisdiction × runtime × form-factor × commercial-tier dimensions" | Capability matrix; 10 dimensions; ADRs 0062 + 0063 specify runtime + install-UX |
| **W#34 Wayfinder** | The *configuration system* — Helm + Atlas + Standing Orders log | One of 7 v1 locations in the Ship Architecture; specifies what's *inside* the Navigation department |
| **W#35 Ship Architecture** *(this discovery)* | The *whole ship* — locations × responsibilities × deck-depth | Specifies how Wayfinder + 6 other locations compose; specifies role hierarchy + watch rotation + permission tuple |

Reads cleanly: *"In the Sunfish ship, the Wayfinder is the Navigation department where you set the course (Atlas) and steer the present (Helm). The Engine Room makes the ship go forward. Tactical watches for trouble. The IDC keeps the crew healthy. The OOD stands the Quarterdeck. The whole ship operates within the Mission Space envelope."*

### 4.4 IDC framing for Sick Bay

The Independent Duty Corpsman model maps onto Sunfish's recovery/identity surface with unusual precision — captures the local-first-with-structured-escalation pattern that no other naval medical role does. Detailed in §5.5 and §6.4.

---

## 5. Per-location evaluation

### 5.1 — Quarterdeck

**Coverage tag:** Gap
**Confidence:** Medium
**Recommended next step:** New ADR — Quarterdeck entry-point + executive-summary surface + OOD-watch UI.

#### Gate definition

The top-level entry point a user "reports aboard" upon opening the Sunfish ship. Executive summary across all departments the user has access to; status indicators; OOD-watch banner showing "you have the deck"; permission-gated descent links to other locations.

#### Examples

- Tenant owner opens Anchor → lands on Quarterdeck → sees: Mission Envelope summary (per ADR 0062 — "your device supports HSM-backed keys; mesh-VPN unavailable"), sync state across teams, recent Standing Orders, alert ticker (from Tactical's Lookout), recent audit-event ticker (from ADR 0049)
- OOD signs in for their watch → Quarterdeck banner: "You have the deck — 14:30 UTC handover from prior OOD." Pending Standing Orders awaiting their approval surface here.
- Operator checking ship health → top-deck KPI cards: Engine Room green, Tactical yellow (1 alert), Sick Bay green, Ship's Office green

#### Current coverage

**No current artifact covers the Quarterdeck as a Sunfish concern.** Adjacent: ADR 0036 (5 sync states with ARIA roles + aria-live policies — *partial UI substrate*); paper §13.2 (AP/CP visibility table with staleness thresholds — *partial UX precedent*); ADR 0062 Mission Space Negotiation Protocol (provides `MissionEnvelopeProvider` for the live-state widget); ADR 0049 audit trail (provides the recent-events feed).

#### What's missing (genuine gap)

- **Quarterdeck data model** — what cards / widgets render; sourcing rules per widget; refresh cadence
- **OOD-watch UI** — banner display; watch-handover flow; pending-actions inbox. *Per Stage 1.5 a11y*: banner is a programmatic landmark (`role="banner"` / native equivalent), not just visual; watch-handover announces via `aria-live="polite"`.
- **Permission-gated descent links** — how the Quarterdeck shows/hides departments per role; access denials surface through the §7.1 `PermissionDecision` contract (not blank panes)
- **Alert ticker** *(receives feed from Tactical Lookout)* — motion auto-pauses on `prefers-reduced-motion` per SC 2.3.3; high-priority alerts use `aria-live="assertive"` / equivalent native posting; ticker pause-control is keyboard-reachable
- **KPI cards** — status conveyed via text + icon, never color alone (SC 1.4.1); each card has accessible name including the metric value (e.g., *"Engine Room: green, 0 alerts"*)
- **Deep-link search** — combobox pattern per ARIA APG; result list navigable with arrow keys; live result count announced
- **Cross-tenant Quarterdeck** — for users with access to multiple tenants (Phase 2 commercial scope), how the Quarterdeck composes across tenants

#### Recommendation

New ADR — **Quarterdeck entry-point surface**. Scope: top-deck data model + widget contract + OOD-watch banner + permission-gated descent + deep-link search + cross-tenant composition (Phase 2 placeholder). Cross-references ADRs 0036 (sync states) + 0049 (audit) + 0062 (Mission Envelope). Composes on the Shared Design System ADR (~ADR new). Effort: medium-large (~12-18h authoring + council review including WCAG/a11y subagent).

---

### 5.2 — Wayfinder *(W#34 Specified)*

**Coverage tag:** Specified
**Confidence:** High
**Recommended next step:** Reference only — W#34 covers comprehensively.

#### Gate definition

Per the W#34 discovery, Wayfinder is the *Navigation department* of the ship — the configuration system. Sub-rooms: Helm (live-state pane), Atlas (deep-config UI), Standing Orders log (event stream), Radio Room (federation + messaging), Periscope (peer discovery + topology).

#### Current coverage

`icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` (W#34; 7,616 words; WCAG-hardened) covers the 8 configuration layers + cross-platform OS UX + pro-tool/SaaS UX. 5 follow-on intakes filed: ~ADR 0065 (Wayfinder system + Standing Order bundled) / ~ADR 0066 (Helm + identity Atlas) / ~ADR 0067 (Atlas integration-config) / ~ADR 0068 (security policy) / ADR 0009 amendment (5th-concept extension).

#### Recommendation

No new artifact. Reference W#34 outputs as the authoritative source for Wayfinder's internal structure. The Ship Architecture's §4.3 boundary disambiguation governs how Wayfinder composes with the other 6 v1 locations.

---

### 5.3 — Engine Room

**Coverage tag:** Partial
**Confidence:** Medium-High
**Recommended next step:** New ADR — Engine Room observability surface (Aspire-shaped) composing on existing infrastructure.

#### Gate definition

Technical operations department. Sub-rooms: Main Propulsion (CRDT engine + sync daemon), Electrical (storage + quotas + CRDT growth), Damage Control (recovery procedures + manual CRDT surgery + quarantine override), QA Workshop (test infrastructure + council-review surface + ADR-template enforcement).

#### Examples

- Operator inspects sync-daemon health: Main Propulsion engineering deck shows live event throughput, gossip-cycle counts, peer-list age
- Engineer reviews CRDT growth across all teams: Electrical engineering deck shows per-team byte counts, snapshot ages, compaction-eligibility flags
- SRE responds to a quarantine: Damage Control's below-the-waterline view shows the quarantined records, manual-override action available with elevated audit

#### Current coverage

**Infrastructure layer is Partial.** ADR 0028 (CRDT engine + amendments A1–A8) specifies the engine + sync semantics; `Microsoft.Extensions.Logging` provides structured logs; ADR 0049 audit trail provides queryable history; ADR 0036 sync states surface in UI primitives. **What's missing**: a unified UI surface that aggregates these into the Aspire-shaped observability dashboard — logs / traces / metrics / health / topology in one place.

#### What's missing

- **Aggregate observability UI** — per-resource log/trace/metric viewer (Aspire-style)
- **Tracing infrastructure** — OpenTelemetry-shaped tracing not yet specified
- **Metrics surface** — counters / gauges / histograms — not specified
- **CRDT-growth gauge UI** — per-team / per-tenant growth tracking (data exists; no UI)
- **Damage Control flows** — quarantine override / manual surgery UX
- **QA Workshop UI** — test-runner / coverage / council-review surface

#### Aspire-shaped does NOT mean Aspire-equivalent for a11y *(Stage 1.5 finding)*

Aspire's own a11y has known gaps (log-table virtualization breaks screen-reader row context; trace timeline is largely visual; metric charts lack accessible alternatives). Engine Room MUST exceed Aspire's baseline:

- **Log table** is an accessible data grid with row/column header semantics + virtualization that preserves SR context (per WCAG SC 1.3.1 + 4.1.2)
- **Trace timeline** ships with accessible alternative — text/table representation toggle (SC 1.1.1, 1.4.5)
- **Every metric chart** has a `<table>` data alternative
- **Damage Control below-the-waterline destructive actions** require keyboard-confirmable elevated dialog with explicit accessible naming of the action + consequences

#### Recommendation

New ADR — **Engine Room observability surface**. Scope: Aspire-shaped log/trace/metric/health viewer (with explicit a11y exceedance over Aspire); OpenTelemetry integration; CRDT-growth gauge; Damage Control flows; QA Workshop UI. Cross-references ADRs 0028 + 0036 + 0049. Composes on the Shared Design System ADR. Effort: large (~16-24h authoring + council review including WCAG/a11y subagent).

---

### 5.4 — Tactical

**Coverage tag:** Gap
**Confidence:** Medium
**Recommended next step:** New ADR — Tactical anomaly detection + threat-trigger UX.

#### Gate definition

Monitoring + threat awareness department. Sub-rooms: Sonar Room (acoustic data — anomaly detection), Lookout (visual surveillance — high-priority alert ticker), Fire Control (incident response — runbooks + escalation paths).

#### Examples

- Sonar detects anomaly: spike in failed-decryption events across multiple tenants → Sonar Room alert posts to Lookout's high-priority ticker
- Lookout surfaces alert on Quarterdeck: "Tactical alert — investigate Sonar Room"
- Operator descends to Fire Control: runbooks for the incident type; one-click contact for SRE escalation; audit-trail query helpers

#### Current coverage

**No current artifact covers the Tactical department.** Adjacent: ADR 0043 (unified threat model — covers OSS-project supply chain, NOT tenant runtime threats); ADR 0049 (audit trail — provides the substrate for triggers but not the trigger logic); W#28 inquiry-defense layers (per-block, not cross-cutting); W#22 leasing-pipeline FCRA dispute window (domain-specific).

#### What's missing (genuine gap)

- **Anomaly detection rule engine** — what fires alerts, on what signals, at what thresholds
- **Alert routing** — which alerts go to Lookout (high priority) vs Sonar Room (informational). *Per Stage 1.5 a11y*: alert posting via assertive live region; non-color severity encoding; reduced-motion fallback for any pulsing/flashing severity indicators (SC 2.3.1 flash-threshold).
- **Incident response surface** — runbooks; escalation paths; audit-trail-query helpers. *A11y*: stepwise structure with proper heading hierarchy + skip-links; one-click escalation reachable via keyboard with confirmed activation; long forms use `aria-describedby` for help text and `aria-invalid` on validation failure (SC 3.3.1, 3.3.2).
- **Cross-tenant Tactical** — for Bridge accelerator operators, anomalies across tenants
- **Threat-trigger Standing Orders** — when an anomaly fires, what auto-actions issue (rate-limit increase; quarantine flag; notify OOD)

#### Industry prior-art

- **Datadog / New Relic / Honeycomb** — alert-rule engines + incident-response timelines
- **PagerDuty / Opsgenie** — escalation and on-call rotation
- **Sigma / Sigma Rules** — anomaly-detection rule format

#### Recommendation

New ADR — **Tactical anomaly detection + threat-trigger surface**. Scope: rule engine for detection; alert-routing taxonomy; incident-response UI; threat-trigger Standing Order shapes. Cross-references ADRs 0043 + 0049 + W#34 ~ADR 0068 (security policy). Effort: medium-large (~12-18h authoring + council review including security-engineering subagent).

---

### 5.5 — Sick Bay

**Coverage tag:** Partial
**Confidence:** Medium-High
**Recommended next step:** New ADR — Sick Bay aggregation UI composing on ADR 0046 + W#32 substrate (overlaps §6.4 IDC role).

#### Gate definition

Recovery + identity + key handling department, operated by the IDC ("Doc"). Sub-rooms map to real-Navy IDC sub-functions: **Pharmacy** (key vault — EncryptedField repository per ADR 0046-A2 + W#32; recovery-key bundles), **Lab** (diagnostics — CRDT health probes; attestation verification), **Atmosphere monitor** (runtime health — sync state, quota gauge, system pulse).

#### Examples

- IDC inspects Pharmacy: list of all encrypted-field-protected records per tenant; key-rotation schedule; pending recovery-key distributions
- Lab runs diagnostic: ad-hoc health probe ("is the sync daemon responsive?"); attestation verification ("is this peer truly attested?")
- Atmosphere monitor: live system pulse — CRDT growth rate, sync queue depth, error rate
- IDC stabilizes incident → Medevac to Bridge: encrypted support channel to Sunfish staff

#### Current coverage

- **ADR 0046 + 0046-a1** — encrypted field; spouse-recovery; historical-keys projection
- **ADR 0046-A2 + W#32** — `EncryptedField` + `IFieldDecryptor` substrate (built 2026-04-30)
- **ADR 0049** — audit-trail substrate
- **ADR 0036** — sync state UI primitives
- **ADR 0061** — managed-relay (Medevac path to Bridge)

The **infrastructure layer is solid**; what's missing is the **aggregation UI** that surfaces these as a unified Sick Bay department.

#### What's missing

- **IDC Atlas surface** — Pharmacy / Lab / Atmosphere monitor UI
- **Recovery-contact UX** — enrollment, removal, verification (composes ADR 0046 spouse-recovery). *Per Stage 1.5 a11y*: trust decisions never rely on color/icon alone (SC 1.4.1); verification status is text-equivalent (e.g., *"Recovery contact verified — Spouse, last-confirmed 2026-04-15"*).
- **Key-rotation UX** — when does IDC trigger; rotation-window UX; pending-compromise warnings
- **Key-fingerprint display** — *Per Stage 1.5 a11y*: monospace + grouped chunks with explicit pronunciation hints (`aria-label="fingerprint, group 1 of 8: A B 1 2"`); never image-only
- **Medevac flow UX** — escalating to Bridge encrypted support channel. *A11y*: encrypted-channel state changes announced via live region; consent dialogs accessible-name the destination + scope explicitly
- **Stretcher-bearer cross-training** — DCA + MPA + Comms Officer + Sonar Officer can be paged for first response (formalizes the cross-axis primitive §7.3)
- **First-aid contextual help** — every user surface inherits IDC-level baseline help (formalizes §7.4)

#### Recommendation

New ADR — **Sick Bay aggregation UI + IDC role definition**. Scope: Pharmacy/Lab/Atmosphere monitor UI; recovery-contact UX; key-rotation flow; Medevac escalation; stretcher-bearer paging contract; first-aid baseline integration. Cross-references ADRs 0036 + 0046 + 0046-a1 + 0046-A2 + 0049 + 0061; composes on Shared Design System + W#34 ~ADR 0066 (Helm + identity Atlas — overlapping scope to disambiguate during authoring). Effort: medium-large (~12-18h).

---

### 5.6 — Ship's Office

**Coverage tag:** Partial
**Confidence:** Medium-High
**Recommended next step:** New ADR — Ship's Office content/document aggregation surface.

#### Gate definition

Content management department, operated by the Scribe. Holds structured document content (W9s, lease versions, signature envelopes, bundle manifests, templates, kitchen-sink seeds, apps/docs content). Distinct from Wayfinder's Atlas (which holds *configuration*) — Ship's Office holds *documents and admin records*.

#### Examples

- Scribe browses W9 documents per tenant: list view, search by date / vendor / status, export options
- Reviewing a lease document version: full-text view, signature-envelope rendering, audit-trail of versions
- Managing apps/docs content: editor, preview, publish flow
- Bundle-manifest authoring: per-bundle config schema editor (composes ADR 0007 + ADR 0009)

#### Current coverage

- **W#21 SignatureEnvelope** — kernel-signatures substrate (built 2026-04-30)
- **W#22 LeaseDocumentVersion** — append-only versioning + per-party signatures (built 2026-04-30)
- **W#18 W9Document** — vendor onboarding W9 storage with EncryptedField TIN
- **ADR 0007** — bundle manifest schema
- **ADR 0055 + 0056** — dynamic forms substrate + Foundation.Taxonomy (informs templates)

The **document substrates are solid**; what's missing is the **aggregation surface** that surfaces these as a unified Ship's Office.

#### What's missing

- **Cross-document-type browse + search UI**
- **Scribe role definition** + permission tuple
- **Template + bundle-manifest authoring UX**
- **Apps/docs content editing surface**
- **Document-version diff UX** (composes Stripe-style diff-preview from W#34 §B.3)

#### Recommendation

New ADR — **Ship's Office content aggregation surface**. Scope: cross-document-type UI; Scribe role definition; template authoring; bundle-manifest authoring; apps/docs editing; document-version diff. Cross-references W#21 + W#22 + W#18 + ADRs 0007 + 0055 + 0056. Composes on Shared Design System. Effort: medium (~10-14h).

---

### 5.7 — Supply Office *(deferred)*

**Coverage tag:** Gap
**Confidence:** Low
**Recommended next step:** Defer ADR until Phase 2 commercial work matures.

#### Gate definition

Non-engineering admin: billing, subscriptions, customer support, legal/compliance, payroll for Sunfish-staff (if applicable). Operated by SUPPO (Supply Officer — uniquely *not Sunfish-internal-qualified* in the real-Navy analog; matches the "non-technical admin" framing).

#### Why deferred

Phase 2 commercial work (W#5 Phase 2 commercial MVP + payment substrate per ADR 0051 + messaging per ADR 0052) is in flight but not yet at the maturity to specify Supply Office UX. Premature ADR risks bad-fit-for-actual-needs. Defer until Phase 2 commercial work names concrete UX requirements (likely 6–12 months out).

#### Recommendation

**Track as deferred.** Re-evaluate when Phase 2 commercial work hits its first user-facing milestone (likely a billing UX or subscription-management UX). Until then, no Sunfish-internal Supply Office surface — admin tasks stay scattered across per-package ad-hoc UIs.

---

## 6. Per-responsibility evaluation

### 6.1 — Captain / XO *(tenant ownership)*

**Coverage tag:** Partial · **Confidence:** Medium-High · **Recommended next step:** Reference only.

**Role definition**: Tenant owner (BDFL) and deputy. Have full visibility into their tenant; can read all departments at all decks; can issue any Standing Order; can promote/demote roles.

**Primary location**: Quarterdeck (full visibility from the entry point).

**Responsibilities + authority gradient**: Captain has unilateral authority on tenant-config policy (W#34 ~ADR 0068); XO can act as Captain in their absence; both can designate OOD; both can approve Standing Orders that require multi-actor approval.

**Current coverage + what's missing**: ADRs 0046 + 0032 + 0046-a1 cover the cryptographic identity primitives (root keypair, per-team subkey derivation, role-key wrapping). What's missing: the *role* of Captain/XO is implicit (tenant-owner), not explicitly named in any current ADR. The Shared Design System ADR will name it canonically.

**Recommendation**: No standalone ADR; folded into Shared Design System ADR's role-taxonomy section.

---

### 6.2 — Department Heads *(ENG / NAV / TAC)*

**Coverage tag:** Gap · **Confidence:** Medium · **Recommended next step:** Folded into Shared Design System ADR.

**Role definition**: Three Department Head roles, each owning a major department:
- **ENG** (Engineer Officer) → Engine Room
- **NAV** (Navigator / Operations Officer) → Wayfinder *(W#34 ~ADR 0066 specifies most of NAV's authority)*
- **TAC** (Tactical Officer) → Tactical *(per-domain-specific authority surfaced via the Tactical ADR §5.4)*

**Primary location**: each Department Head's home location. Cross-department visibility at top deck only.

**Responsibilities + authority gradient**: full read/write authority within their department; can issue Standing Orders affecting their department's policy; can promote Division Officers; can stand watch as OOD. Subordinate to Captain/XO.

**Current coverage + what's missing**: no explicit Department-Head role taxonomy in current ADRs. What's missing: canonical role-name registration; permission-tuple grants per role; multi-tenant Department-Head assignment (which user is ENG for tenant X? do roles cross tenants?).

**Recommendation**: Folded into Shared Design System ADR's role-taxonomy section.

---

### 6.3 — Division Officers *(rotating)*

**Coverage tag:** Gap · **Confidence:** Medium · **Recommended next step:** Folded into Shared Design System ADR.

**Role definition**: Junior officers who rotate through assignments to learn the boat — MPA (Main Propulsion Assistant), DCA (Damage Control Assistant), Comms Officer, Sonar Officer, Electrical Officer, QA Officer. Each has primary scope within a sub-room of their Department Head's location.

**Primary location**: sub-room within Department Head's location (e.g., MPA's primary location is Engine Room → Main Propulsion sub-room).

**Responsibilities + authority gradient**: read/write within their sub-room; can issue Standing Orders affecting their sub-room scope; **rotation pattern** is novel — Division Officers cycle through assignments over time. Subordinate to their Department Head.

**Current coverage + what's missing**: no current artifact specifies role rotation in Sunfish. What's missing: rotation-pattern primitive (similar to OOD watch but with longer cycles — months, not hours); Standing Order shape for rotation transitions; permission-handover UX.

**Recommendation**: Folded into Shared Design System ADR's role-taxonomy + rotation-pattern section. Could split into a separate "rotation primitive" ADR if scope grows during authoring.

---

### 6.4 — IDC ("Doc")

**Coverage tag:** Partial · **Confidence:** Medium-High · **Recommended next step:** New ADR — Sick Bay aggregation UI (overlap with §5.5).

**Role definition**: Independent Duty Corpsman — single specialized role with advanced training; operates Sick Bay autonomously without a doctor on site. Real-Navy parallel: senior enlisted with PA-equivalent training; manages pharmacy + lab + radiation health + atmosphere monitoring; medevacs to specialist when needed.

**Primary location**: Sick Bay (Pharmacy + Lab + Atmosphere monitor).

**Responsibilities + authority gradient**:
- Pharmacy = key vault management (composes ADR 0046-A2 + W#32)
- Lab = diagnostic-probe authority
- Atmosphere monitor = runtime-health observation
- Stabilize incidents (recovery primitives)
- Medevac to Bridge accelerator (escalation to Sunfish-staff specialists)
- Remote consultation via Bridge encrypted channel
- Trains stretcher bearers (DCA + MPA + Comms + Sonar) for first response
- Universal first-aid baseline (every user surface inherits contextual help)

Subordinate to Captain/XO; coordinates with Department Heads but operates independently.

**Current coverage + what's missing**: substrate solid (ADRs 0046 + 0046-a1 + 0049 + W#32); UX surface missing — see §5.5.

**Recommendation**: covered by §5.5 Sick Bay aggregation ADR. The role definition lives in the Shared Design System ADR's role-taxonomy section but the operational UX lives in the Sick Bay ADR.

---

### 6.5 — Scribe

**Coverage tag:** Gap · **Confidence:** Medium · **Recommended next step:** Folded into Shared Design System ADR + Ship's Office ADR (§5.6).

**Role definition**: Ship's clerk — content management; W9 docs, lease versions, signature envelopes, bundle manifests, templates, apps/docs. Real-Navy parallel: Yeoman; adopted "Scribe" naming for Sunfish to avoid collision with the book-repo session-role Yeoman.

**Primary location**: Ship's Office.

**Responsibilities + authority gradient**: full read/write authority on document content within tenant; cannot modify Sunfish-internal substrate (just like real-Navy Yeoman is admin, not engineering). Subordinate to Captain/XO.

**Current coverage + what's missing**: no current artifact names the role. What's missing: canonical role registration; permission-tuple grants; UX surface (covered by §5.6).

**Recommendation**: role definition in Shared Design System ADR; UX in Ship's Office ADR (§5.6).

---

### 6.6 — SUPPO *(deferred)*

**Coverage tag:** Gap · **Confidence:** Low · **Recommended next step:** Phase 2 commercial work.

**Role definition**: Supply Officer — non-Sunfish-qualified admin (billing, subscriptions, customer support). Real-Navy parallel: SUPPO is the only officer on a sub who is not nuclear-qualified.

**Primary location**: Supply Office (Phase 2 deferred).

**Responsibilities + authority gradient**: TBD; emerges from Phase 2 commercial work.

**Recommendation**: defer until Phase 2 commercial maturity. Reference §5.7.

---

### 6.7 — OOD / EOOW *(cross-cutting watch)*

**Coverage tag:** Gap · **Confidence:** Medium · **Recommended next step:** **New ADR — OOD pattern + Watch rotation** (confirmed for promotion).

**Role definition**: Officer of the Deck (OOD) — currently-on-watch admin standing the Quarterdeck. Engineering Officer of the Watch (EOOW) — currently-on-watch engineer for the Engine Room. Cross-cutting: OOD/EOOW are *temporally rotating* assignments distinct from Department-Head role; any qualified officer can be OOD on their watch.

**Primary location**: Quarterdeck (OOD) / Engine Room (EOOW), both *for the duration of their shift*.

**Responsibilities + authority gradient**:
- OOD logs all comings/goings on the Quarterdeck during their shift
- All Standing Orders issued during the watch are tagged `IssuedDuringWatch: OodId`
- Watch handover is itself a Standing Order: `OOD.WatchTransferred(from, to, at)`
- OOD can approve Standing Orders awaiting multi-actor approval (during their watch)
- EOOW analog for engineering domain

Composes with W#34 ~ADR 0068 security policy (who can be OOD; minimum factors). Composes with Phase 2 commercial multi-actor delegation (which spouse/co-owner is on watch today?).

**Current coverage + what's missing**: no current artifact specifies the OOD/EOOW pattern. The pattern is genuinely novel for Sunfish.

**Recommendation**: **New ADR — OOD pattern + Watch rotation primitive**. Scope: OOD designation as primitive distinct from role assignment; watch-handover Standing Order shape; per-Standing-Order tagging; audit emission; EOOW analog; composition with security policy + multi-actor delegation. Effort: medium (~10-14h); cross-cutting → council-review-heavy.

---

## 7. Cross-axis primitives

### 7.1 — Permission tuple `(role, location, deck, action)`

The composition rule for the two-layer architecture. A user's effective experience is the join of: roles they hold × locations those roles grant access to × decks within those locations the role can descend to × actions allowed at each cell. The grant/deny decision is a function over those tuples.

**Composition with Sunfish substrate**:
- Roles compose on ADR 0046 role-key wrapping + ADR 0032 per-team subkeys
- Locations are static UI structure (specified by per-location ADRs)
- Decks are static-per-room (specified by per-location ADRs)
- Actions are an enum (read / write / issue-standing-order / approve / promote-role / etc.)

**Denial accessibility contract** *(per Stage 1.5 hardening pass)*: `IPermissionResolver` returns `PermissionDecision { Granted | Denied(reason, remediation) }` — never a bare `bool`. A denial surfaces (a) which role grant is missing, (b) who can grant it (named contact), (c) an accessible escalation action, (d) is announced as a status message via `aria-live` / platform-equivalent. Blank screens or generic 403 errors violate WCAG SC 3.3.1 (Error Identification) + are inaccessible to screen-readers parsing landmarks; downstream UI ADRs must surface denials through this contract.

**Recommendation**: folded into Shared Design System ADR. Specifies the data model + resolution algorithm + `IPermissionResolver` DI surface + `PermissionDecision` denial shape.

### 7.2 — Watch rotation (OOD / EOOW)

Cross-cutting — overlaps §6.7. The watch-rotation primitive specifies that OOD/EOOW assignments rotate independently of Department-Head assignment, are temporally bounded (e.g., 8-hour shifts), and trigger Standing Orders on handover.

**Composition with Sunfish substrate**:
- Composes with ADR 0049 audit (handover Standing Order is an audit event)
- Composes with W#34 ~ADR 0068 security policy (who can be OOD)
- Composes with Phase 2 commercial multi-actor delegation

**Recommendation**: covered by §6.7 OOD ADR.

### 7.3 — Stretcher-bearer pattern

Cross-trained Division Officers from adjacent departments respond to first-aid emergencies in any department. Real-Navy: DCA + MPA + Comms + Sonar all train as first responders so the IDC isn't alone during combat. Sunfish parallel: a permission grant that allows a Division Officer to perform *first-response actions* in a department they don't normally have authority over, conditional on an active alert/incident.

**Composition with Sunfish substrate**:
- Conditional permission grant — composes with permission tuple (§7.1)
- Triggered by Tactical alert (§5.4) or Sick Bay emergency (§5.5)
- Audit emission — every stretcher-bearer action is loudly logged

**Recommendation**: documented here as a primitive; full ADR specification deferred until concrete cross-department on-call escalation matures (likely Phase 2 commercial scope when 24×7 ops becomes relevant).

### 7.4 — First-aid baseline (universal contextual help)

Every user surface across every department inherits a baseline contextual-help layer regardless of role. Real-Navy: every sailor receives basic life-saving training because the IDC can't be everywhere. Sunfish parallel: every UI surface ships with WCAG-conforming help text, error explanations, suggested-next-action prompts.

**Mandatory baseline contract** (auditable; non-conforming surfaces fail Stage 07 review):

| WCAG SC | Requirement |
|---|---|
| 3.3.1 + 3.3.3 | Error messages in plain language naming the affected field + suggested next action |
| 2.1.1 + 2.4.7 + 2.4.11 *(AA-2.2 new)* | Every interactive control reachable via keyboard with visible focus indicator ≥3:1 contrast |
| 3.2.6 *(AA-2.2 new)* | Help available in a consistent location across surfaces |
| 4.1.3 | Status messages announced via aria-live or platform-equivalent |
| 2.5.8 *(AA-2.2 new)* | Target size ≥24×24 CSS px (web) / 44pt (iOS HIG) / 48dp (Material) |
| 4.1.2 | All functional content has a programmatic accessible name |
| 1.4.1 | Color paired with shape/icon/label — never color-only signaling |
| 3.3.7 + 3.3.8 *(AA-2.2 new)* | Redundant entry not required; cognitive-function tests forbidden in MFA UX without accessible alternative |

**Composition with Sunfish substrate**:
- Composes with the Shared Design System ADR's accessibility baseline (WCAG 2.2 AA minimum + EN 301 549; concrete criteria enumerated in §8.2)
- Inherited by every per-location ADR
- Auditable at Stage 07 review — surfaces failing the baseline are rejected

**Recommendation**: folded into Shared Design System ADR.

### 7.5 — Platform a11y API contract

Sunfish surfaces run on multiple platforms per ADR 0048 + 0048-A1. ARIA is web-only; native UIs require platform-native a11y APIs. Every UI primitive must surface accessible name/role/state through the **native** API on each runtime, not only ARIA.

| Platform | Native a11y API | Adapter |
|---|---|---|
| macOS desktop | **NSAccessibility** (AppKit/SwiftUI) | MAUI / Photino bridge |
| iOS / iPadOS | **UIAccessibility** (UIKit/SwiftUI) | native + WebView WKAccessibilityElement |
| Windows | **UI Automation (UIA)** provider tree | MAUI / WinAppSDK |
| Android | **AccessibilityNodeInfo** + Compose accessibility | MAUI |
| visionOS | **UIAccessibility** (SwiftUI 3D) | future Anchor visionOS |
| watchOS | **UIAccessibility** (watchOS) | Helm glance widget on paired-device |
| Web (Bridge) | **WAI-ARIA 1.2** + AOM | React / Blazor |

Per ADR 0048 + 0048-A1, every block-level a11y test runs cross-platform. The Shared Design System ADR specifies the per-platform binding contract; each per-location ADR's adapter implementations must satisfy it.

**Recommendation**: folded into Shared Design System ADR; cross-referenced from every per-location follow-on ADR.

---

## 8. Synthesis — recommended follow-on intakes

Per Phase 4 synthesis. **5–6 follow-on intakes** to file:

### 8.1 — New ADR: OOD pattern + Watch rotation primitive *(confirmed for promotion — §6.7)*

Cross-cutting primitive. Specifies OOD designation, watch handover Standing Order, per-Standing-Order OOD-tagging, EOOW analog. Effort medium (~10-14h); council-review-heavy.

### 8.2 — New ADR: Shared Design System

Load-bearing for every downstream UI ADR. Specifies: role taxonomy (Captain/XO/ENG/NAV/TAC/Division Officers/IDC/Scribe/SUPPO); permission tuple resolution algorithm + `PermissionDecision` denial shape; deck-progressive-disclosure pattern; first-aid baseline (universal contextual help layer); design tokens (color, typography, spacing); component library primitives; **WCAG 2.2 AA + EN 301 549 conformance baseline**.

**Mandatory accessibility scope** *(per Stage 1.5 hardening pass — non-negotiable)*:

1. **WCAG 2.2 AA conformance baseline** — explicit named criteria: 1.3.1, 1.4.1, 1.4.3, 1.4.11, 2.1.1, 2.4.7, 2.4.11 *(new)*, 2.5.7 *(new)*, 2.5.8 *(new)*, 3.2.6 *(new)*, 3.3.1, 3.3.7 *(new)*, 3.3.8 *(new)*, 4.1.2, 4.1.3
2. **Focus management contract** — focus order, focus return after dialogs, focus visibility tokens, focus-trap pattern for modals
3. **Color/theme tokens with contrast guarantees** — every text/background pair in token system meets ≥4.5:1; non-text UI ≥3:1; dark + light themes both audited
4. **Motion/animation tokens** — `prefers-reduced-motion` honored at the token level (not per-component opt-in); `prefers-reduced-transparency`; `prefers-contrast`; `forced-colors` (Windows High Contrast — mandatory)
5. **Form-control contract** — labels, errors, descriptions, required-field semantics; `aria-invalid` + `aria-describedby` wired by primitive
6. **Live-region primitive** — `<LiveAnnouncer>` as a first-class building block; not ad-hoc per surface
7. **Internationalization + RTL** — text direction, locale-aware date/time/number formatting, accessible language attributes (SC 3.1.1, 3.1.2)
8. **Reduced-data / high-contrast modes** — Windows High Contrast, macOS Increase Contrast, iOS Smart Invert all tested
9. **Authoring-time lint contract** — adapter components ship with axe/equivalent rules; CI fails on violations
10. **EN 301 549 chapters 9–11** — explicit chapter-by-chapter mapping for Bridge EU procurement readiness

Effort large (~20-26h authoring + extended council including WCAG/a11y subagent + design-engineering perspective + i18n/RTL subagent if available).

### 8.3 — New ADR: Quarterdeck entry-point surface *(per §5.1)*

Top-deck data model + widget contract + OOD-watch banner + permission-gated descent + deep-link search. Effort medium-large (~12-18h).

### 8.4 — New ADR: Engine Room observability surface *(per §5.3)*

Aspire-shaped log/trace/metric/health viewer; OpenTelemetry integration; CRDT-growth gauge; Damage Control flows; QA Workshop UI. Effort large (~16-24h).

### 8.5 — New ADR: Tactical anomaly detection + threat-trigger surface *(per §5.4)*

Rule engine for detection; alert routing; incident response; threat-trigger Standing Order shapes. Effort medium-large (~12-18h); requires security-engineering subagent.

### 8.6 — New ADR: Sick Bay aggregation UI + IDC role definition *(per §5.5 + §6.4)*

Pharmacy/Lab/Atmosphere monitor UI; recovery-contact UX; key-rotation flow; Medevac escalation; stretcher-bearer paging. Overlap with W#34 ~ADR 0066 (Helm + identity Atlas) — disambiguate during authoring. Effort medium-large (~12-18h).

### 8.7 — New ADR: Ship's Office content aggregation surface *(per §5.6)*

Cross-document-type UI; Scribe role definition (overflow into Shared Design System); template authoring; document-version diff. Effort medium (~10-14h).

### 8.8 — Out-of-scope (track-as-deferred)

- Supply Office UX (§5.7) — Phase 2 commercial work
- Wardroom + Brig (v2 locations) — surface as concrete demand emerges
- Cross-tenant Quarterdeck / Tactical — Bridge accelerator multi-tenant fleet view; defer to a future workstream
- Cockpit (W#29) reframing as Quarterdeck's per-tenant projection — flagged in §10.3 below; defer to a future amendment
- Stretcher-bearer ADR (§7.3) — defer until 24×7 ops requires it

---

## 9. Implementation Guidance

### 9.1 Routing recommendation

| Follow-on | Routing | Rationale |
|---|---|---|
| OOD pattern + Watch rotation | New ADR | Cross-cutting primitive; no clean predecessor |
| Shared Design System | New ADR | Load-bearing for all downstream UI ADRs |
| Quarterdeck entry-point | New ADR | No clean predecessor |
| Engine Room observability | New ADR | Composes on existing infrastructure but UX is novel |
| Tactical anomaly + threat | New ADR | No clean predecessor |
| Sick Bay aggregation | New ADR | Composes on solid substrate; overlap with W#34 ~ADR 0066 to disambiguate |
| Ship's Office content | New ADR | Composes on per-document-type substrates |

### 9.2 Sequencing recommendation

1. **Shared Design System first** — load-bearing for every other ADR; specifies tokens, role taxonomy, permission tuple, WCAG baseline that all downstream consume.
2. **OOD + Watch rotation second** — small primitive; standalone; foundation for §5.1 Quarterdeck.
3. **Quarterdeck third** — depends on Shared Design System + OOD.
4. **Engine Room fourth** — large scope; depends on Shared Design System.
5. **Tactical fifth** — depends on Shared Design System; can run in parallel with Engine Room.
6. **Sick Bay sixth** — depends on Shared Design System + W#34 ~ADR 0066 disambiguation.
7. **Ship's Office seventh** — depends on Shared Design System.

### 9.3 W#29 Owner Web Cockpit relationship *(deferred but flagged)*

W#29 is currently `design-in-flight` for "Owner Web Cockpit (cluster module)" — Anchor + Bridge cockpit views consuming all property-management cluster modules. With the Ship Architecture in place, **W#29 is more naturally framed as the Quarterdeck's per-tenant projection for property-management owners** — the Cockpit becomes the rendered view a Captain (tenant owner) sees when they "report aboard" a property-management tenant. This avoids inventing a parallel naming hierarchy and keeps the Quarterdeck as the canonical entry point.

**Recommendation**: defer W#29 reframing to a future amendment. When W#29 hand-off authoring resumes, reference this discovery's §5.1 Quarterdeck framing and consider renaming to "Property Management Quarterdeck Projection" or similar. Not blocking W#35 follow-on authoring.

### 9.4 Cross-workstream impact

- **W#22 Leasing Pipeline** — Phase 6 compliance half consumes Tactical (§5.4) for FCRA dispute alerts + W#34 ~ADR 0068 for security policy
- **W#23 iOS Field-Capture** — form-factor adaptations of Quarterdeck + Wayfinder + Sick Bay surfaces (paired-device IDC visibility)
- **W#28 Public Listings** — Tactical (§5.4) for inquiry-defense alerts; ADR 0061 Bridge accelerator integration
- **W#29 Owner Web Cockpit** — reframed as Quarterdeck per-tenant projection (§9.3)
- **W#31 Foundation.Taxonomy** — domain-config layer composes with Wayfinder Atlas; informs Ship's Office template authoring
- **W#32 Foundation.Recovery** — Sick Bay's Pharmacy substrate
- **W#33 Mission Space** — Helm widget (§5.2) renders Mission Envelope; Quarterdeck (§5.1) shows Mission Envelope summary
- **W#34 Wayfinder** — one of 7 v1 locations; specifies internal structure; Helm + Atlas + Standing Orders log

### 9.5 Council review posture

Per `feedback_decision_discipline.md`: **pre-merge council canonical** for all 7 follow-on ADR drafts. Cohort metric is now 11+ of substrate amendments needing council fixes (per recent W#33 + W#34 follow-on shipping log).

**Net rule**: every UI-bearing follow-on ADR dispatches a **WCAG / a11y subagent** *(per Stage 1.5 hardening pass on this discovery)*. The cohort lesson from W#34 is unambiguous — without the a11y subagent, every UI ADR ships against an underspecified baseline and accumulates avoidable council fixes.

Per-ADR additional perspectives:

| Follow-on ADR | Required perspectives |
|---|---|
| **Shared Design System** | WCAG/a11y + design-engineering + security-engineering *(role taxonomy intersects security)* + i18n/RTL if available |
| **OOD + Watch rotation** | WCAG/a11y *(handover announcements + watch banner are live-region-heavy)* + security-engineering *(OOD authority intersects W#34 ~ADR 0068)* |
| **Quarterdeck** | WCAG/a11y *(entry-point + OOD-banner + ticker — high-risk surfaces)* |
| **Engine Room** | WCAG/a11y *(Aspire-shaped surfaces have known a11y debt; needs adversarial pass)* |
| **Tactical** | WCAG/a11y *(alert UX under stress is sensitive a11y territory)* + security-engineering *(threat-trigger Standing Order shapes)* |
| **Sick Bay** | WCAG/a11y *(fingerprint-display + verification trust UX — sensitive surfaces)* |
| **Ship's Office** | WCAG/a11y *(document-content surfaces — long-form reading, diff UX, tables — require dedicated review)* |

### 9.6 Pipeline closure

Per the gap-analysis pipeline contract (`icm/pipelines/sunfish-gap-analysis/routing.md`), this discovery is sufficient closure under the **"Approved Gap"** exit pattern. No Stage-02 architecture pass is required *for the matrix itself*. Each follow-on intake in §8 will run its own ICM pipeline (`sunfish-feature-change` for new ADRs).

Pipeline closes when CO records a final "Approved Gap" decision in this doc's frontmatter Status field, after Phase 4 (synthesis intake stubs) and Phase 5 (handoff + active-workstreams ledger flip).

---

## Appendix A — Consolidated-dashboard precedent survey

### A.1 — .NET Aspire Dashboard

**Pattern**: single dashboard with tabs for Resources / Console / Logs / Traces / Metrics / Endpoints / Environment Variables. Cross-pane navigation: click a log entry → jump to its trace → jump to the resource → see its env vars. Resource-graph view shows topology. OpenTelemetry-backed.

**Sunfish takeaway**: the canonical reference for the Engine Room observability surface (§5.3). Cross-pane navigation is a strong pattern; each Sunfish department's engineering deck should support it. Aspire's "all in one URL" is the right shape for the Engine Room sub-rooms.

### A.2 — Grafana Cloud

**Pattern**: multi-data-source observability — logs, metrics, traces, profiles. User-defined dashboards composing widgets across data sources. Strong alerting integration.

**Sunfish takeaway**: Grafana's data-source-abstraction pattern fits the Tactical department (§5.4) — anomaly-detection rules are data-source-agnostic. Less directly applicable than Aspire for the unified-dashboard shape; Sunfish doesn't need user-customizable dashboards in v1.

### A.3 — Heroku Dashboard

**Pattern**: per-app navigation: Overview / Resources / Deploy / Metrics / Activity / Access / Settings. Sidebar + content; per-app scope.

**Sunfish takeaway**: the Heroku per-app sidebar pattern maps onto Sunfish per-tenant Quarterdeck (§5.1) — each tenant has its own ship; the Quarterdeck sidebar lists this tenant's departments. For users with multi-tenant access (Phase 2 commercial), tenant-switcher above the sidebar.

### A.4 — Supabase Studio

**Pattern**: closest to "everything in one place" — Database / Auth / Storage / Functions / Logs / Realtime / API / Settings. Sidebar + content. Each pane is a focused experience.

**Sunfish takeaway**: validates the multi-experience-with-shared-styling shape. Each Sunfish department is a Supabase-pane equivalent. Supabase's settings pane is itself the Wayfinder analog within their architecture.

### A.5 — Kubernetes Dashboard

**Pattern**: resource-list + per-resource detail view. Resource types as primary navigation (Pods / Services / Deployments / etc.). Logs / events / shell access per pod.

**Sunfish takeaway**: maps onto the per-department engineering deck — drilling into a specific resource (e.g., a sync-daemon process) gets you the same logs/events/shell pattern. Engine Room's Main Propulsion sub-room could adopt this directly.

### A.6 — Hashicorp Consul / Nomad UI

**Pattern**: services + KV + ACL + intentions (Consul); jobs + nodes + servers + ACL (Nomad). Deeper IA for distributed-systems concerns.

**Sunfish takeaway**: ACL + intentions pattern informs the permission-tuple §7.1 — Hashicorp's intent-graph approach to authorization is mature; could be a model for Sunfish's `(role, location, deck, action)` resolution.

---

## Cross-references

- Plan: `~/.claude/plans/sunfish-ship-architecture-research.md`
- Methodology playbook (W#33): `~/.claude/plans/mission-space-research-methodology.md`
- Intake: `icm/00_intake/output/2026-05-01_ship-architecture-intake.md`
- Active workstream: `icm/_state/active-workstreams.md` row W#35
- Project memory (naming + architecture): `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_35_ship_architecture_naming.md`
- Precedent (W#33 Mission Space): `icm/01_discovery/output/2026-04-30_mission-space-matrix.md`
- Precedent (W#34 Wayfinder): `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md`
- Pipeline: `icm/pipelines/sunfish-gap-analysis/{README,routing,deliverables}.md`
- Predecessor ADRs: 0007 + 0009 + 0013 + 0028 (+A1–A8) + 0029 + 0032 + 0036 + 0041 + 0043 + 0046 + 0046-a1 + 0048 + 0048-A1 + 0049 + 0051 + 0052 + 0055 + 0056 + 0057 + 0057-A1 + 0061 + 0062 (Proposed) + 0063 (Proposed)
