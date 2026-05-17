# Cross-Cluster Event-Bus Design

**Status:** Canonical convention. All seven Anchor `blocks-*` clusters
inherit this design. Stage 06 hand-offs reference it by section.
**Date:** 2026-05-16
**Authority:** ADR 0088 — *Anchor as All-In-One Local-First Runtime* §4
**Audience:** XO authoring hand-offs; cob authoring Stage 06; dev / dev-win
reading cluster designs.

---

## 0. Context

The five Stage 02 cluster designs landed 2026-05-16 each surface domain
events that cross cluster boundaries:

- `blocks-work-schema-design.md` §4 + §5 names 12+ cross-cluster events
  (`DeficiencyRaised`, `WorkOrderCreated`, `WorkOrderCompleted`,
  `TimeEntryApproved`, `JournalEntryPosted`, `MilestoneInvoiceTriggered`,
  `ContractRendered`, `DocumentSigned`, `ContractRenewed`,
  `ContractExpired`, `BudgetVarianceExceeded`, `RemodelCapitalized`).
- `blocks-docs-schema-design.md` §5–§7 names workflow events
  (`PolicyReviewRequested`, `PolicyPublished`, `WikiPageDrafted`,
  `BrokenLinksFound`, `ContractFullySigned`, `SigningWorkflowCompleted`,
  `EmployeeOnboardingStarted`, `EmployeeRoleChanged`).
- `blocks-people-schema-design.md` §5–§7 names lifecycle events
  (`TenantApplicationSubmitted`, `TenantApplicationApproved`,
  `TenantActivated`, `TenancyEnded`, `OpportunityWon`, `InvoiceIssued`,
  `EmployeeTerminated`, `WorkOrderAssigned`).
- `blocks-financial-schema-design.md` §6 + §8 names ledger events
  (`InvoiceIssued`, `InvoiceVoided`, `InvoiceWrittenOff`, `BillRecorded`,
  `PaymentApplied`, `PaymentUnapplied`, `JournalEntryPosted`,
  `JournalEntryReversed`, `PeriodSoftClosed`, `PeriodLocked`,
  `YearClosed`).
- `blocks-reports-schema-design.md` §7 + §11 Q3 names emission events
  for tax-mapping audit (`TaxFormLineMapEdited`) and report-run lifecycle
  (`ReportRunCompleted`, `ReportRunFailed`).

`blocks-work-schema-design.md` §7 Q12 escalates the canonical question:

> All §4 workflows assume a domain-event bus. ADR 0088 specifies Loro
> CRDT for sync but not eventing. Stage 03: pick an in-process event bus
> (likely `foundation-events` or equivalent) and document idempotency
> keys per event type.

This document is the cross-cluster ratification: one unified event bus
shape that all five clusters (six counting `blocks-property-*`; seven
counting deferred `blocks-storefront-*`) inherit.

---

## 1. Event envelope shape

Every cross-cluster event carries the same envelope. The envelope is
**versioned** so that Stage 06+ schema evolution doesn't break replay.

```ts
interface DomainEventEnvelope<TPayload> {
  // Identity
  eventId: Id<DomainEvent>;          // ULID (§1 of crdt-friendly-schema-conventions.md)
  eventType: EventTypeName;          // cluster-qualified, e.g. "Financial.JournalEntryPosted"
  schemaVersion: string;             // semver, e.g. "1.0.0"

  // Timing
  occurredAt: Date;                  // wall-clock at event creation

  // Scope
  tenantId: Id<Tenant>;              // tenant scope; cross-tenant events forbidden
  originatingReplicaId: string;      // 2-char replica suffix (§1 of crdt-friendly-schema-conventions.md)

  // Causality
  causationId?: Id<DomainEvent>;     // the event that caused this one (if any)
  correlationId?: string;            // tracks a logical workflow across many events

  // Idempotency
  idempotencyKey: string;            // derived from event semantics; see §4

  // Payload
  payload: TPayload;
}

type ClusterName =
  | 'financial' | 'work' | 'people' | 'docs' | 'reports'
  | 'property' | 'storefront';

type EventTypeName = string;         // pattern: `${ClusterName-titlecase}.${PascalCaseVerbPastTense}`
```

### Fields explained

- **`eventId`** — ULID; sortable; primary key in the event-store table.
- **`eventType`** — cluster-qualified namespace + verb-past-tense (see §3).
- **`schemaVersion`** — semver; consumers check compatibility before
  deserializing the payload. Breaking payload changes increment the
  major version; additive changes increment minor.
- **`occurredAt`** — wall-clock at event creation. For backdated events (a
  Tenant.moveInDate set in the past creates a `TenantActivated` event with
  `occurredAt = moveInDate`), this carries the semantic time. Store-side
  write-time (`recorded_at_utc`) is tracked as a denormalization column in
  the SQLite event-store table (§Storage shape) — NOT a producer-envelope
  field. Producers don't set it.
- **`tenantId`** — never null; cross-tenant events are forbidden (§14 of
  crdt-friendly-schema-conventions.md).
- **`originatingReplicaId`** — provenance for audit and for cross-replica
  de-duplication. Same 2-char suffix as the monotonic-number scheme.
- **`causationId`** — when a handler emits a downstream event, this links
  to the upstream cause. Enables debugging "why did this event fire?"
- **`correlationId`** — a logical workflow ID. A lease execution might
  span `LeaseExecuted` → `TenantActivated` → `RentInvoiceScheduled` →
  `PartyRoleOpened`; all four share a `correlationId`.
- **`idempotencyKey`** — per §4, every event has a deterministic key
  derived from its semantic identity. Used to deduplicate replays and
  cross-replica re-emission.
- **`payload`** — event-specific; defined in §3 per event type.

**Store-side denormalization columns (NOT producer-envelope fields):** the
SQLite `domain_events` table adds `recorded_at_utc` (write-time), and may
add `producer_cluster` / `producer_entity_kind` / `producer_entity_id`
columns derived at insertion-time from `eventType` parsing + payload
inspection — for query performance + audit / debugging. Producers never
set these; the store layer computes them on append. See §Storage shape
below for the schema.

### Storage shape

Events are stored in a per-tenant `domain_events` SQLite table:

```sql
CREATE TABLE domain_events (
  event_id TEXT PRIMARY KEY,            -- ULID
  event_type TEXT NOT NULL,
  schema_version TEXT NOT NULL,
  occurred_at TEXT NOT NULL,            -- ISO-8601
  recorded_at_utc TEXT NOT NULL,
  tenant_id TEXT NOT NULL,
  originating_replica_id TEXT NOT NULL,
  causation_id TEXT,
  correlation_id TEXT,
  producer_cluster TEXT NOT NULL,
  producer_entity_kind TEXT,
  producer_entity_id TEXT,
  idempotency_key TEXT NOT NULL,
  payload_json TEXT NOT NULL,           -- serialized payload
  -- envelope CRDT fields (append-only; §4 of crdt-friendly-schema-conventions.md)
  created_at TEXT NOT NULL,
  created_by TEXT NOT NULL,
  deleted_at TEXT
);

CREATE UNIQUE INDEX idx_domain_events_idempotency
  ON domain_events(tenant_id, idempotency_key);

CREATE INDEX idx_domain_events_type_recorded
  ON domain_events(tenant_id, event_type, recorded_at_utc);

CREATE INDEX idx_domain_events_correlation
  ON domain_events(tenant_id, correlation_id)
  WHERE correlation_id IS NOT NULL;
```

The `domain_events` table is **append-only** per §4 of the CRDT-friendly
schema conventions: existing rows never `UPDATE` (only the
`deletedAt` column for crypto-shred). New events are inserts.

---

## 2. Naming convention

### Form

```
<ClusterName-titlecase>.<PascalCaseVerbPastTense>
```

Examples:

- `Financial.JournalEntryPosted`
- `Financial.PaymentApplied`
- `Work.WorkOrderCompleted`
- `Work.ContractRenewed`
- `People.TenantActivated`
- `Docs.PolicyPublished`
- `Docs.SigningWorkflowCompleted`
- `Property.DeficiencyRaised`
- `Reports.ReportRunCompleted`

### Rules

1. **Cluster prefix is the producer.** `Financial.InvoiceIssued` is
   emitted by `blocks-financial-*`. Consumers in other clusters subscribe
   by name; they don't relabel.
2. **Verb past tense** — events describe what *happened*, never what
   *should happen*. `WorkOrderCompleted` (good) vs `CompleteWorkOrder`
   (this is a command, not an event).
3. **No noun-only names.** `Financial.Invoice` is not an event;
   `Financial.InvoiceIssued` / `Financial.InvoiceVoided` are.
4. **PascalCase, no hyphens, no underscores.** Matches the C# event
   class name; survives JSON serialization without quoting issues.
5. **Stable string codes for any enumerated field inside the payload**
   (§5 of crdt-friendly-schema-conventions.md). Event-type names
   themselves are also stable strings — once introduced, never renamed.

### Deprecation

Same discipline as enum codes (§5 of crdt-friendly-schema-conventions.md):

- **Do not rename** existing event types. Add new + deprecate old.
- A consumer migrating to a new event type subscribes to both during a
  transition window; producer drops the old type only when all consumers
  have migrated.
- The `schemaVersion` field handles forward-compatible payload changes
  *within* an event type. Breaking changes require a new event type
  with a new name.

---

## 3. Catalog of events identified across the five clusters

Compiled from the five Stage 02 design docs. Columns:

- **Event** — fully-qualified name.
- **Producer** — emitting cluster.
- **Consumers** — clusters that subscribe.
- **Payload sketch** — minimal payload shape.
- **Idempotency key** — deterministic derivation.

### 3.1 `Financial.*` events

| Event | Consumers | Payload sketch | Idempotency key |
|---|---|---|---|
| `Financial.JournalEntryPosted` | work, reports | `{ entryId, chartId, periodId, lines: [{ accountId, debit, credit, propertyId?, classId?, taxCodeId? }], dimensions?: { projectId?, workOrderId? } }` | `je-posted:{entryId}` |
| `Financial.JournalEntryReversed` | work, reports | `{ originalEntryId, reversalEntryId }` | `je-reversed:{originalEntryId}` |
| `Financial.InvoiceIssued` | people, reports, docs | `{ invoiceId, customerId, totalAmount, dueDate, propertyId? }` | `invoice-issued:{invoiceId}` |
| `Financial.InvoiceVoided` | people, reports | `{ invoiceId, reversalEntryId }` | `invoice-voided:{invoiceId}` |
| `Financial.InvoiceWrittenOff` | people, reports | `{ invoiceId, badDebtJEId }` | `invoice-writeoff:{invoiceId}` |
| `Financial.BillRecorded` | work, reports | `{ billId, vendorId, totalAmount, dueDate, projectId? }` | `bill-recorded:{billId}` |
| `Financial.PaymentApplied` | people, reports | `{ paymentId, applicationId, targetKind: 'invoice'\|'bill', targetId, amount }` | `payment-applied:{applicationId}` |
| `Financial.PaymentUnapplied` | people, reports | `{ paymentId, applicationId, reversalEntryId }` | `payment-unapplied:{applicationId}` |
| `Financial.PeriodSoftClosed` | reports, work | `{ periodId, chartId, closedByPrincipalId, occurredAt }` | `period-soft-closed:{periodId}:{occurredAtTicks}` (re-fire safe — periods CAN be reopened then soft-closed again) |
| `Financial.PeriodLocked` | reports, work | `{ periodId, chartId }` | `period-locked:{periodId}` (one-shot — periods cannot be unlocked) |
| `Financial.PeriodReopened` | reports, work | `{ periodId, chartId, reopenedByPrincipalId, occurredAt }` | `period-reopened:{periodId}:{occurredAtTicks}` (re-fire safe) |
| `Financial.YearClosed` | reports | `{ fyId, chartId, closingEntryId }` | `year-closed:{fyId}` |
| `Financial.YearEndRolloverCompleted` | reports, work | `{ fyId, chartId, closingEntryId, netIncome, incomeAccountsClosed, expenseAccountsClosed }` | `year-end-rollover:{fyId}` |
| `Financial.BudgetVarianceExceeded` | work, reports | `{ budgetId, projectId?, category, variance, variancePercent }` | `budget-variance:{budgetId}:{category}:{periodId}` |

### 3.2 `Work.*` events

| Event | Consumers | Payload sketch | Idempotency key |
|---|---|---|---|
| `Work.ProjectCreated` | property, reports, financial | `{ projectId, code, name, kind, propertyId?, customerPartyId?, ownerPartyId }` | `project-created:{projectId}` |
| `Work.ProjectStatusChanged` | property, reports, financial | `{ projectId, fromStatus, toStatus, transitionedByPartyId, transitionedAt }` | `project-status:{projectId}:{occurredAtTicks}` |
| `Work.MilestoneCreated` | property, reports, financial | `{ milestoneId, projectId, code, kind, plannedDate, paymentAmount?, paymentCurrency?, triggersInvoice }` | `milestone-created:{milestoneId}` |
| `Work.WorkOrderCreated` | property, reports, financial | `{ workOrderId, kind, propertyId?, unitId?, assetId?, deficiencyId?, projectId? }` | `wo-created:{workOrderId}` |
| `Work.WorkOrderAssigned` | people, property | `{ workOrderId, assignedToPartyId, contractorId? }` | `wo-assigned:{workOrderId}:{assignedToPartyId}` |
| `Work.WorkOrderCompleted` | property, reports, financial | `{ workOrderId, propertyId?, unitId?, assetId?, completedAt, actualAmount? }` | `wo-completed:{workOrderId}` |
| `Work.TimeEntryApproved` | financial | `{ timeEntryId, workOrderId?, projectId?, employeeId, hours, billableAmount, glAccountId? }` | `time-approved:{timeEntryId}` |
| `Work.MilestoneAchieved` | financial, reports | `{ milestoneId, projectId, achievedAt, weight? }` | `milestone-achieved:{milestoneId}` |
| `Work.MilestoneInvoiceTriggered` | financial | `{ milestoneId, projectId, paymentAmount, customerPartyId }` | `milestone-invoice:{milestoneId}` |
| `Work.ContractRendered` | docs, financial | `{ contractId, templateId, documentId, counterpartyPartyId }` | `contract-rendered:{contractId}` |
| `Work.ContractRenewed` | financial, reports | `{ contractId, renewalId, newExpirationDate }` | `contract-renewed:{renewalId}` |
| `Work.ContractExpired` | financial, reports | `{ contractId, expiredAt }` | `contract-expired:{contractId}` |
| `Work.RemodelPhaseCompleted` | financial, reports | `{ phaseId, remodelProjectId, projectId, ordinal, name, actualAmount?, currency?, actualEndDate }` | `remodel-phase-completed:{phaseId}` |
| `Work.RemodelCapitalized` | financial, reports | `{ remodelProjectId, propertyId, capitalizedAmount, placedInServiceDate }` | `remodel-capitalized:{remodelProjectId}` |
| `Work.DeliverableSubmitted` | docs, reports | `{ deliverableId, projectId, milestoneId?, approverPartyId }` | `deliverable-submitted:{deliverableId}:{submissionNumber}` |
| `Work.DeliverableAccepted` | financial, docs, reports | `{ deliverableId, projectId, milestoneId?, acceptedAt }` | `deliverable-accepted:{deliverableId}` |

### 3.3 `People.*` events

| Event | Consumers | Payload sketch | Idempotency key |
|---|---|---|---|
| `People.TenantApplicationSubmitted` | property, docs | `{ tenantId, leadId, applicationData }` | `tenant-app-submitted:{tenantId}` |
| `People.TenantApplicationApproved` | property | `{ tenantId, employeeId, decisionNotes }` | `tenant-app-approved:{tenantId}` |
| `People.TenantActivated` | property, financial | `{ tenantId, leaseId, moveInAt }` | `tenant-activated:{tenantId}:{leaseId}` |
| `People.TenancyEnded` | property, financial, docs | `{ tenantId, leaseId, reason, moveOutAt }` | `tenancy-ended:{tenantId}:{leaseId}` |
| `People.OpportunityWon` | financial | `{ opportunityId, customerId?, leadId?, estimatedValue }` | `opp-won:{opportunityId}` |
| `People.EmployeeTerminated` | work, docs, financial | `{ employeeId, terminationDate, reason }` | `emp-terminated:{employeeId}` |
| `People.EmployeeOnboardingStarted` | docs | `{ employeeId, role, departmentId }` | `emp-onboarding-started:{employeeId}:{role}` |
| `People.EmployeeRoleChanged` | docs, work | `{ employeeId, fromRole, toRole, effectiveDate }` | `emp-role-changed:{employeeId}:{effectiveDate}` |
| `People.CompensationChanged` | financial | `{ employeeId, fromAmount, toAmount, effectiveDate }` | `compensation-changed:{employeeId}:{effectiveDate}` |
| `People.ActivityLogged` | (none external — internal append) | `{ activityId, partyId, employeeId, kind, direction }` | `activity-logged:{activityId}` |

### 3.4 `Docs.*` events

| Event | Consumers | Payload sketch | Idempotency key |
|---|---|---|---|
| `Docs.PolicyReviewRequested` | people | `{ policyId, draftVersionId, approverPartyIds }` | `policy-review-requested:{draftVersionId}` |
| `Docs.PolicyPublished` | people | `{ policyId, versionId, effectiveFrom, appliesToRoles }` | `policy-published:{policyId}:{versionId}` |
| `Docs.PolicyAcknowledgmentRecorded` | people | `{ policyId, versionId, employeeId, acknowledgedAt }` | `policy-ack:{policyId}:{versionId}:{employeeId}` |
| `Docs.WikiPageDrafted` | (none external — internal) | `{ pageId, draftId, authorPartyId }` | `wiki-drafted:{draftId}` |
| `Docs.BrokenLinksFound` | (none external — internal alert) | `{ pageId, brokenLinks: string[] }` | `broken-links:{pageId}:{linksDigest}` |
| `Docs.ContractFullySigned` | work, financial | `{ contractInstanceId, finalSignedDocumentId, fullySignedAt }` | `contract-fully-signed:{contractInstanceId}` |
| `Docs.DocumentSigned` | work, financial | `{ documentId, contractId?, signerPartyId, signedAt }` | `document-signed:{documentId}:{signerPartyId}` |
| `Docs.SigningWorkflowCompleted` | work | `{ workflowId, finalDocumentId }` | `signing-completed:{workflowId}` |

### 3.5 `Reports.*` events

| Event | Consumers | Payload sketch | Idempotency key |
|---|---|---|---|
| `Reports.ReportRunCompleted` | (none external — internal) | `{ runId, reportId, artifactId, durationMs }` | `report-run-completed:{runId}` |
| `Reports.ReportRunFailed` | (none external — internal alert) | `{ runId, reportId, errorKind, errorMessage }` | `report-run-failed:{runId}` |
| `Reports.TaxFormLineMapEdited` | (none external — audit) | `{ mapId, fromSelector, toSelector, editedByPartyId }` | `tax-map-edited:{mapId}:{editedAt}` |
| `Reports.KPISnapshotTaken` | (none external — internal) | `{ kpiId, snapshotId, value, recordedAt }` | `kpi-snapshot:{kpiId}:{recordedAt}` |

### 3.6 `Property.*` events (for completeness; Phase 1 cluster)

| Event | Consumers | Payload sketch | Idempotency key |
|---|---|---|---|
| `Property.DeficiencyRaised` | work | `{ deficiencyId, propertyId, unitId?, assetId?, severity }` | `deficiency-raised:{deficiencyId}` |
| `Property.DeficiencyResolved` | work, reports | `{ deficiencyId, resolvedWorkOrderId }` | `deficiency-resolved:{deficiencyId}` |
| `Property.AssetRetired` | work, financial | `{ assetId, retiredAt, retiredReason }` | `asset-retired:{assetId}` |
| `Property.LeaseExecuted` | people, financial | `{ leaseId, tenantPartyIds, propertyId, executedAt, startDate, endDate, monthlyRent }` | `lease-executed:{leaseId}` |
| `Property.LeaseTerminated` | people, financial | `{ leaseId, terminatedAt, reason }` | `lease-terminated:{leaseId}` |

### Catalog upkeep

Every event added in a Stage 06 implementation MUST be back-filled into
this table. A future ADR (or a `_shared/engineering/event-catalog.md`
companion file) will be the canonical machine-readable version once the
catalog stabilizes; this document remains the human-readable design
rationale.

---

## 4. Delivery semantics

### At-least-once, idempotent

The event bus guarantees **at-least-once** delivery. Consumers MUST be
**idempotent**: receiving the same event twice produces the same effect
as receiving it once.

This combination is well-known: exactly-once is not implementable across
a network without unbounded coordination; idempotent at-least-once is
the standard fallback.

### Idempotency key

Every event has a deterministic `idempotencyKey` derived from the event
semantics (column in §3 catalog). The derivation rules:

1. **Includes the unique identifier of the entity the event is about**
   (e.g., `je-posted:{entryId}` for `Financial.JournalEntryPosted`).
2. **Includes any sub-event qualifier** when the same entity emits
   multiple events of the same type (e.g.,
   `wo-assigned:{workOrderId}:{assignedToPartyId}` — reassignment
   produces a new event with a different key).
3. **Does NOT include timestamps** — same event, replayed later, has
   the same key. (Exception: time-of-record events like
   `Reports.KPISnapshotTaken` whose semantic identity *is* the
   snapshot timestamp.)
4. **Is stable** — the key for an event is the same regardless of which
   replica emitted it (e.g., if a manual JE-post happens on replica A
   and the same JE is re-applied during sync on replica B, both
   emissions share `je-posted:{entryId}` and the consumer dedupes).

### Cross-replica deduplication

The `domain_events` table has a unique index on `(tenant_id,
idempotency_key)`. When a Loro sync brings in events from another
replica, the kernel-sync layer attempts the insert; conflicts on the
unique index are silently dropped (the event has already been recorded
on this replica via a different path).

**Sneaky edge case.** If two replicas independently emit the *same*
logical event (e.g., both observe the same Lease activation in disjoint
ways), the idempotency key will collide and only one event survives.
This is correct: there's only one logical event in the world. The
producer-replica identity is captured in the surviving row's
`originatingReplicaId`; the other replica's emission is dropped, and
its handlers see the consequence (the surviving event) via sync.

### Loro op-log integration

Events are written into the `domain_events` SQLite table. The Loro CRDT
treats `domain_events` as an append-only `List` container (per §4 of
crdt-friendly-schema-conventions.md). Each new event becomes a Loro
list-insert op; replication carries the op to peers, which insert into
their local `domain_events` table on receipt.

The flow:

1. Producer cluster writes event to local `domain_events` table.
2. Local handlers run (in-process, same SQLite transaction or shortly
   after).
3. Loro records the insert as a list-op in its op-log.
4. kernel-sync replicates the op to peer Anchor instances.
5. On peer receipt, the insert hits the local `domain_events` table.
   Unique-index dedup drops duplicates.
6. Peer-local handlers see the new row and react.

---

## 5. Subscription model

### Consumer registration

Each cluster registers handlers at bootstrap via DI:

```ts
// Hypothetical Stage 06 shape; concrete TS / C# implementations TBD.
container.registerEventHandler<Financial.JournalEntryPosted>(
  eventType: 'Financial.JournalEntryPosted',
  handler: async (event) => {
    if (event.payload.dimensions?.projectId) {
      await workActuals.upsertFromJournalLine(event);
    }
  }
);
```

Per `blocks-work-schema-design.md` §4.6:

> blocks-work-* listens to the resulting JournalEntryPosted event and
> upserts a ProjectActual row.

### Per-replica position cursors

Each replica tracks **which events it has handled** via a per-handler
cursor:

```sql
CREATE TABLE event_handler_cursors (
  handler_id TEXT NOT NULL,         -- e.g. "blocks-work.ProjectActualsUpserter"
  tenant_id TEXT NOT NULL,
  last_handled_event_id TEXT,       -- highest event_id processed
  last_handled_at TEXT NOT NULL,
  PRIMARY KEY (handler_id, tenant_id)
);
```

When new events arrive (via local write OR via sync), the dispatcher
walks `domain_events` from each handler's cursor forward, invoking the
handler on each event, and advancing the cursor on success.

### Disconnected-replica behavior

When a replica reconnects after extended offline time:

1. Loro sync brings in the missed events into `domain_events`.
2. Each handler's cursor sees a gap and walks the new events in order.
3. Idempotency guarantees the handler's effects are reapplied only when
   they actually change state.

### Persistent subscriptions

Subscriptions survive Anchor restart because the cursors are persisted
in SQLite. A subscription is "lost" only if its handler is unregistered
from the cluster bootstrap — at which point the cursor becomes orphaned
and may be GC'd via a separate maintenance task.

### Cross-replica subscription consistency

Each replica handles **all** events independently. Two replicas with
the same handler registered will each react to each event on their own
side. The handler's idempotency (combined with each replica's own
SQLite store) ensures convergent state.

**What this rules out.** A "lead replica" model where only one replica
handles each event and others trust the result. We considered this
during design and rejected it: a lead replica going offline halts all
event processing for the tenant, which contradicts the offline-first
promise.

---

## 6. Backpressure + retry

### Handler failure

A handler that throws / fails: the dispatcher:

1. Records the failure in `event_handler_failures`:
   ```sql
   CREATE TABLE event_handler_failures (
     id TEXT PRIMARY KEY,                -- ULID
     handler_id TEXT NOT NULL,
     event_id TEXT NOT NULL,
     tenant_id TEXT NOT NULL,
     attempt_number INTEGER NOT NULL,
     failed_at TEXT NOT NULL,
     error_message TEXT NOT NULL,
     next_retry_at TEXT,                 -- null = retry exhausted
     resolved_at TEXT
   );
   ```
2. Does NOT advance the handler's cursor.
3. Schedules a retry with exponential backoff: 30s, 2m, 10m, 1h, 6h,
   24h, 72h. After 7 attempts (~ 4 days), the failure is marked
   "retry-exhausted" and surfaces in the admin "Sync health" view for
   operator review.

The cursor stays put, so the handler will revisit the event on the next
dispatch loop. Other handlers move forward independently — one handler
blocking another is forbidden.

### Slow consumers

Handlers are expected to be fast. If a handler's per-event cost makes
the dispatcher fall behind, the dispatcher:

1. Logs the lag.
2. Continues invoking the handler (no abort).
3. Surfaces lag > 1 minute in the admin view.

There is no explicit backpressure mechanism: the producer doesn't slow
down for a slow consumer because the producer can't observe consumer
state. Slow handlers are a Stage 06 quality bug, fixed by handler
optimization or by deferring slow work to a separate background task.

### Offline / disconnected

A replica that's offline cannot deliver events to peers. Events written
locally accumulate in `domain_events`. On reconnect, Loro sync drains
the backlog peer-to-peer. There's no centralized broker, so no
broker-side queue overflow concern.

If the offline period is so long that the local `domain_events` table
grows beyond practical sync size, kernel-sync chunks the catch-up sync
into manageable batches; each batch is internally consistent.

---

## 7. Event-sourcing posture

### What is sourced from events

`blocks-reports-*` is the primary event-sourced consumer: per
`blocks-reports-schema-design.md` §6 + §9, every report runs against a
snapshot of the producing-cluster state at a point in time, with the
event stream available as a complementary audit trail.

`blocks-work-*` event-sources `ProjectActual` from `JournalEntryPosted`
events (per `blocks-work-schema-design.md` §4.6 and §7 Q2 —
"`ProjectActual` storage — materialized table or event-sourced read
model? §2.22 implies a row-per-actual table. An alternative is to
derive actuals entirely from `blocks-financial-*` journal queries at
read time").

### What stays in SQLite as primary

The cluster designs all keep their **primary entity data** in SQLite
tables (per ADR 0088 §1: "SQLite is the primary store"). Events are
**not** the primary store; they are the cross-cluster bridge.

This is the canonical posture:

- **SQLite tables** = primary source of truth for each cluster.
- **Loro op-log** = CRDT sync overlay for SQLite rows.
- **`domain_events` table** = cross-cluster bridge + audit trail. Also
  CRDT-replicated as a Loro `List`.
- **Event-sourced read models** = derived per consumer; rebuildable
  from `domain_events` if needed.

### Why this posture

Pure event-sourcing (no canonical primary tables; rebuild state from
events on every read) imposes too much rebuild cost on Anchor's light-
hardware target (Surface Pro 7 class). Hybrid posture gives event-
sourcing's audit and cross-cluster benefits without the rebuild cost.

The trade-off: drift between events and primary tables is possible if a
producer writes the table but fails to emit the event (or vice versa).
Mitigation: the producer cluster wraps its write + event-emit in the
same SQLite transaction so they atomically succeed or atomically fail.

### Event replay

To rebuild a consumer's derived state from scratch:

1. Reset the consumer's handler cursor to the beginning of time.
2. Reset the consumer's derived tables.
3. Restart the dispatch loop; handlers replay from event 1.

This is offline-safe and deterministic given idempotent handlers. It's
also the recovery path when a consumer-derived schema changes (e.g.,
adding a column to `ProjectActual` — replay from events to repopulate).

---

## 8. Versioning and schema evolution

### Within an event type

`schemaVersion` is semver. Producers increment minor for additive
changes (new optional field) and major for breaking changes (rename,
type change, mandatory new field).

Consumers check `schemaVersion` before deserializing. Receiving an
event with an unsupported major version logs a warning and skips the
event without advancing the cursor (allowing a later consumer-side
upgrade to backfill).

### Across event types

Renames are forbidden (§2). To replace `Financial.InvoiceIssued` with
`Financial.InvoiceMaterialized` (hypothetical), the producer:

1. Continues emitting `Financial.InvoiceIssued` for a transition window.
2. Adds emission of `Financial.InvoiceMaterialized` for new events.
3. Waits for all consumers to migrate.
4. Stops emitting `Financial.InvoiceIssued` once verified.

The old event type stays in the catalog forever (with `Deprecated:
true`), since old events with that type live in `domain_events`
indefinitely.

---

## 9. Cross-cluster contract discipline

### Producer responsibilities

A cluster that emits an event MUST:

1. Declare the event in its Stage 02 design doc (§Cross-cluster
   contracts section).
2. List the event in §3 of this document (cross-document update via
   PR).
3. Document the payload schema in a typed event-class file under
   `packages/blocks-{cluster}-events/`.
4. Wrap the emit in the same SQLite transaction as the entity write.
5. Increment `schemaVersion` on payload changes (per §8).

### Consumer responsibilities

A cluster that subscribes to another cluster's event MUST:

1. Declare the subscription in its Stage 02 design doc.
2. Register the handler at bootstrap with idempotency guarantees.
3. Tolerate out-of-order receipt (multiple events may arrive interleaved
   across types).
4. Tolerate retries; handler logic must be deterministic given
   `(event_id, payload)`.
5. Never read the producer cluster's SQLite tables directly; the event
   payload + the producer cluster's typed read-only query interface are
   the only allowed cross-cluster access paths.

### Forbidden patterns

- **Direct cross-cluster writes.** A consumer never inserts into the
  producer's tables. The event is the contract.
- **Cross-cluster transactions.** A consumer's handler runs in its own
  transaction, separate from the producer's emit transaction. (This is
  why idempotency matters: the consumer can succeed on retry even if
  it failed once.)
- **Event chains > 3 deep without explicit design.** A → B → C → D event
  cascade is hard to debug. Stage 02 design docs name the cascade
  explicitly; deeper chains require a dedicated workflow ADR.
- **Recursive event emission.** A handler for `Financial.InvoiceIssued`
  emits `Financial.InvoiceIssued`. The event-store unique constraint
  will dedup, but the pattern is a code smell.

---

## 10. Open questions for CO / cob ratification

### Q1. Foundation package home — `foundation-events` vs `kernel-events`

**Question:** Where does the event-bus dispatcher live in the
foundation/kernel hierarchy? `blocks-work-*` §7 Q12 names
"`foundation-events` or equivalent" — not yet ratified.

**Recommendation:** `foundation-events` (alongside
`foundation-localfirst`, `foundation-multitenancy`). Kernel layer is for
crypto + sync transport primitives; events are domain-shaped and
foundation-tier is the right home. Stage 03 to ratify.

### Q2. Event-bus testing harness

**Question:** Cross-cluster event flows are integration-heavy; what
test pattern is canonical? In-memory fake event bus per test? End-to-
end SQLite-backed?

**Recommendation:** Two-tier testing:
- Per-handler unit tests with a fake event-bus that records emissions
  and lets the test inject events into the handler under test.
- Per-cluster-cluster integration tests in `tests/integration/events/`
  using a real SQLite-backed bus + two simulated replicas.

Stage 03 to design the test harness; reference W#46 shared-design-system
test patterns.

### Q3. Bridge tier event flow

**Question:** Per ADR 0031 + ADR 0088 §4 (Standard / Hosted tiers), the
Bridge runtime exposes Anchor data over a remote API. Do Anchor events
flow to Bridge consumers, or does Bridge query SQLite tables directly?

**Recommendation:** Anchor events flow through to Bridge via the
existing Bridge sync transport. Bridge handlers register the same way as
Anchor-side handlers but live in the Bridge process. This preserves the
"event is the contract" rule and prevents Bridge from coupling to Anchor
SQLite schemas. Out of scope for this document; addressed in a
Bridge-specific ADR.

### Q4. Event payload size limits

**Question:** No explicit cap on payload JSON size. Large payloads (e.g.,
a `Property.LeaseExecuted` event embedding the full lease document)
balloon Loro op-log and `domain_events` table. Cap?

**Recommendation:** 16 KB per event payload. Anything larger goes by
StorageRef (§9 of crdt-friendly-schema-conventions.md): the event
payload carries the `contentHash`, and the body is fetched from the CAS
out of band. Stage 03 to enforce via build-time analyzer.

### Q5. Tombstone of events — when?

**Question:** Events are append-only; do they ever GC?

**Recommendation:** Tombstones happen for crypto-shred only (per §2 of
crdt-friendly-schema-conventions.md). Bulk GC for events older than the
tenant's audit-retention threshold (default 7 years) is a separate
maintenance task, coordinated cross-replica. Out of scope here; flag in
a retention-policy ADR.

### Q6. Event-bus performance budget

**Question:** What's the SLA for event-handler dispatch latency on
Surface Pro 7 / 4 GB RAM target?

**Recommendation:** Per-event in-process dispatch < 5 ms p99 with
zero handlers. Per-handler invocation budget < 20 ms p99. Total
dispatch lag (event recorded → all handlers complete) < 500 ms p99 for
a moderate fan-out (≤ 5 handlers). Stage 06 acceptance tests verify.

### Q7. Causation-chain depth limit

**Question:** §9 forbids chains > 3 deep without explicit design. Is
the limit enforced or advisory?

**Recommendation:** Advisory in v1; warning logged when a handler emits
an event with depth > 3 (depth counted via `causationId` chain). Hard
limit deferred to Stage 06 implementation experience.

### Q8. Out-of-order delivery in Loro

**Question:** Loro's CRDT semantics may apply ops out of timestamp order
during catch-up sync (Lamport-like causality, not wall-clock order). Is
this a problem for handlers that depend on ordering?

**Recommendation:** Handlers must tolerate out-of-order delivery (per
§9 consumer responsibilities). When ordering matters
(`InvoiceIssued` then `PaymentApplied`), the consumer uses
`correlationId` to recognize the workflow and the producer's
`recordedAtUtc` to order within. Out-of-order arrival of events that
share a `correlationId` is allowed; the consumer logic must
reconstruct state from the union.

### Q9. Schema-version negotiation across Anchor releases

**Question:** Anchor A (running v1.0) emits a `schemaVersion: 1.1.0`
event. Anchor B (still on v0.9) receives it — does B reject the event,
upgrade in place, or refuse to sync until B is upgraded?

**Recommendation:** B accepts the event (records in `domain_events`)
but does not deserialize the payload for any handler that requires
v1.x. B's `event_handler_cursor` does not advance past the
unrecognized event for those handlers. When B is upgraded, the
backlog handlers replay the events. UI surfaces "this replica is on an
older version; some events not yet processed" in the admin view.

### Q10. Cross-cluster event vs in-cluster event

**Question:** Some events are purely in-cluster (`People.ActivityLogged`,
`Reports.KPISnapshotTaken`). Do they go through the same envelope and
table as cross-cluster events?

**Recommendation:** Yes. The uniform envelope is simpler than two
mechanisms, and an "in-cluster" event today may grow cross-cluster
consumers tomorrow. Performance impact is negligible (events are small;
SQLite handles 10k events/s on the Surface Pro 7 target).

---

## 11. Discipline summary — how to apply this document

When designing a cluster Stage 02 doc:

1. **Identify cross-cluster touchpoints.** Each becomes either a
   read-only query interface (sync read) or an event (async signal).
2. **For each event, name it** per §2 and add it to §3 with payload +
   idempotency-key derivation.
3. **For each subscribed event, list it** in the §Cross-cluster
   contracts section with handler responsibility.
4. **Wrap event emission in the entity-write transaction** (§9
   producer responsibilities).
5. **Design handlers to be idempotent** (§4 + §9 consumer
   responsibilities).
6. **Document causation chains** longer than 3 events.
7. **Set `schemaVersion`** on every event; bump per §8.

Cluster Stage 06 hand-offs cite this document and reference the
event-table rows in §3 for each event they touch.

---

**End of canonical cross-cluster event-bus design.** Companion documents:

- `crdt-friendly-schema-conventions.md` — CRDT-friendly entity envelope,
  state-machine resolution, posted-then-immutable, StorageRef.
- `party-model-convention.md` — Party-as-base-actor pattern (used in
  event payloads for `partyId`, `customerId`, `employeeId`, etc.).
- `foss-source-survey-anchor-domain.md` — sources studied behind these
  conventions (ADR 0088 Appendix A).
