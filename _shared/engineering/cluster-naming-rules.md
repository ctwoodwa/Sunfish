# Cluster Naming Rules (UPF Rules 1–5)

**Status:** Canonical. Emerged from the property-ops cluster reconciliation
(`icm/07_review/output/property-ops-cluster-naming-upf-review-2026-04-28.md`)
and have been applied across W#18 / W#19 / W#24 / W#25 / W#27 / W#28.

**Audience:** XO + COB + PAO when authoring intakes, ADRs, hand-offs, or
new packages within a cluster (any group of cluster-tagged workstreams).

---

## Rule 1 — Cluster prefix on new blocks

When a cluster introduces NEW blocks that overlap with the cluster's domain
prefix, name them with the cluster prefix.

- ✅ `blocks-property-equipment`, `blocks-property-receipts`,
  `blocks-property-listings` (property-ops cluster)
- ❌ `blocks-equipment` (collides with future non-property equipment use)
- **Exception:** the cluster's spine block (e.g., `blocks-properties`)
  stays unprefixed because IT IS the cluster anchor

---

## Rule 2 — EXTEND-vs-NEW disposition resolution

Before authoring a new block, AUDIT existing packages:

```bash
ls packages/ | grep -E "^blocks-|^foundation-|^kernel-"
```

If an existing block covers ≥70% of the proposed scope, the disposition
is EXTEND (not NEW). The cluster intake should reflect this; the hand-off
targets the existing block.

Examples (property-ops cluster):
- W#19 Work Orders → EXTEND `blocks-maintenance` (existing
  `WorkOrder` + `WorkOrderStatus` covered ~85% of cluster scope)
- W#25 Inspections → EXTEND `blocks-inspections` (~70% covered)
- W#27 Leases → EXTEND `blocks-leases` (~80% covered)
- W#18 Vendors → EXTEND `blocks-maintenance` (Vendor entity already there)

Codified in: `feedback_audit_existing_blocks_before_handoff` user memory.

---

## Rule 3 — Foundation-tier names are reserved for foundation-tier types

Generic-entity terms used by `Sunfish.Foundation.*` MUST NOT be reused at
block-tier for a domain-specific concept.

**Reserved (foundation-tier; don't overload):**
- `Asset` — `Sunfish.Foundation.Assets.Common.EntityId` is the generic-entity
  identifier; physical-equipment/property-asset domain types use `Equipment`
- `Tenant` — `Sunfish.Foundation.MultiTenancy.TenantId` is the BDFL-org
  boundary type; lease-holder/renter-of-property type uses `Leaseholder`
  (per ADR 0060 amendment A3)
- `Identity`, `User`, `Account` — reserved for future foundation-tier
  identity / multi-actor work; domain uses `Party`, `Actor`, `Operator` etc.

**Property-ops cluster rename precedent (2026-04-28):** Asset → Equipment
across the cluster (PR #216 + cascading), driven by Rule 3.

---

## Rule 4 — Don't overload existing primitive types

When the cluster needs a concept similar to an existing foundation type,
DO NOT extend the existing type with cluster-specific shape; instead
**leverage existing primitive + add cluster-specific attribute**.

Example:
- ✅ `LeaseHolderRole : enum { PrimaryLeaseholder, CoLeaseholder, Occupant, Guarantor }`
  + reuse existing `Sunfish.Foundation.Multitenancy.PartyKind.Tenant`
- ❌ Add `PartyKind.Leaseholder` to `PartyKind` enum (conflates BDFL-org-boundary
  with renter)

This was an explicit cluster amendment (W#27 Leases EXTEND, codified in
the hand-off's Phase 4).

---

## Rule 5 — Discriminated overlap, not duplication

When two cluster modules need the same concept at different specificity,
discriminate via a value-of-record field, not duplicate types.

Example:
- ✅ `MessageVisibility : enum { Public, PartyPair, OperatorOnly }` +
  `Thread.Participants : IReadOnlyList<Participant>` — visibility per
  thread is determined by participant set, not duplicated per module
- ❌ Define `WorkOrderMessage`, `LeaseMessage`, `InquiryMessage` as
  distinct types each with their own visibility enum

Cluster reconciliation 2026-04-28 applied Rule 5 to cluster-internal
messaging concerns; all cluster modules consume `Sunfish.Blocks.Messaging`
+ `Sunfish.Foundation.Integrations.Messaging` rather than each defining
its own messaging surface.

---

## When a Rule conflicts with existing code

The cluster naming rules apply going forward. Existing code that violates
a rule (e.g., a block predates Rule 3) is grandfathered until a
substantial-change ADR forces a rename. Don't proactively rename; wait
for a forcing function.

The Asset → Equipment rename (W#24) WAS a substantial-change ADR (because
the cluster ADRs were drafting against `Asset` and a downstream conflict
would have been worse). Most rule violations don't reach that threshold.

---

## When to add a new Rule

If a recurring naming-collision pattern surfaces during cluster authoring,
add a Rule here. New rules require:

1. ≥3 instances across cluster work where the same pattern caused friction
2. UPF Stage 1.5 council review covering the new rule
3. CO acceptance via PR review of this file

The 5 rules above all met that bar via the property-ops cluster
reconciliation review.

---

## References

- [property-ops-cluster-naming-upf-review-2026-04-28.md](../../icm/07_review/output/property-ops-cluster-naming-upf-review-2026-04-28.md)
  — original adversarial UPF review that codified Rules 1–5
- [property-ops-cluster-vs-existing-reconciliation-2026-04-28.md](../../icm/07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md)
  — applied the rules across 14 cluster intakes
- `feedback_audit_existing_blocks_before_handoff` (user memory) — Rule 2 pre-flight check
- `feedback_verify_cited_symbols_before_adr_acceptance` (user memory) — verifies Rules 3+4 violations don't slip through
