# Anti-Pattern Sweep — Batch 2

**Date:** 2026-04-28
**Auditor:** Automated subagent (Sonnet 4.6 / low effort)
**Reference:** `.claude/rules/universal-planning.md` — 21 Anti-Patterns
**ADRs in batch:** 0007, 0009, 0011

---

## ADR 0007 — Bundle Manifest Schema

**Decision summary:** Defines the C# record shapes, JSON serialization rules, `IBundleCatalog` interface, embedded-resource shipping strategy, and patch/minor/major versioning semantics for business-case bundle manifests in `Sunfish.Foundation.Catalog.Bundles`.

| AP # | Description | Hit | Severity | Recommendation |
|---|---|---|---|---|
| 1 | Unvalidated assumptions | Partial | Minor | The ADR assumes `JsonStringEnumConverter` round-trips all enum values correctly across STJ versions without citing a validation step. Annotate: add a CI serialization round-trip smoke test to the follow-ups list. |
| 4 | No rollback | Partial | Minor | Versioning rules are stated (patch/minor/major), but no rollback procedure appears for the provisioning step that applies a manifest. ADR 0011 later fills this gap — annotate a forward-reference to 0011 here so the ADR is self-contained. |
| 10 | First idea unchallenged | Partial | Minor | Embedded JSON (Option 1) is chosen with the note "If the catalog grows or external teams ship their own bundles, a sibling `catalog-seed` package becomes the follow-up split." No alternative embedding strategies (e.g., manifest discovery via assembly attributes, content-file NuGet items) were explicitly evaluated. Acceptable given the stated low stakes, but worth a brief annotation that Option 2 was considered. |
| 21 | Assumed facts without sources | Partial | Minor | "First five bundles are a few kilobytes total; not material for a decade." No measurement or citation. Annotate with an actual size figure once three bundles exist, or qualify as an estimate. |

**Overall grade: annotation-only**

---

## ADR 0009 — Foundation.FeatureManagement

**Decision summary:** Introduces a four-primitive feature model (flags / product features / entitlements / editions) behind an OpenFeature-style `IFeatureProvider` seam, composed by `IFeatureEvaluator`, with `IEntitlementResolver` and `IEditionResolver` as separate interfaces; real multi-tenant impls deferred to P2.

| AP # | Description | Hit | Severity | Recommendation |
|---|---|---|---|---|
| 1 | Unvalidated assumptions | Partial | Major | The ADR states `NoOpEntitlementResolver` ships for P1 and the bundle-backed resolver arrives in P2. The assumption is that P1 consumers can function without real entitlement resolution. No validation step is listed: what happens if Bridge wires in `IFeatureEvaluator` before P2 lands? The resolution path falls through to `FeatureSpec.DefaultValue`, silently granting or denying features. Amend: add an explicit note that P1 Bridge integration is blocked or must run with all features open/closed by default, with a kill-switch follow-up. |
| 5 | Plan ending at deploy | Partial | Minor | Follow-ups include "Persistent feature catalog" and "OpenFeature adapter package" but no ongoing observability (e.g., audit log of evaluation decisions, anomaly detection on entitlement mismatches). Annotate: add a monitoring/audit follow-up. |
| 11 | Zombie project (no kill criteria) | Partial | Minor | `IEditionResolver` ships with `FixedEditionResolver` ("for demos"), but there is no stated condition under which the real resolver must be in place before GA. Annotate: add a blocking criterion — e.g., "FixedEditionResolver must not reach Bridge production." |
| 13 | Confidence without evidence | Partial | Minor | "OpenFeature seam keeps vendor choice deferrable and swappable" is stated as a positive consequence without evidence that the `IFeatureProvider` interface is actually compatible with OpenFeature's provider contract in practice. The ADR acknowledges an adapter package is a follow-up, which mitigates this, but the confidence claim should be softened or cite the OpenFeature spec section it maps to. |

**Overall grade: needs-amendment** (AP #1 is major: a silent fail-open/fail-closed risk during the P1→P2 gap with no stated mitigation)

---

## ADR 0011 — Bundle Versioning and Upgrade Policy

**Decision summary:** Formalizes the semver rules for bundle manifests (normative table of change→bump), defines tenant upgrade behavior (silent patch / auto-minor / explicit-major), specifies a JSON Merge Patch three-way merge for template overlays on major bumps, sets deprecation/archival timelines, and restricts manual rollback to patch/minor within 7 days.

| AP # | Description | Hit | Severity | Recommendation |
|---|---|---|---|---|
| 1 | Unvalidated assumptions | Partial | Minor | "Changing a feature default value — Patch if the change is conservative (safer default). Minor if permissive." The determination of conservative vs. permissive is left to author judgment with no defined test or review gate. Annotate: the CI lint follow-up (item 5) should include a linting rule or checklist item for this distinction. |
| 4 | No rollback | Partial | Minor | Manual rollback after a major upgrade is explicitly disallowed. The stated escape valve is "forward-only fixes (a patch on the new major line)." This is a deliberate design choice but needs a brief annotation explaining what "patch on the new major line" means in concrete operational terms — it is not self-evident to a future bundle author. |
| 8 | Blind delegation trust | Partial | Minor | The three-way merge algorithm description assumes the implementation will faithfully identify conflicts: "Where `baseNew` has moved or removed a path the overlay modified, mark that overlay hunk as a **conflict**." No acceptance criteria or test cases are defined for the merge implementation. Amend or annotate: the P3 three-way merge follow-up should include explicit conflict-detection acceptance tests as an exit criterion. |
| 18 | Unverifiable gates | Partial | Minor | The deprecation rule "at least 180 days before next status change" has no stated enforcement mechanism (no CI check, no admin lock). Annotate: the bundle lifecycle audit log follow-up (item 4) should track status transitions and gate `Archived` transitions programmatically. |

**Overall grade: annotation-only**

---

## Batch Summary

| ADR | Decision | Grade |
|---|---|---|
| 0007 | Bundle Manifest Schema | annotation-only |
| 0009 | Foundation.FeatureManagement | needs-amendment |
| 0011 | Bundle Versioning and Upgrade Policy | annotation-only |

**Action required:** ADR 0009 should be amended to address the P1→P2 entitlement resolution gap (AP #1, major severity) before Bridge wires in `IFeatureEvaluator`. All other findings are annotation-level — add notes to the respective ADRs in a subsequent pass.
