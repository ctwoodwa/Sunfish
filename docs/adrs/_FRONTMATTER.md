# ADR Frontmatter Schema (v1)

**Status:** Active (introduced 2026-05-01 as part of the ADR portfolio foundation; first author cohort: ADR 0001-0065 retroactive sweep + every new ADR going forward).
**Owner:** XO research session.
**Tooling:** projections in `tools/adr-projections/` consume this schema; validators in the same directory enforce it.

---

## Why frontmatter

ADRs are the **journal** in our event-sourcing-shaped documentation model (per CO 2026-05-01 architectural framing). The journal is authoritative but inefficient for "current state" queries. Frontmatter is the per-ADR metadata that powers **projections** — derived read-models computed from the journal:

- **Status projection** — "which ADRs are currently Active?"
- **Topical projection** — "which ADRs touch security?"
- **Tier projection** — "which ADRs apply to the foundation layer?"
- **Dependency graph** — "what does ADR X compose / extend / supersede?"
- **Capability projection** — "which ADR enabled feature Y?"

Projections are NEVER authoritative — they're rebuilt from the journal whenever the schema or content changes. Like materialized views in a database.

---

## Schema (YAML, at top of file before the H1 title)

```yaml
---
id: 65
title: Wayfinder System + Standing Order Contract
status: Proposed
date: 2026-05-01
tier: foundation
pipeline_variant: sunfish-feature-change

concern:
  - configuration
  - audit
  - distribution
  - accessibility

enables:
  - operator-issued-config-via-standing-order
  - dual-surface-form-json-toggle
  - atlas-projection

composes:
  - 28   # CRDT engine
  - 49   # Audit substrate
  - 9    # FeatureManagement

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

amendments: []
---
```

The body of the ADR (everything after the `---` closing delimiter) is unchanged — keep the existing `# ADR NNNN — Title` H1 plus the `**Status:**` and `**Date:**` bold-prefixed lines for human-readable continuity. Tooling reads the frontmatter; humans can read either.

---

## Field reference

### Required fields

| Field | Type | Notes |
|---|---|---|
| `id` | integer | The 4-digit ADR number, as integer. `0065` → `65`. |
| `title` | string | Short title (must match H1 after the "ADR NNNN — " prefix). |
| `status` | enum | `Proposed` \| `Accepted` \| `Superseded` \| `Deprecated` \| `Withdrawn` |
| `date` | string (YYYY-MM-DD) | When the **current** status was set. Acceptance date for Accepted; supersession date for Superseded; etc. |
| `tier` | enum | See tier vocabulary below. |

### Conditional-required fields

| Field | Type | Required when | Notes |
|---|---|---|---|
| `superseded_by` | integer | `status == Superseded` | The ADR number that supersedes this one. Single value (an ADR is superseded by exactly one descendant). |
| `deprecated_in_favor_of` | integer | `status == Deprecated` | Optional but recommended. Single value if present. |

### Optional fields (populate when authoring; tooling re-derives some)

| Field | Type | Notes |
|---|---|---|
| `pipeline_variant` | enum | ICM pipeline that produced this ADR. See vocabulary below. |
| `concern` | string array | Controlled-vocabulary topical tags. See vocabulary below. |
| `enables` | string array | Capabilities this ADR enables. Free-form short identifiers (kebab-case). |
| `composes` | integer array | ADR numbers whose contracts this ADR uses. |
| `extends` | integer array | ADR numbers this ADR adds to (without superseding). |
| `supersedes` | integer array | ADR numbers this ADR supersedes (replaces). |
| `consumed_by` | integer array | ADR numbers known to consume this. **Tooling-derived** — humans don't author this; the projection tool computes it from `composes` / `extends` of other ADRs. |
| `amendments` | string array | Amendment IDs as strings (e.g., `["A1", "A2", "A3"]`). Empty for ADRs with no amendments. |

---

## Controlled vocabularies

### `tier` (single value)

| Value | Meaning |
|---|---|
| `foundation` | `Sunfish.Foundation.*` packages — framework-agnostic contracts. |
| `kernel` | `Sunfish.Kernel.*` packages — runtime substrates. |
| `ui-core` | `Sunfish.UI.*` packages — framework-agnostic UI contracts. |
| `adapter` | UI adapters (Blazor, React, etc.) — `ui-adapters-*` packages. |
| `block` | Composition layer — `blocks-*` packages. |
| `accelerator` | Anchor / Bridge / future accelerators. |
| `governance` | Repo governance, branch protection, CI policy, license posture. |
| `policy` | Compliance, regulatory, threat-model decisions. |
| `tooling` | Scaffolding, generators, build tools. |
| `process` | ICM pipeline, multi-session coordination, naval-org structure, council-batting-average discipline. |

### `concern` (string array; multi-value; controlled vocabulary)

| Tag | Meaning |
|---|---|
| `security` | Cryptography, signatures, capabilities, threat model, secrets. |
| `persistence` | Storage, schema, migrations, encoding. |
| `ui` | Adapter parity, components, UX patterns. |
| `accessibility` | WCAG, a11y, EN 301 549. |
| `regulatory` | HIPAA, GDPR, PCI, SOC 2, EU AI Act, FCRA, FHA, CCPA. |
| `distribution` | Federation, sync, mesh, transport, replication. |
| `multi-tenancy` | TenantId, tenant scoping, cross-tenant boundaries. |
| `audit` | Audit trail, event records, attestation. |
| `identity` | ActorId, principal, capability, authentication. |
| `capability-model` | Capability declarations, capability-graph, attestation. |
| `configuration` | Settings, feature flags, entitlements, editions, Wayfinder. |
| `observability` | Logging, telemetry, metrics, tracing. |
| `threat-model` | Trust boundaries, blast radius, public-OSS posture. |
| `governance` | Repo, license, contributor, branch protection. |
| `dev-experience` | Scaffolding, templates, kitchen-sink, apps/docs. |
| `operations` | Bridge ops, hosted-node-as-SaaS, staffing model. |
| `commercial` | Editions, subscriptions, billing, edition-tier. |
| `mission-space` | Capability dimension, environment fit, install-UX. |
| `data-residency` | Per-jurisdiction data placement, EU data, US data. |
| `version-management` | Schema epochs, version vectors, compatibility relations. |

(New tags can be added — but each addition costs every consumer adapting; prefer adding to the existing vocabulary unless the new concern is genuinely orthogonal.)

### `pipeline_variant` (single value; from ICM)

`sunfish-feature-change` | `sunfish-api-change` | `sunfish-scaffolding` | `sunfish-docs-change` | `sunfish-quality-control` | `sunfish-test-expansion` | `sunfish-gap-analysis`

### `status` (single value)

| Value | Meaning |
|---|---|
| `Proposed` | ADR drafted; not yet adopted. PR open; council may be in flight. |
| `Accepted` | ADR adopted; the decision is in effect. |
| `Superseded` | A later ADR replaced this one. `superseded_by` MUST be set. The original ADR is still readable (history); but consumers should follow the supersession link to the active decision. |
| `Deprecated` | Decision is no longer in effect but no replacement was authored. Less common than `Superseded`; usually the right answer is to write a successor and use `Superseded` instead. |
| `Withdrawn` | ADR was authored then withdrawn before acceptance (rare; e.g., an option became infeasible during council). Original text preserved for history. |

---

## Validation rules (enforced by projection tooling)

1. `id` must be a unique positive integer matching the filename prefix (`0065-...md` → `id: 65`).
2. `title` must match the H1 line's title-portion (everything after `# ADR NNNN — `).
3. `status` must be one of the 5 enum values.
4. `date` must be a valid ISO-8601 calendar date (YYYY-MM-DD).
5. `tier` must be one of the controlled-vocabulary values.
6. `pipeline_variant` if present must be one of the 7 ICM-defined values.
7. `concern` if present must be a non-empty array of controlled-vocabulary tags.
8. If `status == Superseded`, `superseded_by` MUST be set to a valid ADR number.
9. `superseded_by` must NOT equal `id`.
10. `supersedes` and `superseded_by` cannot both point at the same ADR (would create a cycle).
11. Every integer in `composes` / `extends` / `supersedes` / `consumed_by` must be a real ADR number that exists in `docs/adrs/`.
12. `amendments` strings must match the pattern `A\d+` (e.g., `A1`, `A2.1`).

Tooling that fails validation MUST emit a clear error citing the ADR and field.

---

## Migration strategy (retrofitting 60 existing ADRs)

**Path A (chosen):** Apply frontmatter to all 60 existing ADRs in one sweep.

**Why not Path B (lazy migration — only on next-touch):**

- Lazy migration leaves the projection tool partially blind for months — projections become misleading, not just incomplete.
- The token cost of touching 60 files in one sweep is roughly the same as touching them in 60 separate edits.
- One PR is easier to review than 60.

**The sweep PR includes:**

1. This schema doc (`_FRONTMATTER.md`).
2. Updated `_template.md` with the frontmatter block as the new convention for new ADRs.
3. All 60 existing ADRs with frontmatter applied (status preserved from existing `**Status:**` lines; `date`, `tier`, `concern`, `composes` populated by reading each ADR; relationship fields populated by cross-referencing).
4. Initial projection tool MVP (separate stage 2 PR).

---

## Examples

### Minimum-viable frontmatter (Accepted ADR with no supersession history)

```yaml
---
id: 49
title: Audit Trail Substrate
status: Accepted
date: 2026-04-22
tier: kernel
concern:
  - audit
  - security
---
```

### Full frontmatter (Proposed ADR with rich relationships)

```yaml
---
id: 65
title: Wayfinder System + Standing Order Contract
status: Proposed
date: 2026-05-01
tier: foundation
pipeline_variant: sunfish-feature-change
concern:
  - configuration
  - audit
  - distribution
  - accessibility
enables:
  - operator-issued-config-via-standing-order
  - dual-surface-form-json-toggle
composes:
  - 28
  - 49
  - 9
amendments: []
---
```

### Superseded ADR

```yaml
---
id: 12
title: Foundation.LocalFirst (initial design)
status: Superseded
date: 2026-04-30
superseded_by: 36
tier: foundation
concern:
  - persistence
  - distribution
---
```

---

## Tooling expectations

A projection tool in `tools/adr-projections/` will:

1. Parse frontmatter from every `*.md` in `docs/adrs/` matching `[0-9]{4}-*.md`.
2. Validate per the rules above.
3. Emit:
   - `docs/adrs/INDEX.md` — topical projection (by `concern`) and tier projection (by `tier`).
   - `docs/adrs/STATUS.md` — current-state projection (by `status`).
   - `docs/adrs/GRAPH.md` — composition / supersession dependency graph (Mermaid or DOT).
4. Fail CI if validation errors are present.
5. (Future) Auto-derive `consumed_by` from other ADRs' `composes` / `extends` and check for stale references.

---

## Cross-references

- `_template.md` — ADR template; carries the frontmatter block as the first section.
- `tools/adr-projections/README.md` — projection tool docs (added in stage 2).
- `_shared/product/local-node-architecture-paper.md` — high-level architectural snapshot (Layer 4 in the four-layer model).
- `docs/architecture/snapshot-YYYY-QX.md` — periodic hand-curated snapshot (Layer 3; first instance in stage 5).
