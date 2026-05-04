---
id: 2
title: Kernel Module Format
status: Accepted
date: 2026-04-19
tier: kernel
concern:
  - governance
  - dev-experience
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0002 — Kernel Module Format

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** G32 (Appendix C #2)

---

## Context

The Sunfish platform specification asks (Appendix C #2): *"Kernel module format — is it an
Assembly + manifest (like ASP.NET Areas), an OCI artifact, or a plain NuGet package?"*

The question affects plugin authors (compliance packs, domain schemas, third-party blocks) and
deployment topology designers (CI/CD pipelines, GitOps environments).

**Shipping reality today:**
All Sunfish packages ship as `.nupkg` files consumed via standard NuGet feeds. The `Sunfish.Kernel`
façade (G1, PR #21), `Sunfish.Kernel.SchemaRegistry` (G2, PR #22), and `Sunfish.Kernel.EventBus`
(G3, PR #23) are all NuGet packages. There is no OCI artifact pipeline and no assembly-manifest
loader in the current codebase.

**Plugin surface:**
There is no `IPluginManifest` interface or equivalent plugin discovery mechanism in the codebase
at the time of this ADR. A plugin registration surface is a Phase 2 follow-up item.

**OCI artifacts** (e.g., OCI image layers, Helm charts, ORAS artifacts) are a valid distribution
format for GitOps / ArgoCD-style sealed supply chains, but they require tooling (ORAS CLI,
registry infrastructure) and add operational complexity with no benefit for the common developer
workflow.

**Assembly + manifest** (ASP.NET Areas / Razor Class Library pattern) is heavyweight: it requires
a custom assembly loader, manifest parsing, and versioning machinery that duplicates what NuGet
already provides. It is appropriate only when plugins must be hot-loaded at runtime without
redeployment — a requirement Sunfish does not currently have.

---

## Decision

**NuGet is the default and primary module format for Sunfish kernels and plugins.**

1. **All Sunfish kernel packages ship as `.nupkg`** consumed via NuGet feeds (nuget.org for OSS,
   private feeds for commercial accelerators).

2. **Plugin authors ship `Sunfish.Plugin.*` NuGet packages** that reference `Sunfish.Kernel`
   and register their contributions at application startup via standard .NET DI
   (`IServiceCollection` extensions). A formal `IPluginManifest` interface is a Phase 2
   deliverable; the absence of that interface today is a known gap, not a blocker for NuGet as
   the format.

3. **OCI-artifact distribution is a v1+ add-on**, not a v0 requirement. It is reserved for
   ops-driven deployments requiring sealed supply-chain provenance (e.g., government mandates,
   SLSA Level 3+). When OCI support ships, it will be an overlay on top of NuGet — the same
   `.nupkg` artifact will be repackaged as an OCI layer; no new SDK or plugin API is anticipated.

4. **Assembly + manifest hot-loading is explicitly out of scope for v0.** If a future runtime
   hot-reload requirement surfaces (e.g., compliance pack updates without redeployment), it will
   be assessed as a separate ADR.

---

## Consequences

**Positive**
- Zero new tooling: plugin authors use the standard `dotnet pack` / `dotnet add package`
  workflow they already know.
- NuGet transitive dependency resolution handles versioning conflicts across plugins
  automatically.
- NuGet feeds (nuget.org, Azure Artifacts, GitHub Packages) provide signing, provenance, and
  download statistics out of the box.
- OCI overlay is non-breaking when it ships — no API changes required.

**Negative / Trade-offs**
- NuGet requires a full application rebuild and redeployment to update a plugin. Acceptable for
  v0; revisit at v1 if hot-reload demand materializes.
- OCI path is undefined in v0; GitOps teams that want sealed supply chains must wait for v1+
  or manage their own ORAS wrapping.
- `IPluginManifest` does not exist yet; plugin discovery is currently convention-based DI
  registration, not contract-enforced. Phase 2 must close this.

**Revisit triggers**
- A customer requires compliance pack updates without full redeployment.
- SLSA Level 3+ supply chain requirement appears in a commercial contract.
- `IPluginManifest` interface is designed in Phase 2 — at that point this ADR should be updated
  to reference the concrete contract.

---

## References

- Sunfish platform spec Appendix C #2
- G1 Sunfish.Kernel façade (PR #21)
- G2 Sunfish.Kernel.SchemaRegistry (PR #22)
- G3 Sunfish.Kernel.EventBus (PR #23)
- Gap analysis G32: `icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md`
