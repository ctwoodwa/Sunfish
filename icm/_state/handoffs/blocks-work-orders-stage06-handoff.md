# Hand-off — `blocks-work-orders` Work Order + Maintenance Schedule substrate (Phase 1 foundation)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-16
**Status:** `ready-to-build`
**Workstream:** W#60 P4 — Path II native domain, work cluster (work-orders foundation slice)
**Spec source:**
- [`icm/02_architecture/blocks-work-schema-design.md`](../../02_architecture/blocks-work-schema-design.md) §2.1–§2.11, §2.20, §4.1, §4.3, §5.1–§5.3, §6 (FOSS citations)
- [`_shared/engineering/crdt-friendly-schema-conventions.md`](../../../_shared/engineering/crdt-friendly-schema-conventions.md) §1–§7
- [`_shared/engineering/cross-cluster-event-bus-design.md`](../../../_shared/engineering/cross-cluster-event-bus-design.md) §3.2 (`Work.*` catalog)
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) §1 (cluster grouping; `blocks-work-*`), Appendix B Phase 1
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~8–10h sunfish-PM (4 PRs, ~28–32 tests, docs, attribution)
**PR count:** 4 PRs
**Pre-merge council:** NOT required (substrate scope; mirrors the W#60 P4 ledger/people foundation pattern). Standard COB self-audit applies.
**Audit before build:**
```bash
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-work-"
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-workflow/
```
Expected: `blocks-workflow/` exists (state-machine engine, DIFFERENT package — do NOT confuse); nothing matching `blocks-work-orders/`.

---

## Context

### What this hand-off is — the minimal work-order substrate

`blocks-work-orders` ships the **work-order execution + maintenance scheduling** slice of the work cluster. This is the high-frequency day-to-day domain: recording and dispatching repair tickets, scheduling preventive maintenance, tracking contractor assignments, and accepting deficiency hand-offs from the property inspection cluster.

**Deferred to follow-on hand-offs (NOT in scope here):**
- `blocks-work-projects` — `Project` + `ProjectMilestone` + `RemodelProject` + `ProjectBudget` + `ProjectActual` + contract anchor to projects
- `blocks-work-contracts` — `Contract` + `ContractTerm` + `ContractAmendment` + `ContractRenewal` + `ContractorAgreement`
- `blocks-work-deliverables` — `Deliverable` + acceptance/sign-off state machine
- `TimeEntry` — labor time tracking (depends on `blocks-people-*` HR extensions not yet shipped)
- Milestone invoice triggers and remodel capitalization events (depend on `blocks-work-projects`)

### Naming (binding)

Per naming-check output 2026-05-16: `blocks-work-orders` is CLEAN (no collision with `blocks-workflow`). These are distinct packages:

| Package | Role |
|---|---|
| `blocks-workflow` | Infrastructure — state-machine engine; `IWorkflowRuntime`, `WorkflowDefinitionBuilder` |
| `blocks-work-orders` | Domain — work-order entities; `WorkOrder`, `MaintenanceSchedule`, `Contractor` |

Never rename or merge them.

### Inline design decisions (resolves Stage 02 open questions in scope)

**Q10 — WorkOrder.number collision-free under CRDT:**
Use `WO-{yyyyMMdd}-{id.ToHex7()}` where `ToHex7()` returns the first 7 hex chars of the UUIDv7 id. Example: `WO-20260516-a3f2b91`. No replica coordination needed — the id is collision-free by UUIDv7 time-prefix; the number is derived from it. The number is display-only (no FK references use it). Log it on creation; never update.

**Q7 — CRDT state-machine fields (`WorkOrder.status`):**
Last-write-wins at the Loro layer. The application layer enforces invariants on every `status` mutation via `WorkOrderStatusMachine.CanTransition(from, to)` — if the incoming status violates the state machine from the current local status, the service throws `InvalidStatusTransitionException`. For CRDT merge conflicts on status: the `InMemoryWorkOrderRepository` defers to last-write-wins (no custom merge yet); a future reconciler (`IWorkOrderStatusReconciler`) will catch illegal merged-states at sync time and emit a `Work.WorkOrderStatusConflict` event for operator resolution. This is safe for MVP because the primary conflict scenario (two replicas concurrently move WO from `scheduled` to `in-progress`) is not safety-critical.

**Q12 — Eventing transport:**
Use the same `IWorkOrderEventPublisher` + `InMemoryWorkOrderEventPublisher` pattern as sibling hand-offs (`IPartyWriteService` → `IPartyEventPublisher`, `IJournalPostingService` → in-memory publisher). Real cross-cluster transport wires up in a future `kernel-events` integration hand-off. Every event method is idempotent (passes idempotency key per event-bus spec §2).

### FOSS attribution (binding for Stage 06)

Per `blocks-work-schema-design.md` §6 — attribution headers required at implementation time for borrowed permissive sources:
- `WorkOrder` / `WorkOrderLine` / `WorkOrderLine` shapes: `// Inspired by Apache OFBiz WorkEffort module (Apache 2.0) — clean-room expression.`
- `MaintenanceSchedule.recurrenceRule`: `// RRULE syntax per RFC 5545 §3.3.10 (IETF; open standard).`
- `Contractor`: `// Contractor projection pattern inspired by Apache OFBiz Party/PartyRole (Apache 2.0).`

---

## PR 1 — `WorkOrder` + `WorkOrderLine` + `RepairTicket` core entities

**Branch:** `cob/blocks-work-orders-core-entities`
**Commit subject:** `feat(blocks-work-orders): scaffold WorkOrder + WorkOrderLine + RepairTicket + status state machine`
**Estimated effort:** ~2–2.5h

### Files

| File | Role |
|---|---|
| `packages/blocks-work-orders/Sunfish.Blocks.WorkOrders.csproj` | Package project; targets `net9.0`; refs `foundation-*` per ADR 0015 |
| `packages/blocks-work-orders/Models/WorkOrderId.cs` | `readonly record struct WorkOrderId(Guid Value)` — UUIDv7 |
| `packages/blocks-work-orders/Models/WorkOrderLineId.cs` | `readonly record struct WorkOrderLineId(Guid Value)` |
| `packages/blocks-work-orders/Models/RepairTicketId.cs` | `readonly record struct RepairTicketId(Guid Value)` |
| `packages/blocks-work-orders/Models/WorkOrderKind.cs` | `enum WorkOrderKind { Task, Repair, PreventiveMaintenance, Turnover, InspectionFollowup }` |
| `packages/blocks-work-orders/Models/WorkOrderStatus.cs` | `enum WorkOrderStatus { New, Triaged, Estimated, Approved, Scheduled, InProgress, OnHold, Blocked, Completed, Verified, Invoiced, Closed, Cancelled }` |
| `packages/blocks-work-orders/Models/WorkOrderSeverity.cs` | `enum WorkOrderSeverity { Cosmetic, Minor, Major, Safety, Habitability }` |
| `packages/blocks-work-orders/Models/WorkOrderLineKind.cs` | `enum WorkOrderLineKind { Labor, Material, Equipment, Subcontract, Fee, Reimbursable }` |
| `packages/blocks-work-orders/Models/WorkOrderStatusMachine.cs` | `static bool CanTransition(WorkOrderStatus from, WorkOrderStatus to)` — enforces state diagram per schema §2.6 |
| `packages/blocks-work-orders/Models/WorkOrder.cs` | Entity per spec §2.4; `static WorkOrder Create(...)` factory method |
| `packages/blocks-work-orders/Models/WorkOrderLine.cs` | Entity per spec §2.7 |
| `packages/blocks-work-orders/Models/RepairTicket.cs` | Sidecar entity per spec §2.5 |
| `packages/blocks-work-orders/tests/tests.csproj` | Test project; refs `xunit` + `FluentAssertions` |

### WorkOrder entity shape (binding)

```csharp
public sealed class WorkOrder
{
    public WorkOrderId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Number { get; private set; }          // WO-{yyyyMMdd}-{id.ToHex7()}; set on Create(); never mutated
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public WorkOrderKind Kind { get; private set; }
    public WorkOrderStatus Status { get; private set; }
    public Priority Priority { get; private set; }
    public WorkOrderSeverity? Severity { get; private set; }

    // Cross-cluster anchors (loose FK — no EF navigation; read-model resolves names)
    public Guid? ProjectId { get; private set; }        // → blocks-work-projects.Project (future)
    public Guid? PropertyId { get; private set; }       // → blocks-property-*.Property
    public Guid? UnitId { get; private set; }           // → blocks-property-*.Unit
    public Guid? AssetId { get; private set; }          // → blocks-property-*.Asset
    public Guid? DeficiencyId { get; private set; }     // → blocks-property-*.Deficiency

    // Parties
    public Guid? RequestedByPartyId { get; private set; }
    public Guid? AssignedToPartyId { get; private set; }
    public ContractorId? ContractorId { get; private set; }

    // Scheduling
    public DateTimeOffset? ReportedAt { get; private set; }
    public DateTimeOffset? ScheduledStart { get; private set; }
    public DateTimeOffset? ScheduledEnd { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? DueBy { get; private set; }

    // Cost
    public decimal? EstimatedAmount { get; private set; }
    public string? EstimatedCurrency { get; private set; }
    public decimal? ActualAmount { get; private set; }  // computed from WorkOrderLines; not user-editable

    // Maintenance link
    public MaintenanceScheduleId? MaintenanceScheduleId { get; private set; }

    // Billing
    public bool TenantBillable { get; private set; }
    public Guid? RebillPartyId { get; private set; }

    // Audit
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public long Version { get; private set; }

    public static WorkOrder Create(TenantId tenantId, string title, WorkOrderKind kind,
                                   Priority priority, Guid createdBy, ...);
    public void Transition(WorkOrderStatus to, Guid updatedBy);  // throws InvalidStatusTransitionException if CanTransition fails
    public void Assign(Guid? assignedToPartyId, ContractorId? contractorId, Guid updatedBy);
    public void UpdateEstimate(decimal amount, string currency, Guid updatedBy);
    public void SetSeverity(WorkOrderSeverity severity, DateTimeOffset? dueBy, Guid updatedBy);
    public void SoftDelete(Guid deletedBy);

    private static string DeriveNumber(WorkOrderId id) =>
        $"WO-{DateTimeOffset.UtcNow:yyyyMMdd}-{id.Value:N}[..7]";  // first 7 hex chars of Guid
}
```

### WorkOrderStatusMachine transitions (binding)

Implement as a `static readonly Dictionary<WorkOrderStatus, HashSet<WorkOrderStatus>>` allowed-transitions map, matching the state diagram in schema §2.6 exactly:
- New → {Triaged, Cancelled}
- Triaged → {Estimated, Scheduled, Cancelled}
- Estimated → {Approved, Cancelled}
- Approved → {Scheduled}
- Scheduled → {InProgress, OnHold, Cancelled}
- InProgress → {OnHold, Blocked, Completed}
- OnHold → {InProgress, Cancelled}
- Blocked → {InProgress, Cancelled}
- Completed → {Verified, InProgress}
- Verified → {Invoiced, Closed}
- Invoiced → {Closed}
- Closed → {} (terminal)
- Cancelled → {} (terminal)

### Tests required — PR 1 (~8–10 tests)

`tests/WorkOrderTests.cs`:
- `Create_GeneratesNumberInFormat` — verify `Number` matches `WO-YYYYMMDD-xxxxxxx`
- `Create_KindRepair_StatusIsNew`
- `Transition_ValidTransition_UpdatesStatus` (New → Triaged)
- `Transition_InvalidTransition_Throws` (New → Completed directly)
- `Transition_ToTerminalState_CannotExit` (Closed → New throws)
- `SeveritySafety_RequiresDueBy` (if Severity = Safety and DueBy null, Create throws)

`tests/WorkOrderLineTests.cs`:
- `EstimatedAmount_ComputedFromQtyAndPrice`
- `LineNumber_UniquePerWorkOrder` (service-layer test in PR 4)

`tests/WorkOrderStatusMachineTests.cs`:
- `AllTerminalStates_ReturnEmptyTransitions` (Closed, Cancelled)
- `StateMachine_AllowsScheduleSkippingEstimate` (Triaged → Scheduled)

---

## PR 2 — `MaintenanceSchedule` + `MaintenanceTask` + RRULE expansion

**Branch:** `cob/blocks-work-orders-maintenance-schedule`
**Commit subject:** `feat(blocks-work-orders): add MaintenanceSchedule + MaintenanceTask + RRULE expansion stub`
**Estimated effort:** ~2–2.5h

### Files

| File | Role |
|---|---|
| `packages/blocks-work-orders/Models/MaintenanceScheduleId.cs` | `readonly record struct MaintenanceScheduleId(Guid Value)` |
| `packages/blocks-work-orders/Models/MaintenanceTaskId.cs` | `readonly record struct MaintenanceTaskId(Guid Value)` |
| `packages/blocks-work-orders/Models/ScheduleStatus.cs` | `enum ScheduleStatus { Active, Paused, Archived }` |
| `packages/blocks-work-orders/Models/TaskStatus.cs` | `enum TaskStatus { Pending, Completed, NotApplicable, Failed }` |
| `packages/blocks-work-orders/Models/MaintenanceTaskTemplate.cs` | Value object embedded in `MaintenanceSchedule`: `Title`, `Description?`, `Priority`, `Severity?`, `AssignedToPartyId?`, `ContractorId?`, `EstimatedHours?`, `EstimatedAmount?`, `EstimatedCurrency?`, `DefaultLines` (list of `WorkOrderLineDraft`), `ChecklistItems` (list of `ChecklistItem`) |
| `packages/blocks-work-orders/Models/WorkOrderLineDraft.cs` | Value object: `WorkOrderLineKind Kind; string Description; decimal? EstimatedQuantity; decimal? EstimatedUnitPrice; string? UnitOfMeasure` |
| `packages/blocks-work-orders/Models/ChecklistItem.cs` | Value object: `int Ordinal; string Text; bool IsMandatory` |
| `packages/blocks-work-orders/Models/MaintenanceSchedule.cs` | Entity per schema §2.9 |
| `packages/blocks-work-orders/Models/MaintenanceTask.cs` | Entity per schema §2.10 — instances on generated WOs |
| `packages/blocks-work-orders/Services/IRruleExpansionService.cs` | Contract: `IReadOnlyList<DateOnly> ExpandOccurrences(string rrule, DateOnly start, DateOnly? end, int lookaheadDays, string timezone)` |
| `packages/blocks-work-orders/Services/InMemoryRruleExpansionService.cs` | Stub implementation: parses simple `FREQ=DAILY`, `FREQ=WEEKLY`, `FREQ=MONTHLY;INTERVAL=N` cases (use `NodaTime` IANA timezone resolution); complex RRULE deferred to a follow-on hand-off that adds the `Ical.Net` NuGet dependency |

### MaintenanceSchedule entity shape (binding)

```csharp
public sealed class MaintenanceSchedule
{
    public MaintenanceScheduleId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }

    // Scope (at least one recommended)
    public Guid? PropertyId { get; private set; }
    public Guid? UnitId { get; private set; }
    public Guid? AssetId { get; private set; }

    // Recurrence
    public string RecurrenceRule { get; private set; }  // RFC 5545 RRULE string
    public DateOnly StartsOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }
    public string Timezone { get; private set; }        // IANA tz id

    public MaintenanceTaskTemplate TaskTemplate { get; private set; }
    public int GenerateLeadDays { get; private set; }
    public int LookaheadHorizonDays { get; private set; }

    public ScheduleStatus Status { get; private set; }
    public DateTimeOffset? LastGeneratedAt { get; private set; }
    public DateOnly? NextDueAt { get; private set; }

    // Audit fields omitted for brevity; same shape as WorkOrder
}
```

### Tests required — PR 2 (~8 tests)

`tests/MaintenanceScheduleTests.cs`:
- `Create_ValidSchedule_StatusIsActive`
- `Pause_ActiveSchedule_StatusIsPaused`
- `Archive_PausedSchedule_StatusIsArchived`

`tests/InMemoryRruleExpansionServiceTests.cs`:
- `Expand_DailyFreq_ReturnsCorrectCount`
- `Expand_MonthlyInterval3_ReturnsQuarterly`
- `Expand_EndsOn_DoesNotExceedBound`
- `Expand_LeadDaysHonored_SkipsEarlyOccurrences`

`tests/MaintenanceTaskTests.cs`:
- `Create_MandatoryTask_PendingByDefault`

---

## PR 3 — `Contractor` projection + `IContractorReadModel` + local `IPartyReadModel` stub

**Branch:** `cob/blocks-work-orders-contractor`
**Commit subject:** `feat(blocks-work-orders): add Contractor projection + IContractorReadModel + IPartyReadModel stub`
**Estimated effort:** ~1.5–2h

### Context

`Contractor` is a *projection* of `blocks-people-*.Party` in the vendor/contractor role. The contractor-specific fields (insurance, license, trade categories, ratings) live in `blocks-work-orders`, NOT on `Party`. A `ContractorId` is a distinct strongly-typed id (not the same as the `PartyId` of the underlying party).

Until `blocks-people-foundation` ships its canonical `IPartyReadModel`, this PR carries a **local stub** (same pattern as `blocks-financial-ar` §PR3):

```csharp
// LOCAL STUB — relocate import to Sunfish.Blocks.People.Foundation when that package ships.
public interface IPartyReadModel
{
    Task<string?> GetDisplayNameAsync(Guid partyId, CancellationToken ct = default);
}
public sealed class InMemoryPartyReadModel : IPartyReadModel { ... }
```

When `blocks-people-foundation` merges, COB replaces the local stub with a `using` import — no API surface change.

### Files

| File | Role |
|---|---|
| `packages/blocks-work-orders/Models/ContractorId.cs` | `readonly record struct ContractorId(Guid Value)` |
| `packages/blocks-work-orders/Models/TradeCategory.cs` | `enum TradeCategory { General, Plumbing, Electrical, Hvac, Roofing, Landscaping, Cleaning, Pest, Paint, Flooring, Appliance, Other }` |
| `packages/blocks-work-orders/Models/ContractorStatus.cs` | `enum ContractorStatus { Active, Paused, Blacklisted, Archived }` |
| `packages/blocks-work-orders/Models/Contractor.cs` | Entity per schema §2.11; `static Contractor Create(...)` factory |
| `packages/blocks-work-orders/Services/IContractorReadModel.cs` | `GetByIdAsync`, `FindByTradeAsync`, `GetPreferredContractorsAsync` |
| `packages/blocks-work-orders/Services/InMemoryContractorRepository.cs` | Backed by `ConcurrentDictionary<ContractorId, Contractor>` |
| `packages/blocks-work-orders/Services/IPartyReadModel.cs` | **LOCAL STUB** — `GetDisplayNameAsync` only; `// TODO: relocate to blocks-people-foundation` |
| `packages/blocks-work-orders/Services/InMemoryPartyReadModel.cs` | Local stub implementation; always returns `null` for unknown IDs |

### Contractor entity shape (binding)

```csharp
public sealed class Contractor
{
    public ContractorId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid PartyId { get; private set; }           // → blocks-people-*.Party (1:1)
    public string DisplayName { get; private set; }     // denormalized from Party.displayName
    public IReadOnlyList<TradeCategory> Trades { get; private set; }

    // Compliance
    public string? LicenseNumber { get; private set; }
    public DateOnly? LicenseExpiresOn { get; private set; }
    public string? InsurancePolicyNumber { get; private set; }
    public DateOnly? InsuranceExpiresOn { get; private set; }
    public decimal? BondedAmount { get; private set; }
    public string? BondedCurrency { get; private set; }
    public bool W9OnFile { get; private set; }
    public DateOnly? W9ReceivedOn { get; private set; }

    // Operational
    public bool PreferredFlag { get; private set; }
    public decimal? Rating { get; private set; }        // 1..5
    public int RatingCount { get; private set; }
    public decimal? HourlyRate { get; private set; }
    public string? HourlyRateCurrency { get; private set; }
    public bool EmergencyAvailable { get; private set; }

    public ContractorStatus Status { get; private set; }
    public string? Notes { get; private set; }

    // Audit fields same shape as WorkOrder
}
```

**Compliance expiry warning (non-blocking):** `Contractor.IsComplianceExpiringSoon(int warningDays = 30)` returns true if either `LicenseExpiresOn` or `InsuranceExpiresOn` is within `warningDays` days of today. Not a constraint — callers surface as a warning badge.

### Tests required — PR 3 (~6 tests)

`tests/ContractorTests.cs`:
- `Create_ValidContractor_StatusIsActive`
- `Blacklist_ActiveContractor_StatusIsBlacklisted`
- `IsComplianceExpiringSoon_InsuranceIn29Days_ReturnsTrue`
- `IsComplianceExpiringSoon_InsuranceIn31Days_ReturnsFalse`
- `FindByTrade_ReturnsOnlyMatchingTrade`
- `GetPreferredContractors_ReturnsPreferredOnly`

---

## PR 4 — `IWorkOrderService` + event publisher + `DeficiencyRaised` consumer + DI + docs

**Branch:** `cob/blocks-work-orders-service-and-di`
**Commit subject:** `feat(blocks-work-orders): IWorkOrderService + DeficiencyRaised consumer + event publisher + DI extension`
**Estimated effort:** ~2–2.5h

### Files

| File | Role |
|---|---|
| `packages/blocks-work-orders/Services/IWorkOrderService.cs` | Write surface: `CreateAsync`, `AssignAsync`, `TransitionAsync`, `AddLineAsync`, `UpdateEstimateAsync`, `CreateRepairTicketAsync`, `SoftDeleteAsync` |
| `packages/blocks-work-orders/Services/InMemoryWorkOrderService.cs` | In-memory implementation; enforces `WorkOrderStatusMachine.CanTransition`; emits `IWorkOrderEventPublisher` events |
| `packages/blocks-work-orders/Services/IMaintenanceScheduleService.cs` | `CreateAsync`, `PauseAsync`, `ResumeAsync`, `ArchiveAsync`, `GenerateDueWorkOrdersAsync(DateOnly asOf)` |
| `packages/blocks-work-orders/Services/InMemoryMaintenanceScheduleService.cs` | `GenerateDueWorkOrdersAsync` calls `IRruleExpansionService.ExpandOccurrences`; idempotent on `(maintenanceScheduleId, scheduledStart date)` per schema §4.3 pseudocode |
| `packages/blocks-work-orders/Events/IWorkOrderEventPublisher.cs` | `PublishWorkOrderCreatedAsync`, `PublishWorkOrderAssignedAsync`, `PublishWorkOrderCompletedAsync` — signatures match `cross-cluster-event-bus-design.md` §3.2 payloads |
| `packages/blocks-work-orders/Events/InMemoryWorkOrderEventPublisher.cs` | In-memory; accumulates events in `List<object>`; exposed as `DrainEvents()` for test assertions |
| `packages/blocks-work-orders/Events/IDeficiencyRaisedHandler.cs` | Contract: `Task HandleAsync(DeficiencyRaisedEvent evt, CancellationToken ct)` |
| `packages/blocks-work-orders/Events/InMemoryDeficiencyRaisedHandler.cs` | Idempotent implementation per schema §4.1 pseudocode: check `WorkOrder.DeficiencyId == evt.DeficiencyId` first; create WO + optional RepairTicket if not found |
| `packages/blocks-work-orders/Events/DeficiencyRaisedEvent.cs` | Input record: `Guid DeficiencyId, Guid? PropertyId, Guid? UnitId, Guid? AssetId, string Severity, string Description` |
| `packages/blocks-work-orders/WorkOrdersServiceCollectionExtensions.cs` | `AddBlocksWorkOrders()`: registers all I* → InMemory* singletons |
| `apps/docs/blocks-work-orders/overview.md` | Package overview; same minimal format as sibling docs |

### `AddBlocksWorkOrders()` registrations (binding)

```csharp
public static IServiceCollection AddBlocksWorkOrders(this IServiceCollection services)
{
    services.AddSingleton<InMemoryWorkOrderRepository>();
    services.AddSingleton<IWorkOrderService, InMemoryWorkOrderService>();
    services.AddSingleton<InMemoryContractorRepository>();
    services.AddSingleton<IContractorReadModel>(sp => sp.GetRequiredService<InMemoryContractorRepository>());
    services.AddSingleton<IMaintenanceScheduleService, InMemoryMaintenanceScheduleService>();
    services.AddSingleton<IRruleExpansionService, InMemoryRruleExpansionService>();
    services.AddSingleton<IWorkOrderEventPublisher, InMemoryWorkOrderEventPublisher>();
    services.AddSingleton<IDeficiencyRaisedHandler, InMemoryDeficiencyRaisedHandler>();
    services.AddSingleton<IPartyReadModel, InMemoryPartyReadModel>();  // LOCAL STUB
    return services;
}
```

### Tests required — PR 4 (~8–10 tests)

`tests/InMemoryWorkOrderServiceTests.cs`:
- `CreateWorkOrder_ValidInput_ReturnsNew`
- `TransitionWorkOrder_ValidTransition_Success`
- `TransitionWorkOrder_InvalidTransition_Throws`
- `AssignWorkOrder_SetsAssigneeAndContractor`
- `SoftDelete_SetsDeletedAt_CannotTransition`

`tests/InMemoryMaintenanceScheduleServiceTests.cs`:
- `GenerateDueWorkOrders_FirstRun_CreatesWorkOrders`
- `GenerateDueWorkOrders_SecondRun_Idempotent` (no duplicate WOs)

`tests/InMemoryDeficiencyRaisedHandlerTests.cs`:
- `Handle_NewDeficiency_CreatesWorkOrder`
- `Handle_ExistingDeficiency_Idempotent`
- `Handle_SeveritySafety_SetsKindRepairAndDueBy`

---

## Sequencing + prerequisites

| PR | Depends on | Ready when |
|---|---|---|
| PR 1 (entities) | blocks-financial-ledger PR 1 merged (rename done; verifies `blocks-work-orders` is greenfield) | Now — FL PR 1 #892 merged |
| PR 2 (maintenance schedule) | PR 1 merged | After PR 1 |
| PR 3 (contractor) | PR 1 merged; `IPartyReadModel` stub is self-contained | After PR 1 |
| PR 4 (service + DI) | PRs 1–3 merged | After PRs 1–3 |

PRs 2 and 3 can be filed concurrently (no dependency between them) once PR 1 merges.

---

## Halt conditions

**H1 — `blocks-workflow` collision:** If any file in this hand-off would shadow or extend `Sunfish.Blocks.Workflow.*` namespace — STOP, file `cob-question-*`. The namespaces are `Sunfish.Blocks.WorkOrders.*` (this package) vs `Sunfish.Blocks.Workflow.*` (engine package); they must never share a namespace.

**H2 — RRULE complexity beyond stub:** If `InMemoryRruleExpansionService` cannot handle the required recurrence patterns (e.g., `BYDAY=MO,WE,FR` or `BYSETPOS` rules) — implement only the simple cases (DAILY, WEEKLY, MONTHLY;INTERVAL=N) and file `cob-question-*` with the gap. Do NOT add `Ical.Net` NuGet without XO sign-off.

**H3 — Mandatory task blocks WO completion:** Verify that `WorkOrder.Transition(Completed)` checks that no `MaintenanceTask` with `IsMandatory=true` remains in `Pending` status. If the in-memory service cannot enforce this without a cross-entity repository call, add a `ValidateCompletionAsync(WorkOrderId, CancellationToken)` method to `IWorkOrderService` that callers invoke before transitioning.

**H4 — `WorkOrder.ActualAmount` computation:** `ActualAmount` is computed from `WorkOrderLine.ActualAmount` sums; it must NOT be user-editable. If a direct set-path exists via the model, make the setter `private set` (no public mutator).

**H5 — Tenant isolation:** All repository methods must filter by `TenantId`. A `GetByIdAsync` that returns a WO from a different tenant is a security defect. Add a test: `GetById_WrongTenant_ReturnsNull`.

---

## After all 4 PRs merge

1. Flip the `blocks-work-orders` row in `active-workstreams.md` to `built` (via workstream source file + render-ledger.py).
2. File `cob-status-*` with PR numbers.
3. XO will then file the `blocks-work-projects` hand-off (the next cluster slice: Project + Milestone + RemodelProject + budget/actual; requires this hand-off's `WorkOrder` entity to already exist as a cross-entity anchor).
