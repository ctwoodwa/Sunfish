# ADR 0027 — Kernel Runtime Split

**Status:** Proposed
**Date:** 2026-04-22
**Resolves:** Today's `packages/kernel` is a type-forwarding façade over Foundation primitives (per the package's own README: "virtual package ... no primitive is (re)implemented here"). The paper's kernel is a runtime with node lifecycle, plugin discovery, sync daemon orchestration, and versioned extension contracts. These are two different things sharing one name; the paper's kernel cannot be built inside the façade without confusing the façade's own semantics.

---

## Context

`packages/kernel/` is a **virtual package**. Its `TypeForwards.cs` uses `[assembly: TypeForwardedTo]` to re-expose the seven spec-§3 kernel primitives (Entity Store, Version Store, Audit Log, Schema Registry, Permission Evaluator, Event Bus, Blob Store) under their shipped `Sunfish.Foundation.*` namespaces. No primitive is implemented inside this assembly. The package exists to close gap **G1** from `icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md` — giving the spec's Layer 2 a single, nameable entry point without moving any code.

The paper (`_shared/product/local-node-architecture-paper.md`) describes a *different* kernel:

- **§5.1 Kernel and Plugin Model** lists seven runtime responsibilities: node lifecycle, plugin registry, sync-daemon orchestration, stream topology, projection scheduling, schema-version coordination, UI block manifest surfacing.
- **§5.3 Extension Point Contracts** names five versioned extension-point interfaces: `ILocalNodePlugin`, `IStreamDefinition`, `IProjectionBuilder`, `ISchemaVersion`, `IUiBlockManifest`.

These are runtime services, not type contracts. They host mutable state, hold disposable resources, and coordinate cross-cutting lifecycle — none of which can live behind `[TypeForwardedTo]`. The paper-alignment audit (`icm/07_review/output/paper-alignment-audit-2026-04-22.md` §2.1, conflict β) flagged the name collision: "kernel" now means two incompatible things, and agent onboarding documents cannot describe "the kernel" without first disambiguating which one.

---

## Decision drivers

- **Paper is the source of truth.** Paper §5.1 lists the canonical kernel responsibilities; those responsibilities need a home.
- **The façade is closing a real gap.** G1 gave Sunfish a Layer-2 surface at `packages/kernel/`. Deleting or reshaping the façade reopens G1 and breaks any consumer that already took a `Sunfish.Kernel` package reference.
- **Runtime ≠ types package.** `[TypeForwardedTo]` infrastructure and runtime lifecycle services don't share a cohesive package boundary.
- **Pre-release window.** Per the latest-first package policy, breaking package renames are still permissible — but only when they buy enough clarity to justify consumer churn. They are not free.

---

## Considered options

### Option A — Keep façade; add `packages/kernel-runtime/` alongside

- `packages/kernel/` stays as the typed-contract façade (the `[TypeForwardedTo]` assembly).
- `packages/kernel-runtime/` is new: holds `ILocalNodePlugin` registry, node-lifecycle services, sync-daemon client, plugin-discovery logic, and paper §5.3's five extension-point interfaces. Depends on `packages/kernel/`.
- **Pro:** no churn on existing `Sunfish.Kernel` consumers; clear separation of "types" vs "runtime."
- **Con:** two packages with similar names; a reader must learn both.

### Option B — Rename façade to `packages/kernel-primitives-facade/`; free `packages/kernel/` for the runtime

- `packages/kernel/` becomes the runtime.
- Façade moves to `packages/kernel-primitives-facade/`.
- **Pro:** paper-aligned naming ("kernel" = runtime).
- **Con:** breaking rename; every downstream consumer of `Sunfish.Kernel` updates in lockstep.

### Option C — Keep façade at `packages/kernel/`; put runtime inside it (merge)

- **Rejected.** Mixing `[TypeForwardedTo]` infrastructure with runtime lifecycle services produces an incoherent package: the assembly's `Sunfish.Kernel` namespace would host both "types that live in Foundation" and "services that run the node." Neither the README nor the dependency graph would scan cleanly.

### Option D — Delete the façade; type-forwards return to Foundation

- **Rejected.** The façade exists to close spec-§3 gap G1. Deletion reopens it. The paper's kernel is a *new* concept; it doesn't retire the Layer-2 surface need.

---

## Decision (recommended)

**Adopt Option A: keep the façade at `packages/kernel/`; add `packages/kernel-runtime/` as a new package.**

Rationale:

- Preserves the spec-§3 Layer-2 surface the façade exists for. G1 stays closed.
- No breaking change: both packages coexist. Pre-release permission to break is not spent on a rename that buys no additional clarity.
- Paper's seven kernel responsibilities and five extension-point interfaces get a clean, single-purpose home in `kernel-runtime`.
- Agent onboarding becomes unambiguous: **`kernel` = typed contracts façade; `kernel-runtime` = the running kernel per the paper.**

---

## Decision consequences

### Positive

- Zero churn on existing `Sunfish.Kernel` consumers.
- Paper's kernel gets a dedicated package to host `ILocalNodePlugin`, `IStreamDefinition`, `IProjectionBuilder`, `ISchemaVersion`, `IUiBlockManifest`.
- Sync-daemon client, plugin discovery, and node-lifecycle orchestration land in one package with a single responsibility.
- The façade's `TypeForwards.cs` stays stable — no risk of `[TypeForwardedTo]` churn breaking downstream type resolution.

### Negative

- **Package count grows.** The kernel family now reads `packages/kernel`, `packages/kernel-event-bus`, `packages/kernel-schema-registry`, `packages/kernel-runtime` — four packages. Mitigated by each package having a single, clearly-stated responsibility.
- **Documentation burden.** Two "kernel" docs (façade and runtime) must cross-link clearly so newcomers understand the split on first read.

---

## Compatibility plan

- `packages/kernel/README.md` gains a "For the paper's kernel runtime, see `packages/kernel-runtime`" pointer section.
- `packages/kernel-runtime/` ships as a **new package in Wave 1** of the paper-alignment plan. This ADR is **Proposed** now to unblock scaffolding in Wave 0.3 without committing to Wave 1's full delivery scope.
- No consumer migration required. Consumers who need runtime services add the `Sunfish.Kernel.Runtime` package reference in addition to (not instead of) `Sunfish.Kernel`.

---

## Implementation checklist

- [ ] Scaffold `packages/kernel-runtime/` with csproj + stubs for the five paper §5.3 extension-point interfaces (`ILocalNodePlugin`, `IStreamDefinition`, `IProjectionBuilder`, `ISchemaVersion`, `IUiBlockManifest`) + plugin-registry entry points
- [ ] Update `packages/kernel/README.md` with a pointer section "For the paper's kernel runtime, see `packages/kernel-runtime`"
- [ ] Add `/kernel-runtime/` folder to `Sunfish.slnx` (batched with other Wave 1 slnx updates)
- [ ] Cross-reference in `CLAUDE.md`: "kernel = façade; kernel-runtime = paper-kernel"

---

## References

- Paper §5.1 (Kernel and Plugin Model), §5.3 (Extension Point Contracts)
- `packages/kernel/README.md`
- `packages/kernel/TypeForwards.cs`
- `icm/07_review/output/paper-alignment-audit-2026-04-22.md` (§2.1, conflict β)
- `icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md` (gap G1)
- ADR 0006 (Bridge context — cited for ecosystem completeness)
