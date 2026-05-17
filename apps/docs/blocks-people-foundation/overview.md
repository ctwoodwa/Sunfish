# blocks-people-foundation

The canonical **Party** substrate for Sunfish. Every other block that talks about humans or organizations — leases, AR, AP, work orders, comms — attaches role-edges to a `Party` defined here, instead of carrying its own customer / tenant / vendor table.

## Why this block exists

Stage 02 §4 calls out the foundational identity model: a single `Party` identity is referenced by many downstream records via role-edges, so the same human can be "tenant on lease #42" + "vendor on bill #87" + "employee on payroll" without their email, phone, and display name being duplicated three times. Without this block, every block has to define its own party-shaped table and keep them in sync — which is exactly the integration nightmare local-first software is supposed to avoid.

## Domain shape

```text
Party (1) ── (N) PartyRole (N) ─── (1) any consumer-cluster record
                                       (Invoice, Lease, WorkOrder, Bill, …)

Party (1) ── (N) EmailAddress
Party (1) ── (N) PhoneNumber
Party (1) ── (N) PartyAddress
```

`Party` is the **canonical identity**; contact info lives in three append-only sibling collections (`EmailAddress`, `PhoneNumber`, `PartyAddress`); `PartyRole` is the edge model joining a party to a record in another cluster (and vice versa).

## Entities

| Entity | Aggregate? | Shape | Append-only? |
|---|---|---|---|
| **`Party`** | yes | `PartyId`, `TenantId`, `Kind` (Person/Organization), `DisplayName`, plus person-shaped (`GivenName`, `DateOfBirth`, …) and organization-shaped (`LegalEntityType`, `TaxId`, `ParentOrgId`) fields collapsed onto one record. Standard CRDT envelope. | no — mutated in place, version-bumped on every write. Tombstoned via `DeletedAt`. |
| **`EmailAddress`** | sibling collection | `EmailAddressId`, `PartyId`, `Address` (RFC 5322), `Label`, `IsPrimary`, `IsValidated`, `ValidatedAt?`, `OptedOutAt?`, `ReplacedAt?` | **yes**. Updates = INSERT new row + set old row's `ReplacedAt` (CRDT §4). |
| **`PhoneNumber`** | sibling collection | `PhoneNumberId`, `PartyId`, `E164`, `Extension?`, `Label`, `IsPrimary`, `IsMobile`, `SmsOptedOutAt?`, `ReplacedAt?` | yes |
| **`PartyAddress`** | sibling collection | `PartyAddressId`, `PartyId`, `Address` (flat value object), `Label`, `IsPrimary`, `ValidFrom?`, `ValidTo?`, `ReplacedAt?` | yes |
| **`PartyRole`** | edge | `PartyRoleId`, `PartyId`, `RoleName`, `RoleRecordId` (opaque string pointing into consumer cluster), `StartedAt`, `EndedAt?`, `EndedReason?` | append-only with tombstone. Detach is UPDATE `EndedAt`; re-attach inserts a new row. |

### Canonical role codes

Defined in `Models/PartyRoleName.cs`:

- `customer`
- `tenant`
- `vendor`
- `contractor`
- `employee`

Open-set per CRDT §5: future hand-offs add codes (e.g. `landlord`, `lead`, `applicant`) *additively* — never rename existing ones. Unknown shape-valid codes pass validation; `PartyRoleName.IsKnown(...)` is informational, not a gate.

## Services

```csharp
public interface IPartyReadModel { … }      // 11 methods, active-rows-only
public interface IPartyWriteService { … }   // 9 methods, validator-throwing
```

One `InMemoryPartyRepository` implements both. Persistence-backed implementations (SQLite, Postgres) land in a follow-on substrate hand-off and shadow the in-memory binding.

Every write method takes `actor: PartyId` explicitly rather than reaching into a context. The platform's `IActorPrincipalResolver` (in `foundation-ship-common`) resolves ActorId → Principal, but the Principal → PartyId mapping isn't ratified yet — taking the actor as a parameter keeps that decision at the call site.

## Validation

`Validation/PartyValidator` + 3 contact-info validators + `PartyRoleValidator` enforce:

- Person parties require `GivenName` or `DisplayName`.
- Organization parties require `DisplayName` or `LegalName`.
- `ParentOrgId` only valid on organizations.
- Email is RFC 5322 (bare addr-spec, no display-name form).
- Phone is strict E.164 (`^\+[1-9]\d{1,14}$`).
- Address country is ISO 3166-1 alpha-2.
- Role name is lowercase kebab-case, ≤64 chars.

Validators return `ValidationResult` rather than throwing — write services translate failures to `PartyValidationException` so call sites can branch on `result.IsValid` when they want non-throwing semantics.

## Events

`InMemoryPartyRepository` emits 7 canonical event types via `Sunfish.Foundation.Events.IDomainEventPublisher`:

| Event type | Triggered by |
|---|---|
| `People.PartyCreated` | `CreateAsync` |
| `People.PartyDeleted` | `DeleteAsync` |
| `People.RoleAttached` | `AttachRoleAsync` (new active row; idempotent re-attach emits nothing) |
| `People.RoleDetached` | `DetachRoleAsync` |
| `People.EmailAddressAdded` | `AddEmailAsync` + `SupersedeEmailAsync` (new row) |
| `People.PhoneNumberAdded` | `AddPhoneAsync` |
| `People.AddressAdded` | `AddAddressAsync` |

Idempotency keys follow `{action}:{entityId}` — same (tenant, key) tuple collapses to one row at the `IDomainEventStore` level per `cross-cluster-event-bus-design.md` §4.

A `PartyUpdated` event is deliberately omitted in v1 — distinguishing "meaningful change" from "no-op" needs a delta protocol that's out of scope here. Additive when needed.

## ERPNext migration importer

`Migration/IErpnextPartyImporter` upserts ERPNext `Customer` + `Supplier` doctypes into the canonical Party substrate. Idempotent on `(Name, Modified)`: re-running the same import is a no-op (`Skipped`); a newer `Modified` key triggers an `Updated`.

The importer tags every imported Party with two entries on `Party.Tags`:

- `externalRef:erpnext:customer:{Name}` or `externalRef:erpnext:supplier:{Name}` — the FK
- `erpnextModified:{Modified}` — the version key

`CustomerType` = `"Company"` → `PartyKind.Organization`; anything else (including `"Individual"` and null) → `PartyKind.Person`. Email + phone rows are added when the source carries shape-valid values; malformed values are dropped silently rather than failing the whole party import.

ERPNext namespaces are doctype-scoped — a `Customer.name = "ENT-001"` and a `Supplier.name = "ENT-001"` produce two **separate** Parties (one with the `customer` role, one with the `vendor` role). If you intend a single party that's both, run the second importer with the matching first-party's id and attach the role directly via `IPartyWriteService.AttachRoleAsync`.

## DI

```csharp
services.AddBlocksPeopleFoundation();
```

Registers one `InMemoryPartyRepository` singleton, exposes it through both `IPartyReadModel` and `IPartyWriteService` bindings, registers `ErpnextPartyImporter`, and `TryAddSingleton<IDomainEventPublisher, NoopDomainEventPublisher>` so hosts that wire a real publisher upstream are unaffected.

## PII posture

`Party.TaxId` and `Party.DateOfBirth` ship **UNENCRYPTED** in v1, marked with `// TODO: encrypt-at-rest per W#37/ADR-0068`. The encryption pass wraps them at the persistence boundary once Stronghold / OS-keychain support lands and ADR 0068 reaches `Accepted`. Public API surface does not change.

## What's not here (intentional deferral)

| Concern | Where it ships |
|---|---|
| Persistence backend (SQLite / Postgres) | follow-on substrate hand-off; in-memory is plenty for the v1 Anchor seed |
| History queries (`includeReplaced` / `includeEnded`) | follow-on history-projection workstream once persistence lands |
| `PartyUpdated` event | additive; needs a delta protocol that's out of scope |
| Encryption-at-rest for PII | follow-on workstream paired with W#37 / ADR 0068 acceptance |
| `IPartyContext.GetCurrentPartyId()` canonical | when the platform-wide actor→party mapping ratifies (after foundation-ship-common evolves) |
| Pre-PR-1 `Sunfish.Blocks.Leases.Models.PartyId` retrofit | separate retrofit hand-off (XO directive `xo-directive-2026-05-17T02-40Z-leases-party-retrofit-ready.md`) |
