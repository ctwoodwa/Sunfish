# UI Adapter Parity Matrix

Tracks per-component and per-contract parity between first-party UI adapters. Governed by [ADR 0014](../../docs/adrs/0014-adapter-parity-policy.md).

**Agent relevance:** Loaded by agents adding components or UI contracts, or reviewing adapter coverage. High-frequency for ui-core/ui-adapters work.

**Rule:** a new component or contract lands in every first-party adapter in the same PR, or registers an exception below.

Status key: ✅ implemented · 🚧 in progress · ❌ not implemented · — not applicable

## Adapters tracked

| Adapter | Package | Status |
|---|---|---|
| Blazor | `packages/ui-adapters-blazor` | Active |
| React | `packages/ui-adapters-react` | **Not yet created** (P6 deliverable) |

All current Blazor-only components are registered as bootstrap-phase exceptions below. When the React adapter lands, each row gets a concrete target version.

## Component parity

| Component / Contract | UI-core contract | Blazor | React | Notes |
|---|---|---|---|---|
| SunfishDataGrid | `IDataGridContract` (pending) | 🚧 (G37 in flight) | ❌ | Bootstrap-phase exception |
| SunfishGridColumn | n/a (component only) | 🚧 | ❌ | Bootstrap-phase exception |
| SunfishForm | `IFormContract` (pending) | ✅ | ❌ | Bootstrap-phase exception |
| SunfishCard | TBD | ✅ | ❌ | Bootstrap-phase exception |
| AppShell | TBD | ✅ | ❌ | Bootstrap-phase exception |
| Theme service | `IThemeService` | ✅ | ❌ | Bootstrap-phase exception |
| Icon providers | `IIconProvider` | ✅ (Tabler, Legacy) | ❌ | Bootstrap-phase exception |
| CSS providers | `ICssProvider` (pending) | ✅ (FluentUI, Bootstrap, Material) | ❌ | Providers are Blazor-specific by design; React will use its own styling stack |

(Refine per component as ui-core contracts get named explicitly.)

## Exceptions register

Each exception follows the format mandated by ADR 0014.

### Exception: Entire React adapter

- **Adapter lacking:** react
- **Reason:** `packages/ui-adapters-react` package has not been scaffolded yet. The parity policy is documented so work against it starts correctly once scaffolding lands.
- **Owner:** Platform team
- **Target:** P6 milestone (ADR 0014 references). A dedicated follow-up ADR will confirm React scaffolding choices (reconciler, CSS strategy, state primitives) before component work begins.
- **Logged:** 2026-04-19

### Exception: SunfishDataGrid (G37 in-flight)

- **Adapter lacking:** react
- **Reason:** G37 is actively building DataGrid features in the Blazor adapter. No React implementation is required during this bootstrap phase, per ADR 0014's bootstrap clause.
- **Owner:** G37 track
- **Target:** First React adapter release after P6 scaffolding.
- **Logged:** 2026-04-19

### Exception: SunfishForm, SunfishCard, AppShell, Theme, Icons, CSS providers

- **Adapter lacking:** react
- **Reason:** All shipped prior to ADR 0014; bootstrap-phase gap. Listed collectively to avoid duplicate noise.
- **Owner:** Platform team
- **Target:** React-adapter initial release (P6).
- **Logged:** 2026-04-19

## Update procedure

- Every PR that adds or modifies a first-party adapter updates the relevant row(s) in the component table.
- Every new exception gets an entry in the exceptions register with target + owner + logged date.
- Stale exceptions (past their target with no update) are flagged during monthly roadmap review.
- When CI enforcement (ADR 0014 follow-up #2) lands, a missing row for a newly-added component will fail the build.

## Out-of-scope

- Provider theme packages (FluentUI, Bootstrap, Material) — styling within the Blazor adapter; no cross-adapter parity expected.
- `compat-telerik` — compatibility shim, not a first-party adapter.
- Accelerator-specific UI in Bridge — accelerators are consumers of adapters, not adapters themselves.
