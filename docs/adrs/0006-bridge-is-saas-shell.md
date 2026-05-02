---
id: 6
title: Bridge Is a Generic SaaS Shell, Not a Vertical App
status: Accepted
date: 2026-04-19
tier: accelerator
concern:
  - commercial
  - operations
composes:
  - 5
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0006 — Bridge Is a Generic SaaS Shell, Not a Vertical App

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** Platform identity mismatch between current Bridge framing and the Sunfish multi-bundle architecture.

---

## Context

The root `README.md` describes Bridge as "a full-stack property-management app." `accelerators/bridge/README.md` opens by calling Bridge "a full-stack project-management app" in one sentence and "the property-management vertical reference implementation" in the next — an internal contradiction inherited from earlier migration work. `accelerators/bridge/PLATFORM_ALIGNMENT.md` doubles down with "Bridge is the property-management vertical reference implementation." The platform specification (`docs/specifications/sunfish-platform-specification.md`) dedicates §4.7 and §6 to a "Property Management Vertical" phase and MVP feature set.

This framing is inconsistent with the architectural direction established by ADR 0005 (type-customization model) and with Sunfish's stated goal — a multi-bundle SaaS accelerator stack. If Bridge is *the* property-management product, then Asset Management, Project Management, Facility Operations, and Acquisition / Underwriting cannot be first-class bundles on the same host, and the bundle manifest work coming in ADR 0007 has no coherent home.

Concretely, the problem is:

1. New contributors reading the READMEs are told Bridge is PM, which contaminates every architectural decision — from what belongs in `Sunfish.Bridge.Data` to how the admin shell is structured.
2. The PLATFORM_ALIGNMENT matrix conflates Bridge's own requirements with PM-MVP coverage, so engineers can't tell which rows describe shell concerns vs. vertical concerns.
3. The platform spec's §6 "Property Management MVP" list is written as though it is Bridge's deliverable, but in the multi-bundle model it is the specification of *one bundle* that Bridge hosts.

The spec's PM-MVP content is still correct and valuable — it is the shape of the Property Management bundle. What needs to change is whose deliverable it is.

---

## Decision

**Bridge is the Sunfish reference SaaS shell accelerator** — a generic multi-tenant platform host. It is *not* a property-management product, a project-management product, or any other vertical. Bridge's responsibilities are shell-level: tenant lifecycle, subscription/edition enforcement, bundle activation, per-tenant feature management, admin backoffice, observability, and integration configuration.

**Property Management becomes Bridge's first reference bundle** — not its product. Asset Management, Project Management, Facility Operations, and Acquisition / Underwriting become equal-peer bundles shipped against the same shell, produced on the roadmap cadence defined by phases P2 and P3.

Concrete corrections (landed with this ADR):

1. `README.md` — line 31 rewording from "property-management app" to "generic multi-tenant SaaS shell accelerator" with a trailing pointer to Property Management as the first reference bundle.
2. `accelerators/bridge/README.md` — first paragraph rewritten; Bridge characterized as a SaaS shell; property-management language removed as a Bridge characterization; preserved as a reference-bundle pointer with links to ADR 0006 and ADR 0007.
3. `accelerators/bridge/PLATFORM_ALIGNMENT.md` — preamble note clarifying that the "Spec Section 6 — Property Management MVP Coverage" rows track the **Property Management bundle**, not Bridge itself.

### What does not change with this ADR

- The content of the platform spec's §4.7 and §6. Those sections remain valid as the *Property Management bundle* specification; a documentation follow-up will reconcile phrasing without altering technical content.
- `Sunfish.Bridge.Data` entities, demo auth, and the Aspire orchestration. An audit is scheduled (see follow-ups) to move any PM-specific entities into the eventual `blocks-*` modules, but no reshuffle happens under this ADR alone.
- The split-solution posture (Bridge has its own `Sunfish.Bridge.slnx`). Consistent with shell-vs-bundle separation.

### Shell vs. bundle split (policy)

When uncertain whether a capability belongs in Bridge or in a bundle:

- **Bridge (shell)** — tenant identity, tenant signup, subscription selection, bundle activation, feature-flag management UI, admin backoffice, integration configuration UI, system-level observability, tenant-level support tooling.
- **Bundle** — domain entities, workflows, forms, reports, templates, business rules, persona surfaces.

If a feature references "Lease," "Unit," "Invoice," or any other business concept, it belongs in a bundle's module, not in Bridge.

---

## Consequences

### Positive

- Multi-bundle architecture becomes achievable. Five reference bundles can target the same shell without structural contortion.
- Contributor onboarding is unambiguous: Bridge is tenancy + activation + admin; bundles carry the domain.
- ADR 0007's bundle manifest infrastructure has a coherent home.
- PR review gains a clean rule for rejecting leakage: "Does this change reference a business entity? If so, it doesn't belong in Bridge."

### Negative

- The platform spec's PM-heavy language becomes stale phrasing until a dedicated doc pass reconciles it. Readers of the spec may still assume Bridge ≡ PM for a period.
- `PLATFORM_ALIGNMENT.md` grows a preamble that future editors need to respect when updating coverage rows.
- Existing Bridge code in `Sunfish.Bridge.Data` may contain PM-specific types that should move to `blocks-*`. Identifying and moving them is outside this ADR's scope but becomes follow-up work.

### Follow-ups

1. **Platform spec revision** — §4.7 and §6 rephrased from "Property Management Vertical" to "Property Management bundle" language. No technical content changes.
2. **Bridge.Data audit** — inventory PM-specific entities and move them to appropriate `blocks-*` modules in phases P1–P3.
3. **Bundle activation service in Bridge** — consumes the ADR 0007 catalog to activate modules + features per tenant.
4. **Adjacent ADRs (P1)** — 0008 (MultiTenancy), 0009 (FeatureManagement) formalize the shell's remaining abstractions.

---

## References

- ADR 0005 — Type-Customization Model (four-layer hybrid; this ADR depends on the bundle concept it introduced).
- ADR 0007 — Bundle Manifest Schema (parallel ADR in this batch; formalizes what a bundle *is*).
- `README.md` (root) — corrected here.
- `accelerators/bridge/README.md` — corrected here.
- `accelerators/bridge/PLATFORM_ALIGNMENT.md` — preamble added here; PM-MVP row semantics clarified.
- `docs/specifications/sunfish-platform-specification.md` §4.7, §6 — PM content remains valid as bundle specification; full reconciliation is a follow-up.
