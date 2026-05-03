# ADR 0008 — Foundation.MultiTenancy Contracts + Finbuckle Boundary — UPF Audit

**Auditor:** Subagent (autonomous overnight run)
**Date:** 2026-04-28
**Framework:** Universal Planning Framework v1.2 (full audit — Stage 0, Stage 1 CORE, Stage 2 + 21-AP scan)
**Source ADR:** `docs/adrs/0008-foundation-multitenancy.md`
**Amendment context:** `icm/00_intake/output/tenant-id-sentinel-pattern-intake-2026-04-28.md`

---

## Headline

**Grade: B-** — Solid contract-shape ADR with clear vendor-boundary reasoning and explicit follow-ups, but ships three overlapping marker interfaces (`ITenantScoped` / `IMustHaveTenant` / `IMayHaveTenant`) and a known-overloaded sibling `ITenantContext` without validation evidence that any of the three markers will be used. The 2026-04-28 intake has now confirmed `IMayHaveTenant` adoption is **zero** in production code — vindicating the latent AP-15 (premature precision) noted below.

## Most-Important Amendment

Add an "Adoption status (2026-04-28)" addendum capturing the intake's verified findings — `IMayHaveTenant` zero adoption, `IMustHaveTenant` 7-record adoption, sentinel pattern de-facto established via `TenantId.Default` — and explicitly flag that the marker-set is being revisited under the multi-tenancy type-surface convention. Without this, ADR 0008 reads as authoritative on a surface that the BDFL is actively deprecating.

---

## Stage 0 Findings (Discovery & Sparring)

The ADR shows partial Stage 0 work: alternatives are implicitly considered (Finbuckle in/out, decompose-now vs. defer) and prior code-state is enumerated in Context. But three Stage-0 checks were under-served. (1) **Existing Work** — `TenantId.Default` is already an in-use sentinel (16+ sites per intake) yet the ADR does not mention sentinel semantics at all, instead introducing `IMayHaveTenant` as a parallel mechanism for the same concept. (2) **Better Alternatives / AHA** — the dual-marker `IMustHaveTenant`/`IMayHaveTenant` pair was not stress-tested against a sentinel-only design; the intake now demonstrates the simpler design wins. (3) **Factual Verification** — the claim that `IMayHaveTenant` is needed for "system-level / cross-tenant records" was not validated by enumerating actual cross-tenant record types; intake found none.

---

## Stage 1 — 5 CORE Section Findings

**1. Context & Why** — Strong. Three concrete problems named with file paths (`Foundation.Authorization.ITenantContext` overloading, `Assets.Common.TenantId` namespacing, `DemoTenantContext` hardcode, ADR 0005's unfulfilled Finbuckle mandate). Exceeds the 3-sentence guideline but justified by the package-introduction scope.

**2. Success Criteria** — **Weak.** No measurable outcomes, no FAILED conditions, no kill triggers. The ADR doesn't say what success looks like beyond "the package exists." There is no signal for "this surface is wrong; revisit." The intake landing 9 days later is *itself* such a signal but was not anticipated. Major gap.

**3. Assumptions & Validation** — **Weak.** Implicit assumptions are unannotated: that consumers will prefer the new tenant-only `ITenantContext` (no migration plan); that `IMayHaveTenant` will have implementers (zero so far); that `ValueTask<T>` async shape is right for hot-path catalog reads (no benchmark). None follow the "Assumption → VALIDATE BY → IMPACT IF WRONG" form. Major gap.

**4. Phases** — N/A for an ADR (this is decision-shaped, not plan-shaped). The follow-up list (5 items) functions as a deferred-phase register and is reasonable, though item 2 ("move TenantId") is now ~9 days stale and item 1 ("decompose Authorization.ITenantContext") still has no owner.

**5. Verification** — **Missing.** No automated test, no manual review trigger, no observability hook for "is the new package being adopted vs. ignored?" The intake's grep-based adoption survey is the kind of verification that should have been pre-committed in this ADR.

---

## Stage 2 — Meta-Validation + 21-AP Scan

**Cold Start Test:** Pass — a fresh agent could implement the package from this ADR.
**Plan Hygiene:** Two stale follow-ups (1, 2) with no revisit cadence.
**Discovery Consolidation:** N/A.

**21-AP scan (hits only):**

| AP # | Description | Severity | Detail |
|------|-------------|----------|--------|
| AP-1 | Unvalidated assumptions | major | `IMayHaveTenant` introduced without evidence of need; intake confirms zero adoption. |
| AP-3 | Vague success criteria | major | No measurable outcome or FAILED condition. |
| AP-10 | First idea unchallenged | major | Dual-marker design (`IMustHaveTenant`/`IMayHaveTenant`) not stress-tested against sentinel-only alternative. |
| AP-15 | Premature precision | major | `IMayHaveTenant` defined before any consumer demanded it; classic over-design. |
| AP-18 | Unverifiable gates | minor | "New code should prefer the new ITenantContext" is policy-only with no enforcement or audit. |
| AP-20 | Discovery amnesia | minor | `TenantId.Default` sentinel pattern existed in code but was not surfaced or reconciled. |

---

## Recommended Amendments

| # | Severity | Amendment |
|---|----------|-----------|
| 1 | **critical** | Add an "Adoption status (2026-04-28)" addendum citing the intake's findings (`IMayHaveTenant` zero adoption; `IMustHaveTenant` 7 records; `TenantId.Default` 16+ sites). State that the marker-set is under active revision via the multi-tenancy type-surface convention; mark `IMayHaveTenant` as `@deprecated-pending` in the API table. |
| 2 | **major** | Add a **Success Criteria** section with at least: (a) "Bridge wires Finbuckle behind a `FinbuckleTenantResolver : ITenantResolver` adapter — no Finbuckle reference in any non-Bridge package"; (b) "≥1 non-Bridge host (lite-mode/test) consumes `InMemoryTenantCatalog`"; (c) FAILED condition: "If marker interfaces have <1 implementer per marker by 6 months post-acceptance, revisit the marker design." |
| 3 | **major** | Add an **Assumptions & Validation** section in UPF format. At minimum: "Consumers will prefer the tenant-only `ITenantContext` over the overloaded one" (validated by adoption count); "`IMayHaveTenant` has real consumers" (now invalidated — note the contradiction); "`ValueTask<T>` is right for the catalog hot-path" (validated by benchmark or removed as unsupported claim). |
| 4 | **major** | Reconcile sentinel and marker patterns explicitly. Either (a) state that `TenantId.Default` is the canonical sentinel and `IMayHaveTenant` is its strongly-typed escape-hatch; or (b) deprecate `IMayHaveTenant` in favor of sentinel-only — the intake's likely landing place. Do not leave the ADR silent on `TenantId.Default`. |
| 5 | **minor** | Cross-link to the convention intake (`tenant-id-sentinel-pattern-intake-2026-04-28.md`) in the References section so readers find the in-flight amendment without grepping. |
| 6 | **minor** | Annotate follow-up #1 ("decompose `Foundation.Authorization.ITenantContext`") with a kill criterion: e.g., "If not landed by Phase 2 commercial-MVP cutover, accept the overloaded interface as permanent and remove the new `ITenantContext` from the public API." Currently AP-11 zombie-project shaped. |
| 7 | **minor** | Add a **Verification** section: a one-line CI check or scheduled audit confirming no non-Bridge package references `Finbuckle.MultiTenant`. The "namespace-level separation is policy-enforced" Negative Consequence flags exactly the gap a verification line would close. |

---

**Word count:** ~795
