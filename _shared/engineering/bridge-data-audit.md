# Bridge.Data Audit — PM-Leakage Inventory + Move Plan

**Status:** Draft for review
**Date:** 2026-04-19
**Context:** Follow-up work identified by [ADR 0006](../../docs/adrs/0006-bridge-is-saas-shell.md) — "Bridge is a Generic SaaS Shell, Not a Vertical App." This doc inventories what's actually in `accelerators/bridge/Sunfish.Bridge.Data`, classifies each type as shell-level vs. vertical-leakage, and proposes a phased move plan.

---

## TL;DR

`Sunfish.Bridge.Data` currently contains **project-management domain work mislabeled as shell infrastructure**. Of 9 entities in `Entities/Entities.cs`, only one (`AuditRecord`) is arguably a shell concern; the other eight belong in domain `blocks-*` modules that do not yet exist (`blocks-projects` primarily, with edges into `blocks-tasks`, `blocks-communications`, `blocks-accounting`). The `SunfishBridgeDbContext` mirrors the same split.

This is not an emergency — the code is pre-1.0, works, and consumers are internal only. But every week it stays there, it deepens the "Bridge ≡ project management" assumption ADR 0006 set out to kill. The move plan below is phased so Bridge demos never regress.

---

## Current inventory

### `Entities/Entities.cs`

| Type | Lines | Classification | Target home | Notes |
|---|---|---|---|---|
| `Project` | 11–20 | **Moves to block** | `blocks-projects` (NEW) | Has `TenantId`, status, timestamps — generic project aggregate |
| `ProjectStatus` enum | 3 | Moves to block | `blocks-projects` | |
| `ProjectMember` | 22–28 | Moves to block | `blocks-projects` | Membership join; belongs with projects |
| `TaskItem` | 30–41 | **Moves to block** | `blocks-tasks` (exists — but currently a component package, not a domain block) | Project FK needs to become polymorphic or a soft reference |
| `TaskStatus` enum | 4 | Moves to block | `blocks-tasks` | |
| `TaskPriority` enum | 5 | Moves to block | `blocks-tasks` | |
| `Subtask` | 43–49 | Moves to block | `blocks-tasks` | |
| `Comment` | 51–58 | Moves to block | `blocks-communications` (NEW) **or** `blocks-tasks` if comments stay task-attached | Decision point: comms-as-substrate vs. comms-as-edge |
| `Milestone` + `MilestoneStatus` | 60–67 | Moves to block | `blocks-projects` | |
| `Risk` + `RiskCategory` + `RiskLevel` + `RiskStatus` | 69–80 | Moves to block | `blocks-projects` (risk register) | Risk-register is a project-management feature |
| `BudgetLine` | 82–90 | **Decision point** | `blocks-projects` (project budget) OR `blocks-accounting` (budget line-items) | Recommend `blocks-projects` for project-scoped budgets; real accounting goes in `blocks-accounting` when invoicing/billing lands |
| `AuditRecord` | 92–103 | **Shell-level** | Bridge stays, or `Foundation` if the pattern is cross-accelerator | Universal shape; no project knowledge |

**Net:** 8 of 9 entities are vertical-leakage; 1 is shell-level.

### `SunfishBridgeDbContext.cs`

Owns `DbSet`s for all the above + query filters + lowercase table-name mappings for DAB. Once entities move, Bridge's DbContext keeps only shell tables (tenants, subscriptions, feature overrides, audit records, provider credentials, webhook envelopes). Domain blocks either:

1. Bring their own `DbContext` + migrations, merged at startup by EF Core's model-builder hook, **or**
2. Register entities into Bridge's DbContext from a module-extension hook.

Recommended: option 2 for Bridge (single-DbContext simplifies transactions and tooling like DAB). ADR follow-up needed to formalize the module-entity-registration pattern.

### `Authorization/`

| File | Classification | Notes |
|---|---|---|
| `Roles.cs` — `Owner`, `Admin`, `ProjectManager`, `TeamMember`, `Viewer` | Mostly shell | `ProjectManager` is a vertical term — rename to `Manager` or make it a per-bundle role. Others are shell-generic. |
| `Permissions.cs` — `ProjectCreate`, `ProjectDelete`, `TaskCreate`, `TaskDelete`, `BudgetEdit`, `RiskExport`, `AuditRead`, `OrgUpdate`, `SsoManage` | Split | `OrgUpdate`, `SsoManage`, `AuditRead` → shell. All `Project*`, `Task*`, `BudgetEdit`, `RiskExport` → move with the entities they protect. |

### `Seeding/BridgeSeeder.cs`

Seeds demo data using the PM entities. When entities move, the seeder either moves with them (per-block seeders chained at tenant provisioning) or stays in Bridge as a demo-tenant convenience that composes multiple block seeders. Recommended: per-block seeders, chained by bundle-provisioning service (P1 work per roadmap).

### `Migrations/`

EF Core migration history assumes the current schema. Moves are migration events: tables rename namespace prefixes (`projects` → `pm_projects` or `pjm_projects` depending on block keys) or split across schemas. Each move is a breaking change for any external tool reading the Postgres tables directly (including DAB via `dab-config.json`).

### `DesignTimeDbContextFactory.cs`

Tooling only. Moves with the DbContext.

---

## Classification summary

| Bucket | Count | Examples |
|---|---|---|
| **Shell — stays in Bridge** | 1 entity + partial auth | `AuditRecord`, shell-level roles/permissions |
| **Domain — moves to existing block** | 3 entities + related | `TaskItem`, `Subtask`, (arguably `Comment`) → `blocks-tasks` or `blocks-communications` |
| **Domain — moves to a new block** | 5 entities + related | `Project`, `ProjectMember`, `Milestone`, `Risk`, `BudgetLine` → `blocks-projects` (NEW) |
| **Shared infrastructure — stays** | DbContext shell parts, seeder orchestration, migrations tooling | |

---

## Move plan (phased)

### Phase M0 — Baseline (no moves yet)

- Document the audit (this file).
- Open ADR 0015 *"Module-entity registration into the Bridge DbContext"* deciding option-1-vs-option-2 above before any move happens.
- Add a PR-review rule: no new entity gets added to `Bridge.Data.Entities` without a written rationale ("this is shell, not a block").

### Phase M1 — Task block (after `blocks-tasks` is confirmed as a domain block in P2)

Currently `blocks-tasks` is a Blazor Razor component library, **not** a domain block with entities. Decide in ADR 0015 whether to:

- extend `blocks-tasks` with entities, or
- create `blocks-tasks-domain` alongside it and keep the component package separate.

Move `TaskItem`, `TaskStatus`, `TaskPriority`, `Subtask` — and their permissions — to the chosen home. Update `SunfishBridgeDbContext` to compose via module-entity registration. Migrations: rename tables with a block prefix.

### Phase M2 — Create `blocks-projects` (NEW P2 module)

Create `packages/blocks-projects/` with domain shape. Move `Project`, `ProjectStatus`, `ProjectMember`, `Milestone`, `MilestoneStatus`, `Risk` (+ enums), `BudgetLine`. Update seeder to bundle-driven composition. Update bundle manifests for property-management, project-management, asset-management, acquisition-underwriting to require `blocks-projects` when appropriate.

This phase produces the runnable project-management bundle the manifests in `packages/foundation-catalog/Manifests/Bundles/project-management.bundle.json` anticipate.

### Phase M3 — Comments decision

Ship `blocks-communications` contracts (P2) first. Decide then whether `Comment` moves to a generic `blocks-communications` (comments as a communications primitive, usable across tasks/projects/diligence) or stays with `blocks-tasks` if it's truly task-local.

Recommend: `blocks-communications` — comments are cross-cutting and will want a unified timeline eventually.

### Phase M4 — Budget / accounting split

If P2 ships `blocks-invoicing` + `blocks-billing` + `blocks-reconciliation`, revisit whether `BudgetLine` should become part of `blocks-accounting` rather than `blocks-projects`. Until then, `BudgetLine` lives with projects.

### Phase M5 — AuditRecord final home

Decide if `AuditRecord` is:

- **Bridge-local** (operator-specific audit view)
- **Foundation-level** (a platform primitive used by all accelerators)
- **Split**: Foundation contract + Bridge persistent implementation

Recommend Foundation-level contract (`IAuditLog` + `AuditEntry` record) with Bridge providing the persistent implementation. Parallel to how `ITenantCatalog` is a Foundation contract with in-memory/DB impls.

---

## What stays in Bridge after all phases

Bridge.Data should ultimately own only:

- `SunfishBridgeDbContext` — the shell DbContext that composes module entity registrations.
- Shell entities: tenant records, subscription state, feature overrides, provider credential references (if Bridge is also the secrets host), webhook envelope persistence (optional — could move to `Foundation.Integrations`-backed store later), tenant admin activity audit.
- Shell `Roles` and `Permissions` — the generic `Owner`/`Admin`/`Manager`/`Member`/`Viewer` set plus `OrgUpdate`/`SsoManage`/`AuditRead`.
- `Migrations/` — but most recent migrations will be adding shell-only tables; pre-move migrations will be superseded by block-owned migrations when blocks ship.

Every other table belongs to a block.

---

## Migration considerations

1. **DAB config (`dab-config.json`)** references table names directly. Each move requires coordinated updates to DAB config or a switch to module-scoped DAB configs per block.
2. **Direct SQL readers** (if any exist outside the codebase — reports, backups, BI tools) break on rename. Document the rename window and publish alias views during transition if needed.
3. **Query filters** (`modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == _currentTenantId)`) duplicate the shell's tenant-context responsibility. When entities move, replace with `IMustHaveTenant` marker (ADR 0008) and a shared query-filter extension in `Foundation.MultiTenancy`.
4. **Test fixtures** — bridge integration tests seed from `BridgeSeeder`. Per-block seeders need a deterministic ordering contract so tests remain stable.

---

## Risks

- **Moving without a block home first creates orphaned code.** Rule: don't move an entity until its target block exists (even as a scaffold). This is why phases M1–M4 are sequenced after P2 module creation.
- **DAB GraphQL schema churn.** Every move is a GraphQL breaking change for any external consumer. If Bridge demos rely on stable GraphQL, freeze external exposure until moves settle.
- **Seeder brittleness.** Seeder ordering matters once blocks own their seeds. Orchestration via bundle manifests (ADR 0007 — `seedWorkspaces`) is the clean answer but requires bundle-provisioning service (P1).

---

## Recommendations (concrete)

1. **Land ADR 0015** before any Bridge.Data move happens. Decide the module-entity registration pattern (external DbContext per block, or module-extension into Bridge's DbContext).
2. **Add a `PR-template` or `CODEOWNERS` rule** that flags new entities in `Bridge.Data.Entities` for extra review — catches future leakage.
3. **Freeze new domain additions to `Bridge.Data.Entities`.** New domain work goes into a `blocks-*` module (creating the module if necessary) even while the existing entities remain in place.
4. **Rename `Roles.ProjectManager` → `Roles.Manager`** — tiny change, removes the most visible vertical-specific language from shell code. Do this alongside ADR 0006 follow-ups.
5. **Defer actual entity moves to P2** when `blocks-projects` and friends actually exist. Don't churn the code until there's a target.
6. **Update `DemoTenantContext.Roles`** to match whatever rename lands in point 4.

---

## Related ADRs

- [ADR 0006](../../docs/adrs/0006-bridge-is-saas-shell.md) — Bridge Is a Generic SaaS Shell, Not a Vertical App.
- [ADR 0007](../../docs/adrs/0007-bundle-manifest-schema.md) — Bundle Manifest Schema (bundles will compose the future blocks).
- [ADR 0008](../../docs/adrs/0008-foundation-multitenancy.md) — `IMustHaveTenant` will replace the ad-hoc `TenantId == _currentTenantId` filters.
- **ADR 0015** (pending) — Module-entity registration into Bridge's DbContext. This audit's largest open architectural question.
