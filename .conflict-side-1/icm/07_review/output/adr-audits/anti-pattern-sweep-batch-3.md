# Anti-Pattern Sweep — Batch 3

**Date:** 2026-04-28
**Auditor:** Automated subagent (Sonnet 4.6)
**Framework:** Universal Planning Framework v1.2 — 21 Anti-Patterns only
**ADRs in batch:** 0012, 0014, 0017

---

## ADR 0012 — Foundation.LocalFirst Contracts + Federation Relationship

**Summary:** Introduces `Sunfish.Foundation.LocalFirst` as a thin, contracts-only package for offline operation, sync queuing, conflict resolution, and data export/import; intentionally orthogonal to the existing `federation-*` packages.

| AP # | Description | Hit | Severity | Recommendation |
|---|---|---|---|---|
| 1 | Unvalidated assumptions | Partial | Minor | The ADR assumes federation packages "can migrate internals to LocalFirst contracts on their own cadence" but no migration spike has been done to verify `IChangeStore`/`ICapabilityOpStore` map cleanly onto `IOfflineStore`/`IOfflineQueue`. This is acknowledged as a P4 discovery spike, which mitigates severity. Annotate: mark the assumption explicitly — "not yet validated; see follow-up 1." |
| 11 | Zombie project (no kill criteria) | Partial | Minor | Six follow-ups are listed but none carry kill/drop criteria. If the P4 federation-retrofit spike finds that `IChangeStore` is structurally incompatible, there is no stated disposition (accept parallel surfaces forever? deprecate? fork?). Annotate: add a one-line "if retrofit is infeasible, parallel surfaces are permanent and accepted" note. |
| 21 | Assumed facts without sources | Partial | Minor | "A solo-device app syncing to a central Bridge tenant is local-first but not federated" — stated as fact without citing the architecture paper or a definition source. Minor because the claim is intuitive, but sourcing it to the local-node architecture paper §x would close the gap. Annotate: add inline reference. |

**Overall grade: annotation-only**

---

## ADR 0014 — UI Adapter Parity Policy (Blazor ↔ React)

**Summary:** Establishes parity-by-default between first-party UI adapters; all deviations must be registered as explicit, time-boxed exceptions in a living parity matrix; enforcement is review-enforced now, CI-enforced at P6.

| AP # | Description | Hit | Severity | Recommendation |
|---|---|---|---|---|
| 3 | Vague success criteria | Partial | Minor | The policy names the parity matrix and the exception format but does not define what "substantially the same component behavior" means in a measurable way. The ADR says "adapters may differ in rendering but not in observable behavior" without specifying how observable behavior is validated before the CI harness exists (Phase 2). Annotate: add a sentence defining observable behavior minimally — e.g., "same public parameter names, same event payloads, same ARIA roles." |
| 11 | Zombie project (no kill criteria) | Partial | Minor | Follow-up 2 (parity CI check) and follow-up 3 (kitchen-sink dual-render) have no target milestone or drop condition. If P6 never ships, the policy degrades silently to honor-system indefinitely. The ADR acknowledges this risk ("the policy is honor-system") but does not state a kill or escalation threshold. Annotate: add a note like "if the React adapter has not landed by M2 per ADR 0017, revisit exception policy scope." |
| 18 | Unverifiable gates | Partial | Minor | Phase 1 enforcement is "PR template asks" — a check that relies on contributor self-reporting. Until Phase 2 CI lands, no automated gate exists to catch undeclared drift. The ADR acknowledges this but frames CI as an optional follow-up. Annotate: flag explicitly that Phase 1 gates are unverifiable by construction and that undeclared drift before CI is a known accepted risk. |

**Overall grade: annotation-only**

---

## ADR 0017 — Spec-First UI Contracts with Native Framework Adapters and Optional Web-Components Consumption Track

**Summary:** Establishes `ui-core` as the canonical spec layer (four-contract shape per component), native Blazor and React as first-class adapters, and `ui-components-web` (Lit) as a third peer consumption track; corrects a prior WC-first decision made in the same ADR revision cycle.

| AP # | Description | Hit | Severity | Recommendation |
|---|---|---|---|---|
| 1 | Unvalidated assumptions | Partial | Major | The migration plan (M0–M5) assumes the four-contract shape (semantic, accessibility, styling, interaction) is workable for all ~40 existing Razor components. The only proposed validation is a single proof-point component in M0. The ADR acknowledges "contract churn during M0–M5 is real" — if the contract shape turns out not to fit complex components (e.g., DataGrid's generic typed child contexts), the entire migration plan's sequencing is wrong. Amend: M0 exit criteria should require the proof-point to cover at least one complex component (e.g., DataGrid column-slot generics), not just `SunfishButton`/`SunfishSearchBox`. |
| 2 | Vague phases | Partial | Minor | Phase M2 defers three concrete decisions: build tool (Vite vs. esbuild vs. Rollup), CSS strategy, and state primitive. Deferring all three to a follow-up ADR (0020) means M2's exit criteria cannot be evaluated without that ADR existing. If ADR 0020 is delayed, M2 has no executable spec. Annotate: note that M2 is blocked on ADR 0020 and add it as an explicit dependency in the Migration Plan. |
| 10 | First idea remaining unchallenged | Yes | Minor | The Lit choice (§5) records that FAST, Stencil, and vanilla Custom Elements were "considered and rejected" but gives no evidence or data for those rejections — only one-line characterizations ("ecosystem breadth," "tooling complexity"). The original Option A (WC-as-canonical) was challenged and revised, which is good; the Lit-vs-alternatives decision within Option B was not subjected to the same scrutiny. This is minor because the WC track is a peer, not canonical, so the blast radius of a wrong Lit choice is limited. Annotate: add a one-paragraph rationale stub or link to a design-doc comparison for the Lit decision. |
| 11 | Zombie project (no kill criteria) | Partial | Major | M4 (`ui-components-web`) and M5 (fan-out across three tracks) have no kill criteria or drop conditions. The ADR states ~120 implementations (40 components × 3 tracks) as the migration scope. If M3 parity harness proves flaky or M4 WC scaffolding is delayed, there is no stated policy for whether the WC track gets dropped, deferred, or held as a permanent exception. Amend: add a kill/defer condition for the WC track — e.g., "if the WC track harness is not passing for at least 5 components by [milestone], the track is deferred and `ui-components-web` is deprioritized pending renewed resourcing." |
| 18 | Unverifiable gates | Partial | Minor | M0 exit criteria state "at least 10% of components have explicit `ui-core` contracts and existing Blazor implementations assert against them." Ten percent of an unspecified total is an unverifiable gate — the total component count is not stated in the ADR. Annotate: add the approximate total count of Blazor components today so "10%" is a concrete number. |

**Overall grade: needs-amendment**

---

## Batch Summary

| ADR | Grade |
|---|---|
| 0012 — Foundation.LocalFirst | annotation-only |
| 0014 — UI Adapter Parity Policy | annotation-only |
| 0017 — Spec-First UI Contracts + WC Track | needs-amendment |

**Key amendment targets for ADR 0017:**
- AP #1: Broaden M0 proof-point to include a complex generic-slot component, not only simple leaf components.
- AP #11: Add an explicit kill/defer condition for the WC track (`ui-components-web`) if the parity harness does not reach minimum coverage by a stated milestone.
