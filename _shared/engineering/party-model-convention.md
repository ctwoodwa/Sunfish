# Party-Model Convention

**Status:** Canonical convention. `blocks-people-*` owns the `Party`
entity; `blocks-financial-*`, `blocks-property-*`, `blocks-work-*`, and
`blocks-docs-*` reference it by ID. Stage 06 hand-offs cite this
document by section.
**Date:** 2026-05-16
**Authority:** ADR 0088 — *Anchor as All-In-One Local-First Runtime* §1;
`blocks-people-schema-design.md` §3.1, §6.
**Audience:** XO authoring hand-offs; cob authoring Stage 06; dev /
dev-win reading cluster designs.

---

## 0. Context

`blocks-people-schema-design.md` §3 establishes `Party` as the
architectural anchor for the people cluster, derived clean-room from
Apache OFBiz's `party` module (Apache 2.0, borrow-with-attribution):

> Every actor in `blocks-people-*` (employee, contact, customer, tenant,
> lead, contractor) shares a single base abstraction: `Party`. A Party
> is a person or organization with a stable identity; the role-specific
> entities (`Employee`, `Customer`, `Tenant`, etc.) are role records
> *referencing* a Party. This pattern is derived from Apache OFBiz's
> `party` module (Apache 2.0, borrow-with-attribution) and is the single
> most leveraged simplification in the cluster.

Three other clusters reference Party:

- **`blocks-financial-*`** — `Customer` references a Party + role
  `customer` + AR account + default payment terms. `Vendor` is the
  symmetric role (Party + role `vendor` + AP account). See
  `blocks-financial-schema-design.md` §3.5 (`Invoice.customerId`),
  §3.7 (`Bill.vendorId`).
- **`blocks-property-*`** — `Tenant` is a Party + role `tenant` + active
  lease link; `Lease.tenantPartyIds: ID<Party>[]` references Party
  directly (joint-and-several leases). See
  `blocks-people-schema-design.md` §3.5 (`Tenant`), §7.1 cross-cluster
  contract.
- **`blocks-work-*`** — `Contractor` is a Party + role `contractor` +
  agreement link; `WorkOrder.assignedToPartyId` and
  `Project.ownerPartyId` reference Party. See
  `blocks-work-schema-design.md` §2.4 (`WorkOrder`), §2.11
  (`Contractor`), §5.2 cross-cluster contract.

Without a unifying convention, each cluster duplicates name + email +
phone + address fields on its own role entity. That path leads to the
well-documented pathologies in `blocks-people-schema-design.md` §6
(duplicate identities, role-transition data copies, fragmented
privacy controls, structurally impossible deduplication).

This document elevates the Party-as-base pattern from a per-cluster
choice to a canonical convention that all four consuming clusters
inherit.

---

## 1. Why Party-as-base — the OFBiz pattern, restated

### One identity per actor; many roles

A real-world entity (a person, an organization) has one identity in the
system: one `Party` row. Around that identity, the system attaches:

- **PartyRole** rows linking the Party to one of N role-specific
  records.
- **Role records** (`Employee`, `Customer`, `Tenant`, `Contact`, `Lead`,
  `Contractor`, `Vendor`) carrying role-specific data: employee number
  + position; customer number + AR account; tenant number + lease.
- **PartyRelationship** rows capturing actor-to-actor edges (org
  employs person, person is contact-at org, person is emergency-contact-
  for person).

### What this gives us

1. **Same human, many roles.** Jane Doe is an Employee (her job), a
   Customer (her LLC buys our services), and an emergency contact for
   another Employee — three role records, one Party.
2. **Role transitions are state changes, not data copies.** Lead → Tenant
   promotes via a new Tenant row referencing the same Party; the Lead
   row stays for analytics.
3. **Deduplication is a single operation.** Merging two Party rows
   re-points all role records; no per-table fixups.
4. **Cascading privacy controls.** `doNotCall = true` on the Party row
   suppresses calls regardless of which role hat the contact would have
   triggered.
5. **Cross-cluster references are uniform.** Everything outside the
   people cluster points at `partyId`. The financial cluster doesn't
   need to know "is this Party a Customer or a Vendor?" — it knows the
   Party ID; the role is resolved when needed.

### What we accept

- **Unavoidable joins.** Rendering an `Employee` requires Employee +
  Party + (sometimes) PartyAddress / EmailAddress / PhoneNumber. SQLite
  handles this comfortably at our scale (≤ 50k Parties per tenant).
- **Sub-entity row volume.** Contact methods are sub-entity rows, not
  denormalized strings on the Party. The benefit (CRDT-safe addition /
  removal of methods, `isPrimary` semantics, label categorization)
  outweighs the row count.
- **Service-layer indirection.** A `PartyService` (or equivalent
  per-language facade) wraps the join boilerplate; consumers see role
  records with hydrated Party data, not the raw two-table reality.

### What this is NOT

- **Not a class hierarchy.** Employee does not "extend" Party in the
  type system. Composition by foreign key only.
- **Not a permission model.** RBAC / actor-principal-resolver (per W#1
  / `IActorPrincipalResolver`) operates on identity at a different
  layer; `PartyRole` is a business-domain concept, not a security
  primitive.
- **Not optional.** Every actor in every cluster must have a Party.
  There is no "lightweight contact" shortcut.

---

## 2. The Party entity — canonical shape

Authoritative definition lives in `blocks-people-schema-design.md`
§3.1. Restated here for cross-document reference:

```ts
type PartyKind = "person" | "organization";

interface Party {
  // CRDT envelope (per crdt-friendly-schema-conventions.md §13)
  id: Id<Party>;                       // ULID
  tenantId: Id<Tenant>;
  createdAt: Date;
  createdBy: Id<Party>;
  updatedAt: Date;
  updatedBy: Id<Party>;
  deletedAt?: Date;
  deletedBy?: Id<Party>;
  deletedReason?: string;
  version: number;
  revisionVector: Record<string, number>;

  // Identity (per blocks-people-schema-design.md §3.1)
  kind: PartyKind;
  displayName: string;                 // canonical: "Doe, Jane" / "Acme Corp"
  legalName?: string;                  // formal legal name when different
  preferredName?: string;              // first-name basis ("Jane")

  // Person-only
  givenName?: string;
  familyName?: string;
  middleName?: string;
  suffix?: string;                     // Jr., Sr., III
  pronouns?: string;                   // free-text, respect user input
  dateOfBirth?: Date;                  // SENSITIVE — encrypt at rest

  // Org-only
  legalEntityType?: string;            // "LLC", "C-Corp", etc.
  taxId?: string;                      // EIN/SSN; ENCRYPT at rest; REDACT in UI by default
  parentOrgId?: Id<Party>;             // org hierarchy (self-ref)

  // Common contact surface (sub-entities; see §3 below)
  emails: EmailAddress[];
  phones: PhoneNumber[];
  addresses: PartyAddress[];
  webSites: string[];
  notes?: string;
  tags: string[];                      // free-form labels for segmentation
  preferredLanguage?: string;          // BCP-47 code

  // Privacy controls (cascade to all roles)
  doNotContact: boolean;               // global suppression
  doNotEmail: boolean;
  doNotCall: boolean;
  doNotSms: boolean;
}
```

### Validation (per `blocks-people-schema-design.md` §3.1)

- `kind = "person"` → `givenName` or `displayName` required.
- `kind = "organization"` → `displayName` or `legalName` required.
- At least one of `emails[]` / `phones[]` / `addresses[]` required for
  any Party in an active role record (relaxed for bulk-imported leads).
- `taxId` MUST be encrypted at rest (Stronghold/DPAPI per W#60 P4 PR1).
- `dateOfBirth` MUST be encrypted at rest.

### Why the contact methods are sub-entities, not columns

`emails`, `phones`, and `addresses` are **arrays of sub-entity rows**,
not denormalized columns on the Party. Each sub-entity is its own table:

```ts
interface EmailAddress {
  id: Id<EmailAddress>;
  partyId: Id<Party>;
  address: string;                     // RFC 5322
  label?: "work" | "personal" | "billing" | "other";
  isPrimary: boolean;
  isValidated: boolean;
  validatedAt?: Date;
}

interface PhoneNumber {
  id: Id<PhoneNumber>;
  partyId: Id<Party>;
  e164: string;                        // ITU-T E.164 canonical
  label?: "mobile" | "work" | "home" | "fax" | "other";
  isPrimary: boolean;
  smsCapable: boolean;
}

interface PartyAddress {
  id: Id<PartyAddress>;
  partyId: Id<Party>;
  address: Address;                    // { line1, line2?, city, region, postalCode, country }
  label?: "primary" | "mailing" | "billing" | "shipping" | "physical";
  isPrimary: boolean;
  validFrom?: Date;
  validTo?: Date;
}
```

Reasons:

1. **CRDT semantics need it.** Adding / removing contact methods is
   inherently a list operation. Per §4 of crdt-friendly-schema-conventions,
   append-only sub-collections survive CRDT merge cleanly.
2. **Multiple values per kind.** A Party may have a work email + a
   personal email + a billing email; `isPrimary` semantics need a
   first-class column.
3. **Label categorization.** Filtering "give me all the Parties with a
   `billing` email" is a simple sub-entity query; a comma-separated
   string column wouldn't support that.
4. **Validation per row.** `isValidated: boolean` on EmailAddress lets
   the system track which email passed verification; same logic on
   PhoneNumber for SMS-capable verification.

---

## 3. PartyRole + role-specific extension entities

### PartyRole — the role registry

```ts
type RoleKind =
  | "employee" | "contact" | "customer" | "tenant" | "vendor"
  | "lead" | "applicant" | "contractor" | "owner" | "user";

interface PartyRole {
  id: Id<PartyRole>;
  tenantId: Id<Tenant>;
  partyId: Id<Party>;
  roleKind: RoleKind;
  roleRecordId: string;                // Id<Employee> | Id<Customer> | ...
  startedAt: Date;
  endedAt?: Date;                      // null = active
  endedReason?: string;
}
```

- **Validation:** `(partyId, roleKind, roleRecordId)` unique. A Party
  may hold multiple roles simultaneously.
- **Workflow:** `startedAt` set on role creation; `endedAt` set when the
  role record transitions to terminal state. UI uses this to show "all
  hats this person wears."

### Role record entities — what each cluster owns

Each consuming cluster owns its own role record entity. The role
record:

1. **References Party** by `partyId`.
2. **Carries role-specific data** (employee number, AR account, lease,
   etc.).
3. **Does NOT duplicate** name / email / phone / address fields.
4. **Renders** by joining its row + the Party row + sub-entities at
   read time.

| Cluster | Role record | Carries | Owns lifecycle of |
|---|---|---|---|
| `blocks-people-*` | `Employee` | `employeeNumber`, `positionId`, `compensation`, `hireDate`, `terminationDate` | Employee status (active, terminated, on-leave) |
| `blocks-people-*` | `Contact` | `primaryOrgPartyId`, `titleAtOrg`, `preferredContactMethod`, `ownedByEmployeeId` | Contact status (active, inactive) |
| `blocks-people-*` | `Lead` | `leadNumber`, `statusCode`, `score`, `convertedTo*` | Lead funnel state machine |
| `blocks-financial-*` | `Customer` | `customerNumber`, `arAccountId`, `defaultPaymentTermsId`, `creditLimit`, `taxExempt`, `taxExemptionNumber`, `ownedByEmployeeId` | Customer status (prospect, active, on-hold, former, blacklisted) |
| `blocks-financial-*` | `Vendor` (symmetric to Customer; placement TBD per `blocks-people-schema-design.md` §9 Q9) | `vendorNumber`, `apAccountId`, `defaultPaymentTermsId`, `taxId` (for 1099 reporting), `is1099Eligible` | Vendor status |
| `blocks-property-*` (cluster-internal) | `Tenant` (canonical Tenant lives in `blocks-people-*` §3.5) | `tenantNumber`, `status`, `currentLeaseId`, `applicationLeadId`, `moveInAt`, `moveOutAt`, `emergencyContactPartyId` | Tenancy status (applicant, approved, active, notice-given, former, evicted, deceased) |
| `blocks-work-*` | `Contractor` (sidecar to Party with role `contractor` + agreement) | `contractorAgreementId`, `insuranceVerifiedThrough`, `licenseNumbers`, `defaultHourlyRate` | Contractor status (active, inactive) |
| `blocks-docs-*` | (no role record; references Party + role from people) | — | — |

### Why the role record is a separate table, not a Party.subtype enum

Two reasons:

1. **Role records carry role-specific data.** Customer has `arAccountId`;
   Tenant has `currentLeaseId`; Employee has `positionId`. Sticking
   those on Party as nullable columns explodes column count and
   loses validity guarantees.
2. **Multi-tenancy of roles within one Party.** A Party can be a
   Customer (active) AND a Tenant (terminated) AND a Lead (converted)
   simultaneously. Each role has its own lifecycle. A single
   `Party.role: RoleKind` column couldn't represent the AND.

### Symmetric Customer + Vendor

`blocks-people-schema-design.md` §9 Q9 raises:

> Vendor / contractor entity placement. The cluster lists Contact
> (vendor-contact person) but not Vendor (the org). Vendor sits at the
> intersection of people (it's a Party) and financial (it has an AP
> account). Where does the Vendor role record live? Recommendation: in
> `blocks-people-*` as a parallel to Customer (Party + role=vendor +
> AP-account reference), with `Customer` and `Vendor` symmetric. To be
> confirmed in the `blocks-financial-*` schema design.

`blocks-financial-schema-design.md` §3.7 (`Bill`) confirms the
symmetric posture: a `Bill.vendorId` references a Vendor role record,
which itself references a Party. The cluster naming question (does the
`Vendor` row live in `blocks-people-*` or `blocks-financial-*`?) is
unresolved — see §6 Open Questions Q1 below.

---

## 4. Cross-cluster relationships — how the four consuming clusters reference Party

### General pattern

Every cross-cluster reference is a **string ID** pointing at a Party
(or at a role record, which itself points at a Party). The reference
side does NOT own Party data; it consumes it through a typed read-only
query interface exposed by `blocks-people-*`.

### `blocks-financial-*` ↔ `blocks-people-*`

Per `blocks-people-schema-design.md` §7.2 + `blocks-financial-schema-
design.md` §3.5 + §3.7:

| Direction | Reference | Owner |
|---|---|---|
| financial → people | `Customer.partyId: Id<Party>` | People cluster owns Party + Customer role record |
| people → financial | `Customer.arAccountId: Id<Account>` | Financial cluster owns Account |
| people → financial | `Customer.defaultPaymentTermsId: Id<PaymentTerms>` | Financial cluster owns PaymentTerms |
| financial → people | `Bill.vendorId` → `Vendor.partyId` | Vendor role record (placement TBD per §6 Q1); Party in people cluster |
| Events | `OpportunityWon` (people → financial); `InvoiceIssued` (financial → people) | See cross-cluster-event-bus-design.md §3 |

### `blocks-property-*` ↔ `blocks-people-*`

Per `blocks-people-schema-design.md` §7.1:

| Direction | Reference | Owner |
|---|---|---|
| people → property | `Tenant.currentLeaseId: Id<Lease>` | Property cluster owns Lease |
| property → people | `Lease.tenantPartyIds: Id<Party>[]` (multi-tenant joint leases) | People cluster owns Party. Property cluster references Party IDs only |
| Events | `Property.LeaseExecuted` triggers `People.activateTenantOnLeaseExecution`; `People.TenancyEnded` triggers `blocks-property-*.closeTenancy` | See cross-cluster-event-bus-design.md §3 |

### `blocks-work-*` ↔ `blocks-people-*`

Per `blocks-people-schema-design.md` §7.3 + `blocks-work-schema-
design.md` §5.2:

| Direction | Reference | Owner |
|---|---|---|
| work → people | `WorkOrder.assignedToPartyId: Id<Party>` | People cluster owns Party |
| work → people | `WorkOrder.requestedByPartyId: Id<Party>` | People cluster owns Party |
| work → people | `Project.ownerPartyId: Id<Party>`, `Project.sponsorPartyId: Id<Party>` | People cluster owns Party |
| work → people | `Project.customerPartyId: Id<Party>` (for external-customer projects) | People cluster owns Party |
| work → people | `WorkOrder.contractorId: Id<Contractor>` → references a Party with role `contractor` | Contractor row lives in work cluster (per §3 above); Party in people cluster |
| people → work | `Activity.workOrderId: Id<WorkOrder>` (polymorphic anchor) | Activity in people cluster; WorkOrder ID is a foreign reference |
| Events | `EmployeeTerminated` triggers WorkOrder reassignment; `WorkOrderAssigned` creates an Activity row | See cross-cluster-event-bus-design.md §3 |

### `blocks-docs-*` ↔ `blocks-people-*`

Per `blocks-people-schema-design.md` §7.4 + `blocks-docs-schema-
design.md` §7.1:

| Direction | Reference | Owner |
|---|---|---|
| docs → people | `Document.ownerId: Id<Party>` (or `Id<Employee>` resolved via PartyRole) | People owns Party |
| docs → people | `SigningParty.principalId: Id<Party>` | People owns Party |
| docs → people | `PolicyAcknowledgment.employeeId: Id<Employee>` | People owns Employee + Party |
| docs → people | `DocumentPermission.principalId: Id<Party>` | People owns Party |
| people → docs | `OnboardingStep.policyDocumentId: Id<Document>` | Docs owns Document |
| Events | `People.EmployeeOnboardingStarted` triggers policy-acknowledgment enqueue; `Docs.PolicyAcknowledgmentRecorded` triggers OnboardingTaskAssignment completion | See cross-cluster-event-bus-design.md §3 |

### Cross-cluster read interface

Per `blocks-reports-schema-design.md` §9 and `blocks-people-schema-
design.md` §7.4:

> `blocks-people-*` exposes `IPeopleReportQueries` (read-only typed
> accessors) including `getPrincipalsByIds(ids)`,
> `vendorTinAndAddress(vendorId)`,
> `recipientEmailByPrincipal(principalId)`.

Each consuming cluster receives **only typed read accessors**, never raw
table access:

```ts
interface IPartyReadModel {
  getById(id: Id<Party>): Promise<Party | null>;
  getMany(ids: Id<Party>[]): Promise<Map<Id<Party>, Party>>;
  getDisplayName(id: Id<Party>): Promise<string | null>;
  getEmail(id: Id<Party>, label?: EmailLabel): Promise<string | null>;
  getPhone(id: Id<Party>, label?: PhoneLabel): Promise<string | null>;
  getPrimaryAddress(id: Id<Party>): Promise<Address | null>;
  findByExactEmail(email: string): Promise<Party[]>;
  findByExactPhoneE164(e164: string): Promise<Party[]>;
  // For 1099 reporting:
  getTaxIdAndAddress(id: Id<Party>): Promise<{ taxId: string; address: Address } | null>;
}
```

The interface is versioned with `[Obsolete]` discipline. Any breaking
change follows the `sunfish-api-change` ICM pipeline variant.

---

## 5. Deduplication

### The problem

`blocks-people-schema-design.md` §9 Q1 raises the question explicitly:

> Party de-duplication strategy. When the system ingests a new Lead and
> the email matches an existing Party, do we (a) auto-link, (b) flag
> for manual review, (c) create a duplicate and surface in a dedup
> queue?

Under CRDT, duplicate detection is **strictly harder** than in a
centralized system: two replicas may independently create Party rows
for the same person and not realize the conflict until sync.

### Recommended strategy — manual review for v1

Per `blocks-people-schema-design.md` §9 Q1 recommendation: **flag for
manual review** (option (b)) for v1. Auto-merge deferred to Phase 4.

The mechanism:

1. **At creation time**, the persistence layer runs a fuzzy-match
   check against existing Party rows on the same tenant:
   - **Email exact match** (case-insensitive, post-RFC-5322
     normalization) — high confidence.
   - **Phone E.164 exact match** — high confidence.
   - **Display name + DOB match** — medium confidence (some false
     positives — common names + same birth year).
   - **Display name + primary address match** — medium confidence.
2. **High-confidence matches** surface a "possible duplicate" UI prompt:
   "We found an existing Party with this email — link to it instead?"
3. **Medium-confidence matches** create the new Party but enqueue a
   `PartyDeduplicationCandidate` row for admin review in a dedup queue
   UI.
4. **No match** creates the Party normally.

### Merge mechanics — when admin confirms a duplicate

```ts
async function mergeParty(survivingId: Id<Party>, mergedId: Id<Party>): Promise<void> {
  // 1. Validate same tenant; merge cross-tenant is forbidden (§7 below).
  // 2. All PartyRole rows on mergedId re-pointed to survivingId.
  // 3. All cross-cluster references to mergedId re-pointed:
  //    - Customer.partyId, Tenant.partyId, Employee.partyId,
  //      Lead.partyId, Contractor.partyId, etc.
  //    - WorkOrder.{assignedTo,requestedBy}PartyId
  //    - Project.{owner,sponsor,customer}PartyId
  //    - Document.ownerId, SigningParty.principalId,
  //      DocumentPermission.principalId, PolicyAcknowledgment.employeeId
  //    - Lease.tenantPartyIds (array element replacement)
  // 4. Sub-entities (EmailAddress, PhoneNumber, PartyAddress) on mergedId
  //    re-pointed to survivingId; duplicate exact-match rows deduped.
  // 5. mergedId tombstoned with `deletedReason = 'merged-into:{survivingId}'`.
  // 6. Emit People.PartyMerged event with both IDs in payload.
  // 7. Consumers receive the event and validate their own references
  //    are consistent.
}
```

The merge MUST be atomic in SQLite (same transaction) on the operating
replica. Other replicas receive the merge as a sequence of update +
tombstone ops via Loro; idempotency on the merge event (per
cross-cluster-event-bus-design.md §4) ensures consistency.

### Conflict edge cases

- **Concurrent merge from two replicas.** Both replicas try to merge
  Party A and B simultaneously (one merges A→B, the other merges
  B→A). The second to apply detects the merge-in-progress (`deletedAt`
  set on the target) and aborts; UI surfaces "this party was already
  merged elsewhere; refreshing."
- **Edit-then-merge race.** Replica A merges Party X into Party Y;
  Replica B was editing X concurrently. After sync, B's edits are
  preserved on the tombstone of X; UI surfaces "this party was merged
  into Y; your edits are preserved on the historical record."

These edge cases are rare; the canonical handling pattern is "preserve
all data + surface conflict in UI" rather than "silently lose data."

### Auto-merge — Phase 4 / deferred

Auto-merge (without manual review) requires:

- High-precision matching rules (false-positive rate < 0.1%).
- Reversibility (any auto-merge must be undoable).
- User opt-in per tenant.

None of these are present in Phase 1. Deferred per `blocks-people-
schema-design.md` §9 Q1 recommendation.

---

## 6. Multi-tenant isolation

### Rule

Every `Party` row carries `tenantId`. **A Party never crosses tenant
boundaries.** Per §14 of crdt-friendly-schema-conventions, the kernel-
sync transport refuses to apply a Party op whose tenant doesn't match
the local replica's tenant context.

### What this means in practice

- The same human (e.g., Chris Wood) appearing as a contact in **two
  different tenants** results in **two separate Party rows**, one per
  tenant. The two rows are independent; they don't share contact
  methods, don't share role records, don't dedupe across tenants.
- A Lead imported from a public source (Zillow, e.g.) into tenant A
  doesn't ever surface as a Party in tenant B.
- Cross-tenant data movement (e.g., a property-management LLC reorgs
  and transfers a portfolio to a sister LLC) requires an explicit
  export-and-reimport flow. The Stage 06 hand-off addresses this when
  the migration importer extends beyond ERPNext exports.

### Why this is the right rule under Loro

The CRDT layer is configured per-tenant: each tenant has its own Loro
document. Cross-tenant ops would require a cross-tenant document and
violate the isolation guarantee. The kernel-security envelope keys are
also per-tenant; cross-tenant Party rows would be unencryptable under
the existing W#37 / ADR 0068 (Proposed) scheme.

### Connection to W#37 tenant security policy

W#37 / ADR 0068 (Proposed) establishes per-tenant key isolation. Party
PII fields (`taxId`, `dateOfBirth`, `Employee.ssnEncrypted`,
`Employee.bankAccountEncrypted`) are encrypted-at-rest using tenant-
scoped envelope keys per W#60 P4 PR1 Stronghold/DPAPI integration.
This rule + the tenant-scoped envelope keys give the full isolation
story.

---

## 7. Privacy / PII

### Field classification

Party fields by sensitivity classification:

| Field | Sensitivity | Treatment |
|---|---|---|
| `displayName`, `givenName`, `familyName` | Public-by-business-use | Stored plain; visible in UI per role; logged in audit |
| `preferredName`, `pronouns`, `middleName`, `suffix` | Public-by-business-use | Same |
| `dateOfBirth` | **PII** | **Encrypted at rest** (Stronghold/DPAPI); redacted in UI by default; full value only for users with explicit "view DOB" permission |
| `taxId` (EIN / SSN) | **PII** | **Encrypted at rest**; redacted in UI (show last 4 only); full value behind explicit "view taxId" permission + audit-logged on view |
| `legalName` | Public-by-business-use | Stored plain |
| `legalEntityType` | Public-by-business-use | Stored plain |
| `notes` | Variable | **PHI / counsel-sensitive content forbidden** — UI warns user not to enter health/legal info |
| `emails[].address` | PII | Stored plain; suppress per `doNotEmail` |
| `phones[].e164` | PII | Stored plain; suppress per `doNotCall`/`doNotSms` |
| `addresses[]` | PII | Stored plain; suppress per `doNotContact` |
| Tags | Public-by-business-use | Stored plain |
| Communication preferences (`doNotEmail` etc.) | Operational | Stored plain |

Per `blocks-people-schema-design.md` §3.1 validation: "`taxId` MUST be
encrypted at rest (Stronghold/DPAPI per W#60 P4 PR1)."

Per §9 Q7:

> PII encryption scope. Schema notes call out `Party.taxId`,
> `Employee.ssnEncrypted`, `Employee.bankAccountEncrypted` as
> encrypted-at-rest. Does Stronghold/DPAPI (W#60 P4 PR1) cover all of
> these, or do we need a separate `IFieldEncryption` substrate?
> Recommendation: align with W#60 P4 PR1 outcome; if Stronghold covers
> it, use it; otherwise raise a new ADR.

Same recommendation stands for this convention: align with W#60 P4 PR1
outcome.

### Encryption at rest — mechanics

- **Per-tenant envelope key** managed by `kernel-security`.
- **Encrypted columns**: stored as ciphertext + key-ref. SQLite sees
  bytes; decryption happens at the persistence-layer read boundary
  via `kernel-security.decryptField()`.
- **Search**: full-text search over encrypted columns is **not
  supported**; equality match works only via deterministic encryption
  (which leaks frequency) and is forbidden for PII fields. UI search
  for "find Party by SSN last-4" is a separate prefix-encrypted index
  (Phase 3 if needed).
- **Crypto-shred**: per `blocks-docs-schema-design.md` §6.4 and §2 of
  crdt-friendly-schema-conventions, destroying the per-tenant envelope
  key renders all encrypted columns inert. The Party row remains in
  SQLite but `taxId` etc. are unrecoverable ciphertext.

### Right-to-be-forgotten / regulatory erasure

When a Party requests erasure (GDPR / CCPA / equivalent):

1. **Party row tombstoned** + `deletedReason = 'right-to-be-forgotten'`.
2. **PII fields crypto-shredded** at the per-row level (extension of
   tenant-scoped envelope key to per-Party-row key — see §10 Q4 below).
3. **Sub-entities tombstoned** (EmailAddress, PhoneNumber,
   PartyAddress).
4. **Role records preserved** (Customer, Employee, etc.) but with
   their PII fields nulled / redacted; business records (invoices,
   leases) referenced by ID remain intact for accounting / regulatory
   audit retention.
5. **Audit event emitted** (`People.PartyErasureRecorded`) so all
   replicas converge on the erasure.

This is the canonical "logical erasure" pattern under CRDT: the row
stays for referential integrity, but the PII is gone.

### What audit visibility looks like

A Customer's `Invoice` references `customerId = X`. After X's erasure:

- `customerId` still resolves to the (tombstoned) Party row.
- The UI shows "[REDACTED PER GDPR]" instead of the name.
- The Invoice itself stays intact; the AR ledger doesn't lose history.

This is the right behavior. Erasure must NOT discard business records;
it MUST suppress identification.

---

## 8. Examples — same person, different roles, different clusters

### Example A — Jane Doe, Employee + Customer + Tenant + Emergency Contact

```
Party (kind=person, id=PARTY-JANE-A4-001)
  displayName: "Doe, Jane"
  givenName: "Jane"
  familyName: "Doe"
  emails: [
    { address: "jane@example.com", label: "personal", isPrimary: true },
    { address: "jane.doe@chrisllc.com", label: "work", isPrimary: false },
  ]
  phones: [
    { e164: "+15555550100", label: "mobile", isPrimary: true },
  ]
  addresses: [
    { line1: "123 Main St", city: "Boise", region: "ID", postalCode: "83701", country: "US",
      label: "primary", isPrimary: true },
  ]

PartyRole rows:
  (PARTY-JANE-A4-001, "employee",  EMPLOYEE-A4-007)    -- Jane is on payroll
  (PARTY-JANE-A4-001, "customer",  CUSTOMER-A4-019)    -- Jane's LLC is a customer
  (PARTY-JANE-A4-001, "tenant",    TENANT-A4-034)      -- Jane rents Unit 5B from us
  (PARTY-JANE-A4-001, "contact",   CONTACT-A4-152)     -- Jane is Bob's emergency contact

Employee (id=EMPLOYEE-A4-007):
  partyId: PARTY-JANE-A4-001
  employeeNumber: "EMP-0007"
  positionId: POS-MAINTENANCE-LEAD
  compensation: { ... }
  ssnEncrypted: <ciphertext>
  bankAccountEncrypted: <ciphertext>
  hireDate: 2024-03-15

Customer (id=CUSTOMER-A4-019):
  partyId: PARTY-JANE-A4-001
  customerNumber: "CUST-0019"
  status: "active"
  arAccountId: ACCT-1100-AR
  defaultPaymentTermsId: TERMS-NET30
  creditLimit: { amount: 500000n, currency: "USD" }  -- $5,000.00

Tenant (id=TENANT-A4-034):
  partyId: PARTY-JANE-A4-001
  tenantNumber: "TEN-0034"
  status: "active"
  currentLeaseId: LEASE-PROP-WHITNEY-UNIT-5B
  moveInAt: 2025-06-01

Contact (id=CONTACT-A4-152):
  partyId: PARTY-JANE-A4-001
  primaryOrgPartyId: null            -- Jane in personal capacity
  titleAtOrg: null
  preferredContactMethod: "phone"

PartyRelationship rows:
  (fromPartyId=PARTY-BOB-A4-002, toPartyId=PARTY-JANE-A4-001,
   kind="emergency-contact-for", startedAt=2024-04-01)
```

What's notable:

- **One name spelling** for "Doe, Jane" across all four role records.
- **One email + phone + address** — updating Jane's mobile number
  updates it everywhere.
- **`doNotEmail = true` cascades** to all four roles: she stops
  receiving emails as a Customer AND as a Tenant AND for emergency-
  contact uses simultaneously.
- **Cross-cluster references** are all `partyId`:
  - `Invoice.customerId` resolves to CUSTOMER-A4-019 → PARTY-JANE-A4-001.
  - `Lease.tenantPartyIds` includes PARTY-JANE-A4-001 directly.
  - `WorkOrder.assignedToPartyId` (if Jane is the maintenance lead on
    a WO) is PARTY-JANE-A4-001 directly (via Employee resolution).
- **Termination of Employment** doesn't affect Tenant / Customer / Contact
  status: `PartyRole(employee, EMPLOYEE-A4-007).endedAt` is set; the
  other three rows are untouched.

### Example B — Acme HVAC LLC, Vendor + Contractor

```
Party (kind=organization, id=PARTY-ACME-A4-100)
  displayName: "Acme HVAC LLC"
  legalName: "Acme HVAC, LLC"
  legalEntityType: "LLC"
  taxId: <ciphertext>                 -- EIN for 1099 reporting
  emails: [{ address: "billing@acmehvac.com", label: "billing", isPrimary: true }]
  phones: [{ e164: "+15555551200", label: "work", isPrimary: true }]
  addresses: [{ line1: "5500 Industrial Way", ... }]

PartyRole rows:
  (PARTY-ACME-A4-100, "vendor",     VENDOR-A4-021)        -- Acme is in our AP system
  (PARTY-ACME-A4-100, "contractor", CONTRACTOR-A4-008)    -- Acme does work for us

Vendor (id=VENDOR-A4-021):
  partyId: PARTY-ACME-A4-100
  vendorNumber: "VEN-0021"
  status: "active"
  apAccountId: ACCT-2100-AP
  defaultPaymentTermsId: TERMS-NET15
  is1099Eligible: true

Contractor (id=CONTRACTOR-A4-008):
  partyId: PARTY-ACME-A4-100
  contractorAgreementId: AGR-CONTRACTOR-ACME-2026
  insuranceVerifiedThrough: 2026-12-31
  licenseNumbers: ["HVAC-12345"]
  defaultHourlyRate: { amount: 12500n, currency: "USD" }  -- $125.00/hr
```

What's notable:

- **One legal entity** — same EIN, same address, same billing email.
- **Two role records** in two clusters: Vendor in `blocks-financial-*` (or
  `blocks-people-*` — see §6 Q1); Contractor in `blocks-work-*`.
- **`Bill.vendorId = VENDOR-A4-021`** resolves to PARTY-ACME-A4-100 for
  remittance address + EIN.
- **`WorkOrder.contractorId = CONTRACTOR-A4-008`** resolves to the same
  PARTY-ACME-A4-100 for the work-side context.
- **1099 generation** (per `blocks-reports-schema-design.md` §8.4) reads
  Vendor.is1099Eligible + Party.taxId + Party.address + the year's
  payments to generate the 1099-NEC.

### Example C — Lead → Tenant promotion

The transition (`blocks-people-schema-design.md` §5.4):

```
Initial state:
  Party (PARTY-NEWHIRE-A4-050)        kind=person, displayName="Smith, John"
  Lead  (LEAD-A4-088)                 statusCode="qualified",
                                       interest="2BR apartment downtown",
                                       partyId=PARTY-NEWHIRE-A4-050
  PartyRole (PARTY-NEWHIRE-A4-050, "lead", LEAD-A4-088)

After submitTenantApplication():
  Party (PARTY-NEWHIRE-A4-050)        unchanged
  Lead  (LEAD-A4-088)                 unchanged
  Tenant (TENANT-A4-097)              status="applicant",
                                       partyId=PARTY-NEWHIRE-A4-050,
                                       applicationLeadId=LEAD-A4-088
  PartyRole (PARTY-NEWHIRE-A4-050, "lead",      LEAD-A4-088)    -- still active
  PartyRole (PARTY-NEWHIRE-A4-050, "applicant", TENANT-A4-097)  -- new

After activateTenantOnLeaseExecution():
  Party (PARTY-NEWHIRE-A4-050)        unchanged
  Lead  (LEAD-A4-088)                 statusCode="converted",
                                       convertedToTenantId=TENANT-A4-097
  Tenant (TENANT-A4-097)              status="active",
                                       currentLeaseId=LEASE-...,
                                       moveInAt=...
  PartyRole (lead, LEAD-A4-088)        endedAt=now()              -- ended
  PartyRole (applicant, TENANT-A4-097) endedAt=now()              -- ended
  PartyRole (tenant, TENANT-A4-097)    startedAt=moveInAt          -- new active role
```

The Party row never moves; never copies. Only role records and
PartyRole rows change.

---

## 9. Discipline summary — how to apply this convention

### When authoring a new Stage 02 design that touches actors

1. **Reference Party by ID, not by name fields.** Any entity that
   represents a person or org carries `partyId: Id<Party>`, not
   duplicated name/email/phone.
2. **Decide the role record placement** — does the new role record live
   in `blocks-people-*` (the canonical home for general-purpose roles)
   or in the cluster that owns the role's lifecycle (e.g., Contractor
   in `blocks-work-*`)? Document the choice.
3. **Add a PartyRole entry** for the new role kind. Update §3 of this
   document's role-kind list if introducing a new `RoleKind`.
4. **Express cross-cluster references as `partyId`**, not as role-record
   IDs, when the consumer just needs identity (name / email / phone /
   address). Use the role-record ID only when role-specific data is
   needed.
5. **Honor privacy controls.** Any UI / notification that touches a
   Party respects `doNotEmail`/`doNotCall`/`doNotSms`/`doNotContact`
   cascaded from the Party row.
6. **Encrypt PII fields at rest.** Per §7 above; align with W#60 P4 PR1
   Stronghold/DPAPI integration.
7. **Use `IPartyReadModel`** (or the cluster's exposed variant) for
   read access from other clusters. Never query Party tables directly
   from outside `blocks-people-*`.
8. **Handle deduplication** at write time per §5 fuzzy-match rules.

### When authoring a Stage 06 hand-off

1. **Cite this document** by section ("Party-Model Convention §3" etc.)
   in the hand-off's cross-cluster contract section.
2. **List all `partyId` references** the cluster reads, with which
   `IPartyReadModel` accessor is used.
3. **List all PartyRole entries** the cluster maintains (if any).
4. **Note PII encryption** on new fields.
5. **Specify dedupe behavior** for the cluster's role-creation paths.

---

## 10. Open questions for CO / cob ratification

### Q1. Vendor role record placement

**Question:** Per `blocks-people-schema-design.md` §9 Q9 + this
convention §3, the Vendor role record can live in:
- (a) `blocks-people-*` as a parallel to Customer (Party + role=vendor
  + AP-account-reference), with Customer and Vendor symmetric.
- (b) `blocks-financial-*` alongside Bill / Payment as it's the
  cluster that consumes Vendor most.

**Recommendation:** (a). Symmetric Customer + Vendor in `blocks-
people-*`; both reference financial-cluster Account IDs. This keeps
"who is the party" in one cluster + "what financial relationship" in
another. Cleanest separation. To be ratified with `blocks-financial-*`
authors.

### Q2. Contractor role record placement

**Question:** Per `blocks-work-schema-design.md` §2.11 + §5.2, the
Contractor role is currently in `blocks-work-*`. Symmetric reasoning
to Q1 would put it in `blocks-people-*`. Should we move it?

**Recommendation:** Keep Contractor in `blocks-work-*`. Rationale:
the Contractor row carries `contractorAgreementId`, insurance
verification, license numbers, and default hourly rate — all
work-cluster concerns. Customer is in people because customer's
relationship is "billing party" + "lead-funnel target"; Contractor is
in work because the relationship is "operational counterparty for
projects." Asymmetric but justifiable.

### Q3. Multi-tenant lease — joint and several vs grouped tenants

**Question:** Per `blocks-people-schema-design.md` §9 Q2: When a Lease
has multiple Tenants (e.g., spouses), do they each transition through
states independently, or do we model a `TenantGroup` that transitions
atomically?

**Recommendation:** Per the original §9 Q2 recommendation —
independent Tenant rows + `PartyRelationship(kind=household-member-
of)` to express the linkage. Atomic-group treatment lives in the
Lease entity. This convention ratifies that choice.

### Q4. Per-row vs per-tenant encryption key for PII

**Question:** Per `blocks-docs-schema-design.md` §9 Q2 (parallel
question for docs crypto-shred): is the encryption envelope key
per-tenant or per-row?

**Recommendation:** Per-tenant for v1 (matches kernel-security current
posture). Per-row key (for fine-grained right-to-be-forgotten)
deferred to a follow-on intake. For right-to-be-forgotten in v1:
crypto-shred per-tenant is too coarse (would shred all parties in
that tenant); instead, the per-row PII columns are nulled on erasure
and an audit event recorded. Acceptable trade-off; per-row keys are
the Phase 3 upgrade path.

### Q5. PartyRelationship semantics under role transitions

**Question:** When a Lead is converted to a Tenant (per §8 Example C),
relationships of kind `referred-by` etc. stay on the Party. But when
two Parties merge (per §5), the `from/to` references in
`PartyRelationship` need updating. What about the same Party appearing
on both sides of a relationship after merge (`fromPartyId === toPartyId`)?

**Recommendation:** Merge detects self-referencing PartyRelationship
rows and tombstones them (`deletedReason = 'self-reference-after-
merge'`). Documented in the merge-mechanics pseudocode (§5).

### Q6. `IActorPrincipalResolver` boundary with PartyRole

**Question:** Per W#1 + the `IActorPrincipalResolver` ruling
(PR #675), `ActorId = base64url(PrincipalId)`. How does PartyRole
interact with ActorPrincipal? Is a Party the same as a Principal, or
a layer above?

**Recommendation:** PartyRole is a **business-domain concept**, not a
security primitive (per `blocks-people-schema-design.md` §6 explicit
statement). Principal is a security-tier concept owned by
`foundation-multitenancy` / W#1. A Party CAN have a `user` role
record that bridges to a Principal, but most Parties (customers,
tenants, contractors) have NO Principal — they're not system users.
The boundary is clean.

### Q7. Cross-tenant Party correspondence

**Question:** A property-management firm running Anchor for 4 LLCs
(per W#60). The same human (e.g., a frequent contractor) appears as a
Party in all 4 tenants. Currently they're 4 independent Party rows
with no cross-tenant linkage. Should there be?

**Recommendation:** Not for v1. Cross-tenant Party correspondence is a
Bridge-tier concern (ADR 0031) where the Bridge can see across all
tenants and surface "this contractor works for all 4 LLCs" in a
consolidated view. Light-tier Anchor stays strictly tenant-isolated.

### Q8. Background-check entity placement

**Question:** Per `blocks-people-schema-design.md` §9 Q8:

> Tenant background-check workflow. Tenant.status transitions through
> `applicant → approved → active`. Where does background-check
> ingestion sit?

**Recommendation:** Per the original §9 Q8 — separate entity
(`BackgroundCheck`) referencing Tenant, deferred to a follow-on intake
once vendor selection (TransUnion ResidentScreening etc.) is done.
This convention does not address it further; flagged for the future
intake.

### Q9. Audit-log scope for Party changes

**Question:** Per `blocks-people-schema-design.md` §9 Q10: Do we rely
on the universal CRDT/event log for Party changes, or a domain-level
audit table?

**Recommendation:** Universal event log per §4 of crdt-friendly-
schema-conventions + a `People.PartyUpdated` event in cross-cluster-
event-bus-design.md §3.3 (additive: not in the current catalog;
recommended for adding). High-stakes transitions (`PartyMerged`,
`PartyErasureRecorded`, `PartyDataExported`) emit explicit events
with full audit context.

---

## 11. References

- `blocks-people-schema-design.md` §3.1, §6, §7 — canonical Party entity
  + Party-model pattern + cross-cluster contracts.
- `blocks-financial-schema-design.md` §3.5 (Invoice.customerId), §3.7
  (Bill.vendorId), §3.9 (Payment.partyId) — financial cluster's Party
  references.
- `blocks-work-schema-design.md` §2.1 (Project.ownerPartyId), §2.4
  (WorkOrder.assignedToPartyId), §2.11 (Contractor), §5.2 — work
  cluster's Party references.
- `blocks-docs-schema-design.md` §3.5.3 (SigningParty.principalId),
  §7.1 — docs cluster's Party references.
- `blocks-reports-schema-design.md` §9 — `IPeopleReportQueries`
  interface.
- ADR 0088 §1 — cluster definition.
- W#37 / ADR 0068 (Proposed) — tenant security policy + per-tenant
  envelope keys.
- W#1 / `IActorPrincipalResolver` — Principal layer separate from
  Party layer.

Companion convention documents:

- `crdt-friendly-schema-conventions.md` — entity envelope, CRDT
  semantics, encryption-at-rest mechanics.
- `cross-cluster-event-bus-design.md` — events involving Party
  (PartyMerged, PartyErasureRecorded, TenantActivated, OpportunityWon,
  etc.).
- `foss-source-survey-anchor-domain.md` — Apache OFBiz `party` module
  attribution (Apache 2.0, borrow-with-attribution).

---

**End of canonical Party-model convention.**
