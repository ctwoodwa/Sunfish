# ADR 0053 — Work Order Domain Model

**Status:** Proposed
**Date:** 2026-04-28
**Resolves:** Property-ops cluster intake [`property-work-orders-intake-2026-04-28.md`](../../icm/00_intake/output/property-work-orders-intake-2026-04-28.md); cluster workstream #19. Specifies the coordination unit that ties property + asset + maintenance source + vendor + appointment + multi-party communication + completion attestation + payment into one atomic identity.

---

## Context

The property-operations cluster has 13 domain modules. They overlap on the same business process: a property has a failing water heater (Asset), an inspection finds it (InspectionFinding), the BDFL hires Acme Plumbing (Vendor) to replace it on Tuesday at 9 AM (Appointment), notifies the tenant 48 hours prior (right-of-entry notice), the vendor completes the work and signs off (signature attestation), Sunfish receives the invoice (Receipt), the BDFL pays via ACH (Payment).

That is **one coordination unit** with one atomic identity — the work order — touching nine modules: Properties, Assets, Inspections, Maintenance, Vendors, Messaging, Signatures, Receipts, Payments. Without a first-class `WorkOrder` entity each module holds a fragment of the truth and reconciliation becomes impossible: when the audit-trail asks "did the vendor complete the job before payment?" or "was the tenant notified before entry?" or "which asset did this receipt cost-basis?", the answer requires walking nine different stores looking for correlation IDs.

Work order is the *coordination keystone* of the property-ops cluster. The cluster intake INDEX explicitly names it as such:

> Most architecturally consequential after the messaging substrate. Maintenance items become work-order *sources*; vendors become *assignees*; appointments become *coordinated time slots*; receipts become *completion artifacts*; signatures become *sign-off attestations*.

This ADR pins the entity model, state machine, source polymorphism, CP/AP classification per record class, and integration contracts with surrounding modules. ADR 0049 substrate (audit-trail) is the persistence pattern; ADR 0028 + paper §6.3 are the concurrency primitive (CP-class lease for appointment slot booking); ADR 0052 substrate (messaging) is where multi-party threads live. ADR 0053 doesn't rebuild any of those — it composes them.

---

## Decision drivers

- **Cluster keystone.** Eight cluster intakes reference work orders directly: Maintenance, Inspections, Vendors, Receipts, Signatures, Messaging, Properties, Owner Cockpit. Stage 02 design on any of them depends on `WorkOrder` shape being final.
- **Right-of-entry compliance is non-negotiable.** Most US states require 24–48h written notice to tenants for non-emergency vendor entry; the work-order entity owns the audit trail proving notice was given. Compliance failures end property-management businesses.
- **Appointment double-booking is unacceptable.** A vendor cannot be at two properties at the same hour; a property can host only one appointment per slot. This is structurally CP-class per paper §6.3 — a slot booking either succeeds or fails atomically.
- **Multi-party visibility model is hard.** Owner ↔ vendor ↔ tenant communication has overlapping but distinct visibility needs. Work-order owns its primary thread; party-pair side threads (owner-vendor private) are separate.
- **State-machine fidelity matters.** Work orders span days to weeks. State transitions (Approved → AppointmentConfirmed → InProgress → Completed → Invoiced → Paid) are first-class events tied to vendor + tenant + owner actions. Audit-substrate emission per transition is mandatory.
- **Polymorphic source model.** A work order can originate from a maintenance request (tenant-submitted), an inspection finding (annual or move-in/out), manual owner entry, or a recurring schedule. The pattern needs to compose with ADR 0049 audit substrate.
- **ADR 0013 enforcement gate is active.** Vendor magic-link work-order page is served by Bridge; vendor identity boundary per ADR 0043; capability flow via macaroons per ADR 0032. None of these are bespoke to this ADR.
- **Repair workflow basics from Phase 2 commercial intake.** Phase 2 commercial intake names "3-vendor quote → review → approve" + capital-vs-expense classification as in-scope. This ADR's WorkOrder accommodates that flow as a pre-state (`Scoped`) without forcing it.

---

## Considered options

### Option A — Materialized state + append-only event log [RECOMMENDED]

`WorkOrder` is a materialized record with current state stored explicitly. Every state transition emits a typed event into ADR 0049 audit substrate. Event log is the audit-trail-of-record; materialized state is the read-path optimization. State transitions go through a state-machine guard that rejects invalid transitions before they reach the audit substrate.

- **Pro:** Fast reads (no event-replay for current state).
- **Pro:** Aligns with rest of Sunfish kernel (audit substrate is append-only; entity records are materialized).
- **Pro:** State-machine guard is application-layer enforcement (testable, auditable).
- **Con:** Two sources of truth (materialized + log) — must stay consistent. Mitigation: state-machine guard always writes both atomically per workstream-level transaction.

**Verdict:** Recommended.

### Option B — Pure event-sourcing (state-as-projection)

`WorkOrder` exists only as an event stream; current state is computed by replay. No materialized state field.

- **Pro:** Single source of truth.
- **Pro:** Time-travel queries are free.
- **Con:** Read-path cost — replay every event for every read.
- **Con:** Doesn't match the rest of Sunfish's persistence pattern (block entities are materialized).
- **Con:** Snapshot/projection management adds complexity not present elsewhere in the kernel.

**Verdict:** Rejected. Pure event-sourcing is overkill for a Phase 2 SMB property business; complexity not justified.

### Option C — State machine with state-as-string field; no audit emission

Materialized record with a `Status` enum field; no event emission to audit substrate.

- **Pro:** Simplest.
- **Con:** Right-of-entry compliance has no audit trail. Compliance defense impossible.
- **Con:** Reverses 5 years of architecture: ADR 0049 substrate exists specifically to centralize this concern.

**Verdict:** Rejected. Compliance failure mode.

---

## Decision

**Adopt Option A.** Materialized `WorkOrder` record + append-only event log emitting through ADR 0049 substrate; state-machine guard enforces valid transitions at the application layer; CP-class lease guards appointment-slot bookings per paper §6.3.

### Initial contract surface

```csharp
namespace Sunfish.Blocks.WorkOrders;

// Primary entity
public sealed record WorkOrder
{
    public required WorkOrderId Id { get; init; }
    public required TenantId Tenant { get; init; }
    public required PropertyId Property { get; init; }
    public AssetId? Asset { get; init; }                       // nullable: not all WO target an asset
    public PropertyUnitId? Unit { get; init; }
    public required WorkOrderStatus Status { get; init; }
    public required WorkOrderPriority Priority { get; init; }   // Emergency | High | Normal | Low
    public required IdentityRef OpenedBy { get; init; }
    public required DateTimeOffset OpenedAt { get; init; }
    public Guid? RecurrenceParent { get; init; }                // forward-compat for Phase 2.3 recurring
    public VendorId? AssignedVendor { get; init; }
    public DateTimeOffset? ExpectedCompletion { get; init; }
    public DateTimeOffset? ActualCompletion { get; init; }
    public Money? TotalCost { get; init; }                      // ADR 0051
    public string? CompletionNotes { get; init; }
    public required ThreadId PrimaryThread { get; init; }       // ADR 0052; owner+vendor+tenant
}

public readonly record struct WorkOrderId(Guid Value);
public enum WorkOrderPriority { Emergency, High, Normal, Low }

// State machine
public enum WorkOrderStatus
{
    Draft,                    // owner editing; not yet routed
    Scoped,                   // estimates/quotes obtained; awaiting approval
    AssignedToVendor,         // vendor selected; awaiting their accept
    AppointmentProposed,      // vendor proposed slot(s); awaiting confirmation chain
    AppointmentConfirmed,     // slot booked (CP lease held); tenant notified
    InProgress,               // vendor on-site / actively working
    AwaitingSignOff,          // work claimed complete; sign-off pending
    Completed,                // signed off
    Invoiced,                 // invoice received; awaiting payment
    Paid,                     // payment cleared (ADR 0051 ChargeStatus = Settled)
    Closed,                   // terminal: post-ACH-return-window or manual close

    // Side branches
    Canceled,                 // owner-initiated abort
    OnHold,                   // paused (tenant unavailable / parts on order)
    Disputed                  // any party disputes; only owner can resolve
}

// State-machine guard (application-layer enforcement)
public interface IWorkOrderStateMachine
{
    Task<WorkOrderTransitionResult> TransitionAsync(
        WorkOrderId workOrder,
        WorkOrderStatus from,
        WorkOrderStatus to,
        WorkOrderTransitionContext context,
        CancellationToken ct);
}

public sealed record WorkOrderTransitionContext
{
    public required IdentityRef Actor { get; init; }
    public required string Reason { get; init; }
    public required AuditCorrelation Audit { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public enum WorkOrderTransitionResult
{
    Accepted,
    RejectedInvalidTransition,
    RejectedCapabilityMissing,
    RejectedAppointmentLeaseUnavailable
}

// Polymorphic source — represented as the FIRST audit event in the work-order's audit stream
public sealed record WorkOrderOpenedFromEvent : IAuditRecord
{
    public required WorkOrderId WorkOrder { get; init; }
    public required WorkOrderSource Source { get; init; }
    public required IdentityRef OpenedBy { get; init; }
    public required DateTimeOffset OpenedAt { get; init; }
}

public abstract record WorkOrderSource
{
    public sealed record FromMaintenanceRequest(MaintenanceRequestId Id) : WorkOrderSource;
    public sealed record FromInspectionFinding(InspectionId Inspection, AssetConditionAssessmentId Finding) : WorkOrderSource;
    public sealed record Manual(string Reason) : WorkOrderSource;
    public sealed record FromRecurringSchedule(RecurrenceScheduleId Schedule) : WorkOrderSource; // Phase 2.3+
}

// Appointment — CP-class slot booking
public sealed record WorkOrderAppointment
{
    public required Guid Id { get; init; }
    public required WorkOrderId WorkOrder { get; init; }
    public required DateTimeOffset SlotStart { get; init; }
    public required TimeSpan SlotDuration { get; init; }
    public required PropertyId Property { get; init; }
    public required VendorId Vendor { get; init; }
    public required AppointmentStatus Status { get; init; }
    public required TenantAccessCoordination TenantAccess { get; init; }
}

public enum AppointmentStatus
{
    Proposed,                // vendor offered; not yet locked
    Locked,                  // CP-lease held (per paper §6.3); finalization in flight
    Confirmed,               // tenant access coordination complete; slot reserved
    Completed,
    Canceled,
    Rescheduled              // soft-deleted; replaced by another appointment row
}

public sealed record TenantAccessCoordination
{
    public required EntryNoticeId NoticeId { get; init; }
    public required DateTimeOffset NoticeSentAt { get; init; }
    public required EntryNoticeChannel NoticeChannel { get; init; }     // Email | Sms | InPerson
    public TenantAcknowledgement? Acknowledgement { get; init; }
    public required DateTimeOffset LegallyAdvancesAt { get; init; }     // jurisdiction-policy-derived; e.g., NoticeSentAt + 48h
}

public enum EntryNoticeChannel { Email, Sms, InPerson }

public sealed record TenantAcknowledgement
{
    public required IdentityRef AckedBy { get; init; }
    public required DateTimeOffset AckedAt { get; init; }
    public required AcknowledgementKind Kind { get; init; }
}

public enum AcknowledgementKind { Confirmed, Rescheduled, Declined }

// Right-of-entry notice (the policy itself lives in a separate ADR — see "Defers to" below)
public sealed record WorkOrderEntryNotice
{
    public required EntryNoticeId Id { get; init; }
    public required WorkOrderId WorkOrder { get; init; }
    public required Guid AppointmentId { get; init; }
    public required string TemplateVersionHash { get; init; }   // content-hash-bound (signatures pattern; ADR 0054 right-of-entry compliance)
    public required string RenderedContent { get; init; }       // for retention; SHA-256 reproduces TemplateVersionHash
    public required JurisdictionPolicyId Jurisdiction { get; init; }
}

public readonly record struct EntryNoticeId(Guid Value);

// Completion attestation
public sealed record WorkOrderCompletionAttestation
{
    public required WorkOrderId WorkOrder { get; init; }
    public required SignatureEventId VendorSignature { get; init; }     // ADR 005X signatures ADR
    public SignatureEventId? OwnerCounterSignature { get; init; }       // optional; required >threshold
    public SignatureEventId? TenantCounterSignature { get; init; }      // rare; tenant-acknowledged repairs
    public required DateTimeOffset AttestedAt { get; init; }
}
```

(Schema sketch only; XML doc + nullability + `required` enforced at Stage 06.)

### Concurrency: CP/AP per record class (paper §6.3)

| Record class | CP/AP | Why |
|---|---|---|
| `WorkOrder` (entity) | **AP** | Multi-actor concurrent updates safe (LWW for non-state fields; state transitions guarded separately). Conflicts resolve by last-write-wins on metadata fields. |
| `WorkOrder.Status` (state transitions) | **CP-via-lease** | State transitions go through `IWorkOrderStateMachine`; the guard acquires a workstream-level lease per ADR 0028 to serialize transitions for one work order. Prevents two actors racing to advance state to incompatible targets. |
| `WorkOrderAppointment.SlotStart` (booking) | **CP-via-lease** | Slot booking acquires a lease scoped to `(VendorId, SlotStart-window)` AND a lease scoped to `(PropertyId, SlotStart-window)`. Both must succeed atomically. Per paper §6.3 default 30s lease timeout. Prevents double-booking. |
| `WorkOrderEntryNotice` | **AP append-only** | Notice events are immutable; idempotent under concurrent emission. |
| `WorkOrderCompletionAttestation` | **AP append-only** | Each signature event is immutable per ADR 005X signatures ADR; attestation record is the join. |

The CP-via-lease primitive is paper §6.3 distributed lease. ADR 0028's `Kernel.Crdt` substrate already exposes the lease primitive; this ADR composes it, not re-implement.

### Polymorphic source — event-sourced not discriminator

The work-order's source is recorded as the FIRST audit event in its audit stream (`WorkOrderOpenedFromEvent`). Subsequent state transitions stack on top. This pattern:

- Aligns with ADR 0049 substrate (append-only audit log is canonical).
- Avoids nullable-FK proliferation on the `WorkOrder` entity table.
- Allows new source types added without schema migration (just a new `WorkOrderSource` record subtype).
- Preserves source-of-record across state transitions (the OpenedFromEvent is in the audit log forever, even after the work-order is closed).

The `WorkOrder` entity has no `SourceType` discriminator field. Consumers needing source info read the work-order's audit stream.

### Multi-party threads (ADR 0052 composition)

Each work order owns exactly one **primary thread** (`PrimaryThread: ThreadId`) whose `Scope` is `WorkOrder` and whose `Participants` are owner + assigned vendor + tenant (when occupied). This thread is the on-the-record communication channel.

**Side party-pair threads are separate threads**, opened ad hoc per ADR 0052 substrate (e.g., owner-vendor pricing-negotiation thread with `Scope: VendorRelation`). The work order knows about its primary thread; side threads are messaging-substrate's call.

This ADR explicitly avoids per-message visibility overrides (per ADR 0052 OQ-M5 recommendation); cleaner to model owner-vendor private-aside as a separate thread than to layer per-message ACLs onto the primary thread.

### Vendor magic-link page (ADR 0032 composition)

The vendor's view of a work order is served by Bridge as a magic-link page. Authentication uses ADR 0032 macaroons:

- Owner adds vendor to a work order → Bridge issues a short-lived macaroon scoped to `(VendorId, WorkOrderId, capabilities: {view, status-update, photo-upload, sign-off})`
- Macaroon TTL: 7 days default; refreshable by owner; expires on work-order Closed
- Magic link format: `https://{tenant-bridge}/wo/{work_order_id}#{macaroon_token}`
- Token lives in URL fragment (not server-logged via referrer); verified server-side per request

Vendor identity boundary per ADR 0043 (threat model addendum from cluster Vendor intake) — the magic-link is *capability-bearing*, not identity-bearing; vendor doesn't authenticate-as-vendor at the kernel level.

### Audit-substrate integration (ADR 0049)

Every state transition + every appointment booking + every entry notice + every attestation emits a typed audit record:

| Audit record type | Emitted on |
|---|---|
| `WorkOrderOpenedFromEvent` | First event in audit stream; carries `WorkOrderSource` |
| `WorkOrderTransitioned` | Every state transition; carries from + to + actor + reason |
| `AppointmentProposed` / `Locked` / `Confirmed` / `Completed` / `Canceled` / `Rescheduled` | Appointment lifecycle |
| `WorkOrderEntryNoticeSent` | Right-of-entry notice issued |
| `WorkOrderEntryNoticeAcknowledged` | Tenant ack received |
| `WorkOrderCompletionAttested` | Vendor sign-off (with optional counter-signs) |
| `WorkOrderInvoiceLinked` | Receipt FK set (ADR 0051 path) |
| `WorkOrderPaymentSettled` | Payment cleared (ADR 0051 `ChargeStatus.Settled`) |
| `WorkOrderDisputed` / `WorkOrderDisputeResolved` | Dispute lifecycle |

### What this ADR does NOT do

- **Right-of-entry policy.** Multi-jurisdiction entry-notice rules (which states require what notice period; what language is required) live in a separate ADR (cluster INDEX names this explicitly as a follow-up). This ADR's `WorkOrderEntryNotice` references the policy module via `JurisdictionPolicyId` but doesn't encode policies.
- **Recurring-schedule scheduler.** Phase 2.3+. The `RecurrenceParent` field on WorkOrder + `FromRecurringSchedule` source variant are forward-compat reservations.
- **3-vendor-quote workflow.** Phase 2 commercial intake's "Repair workflow basics" deliverable owns the quote/review/approve flow; this ADR's `Scoped` state is the integration point.
- **Payment processing.** Per ADR 0051. WorkOrder's `TotalCost: Money?` and `PaymentSettled` audit event reference the substrate; payment execution is provider-side.
- **Signature mechanism.** Per ADR 005X (signatures ADR; not yet drafted). WorkOrder's `WorkOrderCompletionAttestation` references `SignatureEventId`; signature capture is signatures-ADR's domain.
- **Capital-vs-expense classification.** `blocks-tax-reporting` consumes work-order completion + total cost; classification UX lives there.

---

## Consequences

### Positive

- **Eight cluster intakes unblock simultaneously** on acceptance: Maintenance, Inspections, Vendors, Receipts, Signatures, Messaging, Properties, Owner Cockpit.
- Coordination keystone reduces N×M integration combinations to N+M (every domain plugs into work-orders, not into each other).
- CP/AP per record class is principled (paper §6.3 alignment) — appointment double-booking impossible, work-order entity stays AP-fast.
- Source polymorphism via audit-stream avoids nullable-FK proliferation + supports future source types without schema migration.
- Audit-substrate emission is uniform across the 11 audit record types; right-of-entry compliance defense has a single source.
- Vendor magic-link via macaroon (ADR 0032) is pre-built; no bespoke vendor-auth.
- Recurring-schedule forward-compat (`RecurrenceParent` + `FromRecurringSchedule` reserved) avoids an api-change later when Phase 2.3 ships.

### Negative

- 11 audit record types is significant audit-substrate surface. Reviewers must keep them coherent with ADR 0049's vocabulary.
- State machine has 13 states + 3 side branches. Implementation must enumerate valid transitions exhaustively; missing one creates dead-letter behavior.
- CP-class lease for appointment booking introduces coordination latency (~100ms typical for distributed lease acquire). Acceptable for human-time scheduling; would not be acceptable for sub-second flows.
- Polymorphic source via audit-stream means consumers needing source info must read the audit log, not just the work-order entity row. Slight read-path indirection.
- Right-of-entry policy is deferred to a separate ADR; this ADR's `JurisdictionPolicyId` is opaque until that ADR ships.

### Trust impact / Security & privacy

- **Vendor magic-link is capability-bearing.** Compromised link → workstream-scoped capability theft. Mitigation: short TTL (7 days default), revocable on work-order Close, scoped to single work-order.
- **Right-of-entry compliance is a legal exposure surface.** The audit trail proves notice was given, when, and via which channel. Substrate emission is mandatory; bypass is a compliance bug.
- **Multi-party visibility** is enforced via ADR 0052 thread visibility; no per-message ACL leak.
- **Tenant access coordination state is sensitive.** "Tenant declined entry" is data the vendor doesn't need to see; visibility-rule trims at thread-substrate layer.
- **Appointment slot bookings are CP-class** — by design, a partial network split could prevent booking entirely. Better than allowing double-booking; surface to user clearly when lease unavailable.

---

## Compatibility plan

### Existing callers / consumers

No production code references work-order entity today — `blocks-work-orders` is a future package per cluster intake. This ADR's contracts are entirely additive. Phase 2 commercial intake's "Repair workflow basics" deliverable updates from "freeform repair workflow in `blocks-workflow`" to "repair workflow flows through `blocks-work-orders`" — chore-class follow-up commit after this ADR is Accepted.

### Affected packages (new + modified)

| Package | Change |
|---|---|
| `packages/blocks-work-orders` (new) | **Created** — primary deliverable: WorkOrder entity, state machine guard, appointment + entry-notice + completion-attestation child entities |
| `packages/foundation-multitenancy` (existing) | No change |
| `packages/kernel-audit` (existing) | **Modified** — adds 11 typed audit record subtypes per the table above |
| `packages/kernel-cp` (or wherever paper §6.3 lease lives) | No change (consumer only) |
| `packages/blocks-properties` (sibling intake) | **Eventual consumer** |
| `packages/blocks-vendors` (sibling intake) | **Eventual consumer** |
| `packages/blocks-assets` (sibling intake) | **Eventual consumer** |
| `packages/blocks-inspections` (sibling intake) | **Eventual consumer** — InspectionFinding emits work-order draft |
| `packages/blocks-receipts` (sibling intake) | **Eventual consumer** — Receipt FK to WorkOrder |
| `packages/blocks-messaging` (ADR 0052 follow-on) | **Eventual consumer** — primary thread |
| `packages/blocks-signatures` (signatures ADR follow-on) | **Eventual consumer** — completion attestation |
| `accelerators/bridge` (existing) | **Modified** — vendor magic-link page surface |
| `accelerators/anchor-mobile-ios` (cluster intake) | **Eventual consumer** — open-WO-from-finding flow |

### Migration

No production migration. Phase 2 commercial intake reference update is a 1-line follow-up.

---

## Implementation checklist

- [ ] `Sunfish.Blocks.WorkOrders` package scaffolded with `ISunfishEntityModule` per ADR 0015
- [ ] `WorkOrder`, `WorkOrderId`, `WorkOrderStatus`, `WorkOrderPriority` types defined; full XML doc + nullability + `required` annotations
- [ ] `WorkOrderAppointment`, `AppointmentStatus`, `TenantAccessCoordination` types
- [ ] `WorkOrderEntryNotice`, `EntryNoticeId`, `EntryNoticeChannel` types (policy-opaque; wires to right-of-entry ADR when it lands)
- [ ] `WorkOrderCompletionAttestation` type (references `SignatureEventId` from signatures ADR)
- [ ] `WorkOrderSource` polymorphic record + `WorkOrderOpenedFromEvent` audit record
- [ ] `IWorkOrderStateMachine` interface + default implementation enforcing valid transitions; rejects on capability missing or lease unavailable
- [ ] State machine: 13-state table with valid transitions enumerated; tests for every accept + every reject path
- [ ] Appointment slot booking: CP-class lease acquired on `(VendorId, slot-window)` AND `(PropertyId, slot-window)`; both must succeed atomically; tests for double-booking rejection
- [ ] 11 typed audit records added to kernel-audit per ADR 0049; subtype-coherence with existing audit record types verified
- [ ] Adapter parity tests: Blazor + React work-order list + detail view + appointment scheduler
- [ ] Bridge vendor magic-link page: ADR 0032 macaroon-authenticated; status-update + photo-upload + sign-off paths
- [ ] iOS field-app integration point: open-WO-from-finding flow during inspection
- [ ] kitchen-sink demo: full lifecycle on a sample property (Draft → … → Paid → Closed) with three actors (owner, vendor, tenant)
- [ ] Right-of-entry notice template versioning + content-hash-binding integrated (placeholder until right-of-entry ADR lands)
- [ ] apps/docs entry covering work-order lifecycle + state machine + integration points

---

## Open questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-W1 | Owner counter-sign threshold: required for all work orders, or only over $X? | Stage 02 — config-driven; default required for all in Phase 2.1g; threshold-only in Phase 2.3. |
| OQ-W2 | Disputed state — what triggers it, who can transition into/out of it? | Stage 02 — any party can flag (transition Confirmed/InProgress/AwaitingSignOff → Disputed); only owner can resolve (Disputed → previous state OR Closed). |
| OQ-W3 | OnHold ↔ side states (parts on order, tenant unavailable). Sub-states or single OnHold + free-text reason? | Stage 02 — single OnHold + structured reason (enum) + free-text. |
| OQ-W4 | Recurring-schedule scheduler implementation — Quartz.NET (Phase 2 commercial intake's choice for monthly statements) or domain-specific? | Phase 2.3 separate ADR. Forward-compat reservations sufficient now. |
| OQ-W5 | Tenant access coordination auto-advance after legally-required notice elapses. Does state advance to "right-to-enter" on schedule even without tenant ack? | Right-of-entry ADR (cluster INDEX item #7). This ADR's `LegallyAdvancesAt` field reserves the contract. |
| OQ-W6 | Magic-link macaroon TTL default. 7d vs longer for slow-vendor scenarios. | Stage 02 — 7d default; per-tenant override; refreshable. |
| OQ-W7 | Work-order ID format: GUID (current) vs human-readable (e.g., `WO-2026-04-001`). | Stage 03 — both: GUID is canonical; human-readable is a derived display field per tenant counter. |
| OQ-W8 | Multi-property work order (rare; same vendor, multiple properties same day). Single WO with multiple Property FKs vs N WOs with shared parent? | Defer; not Phase 2.1 requirement. Add when surfaces. |

---

## Revisit triggers

This ADR should be re-evaluated when any of the following fire:

- **Recurring-schedule scheduler is required** (Phase 2.3+). Forward-compat reservations may need amendment.
- **Multi-property work-order pattern** crosses in-scope threshold (a vendor visiting 4 properties on one route). Schema impact.
- **Right-of-entry policy ADR** ships — this ADR's `JurisdictionPolicyId` field becomes load-bearing; integration validation needed.
- **A regulated jurisdiction** (rent control, just-cause eviction laws, sealed evidence requirements) requires audit-record fields not currently captured.
- **Vendor performance scoring** requires data this ADR doesn't capture (response time, on-time-arrival rate, customer-satisfaction surveys).
- **Marketplace/dispatch model** (work-order broadcast to multiple available vendors) becomes a real requirement. Substantial design impact.
- **Appointment slot CP-lease latency** becomes user-visible pain. Currently ~100ms acceptable; if cluster intake on iOS shows it's not, revisit lease scope.

---

## References

### Predecessor and sister ADRs

- [ADR 0008](./0008-foundation-multitenancy.md) — Multi-tenancy.
- [ADR 0013](./0013-foundation-integrations.md) — Provider neutrality. Vendor magic-link has no vendor SDK; pure Bridge surface.
- [ADR 0015](./0015-module-entity-registration.md) — `ISunfishEntityModule` registration for `blocks-work-orders`.
- [ADR 0028](./0028-crdt-engine-selection.md) — CRDT engine + paper §6.3 distributed lease primitive (CP-class slot booking).
- [ADR 0032](./0032-multi-team-anchor-workspace-switching.md) — Macaroon model for vendor magic-link capability flow.
- [ADR 0043](./0043-unified-threat-model-public-oss-chain-of-permissiveness.md) — Vendor identity boundary; magic-link is capability-bearing.
- [ADR 0049](./0049-audit-trail-substrate.md) — Audit substrate; 11 typed work-order audit records emit per the table.
- [ADR 0051](./0051-foundation-integrations-payments.md) — Payment substrate; `WorkOrder.TotalCost: Money` + `PaymentSettled` audit event.
- [ADR 0052](./0052-bidirectional-messaging-substrate.md) — Messaging substrate; primary thread + entry-notice delivery.

### Roadmap and specifications

- Paper §6.3 — Distributed Lease Coordination (CP-class appointment slot pattern).
- [Property-ops cluster INDEX](../../icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md) — pins ADR drafting order; work-order is #2.
- [Work Orders cluster intake](../../icm/00_intake/output/property-work-orders-intake-2026-04-28.md) — Stage 00 spec source.
- Phase 2 commercial intake — "Repair workflow basics" deliverable composes through this ADR.

### Existing code / substrates

- `packages/kernel-audit/` — audit substrate consumer.
- `packages/foundation-macaroons/` — macaroon model for magic-link.
- `packages/foundation-multitenancy/` — `TenantId` + tenant-scoping.
- `packages/blocks-rent-collection/` — sibling block (different domain, similar shape).

### External

- NACHA right-of-entry conventions (US tenant law) — referenced via right-of-entry ADR (separate).
- ITIL / ISO 20000 — informal precedent for change-request/work-order state machines (not normative; informational).

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options considered: materialized + event log (A), pure event-sourcing (B), state-as-string no audit (C). Option A chosen with explicit rejection rationale for B (read-path cost; mismatch with rest of kernel) and C (compliance failure mode).
- [x] **FAILED conditions / kill triggers.** Listed: recurring-schedule scheduler required, multi-property pattern, right-of-entry policy ships, regulated jurisdiction, vendor performance scoring, marketplace dispatch, lease latency pain. Each tied to externally-observable signal.
- [x] **Rollback strategy.** No production data exists. Rollback = revert this ADR + revert `Sunfish.Blocks.WorkOrders` package + revert 11 audit-record-type additions in kernel-audit. Phase 2 commercial intake's "Repair workflow basics" reverts to freeform-in-blocks-workflow.
- [x] **Confidence level.** **HIGH.** Composes well-understood substrates (audit, lease, macaroon, messaging, payments). No novel primitives. Risk is in long-tail state-machine completeness (13 states + 3 side branches × ~50 valid transitions) — discovered at Stage 06 build.
- [x] **Anti-pattern scan.** Glanced at `.claude/rules/universal-planning.md` 21-AP list. None of AP-1, AP-3, AP-9, AP-12, AP-21 apply. Substrate composition explicit; phases observable; sources cited.
- [x] **Revisit triggers.** Seven explicit conditions named.
- [x] **Cold Start Test.** Implementation checklist is 14 specific tasks, each verifiable. Fresh contributor reading this ADR + the Work Orders intake + ADR 0028 + ADR 0049 + ADR 0032 should be able to scaffold `blocks-work-orders` without asking for clarification on shape.
- [x] **Sources cited.** ADR 0008, 0013, 0015, 0028, 0032, 0043, 0049, 0051, 0052 referenced. Paper §6.3 cited. Cluster INDEX cited. ITIL informal precedent flagged as non-normative.
