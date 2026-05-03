# Intake — Tactical Anomaly Detection + Threat-Trigger Surface

**Date:** 2026-05-01
**Requestor:** XO research session (synthesis output of W#35 Ship Architecture discovery §5.4 + §8.5)
**Request:** New ADR specifying the Tactical department — anomaly detection rule engine + alert routing + incident response UI + threat-trigger Standing Order shapes.
**Pipeline variant:** `sunfish-feature-change`
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

The W#35 Ship Architecture discovery (§5.4) identifies Tactical as a Gap — no current artifact specifies the anomaly-detection rule engine, alert routing taxonomy, incident-response UI, or threat-trigger Standing Order shapes. Per-domain inquiry-defense layers exist (W#28 Public Listings, W#22 FCRA disputes) but there is no cross-cutting Tactical surface.

## Predecessor

**No clean predecessor.** Adjacent: ADR 0043 (unified threat model — covers OSS-project supply chain, NOT tenant runtime threats); ADR 0049 (audit trail — provides substrate for triggers but not trigger logic); W#28 inquiry-defense (per-block, not cross-cutting); W#22 leasing-pipeline FCRA dispute window (domain-specific); W#34 ~ADR 0068 (security policy — composes).

## Scope

- **Anomaly detection rule engine** — what fires alerts, on what signals, at what thresholds. Recommend OPA/Rego or typed-DSL pattern (cross-references W#34 ~ADR 0068).
- **Alert routing taxonomy** — high-priority → Lookout (Quarterdeck-visible ticker); informational → Sonar Room (audit log)
- **Incident response surface** — runbooks; escalation paths; audit-trail-query helpers; one-click contact for SRE escalation
- **Threat-trigger Standing Orders** — when an anomaly fires, what auto-actions issue (rate-limit increase / quarantine flag / notify OOD); composes W#34 ~ADR 0065 Standing Order shape
- **Cross-tenant Tactical** — Bridge accelerator operators see anomalies across tenants
- **WCAG 2.2 AA conformance** — alert posting via assertive live region; non-color severity encoding; reduced-motion fallback for pulsing/flashing severity indicators (SC 2.3.1 flash-threshold). Incident-response runbooks: stepwise structure with proper heading hierarchy + skip-links.

## Industry prior-art

- **Datadog / New Relic / Honeycomb** — alert-rule engines + incident-response timelines
- **PagerDuty / Opsgenie** — escalation and on-call rotation
- **Sigma / Sigma Rules** — anomaly-detection rule format
- **Falco** — runtime security anomaly detection (Linux + container)

## Dependencies and Constraints

- **Hard prerequisite**: W#35 ~ADR Shared Design System
- **Hard prerequisite**: W#34 ~ADR 0065 (Standing Order shape for threat-trigger Standing Orders)
- **Soft cross-reference**: W#34 ~ADR 0068 (security policy — informs threat-trigger authority)
- **Effort estimate**: medium-large (~12–18h)
- **Council review posture**: pre-merge canonical + WCAG/a11y subagent (alert UX under stress is sensitive a11y territory) + security-engineering subagent (threat-trigger Standing Order shapes)

## Affected Areas

- foundation: anomaly-detection rule engine + threat-trigger primitive
- ui-core: Tactical surface contract
- ui-adapters-blazor / ui-adapters-react: per-adapter alert rendering
- accelerators/bridge: cross-tenant Tactical view

## Downstream Consumers

- W#22 Leasing Pipeline — Phase 6 compliance half (FCRA dispute alerts)
- W#28 Public Listings — inquiry-defense alerts
- All operators / SREs — production incident response
- Phase 2 commercial MVP — regulated-industry tenant alerting

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery after Shared Design System ADR lands.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-05-01_ship-architecture.md` §5.4 + §8.5
