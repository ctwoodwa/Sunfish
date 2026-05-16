# Path II — Cross-Cluster Event-Bus Vocabulary
**Stage:** 02 Architecture  
**Date:** 2026-05-16  
**Author:** XO (research)  
**Applies to:** All `blocks-*` cluster implementations under ADR 0088 (Anchor all-in-one)

---

## Purpose

This document defines the domain events each cluster emits and consumes, and establishes vocabulary conventions for the cross-cluster event bus. COB must implement event emission for every checked item below before the cluster's Stage 06 hand-off is considered complete.

---

## 1. Event Naming Convention

```
{PascalCaseDomainObject}{PastTensePascalCaseVerb}
```

Examples: `JournalEntryPosted`, `WorkOrderCompleted`, `TenantMagicLinkIssued`

**Rules:**
- All events are past-tense (something already happened)
- No future-tense (`*Requested`, `*Initiated`) except for workflow kickoffs where the initiation is the event (e.g., `SigningWorkflowStarted`)
- Event class naming: `{EventName}Event` — e.g., `JournalEntryPostedEvent`
- Event interface: `ISunfishDomainEvent` (from `foundation`)
- Events are value objects (records); no mutable state

---

## 2. Event Envelope

All cross-cluster events share this envelope:

```csharp
public sealed record SunfishDomainEvent<TPayload> : ISunfishDomainEvent
    where TPayload : notnull
{
    public required Guid EventId { get; init; }           // Unique per emission
    public required string EventType { get; init; }       // E.g., "JournalEntryPosted"
    public required string SourceCluster { get; init; }   // E.g., "blocks-financial-ledger"
    public required string TenantId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required TPayload Payload { get; init; }
    public string? CorrelationId { get; init; }           // Trace across clusters
    public string? CausationEventId { get; init; }        // Parent event (for chains)
}
```

---

## 3. Financial Cluster Events (`blocks-financial-ledger`)

### Emitted

| Event | Trigger | Payload |
|---|---|---|
| `JournalEntryPosted` | Journal entry committed and immutable | `{ JournalEntryId, ChartId, LinesCount, TotalDebit, FiscalPeriodId }` |
| `InvoiceIssued` | Invoice created and sent to customer | `{ InvoiceId, CustomerId, Amount, DueDate, PropertyId? }` |
| `InvoicePaid` | Full payment applied to invoice | `{ InvoiceId, PaymentId, AmountPaid }` |
| `BillReceived` | Bill from vendor entered | `{ BillId, VendorPartyId, Amount, DueDate }` |
| `PaymentApplied` | Payment applied to invoice or bill | `{ PaymentId, TargetId, TargetType, Amount }` |
| `DepreciationScheduled` | Depreciation entry auto-generated | `{ JournalEntryId, AssetId, Amount, AccountId }` |
| `PeriodClosed` | Fiscal period closed and locked | `{ FiscalPeriodId, ChartId, ClosedByPartyId }` |
| `TaxFormLineMapUpdated` | Tax mapping changed | `{ TaxFormKind, TaxYear, Changes }` |

### Consumed

| Event | Source | Action |
|---|---|---|
| `TimeEntryApproved` | `blocks-work` | Create `JournalEntry` for labor cost; post to P&L account |
| `WorkOrderCompleted` | `blocks-work` | Trigger cost allocation review; post actual cost to GL |
| `InvoiceTriggered` | `blocks-work` | Create `Invoice` from milestone trigger |
| `DeliverableAccepted` | `blocks-work` | Release milestone payment; post to AR |

---

## 4. Work Cluster Events (`blocks-work`)

### Emitted

| Event | Trigger | Payload |
|---|---|---|
| `ProjectCreated` | New project opened | `{ ProjectId, OwnerPartyId, PropertyId?, StartDate }` |
| `ProjectStatusChanged` | Status transitions | `{ ProjectId, OldStatus, NewStatus, ChangedByPartyId }` |
| `WorkOrderCreated` | Work unit created (from deficiency or manual) | `{ WorkOrderId, DeficiencyId?, PropertyId, AssetId?, ContractorId? }` |
| `WorkOrderCompleted` | Work unit marked complete | `{ WorkOrderId, CompletedAt, ActualCost, LaborHours }` |
| `WorkOrderVerified` | CO or tenant accepts completed work | `{ WorkOrderId, VerifiedByPartyId, VerifiedAt }` |
| `TimeEntryApproved` | Timelog entry approved by manager | `{ TimeEntryId, Amount, WorkOrderId?, ProjectId, WorkerPartyId }` |
| `MaintenanceTaskCompleted` | Recurring PM task marked done | `{ MaintenanceTaskId, WorkOrderId, CompletedAt }` |
| `ContractRendered` | Contract PDF generated and ready for signing | `{ ContractId, DocumentId }` |
| `DeliverableAccepted` | Deliverable acceptance gate passed | `{ DeliverableId, ProjectId, MilestoneId, AcceptedByPartyId }` |
| `MilestoneAchieved` | Project milestone reached | `{ MilestoneId, ProjectId, AchievedAt, PaymentTriggered }` |
| `InvoiceTriggered` | Milestone-driven invoice request | `{ ProjectId, MilestoneId, Amount, CustomerId }` |

### Consumed

| Event | Source | Action |
|---|---|---|
| `DeficiencyRaised` | `blocks-property` | Create `WorkOrder` from inspection deficiency |
| `AssetRetired` | `blocks-property` | Archive `MaintenanceSchedule` entries for retired asset |
| `JournalEntryPosted` | `blocks-financial` | Update `ProjectActual` budget-vs-actual tracker |

---

## 5. People Cluster Events (`blocks-people`)

### Emitted

| Event | Trigger | Payload |
|---|---|---|
| `EmployeeOnboarded` | Onboarding tasks complete | `{ EmployeeId, PartyId, Role, DepartmentId }` |
| `EmployeeRoleChanged` | Role updated | `{ EmployeeId, OldRole, NewRole, EffectiveDate }` |
| `LeadConverted` | Lead promoted to Customer or Tenant | `{ LeadId, ConvertedToPartyId, ConversionType }` |
| `PartyContactUpdated` | Email verified, phone opted out, address changed | `{ PartyId, ChangeKind }` |
| `TenantAssigned` | Tenant linked to a lease | `{ TenantId, LeaseId, PropertyId, UnitId }` |
| `TenantOffboarded` | Tenant offboarding complete | `{ TenantId, LeaseId, OffboardedAt }` |

### Consumed

| Event | Source | Action |
|---|---|---|
| `PolicyPublished` | `blocks-docs` | Create `PolicyAcknowledgmentRequired` task for each `Employee` |
| `LeaseCreated` | `blocks-property` | Create or link `Tenant` party from lease contact info |

---

## 6. Docs Cluster Events (`blocks-docs`)

### Emitted

| Event | Trigger | Payload |
|---|---|---|
| `DocumentPublished` | Wiki page, policy, or procedure published | `{ DocumentId, VersionId, Sensitivity, DocumentType }` |
| `ContractRendered` | Template instantiated to contract PDF | `{ ContractId, TemplateId, DocumentId }` |
| `SigningWorkflowStarted` | Signature request sent to parties | `{ WorkflowId, DocumentId, Parties[] }` |
| `SigningWorkflowCompleted` | All signatures collected | `{ WorkflowId, FinalSignedDocumentId, AllPartiesSigned }` |
| `PolicyPublished` | Policy published and effective | `{ PolicyId, VersionId, EffectiveFrom }` |
| `DocumentSigned` | Individual signing action | `{ DocumentId, ContractId, SignerPartyId, SignedAt }` |

### Consumed

| Event | Source | Action |
|---|---|---|
| `ContractRendered` | `blocks-work` | Trigger `SigningWorkflow` for contract parties |
| `EmployeeOnboarded` | `blocks-people` | Create document acknowledgment tasks for new hire policies |

---

## 7. Reports Cluster Events (`blocks-reports`)

### Emitted (internal only — reports cluster does not publish cross-cluster)

| Event | Trigger | Internal consumers |
|---|---|---|
| `ReportRunCompleted` | Report generation finished | UI notification layer |
| `KPISnapshotRecorded` | KPI value computed | Dashboard widget refresh |

### Consumed (reads only — no writes cross-cluster)

| Event | Source | Action |
|---|---|---|
| `JournalEntryPosted` | `blocks-financial` | Trigger P&L, Balance Sheet incremental refresh |
| `InvoicePaid` | `blocks-financial` | Trigger AR aging, cash flow refresh |
| `WorkOrderCompleted` | `blocks-work` | Trigger maintenance cost report refresh |
| `PeriodClosed` | `blocks-financial` | Trigger year-end snapshot; Schedule E report run |
| `LeaseCreated` | `blocks-property` | Trigger rent roll refresh |

---

## 8. Property Cluster Events (`blocks-property`, existing)

**Included for cross-cluster reference only — blocks-property is pre-existing.**

| Event emitted | Consumers |
|---|---|
| `DeficiencyRaised` | `blocks-work` (creates WorkOrder) |
| `AssetRetired` | `blocks-work` (archives MaintenanceSchedule) |
| `LeaseCreated` | `blocks-people` (links/creates Tenant), `blocks-reports` (rent roll) |
| `RentPaymentRecorded` | `blocks-financial` (posts to AR; creates JournalEntry) |

---

## 9. Event Bus Implementation

**Transport in Light tier (single-node Anchor):** In-process `MediatR` `INotificationHandler<T>`. No message broker. Event is a MediatR notification; handlers are registered in DI.

**Transport in Standard/Enterprise tier (multi-node):** `kernel-sync` event log exchange over Headscale Tier 2. Events are serialized to the append-only event log; replayed idempotently on peer nodes. Use `EventId` (GUID) for idempotency check.

**Ordering guarantee:** Within a single cluster, events are ordered (sequential in the CP log). Across clusters, events are causally ordered (via `CausationEventId` chain) but not strictly globally ordered. Consumers must handle out-of-order delivery for AP events.

**Idempotency:** Every cross-cluster event handler must be idempotent on `EventId`. Check `EventId` in the `ProcessedEvents` table before applying.

```csharp
// All cross-cluster event handlers follow this pattern:
public sealed class JournalEntryPostedHandler : INotificationHandler<JournalEntryPostedEvent>
{
    public async Task Handle(JournalEntryPostedEvent @event, CancellationToken ct)
    {
        if (await _processedEvents.IsProcessedAsync(@event.EventId, ct)) return;
        // apply domain logic...
        await _processedEvents.MarkProcessedAsync(@event.EventId, ct);
    }
}
```

---

## 10. Dependency Direction (No Circular Events)

```
blocks-property  ──────────────────────────────▶  blocks-work
      │                                                 │
      │                                                 ▼
      ▼                                         blocks-financial
blocks-people  ──────────────────────────────▶  blocks-financial
      ▲                                                 │
      │                                                 │
      └──────────────── blocks-docs ───────────────────┘
                              │
                              ▼
                       blocks-reports  ◀─── (reads all; emits none cross-cluster)
```

**Rules:**
- `blocks-reports` is read-only: consumes events from all clusters, emits none cross-cluster
- `blocks-financial` never emits to `blocks-property` or `blocks-people` (it receives from them)
- `blocks-docs` emits signing events; `blocks-work` and `blocks-people` consume them
- No circular event chains — any circular dependency is a design error; halt + question XO
