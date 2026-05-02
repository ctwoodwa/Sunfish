---
id: 16
title: App and Accelerator Naming Convention
status: Accepted
date: 2026-04-19
tier: governance
concern:
  - dev-experience
  - governance
composes:
  - 6
  - 14
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0016 — App and Accelerator Naming Convention

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** Codify the namespace, assembly, and csproj naming convention for projects under `apps/` and `accelerators/`. Close the open question flagged in [naming.md](../../_shared/product/naming.md) that listed `apps/kitchen-sink/Sunfish.KitchenSink.csproj` as a "legacy exception" without specifying what the correct shape should be.

---

## Context

[naming.md](../../_shared/product/naming.md) defines a clear tier-prefix rule for library packages under `packages/`: the kebab folder maps to a PascalCase namespace with the tier as the first segment.

- `packages/foundation-multitenancy` → `Sunfish.Foundation.MultiTenancy`
- `packages/blocks-leases` → `Sunfish.Blocks.Leases`
- `packages/ui-adapters-react` → `Sunfish.UIAdapters.React` (per the pending React adapter ADR)

That rule does not carry over cleanly to projects under `apps/` and `accelerators/`:

1. `apps/` and `accelerators/` are **deployables** — top-level compositions of library packages — not layered-architecture tiers. They consume the layering; they do not sit inside it.
2. The only existing accelerator, Bridge, already uses a **flat namespace** (`Sunfish.Bridge`, `Sunfish.Bridge.Data`, `Sunfish.Bridge.Client`, etc.). There is no `Sunfish.Accelerators.Bridge` prefix.
3. The only existing app, kitchen-sink, also uses a flat namespace (`Sunfish.KitchenSink`) — consistent with Bridge's pattern, but [naming.md](../../_shared/product/naming.md) has listed it as a "legacy exception" without stating what the replacement should look like. That ambiguity has been in place long enough to block both renames and new app creation.
4. Future work will add more apps. `apps/docs` already exists as a DocFX documentation site (non-.NET project). Phase 6+ will likely add additional .NET-based apps (admin consoles, demo hosts, provider test runners).

Without a written convention, the next app added to `apps/` will guess — producing the same inconsistency the package-tier rule was written to prevent.

The two realistic options were:

- **A — Tier-prefixed (`Sunfish.Apps.*`, `Sunfish.Accelerators.*`).** Consistent with package-tier pattern. Requires renaming every Bridge project (100+ files of namespace declarations and using directives, roughly the same scale as the pending `Sunfish.Components.Blazor` rename). Makes the namespace self-describing ("this is an app" / "this is an accelerator").
- **B — Flat (`Sunfish.<Name>`).** Matches Bridge's existing pattern. Aligns with how apps and accelerators are modeled architecturally (top-level deployables, not library tiers). No Bridge rename needed. Kitchen-sink is already on-pattern.

Option B reflects the architectural reality: deployables are not part of the layering. It also avoids a large second-wave Bridge rename on top of the already-large `Sunfish.Components.Blazor` → `Sunfish.UIAdapters.Blazor` rename that is separately scheduled. Option A's discoverability benefit is real but modest; the folder path (`apps/` vs. `accelerators/` vs. `packages/`) already carries the same information.

---

## Decision

**Apps and accelerators use a flat `Sunfish.<Name>` namespace pattern** — no `Apps.` or `Accelerators.` tier prefix. Internal sub-namespaces extend this root (`Sunfish.Bridge.Data`, `Sunfish.Bridge.Authorization`, `Sunfish.KitchenSink.Pages`).

### Scope

Applies to every project under:

- `apps/<kebab-name>/` — .NET web apps, demo hosts, admin consoles, kitchen-sink-style playgrounds.
- `accelerators/<kebab-name>/` — SaaS shells and opinionated deployables (Bridge today; future accelerators).

Does **not** apply to:

- Library packages under `packages/` — see [naming.md §Root namespace](../../_shared/product/naming.md#root-namespace) for the tier-prefix rule.
- Non-.NET projects (DocFX sites, static asset bundles, scripts). These follow their own tooling's conventions; `apps/docs/` is DocFX-scoped and has no .csproj.

### Rules

1. **Folder:** kebab-case, multi-word names always hyphenated. `apps/kitchen-sink/`, `accelerators/bridge/`, a hypothetical `apps/admin-console/`.

2. **Root namespace:** `Sunfish.<PascalCaseName>`. The kebab folder name joins to a single PascalCase segment — hyphens disappear, do not become namespace separators. `kitchen-sink` → `KitchenSink`, `admin-console` → `AdminConsole`. This matches how single-kebab package folders map (`ui-core` → `UICore`).

3. **Sub-namespaces** extend the root. `Sunfish.Bridge.Data`, `Sunfish.KitchenSink.Services`, `Sunfish.KitchenSink.Pages`. Internal structure uses whatever organization the app prefers — no tier rules apply below the root.

4. **csproj file name** matches the namespace: `Sunfish.KitchenSink.csproj`, `Sunfish.Bridge.csproj`, `Sunfish.Bridge.Data.csproj`.

5. **`RootNamespace` and `AssemblyName`** in the csproj are set explicitly to the same value. `<RootNamespace>Sunfish.Bridge.Data</RootNamespace>`, `<AssemblyName>Sunfish.Bridge.Data</AssemblyName>`. Explicit values prevent MSBuild's folder-name default from producing a mismatched assembly name on Windows.

6. **Multi-project apps / accelerators** are allowed. Bridge already has `Sunfish.Bridge`, `Sunfish.Bridge.Client`, `Sunfish.Bridge.Data`, `Sunfish.Bridge.ServiceDefaults`, `Sunfish.Bridge.MigrationService`, `Sunfish.Bridge.AppHost`, `Sunfish.Bridge.Tests.*`. Each sub-project extends the root namespace; there is no tier layer between root and sub.

7. **`IsPackable=false`** on app and accelerator projects. Apps ship as deployed binaries, not NuGet packages.

8. **`GenerateDocumentationFile=false`** and `<NoWarn>$(NoWarn);CS1591</NoWarn>` on app projects. Apps are consumers, not public API surfaces — XML documentation is not useful and forces noise comments on internal types. Accelerator projects that do define public contracts consumable by external code keep docs on (Bridge keeps the repo default).

9. **Test projects** for apps and accelerators follow the library-package test convention — `tests/` subfolder, `tests.csproj` file, explicit `AssemblyName` of `Sunfish.<Root>.Tests.<Kind>` (e.g. `Sunfish.Bridge.Tests.Unit`, `Sunfish.Bridge.Tests.Integration`, `Sunfish.Bridge.Tests.Performance`). See [package-conventions.md](../../_shared/engineering/package-conventions.md).

### Resolution of flagged inconsistencies

- **`apps/kitchen-sink/Sunfish.KitchenSink.csproj`** — On-pattern under this ADR. Remove from the "legacy exceptions" list in [naming.md](../../_shared/product/naming.md).
- **`accelerators/bridge/Sunfish.Bridge.csproj`** (and the other Bridge sub-projects) — On-pattern under this ADR. No rename required.

### Future apps and accelerators

When adding a new app or accelerator:

1. Choose a kebab folder name under `apps/` or `accelerators/`.
2. Compute the root namespace by dropping hyphens and PascalCasing (`admin-console` → `Sunfish.AdminConsole`).
3. csproj file and `AssemblyName` / `RootNamespace` all match the root namespace.
4. Additional sub-projects in the same app extend the root (`Sunfish.AdminConsole.Data`, `Sunfish.AdminConsole.Api`).
5. Register the tier folder in `Sunfish.slnx` under `/apps/` or `/accelerators/` to match the repo convention (per [naming.md §slnx folder names](../../_shared/product/naming.md#slnx-folder-names)).

---

## Consequences

### Positive

- Closes the longest-standing open question in [naming.md](../../_shared/product/naming.md). Kitchen-sink is no longer "legacy" — it is on-pattern.
- Bridge does not need a second rename on top of the pending `Sunfish.Components.Blazor` rename.
- Adding a new app or accelerator is unambiguous: the rules produce exactly one correct answer.
- Reinforces the architectural distinction between libraries (tiered) and deployables (flat).

### Negative

- Namespace alone does not disambiguate app-vs-accelerator-vs-library when a reader sees only `Sunfish.<Name>` out of context. The folder path still carries that information, but IDE search results and stack traces do not.
- When a future app or accelerator happens to share a name with a planned library tier concept, there is no prefix to prevent collision. (Unlikely in practice; names like `KitchenSink`, `Bridge`, `AdminConsole` do not collide with tier-level nouns.)
- Consumers of an accelerator's public API (none today, but Bridge may eventually expose a contract) will `using Sunfish.Bridge.*` rather than the more-specific `using Sunfish.Accelerators.Bridge.*`. Accepted trade-off.

### Follow-ups

1. **[naming.md](../../_shared/product/naming.md) update** — add an "Apps and accelerators" subsection to the namespace table; remove the kitchen-sink entry from the legacy-exceptions paragraph; cross-reference this ADR. *(Landed alongside this ADR.)*
2. **Sub-project naming for future multi-project apps** — if an app grows past two or three sub-projects, consider whether a shared Core/Contracts split is warranted, following the Bridge precedent. No new convention needed — existing sub-namespace rules cover it.
3. **Test-project AssemblyName pattern** — the convention of `Sunfish.<Root>.Tests.<Kind>` for multi-test-project apps (`.Unit`, `.Integration`, `.Performance`) is codified here by reference to Bridge; `package-conventions.md` may want to call this out explicitly for library packages with multiple test shapes.

---

## References

- [naming.md](../../_shared/product/naming.md) — Root namespace rule for library packages; the app-tier section introduced alongside this ADR.
- [package-conventions.md](../../_shared/engineering/package-conventions.md) — csproj shape for apps, accelerators, and their tests.
- [architecture-principles.md](../../_shared/product/architecture-principles.md) — accelerators and apps as consumers of the layered architecture, not tiers within it.
- `accelerators/bridge/` — reference layout for a multi-project accelerator.
- `apps/kitchen-sink/` — reference layout for an app.
- ADR 0006 — Bridge Is a Generic SaaS Shell (confirms Bridge as an accelerator / consumer, not a tier).
- ADR 0014 — UI Adapter Parity Policy (kitchen-sink's role as the parity demo surface).
