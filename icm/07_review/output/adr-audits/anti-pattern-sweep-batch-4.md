# Anti-Pattern Sweep — Batch 4

**Date:** 2026-04-28
**ADRs audited:** 0027, 0029, 0031
**Framework:** Universal Planning Framework v1.2 — 21 Anti-Patterns only (Stage 0 and 5-CORE re-analysis skipped per batch procedure)

Rows with "no" hit are omitted for brevity.

---

## ADR 0027 — Kernel Runtime Split

**Decision:** Keep the existing `packages/kernel/` type-forwarding façade intact and add a new `packages/kernel-runtime/` package to house the paper's §5.1 runtime responsibilities and §5.3 extension-point interfaces.

| AP # | Description | Hit | Severity | Recommendation |
|---|---|---|---|---|
| 1 | Unvalidated assumptions | Partial | Minor | The ADR assumes "consumers who need runtime services [will] add the `Sunfish.Kernel.Runtime` package reference in addition to … `Sunfish.Kernel`" without validating that any current consumers exist or that the façade surface is stable enough to remain unchanged. Annotate with a note that a consumer inventory was (or was not) run before accepting. |
| 11 | Zombie project (no kill criteria) | Partial | Minor | The implementation checklist has four items all marked `[ ]` with no timeline or wave-completion gate. "Wave 1" is referenced but not defined here; if Wave 1 slips, the checklist hangs open indefinitely. Annotate with a pointer to the wave plan and an explicit completion gate. |
| 21 | Assumed facts without sources | Partial | Minor | The claim "breaking package renames are still permissible" under the pre-release policy is asserted without citing the policy document. The policy is real (per `.wolf/memory.md` pre-release-latest-first entry), but the ADR should cite it explicitly so the reasoning is independently verifiable. |

**Overall grade: annotation-only**

---

## ADR 0029 — Federation vs. Gossip Reconciliation

**Decision:** Maintain the four existing `federation-*` packages for inter-organizational/relay-mediated sync, and add a new `packages/kernel-sync/` package implementing paper §6.1–6.2 intra-team gossip — dual-track, two protocols, two scopes.

| AP # | Description | Hit | Severity | Recommendation |
|---|---|---|---|---|
| 1 | Unvalidated assumptions | Partial | Major | The ADR states "A new `docs/specifications/sync-architecture.md` documents the tier-1/tier-2 vs tier-3 boundary" as part of the compatibility plan, but that document is listed as a checklist item `[ ]`, not a pre-decision artifact. The boundary claim — that gossip and federation do not overlap at the "cross-team / relay boundary" — is a structural assumption that drives the entire dual-track choice. It should have been validated (even informally) before acceptance, not deferred to a future spec. Amend to either produce the boundary spec before marking Accepted, or explicitly note the assumption is untested and add a replanning trigger if Wave 2 cross-tests reveal overlap. |
| 11 | Zombie project (no kill criteria) | Partial | Minor | The cross-test checklist item — "spin up team-A and team-B; verify A-gossips-to-A, A-federates-to-B" — is deferred to "Wave 2" with no definition of what Wave 2 encompasses or what happens if the cross-test reveals the boundary is wrong. Add a follow-up ADR trigger or a named wave-gate. |
| 13 | Confidence without evidence | Partial | Major | The ADR dismisses Option A (retrofit federation as gossip transport) with: "the retrofit produces a worse version of both protocols." This is a qualitative claim stated with high confidence but without a prototype, spike, or reference to prior art showing the mismatch is insurmountable. The concern may be well-founded, but for a structural dual-track decision of this magnitude the rejection deserves more than an assertion. Annotate Option A's rejection with a reference to any spike work or prior analysis, or acknowledge this is a judgment call under uncertainty. |
| 20 | Discovery amnesia | Partial | Minor | The ADR references "ADR 0013 (foundation-integrations) already positions cross-jurisdictional exchange as a separate concern" but does not reproduce the relevant ADR 0013 language or quote the clause. If ADR 0013 is superseded or amended later, this reference silently loses its meaning. Annotate with the specific ADR 0013 language that supports the claim, or add a stability note. |

**Overall grade: needs-amendment** (AP #1 and #13 together underpin the dual-track structural choice; neither is resolved by annotation alone — the boundary spec should be produced or AP #1 explicitly relabeled as an open assumption with a replanning trigger)

---

## ADR 0031 — Bridge as Hybrid Multi-Tenant SaaS

**Decision:** Bridge adopts Zone-C Hybrid multi-tenancy: shared control plane + per-tenant data-plane (`local-node-host` process + SQLCipher DB + subdomain), with Option B dedicated deployment as a paid contractual upgrade tier.

| AP # | Description | Hit | Severity | Recommendation |
|---|---|---|---|---|
| 1 | Unvalidated assumptions | Partial | Minor | The ADR asserts "4 clients today grows to 30–40 within 18 months" as a decision driver for ruling out per-tenant isolated deployments (Option B as default). No source or model is cited for this growth projection. The claim is reasonable but if actual growth is slower, Option C's added orchestration complexity may have been incurred unnecessarily. Annotate with either a source or an explicit acknowledgment that the growth assumption is speculative. |
| 12 | Timeline fantasy | Yes | Minor | Wave 5 estimates ("~1 week", "~2-3 weeks", "~7-8 weeks total") are stated as wall-clock projections without qualification. Per the UPF, time estimates in a coding domain are "false precision." The estimates may be directionally correct, but as written they will age into commitments. Annotate with the standard caveat that these are rough-order-of-magnitude scope estimates, not delivery commitments. |
| 11 | Zombie project (no kill criteria) | Partial | Minor | Five deferred decisions are explicitly listed (OPFS opt-in, cross-tenant collaboration, admin recovery, multi-team Anchor, WebAuthn regulated tier) with "Open BDFL-sign-off tickets" as the only disposition. No replanning trigger or deadline is attached. If the BDFL tickets are not opened, these deferrals hang silently. Annotate each deferred item with a wave-gate or named trigger condition. |
| 21 | Assumed facts without sources | Partial | Minor | The statement "Per-tenant isolated deployments don't scale at that rate without a large ops team" is stated as fact. This is a reasonable ops-cost argument but is asserted without referencing a cost model, competitor precedent, or infrastructure analysis. It is the key Con that eliminates Option B as the default. Annotate or add a one-line rationale (e.g., linear infra cost vs. shared control-plane cost model). |

**Overall grade: annotation-only**

---

## Batch Summary

| ADR | Title | Grade |
|---|---|---|
| 0027 | Kernel Runtime Split | annotation-only |
| 0029 | Federation vs. Gossip Reconciliation | needs-amendment |
| 0031 | Bridge as Hybrid Multi-Tenant SaaS | annotation-only |

**Highest-priority action:** ADR 0029 AP #1 — the tier-boundary between federation and kernel-sync is an unvalidated structural assumption. Produce `docs/specifications/sync-architecture.md` (already a checklist item) before the dual-track decision is treated as settled, or explicitly restate that the boundary is an open assumption with a replanning trigger if Wave 2 cross-tests reveal overlap.
