---
id: 14
title: UI Adapter Parity Policy (Blazor ↔ React)
status: Accepted
date: 2026-04-19
tier: adapter
concern:
  - ui
composes:
  - 6
  - 7
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0014 — UI Adapter Parity Policy (Blazor ↔ React)

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** Establish the rule for keeping `ui-adapters-blazor` and the future `ui-adapters-react` (and any subsequent adapter) in sync, define how gaps are registered, and prevent the Blazor adapter from silently becoming the de-facto specification.

---

## Context

Sunfish's "framework-agnostic" claim rests on `ui-core` being the source of truth: headless contracts, state models, interaction semantics, rendering-agnostic component contracts. Adapters (`ui-adapters-blazor` today; `ui-adapters-react` in P6) implement those contracts. The platform position is that a consumer should be able to pick Blazor *or* React and get substantially the same component behavior.

Today the repo has exactly one adapter. G37 work (SunfishDataGrid) is actively adding features to the Blazor adapter. The README promises a React adapter that does not exist. Without a parity policy:

- New features land in Blazor only by default. The Blazor surface becomes the de-facto spec.
- `ui-core` contracts drift — when Blazor-specific assumptions leak into contracts, the contract becomes un-implementable in React.
- Adopters cannot rely on "pick an adapter, stay consistent." They back into Blazor.
- When the React adapter lands, months of Blazor-first work need to be re-specified.

The fix is a standing rule: parity is the default, gaps require an explicit exception entry reviewed by humans.

---

## Decision

**Parity is the default.** A new component, contract, API, or behavior change must land in every first-party UI adapter in the same pull request, **or** register an exception in the same pull request.

### Scope

Applies to:

- `packages/ui-core` — source-of-truth contracts. Any change here obligates adapters.
- `packages/ui-adapters-blazor` — first-party adapter.
- `packages/ui-adapters-react` — first-party adapter (when it lands).
- Future first-party adapters (web components, MAUI, …).

Does not apply to:

- Provider theme packages (`Providers/FluentUI`, `Providers/Bootstrap`, `Providers/Material`) — those are styling layers within one adapter.
- Accelerator-specific UI in Bridge — accelerators are opinionated consumers, not adapters.
- `compat-telerik` — a compatibility shim by design, not a first-party adapter.

### Rules

1. **UI-core contract changes require adapter impact review.** A PR modifying `ui-core` must either (a) update every first-party adapter to keep implementations valid, or (b) register an exception listing which adapters are out-of-date and by when they catch up.

2. **New component in adapter A requires component in adapter B.** If a PR adds a public component to one adapter, the same PR adds it to the other — or registers an exception.

3. **Behavior changes propagate.** Keyboard semantics, ARIA roles, event emission, default options, public parameter names — a change in one adapter is a change in every adapter. Adapters may differ in rendering but not in observable behavior.

4. **Exceptions are explicit and time-boxed.** Each exception names: component or API, adapter lacking parity, reason, owner, target resolution (version or ADR follow-up). Exceptions live in the parity matrix file; PR review rejects undeclared drift.

5. **Kitchen-sink shows parity.** For any component in both adapters, `apps/kitchen-sink` renders both and the demo page links to each. Components with registered exceptions render whichever adapter is current and note the exception.

6. **Single-adapter components are disallowed without an ADR.** A component that is fundamentally unavailable in another adapter (e.g. a Blazor-only server-streamed component, or a React-specific concurrent-rendering primitive) requires a dedicated ADR justifying the split.

### Parity matrix

Lives at `_shared/engineering/adapter-parity.md`. Seeded alongside this ADR with the current state: Blazor's shipped components, React as pending. Update rules:

- Every adapter release updates the matrix.
- Every undeclared-drift regression is a bug tracked against the regressing PR.
- The matrix is scanned in CI (future work) — a missing row for a newly-added component fails the build.

### Exception register format

Within `adapter-parity.md`, exceptions are listed as:

```markdown
### Exception: {Component or API name}
- **Adapter lacking:** blazor | react
- **Reason:** {one line}
- **Owner:** {team or person}
- **Target:** {version tag or ADR reference}
- **Logged:** {YYYY-MM-DD}
```

### Enforcement

Phase 1 (now): review-enforced. PR template asks "did this change UI-core or an adapter? Was parity landed or exception registered?"

Phase 2 (P6): CI-enforced. A script walks the matrix file and the adapter package indexes; a mismatch fails CI. Script is a follow-up deliverable.

### G37 SunfishDataGrid and the current Blazor-only state

G37 is actively building DataGrid features in Blazor. Under this ADR:

- The DataGrid is recorded as a single-adapter exception in the parity matrix until the React adapter exists.
- When the React adapter lands, DataGrid is one of the first components it must implement.
- G37 work itself does not pause; the matrix records the gap.

This is the correct use of the exception mechanism — known single-adapter work during a bootstrap phase gets documented, not suppressed.

---

## Consequences

### Positive

- `ui-core` stays honest as the source of truth; Blazor cannot silently become the spec.
- Contributors know the rule up front; parity is planned into feature work, not retrofitted.
- Adopters can trust that picking either adapter yields substantially the same surface.
- Exceptions are visible, not hidden — gaps are decisions, not accidents.

### Negative

- Shipping features takes more effort than single-adapter work. The baseline cost of a feature doubles once the React adapter exists.
- Review overhead grows: reviewers must think about both adapters on every UI-touching PR.
- CI enforcement (phase 2) requires tooling that does not exist yet. Until it ships, the policy is honor-system.

### Follow-ups

1. **React adapter skeleton (P6)** — the `ui-adapters-react` package with at minimum one reference component implementing a `ui-core` contract, so the parity mechanism has something to enforce against.
2. **Parity CI check** — a script that compares exported components across adapters and flags undeclared drift.
3. **Kitchen-sink dual-render plumbing** — demo pages that can render Blazor and React components side-by-side for one component, to prove observable-behavior parity.
4. **Component-level parity test harness** — `bunit` tests in Blazor have counterparts in React; they assert the same state transitions and emitted events. Defer until two adapters exist.

---

## References

- ADR 0006 — Bridge Is a Generic SaaS Shell (Bridge is a consumer, not an adapter).
- ADR 0007 — Bundle Manifest Schema (bundles reference components by id — those ids must resolve in any adapter the bundle supports).
- `_shared/engineering/adapter-parity.md` — the parity matrix seeded alongside this ADR.
- `packages/ui-core` — contract source of truth.
- `packages/ui-adapters-blazor` — currently the only first-party adapter.
- `apps/kitchen-sink` — where parity is demonstrated at runtime.
