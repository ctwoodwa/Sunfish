# Path II — CRDT-Friendly Schema Conventions
**Stage:** 02 Architecture  
**Date:** 2026-05-16  
**Author:** XO (research)  
**Applies to:** All `blocks-*` cluster implementations under ADR 0088 (Anchor all-in-one)

---

## Purpose

This document establishes the per-entity CRDT classification and schema conventions for all Path II clusters. COB must apply these conventions when scaffolding and implementing `blocks-*` entities. No entity goes to Stage 04 without a classification.

---

## 1. Two Sync Classes

Every entity in every cluster belongs to exactly one of two sync classes:

### CP-class (Coordination-Required)

**Properties:**
- Immutable once committed; changes are new records (append-only)
- State transitions go through a coordinator that holds a semaphore
- No concurrent writes; optimistic concurrency with a version stamp
- Conflicts are rejected (not merged): the coordinator returns a conflict error
- Replicated to peer nodes via the append-only event log (same events, same order, idempotent apply)

**Implemented via:** `kernel-sync` event-sourced log + `kernel-crdt` CP coordinator; SQLite WAL; Sunfish `IRecoveryCoordinator` pattern. NOT via Loro.

**Examples across clusters:**
- `JournalEntry`, `JournalLine` — immutable once posted; no editing, no deleting
- `Invoice`, `Bill`, `Payment`, `PaymentApplication` — monetary; requires coordination
- `FiscalPeriod.status` (open/closed) — period close is a coordinated, once-only action
- `Contract.signingStatus`, `SigningWorkflow`, `Signature` — sequential state machine
- `Policy`, `PolicyVersion`, `PolicyAcknowledgment` — approval + audit trail

### AP-class (CRDT-Mergeable)

**Properties:**
- Concurrent writes from multiple nodes are safe and will converge
- Text fields use Loro text-CRDT (Yjs-compatible, LTM encoding)
- Scalar fields (numbers, enums, booleans) use last-write-wins with Loro's LWW register
- Set fields (tags, labels, assignments) use Loro OR-Set (add wins, concurrent add/remove → add wins)
- Timestamp fields: HLC (Hybrid Logical Clock) for causality

**Implemented via:** Loro CRDT for text/set fields; HLC timestamps in the entity record for scalars; Sunfish `kernel-crdt` AP path.

**Examples across clusters:**
- `WikiPage.markdownBody` — collaborative text editing
- `Document.tags[]`, `MarketingAsset.tags[]` — tag sets
- `MaintenanceTask.notes`, `status` (soft status, not a gate) — field tech can update offline
- `DashboardWidget.config` — layout and parameter customization
- `Lead.status` (prospect funnel soft state) — eventual consistency acceptable
- `Employee.contactInfo`, `Party.addresses[]`, `Party.phoneNumbers[]`

---

## 2. Schema Conventions

### 2.1 CP-class entity schema

```csharp
public sealed record JournalEntry
{
    // Required: monotonic version for optimistic concurrency
    public int Version { get; init; }

    // Required: event log hash for append-only audit chain
    public string? LastEventHash { get; init; }

    // Required: immutability marker — set to true after first commit, block edits
    public bool IsPosted { get; init; }

    // All mutable state is via new records or compensating entries only
    // No edit allowed; mark IsVoided = true + create a compensating JournalEntry
    public bool IsVoided { get; init; }

    // Standard auditing
    public DateTimeOffset CreatedAt { get; init; }
    public Guid CreatedByPartyId { get; init; }
    public DateTimeOffset? PostedAt { get; init; }
    public Guid? PostedByPartyId { get; init; }
}
```

**Rules:**
- Never soft-delete CP entities — use status transitions (void, cancel, close)
- Never update financial amounts post-commit — create a compensating entry
- Use `int Version` + `SELECT ... WHERE Version = @expected` for optimistic concurrency
- `LastEventHash` chains to the previous event hash (tamper-evidence)

### 2.2 AP-class entity schema

```csharp
public sealed record WikiPage
{
    public Guid Id { get; init; }
    public string WikiSpaceId { get; init; } = "";

    // AP text field: stored as Loro binary blob; decoded to markdown on read
    // Column type: BLOB (SQLite), column name suffix: _loro
    public byte[] MarkdownBodyLoro { get; init; } = [];

    // AP scalar: last-write-wins with HLC timestamp
    public string Title { get; init; } = "";
    public string TitleHlc { get; init; } = ""; // HLC timestamp for this field

    // AP set field: stored as Loro OR-Set; materialized to []string on read
    public byte[] TagsLoro { get; init; } = [];

    // Standard
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
```

**Rules:**
- Loro binary blob columns end in `_loro` (suffix convention — anatomy.md must track these)
- Scalar LWW fields have a paired `*_hlc` column for causality ordering
- OR-Set fields (tag arrays, assignment arrays) also end in `_loro`
- Expose clean DTO properties (string, string[]) from the domain object; hide the Loro internals
- Never query inside Loro blobs from SQL — materialize first, then filter in C#

### 2.3 Cross-class entity — split record pattern

Some entities have both CP core and AP annotations. Use two records:

```
Deliverable (CP) — status, acceptance gate, milestone link, legal
DeliverableNote (AP) — notes, comments, tags added by field team
```

Never mix CP and AP fields in the same aggregate root. If you find yourself wanting to, split.

---

## 3. SQLite Storage Conventions

| Class | Primary key | Version col | Blob cols |
|---|---|---|---|
| CP | `GUID TEXT NOT NULL PRIMARY KEY` (stored as 36-char string) | `Version INTEGER NOT NULL DEFAULT 0` | none |
| AP | `GUID TEXT NOT NULL PRIMARY KEY` | none (version via Loro clock) | `*_loro BLOB NOT NULL DEFAULT X''` |

**Indexes:**
- CP: `(TenantId, UpdatedAt DESC)` for sync watermark queries
- AP: `(TenantId, WikiSpaceId, UpdatedAt DESC)` — domain-specific compound indexes
- Both: `(IsDeleted)` partial index WHERE IsDeleted = 0 (soft delete for UI; AP only — CP uses status)

**Period boundaries:** `FiscalPeriod` + `JournalEntry.fiscalPeriodId` enforce the accounting boundary. Never let a SQL cascade update cross a closed FiscalPeriod.

---

## 4. Entity Registration Convention

All `blocks-*` entities register with `ISunfishEntityModule` (per ADR 0015). Each cluster gets one `*EntityModule.cs`:

```csharp
// packages/blocks-financial-ledger/Sunfish.Blocks.FinancialLedger.EntityModule.cs
public sealed class FinancialLedgerEntityModule : ISunfishEntityModule
{
    public void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChartOfAccounts>().ToTable("FinancialLedger_ChartOfAccounts");
        modelBuilder.Entity<Account>().ToTable("FinancialLedger_Account");
        modelBuilder.Entity<JournalEntry>().ToTable("FinancialLedger_JournalEntry");
        // ...
    }
}
```

Table name convention: `{ClusterPascalName}_{EntityPascalName}` — no collision across clusters.

---

## 5. CRDT Classification Table (All Clusters)

### blocks-financial-ledger (all CP)

| Entity | Class | Reason |
|---|---|---|
| `ChartOfAccounts` | CP | Legal entity accounting anchor; once-per-entity |
| `Account` | CP | GL account tree; structural change requires coordination |
| `JournalEntry`, `JournalLine` | CP | Immutable once posted; financial audit |
| `Invoice`, `InvoiceLine` | CP | Monetary; AR requires coordination |
| `Bill`, `BillLine` | CP | Monetary; AP requires coordination |
| `Payment`, `PaymentApplication` | CP | Cash movement; sequential application |
| `TaxCode`, `TaxRate` | CP | Tax rule references; stable reference data |
| `FiscalPeriod` | CP | Period close is a once-only coordinated action |
| `TaxFormLineMap` | CP | Legal mapping; audit trail required |

### blocks-people (mixed)

| Entity | Class | Reason |
|---|---|---|
| `Party` | CP (core fields) | Identity is authoritative; canonical |
| `PartyAddress[]`, `PhoneNumber[]` | AP (via OR-Set) | Contact info concurrent updates expected |
| `Employee` (structural) | CP | Employment record; HR legal |
| `Employee.notes` | AP | Soft annotation; concurrent updates OK |
| `Customer`, `Tenant` | CP | Financial/legal relationship |
| `Lead`, `Opportunity` | AP | CRM funnel; soft state; last-write-wins |
| `OnboardingTask.status` | CP (if gate) / AP (if checklist) | CP if it unblocks payroll; AP if informational |

### blocks-work (mixed)

| Entity | Class | Reason |
|---|---|---|
| `Project` (core: status, budget) | CP | Status gates invoicing; budget is financial |
| `Project.description`, `notes` | AP | Collaborative editing |
| `WorkOrder` (status, cost) | CP | Status gates payment; cost is financial |
| `WorkOrderLine.notes` | AP | Field tech annotation |
| `MaintenanceSchedule`, `MaintenanceTask` | AP (status) | Field tech can update offline; soft convergence |
| `Contract`, `ContractTerm` | CP | Legal; signing workflow |
| `Deliverable` (acceptance status) | CP | Acceptance gate is coordinated |
| `Deliverable.notes` | AP | Annotation |
| `TimeEntry` | CP (once approved) | Labor cost → JournalEntry; approved = immutable |
| `TimeEntry` (draft) | AP | Draft can be edited by worker offline |

### blocks-docs (mixed)

| Entity | Class | Reason |
|---|---|---|
| `WikiPage.markdownBody` | AP | Collaborative editing; Loro text-CRDT primary use case |
| `WikiPage.title`, `metadata` | AP (LWW) | Last-write-wins; concurrent edits rare |
| `Policy`, `PolicyVersion` | CP | Approval workflow; immutable once published |
| `PolicyAcknowledgment` | CP | Audit record; append-only |
| `ContractTemplate` (fields, clauses) | CP | Legal document; version-controlled |
| `ContractInstance` | CP | Rendered contract; signing state machine |
| `SigningWorkflow`, `Signature` | CP | Sequential signing; order matters |
| `MarketingAsset` (core fields: url, type) | CP | Asset reference is authoritative |
| `MarketingAsset.tags[]`, `altText` | AP | DAM metadata; concurrent updates OK |
| `DocumentTag[]` | AP (OR-Set) | Tag set; add commutative |

### blocks-reports (AP read-layer + CP run records)

| Entity | Class | Reason |
|---|---|---|
| `Report`, `ReportTemplate` | CP | Report definition is versioned; breaking change gate |
| `ReportRun`, `ReportArtifact` | CP (append-only) | Execution history; audit |
| `ReportSchedule`, `ReportSubscription` | AP (user preference) | User configures their schedule independently |
| `Dashboard.layout` | AP (LWW) | User customizes dashboard layout; last-write-wins |
| `DashboardWidget.config` | AP (LWW) | Widget parameters; concurrent updates OK |
| `KPISnapshot` | CP (append-only) | Computed metric history; append-only time series |

---

## 6. Peer Sync Behavior

**CP entities** sync via the append-only event log:
- Each node appends events to its local event log
- `kernel-sync` Tier 2 (Headscale Gossip): event log is exchanged on connect; idempotent apply
- Conflicts on CP entities: the second writer's operation is rejected (re-queue + notify user)
- Financial journals are CP-strict: no concurrent writes even on peer nodes

**AP entities** sync via Loro CRDT:
- Each node maintains a Loro document per AP aggregate root
- Merge is mathematical: Loro merge is commutative + associative
- `kernel-sync` Tier 2: Loro binary update vectors exchanged on connect
- Conflicts: Loro resolves automatically; no user intervention for text merges
- Loro clock is the source of truth for AP scalars (not SQLite `UpdatedAt`)

---

## 7. Must-Not Patterns (Do-Not-Repeat)

- **Never put financial amount in an AP entity.** Any monetary value = CP entity.
- **Never soft-delete a CP entity.** Use status transitions (void, archive, close).
- **Never update a JournalEntry or JournalLine row.** Compensating entries only.
- **Never let a GL account balance be stored directly.** Compute from JournalLine sum.
- **Never mix CP and AP fields in the same aggregate root.** Split the record.
- **Never query inside a Loro blob from SQL.** Materialize in C# first, then filter.
- **Never omit `_loro` suffix on Loro blob columns.** anatomy.md tracks these by convention.
- **Never apply an AP entity's Loro update in the same SQLite transaction as a CP entity write.** They use different consistency paths.
