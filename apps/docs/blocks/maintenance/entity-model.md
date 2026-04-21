---
uid: block-maintenance-entity-model
title: Maintenance — Entity Model
description: Vendor, MaintenanceRequest, Rfq, Quote, and WorkOrder — the entities that make up Sunfish.Blocks.Maintenance.
---

# Maintenance — Entity Model

## Overview

The maintenance block exposes five records and a family of lifecycle enums. All records
are immutable; every state change happens through `IMaintenanceService` and returns a new
record.

## Vendor

A contractor or service provider.

| Field           | Type               | Notes |
|-----------------|--------------------|-------|
| `Id`            | `VendorId`         | Unique identifier. |
| `DisplayName`   | `string`           | Display name shown in the UI. |
| `ContactName`   | `string?`          | Primary contact person. |
| `ContactEmail`  | `string?`          | Primary contact email. |
| `ContactPhone`  | `string?`          | Primary contact phone. |
| `Specialty`     | `VendorSpecialty`  | Trade / service category. |
| `Status`        | `VendorStatus`     | `Active`, `Preferred`, `Suspended`, `Inactive`. |

`VendorSpecialty`: `GeneralContractor`, `Plumbing`, `Electrical`, `HVAC`, `Landscaping`,
`Painting`, `Roofing`, `PestControl`, `Appliances`, `Cleaning`, `Other`.

## MaintenanceRequest

A request for maintenance work, submitted by a tenant or property manager.

| Field                    | Type                        | Notes |
|--------------------------|-----------------------------|-------|
| `Id`                     | `MaintenanceRequestId`      | Unique identifier. |
| `PropertyId`             | `EntityId`                  | Property this request is against. |
| `RequestedByDisplayName` | `string`                    | Name of the submitter. |
| `Description`            | `string`                    | Description of the issue. |
| `Priority`               | `MaintenancePriority`       | `Low`, `Normal`, `High`, `Emergency`. |
| `Status`                 | `MaintenanceRequestStatus`  | See lifecycle below. |
| `RequestedDate`          | `DateOnly`                  | Submission date. |
| `DeficiencyReference`    | `string?`                   | Opaque back-reference to an inspection deficiency (plain string — no compile-time coupling to `blocks-inspections`). |
| `CreatedAtUtc`           | `Instant`                   | First-persisted instant. |

## Rfq

A Request for Quote sent to one or more vendors for a specific maintenance request.

| Field               | Type                      | Notes |
|---------------------|---------------------------|-------|
| `Id`                | `RfqId`                   | Unique identifier. |
| `RequestId`         | `MaintenanceRequestId`    | The maintenance request this RFQ addresses. |
| `InvitedVendors`    | `IReadOnlyList<VendorId>` | Vendors invited to respond. |
| `ResponseDueDate`   | `DateOnly`                | Expected response deadline. |
| `Scope`             | `string`                  | Scope of work to quote. |
| `Status`            | `RfqStatus`               | `Draft`, `Sent`, `Closed`, `Cancelled`. |
| `SentAtUtc`         | `Instant`                 | First-persisted instant. |

## Quote

A price quotation submitted by a vendor in response to an RFQ (or direct solicitation).

| Field            | Type                      | Notes |
|------------------|---------------------------|-------|
| `Id`             | `QuoteId`                 | Unique identifier. |
| `VendorId`       | `VendorId`                | Submitting vendor. |
| `RequestId`      | `MaintenanceRequestId`    | The maintenance request this quote addresses. |
| `Amount`         | `decimal`                 | Quoted cost in the property currency. |
| `ValidUntil`     | `DateOnly`                | Last date the quote is valid for acceptance. |
| `Scope`          | `string?`                 | Optional scope description. |
| `Status`         | `QuoteStatus`             | `Draft`, `Submitted`, `Accepted`, `Declined`, `Expired`, `Withdrawn`. |
| `SubmittedAtUtc` | `Instant`                 | First-persisted instant. |

## WorkOrder

A formal instruction to a vendor to perform work.

| Field               | Type                      | Notes |
|---------------------|---------------------------|-------|
| `Id`                | `WorkOrderId`             | Unique identifier. |
| `RequestId`         | `MaintenanceRequestId`    | The originating maintenance request. |
| `AssignedVendorId`  | `VendorId`                | The vendor assigned. |
| `Status`            | `WorkOrderStatus`         | See lifecycle below. |
| `ScheduledDate`     | `DateOnly`                | Scheduled work date. |
| `CompletedDate`     | `DateOnly?`               | Completion date, or `null` until complete. |
| `EstimatedCost`     | `decimal`                 | Estimated cost. |
| `ActualCost`        | `decimal?`                | Actual cost on completion, or `null`. |
| `Notes`             | `string?`                 | Free-form notes. |
| `CreatedAtUtc`      | `Instant`                 | First-persisted instant. |

## Relationships

```
Vendor              1 ─── N  Quote               (by VendorId)
Vendor              1 ─── N  WorkOrder           (by AssignedVendorId)
Vendor              N ─── N  Rfq                 (via Rfq.InvitedVendors)

MaintenanceRequest  1 ─── N  Rfq                 (by RequestId)
MaintenanceRequest  1 ─── N  Quote               (by RequestId)
MaintenanceRequest  1 ─── N  WorkOrder           (by RequestId)

Deficiency          ← opaque string ─── MaintenanceRequest.DeficiencyReference
```

The `DeficiencyReference` link is a plain string — the block has no compile-time
dependency on `blocks-inspections`. Consumer code translates between the real
`DeficiencyId` value and this string when wiring the two blocks together.

## Related pages

- [Overview](overview.md)
- [Service Contract](service-contract.md)
- [Workflow](workflow.md)
