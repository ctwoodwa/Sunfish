# Hand-off — `blocks-work-projects` Project + Milestone + Budget + TimeEntry + Remodel cluster

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-17
**Status:** `ready-to-build`
**Workstream:** W#60 P4 — Path II native domain, work cluster (projects + time + budget slice)
**Spec source:**
- [`icm/02_architecture/blocks-work-schema-design.md`](../../02_architecture/blocks-work-schema-design.md) §2.1 (Project), §2.2 (ProjectStatus), §2.3 (ProjectMilestone), §2.8 (RemodelProject + RemodelPhase), §2.20 (TimeEntry/TimeLog), §2.21 (ProjectBudget), §2.22 (ProjectActual), §3 (relationships), §4.2 (budget-vs-actual reconciliation), §4.5 (milestone acceptance → invoice trigger), §4.6 (time-entry → cost allocation → journal), §5 (cross-cluster contracts), §6 (FOSS citations)
- [`_shared/engineering/crdt-friendly-schema-conventions.md`](../../../_shared/engineering/crdt-friendly-schema-conventions.md) §1 (ULID + monotonic numbers), §4 (append-only sub-collections), §5 (stable string codes), §6 (posted-then-immutable), §7 (state machines under CRDT — Pattern A for Project lifecycle), §13 (envelope)
- [`_shared/engineering/cross-cluster-event-bus-design.md`](../../../_shared/engineering/cross-cluster-event-bus-design.md) §1 (10-field envelope per T21-59Z ruling), §2 (naming), §3.1 (`Financial.*`), §3.2 (`Work.*`), §4 (idempotency keys — catalog format per T21-59Z ruling)
- [`_shared/engineering/party-model-convention.md`](../../../_shared/engineering/party-model-convention.md) §3 (role-record placement), §4 (cross-cluster references via `IPartyReadModel`)
**Sibling shipped:** [`blocks-work-orders-stage06-handoff.md`](./blocks-work-orders-stage06-handoff.md) — COB completed 2026-05-17 (4 PRs, 77 tests). This hand-off is its sibling-pattern continuation in the same cluster.
**Rulings consulted:**
- `coordination/_archive/xo-ruling-2026-05-16T21-59Z-cob-envelope-and-idempotency-reconciliation.md` — 10-field producer envelope is canonical; catalog idempotency-key format wins
- `coordination/_archive/xo-ruling-2026-05-17T00-18Z-cob-work-orders-complete-and-row-deferral.md` — work-orders cluster acceptance
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) Appendix B Phase 2 (`blocks-work-*` cluster)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~10–14h sunfish-PM (5–6 PRs, ~50–60 tests, docs, attribution)
**PR count:** 6 PRs (5 entity/feature + 1 DI + docs + importer)
**Pre-merge council:** NOT required (substrate scope; mirrors sibling `blocks-work-orders` pattern). Standard COB self-audit applies.

**Audit before build:**

```bash
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-work-"
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-work-orders/
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-people-foundation"
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^foundation-events"
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-financial-periods"
```

Expected:
- `blocks-work-orders/` exists (sibling — sources `IPartyReadModel` stub pattern; provides `WorkOrderId` for cross-references).
- `blocks-workflow/` exists (engine package — do NOT confuse with `blocks-work-*` domain).
- Nothing matching `blocks-work-projects/` yet (greenfield).
- `blocks-people-foundation/` MAY or MAY NOT exist yet — if it does, prefer its canonical `IPartyReadModel`; if not, mirror the work-orders local-stub pattern.
- `foundation-events/` MAY or MAY NOT exist yet — same fall-through (local `IDomainEventPublisher` + `NoopDomainEventPublisher` if absent).
- `blocks-financial-periods/` MAY or MAY NOT exist yet — same fall-through (local `IPeriodResolver` stub if absent; see H3).

---

## Context

### What this hand-off is — the project-management slice of `blocks-work-*`

The work cluster (ADR 0088 §1; `blocks-work-schema-design.md`) decomposes
into four shippable slices. The sibling `blocks-work-orders` hand-off
landed the **execution slice** (WorkOrder + MaintenanceSchedule +
Contractor + RepairTicket). This hand-off ships the **project-management
slice**:

- **Project** + **ProjectStatus** 8-state lifecycle.
- **ProjectMilestone** + **MilestoneStatus** (with invoice-trigger flag).
- **ProjectBudget** + **ProjectBudgetLine** (planned cost by category).
- **TimeEntry** + **TimeLog** append-only series (labor + cost allocation).
- **ProjectActual** event-sourced from `Financial.JournalEntryPosted`.
- **RemodelProject** + **RemodelPhase** (capital-improvement specialization).
- **`IProjectService`** + **`ITimeEntryService`** + **`IProjectActualProjector`**.
- **`AddBlocksWorkProjects()`** DI extension + `apps/docs/blocks-work-projects/overview.md`.

**Deferred to later hand-offs (NOT in scope here):**

- `blocks-work-contracts` — `Contract` + `ContractTerm` + `ContractAmendment` + `ContractRenewal` + `ContractorAgreement` (Stage 02 §2.12, §2.15–§2.19).
- `blocks-work-deliverables` — `Deliverable` + acceptance/sign-off state machine (Stage 02 §2.13–§2.14).
- The capital-asset journal-entry posting reactor on the financial side
  (Stage 02 §4 RemodelCapitalized → JE) — this hand-off **emits** the
  event; the financial reactor consumes it in a future
  `blocks-financial-ledger` follow-on hand-off.
- Real cross-replica Loro CRDT integration for `Project.status` Pattern A
  designated-authority enforcement — this hand-off implements the
  invariant at the service layer; the kernel-crdt resolver-registration
  wiring lands in a future sweep.

### Naming (binding)

Per naming-check policy + sibling work-orders pattern: `blocks-work-projects` is CLEAN.

| Package | Role |
|---|---|
| `blocks-workflow` | Infrastructure — state-machine engine (`IWorkflowRuntime`); NOT this package |
| `blocks-work-orders` | Domain — execution (WorkOrder, MaintenanceSchedule); SIBLING already shipped |
| `blocks-work-projects` | **THIS hand-off** — projects, milestones, budgets, time, remodel |

The namespace for this package is `Sunfish.Blocks.WorkProjects` (mirrors
`Sunfish.Blocks.WorkOrders`). NEVER share a namespace with `Sunfish.Blocks.Workflow.*`.

### Inline design decisions (resolves Stage 02 open questions in scope)

**Q2 — `ProjectActual` storage:** materialized table, sourced from
`Financial.JournalEntryPosted` events filtered by `dimensions.projectId`.
Stage 02 §7 Q2 raised the choice between materialized vs event-sourced
read-model. Decision: **materialized**, but **derivation is event-sourced**
(the projector consumes `Financial.JournalEntryPosted` and upserts
`ProjectActual` rows). This combines low read latency (Anchor Surface
Pro 7 target) with full event-replay rebuildability per `crdt-friendly-
schema-conventions.md` §7 (event-sourcing posture).

**Q4 — TimeEntry approval policy:** per-Tenant default + per-Party opt-out.
For this hand-off ship the **simpler v1**: every `TimeEntry` requires
approval before `Work.TimeEntryApproved` fires. The approval is performed
via `ITimeApprovalService.ApproveAsync(timeEntryId, approverPartyId)`.
Tenant-level auto-approve and per-Party opt-out are deferred to a
follow-on hand-off (see §Deferred work).

**Q7 — CRDT state-machine pattern for `Project.status`:** **Pattern A
(designated authority)** per `crdt-friendly-schema-conventions.md` §7.
A `Project` row carries `OwnerPartyId`; the replica logged in as the
owner-Party (or another principal explicitly delegated via PartyRole) is
the authority for state transitions. Other replicas display the project
+ may propose transitions (via UI "request status change" affordance)
but cannot directly advance the status field. **Why Pattern A here:**
project lifecycle changes (start, complete, close-out) have downstream
side-effects (invoice triggers, ledger reconciliation, budget freeze)
that must run exactly once; designating the authority replica avoids
divergent side-effects. The service layer enforces this by checking
`_actor.GetCurrentPartyId() == project.OwnerPartyId` (or a delegated-
authority role record) on every `TransitionStatusAsync` call.

**Q10 — `Project.code` collision-free under CRDT:**
Use `PRJ-{yyyy}-{replicaSuffix}{seq:0000}` where `replicaSuffix` is the
2-char install-time replica id from `foundation-localfirst` (per
`crdt-friendly-schema-conventions.md` §1.8). Example: `PRJ-2026-CW0014`.
Per-replica monotonic sequence; numbers are not globally monotonic but
display order remains stable via `(createdAt, replicaId, sequence)`.

**Q12 — Eventing transport:**
Per the T21-59Z ruling, the cross-cluster envelope is the 10-field
producer-facing shape; storage adds 3 denormalization columns at
append-time. This hand-off declares `IDomainEventPublisher` LOCALLY (same
pattern as `blocks-financial-periods`); the sweep PR migrates to the
canonical `foundation-events` interface when that package ships.
Idempotency keys use the catalog format (kebab-case-prefix:{entityId}
[:{secondaryKey}]) from PR 1 — no T21-12Z-format keys may ship in this
hand-off.

### Inline scope-edge resolutions

**WorkOrder ↔ Project relationship direction:** `WorkOrder.ProjectId?`
(already present on `blocks-work-orders.Models.WorkOrder` per the sibling
hand-off §PR1) is the canonical link. `Project` does NOT carry a
`WorkOrderIds[]` reverse list — read queries fan out from
`IWorkOrderReadModel.GetByProjectAsync(projectId)`. This avoids
denormalization drift and keeps `WorkOrder` as the system-of-record for
its own assignment.

**`TimeEntry` target polymorphism:** Stage 02 §2.20 specifies exactly
one of `projectId | workOrderId | maintenanceTaskId` is set. PR 3 enforces
this via a validation rule + a SQL `CHECK` constraint. The
`maintenanceTaskId` target requires `blocks-work-orders.MaintenanceTask`
(already shipped via sibling PR 2); no new dependency.

**`ProjectActual` is event-sourced + rebuildable.** The projector cursor
lives in the canonical `event_handler_cursors` table (per
`cross-cluster-event-bus-design.md` §5). Reset-the-cursor-and-replay is
the rebuild path; the `ProjectActual` table is treated as derived state
(per `crdt-friendly-schema-conventions.md` §7).

### FOSS attribution (binding for Stage 06)

Per `blocks-work-schema-design.md` §6 (cluster-wide license posture) —
attribution headers required at implementation time for borrowed permissive
sources. **For this hand-off:**

- `Project` / `ProjectMilestone` / `ProjectBudget` shapes:
  `// Inspired by Apache OFBiz WorkEffort + AgreementItem modules (Apache 2.0) — clean-room expression.`
- `TimeEntry` / `TimeLog` shapes:
  `// Activity-kind taxonomy informed by Redmine (GPLv2) clean-room study — no code copied; pattern only.`
  (NOTE: this is a study attribution, not a code-borrow — Redmine is GPLv2; the comment exists for audit-trail discipline only.)
- `RemodelProject` / `RemodelPhase`:
  `// Multi-phase WBS pattern informed by OpenProject (GPLv3) clean-room study — no code copied; pattern only.`
  (Same posture as Redmine — clean-room study only.)
- `ProjectActual` event-sourced derivation:
  `// Inspired by Apache OFBiz GlAccountTrans posting model (Apache 2.0) — derived-state-from-journal pattern.`

`NOTICE.md` for the package is required (PR 6) — same template shape as
the sibling `blocks-work-orders/NOTICE.md` from the prior hand-off.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify greenfield state.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-work-projects"
   ```
   Expected: empty (greenfield). If anything exists, file `cob-question-*`.

2. **Verify sibling work-orders cluster shipped + on main.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-work-orders/Models/WorkOrderId.cs
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-work-orders/Models/MaintenanceTaskId.cs
   ```
   Expected: both exist (cross-cluster references depend on them).

3. **Confirm ADR 0088 status.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```
   Expected: `status: Accepted` (per W#60 P4 ratification trail).

4. **Confirm no parallel-session PRs touch `blocks-work-projects/`.**
   ```bash
   gh pr list --state open --search "blocks-work-projects in:title,body"
   gh pr list --state open --search "blocks-work in:title,body"
   ```
   Expected: empty (or only sibling work-orders chore PRs). If anything
   touches projects/budget/milestone scope, file `cob-question-*` before
   PR 1.

5. **Check `blocks-people-foundation` arrival.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-foundation/Services/IPartyReadModel.cs 2>/dev/null
   ```
   - **If present** → import the canonical `IPartyReadModel` in PR 3.
   - **If absent** → mirror the sibling work-orders local stub (one-method
     `GetDisplayNameAsync`) with `// TODO: relocate import to
     Sunfish.Blocks.People.Foundation when ready` comment. See H1.

6. **Check `foundation-events` arrival.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/foundation-events/IDomainEventPublisher.cs 2>/dev/null
   ```
   - **If present** → import the canonical `IDomainEventPublisher`.
   - **If absent** → declare locally with the 10-field envelope shape;
     register `NoopDomainEventPublisher` via `TryAddSingleton<>` so a
     downstream sweep can override. See H2.

7. **Check `blocks-financial-periods` arrival.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-periods/Services/IPeriodResolver.cs 2>/dev/null
   ```
   - **If present** → import + use for `TimeEntry` period-gating in PR 3.
   - **If absent** → ship a local stub (`InMemoryPeriodResolver` always
     returns `Open`); document the swap-target in a `// TODO:` comment.
     See H3.

8. **Verify `but status` / `git status` clean** and current branch is
   `main` (or a fresh worktree from `main`, per
   `feedback_worktree_base_main_not_gitbutler.md`).

9. **Read the 10-field envelope spec** in
   `_shared/engineering/cross-cluster-event-bus-design.md` §1 + the
   T21-59Z ruling at
   `coordination/_archive/xo-ruling-2026-05-16T21-59Z-cob-envelope-and-idempotency-reconciliation.md`.
   The producer-side envelope has 10 fields (`eventId`, `eventType`,
   `schemaVersion`, `occurredAt`, `tenantId`, `originatingReplicaId`,
   `causationId?`, `correlationId?`, `idempotencyKey`, `payload`). Do NOT
   add `recordedAtUtc` / `producerCluster` / `producerEntity*` to the
   producer envelope — those are store-side denormalization columns
   added by the foundation-events SQLite store at insert-time.

---

## Per-PR deliverables

This hand-off splits into **6 PRs** by responsibility. PR 1 is the
foundational entity scaffold; PRs 2 + 3 + 4 + 5 are largely independent
once PR 1 lands and can be parallelized; PR 6 sequences last (DI + docs +
importer entry-points).

---

### PR 1 — Package scaffold + `Project` + `ProjectStatus` + `ProjectMilestone`

**Branch:** `cob/blocks-work-projects-core-entities`
**Commit subject:** `feat(blocks-work-projects): scaffold Project + ProjectMilestone + 8-state ProjectStatus machine`
**Estimated effort:** ~2.5–3h
**Scope:** package scaffold; `Project` + `ProjectStatus` 8-state machine; `ProjectMilestone` + `MilestoneStatus`; ID types; FOSS attribution headers; ~14 tests

#### Files

| File | Role |
|---|---|
| `packages/blocks-work-projects/Sunfish.Blocks.WorkProjects.csproj` | Package project; targets `net9.0`; refs `foundation-*` per ADR 0015 + `blocks-work-orders` for `WorkOrderId` |
| `packages/blocks-work-projects/NOTICE.md` | FOSS attribution (Apache OFBiz workeffort + AgreementItem; clean-room study refs for Redmine + OpenProject) |
| `packages/blocks-work-projects/README.md` | Package overview; cross-refs Stage 02 §2.1–§2.8 + ADR 0088 |
| `packages/blocks-work-projects/Models/ProjectId.cs` | `readonly record struct ProjectId(Ulid Value)` |
| `packages/blocks-work-projects/Models/MilestoneId.cs` | `readonly record struct MilestoneId(Ulid Value)` |
| `packages/blocks-work-projects/Models/ProjectKind.cs` | `enum ProjectKind { Generic, Remodel, Capex, Turnover, CapitalImprovement }` |
| `packages/blocks-work-projects/Models/ProjectStatus.cs` | `enum ProjectStatus { Draft, Planned, InProgress, OnHold, Blocked, Completed, Closed, Cancelled }` |
| `packages/blocks-work-projects/Models/ProjectStatusMachine.cs` | `static bool CanTransition(ProjectStatus from, ProjectStatus to)` — enforces state diagram per Stage 02 §2.2 |
| `packages/blocks-work-projects/Models/MilestoneKind.cs` | `enum MilestoneKind { Schedule, Payment, Gate, DeliverableDue }` |
| `packages/blocks-work-projects/Models/MilestoneStatus.cs` | `enum MilestoneStatus { Pending, AtRisk, Achieved, Missed, Cancelled }` |
| `packages/blocks-work-projects/Models/Priority.cs` | `enum Priority { Low, Normal, High, Urgent }` (mirror of sibling work-orders) |
| `packages/blocks-work-projects/Models/Project.cs` | Entity per Stage 02 §2.1; `static Project Create(...)` factory |
| `packages/blocks-work-projects/Models/ProjectMilestone.cs` | Entity per Stage 02 §2.3 |
| `packages/blocks-work-projects/tests/Sunfish.Blocks.WorkProjects.Tests.csproj` | Test project; refs `xunit` + `FluentAssertions` + `NodaTime.Testing` |

#### `Project` entity shape (binding — Stage 02 §2.1 mapped to C#)

```csharp
// Inspired by Apache OFBiz WorkEffort + AgreementItem modules (Apache 2.0) — clean-room expression.
public sealed class Project
{
    public ProjectId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Code { get; private set; }         // PRJ-{yyyy}-{replicaSuffix}{seq:0000}; set on Create(); never mutated
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public ProjectKind Kind { get; private set; }
    public ProjectStatus Status { get; private set; }
    public Priority Priority { get; private set; }

    // Cross-cluster anchors (loose FK — no EF navigation; read-model resolves names)
    public Guid? PropertyId { get; private set; }    // → blocks-property-*.Property
    public Guid? AssetId { get; private set; }       // → blocks-property-*.Asset
    public Guid? UnitId { get; private set; }        // → blocks-property-*.Unit
    public Guid? CustomerPartyId { get; private set; } // → blocks-people-*.Party (external customer)

    // Hierarchy
    public ProjectId? ParentProjectId { get; private set; }

    // Scheduling
    public LocalDate? PlannedStartDate { get; private set; }
    public LocalDate? PlannedEndDate { get; private set; }
    public LocalDate? ActualStartDate { get; private set; }
    public LocalDate? ActualEndDate { get; private set; }

    // Ownership (Party-model-convention §4)
    public Guid OwnerPartyId { get; private set; }   // → blocks-people-*.Party (REQUIRED — designated authority per CRDT Pattern A)
    public Guid? SponsorPartyId { get; private set; }

    // Roll-ups (denormalized; recomputed)
    public decimal? BudgetedAmount { get; private set; }
    public string? BudgetedCurrency { get; private set; }
    public decimal? ActualAmount { get; private set; }
    public string? ActualCurrency { get; private set; }
    public decimal? PercentComplete { get; private set; }  // 0..100

    // Tags / classification
    public IReadOnlyList<string> Tags { get; private set; } = Array.Empty<string>();
    public Instant? ArchivedAt { get; private set; }

    // Audit envelope (per crdt-friendly-schema-conventions §13)
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public Instant? DeletedAt { get; private set; }
    public long Version { get; private set; }

    public static Project Create(
        TenantId tenantId,
        ProjectId id,
        string code,                       // pre-generated via IProjectCodeGenerator
        string name,
        ProjectKind kind,
        Priority priority,
        Guid ownerPartyId,
        Guid createdBy,
        Instant createdAt,
        string? description = null,
        Guid? propertyId = null,
        Guid? assetId = null,
        Guid? unitId = null,
        Guid? customerPartyId = null,
        ProjectId? parentProjectId = null,
        Guid? sponsorPartyId = null,
        LocalDate? plannedStartDate = null,
        LocalDate? plannedEndDate = null,
        IReadOnlyList<string>? tags = null);

    public void TransitionStatus(ProjectStatus to, Guid updatedBy, Instant updatedAt);
    public void UpdatePlannedDates(LocalDate? start, LocalDate? end, Guid updatedBy, Instant updatedAt);
    public void RecordActualStart(LocalDate date, Guid updatedBy, Instant updatedAt);
    public void RecordActualEnd(LocalDate date, Guid updatedBy, Instant updatedAt);
    public void UpdateRollups(decimal? budgetedAmount, decimal? actualAmount, decimal? percentComplete, string? currency);
    public void Archive(Guid updatedBy, Instant updatedAt);
    public void SoftDelete(Guid deletedBy, Instant deletedAt);
}
```

#### `ProjectStatusMachine` transitions (binding — Stage 02 §2.2 state diagram)

Implement as a `static readonly Dictionary<ProjectStatus, ImmutableHashSet<ProjectStatus>>`:

| From | Allowed transitions |
|---|---|
| `Draft` | `{Planned, Cancelled}` |
| `Planned` | `{InProgress, Cancelled}` |
| `InProgress` | `{OnHold, Blocked, Completed, Cancelled}` |
| `OnHold` | `{InProgress, Cancelled}` |
| `Blocked` | `{InProgress, Cancelled}` |
| `Completed` | `{Closed, InProgress}` (reopen path) |
| `Closed` | `{}` (terminal except via audit-correction; see H6) |
| `Cancelled` | `{}` (terminal) |

`CanTransition(from, to)` returns `true` iff `to ∈ allowed[from]`.
Invalid transitions throw `InvalidProjectStatusTransitionException`.

**Per Stage 02 §2.2 transition rules:**
- `Completed` requires all child `WorkOrder.status ∈ {Completed, Cancelled}` — enforced at the service layer (PR 6), NOT at the entity layer. Add `IProjectService.ValidateCompletionAsync(projectId, CancellationToken)` for the cross-entity check.
- `Closed` requires `ProjectBudget` vs `ProjectActual` reconciliation row written — also enforced at the service layer (PR 6).

#### `ProjectMilestone` entity shape (binding — Stage 02 §2.3)

```csharp
public sealed class ProjectMilestone
{
    public MilestoneId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ProjectId ProjectId { get; private set; }
    public string Code { get; private set; }                   // unique within projectId (e.g., "M1", "M2")
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public MilestoneKind Kind { get; private set; }
    public LocalDate PlannedDate { get; private set; }
    public LocalDate? ActualDate { get; private set; }
    public MilestoneStatus Status { get; private set; }
    public decimal? Weight { get; private set; }               // 0..1
    public decimal? PaymentAmount { get; private set; }        // required if Kind=Payment
    public string? PaymentCurrency { get; private set; }
    public bool TriggersInvoice { get; private set; }
    public MilestoneId? PredecessorMilestoneId { get; private set; }
    public Guid? CustomerPartyId { get; private set; }         // denormalized from Project; required for invoice triggering

    // Audit envelope
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public Instant? DeletedAt { get; private set; }
    public long Version { get; private set; }

    public static ProjectMilestone Create(
        TenantId tenantId,
        MilestoneId id,
        ProjectId projectId,
        string code,
        string name,
        MilestoneKind kind,
        LocalDate plannedDate,
        Guid createdBy,
        Instant createdAt,
        decimal? paymentAmount = null,
        string? paymentCurrency = null,
        bool triggersInvoice = false,
        MilestoneId? predecessorMilestoneId = null,
        Guid? customerPartyId = null,
        decimal? weight = null,
        string? description = null);

    public void MarkAtRisk(Guid updatedBy, Instant updatedAt);
    public void Achieve(LocalDate actualDate, Guid updatedBy, Instant updatedAt);
    public void Miss(Guid updatedBy, Instant updatedAt);
    public void Cancel(Guid updatedBy, Instant updatedAt);
}
```

**Validation (`Create` factory):**
- `paymentAmount` and `paymentCurrency` required if `kind == Payment`.
- `triggersInvoice == true` requires `customerPartyId != null` AND `paymentAmount != null`.
- `weight` ∈ [0, 1] when set.
- `predecessorMilestoneId` must not cycle (closure check at service layer; not at entity layer).

#### `IProjectCodeGenerator` (substrate stub — local)

```csharp
namespace Sunfish.Blocks.WorkProjects.Services;

public interface IProjectCodeGenerator
{
    /// <summary>
    /// Generates a Project.Code in the form PRJ-{yyyy}-{replicaSuffix}{seq:0000}
    /// per crdt-friendly-schema-conventions §1.8 (monotonic-per-replica).
    /// </summary>
    Task<string> NextAsync(TenantId tenantId, int year, CancellationToken ct = default);
}

/// <summary>
/// In-memory implementation; reads-and-increments a per-(tenant, year)
/// counter. Real implementation persists the counter in
/// foundation-localfirst's replica record.
/// </summary>
public sealed class InMemoryProjectCodeGenerator : IProjectCodeGenerator { /* ... */ }
```

The replica suffix MUST be obtained from `foundation-localfirst`'s
`IReplicaContext` when available; if absent, hard-code `"L0"` (local-
zero) as the suffix with a `// TODO: wire IReplicaContext when foundation-localfirst exposes it` comment. See H5.

#### Tests required — PR 1 (~14 tests)

`tests/ProjectTests.cs`:
- `Create_ValidInput_StatusIsDraft`
- `Create_KindRemodel_DoesNotRequireRemodelSidecar` (sidecar lives in PR 5; entity creation is independent)
- `Create_PlannedEndBeforeStart_Throws`
- `TransitionStatus_DraftToPlanned_Succeeds`
- `TransitionStatus_DraftToCompleted_Throws_InvalidTransition`
- `TransitionStatus_ClosedToInProgress_Throws_TerminalState`
- `Archive_SetsArchivedAt` (distinct from soft-delete)
- `SoftDelete_SetsDeletedAt_CanNoLongerTransition`

`tests/ProjectMilestoneTests.cs`:
- `Create_KindPayment_WithoutAmount_Throws`
- `Create_TriggersInvoice_WithoutCustomer_Throws`
- `Achieve_SetsActualDateAndStatus`
- `MarkAtRisk_FromPending_Succeeds`

`tests/ProjectStatusMachineTests.cs`:
- `AllTerminalStates_ReturnEmptyTransitions` (Closed, Cancelled)
- `StateMachine_AllowsReopen_CompletedToInProgress`

`tests/InMemoryProjectCodeGeneratorTests.cs`:
- `Next_TwoCallsSameTenantYear_ReturnsSequentialNumbers`
- `Next_FormatMatchesPRJ_YYYY_RR_NNNN_pattern`

#### Verification (PR 1)

- `dotnet build packages/blocks-work-projects/` succeeds.
- `dotnet test packages/blocks-work-projects/tests/` ~14 tests pass.
- `grep -r "Sunfish.Blocks.Workflow" packages/blocks-work-projects/` returns zero hits (namespace discipline).
- FOSS-attribution header lines present on `Project.cs` + `ProjectMilestone.cs`.

#### Do NOT in this PR

- Do NOT add `ProjectBudget` / `TimeEntry` / `ProjectActual` / `RemodelProject`. Those are PRs 2–5.
- Do NOT add `IProjectService`. The service surface lives in PR 6 (DI + service + docs).
- Do NOT add event publishers. PRs 2/3/4/5 each emit their own events.
- Do NOT depend on `blocks-people-foundation` — the entity layer has no Party lookups; cross-cluster name resolution happens at the read-model layer (PR 6).

---

### PR 2 — `ProjectBudget` + `ProjectBudgetLine` + `IProjectBudgetRepository`

**Branch:** `cob/blocks-work-projects-budget`
**Commit subject:** `feat(blocks-work-projects): add ProjectBudget + ProjectBudgetLine + non-overlap validation`
**Estimated effort:** ~1.5–2h
**Scope:** `ProjectBudget` header + `ProjectBudgetLine` per category; revision-aware budget repository; non-overlap validation across budget periods; ~9 tests

#### Files

| File | Role |
|---|---|
| `packages/blocks-work-projects/Models/ProjectBudgetId.cs` | `readonly record struct ProjectBudgetId(Ulid Value)` |
| `packages/blocks-work-projects/Models/ProjectBudgetLineId.cs` | `readonly record struct ProjectBudgetLineId(Ulid Value)` |
| `packages/blocks-work-projects/Models/BudgetCategory.cs` | `enum BudgetCategory { Labor, Materials, Equipment, Subcontract, Permits, Contingency, Other }` |
| `packages/blocks-work-projects/Models/ProjectBudget.cs` | Header entity (one row per revision per project) |
| `packages/blocks-work-projects/Models/ProjectBudgetLine.cs` | Per-category line (multiple per budget header) |
| `packages/blocks-work-projects/Services/IProjectBudgetRepository.cs` | Read/write surface — atomic header+lines insert; revision number derivation |
| `packages/blocks-work-projects/Services/InMemoryProjectBudgetRepository.cs` | In-memory implementation; `ConcurrentDictionary<ProjectBudgetId, (ProjectBudget header, IReadOnlyList<ProjectBudgetLine> lines)>` |

#### `ProjectBudget` entity shape (binding)

```csharp
public sealed class ProjectBudget
{
    public ProjectBudgetId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ProjectId ProjectId { get; private set; }
    public int RevisionNumber { get; private set; }            // 1-based per projectId; new revision = new row
    public LocalDate EffectiveFrom { get; private set; }
    public LocalDate? EffectiveUntil { get; private set; }     // null = current revision
    public string? Notes { get; private set; }

    // Audit envelope (Stage 02 §13 — but ProjectBudget is *append-only*: once
    // a revision is written, only EffectiveUntil may mutate when superseded.
    // Treat as posted-then-immutable per crdt-friendly-schema-conventions §6.)
    public Instant CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Instant? SupersededAt { get; private set; }         // set when a higher revision is written
    public Instant? DeletedAt { get; private set; }

    public static ProjectBudget Create(/* ... */);
    internal void Supersede(Instant at);                       // called by repository when revision N+1 lands
}

public sealed class ProjectBudgetLine
{
    public ProjectBudgetLineId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ProjectBudgetId BudgetId { get; private set; }
    public BudgetCategory Category { get; private set; }
    public Guid? GlAccountId { get; private set; }             // → blocks-financial-ledger.GLAccount (loose FK)
    public decimal BudgetedAmount { get; private set; }
    public string Currency { get; private set; }
    public string? Notes { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
}
```

#### `IProjectBudgetRepository` (binding)

```csharp
public interface IProjectBudgetRepository
{
    Task<ProjectBudget?> GetAsync(ProjectBudgetId id, CancellationToken ct = default);

    Task<ProjectBudget?> GetCurrentAsync(ProjectId projectId, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectBudget>> GetRevisionsAsync(ProjectId projectId, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectBudgetLine>> GetLinesAsync(ProjectBudgetId budgetId, CancellationToken ct = default);

    /// <summary>
    /// Inserts a new revision (auto-increments RevisionNumber), supersedes
    /// the prior current revision (sets EffectiveUntil + SupersededAt), and
    /// inserts the lines atomically.
    /// </summary>
    /// <exception cref="OverlappingBudgetRevisionException">
    /// Thrown if EffectiveFrom <= prior revision's EffectiveFrom.
    /// </exception>
    Task<ProjectBudget> InsertRevisionAsync(
        ProjectId projectId,
        LocalDate effectiveFrom,
        IReadOnlyCollection<ProjectBudgetLineDraft> lines,
        Guid createdBy,
        string? notes,
        CancellationToken ct = default);
}

public sealed record ProjectBudgetLineDraft(
    BudgetCategory Category,
    decimal BudgetedAmount,
    string Currency,
    Guid? GlAccountId = null,
    string? Notes = null);
```

#### Validation rules (PR 2)

- `Currency` is 3-letter ISO 4217.
- `BudgetedAmount > 0`.
- Within a single revision, each `Category` appears at most once (composite-key uniqueness on `(BudgetId, Category)`).
- `InsertRevisionAsync` is atomic: header + lines insert in one operation; failure rolls back both.
- `EffectiveFrom` of a new revision must be strictly after the prior current revision's `EffectiveFrom` — `OverlappingBudgetRevisionException` otherwise.

#### Tests required — PR 2 (~9 tests)

`tests/ProjectBudgetRepositoryTests.cs`:
- `InsertRevision_FirstRevision_RevisionNumberIs1`
- `InsertRevision_SecondRevision_RevisionNumberIs2_AndPriorIsSuperseded`
- `InsertRevision_SameEffectiveFromAsPrior_Throws_OverlappingBudgetRevision`
- `InsertRevision_DuplicateCategory_Throws`
- `InsertRevision_NegativeAmount_Throws`
- `GetCurrent_ReturnsLatestNonSuperseded`
- `GetRevisions_ReturnsAllRevisionsInChronologicalOrder`
- `GetLines_ReturnsAllLinesForRevision`

`tests/ProjectBudgetTests.cs`:
- `Supersede_SetsSupersededAt_OnlyInternallyCallable`

#### Verification (PR 2)

- `dotnet build` succeeds.
- All PR 1 + PR 2 tests pass.
- `IProjectBudgetRepository` surface is purely additive (no PR 1 break).

#### Do NOT in this PR

- Do NOT compute or update `Project.BudgetedAmount` rollup. That happens in PR 6's `IProjectService.RecomputeRollupsAsync`.
- Do NOT emit `Work.ProjectBudgetUpdated` events. (Reserved for a follow-on hand-off; not in the §3.2 catalog yet — adding it now would be premature.)
- Do NOT add EF Core migrations. In-memory only per the sibling work-orders pattern.

---

### PR 3 — `TimeEntry` + `TimeLog` + `ITimeEntryService` + `ITimeApprovalService` + period gating

**Branch:** `cob/blocks-work-projects-time`
**Commit subject:** `feat(blocks-work-projects): add TimeEntry + TimeApprovalService + period-gated submission + TimeEntryApproved event`
**Estimated effort:** ~2.5–3h
**Scope:** `TimeEntry` append-only series; `TimeLog` view; submission + approval workflow; period-gating via `IPeriodResolver` (stub if periods package absent); `Work.TimeEntrySubmitted` + `Work.TimeEntryApproved` event emission; ~14 tests

#### Files

| File | Role |
|---|---|
| `packages/blocks-work-projects/Models/TimeEntryId.cs` | `readonly record struct TimeEntryId(Ulid Value)` |
| `packages/blocks-work-projects/Models/ActivityKind.cs` | `enum ActivityKind { Labor, Travel, Consultation, Inspection, Admin, Callout, Overtime }` |
| `packages/blocks-work-projects/Models/TimeEntryStatus.cs` | `enum TimeEntryStatus { Open, Submitted, Approved, Rejected, Invoiced }` |
| `packages/blocks-work-projects/Models/TimeEntry.cs` | Entity per Stage 02 §2.20 |
| `packages/blocks-work-projects/Models/TimeLog.cs` | Read-side view; aggregation over `TimeEntry` for a (workerPartyId, dateRange) tuple |
| `packages/blocks-work-projects/Services/ITimeEntryService.cs` | Write surface: `OpenAsync`, `StopAsync`, `SubmitAsync`, `UpdateDescriptionAsync` |
| `packages/blocks-work-projects/Services/InMemoryTimeEntryService.cs` | In-memory implementation |
| `packages/blocks-work-projects/Services/ITimeApprovalService.cs` | `ApproveAsync(TimeEntryId, approverPartyId, ...)`, `RejectAsync(TimeEntryId, reason, ...)` |
| `packages/blocks-work-projects/Services/InMemoryTimeApprovalService.cs` | In-memory; emits `Work.TimeEntryApproved` on approve |
| `packages/blocks-work-projects/Services/IPeriodResolver.cs` | **LOCAL STUB (only if blocks-financial-periods absent)** — `Task<PeriodSnapshot?> ResolveAsync(ChartId?, LocalDate, CancellationToken)` |
| `packages/blocks-work-projects/Services/InMemoryPeriodResolver.cs` | Local stub; always returns `PeriodSnapshot(Status=Open)` |
| `packages/blocks-work-projects/Events/IDomainEventPublisher.cs` | **LOCAL STUB (only if foundation-events absent)** — single-method publisher with 10-field envelope |
| `packages/blocks-work-projects/Events/NoopDomainEventPublisher.cs` | Local stub; collects in a `List<object>` for tests; registered via `TryAddSingleton<>` |
| `packages/blocks-work-projects/Events/TimeEntrySubmittedEvent.cs` | Payload + envelope record |
| `packages/blocks-work-projects/Events/TimeEntryApprovedEvent.cs` | Payload + envelope record |

#### `TimeEntry` entity shape (binding — Stage 02 §2.20)

```csharp
public sealed class TimeEntry
{
    public TimeEntryId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid WorkerPartyId { get; private set; }            // → blocks-people-*.Party (REQUIRED)

    // Target — EXACTLY ONE of these is non-null (CHECK constraint at SQL layer; runtime invariant at entity layer)
    public ProjectId? ProjectId { get; private set; }
    public Guid? WorkOrderId { get; private set; }             // → blocks-work-orders.WorkOrder
    public Guid? MaintenanceTaskId { get; private set; }       // → blocks-work-orders.MaintenanceTask

    public ActivityKind ActivityKind { get; private set; }
    public Instant StartedAt { get; private set; }
    public Instant? EndedAt { get; private set; }              // null while running
    public int DurationMinutes { get; private set; }           // derived; recomputed from start/end; stored for query speed

    public bool Billable { get; private set; }
    public decimal? HourlyRate { get; private set; }           // captured at entry-stop time
    public string? HourlyRateCurrency { get; private set; }
    public decimal? Amount { get; private set; }               // = (durationMinutes/60) × hourlyRate; computed
    public Guid? GlAccountId { get; private set; }             // → blocks-financial-ledger.GLAccount
    public string? Description { get; private set; }

    // Workflow
    public TimeEntryStatus Status { get; private set; }
    public Instant? SubmittedAt { get; private set; }
    public Guid? ApprovedByPartyId { get; private set; }
    public Instant? ApprovedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public bool InvoicedFlag { get; private set; }             // one-way

    // Audit envelope — TimeEntry is append-only AFTER Approved; corrections via reverse + new entry
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public Instant? DeletedAt { get; private set; }
    public long Version { get; private set; }

    public static TimeEntry Open(/* ... */);                   // Status=Open; StartedAt=now; EndedAt=null
    public void Stop(Instant endedAt, decimal? hourlyRate, string? rateCurrency, Guid updatedBy);
    public void Submit(Instant submittedAt, Guid updatedBy);   // Status=Open → Submitted
    internal void Approve(Guid approverPartyId, Instant approvedAt);  // called by ITimeApprovalService
    internal void Reject(string reason, Guid approverPartyId, Instant rejectedAt);
    public void MarkInvoiced();                                // one-way; called by financial cluster reactor
}
```

**Invariants (entity-layer):**
- Exactly one of `(ProjectId, WorkOrderId, MaintenanceTaskId)` is set on `Open()`.
- `EndedAt >= StartedAt` on `Stop()`.
- `DurationMinutes = floor((EndedAt - StartedAt) / 60_000)`; recomputed on `Stop()`.
- `Amount = round(DurationMinutes / 60 * HourlyRate, 2)` when both `DurationMinutes` and `HourlyRate` are non-null.
- Status transitions: `Open → Submitted → Approved | Rejected → Invoiced` (terminal). Reverse paths forbidden — corrections via new entry.
- Once `Status == Approved` the entity is **append-only at the row level** (per `crdt-friendly-schema-conventions §6` — posted-then-immutable). `MarkInvoiced()` is the sole permitted post-approval mutation.

#### `ITimeEntryService` (binding)

```csharp
public interface ITimeEntryService
{
    Task<TimeEntry> OpenAsync(
        TenantId tenantId,
        Guid workerPartyId,
        ActivityKind kind,
        Instant startedAt,
        ProjectId? projectId = null,
        Guid? workOrderId = null,
        Guid? maintenanceTaskId = null,
        bool billable = true,
        Guid? glAccountId = null,
        string? description = null,
        CancellationToken ct = default);

    Task<TimeEntry> StopAsync(
        TimeEntryId id,
        Instant endedAt,
        decimal? hourlyRate,
        string? rateCurrency,
        Guid updatedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Transitions Open → Submitted. Enforces period-gating: looks up the
    /// containing FiscalPeriod via IPeriodResolver; rejects with
    /// PeriodSoftClosedOrLockedException if the period is not Open.
    /// </summary>
    Task<TimeEntry> SubmitAsync(
        TimeEntryId id,
        Instant submittedAt,
        Guid updatedBy,
        CancellationToken ct = default);

    Task UpdateDescriptionAsync(TimeEntryId id, string description, Guid updatedBy, CancellationToken ct = default);
}
```

#### `ITimeApprovalService` (binding)

```csharp
public interface ITimeApprovalService
{
    /// <summary>
    /// Transitions Submitted → Approved. Emits Work.TimeEntryApproved.
    /// </summary>
    Task<TimeEntry> ApproveAsync(
        TimeEntryId id,
        Guid approverPartyId,
        Instant approvedAt,
        CancellationToken ct = default);

    Task<TimeEntry> RejectAsync(
        TimeEntryId id,
        Guid approverPartyId,
        Instant rejectedAt,
        string reason,
        CancellationToken ct = default);
}
```

#### `IPeriodResolver` (local stub — only if `blocks-financial-periods` absent)

If `blocks-financial-periods/Services/IPeriodResolver.cs` exists on main,
**import it** and skip this stub. Otherwise:

```csharp
namespace Sunfish.Blocks.WorkProjects.Services;

// TODO: relocate to Sunfish.Blocks.Financial.Periods when that package ships.
public interface IPeriodResolver
{
    Task<PeriodSnapshot?> ResolveAsync(
        Guid? chartOfAccountsId,
        LocalDate date,
        CancellationToken ct = default);
}

public enum PeriodStatus { Open, SoftClosed, Locked }

public sealed record PeriodSnapshot(
    Guid PeriodId,
    LocalDate StartDate,
    LocalDate EndDate,
    PeriodStatus Status);

public sealed class InMemoryPeriodResolver : IPeriodResolver
{
    public Task<PeriodSnapshot?> ResolveAsync(Guid? chartId, LocalDate date, CancellationToken ct = default)
        => Task.FromResult<PeriodSnapshot?>(new PeriodSnapshot(Guid.NewGuid(), date, date, PeriodStatus.Open));
}
```

The submit-side gating rejects with `PeriodSoftClosedOrLockedException`
when the resolved period's status is `SoftClosed` (non-admin) or `Locked`
(any caller). Admin override is deferred — for v1, the stub never
returns non-Open, so the test suite focuses on the happy path; one
explicit "stub-returns-SoftClosed-via-test-double" test exercises the
rejection branch.

#### `IDomainEventPublisher` (local stub — only if `foundation-events` absent)

If `foundation-events/IDomainEventPublisher.cs` exists on main, **import
it** and skip this stub. Otherwise (per the canonical pattern from
`blocks-financial-periods` PR 2):

```csharp
namespace Sunfish.Blocks.WorkProjects.Events;

// TODO: relocate to Sunfish.Foundation.Events when that package ships.
public interface IDomainEventPublisher
{
    Task PublishAsync<TPayload>(DomainEventEnvelope<TPayload> envelope, CancellationToken ct = default);
}

/// <summary>
/// Canonical 10-field envelope per cross-cluster-event-bus-design.md §1
/// (post T21-59Z ruling: producer fields only; recordedAtUtc and producer*
/// columns are added store-side).
/// </summary>
public sealed record DomainEventEnvelope<TPayload>(
    Ulid EventId,                          // 1
    string EventType,                      // 2 — e.g., "Work.TimeEntryApproved"
    string SchemaVersion,                  // 3 — semver, e.g., "1.0.0"
    Instant OccurredAt,                    // 4
    TenantId TenantId,                     // 5
    string OriginatingReplicaId,           // 6 — 2-char suffix
    Ulid? CausationId,                     // 7
    string? CorrelationId,                 // 8
    string IdempotencyKey,                 // 9 — catalog format per T21-59Z
    TPayload Payload                       // 10
);

public sealed class NoopDomainEventPublisher : IDomainEventPublisher
{
    private readonly List<object> _captured = new();
    public IReadOnlyList<object> Captured => _captured;
    public Task PublishAsync<TPayload>(DomainEventEnvelope<TPayload> envelope, CancellationToken ct = default)
    {
        lock (_captured) _captured.Add(envelope);
        return Task.CompletedTask;
    }
}
```

DI registration (PR 6) uses `services.TryAddSingleton<IDomainEventPublisher, NoopDomainEventPublisher>();` so the foundation-events sweep can supersede via plain `AddSingleton`.

#### Event payloads (binding — match `cross-cluster-event-bus-design.md` §3.2)

```csharp
public sealed record TimeEntrySubmittedPayload(
    TimeEntryId TimeEntryId,
    ProjectId? ProjectId,
    Guid? WorkOrderId,
    Guid? MaintenanceTaskId,
    Guid WorkerPartyId,
    int DurationMinutes,
    Instant SubmittedAt);

public sealed record TimeEntryApprovedPayload(
    TimeEntryId TimeEntryId,
    ProjectId? ProjectId,
    Guid? WorkOrderId,
    Guid WorkerPartyId,
    int DurationMinutes,
    decimal? Amount,
    string? Currency,
    Guid? GlAccountId,
    Instant ApprovedAt);
```

**Idempotency keys** (per `cross-cluster-event-bus-design.md` §3.2 +
T21-59Z ruling — catalog format):

| Event | Idempotency key |
|---|---|
| `Work.TimeEntrySubmitted` | `time-submitted:{timeEntryId}` |
| `Work.TimeEntryApproved` | `time-approved:{timeEntryId}` |

Both are one-shot (submission and approval are append-only on the
TimeEntry's lifecycle) — no `:{occurredAtTicks}` suffix needed.

**Catalog upkeep:** if `cross-cluster-event-bus-design.md` §3.2 already
lists `Work.TimeEntryApproved` (it does, per the §3.2 table) — leave it.
Add `Work.TimeEntrySubmitted` as a new row in the same PR (docs-only
edit to the catalog, bundled with PR 3).

#### Tests required — PR 3 (~14 tests)

`tests/TimeEntryTests.cs`:
- `Open_ValidInput_StatusIsOpen`
- `Open_MissingAllTargets_Throws` (none of project/workOrder/maintenanceTask set)
- `Open_TwoTargets_Throws` (e.g., both projectId and workOrderId set)
- `Stop_RecomputesDurationMinutes`
- `Stop_WithHourlyRate_ComputesAmount`
- `Stop_EndedBeforeStarted_Throws`
- `Submit_FromOpen_TransitionsToSubmitted`
- `Approve_FromSubmitted_TransitionsToApproved_IsImmutableAfter`

`tests/InMemoryTimeEntryServiceTests.cs`:
- `SubmitAsync_OpenPeriod_Succeeds`
- `SubmitAsync_SoftClosedPeriod_ThrowsPeriodSoftClosedOrLocked` (driven via a test-double `IPeriodResolver` returning `SoftClosed`)
- `SubmitAsync_LockedPeriod_Throws` (same)
- `SubmitAsync_EmitsWorkTimeEntrySubmittedEvent_WithCatalogIdempotencyKey`

`tests/InMemoryTimeApprovalServiceTests.cs`:
- `Approve_TransitionsAndEmitsTimeEntryApprovedEvent`
- `Approve_RejectsAlreadyInvoiced` (post-Approved invariant)

`tests/DomainEventEnvelopeTests.cs`:
- `Envelope_Has10ProducerFields_NoRecordedAtUtc` (regression for T21-59Z ruling discipline)

#### Verification (PR 3)

- All previous PR tests pass.
- New PR 3 tests pass.
- Submitted + Approved events use **catalog idempotency-key format only**
  (`grep -rn "time-submitted\\|time-approved" packages/blocks-work-projects/`
  shows the expected formats; no pipe-delimited T21-12Z format).
- The 10-field envelope record has exactly 10 positional fields — no
  `RecordedAtUtc` / `ProducerCluster` / `ProducerEntity*`.

#### Do NOT in this PR

- Do NOT register `IDomainEventPublisher` as `AddSingleton` (use `TryAddSingleton`); the foundation-events sweep needs a clean override path.
- Do NOT post a `Financial.JournalEntry` from `ApproveAsync`. The financial cluster reactor consumes `Work.TimeEntryApproved` and posts the labor JE itself (per Stage 02 §4.6); cross-cluster writes are forbidden (per `cross-cluster-event-bus-design.md` §9).
- Do NOT compute `Project.ActualAmount` rollup here — `ProjectActual` is consumed event-source style in PR 4.

---

### PR 4 — `ProjectActual` + `IProjectActualProjector` (event-sourced from JournalEntryPosted)

**Branch:** `cob/blocks-work-projects-actuals-projector`
**Commit subject:** `feat(blocks-work-projects): add ProjectActual + IProjectActualProjector consuming Financial.JournalEntryPosted`
**Estimated effort:** ~1.5–2h
**Scope:** `ProjectActual` materialized table; `IProjectActualProjector` event handler consuming `Financial.JournalEntryPosted` filtered by `dimensions.projectId`; cursor advancement; idempotency on `(projectId, sourceKind, sourceRefId)`; ~9 tests

#### Files

| File | Role |
|---|---|
| `packages/blocks-work-projects/Models/ProjectActualId.cs` | `readonly record struct ProjectActualId(Ulid Value)` |
| `packages/blocks-work-projects/Models/ActualSourceKind.cs` | `enum ActualSourceKind { WorkOrderLine, TimeEntry, JournalEntry, Invoice, Manual }` |
| `packages/blocks-work-projects/Models/ProjectActual.cs` | Entity per Stage 02 §2.22 |
| `packages/blocks-work-projects/Services/IProjectActualRepository.cs` | Read-side: `GetByProjectAsync`, `GetByCategoryAsync`, `GetTotalsAsync` |
| `packages/blocks-work-projects/Services/InMemoryProjectActualRepository.cs` | In-memory implementation |
| `packages/blocks-work-projects/Events/JournalEntryPostedHandler.cs` | `IDomainEventHandler<JournalEntryPostedPayload>` — extracts `dimensions.projectId`; upserts `ProjectActual` row |
| `packages/blocks-work-projects/Events/JournalEntryPostedPayload.cs` | Receiver-side mirror of the financial payload (per `cross-cluster-event-bus-design.md` §3.1) — local read-only record |
| `packages/blocks-work-projects/Services/IProjectActualProjector.cs` | Service that wires the handler to the dispatcher; surfaces a `RebuildFromCursorAsync(fromEventId?)` for replay |
| `packages/blocks-work-projects/Services/InMemoryProjectActualProjector.cs` | In-memory implementation; tracks the handler cursor per `cross-cluster-event-bus-design.md` §5 |

#### `ProjectActual` entity shape (binding — Stage 02 §2.22)

```csharp
public sealed class ProjectActual
{
    public ProjectActualId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ProjectId ProjectId { get; private set; }
    public BudgetCategory Category { get; private set; }
    public Guid? GlAccountId { get; private set; }
    public decimal PostedAmount { get; private set; }
    public string Currency { get; private set; }
    public LocalDate PostedDate { get; private set; }
    public ActualSourceKind SourceKind { get; private set; }
    public Guid? SourceRefId { get; private set; }             // → WorkOrderLine | TimeEntry | JournalEntry | Invoice
    public string? Notes { get; private set; }

    // Audit envelope — append-only (derived from events); no UpdatedAt/By beyond CreatedAt
    public Instant CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }                // event-projector principal
    public Instant? DeletedAt { get; private set; }            // tombstone if upstream JE reversed

    public static ProjectActual Create(/* ... */);
    internal void Tombstone(Instant at);                       // called on JournalEntryReversed
}
```

#### `JournalEntryPostedHandler` algorithm (binding)

```csharp
public sealed class JournalEntryPostedHandler
{
    public async Task HandleAsync(
        DomainEventEnvelope<JournalEntryPostedPayload> evt,
        CancellationToken ct)
    {
        var payload = evt.Payload;

        // Filter — only handle JEs that carry a project dimension
        foreach (var line in payload.Lines)
        {
            if (!line.Dimensions.TryGetValue("projectId", out var projectIdStr)
                || !Guid.TryParse(projectIdStr, out var projectIdGuid))
                continue;

            var projectId = new ProjectId(Ulid.Parse(projectIdStr));

            // Idempotency — composite key (projectId, sourceKind, sourceRefId)
            var sourceKind = MapSourceKind(payload.SourceKind);
            var existing = await _repository.FindAsync(projectId, sourceKind, payload.EntryId, ct);
            if (existing is not null) return;   // already projected

            // Category — derived from GL account type (best-effort) or fallback Other
            var category = await DeriveCategoryAsync(line.AccountId, ct);

            var actual = ProjectActual.Create(
                tenantId: evt.TenantId,
                id: new ProjectActualId(Ulid.NewUlid()),
                projectId: projectId,
                category: category,
                glAccountId: line.AccountId,
                postedAmount: line.Debit - line.Credit,
                currency: line.Currency ?? "USD",
                postedDate: payload.EntryDate,
                sourceKind: sourceKind,
                sourceRefId: payload.EntryId,
                createdAt: _time.GetCurrentInstant(),
                createdBy: ProjectorPrincipalId);

            await _repository.InsertAsync(actual, ct);
        }
    }

    private ActualSourceKind MapSourceKind(string financialSourceKind) =>
        financialSourceKind switch
        {
            "TimeEntry" => ActualSourceKind.TimeEntry,
            "Invoice" => ActualSourceKind.Invoice,
            "Bill" => ActualSourceKind.JournalEntry,
            _ => ActualSourceKind.JournalEntry,
        };
}
```

**Idempotency contract:** the composite key `(projectId, sourceKind, sourceRefId)` is the projector's effective dedupe. The handler's cursor (per `cross-cluster-event-bus-design.md` §5) is the dispatcher's dedupe; both layers must be safe under replay.

**Reversal handling (deferred):** `Financial.JournalEntryReversed`
consumption is **NOT in scope** for this PR. When a reversal JE posts,
the projector creates a NEW `ProjectActual` row with the reversal
amounts (typically negative). Tombstoning the original is reserved for a
follow-on hand-off where reversal-semantics are explicit.

#### Tests required — PR 4 (~9 tests)

`tests/ProjectActualTests.cs`:
- `Create_SetsAllFields_FromHandlerInputs`
- `Tombstone_SetsDeletedAt`

`tests/JournalEntryPostedHandlerTests.cs`:
- `Handle_JeWithoutProjectDimension_Skips`
- `Handle_JeWithProjectDimension_CreatesProjectActual`
- `Handle_SameJeProjected_Idempotent` (second call no-ops)
- `Handle_MapsSourceKindTimeEntry_Correctly`
- `Handle_MultipleLinesSameProject_CreatesMultipleActualRows`
- `Handle_DebitMinusCreditComputed` (positive for debit-side; negative for credit-side)

`tests/InMemoryProjectActualProjectorTests.cs`:
- `RebuildFromCursor_ReplaysAllEventsFromCursor_IdempotentResult`

#### Verification (PR 4)

- All previous PR tests pass.
- New PR 4 tests pass.
- The handler uses **only** typed payload fields — never reaches into a
  raw `dictionary<string, object>` for financial data (one-way coupling
  via the typed `JournalEntryPostedPayload` record).

#### Do NOT in this PR

- Do NOT subscribe the handler to a real event bus. The dispatcher
  wire-up is foundation-events territory; this PR ships only the handler
  + projector facade.
- Do NOT recompute `Project.ActualAmount` rollup automatically — that's
  `IProjectService.RecomputeRollupsAsync` in PR 6.
- Do NOT consume `Financial.JournalEntryReversed` — out of scope (see
  above).

---

### PR 5 — `RemodelProject` + `RemodelPhase` + capitalization event

**Branch:** `cob/blocks-work-projects-remodel`
**Commit subject:** `feat(blocks-work-projects): add RemodelProject + RemodelPhase + Work.RemodelCapitalized event`
**Estimated effort:** ~1.5–2h
**Scope:** `RemodelProject` sidecar to `Project` when `Kind=Remodel`; `RemodelPhase` array; capitalization workflow → `Work.RemodelCapitalized` event; ~10 tests

#### Files

| File | Role |
|---|---|
| `packages/blocks-work-projects/Models/RemodelProjectId.cs` | `readonly record struct RemodelProjectId(Ulid Value)` |
| `packages/blocks-work-projects/Models/RemodelPhaseId.cs` | `readonly record struct RemodelPhaseId(Ulid Value)` |
| `packages/blocks-work-projects/Models/RemodelKind.cs` | `enum RemodelKind { Kitchen, Bath, WholeUnit, Exterior, Roof, SystemReplacement, Custom }` |
| `packages/blocks-work-projects/Models/PhaseStatus.cs` | `enum PhaseStatus { Planned, Active, Complete, OverBudget, Cancelled }` |
| `packages/blocks-work-projects/Models/RemodelProject.cs` | Entity per Stage 02 §2.8 |
| `packages/blocks-work-projects/Models/RemodelPhase.cs` | Entity (sub-entity table per CRDT conventions §4) |
| `packages/blocks-work-projects/Services/IRemodelProjectService.cs` | `CreateAsync`, `AddPhaseAsync`, `MarkPhaseCompleteAsync`, `CapitalizeAsync` |
| `packages/blocks-work-projects/Services/InMemoryRemodelProjectService.cs` | In-memory implementation; emits `Work.RemodelPhaseCompleted` + `Work.RemodelCapitalized` events |
| `packages/blocks-work-projects/Events/RemodelPhaseCompletedEvent.cs` | Envelope + payload record |
| `packages/blocks-work-projects/Events/RemodelCapitalizedEvent.cs` | Envelope + payload record |

#### `RemodelProject` entity shape (binding — Stage 02 §2.8)

```csharp
// Multi-phase WBS pattern informed by OpenProject (GPLv3) clean-room study — no code copied; pattern only.
public sealed class RemodelProject
{
    public RemodelProjectId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ProjectId ProjectId { get; private set; }            // → Project (1:1)

    // Scope
    public string ScopeStatement { get; private set; }
    public RemodelKind RemodelKind { get; private set; }

    // Permits
    public bool PermitRequired { get; private set; }
    public string? PermitNumber { get; private set; }
    public LocalDate? PermitIssuedAt { get; private set; }
    public IReadOnlyList<string> InspectionsRequired { get; private set; } = Array.Empty<string>();

    // Capitalization handoff to financial cluster
    public Guid? CapitalizationAccountId { get; private set; }  // → blocks-financial-ledger.GLAccount (CIP account)
    public LocalDate? PlacedInServiceAt { get; private set; }
    public Instant? CapitalizedAt { get; private set; }
    public decimal? CapitalizedAmount { get; private set; }
    public string? CapitalizedCurrency { get; private set; }

    // Audit envelope
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public Instant? DeletedAt { get; private set; }
    public long Version { get; private set; }

    public static RemodelProject Create(/* ... */);
    public void SetPermit(string permitNumber, LocalDate issuedAt, Guid updatedBy, Instant updatedAt);
    public void Capitalize(
        Guid capitalizationAccountId,
        LocalDate placedInServiceAt,
        decimal capitalizedAmount,
        string currency,
        Guid updatedBy,
        Instant capitalizedAt);
}

public sealed class RemodelPhase
{
    public RemodelPhaseId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public RemodelProjectId RemodelProjectId { get; private set; }
    public int Ordinal { get; private set; }                    // 1-based; unique per RemodelProjectId
    public string Name { get; private set; }                    // e.g., "demolition", "rough-in", "finish"
    public decimal BudgetedAmount { get; private set; }
    public string BudgetedCurrency { get; private set; }
    public decimal? ActualAmount { get; private set; }
    public LocalDate? PlannedStartDate { get; private set; }
    public LocalDate? PlannedEndDate { get; private set; }
    public LocalDate? ActualStartDate { get; private set; }
    public LocalDate? ActualEndDate { get; private set; }
    public PhaseStatus Status { get; private set; }

    public static RemodelPhase Create(/* ... */);
    public void Start(LocalDate startDate, Guid updatedBy, Instant updatedAt);
    public void Complete(LocalDate endDate, decimal? actualAmount, Guid updatedBy, Instant updatedAt);
    public void MarkOverBudget(decimal actualAmount, Guid updatedBy, Instant updatedAt);
    public void Cancel(Guid updatedBy, Instant updatedAt);
}
```

**Validation:**
- `RemodelProject.Create` requires the referenced `Project.Kind == Remodel` — checked at the service layer (PR 6 wiring).
- `PermitNumber` required when `PermitRequired == true`.
- `Capitalize` requires `CapitalizationAccountId` to be non-null.
- `Phase.Ordinal` unique per `RemodelProjectId` (service-layer enforced).

#### Capitalization workflow (binding — Stage 02 §4 +  cross-cluster contract)

```csharp
// In InMemoryRemodelProjectService.CapitalizeAsync(...)
public async Task<RemodelProject> CapitalizeAsync(
    RemodelProjectId id,
    Guid capitalizationAccountId,
    LocalDate placedInServiceAt,
    decimal capitalizedAmount,
    string currency,
    Guid updatedBy,
    CancellationToken ct)
{
    var rp = await _repo.GetAsync(id, ct)
             ?? throw new RemodelProjectNotFoundException(id);
    var phases = await _repo.GetPhasesAsync(id, ct);

    // Invariant — all phases must be Complete (Cancelled OK; OverBudget OK)
    if (phases.Any(p => p.Status == PhaseStatus.Planned || p.Status == PhaseStatus.Active))
        throw new RemodelHasIncompletePhases();

    var now = _time.GetCurrentInstant();
    rp.Capitalize(capitalizationAccountId, placedInServiceAt, capitalizedAmount, currency, updatedBy, now);
    await _repo.UpdateAsync(rp, ct);

    await _events.PublishAsync(new DomainEventEnvelope<RemodelCapitalizedPayload>(
        EventId: Ulid.NewUlid(),
        EventType: "Work.RemodelCapitalized",
        SchemaVersion: "1.0.0",
        OccurredAt: now,
        TenantId: rp.TenantId,
        OriginatingReplicaId: _replica.GetReplicaId(),
        CausationId: null,
        CorrelationId: null,
        IdempotencyKey: $"remodel-capitalized:{id}",
        Payload: new RemodelCapitalizedPayload(
            RemodelProjectId: id,
            ProjectId: rp.ProjectId,
            PropertyId: /* resolved via IProjectReadModel or null */,
            CapitalizationAccountId: capitalizationAccountId,
            CapitalizedAmount: capitalizedAmount,
            Currency: currency,
            PlacedInServiceDate: placedInServiceAt)
    ), ct);

    return rp;
}
```

**Cross-cluster contract:** the `Work.RemodelCapitalized` event is the
trigger for `blocks-financial-ledger` to post a capital-asset
`JournalEntry` (debit the FixedAsset/CIP account; credit the cost-
clearing account). **This hand-off DOES NOT implement that reactor** —
it's a future `blocks-financial-ledger` follow-on. The contract is
documented here; the financial side honors it on its own schedule.

#### Event payloads (binding — match `cross-cluster-event-bus-design.md` §3.2)

```csharp
public sealed record RemodelPhaseCompletedPayload(
    RemodelPhaseId PhaseId,
    RemodelProjectId RemodelProjectId,
    ProjectId ProjectId,
    int Ordinal,
    string Name,
    decimal? ActualAmount,
    string? Currency,
    LocalDate ActualEndDate);

public sealed record RemodelCapitalizedPayload(
    RemodelProjectId RemodelProjectId,
    ProjectId ProjectId,
    Guid? PropertyId,
    Guid CapitalizationAccountId,
    decimal CapitalizedAmount,
    string Currency,
    LocalDate PlacedInServiceDate);
```

**Idempotency keys** (catalog format per T21-59Z ruling):

| Event | Idempotency key |
|---|---|
| `Work.RemodelPhaseCompleted` | `remodel-phase-completed:{phaseId}` |
| `Work.RemodelCapitalized` | `remodel-capitalized:{remodelProjectId}` |

Both are one-shot (capitalization is terminal; phase-complete is
append-only per the phase lifecycle).

**Catalog upkeep:** `Work.RemodelCapitalized` is already in §3.2.
`Work.RemodelPhaseCompleted` is NEW — add to the catalog table in the
same PR (docs-only edit, bundled with PR 5).

#### Tests required — PR 5 (~10 tests)

`tests/RemodelProjectTests.cs`:
- `Create_ValidInput_StatusUnstarted`
- `SetPermit_RequiresIssuedDate`
- `Capitalize_WithoutAccount_Throws`
- `Capitalize_SetsCapitalizedAt`

`tests/RemodelPhaseTests.cs`:
- `Create_StatusIsPlanned`
- `Start_TransitionsToActive`
- `Complete_TransitionsToCompleteAndSetsActualEndDate`
- `MarkOverBudget_TransitionsToOverBudget`

`tests/InMemoryRemodelProjectServiceTests.cs`:
- `CapitalizeAsync_AllPhasesComplete_EmitsRemodelCapitalizedEvent`
- `CapitalizeAsync_PendingPhases_Throws_RemodelHasIncompletePhases`

#### Verification (PR 5)

- All previous PR tests pass.
- New PR 5 tests pass.
- `Work.RemodelCapitalized` event emitted with catalog idempotency key
  `remodel-capitalized:{remodelProjectId}` (verified via
  `NoopDomainEventPublisher.Captured`).
- Catalog row for `Work.RemodelPhaseCompleted` added to
  `_shared/engineering/cross-cluster-event-bus-design.md` §3.2 (single
  bundled commit).

#### Do NOT in this PR

- Do NOT post a `Financial.JournalEntry` directly. The financial
  cluster reactor handles capital-asset posting on its own (out of
  scope here).
- Do NOT compute phase rollups against `RemodelProject.actualAmount`
  beyond the per-phase `ActualAmount`. Aggregate rollups happen at the
  service layer (PR 6).

---

### PR 6 — `IProjectService` + `AddBlocksWorkProjects()` DI + `IErpnextProjectImporter` + `apps/docs/blocks-work-projects/overview.md`

**Branch:** `cob/blocks-work-projects-service-di-docs`
**Commit subject:** `feat(blocks-work-projects): IProjectService + AddBlocksWorkProjects + ERPNext importer entry + apps/docs overview`
**Estimated effort:** ~2–2.5h
**Scope:** `IProjectService` orchestrator (create/transition/rollup); cross-entity validations (cycle check on parent; completion gate); DI extension wiring every interface; `IErpnextProjectImporter` Pass-1 entry-point (idempotent on `externalRef`); cluster docs page; ~8 tests

#### Files

| File | Role |
|---|---|
| `packages/blocks-work-projects/Services/IProjectService.cs` | Write surface: `CreateAsync`, `TransitionStatusAsync`, `ValidateCompletionAsync`, `RecomputeRollupsAsync`, `AddMilestoneAsync`, `AddBudgetRevisionAsync` |
| `packages/blocks-work-projects/Services/InMemoryProjectService.cs` | In-memory implementation; cross-entity orchestration; emits `Work.ProjectCreated`, `Work.ProjectStatusChanged`, `Work.MilestoneCreated`, `Work.MilestoneAchieved`, `Work.MilestoneInvoiceTriggered` |
| `packages/blocks-work-projects/Services/IProjectReadModel.cs` | Read accessor exposed to other clusters: `GetByIdAsync`, `GetSummaryAsync` (code + name + status), `GetMilestonesAsync` |
| `packages/blocks-work-projects/Services/InMemoryProjectReadModel.cs` | In-memory implementation |
| `packages/blocks-work-projects/Services/IPartyReadModel.cs` | **LOCAL STUB** (one-method `GetDisplayNameAsync`) IF `blocks-people-foundation` absent; otherwise IMPORT canonical |
| `packages/blocks-work-projects/Services/InMemoryPartyReadModel.cs` | Local stub implementation (returns `null` for unknown IDs) |
| `packages/blocks-work-projects/Migration/IErpnextProjectImporter.cs` | `UpsertFromErpnextAsync(ErpnextProjectSource, ...)` → idempotent on `(source, externalRefId)` |
| `packages/blocks-work-projects/Migration/ErpnextProjectImporter.cs` | Implementation |
| `packages/blocks-work-projects/Migration/ErpnextProjectSource.cs` | Source record shape (mirrors sibling work-orders' importer convention) |
| `packages/blocks-work-projects/Migration/ImportOutcome.cs` | Reused from sibling pattern if accessible; otherwise local record |
| `packages/blocks-work-projects/WorkProjectsServiceCollectionExtensions.cs` | `AddBlocksWorkProjects()` |
| `apps/docs/blocks-work-projects/overview.md` | Cluster docs page |

#### `IProjectService` (binding)

```csharp
public interface IProjectService
{
    Task<Project> CreateAsync(
        TenantId tenantId,
        string name,
        ProjectKind kind,
        Priority priority,
        Guid ownerPartyId,
        Guid createdBy,
        ProjectId? parentProjectId = null,
        Guid? propertyId = null,
        Guid? customerPartyId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Transitions Project.Status. Enforces:
    /// - state-machine validity (ProjectStatusMachine.CanTransition)
    /// - designated-authority (Pattern A): caller must be OwnerPartyId
    /// - cross-entity invariants (Completed requires all WorkOrders Closed/Cancelled
    ///   — invoked via WorkOrderReadModel from blocks-work-orders)
    /// Emits Work.ProjectStatusChanged.
    /// </summary>
    Task<Project> TransitionStatusAsync(
        ProjectId id,
        ProjectStatus to,
        Guid actingPartyId,
        Guid updatedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Cross-entity check helper. Returns the list of blockers preventing
    /// transition to Completed (empty list = OK to complete).
    /// </summary>
    Task<IReadOnlyList<CompletionBlocker>> ValidateCompletionAsync(
        ProjectId id,
        CancellationToken ct = default);

    Task RecomputeRollupsAsync(ProjectId id, CancellationToken ct = default);

    Task<ProjectMilestone> AddMilestoneAsync(/* ... */);

    Task<ProjectBudget> AddBudgetRevisionAsync(
        ProjectId projectId,
        LocalDate effectiveFrom,
        IReadOnlyCollection<ProjectBudgetLineDraft> lines,
        Guid createdBy,
        string? notes,
        CancellationToken ct = default);

    Task<ProjectMilestone> AchieveMilestoneAsync(
        MilestoneId id,
        LocalDate actualDate,
        Guid updatedBy,
        CancellationToken ct = default);
}

public sealed record CompletionBlocker(string Kind, string Detail);
```

**`AchieveMilestoneAsync` workflow (binding — Stage 02 §4.5):**

```text
1. Load milestone; verify status == Pending or AtRisk.
2. milestone.Achieve(actualDate, updatedBy, now).
3. Emit Work.MilestoneAchieved.
4. If milestone.TriggersInvoice && milestone.PaymentAmount != null:
     emit Work.MilestoneInvoiceTriggered.
     (The financial-ar cluster consumes this and drafts an Invoice; this
      hand-off doesn't draft the invoice itself.)
```

#### Event payloads (binding — match `cross-cluster-event-bus-design.md` §3.2)

```csharp
public sealed record ProjectCreatedPayload(
    ProjectId ProjectId,
    string Code,
    string Name,
    ProjectKind Kind,
    Guid? PropertyId,
    Guid? CustomerPartyId,
    Guid OwnerPartyId);

public sealed record ProjectStatusChangedPayload(
    ProjectId ProjectId,
    ProjectStatus FromStatus,
    ProjectStatus ToStatus,
    Guid TransitionedByPartyId,
    Instant TransitionedAt);

public sealed record MilestoneCreatedPayload(
    MilestoneId MilestoneId,
    ProjectId ProjectId,
    string Code,
    MilestoneKind Kind,
    LocalDate PlannedDate,
    decimal? PaymentAmount,
    string? PaymentCurrency,
    bool TriggersInvoice);

public sealed record MilestoneAchievedPayload(
    MilestoneId MilestoneId,
    ProjectId ProjectId,
    LocalDate AchievedDate,
    decimal? Weight);

public sealed record MilestoneInvoiceTriggeredPayload(
    MilestoneId MilestoneId,
    ProjectId ProjectId,
    decimal PaymentAmount,
    string PaymentCurrency,
    Guid CustomerPartyId);
```

**Idempotency keys** (catalog format per T21-59Z ruling):

| Event | Idempotency key | Notes |
|---|---|---|
| `Work.ProjectCreated` | `project-created:{projectId}` | one-shot |
| `Work.ProjectStatusChanged` | `project-status:{projectId}:{occurredAtTicks}` | re-fire safe (project lifecycle can transition multiple times) |
| `Work.MilestoneCreated` | `milestone-created:{milestoneId}` | one-shot |
| `Work.MilestoneAchieved` | `milestone-achieved:{milestoneId}` | one-shot (already in catalog) |
| `Work.MilestoneInvoiceTriggered` | `milestone-invoice:{milestoneId}` | one-shot (already in catalog) |

**Catalog upkeep (bundled with PR 6):** add new rows for
`Work.ProjectCreated`, `Work.ProjectStatusChanged`, and
`Work.MilestoneCreated` to `cross-cluster-event-bus-design.md` §3.2.
`Work.MilestoneAchieved`, `Work.MilestoneInvoiceTriggered`,
`Work.RemodelCapitalized`, and `Work.TimeEntryApproved` are already in
the catalog — leave as-is.

#### `AddBlocksWorkProjects()` (binding)

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.WorkProjects.Events;
using Sunfish.Blocks.WorkProjects.Migration;
using Sunfish.Blocks.WorkProjects.Services;

namespace Sunfish.Blocks.WorkProjects;

public static class WorkProjectsServiceCollectionExtensions
{
    public static IServiceCollection AddBlocksWorkProjects(this IServiceCollection services)
    {
        // Core entities — repositories
        services.AddSingleton<InMemoryProjectRepository>();
        services.AddSingleton<InMemoryProjectMilestoneRepository>();
        services.AddSingleton<IProjectBudgetRepository, InMemoryProjectBudgetRepository>();
        services.AddSingleton<IProjectActualRepository, InMemoryProjectActualRepository>();
        services.AddSingleton<InMemoryRemodelProjectRepository>();

        // Code generator
        services.AddSingleton<IProjectCodeGenerator, InMemoryProjectCodeGenerator>();

        // Services
        services.AddSingleton<IProjectService, InMemoryProjectService>();
        services.AddSingleton<IProjectReadModel, InMemoryProjectReadModel>();
        services.AddSingleton<ITimeEntryService, InMemoryTimeEntryService>();
        services.AddSingleton<ITimeApprovalService, InMemoryTimeApprovalService>();
        services.AddSingleton<IRemodelProjectService, InMemoryRemodelProjectService>();
        services.AddSingleton<IProjectActualProjector, InMemoryProjectActualProjector>();

        // Period gating — local stub IF blocks-financial-periods absent
        services.TryAddSingleton<IPeriodResolver, InMemoryPeriodResolver>();

        // Event publisher — local NOOP IF foundation-events absent
        services.TryAddSingleton<IDomainEventPublisher, NoopDomainEventPublisher>();

        // Party read model — local stub IF blocks-people-foundation absent
        services.TryAddSingleton<IPartyReadModel, InMemoryPartyReadModel>();

        // Importer
        services.AddSingleton<IErpnextProjectImporter, ErpnextProjectImporter>();

        return services;
    }
}
```

**Discipline:** every cross-cluster contract type uses `TryAddSingleton<>`
so that downstream sweeps from `foundation-events`, `blocks-people-
foundation`, and `blocks-financial-periods` cleanly override via plain
`AddSingleton<>` without throwing.

#### `IErpnextProjectImporter` (binding — Pass-1 entry-point)

```csharp
public interface IErpnextProjectImporter
{
    /// <summary>
    /// Upserts a project from an ERPNext source record. Idempotent on
    /// (source, externalRefId).
    /// </summary>
    Task<ImportOutcome<Project>> UpsertFromErpnextAsync(
        ErpnextProjectSource source,
        TenantId tenantId,
        CancellationToken ct = default);
}

public sealed record ErpnextProjectSource(
    string Name,                                  // ERPNext stable id
    string Modified,                              // version key
    string ProjectName,
    string Status,                                // ERPNext status code
    LocalDate? ExpectedStartDate,
    LocalDate? ExpectedEndDate,
    LocalDate? ActualStartDate,
    LocalDate? ActualEndDate,
    string? Customer,                             // ERPNext customer party ref (by name)
    string? CostCenter,                           // mapped to GL dimension
    decimal? EstimatedCosting);
```

**Per-record flow:**

1. Look up existing `Project` by `Code == "ERPNEXT:" + source.Name` (use
   `Project.Tags` containing `"externalRef:erpnext:" + source.Name`
   for v1 since `Project.ExternalRef` isn't part of the entity surface
   yet — see H7 if a richer external-ref shape is needed).
2. If exists and Modified key unchanged → `Skipped`.
3. If exists and key moved forward → update mutable fields → `Updated`.
4. If new → resolve `OwnerPartyId` via `IPartyReadModel` (placeholder
   sentinel if unresolved; per `crdt-friendly-schema-conventions §12`
   orphan tolerance) → `Project.Create(...)` → persist → `Inserted`.

#### Docs page (`apps/docs/blocks-work-projects/overview.md`)

```markdown
# blocks-work-projects

Project management slice of the `blocks-work-*` cluster per ADR 0088 §1.
Ships project lifecycles, milestones, budgets, time tracking, and the
remodel-capitalization workflow.

## Overview

This package provides:

- `Project` — bounded body of work with an 8-state lifecycle.
- `ProjectMilestone` — payment-trigger or schedule-landmark checkpoint.
- `ProjectBudget` + `ProjectBudgetLine` — planned cost by category, with
  revision history.
- `TimeEntry` + `TimeApprovalService` — labor tracking, append-only after
  approval, period-gated submission.
- `ProjectActual` — event-sourced from `Financial.JournalEntryPosted`;
  rebuildable from the event log.
- `RemodelProject` + `RemodelPhase` — capital-improvement specialization
  with capitalization handoff to the financial ledger.

## Naming

`blocks-work-projects` is distinct from `blocks-workflow` (engine) and
`blocks-work-orders` (execution). Namespace: `Sunfish.Blocks.WorkProjects`.

## Quickstart

```csharp
services.AddBlocksWorkProjects();
// ... in a request handler ...
var project = await projectService.CreateAsync(
    tenantId, "Whitney Unit 5B Remodel", ProjectKind.Remodel,
    Priority.High, ownerPartyId, currentUserId);
```

## Algorithms

- **Status state machine:** Stage 02 §2.2.
- **Budget vs actual reconciliation:** Stage 02 §4.2; sourced from
  `Financial.JournalEntryPosted` via `IProjectActualProjector`.
- **Milestone invoice trigger:** Stage 02 §4.5; emits
  `Work.MilestoneInvoiceTriggered` consumed by `blocks-financial-ar`.
- **Time-entry approval:** Stage 02 §4.6; emits `Work.TimeEntryApproved`
  consumed by `blocks-financial-ledger`.
- **Remodel capitalization:** Stage 02 §2.8; emits
  `Work.RemodelCapitalized` consumed by `blocks-financial-ledger`.

## Related

- `blocks-work-orders` (sibling — execution slice; already shipped)
- `blocks-financial-ledger` (consumes `Work.TimeEntryApproved`,
  `Work.RemodelCapitalized`)
- `blocks-financial-ar` (consumes `Work.MilestoneInvoiceTriggered`)
- `blocks-financial-periods` (provides `IPeriodResolver` for time-entry
  period-gating)
- `blocks-people-foundation` (provides `IPartyReadModel`)
- `foundation-events` (provides canonical `IDomainEventPublisher`)
```

#### Tests required — PR 6 (~8 tests)

`tests/InMemoryProjectServiceTests.cs`:
- `CreateAsync_AssignsCodeAndEmitsProjectCreated`
- `TransitionStatusAsync_NonOwner_Throws_NotAuthorityForProject` (Pattern A enforcement)
- `TransitionStatusAsync_ValidByOwner_EmitsProjectStatusChanged`
- `ValidateCompletionAsync_PendingWorkOrders_ReturnsBlockers` (verified against an `IWorkOrderReadModel` test-double)
- `AchieveMilestoneAsync_TriggersInvoice_EmitsMilestoneInvoiceTriggered`

`tests/InMemoryProjectServiceCatalogIdempotencyTests.cs`:
- `EmittedEvents_UseCatalogIdempotencyKeyFormat` (regression for T21-59Z; greps the captured envelopes for non-pipe-delimited keys)

`tests/ErpnextProjectImporterTests.cs`:
- `UpsertFromErpnextAsync_NewSource_InsertsProject`
- `UpsertFromErpnextAsync_SameModified_ReturnsSkipped`

#### Verification (PR 6)

- All previous PR tests pass.
- New PR 6 tests pass (~50–60 total across the package).
- `apps/docs/blocks-work-projects/overview.md` renders; all relative
  links resolve (ADR 0088, Stage 02 schema, sibling cluster docs).
- `dotnet build` succeeds across every consumer.

#### Do NOT in this PR

- Do NOT draft an `Invoice` from `AchieveMilestoneAsync`. The financial
  cluster reactor handles that — this PR emits the trigger event only.
- Do NOT post a JournalEntry from `ApproveAsync` (covered in PR 3 do-not).
- Do NOT introduce SQLite persistence — in-memory only per the sibling
  work-orders pattern. SQLite write-paths land in a future hand-off
  paired with the `foundation-events` substrate.

---

## Sequencing + prerequisites

| PR | Depends on | Ready when |
|---|---|---|
| PR 1 (Project + Milestone + status machine) | sibling `blocks-work-orders` PR 1+ merged (for `WorkOrderId` cross-ref); ADR 0088 Accepted | NOW |
| PR 2 (Budget) | PR 1 merged | After PR 1 |
| PR 3 (TimeEntry + approval + period-gating) | PR 1 merged | After PR 1 |
| PR 4 (ProjectActual projector) | PR 1 merged | After PR 1 |
| PR 5 (RemodelProject + RemodelPhase) | PR 1 merged | After PR 1 |
| PR 6 (Service + DI + importer + docs) | PRs 1–5 merged | After PRs 1–5 |

PRs 2, 3, 4, 5 can be filed concurrently after PR 1 lands (no inter-PR
dependencies). PR 6 sequences last.

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*`
beacon to
`/Users/christopherwood/Projects/SunfishSoftware/coordination/inbox/`.

### H1. `blocks-people-foundation` arrival (PR 3 + PR 6)

If `blocks-people-foundation/Services/IPartyReadModel.cs` arrives on
main mid-flight (e.g., between PR 1 and PR 6), **use the canonical
import** in PR 6 instead of the local stub. The local-stub pattern is
purely a fall-back. If the canonical interface signature drifts from
the local stub (e.g., adds methods this hand-off doesn't reference),
import the canonical and leave the unused methods un-called — don't
re-shape the local API.

If the question is "should I block PR 3 until people-foundation lands?"
the answer is **no** — ship the local stub and migrate in a separate
chore PR.

### H2. `foundation-events` arrival (PR 3 + PR 6)

Same posture as H1. If `foundation-events/IDomainEventPublisher.cs`
arrives mid-flight, the next PR after arrival imports the canonical
interface; previously-shipped PRs in this hand-off keep the local stub
until the sweep PR migrates. **Use `TryAddSingleton<>` for the local
publisher registration** so the sweep can override cleanly.

Per the T21-59Z ruling: the canonical interface signature is the
10-field envelope. If `foundation-events` lands with a different
signature, file `cob-question-*` — that's an envelope-discipline
regression and XO needs to know.

### H3. `blocks-financial-periods` arrival (PR 3)

Same posture as H1/H2. The local `IPeriodResolver` stub (always returns
`Open`) lets PR 3 ship; when `-periods` ships, the sweep PR swaps it.
**Important:** the local stub never returns `SoftClosed`/`Locked`, so
the period-gating rejection branch is exercised only by an explicit
test-double (per the test list above). Do NOT mark this halt-condition
as a build blocker.

### H4. `IReplicaContext` absent (PR 1)

`Project.Code` derives a 2-char replica suffix from
`foundation-localfirst`'s `IReplicaContext` per
`crdt-friendly-schema-conventions §1.8`. If `IReplicaContext` doesn't
exist on main yet, hard-code suffix `"L0"` with a `// TODO: wire
IReplicaContext` comment + file a NOTE (not a question) so XO can
track the gap. The hand-off is not blocked; the suffix is a placeholder.

### H5. `Project.Kind == Remodel` + missing `RemodelProject` sidecar (PR 5)

Stage 02 §2.1 validation says "A project with `kind = 'remodel'` MUST
have a `RemodelProject` sidecar row." This hand-off ships
`RemodelProject` in PR 5 but does NOT enforce the invariant on
`Project.Create` (since the sidecar may not exist yet when the project
is being scaffolded). **Soft enforcement only:** PR 6's
`IProjectService.CreateAsync` for `Kind=Remodel` auto-creates a stub
`RemodelProject` with minimal fields (empty `ScopeStatement`, default
`RemodelKind.Custom`) which the user fleshes out via
`IRemodelProjectService.UpdateAsync` later. If COB feels strongly that
this should be a hard invariant, file `cob-question-*` — XO recommends
soft for v1.

### H6. `ProjectStatus.Closed` audit-correction path (PR 1)

Stage 02 §2.2 marks `Closed` as terminal "except via audit-correction."
The audit-correction transition (`Closed → InProgress` with an admin-
role override) is **NOT in scope** for PR 1. The
`ProjectStatusMachine.CanTransition(Closed, *)` returns `false` for
every target; an admin escape hatch is deferred to a future hand-off
paired with `IActorPrincipalResolver` audit-event integration. Do NOT
attempt to implement audit-correction in this hand-off; file
`cob-question-*` only if a council reviewer pushes back.

### H7. `Project.ExternalRef` shape needed by importer (PR 6)

PR 6's `IErpnextProjectImporter` uses `Project.Tags` to carry the
external-ref signal (`"externalRef:erpnext:" + source.Name`) since the
canonical entity doesn't carry an `ExternalRef` field. If COB wants to
add `ExternalRef` to the canonical `Project` entity instead, **don't** —
that's a Stage 02 schema change. Either:
- (a) Ship as-designed using `Tags` (recommended).
- (b) File `cob-question-*` to request a Stage 02 amendment.

### H8. Capitalization workflow needs the financial reactor (PR 5)

`Work.RemodelCapitalized` fires from PR 5 with the expectation that the
financial-ledger cluster eventually consumes it and posts a capital-
asset JE. **The reactor itself is NOT in scope.** If a council reviewer
asks "where does the capital-asset JE get posted?" the answer is "a
future `blocks-financial-ledger` follow-on hand-off; the event contract
is the bridge." Do NOT scaffold the reactor in this hand-off.

### H9. `MilestoneInvoiceTriggered` → AR invoice gap (PR 6)

Same posture as H8. `Work.MilestoneInvoiceTriggered` fires from PR 6;
`blocks-financial-ar` consumes it. The AR-side reactor MAY exist (per
the `blocks-financial-ar` hand-off) — if it does, the round-trip works
end-to-end. If not, the event fires into the void until the AR side
catches up. Acceptable for this hand-off; document in the PR
description.

### H10. `cross-cluster-event-bus-design.md` catalog rows already cover
some new events (PR 3 / PR 5 / PR 6)

Some of the events this hand-off emits are ALREADY in the §3.2 catalog
(`Work.MilestoneAchieved`, `Work.MilestoneInvoiceTriggered`,
`Work.TimeEntryApproved`, `Work.RemodelCapitalized`). Per
`cross-cluster-event-bus-design.md` §2 "no rename" rule, **use the
catalog names verbatim**. Net-new events to ADD to the catalog (single
bundled docs-only commit per PR that introduces them):

- `Work.TimeEntrySubmitted` (PR 3)
- `Work.RemodelPhaseCompleted` (PR 5)
- `Work.ProjectCreated` (PR 6)
- `Work.ProjectStatusChanged` (PR 6)
- `Work.MilestoneCreated` (PR 6)

If a council reviewer suggests renaming any event to fit a different
naming convention, decline with reference to the §2 no-rename rule.

---

## CRDT-friendly schema conventions applied

Per `_shared/engineering/crdt-friendly-schema-conventions.md`:

### §1 Identifier strategy — ULID everywhere

All ID types (`ProjectId`, `MilestoneId`, `ProjectBudgetId`,
`ProjectBudgetLineId`, `TimeEntryId`, `ProjectActualId`,
`RemodelProjectId`, `RemodelPhaseId`) wrap `Ulid`. No autoincrement.

### §1.8 / §8 Monotonic counters — `Project.Code`

`PRJ-{yyyy}-{replicaSuffix}{seq:0000}` per the canonical convention.
`IProjectCodeGenerator` is the single point of derivation; per-replica
monotonic sequence; display order across replicas is
`(createdAt, replicaId, sequence)` (NOT numeric).

### §2 Tombstones — soft delete only

All entities carry `DeletedAt?`. No hard `DELETE` (except for `Open`-
state `TimeEntry` drafts that have never been submitted — those may be
hard-deleted since they emitted no events).

### §4 Append-only sub-collections

- `TimeEntry` is append-only AFTER `Approved`. Corrections via reverse +
  new entry.
- `ProjectActual` is append-only (event-sourced from `JournalEntryPosted`).
  Tombstoning on upstream reversal is deferred.
- `ProjectBudgetLine` is append-only-per-revision (each
  `InsertRevisionAsync` writes new lines; the prior revision's lines
  remain).
- `RemodelPhase` rows are append-only beyond their status field.

### §5 Stable string codes

All enums (`ProjectStatus`, `ProjectKind`, `MilestoneKind`,
`MilestoneStatus`, `ActivityKind`, `TimeEntryStatus`, `BudgetCategory`,
`ActualSourceKind`, `RemodelKind`, `PhaseStatus`) serialize as
kebab-case lowercase strings. Storage uses TEXT not INTEGER.

### §6 Posted-then-immutable

- `TimeEntry.Status == Approved` → entity is row-level immutable
  (only `MarkInvoiced()` permitted post-Approved).
- `ProjectBudget` revisions are append-only — once a revision is
  written, only `SupersededAt` may mutate on the prior revision.
- `ProjectActual` rows are append-only (no `Update` method).

### §7 State machines under CRDT — Pattern A for `Project.status`

`Project.Status` transitions are Pattern A (designated authority):
enforced at the service layer via `IProjectService.TransitionStatusAsync`'s
check against `Project.OwnerPartyId`. Other state machines
(`MilestoneStatus`, `TimeEntryStatus`, `PhaseStatus`) use Pattern B
(state-machine-aware merge with terminal-wins) — for this hand-off the
Pattern B merge resolver is NOT registered (kernel-crdt wiring is out
of scope); service-layer invariants enforce correctness.

### §10 Validation timing — write-time + post-merge

Write-time validations live in entity factories and service methods.
Post-merge invariants (e.g., `Project.Status == Completed` requires all
child WO statuses) are checked via `IProjectService.ValidateCompletionAsync`
on transition. Background post-merge reconciler is out of scope for
this hand-off; surfaced as `IProjectReconciler` placeholder for the
future kernel-crdt wiring.

### §12 Cross-entity reference integrity — orphan tolerance

`Project.PropertyId` / `AssetId` / `UnitId` / `CustomerPartyId` /
`OwnerPartyId` etc. are loose foreign keys. The `IPartyReadModel` /
`IPropertyReadModel` (when wired) returns `null` for unresolved IDs;
the UI renders "(unresolved party — awaiting sync)" per the convention.

### §13 CRDT envelope

Every non-append-only entity carries `Id`, `TenantId`, `CreatedAt`,
`CreatedBy`, `UpdatedAt`, `UpdatedBy`, `DeletedAt?`, `DeletedBy?`,
`Version`. (`RevisionVector` is Loro-managed; not materialized in this
hand-off's entity layer.)

### §14 Multi-tenant isolation

Every method that returns an entity filters by ambient `TenantId` (via
`ITenantContextAccessor` registered by `foundation-multitenancy`). The
in-memory repositories enforce this at the lookup layer; a
`Project.GetByIdAsync` for the wrong tenant returns `null`, not the
foreign entity.

---

## License posture

### Borrowed-with-attribution (permissive)

- **Apache OFBiz** `workeffort` + `agreement` modules (Apache 2.0) —
  `Project` + `ProjectMilestone` + `ProjectBudget` entity shapes;
  `Work.MilestoneInvoiceTriggered` payment-milestone-as-invoice-anchor
  pattern; `GlAccountTrans` posting model informs `ProjectActual`
  derivation.

### Clean-room study only (copyleft — no code, pattern only)

- **Redmine** (GPLv2) — `TimeEntry.ActivityKind` taxonomy.
- **OpenProject** (GPLv3) — `RemodelProject` + `RemodelPhase` multi-
  phase WBS pattern.
- **ERPNext Projects** (GPLv3) — property-aware project linking
  (`Project.PropertyId` cross-cluster anchor pattern).
- **GanttProject** (GPLv3) — `ProjectMilestone.PredecessorMilestoneId`
  predecessor/successor link pattern.

### Attribution requirements

1. `packages/blocks-work-projects/NOTICE.md` (new in PR 1) carries the
   OFBiz Apache 2.0 §4(c) attribution paragraph (mirror the sibling
   `blocks-work-orders/NOTICE.md` template).
2. Source-header comments on `Project.cs`, `ProjectMilestone.cs`,
   `ProjectBudget.cs`, `ProjectActual.cs`, `TimeEntry.cs`,
   `RemodelProject.cs` carry the one-line FOSS-attribution comment per
   the table in §FOSS attribution above.
3. The package's `.csproj` references `NOTICE.md` via
   `<NOTICEFile>NOTICE.md</NOTICEFile>`.

### Discipline check before merging any PR in this hand-off

1. No copyleft code was opened in any editor session that produced this
   hand-off's PRs.
2. No identifier names from any GPL/AGPL source appear in the new code.
3. The clean-room schema in Stage 02 §2 is the source of truth for type
   shapes; deviations from Stage 02 require XO ratification.

### Sunfish output

**All code authored under this hand-off is MIT-licensed**, per ADR 0088
§2 and the project-wide license posture.

---

## Test plan

### Per-PR minima (summary)

| PR | New tests | Coverage focus |
|---|---|---|
| PR 1 (Project + Milestone + status machine) | ~14 | entity construction; state-machine validity; code generation |
| PR 2 (Budget) | ~9 | revision insert atomicity; overlap rejection; category uniqueness |
| PR 3 (TimeEntry + approval) | ~14 | open/stop/submit/approve; period gating; event emission; 10-field envelope |
| PR 4 (ProjectActual projector) | ~9 | dimension extraction; idempotency; replay |
| PR 5 (RemodelProject + Phase) | ~10 | phase lifecycle; capitalization; event emission |
| PR 6 (Service + DI + importer + docs) | ~8 | Pattern A authority; completion gating; importer idempotency; catalog format regression |
| **Total** | **~64** | |

(Estimate range 50–60 was conservative; ~64 is realistic given the
event-emission + envelope-format regression tests.)

### Cluster-level acceptance (PASS gate at end of PR 6)

**A1.** `dotnet build packages/blocks-work-projects/` succeeds; the
package is consumable from the sibling work-orders test project
(verified by adding a trivial reference + asserting the new ID types
resolve).

**A2.** `dotnet test packages/blocks-work-projects/tests/` passes ~64
tests across all 6 PRs.

**A3.** End-to-end create-project flow exercised in an integration test:

```text
project = projectService.CreateAsync(...);
   → Project row created; Work.ProjectCreated emitted
milestone = projectService.AddMilestoneAsync(project.Id, "M1", Payment, plannedDate, paymentAmount, triggersInvoice=true, customerPartyId=X);
   → Milestone row created; Work.MilestoneCreated emitted
timeEntry = timeEntryService.OpenAsync(tenantId, workerPartyId=Y, ActivityKind.Labor, startedAt=now, projectId=project.Id);
timeEntryService.StopAsync(timeEntry.Id, endedAt=now+2h, hourlyRate=50, "USD", updatedBy=Y);
timeEntryService.SubmitAsync(timeEntry.Id, submittedAt=now+3h, updatedBy=Y);
   → TimeEntry transitions Open → Submitted; Work.TimeEntrySubmitted emitted
timeApprovalService.ApproveAsync(timeEntry.Id, approverPartyId=Z, approvedAt=now+4h);
   → TimeEntry transitions Submitted → Approved; Work.TimeEntryApproved emitted (consumed by financial-ledger)
projectService.AchieveMilestoneAsync(milestone.Id, actualDate=today, updatedBy=Y);
   → Milestone transitions Pending → Achieved; Work.MilestoneAchieved emitted;
     Work.MilestoneInvoiceTriggered emitted (consumed by financial-ar)
projectActualProjector.HandleAsync(synthetic JournalEntryPosted with dimensions.projectId=project.Id, lines with debit=100, USD);
   → ProjectActual row created with PostedAmount=100, Category derived
projectService.ValidateCompletionAsync(project.Id);
   → returns [] (no blockers in this test; sibling WorkOrders absent)
projectService.TransitionStatusAsync(project.Id, ProjectStatus.Completed, actingPartyId=ownerPartyId, updatedBy=Y);
   → Project transitions InProgress → Completed; Work.ProjectStatusChanged emitted with catalog idempotency key project-status:{projectId}:{ticks}
```

All five emitted events surface in `NoopDomainEventPublisher.Captured`
with the catalog idempotency-key format.

**A4.** Remodel capitalization flow:

```text
project = projectService.CreateAsync(..., kind=Remodel, ...);  // auto-creates RemodelProject stub
remodelService.AddPhaseAsync(..., ordinal=1, name="demo", budgetedAmount=1000, USD);
remodelService.AddPhaseAsync(..., ordinal=2, name="rough-in", budgetedAmount=5000, USD);
remodelService.AddPhaseAsync(..., ordinal=3, name="finish", budgetedAmount=4000, USD);
remodelService.MarkPhaseCompleteAsync(phase1.Id, today, actualAmount=950, ...);  // → Work.RemodelPhaseCompleted
remodelService.MarkPhaseCompleteAsync(phase2.Id, today, actualAmount=5200, ...); // → Work.RemodelPhaseCompleted (overBudget by 200)
remodelService.MarkPhaseCompleteAsync(phase3.Id, today, actualAmount=4000, ...); // → Work.RemodelPhaseCompleted
remodelService.CapitalizeAsync(remodelProject.Id, capitalizationAccountId=FA-1510, placedInServiceAt=today, capitalizedAmount=10150, USD, updatedBy=Y);
   → RemodelProject.CapitalizedAt set; Work.RemodelCapitalized emitted with payload (remodelProjectId, projectId, capitalizationAccountId, capitalizedAmount, placedInServiceDate)
```

The `Work.RemodelCapitalized` event surfaces with idempotency key
`remodel-capitalized:{remodelProjectId}` — exact catalog form.

**A5.** ProjectActual projector replay:

```text
projectActualProjector.RebuildFromCursorAsync(fromEventId=null);  // replay from start
   → projector cursor reset; all captured JournalEntryPosted events re-processed; ProjectActual rows are idempotent (no duplicates).
```

**A6.** ERPNext importer round-trip:

```text
source = new ErpnextProjectSource("PROJ-001", "2026-05-10 14:32:00", "Whitney Remodel", "Open", expectedStartDate, expectedEndDate, ...);
result = erpnextProjectImporter.UpsertFromErpnextAsync(source, tenantId);
   → result.Action == Inserted
result2 = erpnextProjectImporter.UpsertFromErpnextAsync(source, tenantId);  // same Modified
   → result2.Action == Skipped
source3 = source with { Modified = "2026-05-12 09:00:00" };
result3 = erpnextProjectImporter.UpsertFromErpnextAsync(source3, tenantId);
   → result3.Action == Updated
```

**A7.** Envelope-discipline regression: `DomainEventEnvelope<T>` has
**exactly 10 producer fields** — no `RecordedAtUtc`, `ProducerCluster`,
`ProducerEntity*`. Verified by a reflection test in
`tests/DomainEventEnvelopeTests.cs`.

**A8.** Catalog idempotency-key discipline: every emitted event's
`IdempotencyKey` matches `^[a-z-]+:[A-Z0-9]{26}(:.*)?$` (kebab-case
prefix + ULID + optional secondary). No pipe-delimited keys.

---

## Cross-cluster contracts (explicit)

### Reads (this cluster consumes)

| Cluster | Surface | Why |
|---|---|---|
| `blocks-people-foundation` | `IPartyReadModel.GetDisplayNameAsync` | Resolve `OwnerPartyId` / `CustomerPartyId` / `SponsorPartyId` / `WorkerPartyId` for UI display |
| `blocks-work-orders` | `IWorkOrderReadModel.GetByProjectAsync` (when available) | `ValidateCompletionAsync` cross-entity check |
| `blocks-financial-periods` | `IPeriodResolver.ResolveAsync` | `TimeEntry.SubmitAsync` period-gating |
| `blocks-financial-ledger` (read only, indirect) | n/a — events only | `ProjectActual` projector consumes `Financial.JournalEntryPosted` envelope |

### Events emitted (this cluster produces)

Per `cross-cluster-event-bus-design.md` §3.2 catalog:

| Event | Idempotency key | Consumers |
|---|---|---|
| `Work.ProjectCreated` (new) | `project-created:{projectId}` | property, reports |
| `Work.ProjectStatusChanged` (new) | `project-status:{projectId}:{occurredAtTicks}` | reports |
| `Work.MilestoneCreated` (new) | `milestone-created:{milestoneId}` | reports |
| `Work.MilestoneAchieved` (catalog) | `milestone-achieved:{milestoneId}` | financial, reports |
| `Work.MilestoneInvoiceTriggered` (catalog) | `milestone-invoice:{milestoneId}` | financial-ar |
| `Work.TimeEntrySubmitted` (new) | `time-submitted:{timeEntryId}` | (none external; internal audit) |
| `Work.TimeEntryApproved` (catalog) | `time-approved:{timeEntryId}` | financial-ledger |
| `Work.RemodelPhaseCompleted` (new) | `remodel-phase-completed:{phaseId}` | reports |
| `Work.RemodelCapitalized` (catalog) | `remodel-capitalized:{remodelProjectId}` | financial-ledger, reports |

Five rows to ADD to the §3.2 catalog (bundled docs-only edit per the PR
that emits the event); four rows are pre-existing.

### Events consumed (this cluster subscribes to)

| Event | Source cluster | Handler | This-cluster effect |
|---|---|---|---|
| `Financial.JournalEntryPosted` | financial-ledger | `JournalEntryPostedHandler` (PR 4) | Upsert `ProjectActual` row when `dimensions.projectId` present |

### Future-cluster contracts (NOT in scope; documented for traceability)

- `blocks-financial-ledger` (future reactor): on `Work.RemodelCapitalized`, post a JE debiting `CapitalizationAccountId` and crediting cost-clearing; AND begin a depreciation schedule per `PlacedInServiceAt`.
- `blocks-financial-ar` (future or existing reactor): on `Work.MilestoneInvoiceTriggered`, draft a new `Invoice` for `(customerPartyId, paymentAmount, dueDate=plannedDate + tenant default term)`.
- `blocks-financial-ledger` (future reactor): on `Work.TimeEntryApproved`, post a JE debiting `GlAccountId` (or default labor account) and crediting accrued-payroll; then a subsequent `Financial.JournalEntryPosted` flows back to PR 4's projector and updates `ProjectActual`.

This bidirectional event flow (`Work.TimeEntryApproved` → ledger JE →
`Financial.JournalEntryPosted` → `ProjectActual`) is the canonical
cross-cluster pattern; each cluster owns its own tables; events are the
only contract.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–6 merged to main** (sequentially per the dependency graph;
   PRs 2/3/4/5 may overlap once PR 1 lands).
2. **`apps/docs/blocks-work-projects/overview.md` published** and
   linked from the cluster index.
3. **`DefaultEvents` catalog updated** in
   `_shared/engineering/cross-cluster-event-bus-design.md` §3.2 with
   the 5 new `Work.*` rows.
4. **`active-workstreams.md`** row for W#60 P4 / `blocks-work-projects`
   updated with `built` status + the 6 PR numbers.
5. **Acceptance tests A1–A8 pass** (covered by the ~64 test suite).
6. **End-to-end cluster acceptance:** the 8-step integration scenario
   in A3 runs through without error; all five expected events surface
   in the captured event log with catalog idempotency-key format.
7. **Cross-cluster handoff verified:** `IProjectActualProjector` consumes
   a synthetic `Financial.JournalEntryPosted` event with
   `dimensions.projectId` populated and upserts a `ProjectActual` row
   per acceptance test A5.

When the PASS gate is met, the next hand-offs in the cluster path can
proceed:

- `blocks-work-contracts-stage06-handoff.md` (Contract + ContractTerm +
  ContractAmendment + ContractRenewal + ContractorAgreement).
- `blocks-work-deliverables-stage06-handoff.md` (Deliverable +
  DeliverableStatus state machine).
- `blocks-financial-ledger-remodel-capitalization-reactor-stage06-handoff.md`
  (consumes `Work.RemodelCapitalized` and posts capital-asset JE).
- A potential `blocks-work-projects-budget-variance-stage06-handoff.md`
  for the `Financial.BudgetVarianceExceeded` emission per Stage 02
  §4.2 (deferred from this hand-off — requires `IProjectReconciler`
  background pass).

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-17):**

- `packages/blocks-work-orders/Models/WorkOrderId.cs` (cross-ref dependency) ✓
- `packages/blocks-work-orders/Models/MaintenanceTaskId.cs` (cross-ref dependency) ✓
- `packages/blocks-work-orders/Services/IPartyReadModel.cs` (sibling local-stub pattern reference) ✓
- ADR 0088 (Path II) ✓
- `icm/02_architecture/blocks-work-schema-design.md` ✓
- `_shared/engineering/crdt-friendly-schema-conventions.md` ✓
- `_shared/engineering/cross-cluster-event-bus-design.md` ✓
- `_shared/engineering/party-model-convention.md` ✓
- `coordination/_archive/xo-ruling-2026-05-16T21-59Z-cob-envelope-and-idempotency-reconciliation.md` ✓
- `coordination/_archive/xo-ruling-2026-05-17T00-18Z-cob-work-orders-complete-and-row-deferral.md` ✓
- Sibling hand-off `icm/_state/handoffs/blocks-work-orders-stage06-handoff.md` ✓
- Template hand-off `icm/_state/handoffs/blocks-financial-ledger-chart-and-journal-stage06-handoff.md` ✓
- Template hand-off `icm/_state/handoffs/blocks-financial-periods-stage06-handoff.md` (for `IDomainEventPublisher` / `NoopDomainEventPublisher` pattern) ✓

**Introduced by this hand-off** (ship across PRs 1–6):

- Package: `packages/blocks-work-projects/`
- ID types: `ProjectId`, `MilestoneId`, `ProjectBudgetId`,
  `ProjectBudgetLineId`, `TimeEntryId`, `ProjectActualId`,
  `RemodelProjectId`, `RemodelPhaseId`
- Enums: `ProjectKind`, `ProjectStatus`, `MilestoneKind`,
  `MilestoneStatus`, `Priority`, `BudgetCategory`, `ActivityKind`,
  `TimeEntryStatus`, `ActualSourceKind`, `RemodelKind`, `PhaseStatus`
- Entities: `Project`, `ProjectMilestone`, `ProjectBudget`,
  `ProjectBudgetLine`, `TimeEntry`, `ProjectActual`, `RemodelProject`,
  `RemodelPhase`
- State machine: `ProjectStatusMachine`
- Repositories: `InMemoryProjectRepository`,
  `InMemoryProjectMilestoneRepository`, `IProjectBudgetRepository` +
  `InMemoryProjectBudgetRepository`, `IProjectActualRepository` +
  `InMemoryProjectActualRepository`, `InMemoryRemodelProjectRepository`
- Services: `IProjectCodeGenerator` + `InMemoryProjectCodeGenerator`,
  `IProjectService` + `InMemoryProjectService`, `IProjectReadModel` +
  `InMemoryProjectReadModel`, `ITimeEntryService` +
  `InMemoryTimeEntryService`, `ITimeApprovalService` +
  `InMemoryTimeApprovalService`, `IRemodelProjectService` +
  `InMemoryRemodelProjectService`, `IProjectActualProjector` +
  `InMemoryProjectActualProjector`
- Local-stub services (if upstream packages absent): `IPeriodResolver` +
  `InMemoryPeriodResolver`, `IPartyReadModel` + `InMemoryPartyReadModel`,
  `IDomainEventPublisher` + `NoopDomainEventPublisher`
- Events: `DomainEventEnvelope<T>` (10-field), `TimeEntrySubmittedEvent`
  + payload, `TimeEntryApprovedEvent` + payload,
  `RemodelPhaseCompletedEvent` + payload, `RemodelCapitalizedEvent` +
  payload, `ProjectCreatedEvent`/`ProjectStatusChangedEvent`/
  `MilestoneCreatedEvent`/`MilestoneAchievedEvent`/
  `MilestoneInvoiceTriggeredEvent` + payloads
- Event handler: `JournalEntryPostedHandler` (consumes
  `Financial.JournalEntryPosted`)
- Importer: `IErpnextProjectImporter` + `ErpnextProjectImporter` +
  `ErpnextProjectSource`
- DI extension: `WorkProjectsServiceCollectionExtensions.AddBlocksWorkProjects()`
- Docs: `apps/docs/blocks-work-projects/overview.md`
- Attribution: `packages/blocks-work-projects/NOTICE.md`
- Catalog edits (docs-only): 5 new rows in
  `_shared/engineering/cross-cluster-event-bus-design.md` §3.2

**Self-audit reminder (per ADR 0028-A10):** COB structurally verifies
each cited symbol by reading the actual file before declaring AP-21
clean. Do not rely on grep-only verification. Especially verify the
sibling `blocks-work-orders` `IPartyReadModel` stub pattern before
mirroring it in PR 6.

---

## Cohort discipline

This hand-off is the **second `blocks-work-*` cluster slice** under ADR
0088 Path II. The COB self-audit pattern applied to the sibling
work-orders hand-off (4 PRs, 77 tests, no halt-conditions hit) applies
here verbatim:

- `AddBlocksWorkProjects()` naming for the DI extension (mirrors
  `AddBlocksWorkOrders()`).
- `apps/docs/{cluster}/overview.md` page convention.
- `README.md` + `NOTICE.md` at the package root referencing Stage 02
  design + ADR 0088.
- `ConcurrentDictionary` for in-memory cache fields where
  multi-threaded access is possible (event publisher's `Captured` list,
  projector cursor state).
- 10-field envelope discipline (per T21-59Z ruling) — no
  `RecordedAtUtc` / `ProducerCluster` / `ProducerEntity*` in the
  producer-side record.
- Catalog idempotency-key format from PR 1 — never pipe-delimited.
- `TryAddSingleton<>` for every cross-cluster contract stub (allows
  clean override by upstream substrate packages).

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w60-p4-work-projects-{slug}.md` in
  `/Users/christopherwood/Projects/SunfishSoftware/coordination/inbox/`.
- Halt the workstream + add a note in the `active-workstreams.md` row
  for W#60.
- `ScheduleWakeup 1800s`.

XO scans the inbox every loop iteration and answers via
`xo-ruling-*.md` directly in the inbox.

If COB completes a PR cleanly without hitting any halt, optionally drop
a `cob-status-2026-05-XXTHH-MMZ-w60-p4-work-projects-pr{N}-merged.md`
beacon. (Optional — `gh pr list` is the canonical status source; the
beacon is courtesy.)

When all 6 PRs are merged + acceptance tests A1–A8 pass, drop a
`cob-status-2026-05-XXTHH-MMZ-w60-p4-work-projects-cluster-complete.md`
beacon analogous to the work-orders cluster-complete beacon. XO will
flip the ledger row + author the next-cluster hand-off.

---

*End of hand-off. Sibling continuation of `blocks-work-orders`; companion
to `blocks-financial-ledger`, `blocks-financial-periods`,
`blocks-people-foundation`, and the canonical engineering conventions
docs. Acceptance gate: 6 PRs, ~64 tests, all 9 `Work.*` events emitting
with the catalog idempotency-key format, end-to-end create-project-
through-capitalize flow green.*
