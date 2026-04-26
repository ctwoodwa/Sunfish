# Wave 2 Task 2.0 — Cluster Freeze Decision

**Date:** 2026-04-25
**Plan:** [docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md](../../docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md) (v1.3)
**Author:** Wave 2 driver

## Canonical pattern source

Confirmed by reading `packages/foundation/`:

- `packages/foundation/Resources/Localization/SharedResource.resx` — 8 keys (`severity.{info,warning,error,critical}`, `action.{save,cancel,retry}`, `state.loading`); every `<data>` has non-empty `<comment>`; en-US neutral.
- `packages/foundation/Resources/Localization/SharedResource.ar-SA.resx` — 8 keys, ar-SA translations.
- `packages/foundation/Localization/SharedResource.cs` — marker class is **`public sealed class SharedResource { }`** (note: `public` not `internal`; v1.3 brief amendment needed).

## Pattern divergence inventory

Two distinct DI-surface patterns across the cascade target packages:

### Pattern A — has `ServiceCollectionExtensions`

Has a `<X>ServiceCollectionExtensions.cs` (or similar) with a `static IServiceCollection Add<X>(this IServiceCollection services)` method. Cluster subagent **edits** that method to add `services.AddLocalization()` + `ISunfishLocalizer<>` registration.

Inventoried Pattern A packages (10 of 14 blocks):
- `blocks-accounting/DependencyInjection/AccountingServiceCollectionExtensions.cs`
- `blocks-businesscases/DependencyInjection/BusinessCasesServiceCollectionExtensions.cs`
- `blocks-inspections/DependencyInjection/InspectionsServiceCollectionExtensions.cs`
- `blocks-leases/DependencyInjection/LeasesServiceCollectionExtensions.cs`
- `blocks-maintenance/DependencyInjection/MaintenanceServiceCollectionExtensions.cs`
- `blocks-rent-collection/DependencyInjection/RentCollectionServiceCollectionExtensions.cs`
- `blocks-subscriptions/DependencyInjection/SubscriptionsServiceCollectionExtensions.cs`
- `blocks-tax-reporting/DependencyInjection/TaxReportingServiceCollectionExtensions.cs`
- `blocks-tenant-admin/DependencyInjection/TenantAdminServiceCollectionExtensions.cs`
- `blocks-workflow/src/WorkflowServiceCollectionExtensions.cs` (note: `src/` subdir, not `DependencyInjection/`)

Plus in adapters:
- `ui-adapters-blazor/Renderers/DependencyInjection/RendererServiceCollectionExtensions.cs`

### Pattern B — no DI surface (Razor Class Library or contracts-only)

No `ServiceCollectionExtensions`, no `Program.cs`, no `Module.cs`. Either a Razor SDK library that exports components only, or a contracts-only library. Cluster subagent **does NOT** add a DI registration here — instead, the package ships `.resx` + marker class only; downstream consumers (apps / accelerators) wire the localizer in their own composition root.

Inventoried Pattern B packages (4 of 14 blocks + ui-core):
- `blocks-assets` (Razor SDK)
- `blocks-forms` (Razor SDK)
- `blocks-scheduling` (Razor SDK)
- `blocks-tasks` (Razor SDK)
- `ui-core` (contracts-only — `Microsoft.NET.Sdk`, no DI surface)

## Cluster freeze (final, v1.3-aligned)

### Cluster A (sentinel) — Pattern A only

| Package | DI seam | Pattern |
|---|---|---|
| `packages/blocks-accounting` | `DependencyInjection/AccountingServiceCollectionExtensions.cs` | A |
| `packages/blocks-tax-reporting` | `DependencyInjection/TaxReportingServiceCollectionExtensions.cs` | A |
| `packages/blocks-rent-collection` | `DependencyInjection/RentCollectionServiceCollectionExtensions.cs` | A |
| `packages/blocks-subscriptions` | `DependencyInjection/SubscriptionsServiceCollectionExtensions.cs` | A |

Sentinel validates Pattern A end-to-end. Clean homogeneous cluster — good sentinel choice.

### Cluster B — MIXED

| Package | DI seam | Pattern |
|---|---|---|
| `packages/blocks-assets` | (none — Razor SDK) | B |
| `packages/blocks-inspections` | `DependencyInjection/InspectionsServiceCollectionExtensions.cs` | A |
| `packages/blocks-maintenance` | `DependencyInjection/MaintenanceServiceCollectionExtensions.cs` | A |
| `packages/blocks-scheduling` | (none — Razor SDK) | B |

### Cluster C — MIXED

| Package | DI seam | Pattern |
|---|---|---|
| `packages/blocks-businesscases` | `DependencyInjection/BusinessCasesServiceCollectionExtensions.cs` | A |
| `packages/blocks-forms` | (none — Razor SDK) | B |
| `packages/blocks-leases` | `DependencyInjection/LeasesServiceCollectionExtensions.cs` | A |
| `packages/blocks-tenant-admin` | `DependencyInjection/TenantAdminServiceCollectionExtensions.cs` | A |
| `packages/blocks-workflow` | `src/WorkflowServiceCollectionExtensions.cs` | A (non-standard subdir) |
| `packages/blocks-tasks` | (none — Razor SDK) | B |

### Cluster D1 — MIXED

| Package | DI seam | Pattern |
|---|---|---|
| `packages/ui-core` | (none — contracts-only) | B |
| `packages/ui-adapters-blazor` | `Renderers/DependencyInjection/RendererServiceCollectionExtensions.cs` | A (subdir) |

### Cluster D2 — DEFERRED

`packages/ui-adapters-react` — TypeScript package; no .NET cascade pattern applies. Documented as deferral; future JS-cascade plan to address.

### Cluster E — composition root (special-case)

`apps/kitchen-sink` — composition root that consumes blocks. If it has its own user-facing copy distinct from blocks (and it does — runner UI, settings panel), it gets its own `SharedResource` bundle. Cluster E ships kitchen-sink's bundle + the **central `services.AddLocalization()` + per-block consumer-side wiring** for all Pattern B packages from clusters B/C/D1.

## Brief amendments needed for v1.3 cluster subagent dispatches

Three corrections from what v1.3 says vs what the actual repo requires:

1. **Marker class visibility** — v1.3 brief says `internal sealed`; actual foundation pattern is `public sealed`. Subagents must use `public sealed class SharedResource { }` to match foundation.

2. **Pattern B handling** — v1.3 brief assumes every package has an entry-point file (Program.cs / ServiceCollectionExtensions / Module.cs). For Pattern B packages, no such file exists. The brief's "edit the entry-point" step becomes "skip — package has no DI surface; document as Pattern B in report; downstream consumer wires localizer." Sentinel (Cluster A) doesn't expose this since it's all Pattern A; but Clusters B/C/D1 will hit it.

3. **`blocks-workflow` non-standard path** — `src/WorkflowServiceCollectionExtensions.cs`, not `DependencyInjection/...`. Subagent must locate the file by content (search for `*ServiceCollectionExtensions.cs` recursively), not by fixed path.

These amendments will be communicated inline in each cluster subagent's brief. They do NOT require a v1.4 plan PR — they're operational findings from Task 2.0 pattern discovery, captured here in the freeze document. The v1.3 plan brief framework is correct; these are per-cluster realities.

## Wave 2 dispatch order (per v1.3 sentinel + canary)

1. **Sentinel (Cluster A)** — full implement+review cycle solo. Gates fan-out.
2. **Canary (smallest two of {B, C, D1, E})** — 2-agent parallel dispatch. Measures harness parallelism. By package count, smallest two are: D1 (2 pkgs) and E (1 pkg). Dispatch D1 + E as canary.
3. **Fan-out (B + C remaining)** — if canary GREEN, dispatch B and C in parallel. If canary serialized, drop to sequential.
4. **Wave 3 review** — 4 reviewers (B, C, D1, E foundation-only-derivation for B per v1.2-B2) + diff-shape automated check + spot-check + pre-merge SHA check.

## Decision

**Cluster freeze approved as documented above.** Sentinel proceeds with Cluster A (all Pattern A). Pattern divergence handling and brief amendments to be passed to each cluster subagent inline. Cluster D2 (TypeScript) deferred to a separate plan.
