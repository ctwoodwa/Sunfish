---
id: 1
title: Schema Registry Governance Model
status: Accepted
date: 2026-04-19
tier: governance
pipeline_variant: sunfish-feature-change
concern:
  - governance
  - persistence
  - version-management
enables:
  - schema-registry
  - schema-evolution-policy
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---

# ADR 0001 — Schema Registry Governance Model

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** G31 (Appendix C #1)

---

## Context

The Sunfish platform specification asks (Appendix C #1): *"Canonical schema registry governance
model — is it a foundation, a company, or a W3C working group?"*

`Sunfish.Kernel.SchemaRegistry` (`ISchemaRegistry`) ships with an in-memory backend scoped to a
single deployment. The interface supports `RegisterAsync` / `GetAsync` / `ValidateAsync` against
content-addressed schemas; schema migration methods (`PlanMigrationAsync`, `MigrateAsync`) are
reserved for a follow-up (G2 jsonata work). Nothing in the current codebase assumes any external
registry authority.

The spec §13.3 draws an OSS/commercial line:
- **OSS layer** (`Sunfish.Foundation`, `Sunfish.Kernel.*`) — framework primitives, no domain
  semantics.
- **Commercial/vertical layer** (accelerators, compliance packs) — domain schemas (lease, parcel,
  inspection, workflow) with business meaning.

A governance answer must serve both layers without creating bureaucratic overhead that slows
pre-v1 iteration.

**Why not W3C or a foundation?**
Both require broad ecosystem buy-in and multi-party process that is premature for a pre-v1 product.
CNCF sandbox entry, for example, requires demonstrated production adoption — Sunfish is not there
yet.

---

## Decision

**Adopt a hybrid two-tier governance model.**

### Tier 1 — Repo-local schemas (default)

Any Sunfish deployment owns and evolves its own schema namespace. No external gatekeeper.
`ISchemaRegistry` registers schemas under deployment-chosen IDs. This tier covers all schemas
that live inside a single organization's deployment boundary.

- **OSS layer schemas** (e.g., kernel primitives) are repo-local to the Sunfish OSS repository.
- **Commercial/vertical schemas** (e.g., lease, parcel, inspection, workflow) are repo-local to
  each accelerator package.

This maps cleanly to §13.3: OSS = open namespace; commercial = per-accelerator namespace.

### Tier 2 — Cross-deployment "canonical" schemas (shared vocabulary)

Schemas intended to be interchangeable across independently deployed Sunfish instances — i.e.,
the PM-vertical shared vocabulary — live under a `sunfish.io/schemas/*` namespace.

Governance for Tier 2:
- **Process:** Lightweight RFC run out of the Sunfish OSS GitHub repository (issue → draft PR →
  two-week comment period → merge). No external body required.
- **Authority:** Sunfish core maintainers hold merge authority on `sunfish.io/schemas/*` until
  formal governance is established.
- **Escalation path:** If commercial adoption at v1+ demands a neutral steward, CNCF sandbox
  entry is the reserved escalation. This decision is explicitly deferred to v1+.

### v0 scope

This ADR is a v0 position statement. The governance machinery (RFC template, `sunfish.io/schemas`
hosting, canonical registry endpoint) is **not** built in v0 — no code ships with this ADR.
The decision closes the open question and sets expectations for schema authors.

---

## Consequences

**Positive**
- Schema authors have a clear mental model: local by default, `sunfish.io/schemas/*` only when
  cross-deployment interop is needed.
- Zero process overhead for the common case (single deployment, private schemas).
- OSS/commercial split (§13.3) maps 1:1 onto Tier 1 vs Tier 2.
- Foundation escalation (CNCF) is acknowledged and deferred appropriately.

**Negative / Trade-offs**
- Tier 2 governance machinery does not exist yet; canonical schemas cannot be published until it
  does.
- The RFC process is informal and maintainer-trust-based — may need tightening as ecosystem grows.
- Until `sunfish.io/schemas/*` hosting exists, cross-deployment schema sharing requires
  out-of-band coordination (copy-paste or private registry).

**Revisit triggers**
- A second independent organization wants to publish Tier 2 schemas.
- v1.0 milestone is reached.
- Commercial partner contract requires a neutral schema authority.

---

## References

- Sunfish platform spec §3.4, §13.3
- `packages/kernel-schema-registry/ISchemaRegistry.cs`
- Gap analysis G31: `icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md`
- Gap analysis G2 (schema registry implementation, PR #22)
