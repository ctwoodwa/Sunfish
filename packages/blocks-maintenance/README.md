# Sunfish.Blocks.Maintenance

Block for the maintenance-management surface: vendor management + maintenance requests + RFQ/quote workflow + work-order tracking.

Implements [ADR 0053 — Work-order domain model](../../docs/adrs/0053-work-order-domain-model.md) and [ADR 0058 — Vendor onboarding posture](../../docs/adrs/0058-vendor-onboarding-posture.md).

## What this ships

### Vendor surface (W#18 + ADR 0058)

- **`Vendor`** — v1.1 init-only record with `OnboardingState` lifecycle (Pending → W9Requested → W9Received → Active → Suspended → Retired) + `Specialties` taxonomy-list (replaces the v1.0 enum) + `W9` document-id reference + `PaymentPreference` + `Contacts` list.
- **`VendorContact`** — multi-contact child entity with at-most-one-primary-per-vendor invariant + per-property primary override.
- **`VendorPerformanceRecord`** — append-only event log (Hired / JobCompleted / JobNoShow / JobLate / JobCancelled / RatingAdjusted / InsuranceLapse / Suspended / Retired).
- **`W9Document`** — W#18 Phase 4 IRS W-9 capture; **`TinEncrypted: EncryptedField`** per ADR 0046-A2/A4/A5 (W#32 substrate); per-tenant DEK; capability-gated decrypt with `FieldDecrypted` audit on every read.
- **`W9TaxClassification`** — Individual / LLC / SCorp / CCorp / Partnership / Trust / Other.
- **`W9MailingAddress`** — block-local W-9 address record (kept out of `blocks-properties` to avoid cross-block coupling; W-9 form has different validation profile from tenant-property addresses).
- **`VendorMagicLink`** — one-time-use onboarding token (Phase 5; W#21-gated).

### Work-order surface (W#19 + ADR 0053)

- **`WorkOrder`** — v1.0 init-only record with state-machine-validated transitions.
- **`WorkOrderStatus`** + `TransitionTable<TState>` — public state-machine primitive.
- **`WorkOrderAppointment`** — scheduling child entity.
- **`WorkOrderCompletionAttestation`** — operator-witnessed completion record with `SignatureEventRef` (ADR 0054).
- **`WorkOrderEntryNotice`** — right-of-entry compliance child (ADR 0060).

### Maintenance request workflow

- **`MaintenanceRequest`** — tenant-side request entity with priority + status lifecycle.
- **`Rfq`** + **`Quote`** — RFQ → quote acceptance flow.

### Services

- **`IMaintenanceService`** + `InMemoryMaintenanceService` — CRUD + RFQ + quote acceptance.
- **`IVendorContactService`** + `InMemoryVendorContactService` — primary-invariant-enforcing contact management.
- **`IVendorPerformanceLog`** + `InMemoryVendorPerformanceLog` — append-only event log + work-order-derived projection.
- **`IW9DocumentService`** + `InMemoryW9DocumentService` — encrypt-on-write W-9 capture + capability-gated decrypt.

### Audit

- 7 vendor-onboarding `AuditEventType` constants (`VendorCreated`, `VendorMagicLinkIssued`, `VendorMagicLinkConsumed`, `VendorOnboardingStateChanged`, `W9DocumentReceived`, `W9DocumentVerified`, `VendorActivated`).
- 11 work-order `AuditEventType` constants per ADR 0053.
- **TIN bytes never appear in audit payloads** — only `w9_document_id` document pointer (decryption emission lives on the W#32 boundary).

## Cluster role

Per the property-operations cluster reconciliation (2026-04-29), this block is the **EXTEND target** for: W#18 Vendors (vendor-onboarding posture), W#19 Work Orders (multi-party threads + entry-notice + completion-attestation), and `Equipment` cluster siblings (work-order assignment FK targets).

## DI

DI registration is currently caller-driven (the block has no `AddSunfish*` extension yet); each service is registered directly. Producers of new services follow the same pattern.

## ADR map

- [ADR 0053](../../docs/adrs/0053-work-order-domain-model.md) — work-order domain
- [ADR 0058](../../docs/adrs/0058-vendor-onboarding-posture.md) — vendor onboarding
- [ADR 0046](../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md) — `EncryptedField` substrate (W#18 P4 consumer)
- [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) — completion-attestation signatures
- [ADR 0056](../../docs/adrs/0056-foundation-taxonomy-substrate.md) — vendor specialty taxonomy

## See also

- [apps/docs Overview](../../apps/docs/blocks/maintenance/overview.md)
- [Vendor Onboarding](../../apps/docs/blocks/maintenance/vendor-onboarding.md)
- [Work Orders](../../apps/docs/blocks/maintenance/work-orders.md)
- [Workflow](../../apps/docs/blocks/maintenance/workflow.md)
- [Service Contract](../../apps/docs/blocks/maintenance/service-contract.md)
- [Entity Model](../../apps/docs/blocks/maintenance/entity-model.md)
