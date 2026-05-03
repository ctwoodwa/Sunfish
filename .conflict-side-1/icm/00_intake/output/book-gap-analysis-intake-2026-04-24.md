# Intake — Book-to-Sunfish Gap Analysis
**Date:** 2026-04-24  
**Pipeline variant:** sunfish-gap-analysis  
**Requestor:** Chris Wood  
**Scope:** Full gap analysis between *The Inverted Stack* book (C:\Projects\the-inverted-stack\chapters) and the Sunfish repository implementation

---

## Request Summary

Read all chapters of *The Inverted Stack* and conduct a systematic comparison against the Sunfish codebase to identify what the book specifies that Sunfish does not yet implement, document, or satisfy. Produce a prioritized plan of action.

## Source Material

| Source | Path | Role |
|--------|------|------|
| Book chapters | `C:\Projects\the-inverted-stack\chapters\` | Specification |
| Architecture paper | `_shared/product/local-node-architecture-paper.md` | Implementation spec |
| Sunfish packages | `packages/`, `accelerators/`, `apps/` | Implementation |
| ADRs | `docs/adrs/` | Decisions |
| Paper alignment plan | `_shared/product/paper-alignment-plan.md` | Current gap tracking |

## Affected Packages / Areas

All packages (this is a systemic gap analysis). Key areas flagged:
- `packages/kernel-*` — security, GC, testing
- `packages/kernel-security` — field-level encryption, DEK/KEK, incident response
- `packages/kernel-crdt` — GC policy, shallow snapshots
- `packages/kernel-sync` — gossip rate limiter, VPN tier, stale peer recovery
- `packages/kernel-schema-registry` — stream compaction
- `packages/foundation-localfirst` — disaster recovery, export
- `accelerators/anchor/` — disaster recovery UX, export, migration
- `accelerators/bridge/` — deprovisioning tooling, SBOM, supply chain
- `icm/` — incident response runbook, enterprise procurement

## Pipeline Variant Selection

**sunfish-gap-analysis** — this request identifies missing capabilities against a published specification (the book), scopes remediation priority, and feeds a backlog of new feature/quality requests.

## Advance: Discovery is Complete

Research was conducted as part of this intake. See gap analysis document at:
`icm/01_discovery/output/book-gap-analysis-2026-04-24.md`

Proceeding directly to Architecture (stage 02) to classify gaps and route into the ICM backlog.
