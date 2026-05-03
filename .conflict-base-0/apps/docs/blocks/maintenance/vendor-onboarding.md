# Vendor Onboarding (W#18, ADR 0058)

`Sunfish.Blocks.Maintenance` v1.1 ships the vendor-onboarding posture per [ADR 0058](../../../docs/adrs/0058-vendor-onboarding-posture.md). The substrate covers: vendor-record extension, multi-contact + per-property primary, append-only performance log, taxonomy-backed specialties, and the audit-event substrate. W-9 capture (Phase 4) and magic-link onboarding (Phase 5) are scaffolded but pending W#32 (`EncryptedField` substrate) and W#20 (magic-link delivery) builds respectively.

## Capability gradient

Per ADR 0058 §"Capability gradient" — the lifecycle vocabulary maps to ADR 0043's three-tier trust catalog:

| Tier | `VendorOnboardingState` | What works | Pending |
|---|---|---|---|
| Anonymous | `Pending` | Vendor record exists; recoverable + searchable | No work-order assignment; no payments |
| Vendor | `W9Requested` → `W9Received` → `Active` | Receives magic-link work orders + payments | (Operator wires payment-preference manually) |
| Vendor-with-portal | (deferred to Phase 4+) | Bridge OIDC account + dashboard | Out of W#18 scope |

`Suspended` and `Retired` are operational holds (no new assignments) that any of the three tiers can fall to.

## Vendor record (W#18 Phase 1)

Migrated from the v1.0 positional record to v1.1 init-only per ADR 0058 amendment A2:

```csharp
var vendor = new Vendor
{
    Id = VendorId.NewId(),
    DisplayName = "Acme Plumbing",
    ContactEmail = "ops@acme.example",
    Status = VendorStatus.Active,
    OnboardingState = VendorOnboardingState.Pending,
    Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing),
    // W9 = null until Phase 4 lands
    // PaymentPreference = null until operator chooses
    // Contacts = empty until VendorContacts are added (Phase 2)
};
```

The legacy `VendorSpecialty` enum is preserved through the migration window. Use `VendorSpecialtyClassifications.FromLegacyEnum(specialty)` or `.ToList(specialty)` to mechanically migrate existing call sites. See `MIGRATION.md` for the full v1.0 → v1.1 checklist.

## VendorContact + per-property primary (W#18 Phase 2)

Multi-contact-per-vendor with per-property primary override:

```csharp
var contactService = serviceProvider.GetRequiredService<IVendorContactService>();

await contactService.AddContactAsync(new VendorContact
{
    Id = new VendorContactId(Guid.NewGuid()),
    Vendor = vendor.Id,
    Name = "Bob (Owner)",
    RoleLabel = "Owner",
    Email = "bob@acme.example",
    IsPrimaryForVendor = true,
}, ct);

// Per-property override — Carol is the primary for property-alpha only
await contactService.AddContactAsync(new VendorContact
{
    Id = new VendorContactId(Guid.NewGuid()),
    Vendor = vendor.Id,
    Name = "Carol (Field Tech)",
    RoleLabel = "Field Tech",
    SmsNumber = "+15551234567",
    IsPrimaryForVendor = false,
    PrimaryForProperty = new Dictionary<EntityId, bool> { [propertyAlpha] = true },
}, ct);

// Resolution: per-property override beats vendor-wide default
var primary = await contactService.GetPrimaryForPropertyAsync(vendor.Id, propertyAlpha, ct);
// → Carol (per-property override)

var defaultPrimary = await contactService.GetPrimaryForPropertyAsync(vendor.Id, propertyBeta, ct);
// → Bob (vendor-wide default; no override for property-beta)
```

The InMemory implementation enforces the **at-most-one-primary-per-vendor** invariant: when a contact is upserted with `IsPrimaryForVendor = true`, any prior primary at the same vendor is auto-demoted. Demotion is scoped to the vendor.

## VendorPerformanceRecord append-only log (W#18 Phase 3)

Append-only event log sourced from work-order lifecycle events. Nine event categories: `Hired`, `JobCompleted`, `JobNoShow`, `JobLate`, `JobCancelled`, `RatingAdjusted`, `InsuranceLapse`, `Suspended`, `Retired`.

```csharp
var perfLog = serviceProvider.GetRequiredService<IVendorPerformanceLog>();

// Direct append (operator-initiated event)
await perfLog.AppendAsync(new VendorPerformanceRecord
{
    Id = new VendorPerformanceRecordId(Guid.NewGuid()),
    Vendor = vendor.Id,
    Event = VendorPerformanceEvent.InsuranceLapse,
    OccurredAt = DateTimeOffset.UtcNow,
    RecordedBy = operatorId,
    Notes = "Certificate expired 2026-04-30; vendor notified",
}, ct);

// Projection from work-order events (the W#19 audit pipeline calls this)
await perfLog.ProjectFromWorkOrderAsync(
    vendor.Id, completedWorkOrder.Id, VendorPerformanceEvent.JobCompleted,
    operatorId, completedWorkOrder.CompletedDate.Value, notes: null, ct);

// Pagination
await foreach (var record in perfLog.ListByVendorAsync(vendor.Id, skip: 0, take: 20, ct))
{
    // chronological order; oldest first
}
```

Idempotent on duplicate `VendorPerformanceRecordId` — the log dedupes silently.

## Taxonomy-backed specialties (W#18 Phase 6)

`Sunfish.Vendor.Specialties@1.0.0` (charter at [`icm/00_intake/output/sunfish-vendor-specialties-v1-charter-2026-04-30.md`](../../../icm/00_intake/output/sunfish-vendor-specialties-v1-charter-2026-04-30.md)) ships 11 root anchors (preserving every legacy `VendorSpecialty` enum value 1:1) + 19 sub-specialty children. Civilian deployments may extend with locally-scoped sub-categories (e.g. a custom "Pool Service" specialty) but cannot alter the Sunfish-shipped node set.

| Trade | Sub-specialties (W#18 Phase 6) |
|---|---|
| `plumbing` | water-heater, drain-cleaning, pipe-repair |
| `electrical` | panel, lighting, ev-charger |
| `hvac` | central, minisplit, duct |
| `landscaping` | tree-service, irrigation, snow-removal |
| `roofing` | shingle, flat-roof, gutter |
| `cleaning` | move-out, recurring, carpet, window |

## Audit emission (W#18 Phase 7) — 7 `AuditEventType`

| Event | Emitted by |
|---|---|
| `VendorCreated` | (Phase 8 follow-up will wire `CreateVendorAsync`) |
| `VendorMagicLinkIssued` / `VendorMagicLinkConsumed` | Phase 5 magic-link service (gated on W#20) |
| `VendorOnboardingStateChanged` | Operator-initiated state-machine transitions |
| `W9DocumentReceived` / `W9DocumentVerified` | Phase 4 W-9 service (gated on W#32) |
| `VendorActivated` | `OnboardingState → Active` transition |

`VendorAuditPayloadFactory` is the canonical body builder; bodies carry id pointers + actors + timestamps but **never TIN bytes**. TIN-decryption audit emission rides on the existing `BookkeeperAccess` / `TaxAdvisorAccess` events (W#32 substrate) when the field is read.

A reflection-based test (`NoFactoryBodyKey_LooksLikeATinField`) asserts no body key reads as `tin` / `ssn` / `ein` / `tax_id`.

## Deferred (W#18 Phases 4 + 5)

| Phase | Subject | Gate |
|---|---|---|
| 4 | `W9Document` + `EncryptedField` TIN | **W#32** (`EncryptedField` + `IFieldDecryptor` substrate) build complete |
| 5 | `VendorMagicLink` + onboarding flow + `providers-postmark` first email adapter | **W#20** Phase 2.1+ magic-link delivery contracts |

Both are explicit halt-conditions in the original W#18 hand-off; downstream consumers should treat the substrate as "ready for vendor management; W-9 + magic-link follow when prereqs land."

## See also

- [ADR 0058](../../../docs/adrs/0058-vendor-onboarding-posture.md) — Vendor onboarding posture
- [W#18 hand-off](../../../icm/_state/handoffs/property-vendor-onboarding-stage06-handoff.md)
- [`MIGRATION.md`](https://github.com/ctwoodwa/Sunfish/blob/main/packages/blocks-maintenance/MIGRATION.md) — v1.0 → v1.1 breaking changes
