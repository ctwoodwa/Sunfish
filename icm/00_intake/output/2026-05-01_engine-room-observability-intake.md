# Intake — Engine Room Observability Surface (Aspire-shaped)

**Date:** 2026-05-01
**Requestor:** XO research session (synthesis output of W#35 Ship Architecture discovery §5.3 + §8.4)
**Request:** New ADR specifying the Engine Room observability surface — Aspire-shaped log/trace/metric/health viewer composing on existing Sunfish infrastructure (ADR 0028 CRDT engine + `Microsoft.Extensions.Logging` + ADR 0049 audit + ADR 0036 sync states).
**Pipeline variant:** `sunfish-feature-change`
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

Sunfish has solid observability *infrastructure* but no UI surface aggregating it into the consolidated Aspire-style dashboard the W#35 Ship Architecture discovery (§5.3) identifies as load-bearing for technical operators. Operators currently inspect logs via per-package consoles, traces via TBD (no tracing infrastructure specified), metrics via TBD, and CRDT health via ad-hoc queries. Without aggregation, technical incident response is hours-slower than necessary.

## Predecessor

- **ADR 0028** (CRDT engine selection) + amendments A1–A8 — informs Main Propulsion sub-room
- **`Microsoft.Extensions.Logging`** — structured logging substrate
- **ADR 0049** (audit trail) — queryable history substrate
- **ADR 0036** (sync states) — UI primitives
- **ADR 0061** (transport tiers) — peer health visibility

The infrastructure layer is Partial; what's missing is the **aggregation UI**.

## Scope

- **Aggregate observability UI** — per-resource log/trace/metric viewer (Aspire-style)
- **Tracing infrastructure** — OpenTelemetry-shaped tracing (currently unspecified)
- **Metrics surface** — counters / gauges / histograms (currently unspecified)
- **CRDT-growth gauge UI** — per-team / per-tenant growth tracking
- **Damage Control flows** — quarantine override / manual surgery UX (below-the-waterline)
- **QA Workshop UI** — test-runner / coverage / council-review surface

### Aspire-shaped does NOT mean Aspire-equivalent for a11y *(Stage 1.5 finding)*

Aspire's own a11y has known gaps. Engine Room MUST exceed Aspire's baseline:
- **Log table** is an accessible data grid with row/column header semantics + virtualization that preserves SR context (WCAG SC 1.3.1 + 4.1.2)
- **Trace timeline** ships with accessible alternative — text/table representation toggle (SC 1.1.1, 1.4.5)
- **Every metric chart** has a `<table>` data alternative
- **Damage Control destructive actions** require keyboard-confirmable elevated dialog with explicit accessible naming

## Industry prior-art

- **.NET Aspire Dashboard** (W#35 §A.1) — canonical reference for unified log/trace/metric viewing
- **Grafana Cloud** (W#35 §A.2) — multi-data-source pattern
- **Kubernetes Dashboard** (W#35 §A.5) — per-resource detail-view pattern
- **Honeycomb / Datadog / New Relic** — production-tier observability platforms

## Dependencies and Constraints

- **Hard prerequisite**: W#35 ~ADR Shared Design System (consume tokens + accessible-data-grid primitive)
- **Soft prerequisite**: W#34 ~ADR 0065 (Wayfinder + Standing Order — Engine Room can reference recent Standing Orders affecting infrastructure)
- **Effort estimate**: large (~16–24h)
- **Council review posture**: pre-merge canonical + WCAG/a11y subagent (Aspire-shaped surfaces have known a11y debt) + design-engineering subagent

## Affected Areas

- foundation: tracing + metrics infrastructure (potentially new packages)
- ui-core: Engine Room surface contract
- ui-adapters-blazor / ui-adapters-react: per-adapter rendering
- accelerators/anchor + accelerators/bridge: per-zone rendering (Engine Room differs per zone)

## Downstream Consumers

- All Sunfish operators / engineers / SREs
- W#22 / W#23 / W#28 cluster modules — production observability
- Phase 2 commercial MVP — multi-tenant observability surface

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery after Shared Design System ADR lands.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-05-01_ship-architecture.md` §5.3 + §8.4
- Aspire reference: W#35 §A.1
