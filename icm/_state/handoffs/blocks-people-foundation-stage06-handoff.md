# Hand-off — `blocks-people-foundation` Party + PartyRole + IPartyReadModel (Phase 3 substrate, minimum viable foundation)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-16
**Status:** `ready-to-build`
**Workstream:** W#60 P4 — Path II native domain, people cluster (foundation slice)
**Spec source:**
- [`icm/02_architecture/blocks-people-schema-design.md`](../../02_architecture/blocks-people-schema-design.md) §3.1 (Party canonical), §3.5 (Tenant role record placement), §6 (Party-as-base pattern), §7 (cross-cluster contracts)
- [`_shared/engineering/party-model-convention.md`](../../../_shared/engineering/party-model-convention.md) §2 (Party shape), §3 (PartyRole), §4 (cross-cluster references), §6 (multi-tenant isolation), §7 (privacy/PII)
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) §1 (cluster grouping; `blocks-people-*` Phase 3 cluster); status `Proposed`, ratified by CO 2026-05-16
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~6–8h sunfish-PM (4 PRs + ~30–35 tests + docs + attribution)
**PR count:** 4 PRs
**Pre-merge council:** NOT required (substrate scope; mirrors the W#34/W#35/W#36/W#60-P4 substrate-only pattern from the ledger + AR hand-offs). Standard COB self-audit applies.
**Audit before build:**
```bash
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-people"
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Models/Party.cs
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ar/ 2>&1
```
Expected: nothing matching `blocks-people-*`; `blocks-leases/Models/Party.cs` exists (pre-convention deviation — DO NOT TOUCH in this hand-off); `blocks-financial-ar/` may or may not exist yet (sibling hand-off may have already shipped its local `IPartyReadModel` stub).

---

## Context

### What this hand-off is — the minimum viable Party foundation

`blocks-people-foundation` ships the **identity + role-registry slice** of the people cluster — the part that consuming clusters (`blocks-financial-ar`, `blocks-property-leases`, `blocks-work-*`, `blocks-docs-*`) reference today via local stub `IPartyReadModel` interfaces. The full people cluster (Employee + Compensation + Leave + Shift + Onboarding + Training + Lead/Opportunity/Campaign + PartyRelationship + dedup + portal) is **out of scope**; that lands across separate Phase 3 hand-offs.

This hand-off intentionally inverts the usual cluster-build sequencing: rather than wait for the full Phase 3 people-cluster design to settle before any people code can ship, it carves out the **single sub-surface every other cluster needs right now** — the canonical Party identity + role-attach machinery + the `IPartyReadModel` read interface — and ships it as a thin substrate first. Once it lands, every consumer's local `IPartyReadModel` stub becomes a one-line `using` import; once people-cluster Phase 3 lands the role-extension entities, the existing `blocks-people-foundation` API surface remains stable (additive only).

### Why this is the right slice now

1. **`blocks-financial-ar` already ships a local stub** of `IPartyReadModel` + `PartyId` + `InMemoryPartyReadModel` (see `blocks-financial-ar-stage06-handoff.md` PR 6, lines 1593-1617 of that hand-off). Per the AR hand-off Cited-symbol verification: "Stub interfaces for cross-cluster contracts not yet shipped — applied for `ITaxCalculationService`, `IPartyReadModel`, `IInvoiceEventPublisher`. Each ships locally; relocates when the canonical home lands; DI swap with no public surface change." Landing this foundation unblocks the relocation.
2. **`blocks-leases.Party` is a pre-convention deviation** (per `blocks-property-party-alignment-review.md` §2.11 + §6 H1). The retrofit can only proceed after the canonical Party lands. This hand-off is its predecessor; the retrofit itself is a SEPARATE hand-off (`blocks-property-leases-party-retrofit-stage06-handoff.md`) sequenced after PR 4 of this hand-off merges.
3. **The full `blocks-people-*` Phase 3 cluster takes weeks** (6–8 weeks per ADR 0088 Appendix B); the foundation slice takes ~1–2 days. The cost of *not* shipping the foundation now is that every Stage 06 hand-off touching Party (which is "every customer/tenant/vendor/employee-bearing cluster") ships its own local stub and accrues retrofit debt.
4. **The Party identity + role-registry surface is stable**. The full cluster adds *role-specific extension entities* (`Employee`, `Customer`, `Tenant`, etc.); those entities reference `Party.id` from their respective clusters. The canonical `Party` + `PartyRole` shape does not change as those extension entities land. So the foundation slice can ship before the rest of the cluster without risking a breaking redesign downstream.

### Cluster naming + placement

Per `party-model-convention.md` §3 + `blocks-people-schema-design.md` §3 + ADR 0088 §1: the canonical Party identity lives in the `blocks-people-*` cluster. This hand-off chooses the package name **`blocks-people-foundation`** (not `blocks-people` unprefixed, not `blocks-people-party`, not `blocks-people-core`) for three reasons:

1. **Parity with `blocks-financial-ledger`** — the financial cluster ships its substrate slice under a descriptive cluster-suffix package, not as an unprefixed cluster-root. Foundation slice follows the same precedent.
2. **`-foundation` clearly signals "minimum viable; other slices land separately"**. A future `blocks-people-hr`, `blocks-people-crm`, `blocks-people-scheduling`, etc. compose on top, each referencing this package.
3. **Avoids the `blocks-people-core` ambiguity** — "core" implies a finished cluster minus its periphery; "foundation" makes the "more is coming" clear.

The package directory: `packages/blocks-people-foundation/`. Namespace: `Sunfish.Blocks.People.Foundation`. DI extension: `AddBlocksPeopleFoundation()`.

### What this hand-off ships

Per `party-model-convention.md` §2–§4 and `blocks-people-schema-design.md` §3.1, §3 PartyRole + sub-entities:

1. **`Party` entity** — full canonical shape per convention §2: `id`, `tenantId`, `kind` (Person/Organization), `displayName`, `legalName?`, `preferredName?`, person-only fields (`givenName`, `familyName`, `middleName`, `suffix`, `pronouns`, `dateOfBirth`), org-only fields (`legalEntityType`, `taxId`, `parentOrgId?`), tags, `preferredLanguage?`, privacy flags (`doNotContact`, `doNotEmail`, `doNotCall`, `doNotSms`), CRDT envelope (`version`, `revisionVector`, `createdAt/By`, `updatedAt/By`, `deletedAt/By/Reason`). Sub-collections referenced by ID only on the Party row; the sub-entities themselves live in their own tables (PR 1).
2. **`EmailAddress` / `PhoneNumber` / `PartyAddress`** sub-entities (sub-collections under Party per CRDT conventions §4) — append-only by convention. Each carries `id`, `partyId`, the value (`address` / `e164` / `Address`), `label?`, `isPrimary`, plus envelope fields and `replacedAt?` for marking superseded rows. (PR 1.)
3. **`PartyRole` registry** — joins Party to a role-record-id via a stable `roleName` string code; canonical roles in scope are `customer`, `tenant`, `vendor`, `contractor`, `employee` (PR 2). The role-name field is a stable string code per CRDT §5 (kebab-case, lowercase, never renamed). `startedAt` set on attach; `endedAt?` set on detach; the row is never UPDATEd otherwise (mutations are attach-or-detach events). (PR 2.)
4. **`IPartyReadModel`** — canonical typed read interface; superset of the local stubs sibling clusters already carry. Methods: `GetByIdAsync`, `GetManyAsync`, `GetDisplayNameAsync`, `GetEmailAsync`, `GetPhoneAsync`, `GetPrimaryAddressAsync`, `FindByExactEmailAsync`, `FindByExactPhoneE164Async`, `FindByExactDisplayNameAsync`, `GetTaxIdAndAddressAsync` (1099 surface; redacted by default), `GetRolesAsync`, `HasRoleAsync`. (PR 3.)
5. **`IPartyWriteService`** — write surface for create / update / soft-delete / role-attach / role-detach / contact-method-add. Every mutation emits a `People.*` cross-cluster event per `cross-cluster-event-bus-design.md` §3.3. (PR 3.)
6. **`InMemoryPartyRepository`** — default in-memory implementation backing both interfaces. Backed by `ConcurrentDictionary` per cohort precedent. (PR 3.)
7. **DI extension** — `AddBlocksPeopleFoundation()` registers `IPartyReadModel`, `IPartyWriteService`, the in-memory backing repository, the in-memory event publisher (`IPartyEventPublisher`), and the role-registry validator. (PR 3.)
8. **ERPNext importer integration** — `IErpnextPartyImporter` covering ERPNext `Customer` + `Supplier` doctype mapping → Party + role. Mirrors the `IErpnextAccountImporter` / `IErpnextSalesInvoiceImporter` pattern from the ledger + AR hand-offs. Pass 0 of the ERPNext-to-Anchor migration (before any AR/AP work) per the importer spec. (PR 4.)
9. **`apps/docs/blocks-people-foundation/overview.md`** — cluster docs page. (PR 4.)
10. **Cross-cluster events** per event-bus catalog §3.3: `People.PartyCreated`, `People.PartyUpdated`, `People.PartyDeleted`, `People.RoleAttached`, `People.RoleDetached`, `People.EmailAddressAdded`, `People.PhoneNumberAdded`, `People.AddressAdded`. (PR 3.) **PR 3 includes catalog reconciliation** — if any event names in this list disagree with the canonical catalog at `cross-cluster-event-bus-design.md` §3.3, USE THE CANONICAL NAMES (no rename, per event-bus design §2).

### What this hand-off does NOT ship

- **`Employee`** + `Compensation` + `Leave` + `Shift` + `Position` + `OnboardingTaskAssignment` + `TrainingCompletion` (HR + scheduling + onboarding + training). Deferred to follow-on `blocks-people-hr-*` hand-offs (Phase 3).
- **`Lead`** + `Opportunity` + `Campaign` + `Activity` (CRM funnel). Deferred to follow-on `blocks-people-crm-*` hand-offs (Phase 3).
- **`PartyRelationship`** (e.g., "Jane is spouse of John" or "Acme is parent of Acme-NJ-LLC" or "Bob is emergency-contact-for Jane"). Deferred — interface stub only; implementation in a follow-on hand-off. **Halt condition (§Halt-conditions H5):** if any consumer cluster needs PartyRelationship inside this hand-off's scope window, file `cob-question-*` rather than adding it.
- **Role-specific extension entities** (`Customer` with AR account + payment terms; `Tenant` with current lease + move-in date; `Vendor` with AP account + 1099 eligibility; `Contractor` with insurance + license + hourly rate; `Employee` with employee number + position). Per `party-model-convention.md` §3 + §4, these live in their *respective* consumer clusters (`blocks-financial-*`, `blocks-property-*`, `blocks-work-*`) — NOT in `blocks-people-foundation`. This hand-off only ships the Party identity + the role-attach machinery; the extension entities are owned by their cluster.
- **Party dedup** — fuzzy match + manual merge UI per convention §5. Stub interfaces only (`IPartyDeduplicationCandidateFinder` returns empty in v1). Implementation deferred to follow-on workstream.
- **Background-check workflow** — per `blocks-people-schema-design.md` §9 Q8; vendor-selection-blocked; deferred.
- **Self-service portal** — Phase 4 per ADR 0088 Appendix B.
- **`blocks-leases.Party` migration** — DO NOT touch `packages/blocks-leases/Models/Party.cs` or `PartyKind.cs` or `PartyId.cs` in this hand-off. The migration is a SEPARATE retrofit hand-off (`blocks-property-leases-party-retrofit-stage06-handoff.md`) that ships AFTER this one. See §Halt-conditions H3.
- **PII encryption at rest** — for v1 this foundation ships `taxId` and `dateOfBirth` UNENCRYPTED, with a `// TODO: encrypt-at-rest per W#37/ADR-0068` comment on each field. The migration to encrypted-at-rest is a follow-on workstream (paired with `kernel-security` envelope-key wiring per W#60 P4 PR1 Stronghold/DPAPI integration). See §Halt-conditions H4 + CRDT discipline §3.

### CRDT-friendly conventions applied (binding)

Per `_shared/engineering/crdt-friendly-schema-conventions.md`:

| Convention | Applied where |
|---|---|
| §1 ULID identifiers | `PartyId`, `EmailAddressId`, `PhoneNumberId`, `PartyAddressId`, `PartyRoleId` — strongly typed; ULID-backed |
| §2 Soft-delete tombstones | `Party.deletedAt` / `deletedBy` / `deletedReason`; hard `DELETE` is forbidden. Same shape on every sub-entity. Right-to-be-forgotten (per convention §7) sets `deletedReason = "right-to-be-forgotten"` and nulls PII fields atomically |
| §3 version + revisionVector | Every entity carries `Version: long` + `RevisionVector: IReadOnlyDictionary<string, long>` (Loro-managed; application reads only) |
| §4 Append-only sub-collections | `EmailAddress` / `PhoneNumber` / `PartyAddress` rows are append-only: never UPDATEd after insert (other than `deletedAt` tombstone or `replacedAt` marking). "Updating Jane's mobile number" = insert new row with `isPrimary=true` + mark the old row `replacedAt = now()` + `isPrimary=false`. `PartyRole` is similarly append-only-with-tombstones: every attach is a new row; detach sets `endedAt` (the row itself remains for audit) |
| §5 Stable string codes | `Party.kind` is `"person"` \| `"organization"` (string codes, not int enum); `PartyRole.roleName` is `"customer"` \| `"tenant"` \| `"vendor"` \| `"contractor"` \| `"employee"` (stable string codes; never renamed; deprecation rule applies). Convention §5 deprecation discipline binds: if a role-name ever needs to change, ADD A NEW CODE; never rename the old one |
| §6 Posted-then-immutable | Not directly applicable to Party (Party is mutable identity, not immutable transaction). BUT: `PartyRole` attaches are append-only (§4); `People.*` events are append-only by event-bus design §1 |
| §7 State-machine resolution | Party has no rich state machine in this slice (just `live` vs `deletedAt`-tombstoned). Convention §7 Pattern C (idempotent application) covers the tombstone transition. Role attach/detach uses Pattern A (the append-only event log is the source of truth for "is this role currently active?") |
| §10 Two-tier validation | Tier-1 write-time: Party.kind/displayName/legalName invariants per convention §2; sub-entity validation per convention §2 (RFC 5322 email; E.164 phone). Tier-2 post-merge: stub `IPostMergeReconciler` registered but always returns "no issues" for v1; the reconciler will be filled by the dedup follow-on workstream |
| §14 Per-tenant isolation | `Party.tenantId` is required and enforced at the repository layer: a `IPartyReadModel.GetByIdAsync(id)` call from tenant context A cannot return a Party row with `tenantId == B` (the in-memory repository filters by tenant; the SQLite repository — a follow-on — will enforce it via WHERE clauses). See §Halt-conditions H2 |

The combination ensures: (a) Party rows merge cleanly across replicas via standard last-write-wins on mutable fields + CRDT-friendly subset for tombstones / role attaches / contact-method adds; (b) the canonical "Jane has 4 emails over time" pattern works because each EmailAddress is an append-only row; (c) multi-tenant isolation is enforced at the repository boundary, never crossed.

### Cross-cluster boundary (binding)

Per `party-model-convention.md` §4:

- **`blocks-people-foundation` OWNS the Party identity + role registry**. Other clusters can NEVER write to `Party` or `PartyRole` tables directly. All reads go through `IPartyReadModel`; all writes go through `IPartyWriteService`.
- **Consuming clusters own their role-extension entities** by FK to Party. For example: `blocks-financial-ar.Customer` will (in a future AR-extension hand-off) reference `partyId: PartyId` + `arAccountId: GLAccountId` + `defaultPaymentTermsId` + `creditLimit`. The `Customer` entity itself lives in `blocks-financial-ar`. `blocks-property-leases.Tenant` (canonical, replacing the deprecated `blocks-leases.Party`) lives in `blocks-property-*` with `partyId: PartyId` + `currentLeaseId` + `moveInAt`.
- **Cross-cluster reads = IPartyReadModel only**. No direct table query from outside the people cluster. The interface is versioned with `[Obsolete]` discipline.
- **Cross-cluster writes never happen** — they happen via the People event bus instead. Example: when `blocks-property-leases` activates a Tenant on lease execution, it emits `Property.LeaseExecuted`; a People-side handler subscribes and calls `IPartyWriteService.AttachRoleAsync(partyId, "tenant", tenantRecordId)` on the People side. No `blocks-property-*` code writes to a Party table.

### Why thin slice — sequencing rationale

- **`blocks-financial-ar` already shipped a local stub** of `IPartyReadModel` (per its hand-off PR 6, lines 1593-1617). When this foundation lands, the AR stub's `using` directive flips from `using Sunfish.Blocks.FinancialAr.LocalStubs;` (or whatever local namespace was used) to `using Sunfish.Blocks.People.Foundation;`, and the local stub interface is deleted. Zero behavior change.
- **`blocks-property-leases-party-retrofit-*-handoff.md` is the NEXT hand-off** sequenced after this one. It will: (1) `[Obsolete]`-mark `blocks-leases.Party` + `PartyKind` + `PartyId`; (2) ship aliases from the legacy types to the canonical ones; (3) backfill: each existing `blocks-leases.Party` row creates a canonical `Sunfish.Blocks.People.Foundation.Party` row + a `PartyRole` row with `roleName = "tenant"` or `"landlord"`/`"manager"`/`"guarantor"` (these last three are NEW role codes that the retrofit hand-off introduces — out of THIS hand-off's scope to predetermine all of them).
- **Other Phase 3 people-cluster slices** (`-hr`, `-crm`, `-scheduling`, `-onboarding`, `-training`, `-portal`) all compose on top of this foundation. None of them is the critical path for Phase 1 (financial + property MVP); they ship in Phase 3 weeks 1–8 after Phase 1 closes.

### Open question — Vendor role-record placement (convention §10 Q1)

Per `party-model-convention.md` §10 Q1: Vendor role record can live in `blocks-people-*` (recommendation; symmetric to Customer) OR `blocks-financial-*` (alternative; alongside Bill/Payment). Recommendation is **(a) people-cluster**. This hand-off **does NOT need to resolve Q1** to ship — `blocks-people-foundation` only ships the `PartyRole` registry with the role-NAME `"vendor"` as a valid code; the Vendor *extension entity* (with `apAccountId`, `is1099Eligible`, etc.) is owned by whichever cluster the future Vendor hand-off lives in. This foundation hand-off is compatible with either Q1 outcome.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify no parallel-session work on `blocks-people-*`.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-people"
   gh pr list --state open --search "blocks-people in:title,body"
   ```
   Expected: zero matches in both. If anything is open or any `blocks-people-*` package exists, **STOP** — file a `cob-question-*` beacon before opening PR 1.

2. **Verify pre-convention deviations are present but untouched.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Models/Party.cs
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Models/PartyKind.cs
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Models/PartyId.cs
   ```
   Expected: all three exist. **DO NOT MODIFY** these files in any PR of this hand-off. They are the pre-convention deviation per `blocks-property-party-alignment-review.md` §2.11 and will be retrofitted in the next hand-off.

3. **Verify ADR 0088 status.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```
   Expected: `status: Proposed` (CO ratified design 2026-05-16; status-flip is housekeeping). Hand-off is `ready-to-build` regardless — CO directive operative.

4. **Verify Party convention is in place.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/_shared/engineering/party-model-convention.md
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/_shared/engineering/crdt-friendly-schema-conventions.md
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/_shared/engineering/cross-cluster-event-bus-design.md
   ```
   Expected: all three exist. PRs cite these documents by section in commit messages + the `apps/docs/blocks-people-foundation/overview.md` page.

5. **Check whether `blocks-financial-ar` already shipped a local `IPartyReadModel` stub.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ar/ 2>&1
   grep -rln "IPartyReadModel" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ 2>/dev/null
   ```
   Two cases:
   - **Case A — `blocks-financial-ar/` does NOT exist yet.** Proceed normally. The future AR hand-off will reference *this* foundation's `IPartyReadModel`; no relocation work needed.
   - **Case B — `blocks-financial-ar/` exists with a local `IPartyReadModel` stub.** Note the local stub's namespace + method signatures in your PR 3 commit message. **Out of scope for THIS hand-off** to relocate the AR stub — that's the AR's responsibility in a follow-on housekeeping PR. But the foundation's `IPartyReadModel` SHALL be a superset of the AR stub's surface (so the relocation is a one-line `using` change). PR 3 verification step grep-confirms compatibility.

6. **Verify foundation-multitenancy is available** (for `TenantId` reference type).
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/foundation-multitenancy/
   grep -rln "TenantId" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/foundation-multitenancy/ 2>/dev/null | head -3
   ```
   Expected: package exists; `TenantId` type available. This hand-off's `Party.TenantId` is `Sunfish.Foundation.Multitenancy.TenantId` (not a local placeholder). If the type does NOT exist under that namespace, grep for alternative locations (`grep -rln "public.*record.*TenantId\|public.*struct.*TenantId" packages/foundation-* | head`) and file `cob-question-*` to confirm placement.

7. **Verify W#37 / ADR 0068 status (for PII encryption posture).**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/docs/adrs/0068*.md 2>/dev/null
   ```
   Expected: `status: Proposed`. This hand-off ships PII fields UNENCRYPTED with TODO comments per §Halt-conditions H4. When ADR 0068 advances to Accepted AND the W#60 P4 PR1 Stronghold/DPAPI substrate is wired, a follow-on workstream applies encryption-at-rest atomically. Do NOT attempt to encrypt in this hand-off.

8. **Confirm `but status` (or `git status`) is clean** and current branch is `main` (or a fresh worktree from `main`, NOT from `gitbutler/workspace` HEAD per `feedback_worktree_base_main_not_gitbutler`).

---

## Per-PR deliverables

This hand-off splits into **4 PRs** by responsibility. PRs 1 + 2 + 3 are sequential (PR 2 needs PR 1's Party + sub-entity types; PR 3 needs PR 2's PartyRole). PR 4 sequences last.

---

### PR 1 — Package scaffold + `Party` + sub-entities (`EmailAddress` + `PhoneNumber` + `PartyAddress`) + validation

**Estimated effort:** ~2–3h
**Scope:** create `packages/blocks-people-foundation/` package scaffold; ship the canonical `Party` record + `EmailAddress` / `PhoneNumber` / `PartyAddress` sub-entity records; write-time validation helpers; CRDT envelope on every type; NO services yet
**Commit subject:** `feat(blocks-people-foundation): add Party + EmailAddress/PhoneNumber/PartyAddress sub-entities per party-model-convention §2`
**Branch:** `cob/blocks-people-foundation-party-entity`

#### Package scaffold

- `packages/blocks-people-foundation/Sunfish.Blocks.People.Foundation.csproj` — .NET 11 preview; matches the conventions of `blocks-financial-ledger/Sunfish.Blocks.FinancialLedger.csproj`.
- `packages/blocks-people-foundation/tests/Sunfish.Blocks.People.Foundation.Tests.csproj`.
- `packages/blocks-people-foundation/README.md` — references Stage 02 design + ADR 0088 + the Party-model convention.
- `packages/blocks-people-foundation/NOTICE.md` — attribution for Apache OFBiz `party` module (Apache 2.0; see §License posture).
- Add to `Sunfish.slnx` (or `Sunfish.sln`) via `dotnet sln add ...`.

#### New types — strongly-typed IDs

All ULID-backed, mirroring the existing `InvoiceId` / `GLAccountId` pattern (per the ledger + AR hand-off precedent).

- `Models/PartyId.cs` — readonly record struct + JSON converter; static `NewId()` returns a ULID-string-backed instance.
- `Models/EmailAddressId.cs`.
- `Models/PhoneNumberId.cs`.
- `Models/PartyAddressId.cs`.

**Note on ULID vs Guid:** the existing `blocks-leases/Models/PartyId.cs` uses `Guid.NewGuid().ToString()` (pre-CRDT-conventions code). This hand-off's `PartyId` is **distinct** (a different namespace) and uses **ULID** per `crdt-friendly-schema-conventions.md` §1. There is no namespace collision — `Sunfish.Blocks.Leases.Models.PartyId` and `Sunfish.Blocks.People.Foundation.PartyId` are separate types. The retrofit hand-off (out of scope here) will alias one to the other.

**ULID implementation:** prefer an existing project ULID helper if one exists (search `grep -rln "public.*Ulid\|public.*ULID\|NewUlid\b" packages/foundation-* | head`). If absent, use `Ulid` from the `Cysharp.Ulid` NuGet package (BSD-3-Clause; permissive) — referenced in the csproj. If neither is in use, file `cob-question-*` to align on a project-wide ULID helper.

#### New types — `PartyKind` enum (stable string codes)

```csharp
namespace Sunfish.Blocks.People.Foundation;

/// <summary>
/// Discriminator for whether a <see cref="Party"/> is a natural person or a legal entity.
/// </summary>
/// <remarks>
/// Stored as a stable string code ("person" | "organization") per
/// crdt-friendly-schema-conventions.md §5. Do NOT rename existing codes —
/// add new codes if new Party kinds are ever introduced.
/// </remarks>
public enum PartyKind
{
    /// <summary>A natural person.</summary>
    Person,

    /// <summary>A legal entity (LLC, C-Corp, Sole Proprietorship, etc.).</summary>
    Organization,
}
```

Persisted form: lowercase string codes via a JSON converter (`"person"` / `"organization"`); enum-to-string conversion handled by `PartyKindJsonConverter`.

#### New types — Sub-entity records

**`Models/EmailAddress.cs`** per `party-model-convention.md` §2:

```csharp
public sealed record EmailAddress
{
    public required EmailAddressId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required PartyId PartyId { get; init; }
    public required string Address { get; init; }           // RFC 5322
    public string? Label { get; init; }                     // "work" | "personal" | "billing" | "other"
    public required bool IsPrimary { get; init; }
    public bool IsValidated { get; init; }
    public Instant? ValidatedAt { get; init; }
    public Instant? OptedOutAt { get; init; }               // unsubscribe / bounce-suppression
    public Instant? ReplacedAt { get; init; }               // marks superseded rows (append-only convention)

    // CRDT envelope (per crdt-friendly-schema-conventions.md §13)
    public required Instant CreatedAt { get; init; }
    public required PartyId CreatedBy { get; init; }
    public Instant UpdatedAt { get; init; }
    public PartyId? UpdatedBy { get; init; }
    public Instant? DeletedAt { get; init; }
    public PartyId? DeletedBy { get; init; }
    public string? DeletedReason { get; init; }
    public required long Version { get; init; }
    public IReadOnlyDictionary<string, long> RevisionVector { get; init; }
        = new Dictionary<string, long>();
}
```

**`Models/PhoneNumber.cs`** — mirrors EmailAddress shape; fields per convention §2:
- `E164: string` (`^\+[1-9]\d{1,14}$` validated at write-time)
- `Extension: string?`
- `Label: string?` (`"mobile" | "work" | "home" | "fax" | "other"`)
- `IsPrimary: bool`
- `IsMobile: bool` (gates SMS eligibility)
- `SmsOptedOutAt: Instant?`
- `ReplacedAt: Instant?`
- envelope

**`Models/PartyAddress.cs`** — fields per convention §2:
- `Address: Address` (value object: `{ Line1, Line2?, City, Region, PostalCode, Country }`)
- `Label: string?` (`"primary" | "mailing" | "billing" | "shipping" | "physical"`)
- `IsPrimary: bool`
- `ValidFrom: Instant?`
- `ValidTo: Instant?`
- `ReplacedAt: Instant?`
- envelope

The `Address` value object lives in `Models/Address.cs` as a sealed record with the six fields above; ISO 3166-1 alpha-2 validation on `Country`.

#### New type — `Party` (the canonical entity)

```csharp
public sealed record Party
{
    // Identity
    public required PartyId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required PartyKind Kind { get; init; }
    public required string DisplayName { get; init; }       // "Doe, Jane" / "Acme Corp"
    public string? LegalName { get; init; }
    public string? PreferredName { get; init; }

    // Person-only
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
    public string? MiddleName { get; init; }
    public string? Suffix { get; init; }
    public string? Pronouns { get; init; }
    public LocalDate? DateOfBirth { get; init; }            // TODO: encrypt-at-rest per W#37/ADR-0068

    // Organization-only
    public string? LegalEntityType { get; init; }           // "LLC", "C-Corp", etc.
    public string? TaxId { get; init; }                     // EIN/SSN; TODO: encrypt-at-rest per W#37/ADR-0068
    public PartyId? ParentOrgId { get; init; }

    // Sub-collections — by reference (loaded via repository navigation)
    // The Party row itself does NOT carry the sub-entity arrays inline;
    // the repository exposes them via separate query methods.

    // Metadata
    public string? WebSite { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string? PreferredLanguage { get; init; }         // BCP-47 code

    // Privacy controls (cascade to all roles)
    public required bool DoNotContact { get; init; }
    public required bool DoNotEmail { get; init; }
    public required bool DoNotCall { get; init; }
    public required bool DoNotSms { get; init; }

    // CRDT envelope (per crdt-friendly-schema-conventions.md §13)
    public required Instant CreatedAt { get; init; }
    public required PartyId CreatedBy { get; init; }
    public Instant UpdatedAt { get; init; }
    public PartyId? UpdatedBy { get; init; }
    public Instant? DeletedAt { get; init; }                // tombstone
    public PartyId? DeletedBy { get; init; }
    public string? DeletedReason { get; init; }
    public required long Version { get; init; }
    public IReadOnlyDictionary<string, long> RevisionVector { get; init; }
        = new Dictionary<string, long>();
}
```

**Note on `WebSite: string?`:** Convention §2 specifies `webSites: string[]`. To keep PR 1 lean, this hand-off ships a single `WebSite: string?` column on the Party row; multi-website support can come later as a sub-entity (`PartyWebSite`) when needed. Flag in PR 1 description as "intentional v1 simplification; multi-WebSite added in follow-on".

**Note on `Notes`:** Convention §7 calls out: "PHI / counsel-sensitive content forbidden — UI warns user not to enter health/legal info." This hand-off ships `Notes: string?` without an enforcing validator (UI-side enforcement only). Documented in `apps/docs/blocks-people-foundation/overview.md`.

#### Static factory + validation helpers

**`Models/Party.cs` — static factory `Party.Create`:**

```csharp
public static Party Create(
    PartyId id,
    TenantId tenantId,
    PartyKind kind,
    string displayName,
    PartyId createdBy,
    Instant? createdAt = null,
    string? legalName = null,
    string? givenName = null,
    string? familyName = null,
    string? legalEntityType = null,
    bool doNotContact = false,
    bool doNotEmail = false,
    bool doNotCall = false,
    bool doNotSms = false)
{
    var now = createdAt ?? SystemClock.Instance.GetCurrentInstant();
    return new Party
    {
        Id = id,
        TenantId = tenantId,
        Kind = kind,
        DisplayName = displayName,
        LegalName = legalName,
        GivenName = givenName,
        FamilyName = familyName,
        LegalEntityType = legalEntityType,
        DoNotContact = doNotContact,
        DoNotEmail = doNotEmail,
        DoNotCall = doNotCall,
        DoNotSms = doNotSms,
        CreatedAt = now,
        CreatedBy = createdBy,
        UpdatedAt = now,
        UpdatedBy = createdBy,
        Version = 1,
    };
}
```

**`Validation/PartyValidator.cs`** per convention §2:

```csharp
public static class PartyValidator
{
    public static ValidationResult Validate(Party party)
    {
        var errors = new List<ValidationError>();

        // kind = "person" → givenName OR displayName required
        if (party.Kind == PartyKind.Person
            && string.IsNullOrWhiteSpace(party.GivenName)
            && string.IsNullOrWhiteSpace(party.DisplayName))
            errors.Add(new("kind-person-requires-name",
                "Party of kind=person requires GivenName or DisplayName"));

        // kind = "organization" → displayName OR legalName required
        if (party.Kind == PartyKind.Organization
            && string.IsNullOrWhiteSpace(party.DisplayName)
            && string.IsNullOrWhiteSpace(party.LegalName))
            errors.Add(new("kind-org-requires-name",
                "Party of kind=organization requires DisplayName or LegalName"));

        // parentOrgId only valid when kind=organization
        if (party.ParentOrgId is not null && party.Kind != PartyKind.Organization)
            errors.Add(new("parent-org-on-person",
                "ParentOrgId is only valid on kind=organization Parties"));

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }
}
```

**`Validation/EmailAddressValidator.cs`** — RFC 5322 regex check (use `System.Net.Mail.MailAddress` parse-or-throw; wrap as `ValidationResult`).
**`Validation/PhoneNumberValidator.cs`** — E.164 `^\+[1-9]\d{1,14}$`.
**`Validation/PartyAddressValidator.cs`** — Country ISO 3166-1 alpha-2 (2 uppercase letters); `ValidTo > ValidFrom` when both set.

**Convention §2 "at least one contact method required for any Party in an active role record"** is enforced at the `IPartyWriteService.AttachRoleAsync` boundary (PR 3), not on the Party constructor — because the Party row legitimately exists alone during the create-then-attach-role two-step pattern. Document the deferred check in the PartyValidator XML doc comment.

#### Tests (PR 1)

`tests/PartyTests.cs`:
- `Create_PersonWithGivenName_Succeeds`.
- `Create_OrganizationWithLegalName_Succeeds`.
- `Validate_PersonWithoutName_Fails`.
- `Validate_OrganizationWithoutName_Fails`.
- `Validate_PersonWithParentOrgId_Fails`.
- `Validate_WellFormedPerson_Passes`.
- `Validate_WellFormedOrganization_Passes`.
- `PartyKindJsonRoundtrip_LowercasePersonOrganization` (round-trips through serialization).
- `EnvelopeFields_AllRequired_RoundTrip`.

`tests/EmailAddressTests.cs`:
- `Validate_RFC5322Address_Passes`.
- `Validate_MalformedAddress_Fails` (e.g., `"not-an-email"`).
- `Validate_AddressMissingDomain_Fails`.

`tests/PhoneNumberTests.cs`:
- `Validate_E164Address_Passes`.
- `Validate_NonE164_Fails` (e.g., `"(555) 555-0100"` without country code).
- `Validate_TooLong_Fails` (16-digit number).

`tests/PartyAddressTests.cs`:
- `Validate_AlphaTwoCountry_Passes`.
- `Validate_LowercaseCountry_Fails`.
- `Validate_ValidToBeforeValidFrom_Fails`.
- `Validate_OmittedValidFromAndValidTo_Passes`.

Total new tests this PR: ~14–16.

#### Verification

- `dotnet build` succeeds across the solution.
- `dotnet test packages/blocks-people-foundation/tests/` passes ~14–16 tests.
- `grep -r "Sunfish.Blocks.People.Foundation" packages/` returns hits ONLY in the new package (no other packages consume it yet).
- `grep -r "blocks-leases/Models/Party.cs" packages/blocks-people-foundation/` returns zero hits (no accidental import of the legacy type).

#### PR description template

```
Add Sunfish.Blocks.People.Foundation per
party-model-convention.md §2 + crdt-friendly-schema-conventions.md §§1–5,13.

PR 1 of 4 in the people-foundation hand-off. Ships:

- Package scaffold: packages/blocks-people-foundation/
- Strongly-typed ULID IDs: PartyId, EmailAddressId, PhoneNumberId, PartyAddressId
- PartyKind enum (stable string codes: "person" | "organization")
- Party record (canonical identity per convention §2)
- Sub-entities: EmailAddress, PhoneNumber, PartyAddress (append-only sub-collections per §4)
- Validators: PartyValidator, EmailAddressValidator, PhoneNumberValidator, PartyAddressValidator
- ~14–16 tests covering construction + validation + JSON round-trip + envelope

DOES NOT ship: PartyRole (PR 2), IPartyReadModel (PR 3), services (PR 3),
importer (PR 4). PII encryption-at-rest deferred per W#37/ADR-0068 — TODO
comments on Party.TaxId + Party.DateOfBirth.

Refs: ADR 0088 §1; party-model-convention.md §2 + §4 + §7;
crdt-friendly-schema-conventions.md §§1–5,13;
blocks-people-schema-design.md §3.1.
```

#### Do NOT in this PR

- Do NOT ship `PartyRole`. That's PR 2.
- Do NOT ship `IPartyReadModel` / `IPartyWriteService`. That's PR 3.
- Do NOT touch `packages/blocks-leases/Models/Party.cs` or siblings. That's the retrofit hand-off, not this hand-off.
- Do NOT introduce role-extension entities (`Customer`, `Tenant`, `Vendor`, `Contractor`, `Employee`). Those live in their respective consumer clusters; not here.
- Do NOT add encryption to `TaxId` or `DateOfBirth`. That's a follow-on workstream gated on W#60 P4 PR1 Stronghold/DPAPI being ready.

---

### PR 2 — `PartyRole` registry + canonical role-name codes

**Estimated effort:** ~1.5–2h
**Scope:** add `PartyRole` entity + role-name stable string codes (`customer`, `tenant`, `vendor`, `contractor`, `employee`); validation; NO services yet (those compose with the role-registry in PR 3)
**Commit subject:** `feat(blocks-people-foundation): add PartyRole registry + 5 canonical role-name codes per party-model-convention §3`
**Depends on:** PR 1 merged
**Branch:** `cob/blocks-people-foundation-partyrole`

#### New type — `PartyRoleId`

ULID-backed strongly-typed ID; mirrors `PartyId` pattern from PR 1.

#### New type — `PartyRoleName` (stable string codes)

Per `crdt-friendly-schema-conventions.md` §5 (stable string codes; lowercase; kebab-case; never renamed; deprecation rule applies):

```csharp
namespace Sunfish.Blocks.People.Foundation;

/// <summary>
/// Canonical role-name codes for the <see cref="PartyRole"/> registry.
/// </summary>
/// <remarks>
/// <para>
/// These are <b>stable string codes</b> per
/// <c>crdt-friendly-schema-conventions.md §5</c>:
/// </para>
/// <list type="bullet">
///   <item>kebab-case; lowercase; no spaces</item>
///   <item>Never rename an existing code — add a new code instead</item>
///   <item>Persisted as the string value; the constant is for compile-time discoverability only</item>
///   <item>Future role codes added by follow-on hand-offs (e.g., "landlord", "manager", "guarantor"
///       from the blocks-property-leases retrofit) extend this set additively</item>
/// </list>
/// </remarks>
public static class PartyRoleName
{
    /// <summary>An AR-side party reference; references a Customer extension entity in blocks-financial-ar.</summary>
    public const string Customer = "customer";

    /// <summary>A residential or commercial tenant; references a Tenant extension entity in blocks-property-leases.</summary>
    public const string Tenant = "tenant";

    /// <summary>An AP-side party reference; references a Vendor extension entity (placement per convention §10 Q1).</summary>
    public const string Vendor = "vendor";

    /// <summary>A work-cluster operational counterparty; references a Contractor extension entity in blocks-work-*.</summary>
    public const string Contractor = "contractor";

    /// <summary>An HR-side party reference; references an Employee extension entity in blocks-people-hr-* (Phase 3).</summary>
    public const string Employee = "employee";

    /// <summary>
    /// The complete set of canonical role-name codes shipped by this foundation slice.
    /// Used by validators + the PartyRole CHECK constraint.
    /// </summary>
    public static readonly IReadOnlySet<string> All
        = new HashSet<string>(StringComparer.Ordinal)
        {
            Customer, Tenant, Vendor, Contractor, Employee,
        };

    /// <summary>
    /// True if <paramref name="code"/> is a known canonical role-name code.
    /// Unknown codes are NOT rejected at the storage layer (per CRDT discipline —
    /// an offline replica may emit a role-name added in a future build); the
    /// write-service surface accepts the unknown code with a warning event.
    /// </summary>
    public static bool IsKnown(string code) => All.Contains(code);
}
```

**On the closed-set vs open-set tension:** Convention §3 lists `RoleKind = "employee" | "contact" | "customer" | "tenant" | "vendor" | "lead" | "applicant" | "contractor" | "owner" | "user"` — a wider set than this foundation's 5. Per CRDT §5 deprecation discipline, code names cannot be retired-and-renamed; the foundation chooses **the 5 already needed by Phase 1 / Phase 3 entry-point consumers** and leaves the rest for follow-on hand-offs to *add* (not rename, not redefine). The `IsKnown` helper returns `false` for `"lead"` / `"applicant"` / `"contact"` / `"owner"` / `"user"` in this hand-off; a future hand-off adds them as additional `public const string Lead = "lead";` declarations. No breaking change.

Sub-cluster role names introduced by retrofits (`"landlord"`, `"manager"`, `"guarantor"` from the blocks-leases retrofit) likewise extend this set additively in that retrofit's PR — not here.

#### New type — `PartyRole` (the registry record)

```csharp
public sealed record PartyRole
{
    public required PartyRoleId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required PartyId PartyId { get; init; }
    public required string RoleName { get; init; }            // stable string code; see PartyRoleName
    public required string RoleRecordId { get; init; }        // opaque ID into the consumer cluster's extension entity
    public required Instant StartedAt { get; init; }
    public Instant? EndedAt { get; init; }                    // null = active
    public string? EndedReason { get; init; }

    // CRDT envelope (per crdt-friendly-schema-conventions.md §13)
    public required Instant CreatedAt { get; init; }
    public required PartyId CreatedBy { get; init; }
    public Instant UpdatedAt { get; init; }
    public PartyId? UpdatedBy { get; init; }
    public Instant? DeletedAt { get; init; }                  // tombstone (rare; merge/erasure paths)
    public PartyId? DeletedBy { get; init; }
    public string? DeletedReason { get; init; }
    public required long Version { get; init; }
    public IReadOnlyDictionary<string, long> RevisionVector { get; init; }
        = new Dictionary<string, long>();
}
```

**`RoleRecordId: string` (not strongly-typed)** — the ID points at an entity in a *different* cluster (`blocks-financial-ar.Customer.Id`, `blocks-property-leases.Tenant.Id`, etc.). Strongly typing it would force `blocks-people-foundation` to take dependencies on every consuming cluster's ID type — a circular-dependency disaster. Stored as opaque string; resolved by the consumer cluster at read time. Documented in the field's XML doc comment.

**Append-only with tombstone-on-detach pattern:**

Per CRDT §4 + the convention's "role transitions are state changes, not data copies" principle (`party-model-convention.md` §1):

- **Attach** = INSERT a new `PartyRole` row with `EndedAt == null`.
- **Detach** = UPDATE the existing row to set `EndedAt = now()` + `EndedReason`. This is the ONLY field mutation allowed on a `PartyRole` row.
- **Re-attach** (Party regains a previously-detached role) = INSERT a NEW row (do NOT clear `EndedAt` on the old row).
- **Hard delete** = forbidden except in compliance-erasure paths (per CRDT §2 "When hard delete IS allowed").

The discipline is enforced by the PR 3 `IPartyWriteService.AttachRoleAsync` / `DetachRoleAsync` methods — never via direct repository writes.

#### Validation

**`Validation/PartyRoleValidator.cs`**:

```csharp
public static class PartyRoleValidator
{
    public static ValidationResult Validate(PartyRole role)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(role.RoleName))
            errors.Add(new("role-name-required",
                "PartyRole.RoleName must be a non-empty stable string code"));

        // Validate role-name shape (kebab-case + lowercase) — discipline check.
        // Unknown codes are PERMITTED (open-set per the IsKnown contract) but
        // they must conform to the shape.
        if (!IsValidStableStringCode(role.RoleName))
            errors.Add(new("role-name-shape-invalid",
                $"RoleName '{role.RoleName}' violates stable-string-code shape "
                + "(kebab-case + lowercase only; see crdt-friendly-schema-conventions.md §5)"));

        if (string.IsNullOrWhiteSpace(role.RoleRecordId))
            errors.Add(new("role-record-id-required",
                "PartyRole.RoleRecordId must be a non-empty opaque ID"));

        if (role.EndedAt is not null && role.EndedAt < role.StartedAt)
            errors.Add(new("ended-before-started",
                "PartyRole.EndedAt must be >= StartedAt"));

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private static bool IsValidStableStringCode(string s)
        => !string.IsNullOrEmpty(s)
        && s.Length <= 64
        && s.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-')
        && s[0] != '-' && s[^1] != '-';
}
```

#### Tests (PR 2)

`tests/PartyRoleTests.cs`:
- `Create_WithKnownRoleName_Passes`.
- `Create_WithUnknownButShapeValidRoleName_Passes` (e.g., `"landlord"` — not in this hand-off's set but shape-valid).
- `Validate_EmptyRoleName_Fails`.
- `Validate_RoleNameWithUppercase_Fails` (`"Customer"`).
- `Validate_RoleNameWithSpaces_Fails` (`"property owner"`).
- `Validate_RoleNameWithUnderscores_Fails` (`"property_owner"`).
- `Validate_RoleNameStartingWithDash_Fails`.
- `Validate_EndedBeforeStarted_Fails`.
- `Validate_EmptyRoleRecordId_Fails`.
- `IsKnown_KnownCodes_All5Return_True` (customer, tenant, vendor, contractor, employee).
- `IsKnown_UnknownCode_ReturnsFalse` (`"lead"`).
- `All_ContainsExactly5Codes`.

Total new tests this PR: ~12.

#### Verification

- `dotnet build` succeeds.
- All previous tests (PR 1) pass unchanged.
- New tests pass.
- `grep -r "RoleName.*=.*\"[A-Z]" packages/blocks-people-foundation/` returns zero hits (no uppercase role-name codes anywhere).

---

### PR 3 — `IPartyReadModel` + `IPartyWriteService` + `InMemoryPartyRepository` + DI extension + event surface

**Estimated effort:** ~2.5–3h
**Scope:** ship the typed read interface + the write service + the in-memory repository default + DI extension; emit cross-cluster `People.*` events on every write; canonical-event-catalog reconciliation step
**Commit subject:** `feat(blocks-people-foundation): add IPartyReadModel + IPartyWriteService + InMemoryPartyRepository + AddBlocksPeopleFoundation DI extension`
**Depends on:** PR 2 merged
**Branch:** `cob/blocks-people-foundation-services`

#### Canonical-event-catalog reconciliation (FIRST STEP)

Before ANY code in this PR is written, COB **must** read `_shared/engineering/cross-cluster-event-bus-design.md` §3.3 in full and confirm the People-domain event names this hand-off introduces. The hand-off's stated event names (from §Context):

- `People.PartyCreated`
- `People.PartyUpdated`
- `People.PartyDeleted` (soft)
- `People.RoleAttached`
- `People.RoleDetached`
- `People.EmailAddressAdded`
- `People.PhoneNumberAdded`
- `People.AddressAdded`

If §3.3 catalog disagrees (e.g., names a different verb or different cluster prefix), **USE THE CANONICAL CATALOG NAMES** per event-bus design §2: "Do not rename existing event types. Add new + deprecate old." The catalog is canonical; this hand-off's list is the proposal.

If §3.3 catalog is silent on these 8 events (most likely — the current catalog focuses on lifecycle transitions like `TenantActivated`, `OpportunityWon`, not CRUD-style entity-mutation events), append them to the catalog **in the same PR** (PR 3) per event-bus design §3 "Catalog upkeep" rule: "Every event added in a Stage 06 implementation MUST be back-filled into this table."

The catalog edit is a 9-line addition to `_shared/engineering/cross-cluster-event-bus-design.md` §3.3 (one row per event) in a single commit alongside the C# event-publisher code.

#### New types — event records

**`Events/IPartyEvent.cs`** — marker interface for all `People.*` foundation events.

**`Events/PartyCreatedEvent.cs`** through **`Events/AddressAddedEvent.cs`** — eight records, each carrying the standard envelope fields (per `cross-cluster-event-bus-design.md` §1):

```csharp
public sealed record PartyCreatedEvent(
    EventId EventId,
    string EventType,                // "People.PartyCreated"
    string SchemaVersion,            // "1.0.0"
    Instant OccurredAt,
    Instant RecordedAtUtc,
    TenantId TenantId,
    string OriginatingReplicaId,
    EventId? CausationId,
    string? CorrelationId,
    string ProducerCluster,          // "people"
    string IdempotencyKey,           // "party-created:{partyId}"
    PartyCreatedPayload Payload
) : IPartyEvent;

public sealed record PartyCreatedPayload(
    PartyId PartyId,
    PartyKind Kind,
    string DisplayName);
```

Each event record follows the same shape. Idempotency keys per §3.3 catalog:

| Event | Idempotency key |
|---|---|
| `People.PartyCreated` | `party-created:{partyId}` |
| `People.PartyUpdated` | `party-updated:{partyId}:{version}` |
| `People.PartyDeleted` | `party-deleted:{partyId}` |
| `People.RoleAttached` | `role-attached:{partyRoleId}` |
| `People.RoleDetached` | `role-detached:{partyRoleId}` |
| `People.EmailAddressAdded` | `email-added:{emailAddressId}` |
| `People.PhoneNumberAdded` | `phone-added:{phoneNumberId}` |
| `People.AddressAdded` | `address-added:{partyAddressId}` |

(If the catalog reconciliation in step 1 disagrees with any of these, use the catalog form.)

#### New interface — `IPartyReadModel`

The canonical, framework-agnostic read interface. Lives in the `Services/` folder of the package.

```csharp
namespace Sunfish.Blocks.People.Foundation;

/// <summary>
/// Typed, read-only access to canonical Party + sub-entity + PartyRole data.
/// </summary>
/// <remarks>
/// <para>This is the cross-cluster contract per party-model-convention.md §4.
/// Consumers in blocks-financial-*, blocks-property-*, blocks-work-*, and
/// blocks-docs-* read Party data ONLY through this interface — never via
/// direct repository access.</para>
/// <para>All methods enforce per-tenant isolation per
/// crdt-friendly-schema-conventions.md §14: a call from tenant context A
/// cannot return a Party row with tenantId == B. The TenantId is taken
/// from the ambient ITenantContextAccessor (registered by foundation-multitenancy);
/// callers do NOT pass tenantId explicitly.</para>
/// </remarks>
public interface IPartyReadModel
{
    /// <summary>Look up a Party by its ID. Returns null if not found or soft-deleted.</summary>
    Task<Party?> GetByIdAsync(PartyId id, CancellationToken cancellationToken = default);

    /// <summary>Batch look-up. Missing or soft-deleted IDs are omitted from the result.</summary>
    Task<IReadOnlyDictionary<PartyId, Party>> GetManyAsync(
        IReadOnlyCollection<PartyId> ids,
        CancellationToken cancellationToken = default);

    /// <summary>Convenience: returns just DisplayName, or null if Party not found.</summary>
    Task<string?> GetDisplayNameAsync(PartyId id, CancellationToken cancellationToken = default);

    /// <summary>Returns the primary (or labeled) email address, or null if none.</summary>
    Task<string?> GetEmailAsync(PartyId id, string? label = null, CancellationToken cancellationToken = default);

    /// <summary>Returns the primary (or labeled) phone in E.164 form, or null if none.</summary>
    Task<string?> GetPhoneAsync(PartyId id, string? label = null, CancellationToken cancellationToken = default);

    /// <summary>Returns the primary address, or null if none.</summary>
    Task<Address?> GetPrimaryAddressAsync(PartyId id, CancellationToken cancellationToken = default);

    /// <summary>Exact-match lookup by email (case-insensitive). Returns all matching Parties (rare; usually 0–1).</summary>
    Task<IReadOnlyList<Party>> FindByExactEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Exact-match lookup by E.164 phone. Returns all matching Parties (rare; usually 0–1).</summary>
    Task<IReadOnlyList<Party>> FindByExactPhoneE164Async(string e164, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exact-match lookup by DisplayName (case-insensitive). Used by the migration importer
    /// to resolve ERPNext customer/supplier names to Party IDs.
    /// </summary>
    Task<IReadOnlyList<Party>> FindByExactDisplayNameAsync(
        string displayName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns Party.TaxId + primary Address for 1099 reporting per
    /// party-model-convention.md §7 ("1099 generation reads
    /// Vendor.is1099Eligible + Party.taxId + Party.address").
    /// Caller must have "view-tax-id" permission; the repository
    /// audit-logs every successful call.
    /// </summary>
    /// <remarks>
    /// In this foundation slice the audit-log is a stub
    /// (writes to ILogger only); the full audit substrate wires in
    /// when kernel-audit's <see cref="IActorPrincipalResolver"/>
    /// surface stabilizes (see §Halt-conditions H1).
    /// </remarks>
    Task<(string TaxId, Address Address)?> GetTaxIdAndAddressAsync(
        PartyId id,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all active (non-ended) PartyRole rows for a Party.</summary>
    Task<IReadOnlyList<PartyRole>> GetRolesAsync(
        PartyId id,
        CancellationToken cancellationToken = default);

    /// <summary>Convenience: does this Party currently hold the named role?</summary>
    Task<bool> HasRoleAsync(
        PartyId id,
        string roleName,
        CancellationToken cancellationToken = default);
}
```

#### New interface — `IPartyWriteService`

```csharp
public interface IPartyWriteService
{
    /// <summary>Creates a new Party. Emits People.PartyCreated.</summary>
    Task<Party> CreateAsync(
        PartyKind kind,
        string displayName,
        IPartyWriteOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Updates mutable fields on an existing Party. Emits People.PartyUpdated.</summary>
    Task<Party> UpdateAsync(
        PartyId id,
        IPartyUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the Party (tombstone via deletedAt). Emits People.PartyDeleted.
    /// Hard delete is forbidden; right-to-be-forgotten uses a different path
    /// (PII-null + tombstone via reason="right-to-be-forgotten") deferred to follow-on.
    /// </summary>
    Task DeleteAsync(
        PartyId id,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a role to a Party. Emits People.RoleAttached.
    /// <paramref name="roleRecordId"/> is an opaque string pointing at the consumer cluster's extension entity.
    /// </summary>
    Task<PartyRole> AttachRoleAsync(
        PartyId partyId,
        string roleName,
        string roleRecordId,
        CancellationToken cancellationToken = default);

    /// <summary>Detaches a role (sets endedAt on the active PartyRole row). Emits People.RoleDetached.</summary>
    Task DetachRoleAsync(
        PartyRoleId roleId,
        string endedReason,
        CancellationToken cancellationToken = default);

    /// <summary>Adds an EmailAddress sub-entity. Emits People.EmailAddressAdded.</summary>
    Task<EmailAddress> AddEmailAsync(
        PartyId partyId,
        string address,
        string? label = null,
        bool isPrimary = false,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a PhoneNumber sub-entity. Emits People.PhoneNumberAdded.</summary>
    Task<PhoneNumber> AddPhoneAsync(
        PartyId partyId,
        string e164,
        string? label = null,
        bool isMobile = false,
        bool isPrimary = false,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a PartyAddress sub-entity. Emits People.AddressAdded.</summary>
    Task<PartyAddress> AddAddressAsync(
        PartyId partyId,
        Address address,
        string? label = null,
        bool isPrimary = false,
        CancellationToken cancellationToken = default);
}
```

#### New interface — `IPartyEventPublisher`

```csharp
public interface IPartyEventPublisher
{
    Task PublishAsync(IPartyEvent @event, CancellationToken cancellationToken = default);
}

public sealed class InMemoryPartyEventPublisher : IPartyEventPublisher
{
    private readonly ConcurrentQueue<IPartyEvent> _events = new();

    public IReadOnlyCollection<IPartyEvent> Recorded => _events.ToArray();

    public Task PublishAsync(IPartyEvent @event, CancellationToken cancellationToken = default)
    {
        _events.Enqueue(@event);
        return Task.CompletedTask;
    }
}
```

The real (Loro-wired) event publisher arrives with the `cross-cluster-event-bus-design.md` Stage 06 hand-off when that lands; for now, in-memory is sufficient for tests + the foundation's own consumers.

#### Repository — `InMemoryPartyRepository`

Backing store for both `IPartyReadModel` and `IPartyWriteService`. Uses `ConcurrentDictionary` per cohort discipline.

```csharp
public sealed class InMemoryPartyRepository : IPartyReadModel, IPartyWriteService
{
    private readonly ConcurrentDictionary<(TenantId, PartyId), Party> _parties = new();
    private readonly ConcurrentDictionary<PartyRoleId, PartyRole> _roles = new();
    private readonly ConcurrentDictionary<EmailAddressId, EmailAddress> _emails = new();
    private readonly ConcurrentDictionary<PhoneNumberId, PhoneNumber> _phones = new();
    private readonly ConcurrentDictionary<PartyAddressId, PartyAddress> _addresses = new();
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IPartyEventPublisher _events;
    private readonly IActorPrincipalResolver _actor;   // see §Halt-conditions H1
    private readonly TimeProvider _time;

    // ... (read/write methods enforce _tenantContext.Current at every boundary;
    //      writes emit events via _events; mutations bump Version + UpdatedAt
    //      per crdt-friendly-schema-conventions.md §13)
}
```

**Per-tenant isolation discipline (binding):** every read method first checks `_tenantContext.Current` and filters the dictionary on the `(TenantId, PartyId)` tuple key. Cross-tenant reads (e.g., a tenant-A caller asking for a tenant-B Party) return null/empty, not the row. Tests verify this explicitly (see test list).

**CRDT envelope discipline (binding):** every write bumps `Version`, sets `UpdatedAt = _time.GetUtcNow()`, and sets `UpdatedBy` from `_actor.GetCurrentPartyId()` (or a placeholder if the actor surface is not yet wired — see §Halt-conditions H1).

#### DI extension

**`DependencyInjection/PeopleFoundationServiceCollectionExtensions.cs`**:

```csharp
public static class PeopleFoundationServiceCollectionExtensions
{
    public static IServiceCollection AddBlocksPeopleFoundation(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryPartyRepository>();
        services.AddSingleton<IPartyReadModel>(sp => sp.GetRequiredService<InMemoryPartyRepository>());
        services.AddSingleton<IPartyWriteService>(sp => sp.GetRequiredService<InMemoryPartyRepository>());
        services.AddSingleton<IPartyEventPublisher, InMemoryPartyEventPublisher>();
        // TimeProvider + ITenantContextAccessor + IActorPrincipalResolver are
        // assumed registered upstream by foundation-multitenancy / kernel-audit.
        // If absent, the consumer's DI bootstrap surfaces the missing dep.
        return services;
    }
}
```

The two-overload pattern (audit-disabled / audit-enabled both-or-neither) per the cohort discipline is **not yet required** in this foundation slice because audit is not yet wired (deferred per H1). When audit lands, an overload `AddBlocksPeopleFoundation(this IServiceCollection services, Action<PeopleFoundationOptions> configure)` will land in a follow-on PR.

#### Tests (PR 3)

`tests/PartyReadModelTests.cs`:
- `GetByIdAsync_Found_ReturnsParty`.
- `GetByIdAsync_Tombstoned_ReturnsNull`.
- `GetByIdAsync_CrossTenant_ReturnsNull` (tenant-isolation; the row exists under tenant B; caller under tenant A gets null).
- `GetManyAsync_OmitsMissing`.
- `GetDisplayNameAsync_OnTombstoned_ReturnsNull`.
- `FindByExactEmailAsync_CaseInsensitive`.
- `FindByExactDisplayNameAsync_CaseInsensitive`.
- `GetRolesAsync_OmitsEndedRoles` (only returns rows where `EndedAt == null`).
- `HasRoleAsync_True_WhenActive`.
- `HasRoleAsync_False_WhenEnded`.
- `GetTaxIdAndAddressAsync_ReturnsTuple_WhenAvailable`.
- `GetTaxIdAndAddressAsync_ReturnsNull_WhenNoTaxId`.

`tests/PartyWriteServiceTests.cs`:
- `CreateAsync_PersistsParty_EmitsCreatedEvent`.
- `CreateAsync_BumpsVersionTo1_AndSetsEnvelopeFields`.
- `UpdateAsync_DisplayName_BumpsVersion_AndEmitsUpdatedEvent`.
- `UpdateAsync_OnTombstoned_Throws`.
- `DeleteAsync_SetsTombstone_EmitsDeletedEvent`.
- `DeleteAsync_OnAlreadyDeleted_IsIdempotent`.
- `AttachRoleAsync_KnownCode_Succeeds_EmitsRoleAttached`.
- `AttachRoleAsync_UnknownButValidShapeCode_Succeeds_EmitsRoleAttached` (e.g., a future role like `"landlord"`).
- `AttachRoleAsync_UppercaseCode_Throws_ValidationFails`.
- `DetachRoleAsync_SetsEndedAt_EmitsRoleDetached`.
- `DetachRoleAsync_OnAlreadyDetached_Throws`.
- `AddEmailAsync_RFC5322Valid_Persists_EmitsEvent`.
- `AddPhoneAsync_E164Valid_Persists_EmitsEvent`.
- `AddAddressAsync_AlphaTwoCountry_Persists_EmitsEvent`.

`tests/EventCatalogTests.cs`:
- `EventTypes_AllStartWithPeoplePrefix`.
- `IdempotencyKeys_MatchCatalogPattern` (string-compare against expected derivation per §3.3).
- `ProducerCluster_AlwaysPeople`.

`tests/MultiTenantIsolationTests.cs`:
- `TenantA_CreatesParty_TenantB_CannotRead`.
- `TenantA_AttachesRole_TenantB_CannotReadRoles`.
- `TenantA_AddsEmail_TenantB_CannotFindByEmail`.

Total new tests this PR: ~25.

#### Verification

- `dotnet build` succeeds.
- All previous PR 1 + PR 2 tests pass.
- New tests pass.
- `_shared/engineering/cross-cluster-event-bus-design.md` §3.3 contains 8 new rows (or the equivalent canonical-name set after reconciliation). The catalog edit lives in the SAME PR.
- `grep -rln "IPartyReadModel\b" packages/blocks-financial-ar/ 2>/dev/null` — if the AR package has a local stub, note its method signatures for follow-on harmonization (the AR's local stub is NOT modified by this hand-off; the AR's own follow-on housekeeping PR relocates the stub).

---

### PR 4 — ERPNext importer integration + `apps/docs/blocks-people-foundation/overview.md`

**Estimated effort:** ~1.5–2h
**Scope:** add `IErpnextPartyImporter` interface (Pass 0 of the ERPNext migration); implement ERPNext Customer doctype + Supplier doctype → Party + role mapping; ship the cluster docs page
**Commit subject:** `feat(blocks-people-foundation): add IErpnextPartyImporter (Customer+Supplier doctype mapping) + cluster docs page`
**Depends on:** PR 3 merged
**Branch:** `cob/blocks-people-foundation-importer-docs`

#### Context — Pass 0 placement

Per the migration-importer spec (`_shared/engineering/erpnext-to-anchor-migration-importer-spec.md`, sibling deliverable 2026-05-16), the ERPNext-to-Anchor migration runs in passes:

- **Pass 0 — Parties (Customers + Suppliers).** Maps ERPNext `Customer` + `Supplier` doctypes to canonical `Party` + `PartyRole`. Predecessor to Passes 1–3 (because the AR/AP passes resolve customer/supplier names to Party IDs).
- **Pass 1 — Accounts.** `IErpnextAccountImporter` per the ledger hand-off PR 6.
- **Pass 2 — Sales Invoices.** `IErpnextSalesInvoiceImporter` per the AR hand-off PR 6.
- **Pass 3 — Opening-balance JEs.** `IErpnextJournalEntryImporter` per the ledger hand-off PR 6.

This hand-off ships Pass 0. **The orchestrator** (in `tooling/anchor-import/` or equivalent) consumes Pass 0 in a future hand-off; this PR only ships the integration point.

#### New types

**`Migration/ErpnextPartySource.cs`** — source-shape record for ERPNext Customer + Supplier doctypes (unified; the doctype is captured in the `Doctype` field):

```csharp
public sealed record ErpnextPartySource(
    string Name,                    // ERPNext "name" — stable id (e.g., "CUST-2024-001")
    string Modified,                // ERPNext "modified" — version key (ISO timestamp)
    string Doctype,                 // "Customer" | "Supplier"
    string DisplayName,             // ERPNext customer_name or supplier_name
    string? LegalName,
    string? TaxId,                  // ERPNext tax_id (EIN/SSN for 1099)
    string? Email,                  // ERPNext email_id (primary)
    string? Phone,                  // ERPNext mobile_no (E.164-normalized at source if available)
    ErpnextPartyAddressSource? PrimaryAddress,
    bool IsDisabled,                // ERPNext disabled flag
    PartyKind Kind);                // inferred from doctype + customer_type field

public sealed record ErpnextPartyAddressSource(
    string Line1,
    string? Line2,
    string City,
    string Region,                  // ERPNext state
    string PostalCode,              // ERPNext pincode
    string Country);                // ERPNext country (normalized to ISO 3166-1 alpha-2)
```

**`Migration/ErpnextPartyImporterResult.cs`** — outcome envelope, mirroring `ImportOutcome<T>` from the ledger hand-off:

```csharp
public sealed record ImportPartyOutcome(
    Party Party,
    PartyRole? Role,                // null if doctype maps to a kind without an automatic role
    ImportAction Action,            // Inserted | Updated | Skipped
    string? Detail);

public enum ImportAction { Inserted, Updated, Skipped }
```

#### New interface — `IErpnextPartyImporter`

```csharp
public interface IErpnextPartyImporter
{
    /// <summary>
    /// Upserts a Party + role from an ERPNext source record. Idempotent on
    /// (source.Name, source.Modified). The doctype determines the role-name:
    /// "Customer" → "customer"; "Supplier" → "vendor". If the source maps to
    /// an unsupported doctype, returns Skipped with a Detail string.
    /// </summary>
    Task<ImportPartyOutcome> UpsertFromErpnextAsync(
        ErpnextPartySource source,
        CancellationToken cancellationToken = default);
}
```

#### Implementation — `ErpnextPartyImporter`

Per-record flow:

1. Look up existing Party where `Party.DisplayName == source.DisplayName` (case-insensitive) via `IPartyReadModel.FindByExactDisplayNameAsync`. (Future Pass 0 enhancement: match on `ExternalRef == source.Name`; for v1 this hand-off's Party doesn't carry `ExternalRef` as a first-class field — flagged as a known limitation in PR 4 description. ERPNext name match by DisplayName is the v1 path. **If multiple Parties match the same DisplayName**, returns `Skipped` with `Detail = "ambiguous-display-name"`.)
2. If no existing Party → CREATE via `IPartyWriteService.CreateAsync(...)`. Then add email/phone/address sub-entities via the corresponding write-service methods. Attach role `"customer"` or `"vendor"` per doctype. Return `Inserted`.
3. If existing Party → check whether `source.Modified` is newer than the Party's `UpdatedAt`. If newer, update mutable fields via `IPartyWriteService.UpdateAsync(...)`. Return `Updated` or `Skipped` accordingly.

**Doctype → role mapping table:**

| Doctype | RoleName | PartyKind |
|---|---|---|
| `Customer` | `"customer"` | inferred from ERPNext `customer_type` (`"Individual"` → Person; `"Company"` → Organization) |
| `Supplier` | `"vendor"` | inferred from ERPNext `supplier_type` (`"Individual"` → Person; `"Company"` → Organization) |
| (other doctypes) | (returns Skipped) | — |

**Note on `Customer.tax_id` and `Supplier.tax_id`:** ERPNext stores tax IDs in plain text on the doctype. This hand-off ships them on `Party.TaxId` UNENCRYPTED (per §Halt-conditions H4 + the per-field TODO comment). When PII encryption-at-rest lands, the migration importer will re-encrypt during the encryption-rollout follow-on workstream. v1 importer behavior: copy the value through verbatim.

#### Tests (PR 4)

`tests/ErpnextPartyImporterTests.cs`:
- `Upsert_NewCustomer_InsertsPartyWithRoleCustomer`.
- `Upsert_NewSupplier_InsertsPartyWithRoleVendor`.
- `Upsert_CustomerIndividual_SetsKindPerson`.
- `Upsert_CustomerCompany_SetsKindOrganization`.
- `Upsert_SupplierIndividual_SetsKindPerson`.
- `Upsert_SupplierCompany_SetsKindOrganization`.
- `Upsert_DuplicateDisplayName_ReturnsSkipped_Ambiguous`.
- `Upsert_SameVersion_ReturnsSkipped`.
- `Upsert_HigherVersion_ReturnsUpdated`.
- `Upsert_LowerVersion_ReturnsSkipped` (clock-drift).
- `Upsert_DisabledTrue_SetsTombstoneOnParty` (Soft-deletes via DeleteAsync with reason="erpnext-disabled").
- `Upsert_UnknownDoctype_ReturnsSkipped`.
- `Upsert_WithEmail_AddsEmailSubEntity`.
- `Upsert_WithPhone_AddsPhoneSubEntity`.
- `Upsert_WithAddress_AddsAddressSubEntity`.
- `Upsert_TaxIdPassedThroughVerbatim` (regression: v1 does NOT encrypt; verify the value round-trips raw).

Total new tests this PR: ~16.

#### DI registration

Extend `PeopleFoundationServiceCollectionExtensions.AddBlocksPeopleFoundation()`:

```csharp
services.AddSingleton<IErpnextPartyImporter, ErpnextPartyImporter>();
```

#### Docs

**`apps/docs/blocks-people-foundation/overview.md`** — cluster docs page following the established convention (cite ADR 0088 §1; cite `party-model-convention.md`; cite `cross-cluster-event-bus-design.md` §3.3; document the v1 scope + deferred features explicitly).

Structure:

```
# blocks-people-foundation

Canonical Party identity + role registry + sub-entity surface for the
Sunfish Anchor people cluster (Phase 3 foundation slice).

## Overview

This package is the substrate of `blocks-people-*` per ADR 0088 §1. It
provides:

- `Party` — the canonical actor identity (person OR organization).
- `PartyRole` — joins a Party to one or more role-extension entities in
  consumer clusters (Customer/Tenant/Vendor/Contractor/Employee).
- `EmailAddress` / `PhoneNumber` / `PartyAddress` — append-only sub-
  entities under Party (per `crdt-friendly-schema-conventions.md` §4).
- `IPartyReadModel` — the cross-cluster read interface. Consumers in
  `blocks-financial-*`, `blocks-property-*`, `blocks-work-*`, and
  `blocks-docs-*` access Party data ONLY through this interface.
- `IPartyWriteService` — write surface for create / update / soft-delete /
  role attach / role detach / contact-method add. Every write emits a
  cross-cluster `People.*` event per
  `_shared/engineering/cross-cluster-event-bus-design.md` §3.3.
- `IErpnextPartyImporter` — Pass 0 of the ERPNext-to-Anchor migration;
  consumes ERPNext Customer + Supplier doctypes; predecessor to the
  ledger (Pass 1) + AR (Pass 2) + opening-JE (Pass 3) passes.

## What's in v1 (this package)

- Party identity + role-registry + sub-entity machinery.
- 5 canonical role-name codes: `customer`, `tenant`, `vendor`,
  `contractor`, `employee`.
- ERPNext importer Pass 0 (Customer + Supplier doctype).

## What's NOT in v1 (deferred)

- `Employee` + `Compensation` + `Leave` + `Shift` + `Position` + onboarding
  + training entities — `blocks-people-hr-*` follow-on.
- `Lead` + `Opportunity` + `Campaign` + `Activity` — `blocks-people-crm-*`
  follow-on.
- `PartyRelationship` (e.g., spouse-of, parent-org-of, emergency-contact-
  for) — separate follow-on workstream.
- Party dedup (fuzzy match + manual merge UI) — separate follow-on.
- Background-check workflow — separate follow-on, vendor-selection-blocked.
- Self-service portal — Phase 4 per ADR 0088 Appendix B.
- Role-extension entities (`Customer` / `Tenant` / `Vendor` / `Contractor`
  / `Employee`) — owned by their respective consumer clusters per
  `party-model-convention.md` §3.

## Privacy + PII posture (v1)

- `Party.TaxId` and `Party.DateOfBirth` ship UNENCRYPTED in v1, with TODO
  comments per W#37 / ADR 0068.
- UI redaction is the consumer's responsibility for v1.
- The full encryption-at-rest substrate (Stronghold/DPAPI via W#60 P4 PR1)
  is wired in a follow-on workstream; the public API surface does NOT
  change at that point — only the on-disk format.

## Quickstart

```csharp
// DI registration
services.AddBlocksPeopleFoundation();

// Create a Party + attach a customer role
var party = await partyWriter.CreateAsync(
    PartyKind.Person, "Doe, Jane");
await partyWriter.AddEmailAsync(party.Id, "jane@example.com", "personal", isPrimary: true);
await partyWriter.AttachRoleAsync(party.Id, PartyRoleName.Customer, customerExtensionId);

// Cross-cluster read from blocks-financial-ar
var displayName = await partyReader.GetDisplayNameAsync(party.Id);
```

## Conventions applied

- `party-model-convention.md` §2 (Party shape), §3 (PartyRole), §4
  (cross-cluster references), §6 (multi-tenant isolation), §7 (privacy).
- `crdt-friendly-schema-conventions.md` §1 (ULID), §2 (tombstone soft-
  delete), §3 (version + revisionVector), §4 (append-only sub-collections),
  §5 (stable string codes), §13 (envelope), §14 (tenant isolation).
- `cross-cluster-event-bus-design.md` §1 (envelope), §2 (naming), §3.3
  (People-domain catalog).

## Related

- `blocks-financial-ar` (consumer; references Party via canonical
  IPartyReadModel — relocation from local stub when this lands).
- `blocks-property-leases` (consumer; canonical Tenant retrofit replaces
  legacy `blocks-leases.Party` post-this-hand-off).
- `blocks-work-*` (consumer; references Party from WorkOrder + Project).
- `blocks-docs-*` (consumer; references Party from Document.ownerId +
  SigningParty + DocumentPermission).
- `blocks-people-hr-*` / `blocks-people-crm-*` / `blocks-people-scheduling-*`
  (Phase 3 follow-on slices that compose on top of this foundation).
```

#### Verification

- All previous PR 1–3 tests pass.
- New PR 4 tests pass.
- `apps/docs/blocks-people-foundation/overview.md` renders without broken
  links (relative paths to ADR + Stage 02 doc + convention docs all
  resolve).
- `dotnet build` succeeds.

---

## CRDT-friendly schema conventions applied

This hand-off applies the canonical conventions per `_shared/engineering/crdt-friendly-schema-conventions.md`. The relevant patterns for this hand-off:

### 1. ULID identifiers throughout

Per §1 + `blocks-people-schema-design.md` §3 ("ULID — sortable, conflict-free across nodes; no auto-increment integer keys"): `PartyId`, `EmailAddressId`, `PhoneNumberId`, `PartyAddressId`, `PartyRoleId` are all ULID-backed. The existing `blocks-leases.PartyId` uses `Guid.NewGuid()` — a pre-CRDT-conventions choice; this hand-off ships the canonical ULID-backed `PartyId` under the `Sunfish.Blocks.People.Foundation` namespace, distinct from the legacy type. The retrofit hand-off (out of scope here) handles aliasing.

### 2. Soft-delete tombstones — never DELETE

Per §2: every entity in this hand-off (Party, EmailAddress, PhoneNumber, PartyAddress, PartyRole) carries `DeletedAt` / `DeletedBy` / `DeletedReason`. Hard `DELETE` is forbidden at the repository layer. Right-to-be-forgotten (per `party-model-convention.md` §7) is a special tombstone with `DeletedReason = "right-to-be-forgotten"` plus PII-null on the same row — that path is **stub-only in v1** (the PII-null logic is a follow-on workstream; the tombstone-with-reason path is wired through PR 3's `DeleteAsync`).

### 3. Append-only sub-collections (binding)

Per §4: `EmailAddress` / `PhoneNumber` / `PartyAddress` are append-only sub-collections under Party. Updating Jane's mobile number is **NOT** a row update — it's an INSERT of a new row + a marker (`ReplacedAt = now()` + `IsPrimary = false`) on the old row. The repository's `AddEmailAsync` / `AddPhoneAsync` / `AddAddressAsync` methods enforce this; there is no `UpdateEmailAsync` / `UpdatePhoneAsync` method on `IPartyWriteService`. Documented in the XML doc comments on the write-service interface.

### 4. Stable string codes (binding)

Per §5: `PartyKind` ("person" | "organization") and `PartyRole.RoleName` ("customer" | "tenant" | "vendor" | "contractor" | "employee" — plus open-set future codes from follow-on hand-offs) are stable string codes. No integer-backed enums. The deprecation discipline binds: if a role name ever needs to change, a NEW code is added; the old code is retained-with-deprecation. This hand-off ships the foundational 5; follow-on hand-offs add codes additively.

### 5. Per-tenant isolation (binding)

Per §14 + `party-model-convention.md` §6: every entity carries `TenantId`. The in-memory repository enforces isolation at the `(TenantId, EntityId)` tuple-key level. A future SQLite repository will enforce it via `WHERE tenant_id = ?` clauses on every query. Cross-tenant Party-merge is **forbidden** (the merge logic in convention §5 validates same-tenant before proceeding); cross-tenant correspondence is a Bridge-tier concern (per convention §10 Q7).

### 6. Posted-then-immutable does NOT apply to Party

Per §6: the posted-then-immutable pattern applies to financial-ledger journal entries + docs-cluster document versions, not to Party. Party is **mutable identity**: name changes, address changes, phone-number changes are normal lifecycle. What IS immutable in this hand-off: `PartyRole` rows (mutated only via `EndedAt` tombstone-style detach) and the 8 `People.*` event records (event log per event-bus design §1 is append-only). The Party row itself supports field updates throughout its life.

### 7. State-machine resolution

Per §7: Party has no rich state machine in this slice — just `live` vs `deletedAt`-tombstoned. The tombstone transition is **Pattern C (idempotent application)**: tombstoning an already-tombstoned row is a no-op (verified in PR 3's `DeleteAsync_OnAlreadyDeleted_IsIdempotent` test). Role attach/detach is **Pattern A (append-only)** — every attach is a new row; every detach is a tombstone-style `EndedAt` update.

---

## License posture

### Borrowed-with-attribution (permissive)

- **Apache OFBiz** `party` module (Apache 2.0) — the cluster's entity shapes (`Party` + `PartyRole` + sub-entity collections; the Customer/Vendor/Contractor/Employee role-record pattern) derive from OFBiz's `party` module per `blocks-people-schema-design.md` §2 + `party-model-convention.md` §0–§1 + ADR 0088 Appendix A.

**Attribution requirements:**

1. The package's `Sunfish.Blocks.People.Foundation.csproj` carries a `<NOTICEFile>NOTICE.md</NOTICEFile>` reference.
2. `packages/blocks-people-foundation/NOTICE.md` (new file in PR 1) states:

```markdown
# NOTICE — Sunfish.Blocks.People.Foundation

This package's entity shapes (Party + PartyRole + EmailAddress + PhoneNumber
+ PartyAddress + the role-record pattern with cross-cluster role-extension
entities) derive from Apache OFBiz's `party` entity model
(<https://ofbiz.apache.org/>, Apache 2.0 license).

OFBiz version studied: v18.12.x (as of 2026-05-16).

The Sunfish implementation is original code, distributed under the
MIT License. The OFBiz entity-shape pattern is reproduced with
attribution per Apache 2.0 §4(c) of the OFBiz License.
```

3. Source-header comments on `Party.cs`, `PartyRole.cs`, and the three sub-entity files reference OFBiz in a one-line comment per the cohort precedent (ledger hand-off PR 1 + PR 2).

### Clean-room only (copyleft) — ERPNext doctype reference

Per `blocks-people-schema-design.md` §2 + ADR 0088 §3, ERPNext + Frappe (GPLv3) are **read-only / data-format-only** sources. The Pass 0 importer (PR 4) consumes ERPNext Customer + Supplier doctype data as a **data format**, not as code:

- The `ErpnextPartySource` record's field names mirror ERPNext doctype JSON field names verbatim (e.g., `Name`, `Modified`, `customer_name`, `mobile_no`). Field-name parity is required for clean import; this is **schema borrowing for data interoperability**, not code derivation. Per the clean-room discipline, no ERPNext source code was read in producing this hand-off.
- The doctype-to-PartyKind mapping logic (`Customer.customer_type == "Individual"` → `PartyKind.Person`) is original; the values borrowed are the ERPNext-side enum codes, not the mapping algorithm.

### Clean-room only (copyleft) — other studied sources

Per `blocks-people-schema-design.md` §2 + ADR 0088 §3, the following sources contribute NO code to this hand-off:

- **OrangeHRM** (GPLv3) — HRMS module structure; not consumed in this foundation slice (HR is a Phase 3 follow-on).
- **EspoCRM** + **SuiteCRM** + **Mautic** (GPLv3 / AGPLv3) — CRM patterns; not consumed in this foundation slice (CRM is a Phase 3 follow-on).
- **Cal.com** + **Easy!Appointments** (AGPLv3 / GPLv3) — scheduling; not consumed in this foundation slice.

**Discipline check before merging any PR in this hand-off:**

1. No copyleft (GPL/AGPL) code was opened in any editor session that produced this hand-off's PRs.
2. No identifier names from any GPL/AGPL source appear in the new code beyond ERPNext doctype field names (which are data-format only, not algorithms).
3. The clean-room schema in `blocks-people-schema-design.md` §3 + the `party-model-convention.md` §2 are the source of truth for type shapes; deviations require XO ratification.

### Sunfish output

**All code authored under this hand-off is MIT-licensed**, per ADR 0088 §2 and the project-wide license posture.

---

## Test plan

### Per-PR minima (summary; details under each PR above)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (Party + sub-entities + validators) | ~14–16 | construction + validation + JSON round-trip + envelope |
| PR 2 (PartyRole registry + role codes) | ~12 | role-name shape rules; known vs unknown codes; ended-after-started invariant |
| PR 3 (services + repository + events + DI) | ~25 | read interface + write service + 8 events + multi-tenant isolation + idempotency |
| PR 4 (importer + docs) | ~16 | doctype mapping; kind inference; ambiguous match; idempotency; sub-entity sideloading |
| **Total** | **~67** | (the original budget called for ~30–35; the foundation expanded as the multi-tenant + event-emission surface was scoped — call ~50 the rough floor) |

**Note on the test-count band:** the hand-off's stated budget was `~30–35 tests`. After scoping the multi-tenant isolation + event-catalog reconciliation + ERPNext doctype variants explicitly, the realistic floor is ~50 tests. The ~67 estimate above is the upper bound. **If COB hits ~50 with full coverage of the §PASS gate, that's sufficient.** Do not pad tests just to hit a higher number; do not skip coverage just to hit a lower number.

### Cluster-level acceptance (PASS gate at end of PR 4)

**A1.** `dotnet build` succeeds on the new `Sunfish.Blocks.People.Foundation` package and every downstream consumer that has been updated (none in this hand-off; AR's relocation is the AR's own follow-on PR).

**A2.** `dotnet test packages/blocks-people-foundation/tests/` passes ~50–67 tests across all 4 PRs.

**A3.** A Party with full canonical envelope can be created, queried by ID, queried by display-name, role-attached, sub-entity-extended (email + phone + address), tombstoned, and verified that:
- Each write emits exactly one `People.*` event (verified via `InMemoryPartyEventPublisher.Recorded`).
- Tombstoned Party is invisible to `GetByIdAsync` (returns null).
- Cross-tenant query for the same Party ID returns null (multi-tenant isolation).

**A4.** Role attach + detach lifecycle:
- `AttachRoleAsync(partyId, "customer", customerExtensionId)` creates a PartyRole row with `EndedAt == null` + emits `People.RoleAttached`.
- `HasRoleAsync(partyId, "customer")` returns `true`.
- `DetachRoleAsync(roleId, "no-longer-customer")` updates the existing row to set `EndedAt = now()` + emits `People.RoleDetached`.
- `HasRoleAsync(partyId, "customer")` returns `false`.
- `GetRolesAsync(partyId)` excludes the detached role.

**A5.** Append-only sub-collection: adding a new email with `IsPrimary = true` does NOT mutate the prior primary email's `Address` field; instead it INSERTS a new row with `IsPrimary = true` and the existing row's `ReplacedAt` is set + `IsPrimary` flipped to `false` (the only allowed mutation on a sub-entity row, per CRDT §4 "deletedAt IS allowed; tombstoned row remains" — and `replacedAt` follows the same allowance pattern).

**A6.** ERPNext importer Pass 0:
- `UpsertFromErpnextAsync({Doctype: "Customer", DisplayName: "Acme Corp"})` returns `Inserted` with a Party of kind Organization + a PartyRole(`customer`).
- Re-call with same `Modified` → `Skipped`.
- Re-call with newer `Modified` + changed `Email` → `Updated` (Party updated; new EmailAddress added; old EmailAddress marked `replacedAt`).
- `UpsertFromErpnextAsync({Doctype: "Supplier", ...})` returns `Inserted` with a PartyRole(`vendor`).

**A7.** Multi-tenant isolation: a Party created in tenant A is invisible to a caller in tenant B for every method on `IPartyReadModel` (verified by `MultiTenantIsolationTests`).

**A8.** Event-catalog reconciliation: `_shared/engineering/cross-cluster-event-bus-design.md` §3.3 contains rows for all 8 events emitted by this package (or their canonical-name equivalents post-reconciliation per PR 3 step 1). Idempotency-key column matches the keys produced by the implementation.

**A9.** `apps/docs/blocks-people-foundation/overview.md` published + renders.

**A10.** `active-workstreams.md` row for "blocks-people-foundation" updated with `built` status + the 4 PR numbers.

When the PASS gate is met, the next hand-offs in the dependent path can proceed:

- **`blocks-property-leases-party-retrofit-stage06-handoff.md`** — Critical follow-on. `[Obsolete]`-marks the legacy `blocks-leases.Party` + `PartyKind` + `PartyId`; ships aliases; backfills existing leases data. The retrofit hand-off introduces additional role codes (`"landlord"`, `"manager"`, `"guarantor"`) — additively, not as replacements.
- **`blocks-financial-ar` follow-on housekeeping PR** — relocates the AR package's local `IPartyReadModel` stub to reference the canonical interface in this foundation. One-line `using` change + delete the local stub. Owned by the AR hand-off's follow-on, NOT this foundation.
- **Future `blocks-people-hr-*` hand-offs** — ship the Employee + Compensation + Leave + Shift + Position + onboarding + training entities on top of this foundation.
- **Future `blocks-people-crm-*` hand-offs** — ship the Lead + Opportunity + Campaign + Activity entities on top of this foundation.

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*` beacon to `coordination/inbox/`:

### H1. `IActorPrincipalResolver` boundary (PR 3)

Per `party-model-convention.md` §10 Q6: `PartyRole` is a business-domain concept; `IActorPrincipalResolver` (W#1 / PR #675) is a security-tier concept. The two layers are distinct.

PR 3's `InMemoryPartyRepository` calls `IActorPrincipalResolver.GetCurrentPartyId()` (or equivalent) to populate `CreatedBy` / `UpdatedBy` / `DeletedBy` on every write. **If `IActorPrincipalResolver` is not yet wired into the DI substrate** (verify via `grep -rln "IActorPrincipalResolver" packages/kernel-audit/`):

- **Fallback path 1:** if there's a placeholder `IActorPrincipalResolver` registered (e.g., one that returns a fixed "system" Party ID), use it. Add a `// TODO: replace with real actor-principal substrate once kernel-audit settles` comment.
- **Fallback path 2:** if no such interface exists at all, ship a local `IPartyContext.GetCurrentPartyId()` stub interface in this package, with an `InMemoryPartyContext` default that returns a fixed sentinel Party ID. Document the stub in PR 3 + the docs page. File `cob-question-2026-05-XXTHH-MMZ-w60-p4-people-foundation-actor-principal-wiring.md` to surface the gap to XO.

**Do NOT invent a new actor-principal substrate** in this hand-off. The W#1 work owns that surface.

### H2. Multi-tenant isolation boundary (any PR)

Per `crdt-friendly-schema-conventions.md` §14 + `party-model-convention.md` §6: every entity has `TenantId`; cross-tenant reads/writes are forbidden.

`InMemoryPartyRepository` uses `ITenantContextAccessor.Current` to filter every query. **If `ITenantContextAccessor` is not yet available** at PR 3 time:

- Search `grep -rln "ITenantContextAccessor\|ITenantContext\b" packages/foundation-multitenancy/` to find the current interface name.
- If the interface exists but has a different name, use that name and document in PR 3.
- If no tenant-context accessor exists in foundation-multitenancy at all, file `cob-question-2026-05-XXTHH-MMZ-w60-p4-people-foundation-tenant-context.md` BEFORE shipping PR 3. The multi-tenant isolation guarantee is binding (per W#37 / ADR 0068) — do not ship the foundation without it.

**Verify multi-tenant isolation in tests EXPLICITLY** (see PR 3 test list: `MultiTenantIsolationTests`). The test must demonstrate that a Party created under tenant A cannot be read via `GetByIdAsync(id)` from tenant B, even with the correct ID.

### H3. `blocks-leases.Party` migration is OUT OF SCOPE

This hand-off **must NOT modify** any file under `packages/blocks-leases/`. The legacy `Party` + `PartyKind` + `PartyId` in that package are pre-convention deviations; the canonical replacement ships here under `Sunfish.Blocks.People.Foundation` and the retrofit is a SEPARATE hand-off (`blocks-property-leases-party-retrofit-stage06-handoff.md`) that will:

1. `[Obsolete]`-mark the legacy types.
2. Ship aliases from the legacy `Sunfish.Blocks.Leases.Models.PartyId` to the canonical `Sunfish.Blocks.People.Foundation.PartyId` (via a `[Obsolete]` `record struct` projection).
3. Backfill: each existing `blocks-leases.Party` row creates a canonical Party row + appropriate PartyRole row (`tenant` / `landlord` / `manager` / `guarantor` per the row's `Kind`).
4. Introduce new role codes (`landlord`, `manager`, `guarantor`) additively in the foundation's `PartyRoleName` static class.

**If during PR 1–4 you encounter a temptation to "just clean up" `blocks-leases.Party`:** RESIST. Open the retrofit hand-off file (XO will draft it after PR 4 merges) and let it do the work.

**If the AR hand-off's local `IPartyReadModel` stub creates a build break** (e.g., AR ships first and references the canonical interface that does not yet exist in this foundation): file `cob-question-2026-05-XXTHH-MMZ-w60-p4-ar-foundation-ordering.md` to verify ordering. Recommended resolution: this foundation ships first; the AR local stub is the "compatible-shape predecessor" and continues working until AR's own follow-on housekeeping PR relocates the `using` directive.

### H4. PII encryption-at-rest is OUT OF SCOPE in v1

Per W#37 / ADR 0068 (Proposed): tenant-security-policy with per-tenant envelope keys. The Stronghold/DPAPI substrate (W#60 P4 PR1) is the encryption-at-rest implementation. Per `party-model-convention.md` §7: `Party.TaxId` + `Party.DateOfBirth` should be encrypted at rest.

**This foundation v1 ships these fields UNENCRYPTED** with TODO comments:

```csharp
public LocalDate? DateOfBirth { get; init; }    // TODO: encrypt-at-rest per W#37/ADR-0068
public string? TaxId { get; init; }             // TODO: encrypt-at-rest per W#37/ADR-0068
```

The migration to encrypted-at-rest is a follow-on workstream paired with the W#60 P4 PR1 Stronghold/DPAPI substrate landing + ADR 0068 advancing to Accepted. The public API surface does NOT change at that point; only the on-disk format.

**If COB feels strongly about gating PR 1 on encryption being available**, file `cob-question-2026-05-XXTHH-MMZ-w60-p4-people-foundation-pii-encryption-gating.md`. XO recommendation: ship without encryption now; the AR + property-leases retrofits need this foundation regardless. Encryption is an additive future workstream.

### H5. `PartyRelationship` deferral

This hand-off does **not** ship `PartyRelationship` (the actor-to-actor edge entity per `party-model-convention.md` §3 — "spouse-of", "parent-org-of", "emergency-contact-for", "contact-at"). Convention §10 Q3 + §10 Q5 cover relationship semantics; both flagged as ratification questions.

**If any consumer cluster's pending hand-off needs PartyRelationship in this hand-off's scope window:** file `cob-question-2026-05-XXTHH-MMZ-w60-p4-people-foundation-relationship-deferral.md` rather than adding PartyRelationship here. XO will either (a) explicitly extend this hand-off's scope, or (b) sequence a follow-on `blocks-people-foundation-relationships-*` hand-off.

The retrofit hand-off (`blocks-property-leases-party-retrofit-*`) may itself need `PartyRelationship` for household-member-of semantics on joint leases — that's the retrofit's call, not this foundation's. If the retrofit hand-off (when it lands) requires foundation-side `PartyRelationship` support, this foundation's scope expands at THAT POINT; not preemptively.

### H6. Event-bus catalog reconciliation conflict (PR 3)

Per PR 3 step 1: read `_shared/engineering/cross-cluster-event-bus-design.md` §3.3 in full before authoring the event records. If the catalog already lists People-domain events with names that conflict with this hand-off's proposed names (e.g., the catalog has `People.PartyMerged` already and this hand-off proposes `People.PartyUpdated`), **use the catalog names**. Per event-bus design §2: "Do not rename existing event types."

If the conflict is more than cosmetic (e.g., the catalog's existing event has a different payload shape that's incompatible with this hand-off's use case): file `cob-question-2026-05-XXTHH-MMZ-w60-p4-people-foundation-event-catalog-conflict.md` and halt PR 3 until XO resolves.

### H7. ULID helper unavailable (PR 1)

If neither a project-local ULID helper NOR a permissive (BSD/MIT/Apache) third-party ULID NuGet package is acceptable in the project: file `cob-question-*`. XO recommendation: `Cysharp.Ulid` (BSD-3-Clause; permissive; widely used in .NET) is the default fallback. Reach for a project-local helper only if one already exists.

### H8. `Customer` extension entity placement question

Per `party-model-convention.md` §3 + §4: the `Customer` extension entity (with `arAccountId`, `defaultPaymentTermsId`, `creditLimit`, etc.) is owned by `blocks-financial-ar`, NOT `blocks-people-foundation`. The same is true for `Vendor` (placement per §10 Q1 — recommendation: people-cluster as a future follow-on hand-off; but this foundation does NOT pre-decide it), `Tenant` (owned by `blocks-property-leases` per the retrofit hand-off), `Contractor` (owned by `blocks-work-*`), `Employee` (owned by `blocks-people-hr-*` follow-on).

**If a consumer cluster's hand-off asks `blocks-people-foundation` to own the role-extension entity itself** (e.g., "ship the Customer entity here so AR doesn't have to define it"): file `cob-question-2026-05-XXTHH-MMZ-w60-p4-people-foundation-extension-entity-placement.md`. XO recommendation: REFUSE — `blocks-people-foundation` owns Party + role-registry; extension entities live in their consumer clusters per the convention.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–4 merged to main** (sequentially).
2. **Canonical Party identity available:** the `Party` record + `EmailAddress`/`PhoneNumber`/`PartyAddress` sub-entities + `PartyRole` + the 5 canonical role-name codes + write+read services + `InMemoryPartyRepository` are all present in `packages/blocks-people-foundation/`.
3. **`AddBlocksPeopleFoundation()` DI extension works:** a downstream consumer registering this extension can resolve `IPartyReadModel` + `IPartyWriteService` + `IPartyEventPublisher` + `IErpnextPartyImporter` from the DI container.
4. **Event-catalog reconciliation done:** `_shared/engineering/cross-cluster-event-bus-design.md` §3.3 contains rows for all 8 emitted events (or their canonical-name equivalents post-reconciliation), with idempotency keys matching the implementation.
5. **Multi-tenant isolation verified:** `MultiTenantIsolationTests` pass; the InMemoryPartyRepository enforces tenant-scoped reads at the boundary.
6. **Acceptance tests A1–A10 pass.**
7. **`apps/docs/blocks-people-foundation/overview.md` published.**
8. **`active-workstreams.md`** row for "blocks-people-foundation" updated (via the source W*.md file, not the ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`) with `built` status + the 4 PR numbers.
9. **Tests pass:** ~50–67 tests across the package.
10. **No `[Obsolete]` or modification on `packages/blocks-leases/Models/Party.cs` or siblings** (verified by `git diff main..HEAD -- packages/blocks-leases/`).

When the PASS gate is met, the next hand-offs in the Phase 1 critical path / Phase 3 follow-on path can proceed:

- `blocks-property-leases-party-retrofit-stage06-handoff.md` (immediate next; XO authors after PR 4 merges).
- `blocks-financial-ar` housekeeping PR to relocate its local `IPartyReadModel` stub (AR hand-off's responsibility; not this hand-off's).
- Future `blocks-people-hr-*` / `blocks-people-crm-*` / `blocks-people-scheduling-*` hand-offs (Phase 3 follow-ons).

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-16):**

- `packages/blocks-leases/Models/Party.cs` (pre-convention deviation; cited in §Halt-conditions H3; DO NOT TOUCH) ✓
- `packages/blocks-leases/Models/PartyKind.cs` (same) ✓
- `packages/blocks-leases/Models/PartyId.cs` (same) ✓
- `packages/foundation-multitenancy/` (target of `TenantId` reference; verify in pre-build step 6) ✓
- ADR 0088 §1 (Path II + 7-cluster decomposition; `blocks-people-*` Phase 3 placement) ✓
- `_shared/engineering/party-model-convention.md` §2, §3, §4, §6, §7 ✓
- `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §2, §3, §4, §5, §13, §14 ✓
- `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3.3 ✓
- `icm/02_architecture/blocks-people-schema-design.md` §3.1, §3.5, §6, §7 ✓
- `icm/02_architecture/blocks-property-party-alignment-review.md` §2.11 (gating reason; pre-convention `blocks-leases.Party` deviation context) ✓
- `icm/_state/handoffs/blocks-financial-ar-stage06-handoff.md` (the AR hand-off whose local `IPartyReadModel` stub this foundation supersedes; cited at lines 1593–1617 of that hand-off + the AR Cited-symbol verification noting "stub interfaces for cross-cluster contracts not yet shipped") ✓
- `icm/_state/handoffs/blocks-financial-ledger-chart-and-journal-stage06-handoff.md` (cohort precedent for 6-PR substrate pattern + importer entry-point pattern) ✓

**Introduced by this hand-off** (ship across PRs 1–4):

- New package: `packages/blocks-people-foundation/`
- New types: `PartyId`, `EmailAddressId`, `PhoneNumberId`, `PartyAddressId`, `PartyRoleId`, `PartyKind`, `Party`, `EmailAddress`, `PhoneNumber`, `PartyAddress`, `Address`, `PartyRole`, `PartyRoleName` (static class with 5 const codes + `All` set + `IsKnown`), `ValidationResult`, `ValidationError`, `IPartyWriteOptions`, `IPartyUpdate`
- New event types: `IPartyEvent` + `PartyCreatedEvent` + `PartyUpdatedEvent` + `PartyDeletedEvent` + `RoleAttachedEvent` + `RoleDetachedEvent` + `EmailAddressAddedEvent` + `PhoneNumberAddedEvent` + `AddressAddedEvent` + corresponding payload records
- New services: `IPartyReadModel`, `IPartyWriteService`, `IPartyEventPublisher` + `InMemoryPartyEventPublisher`, `InMemoryPartyRepository`, `IErpnextPartyImporter` + `ErpnextPartyImporter`
- New validators: `PartyValidator`, `EmailAddressValidator`, `PhoneNumberValidator`, `PartyAddressValidator`, `PartyRoleValidator`
- New DI extension: `PeopleFoundationServiceCollectionExtensions.AddBlocksPeopleFoundation()`
- New importer types: `ErpnextPartySource`, `ErpnextPartyAddressSource`, `ImportPartyOutcome`, `ImportAction`
- Docs: `apps/docs/blocks-people-foundation/overview.md`
- Attribution: `packages/blocks-people-foundation/NOTICE.md`
- Catalog edit: `_shared/engineering/cross-cluster-event-bus-design.md` §3.3 — 8 new rows (or canonical-name equivalents)

**Self-audit reminder (per ADR 0028-A10):** COB structurally verifies each cited symbol by reading the actual file before declaring AP-21 clean. Do not rely on grep-only verification. Per `feedback_council_can_miss_spot_check_negative_existence`: spot-check negative existence too (verify `Sunfish.Blocks.People.Foundation.Party` is genuinely absent from origin/main before shipping; verify `IActorPrincipalResolver` placement before PR 3).

---

## Cohort discipline

This hand-off is the **third cluster implementation hand-off under ADR 0088 Path II** (after `blocks-financial-ledger` + `blocks-financial-ar`) and the **first Phase 3 people-cluster slice**. The COB self-audit pattern applied to W#34 / W#35 / W#36 / W#39 / W#40 substrate hand-offs + the ledger + AR hand-offs applies here verbatim:

- **Two-overload constructor (audit-disabled / audit-enabled both-or-neither) pattern** for any DI extension that interacts with audit. NOT REQUIRED in this hand-off's v1 (audit is deferred per §Halt-conditions H1); applies when the actor-principal substrate wires in.
- **`AddBlocksPeopleFoundation()` naming for the DI extension** — matches the cluster convention.
- **`apps/docs/{cluster}/overview.md` page convention** — applied in PR 4.
- **README.md at the package root** referencing Stage 02 design + ADR 0088 + the Party-model convention — ship in PR 1.
- **`ConcurrentDictionary` dedup for any cache** — applied in `InMemoryPartyRepository`, `InMemoryPartyEventPublisher`.
- **Strong-typed Id records** (ULID-backed) — applied for `PartyId`, `EmailAddressId`, `PhoneNumberId`, `PartyAddressId`, `PartyRoleId`.
- **Stub interfaces for cross-cluster contracts not yet shipped** — NOT REQUIRED in this hand-off (this hand-off ships the canonical interfaces themselves); applies in reverse when consumers' local stubs relocate to reference this foundation.
- **Catalog reconciliation** for any cross-cluster event surface — applied in PR 3 step 1 (event-bus design §3.3).
- **Per-tenant isolation enforced at the repository boundary** — applied in `InMemoryPartyRepository` + verified in `MultiTenantIsolationTests`.

---

## Beacon protocol

If COB hits a halt-condition (H1–H8) or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w60-p4-people-foundation-{slug}.md` in `/Users/christopherwood/Projects/SunfishSoftware/coordination/inbox/`.
- Halt the workstream + add a note in the `active-workstreams.md` row for `blocks-people-foundation` (via the source W*.md file).
- `ScheduleWakeup 1800s`.

If COB completes PR 4 + the PASS gate is met:

- Update `active-workstreams.md` (via the source W*.md file, not the ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`).
- Drop `cob-status-2026-05-XXTHH-MMZ-w60-p4-people-foundation-built.md` to inbox.
- Continue with the next hand-off (likely `blocks-property-leases-party-retrofit-*` — XO authors this after PR 4 merges and confirms the canonical interfaces resolve cleanly from the leases consumer side).

---

## Cross-references

- **Canonical convention:** `_shared/engineering/party-model-convention.md` §2 (Party shape), §3 (PartyRole), §4 (cross-cluster references), §6 (multi-tenant isolation), §7 (privacy/PII), §10 Q1 (Vendor placement open question), §10 Q6 (PartyRole vs Principal boundary).
- **Stage 02 spec:** `icm/02_architecture/blocks-people-schema-design.md` §3.1 (Party canonical), §3 (PartyRole + sub-entities), §6 (Party-as-base pattern rationale), §7 (cross-cluster contracts).
- **CRDT conventions:** `_shared/engineering/crdt-friendly-schema-conventions.md` §1 (ULID), §2 (tombstones), §3 (version + revisionVector), §4 (append-only sub-collections), §5 (stable string codes), §13 (envelope), §14 (tenant isolation).
- **Event bus:** `_shared/engineering/cross-cluster-event-bus-design.md` §1 (envelope), §2 (naming), §3 (catalog upkeep), §3.3 (People-domain events).
- **Property-alignment review:** `icm/02_architecture/blocks-property-party-alignment-review.md` §2.11 (gating reason; pre-convention `blocks-leases.Party` deviation) + §6 H1 (highest-priority retrofit identified).
- **ADR 0088:** `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md` §1 (cluster grouping, Phase 3 placement), §2 (MIT output license), §3 (clean-room discipline), Appendix A (FOSS source survey — OFBiz attribution).
- **W#37 / ADR 0068 (Proposed):** tenant-security-policy with per-tenant envelope keys — referenced by the deferred PII encryption-at-rest path (§Halt-conditions H4).
- **W#1 / `IActorPrincipalResolver` (PR #675):** Principal layer (security-tier) separate from Party layer (business-domain); cited at §Halt-conditions H1.
- **Predecessor hand-offs (cohort precedent):**
  - `blocks-financial-ledger-chart-and-journal-stage06-handoff.md` (substrate 6-PR shape + importer entry-point pattern; direct precedent).
  - `blocks-financial-ar-stage06-handoff.md` (the local `IPartyReadModel` stub at lines 1593–1617 that this foundation supersedes; PR 6 importer pattern; PR 5 wrapper retrofit example).
  - `foundation-mission-space-stage06-handoff.md` (W#40 — 5-PR shape; DI extension pattern).
  - `foundation-versioning-stage06-handoff.md` (W#34 — substrate naming).
- **Sibling hand-offs (Phase 3 follow-ons, not yet authored):**
  - `blocks-property-leases-party-retrofit-stage06-handoff.md` (immediate follow-on; XO authors after PR 4 merges).
  - `blocks-people-hr-stage06-handoff.md` (Phase 3; Employee + Compensation + Leave + Shift + Position + onboarding + training).
  - `blocks-people-crm-stage06-handoff.md` (Phase 3; Lead + Opportunity + Campaign + Activity).
  - `blocks-people-scheduling-stage06-handoff.md` (Phase 3; rrule-based shift patterns).

---
