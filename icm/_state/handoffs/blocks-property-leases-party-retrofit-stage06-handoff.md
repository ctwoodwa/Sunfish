# Hand-off — `blocks-leases` Party canonical retrofit (replace local Party / PartyKind / PartyId with `blocks-people-foundation` types)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-16
**Status:** `ready-to-build` *(gate: blocks-people-foundation all 4 PRs merged to origin/main — see Hard prerequisites)*
**Workstream:** W#27 follow-on (Party type alignment)
**Spec source:**
- [`_shared/engineering/party-model-convention.md`](_shared/engineering/party-model-convention.md) §2 (Party shape), §3 (PartyRole), §4 (cross-cluster read interface `IPartyReadModel`), §7 (PII/privacy), §9 (Stage 06 discipline)
- [`icm/02_architecture/blocks-property-party-alignment-review.md`](../../02_architecture/blocks-property-party-alignment-review.md) §2.11 (leases pre-convention deviation), §6 (prerequisites H1)
- [`icm/_state/handoffs/blocks-people-foundation-stage06-handoff.md`](blocks-people-foundation-stage06-handoff.md) (predecessor — ships canonical Party, PartyId, IPartyReadModel)
**ADR:** No dedicated ADR. Authority is `party-model-convention.md` + ADR 0088 §1 (cluster grouping; `blocks-property-*` references `blocks-people-foundation` by ID). This retrofit is a conformance fix, not a design decision.
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~4–6h sunfish-PM (2 PRs + ~15–23 tests + docs + ledger note)
**PR count:** 2 PRs
**Pre-merge council:** NOT required. This is a type-swap retrofit with a clear canonical target and no new security surface. Standard COB self-audit applies to both PRs.
**Audit before build:**
```bash
# Gate check — all three must return results:
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-foundation/Sunfish.Blocks.People.Foundation.csproj
grep -rn "struct PartyId" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-foundation/
grep -rn "IPartyReadModel" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-foundation/
```

---

## Context

### What this hand-off fixes

`packages/blocks-leases/` ships three types that predate the canonical Party convention:

| Legacy type | File | Problem |
|---|---|---|
| `Sunfish.Blocks.Leases.Models.Party` | `Models/Party.cs` | 3 fields only (`Id`, `DisplayName`, `Kind`). The canonical Party has ~30 fields + sub-entities. Two different types named `Party` in the same solution is a dedup and identity hazard. |
| `Sunfish.Blocks.Leases.Models.PartyKind` | `Models/PartyKind.cs` | `Tenant` / `Landlord` / `Manager` / `Guarantor` — overlaps with the canonical `PartyRole.roleName` string-code registry but is a separate enum with no cross-cluster semantics. |
| `Sunfish.Blocks.Leases.Models.PartyId` | `Models/PartyId.cs` | Backed by `Guid.NewGuid()`. The canonical `PartyId` in `blocks-people-foundation` is ULID-backed (per CRDT convention §1). Wire format is incompatible. All callers creating `PartyId.NewId()` are generating a non-ULID identifier that cannot round-trip cleanly through the canonical people cluster. |

These types were introduced in W#27 (Phases 1–4) before the people-cluster canonical convention was established. Now that `blocks-people-foundation` ships the canonical `Party`, `PartyId`, and `IPartyReadModel`, the lease-local types must be deprecated and the consuming code updated.

### What is correct and MUST NOT change

**`LeaseHolderRole.cs` is correct and must stay as-is.** Its semantics are orthogonal to the canonical `PartyRole`:

- `PartyRole.roleName` (`"tenant"`, `"landlord"`, `"manager"`, `"guarantor"`) = what role does this Party play globally in the system's role registry (cross-cluster concept, owned by `blocks-people-foundation`).
- `LeaseHolderRole` (`PrimaryLeaseholder`, `CoLeaseholder`, `Occupant`, `Guarantor`) = what role does this party play *on this specific lease* (lease-local RBAC-style distinction, owned by `blocks-leases`).

The two concepts are not duplicates. `LeaseHolderRole` answers "is this person the primary financial party on this lease or a co-signer or a listed occupant?" — a per-lease-assignment question that has no analogue in the global role registry. Never merge or deprecate `LeaseHolderRole`.

**`LeasePartyRole.cs` structure is correct.** The join table correctly binds a `LeaseHolderRole` to a `PartyId` on a `LeaseId`. Only the `Party: PartyId` field's type changes from `Sunfish.Blocks.Leases.Models.PartyId` to `Sunfish.Blocks.People.Foundation.PartyId`. The record shape, the constructor, and the semantics are all correct.

**`Lease.cs` structure is correct.** `Tenants: IReadOnlyList<PartyId>` and `Landlord: PartyId` stay as they are; only the `PartyId` type changes. No changes to any other `Lease` field.

**`ILeaseService` method signatures** that accept `PartyId` (specifically `RecordPartySignatureAsync`) will have the `PartyId` parameter type updated to the canonical type. No other signature changes.

### What changes

After this retrofit:

1. `blocks-leases.csproj` gains a `<ProjectReference>` to `Sunfish.Blocks.People.Foundation.csproj`.
2. `Sunfish.Blocks.Leases.Models.Party` is `[Obsolete]`-marked (not deleted — deletion is a `sunfish-api-change` pipeline; this is the deprecation step).
3. `Sunfish.Blocks.Leases.Models.PartyKind` is `[Obsolete]`-marked.
4. `Sunfish.Blocks.Leases.Models.PartyId` is `[Obsolete]`-marked.
5. `LeasePartyRole.Party`, `Lease.Tenants`, `Lease.Landlord`, and `ILeaseService.RecordPartySignatureAsync`'s `party` parameter all switch to `Sunfish.Blocks.People.Foundation.PartyId`.
6. `InMemoryLeaseService` gains an injected `IPartyReadModel` for display-name resolution.
7. `LeasesServiceCollectionExtensions.AddInMemoryLeases()` ensures `IPartyReadModel` is available in DI (either already registered by a caller who registered `AddBlocksPeopleFoundation()`, or via a `TryAddSingleton<IPartyReadModel, InMemoryPartyRepository>()` fallback that keeps the no-argument constructor working for tests that don't register the people cluster).
8. A new `ILeaseService.GetLeaseholderDisplaysAsync` method exposes the enriched read surface (display name + role).
9. `apps/docs/blocks/leases/overview.md` is updated with a note about the party-type migration.
10. The W#27 ledger row gets a prose note (no status change needed — W#27 is already `built`).

### Cross-cluster boundary (binding per party-model-convention §4)

> `blocks-leases` reads Party data only through `IPartyReadModel`. It NEVER queries Party tables directly. It NEVER owns a Party write path. It holds `PartyId` foreign-key references; all display-name + contact-method resolution goes through `IPartyReadModel` at read time.

The canonical accessor used in this hand-off is `IPartyReadModel.GetDisplayNameAsync(PartyId, CancellationToken)`. No other `IPartyReadModel` methods are called from `blocks-leases` in this hand-off's scope. If a future workstream needs email or phone resolution on a Lease party, it adds the appropriate accessor call via the same injected `IPartyReadModel`.

### Sequencing rationale

This hand-off is **position 8** in the COB queue (see task brief). The predecessor at position 2 is `blocks-people-foundation` (4 PRs). This hand-off MUST NOT start until all 4 `blocks-people-foundation` PRs are on `origin/main`. The Hard prerequisites section specifies the exact verification commands.

After this hand-off lands, the canonical Party retrofit for `blocks-leases` is complete. The next step (if CO directs) is to remove the three deprecated types in a separate `sunfish-api-change` pipeline pass. That is out of scope here.

---

## Hard prerequisites

ALL of the following must be verified on `origin/main` before opening PR 1. If any check fails, STOP and file a `cob-question-*` beacon naming which prerequisite is missing.

| # | Prerequisite | Verify command | Expected result |
|---|---|---|---|
| H1 | `blocks-people-foundation` package on `origin/main` | `ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-foundation/Sunfish.Blocks.People.Foundation.csproj` | File exists |
| H2 | `Sunfish.Blocks.People.Foundation.PartyId` struct on `origin/main` | `grep -rn "struct PartyId" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-foundation/` | At least 1 match in `blocks-people-foundation/` |
| H3 | `IPartyReadModel` interface on `origin/main` | `grep -rn "IPartyReadModel" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-foundation/` | At least 1 match in `blocks-people-foundation/` |
| H4 | `IPartyReadModel.GetDisplayNameAsync` exists | `grep -rn "GetDisplayNameAsync" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-foundation/` | At least 1 match |
| H5 | No open PRs touching `blocks-leases` | `gh pr list --state open --search "blocks-leases in:title,body"` | Zero results (or only unrelated PRs confirmed as safe) |
| H6 | Working branch is from `main` (not GitButler HEAD) | `git log --oneline -1 main` vs `git merge-base HEAD main` | Worktree was created with `main` as the third arg per `feedback_worktree_base_main_not_gitbutler` |

---

## Substrate verification (COB runs before writing a line of code)

```bash
# 1. Confirm the three legacy types that WILL be deprecated:
grep -n "class Party\|record Party\|enum PartyKind\|struct PartyId\|record struct PartyId" \
  /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Models/Party.cs \
  /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Models/PartyKind.cs \
  /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Models/PartyId.cs

# 2. Confirm the type that MUST NOT change:
grep -n "enum LeaseHolderRole" \
  /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Models/LeaseHolderRole.cs

# 3. Confirm the join table's Party field (will get type-swapped):
grep -n "PartyId Party" \
  /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Models/LeasePartyRole.cs

# 4. Confirm Lease's tenant/landlord PartyId references (will get type-swapped):
grep -n "PartyId" \
  /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Models/Lease.cs

# 5. Confirm current csproj references (no blocks-people-foundation yet):
grep -n "ProjectReference" \
  /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Sunfish.Blocks.Leases.csproj

# 6. Confirm the canonical PartyId's factory method (must be ULID, not Guid):
grep -n "NewId\|Ulid\|NewUlid" \
  /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-foundation/Models/PartyId.cs 2>/dev/null

# 7. Count existing test cases for PartyId to know what to update:
grep -rn "PartyId\|NewId()" \
  /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/tests/ | wc -l
```

If step 6 shows anything OTHER than `Ulid.NewUlid()` (e.g., `Guid.NewGuid()`), STOP and file `cob-question-*` — the predecessor hand-off may have shipped a non-ULID canonical PartyId, which is a prerequisite deviation that needs XO resolution before proceeding.

---

## PR 1 — Type alignment: deprecate local types, swap to canonical PartyId

**Estimated effort:** ~2–3h
**Tests:** ~10–15 tests
**Commit subject:** `feat(blocks-leases): adopt canonical PartyId from blocks-people-foundation; deprecate local Party/PartyKind/PartyId`
**Branch:** `cob/blocks-leases-party-canonical-retrofit`

### Step 1: Add project reference

In `packages/blocks-leases/Sunfish.Blocks.Leases.csproj`, add to the existing `<ItemGroup>` containing `<ProjectReference>` entries:

```xml
<ProjectReference Include="..\blocks-people-foundation\Sunfish.Blocks.People.Foundation.csproj" />
```

The existing references (`foundation`, `kernel-audit`, `kernel-signatures`, `blocks-maintenance`, `ui-core`, `ui-adapters-blazor`) are retained unchanged.

### Step 2: Deprecate `Models/Party.cs`

Add the `[Obsolete]` attribute to the `Party` record declaration. Do NOT change any field or remove the file.

```csharp
/// <summary>
/// A person or entity that is a party to a lease (tenant, landlord, manager, or guarantor).
/// </summary>
/// <remarks>
/// This type predates the canonical Party convention and will be removed in a future
/// api-change release. Use <see cref="Sunfish.Blocks.People.Foundation.Party"/> via
/// <see cref="Sunfish.Blocks.People.Foundation.IPartyReadModel"/> instead.
/// </remarks>
[Obsolete("Use Sunfish.Blocks.People.Foundation.Party via IPartyReadModel. " +
          "This type will be removed in a future api-change release.")]
public sealed record Party
{
    // ... existing fields unchanged ...
}
```

### Step 3: Deprecate `Models/PartyKind.cs`

Add the `[Obsolete]` attribute to the `PartyKind` enum. Do NOT change any member or remove the file.

```csharp
/// <summary>
/// The role a <see cref="Party"/> plays in a lease transaction.
/// </summary>
/// <remarks>
/// This enum predates the canonical PartyRole convention. Use the string role-name codes
/// from <c>PartyRole.roleName</c> (via <see cref="Sunfish.Blocks.People.Foundation.IPartyReadModel"/>)
/// instead: <c>"tenant"</c>, <c>"landlord"</c>, <c>"manager"</c>, <c>"guarantor"</c>.
/// This enum will be removed in a future api-change release.
/// </remarks>
[Obsolete("Use PartyRole.roleName string codes (\"tenant\", \"landlord\", \"manager\", \"guarantor\") " +
          "from IPartyReadModel instead. This enum will be removed in a future api-change release.")]
public enum PartyKind
{
    // ... existing members unchanged ...
}
```

### Step 4: Deprecate `Models/PartyId.cs`

Add the `[Obsolete]` attribute to the `PartyId` record struct. Do NOT change the type or remove the file. Add a prominent warning comment about the ULID vs Guid wire-format difference.

```csharp
/// <summary>
/// Opaque identifier for a <see cref="Party"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
/// <remarks>
/// This type predates the canonical Party convention. Use
/// <see cref="Sunfish.Blocks.People.Foundation.PartyId"/> instead — it is ULID-backed
/// (per CRDT convention §1) whereas this type uses <see cref="Guid.NewGuid()"/> (non-ULID).
/// Wire formats are not interchangeable. This type will be removed in a future api-change release.
/// </remarks>
[Obsolete("Use Sunfish.Blocks.People.Foundation.PartyId (ULID-backed) instead. " +
          "This type uses Guid and is incompatible with the canonical wire format. " +
          "It will be removed in a future api-change release.")]
[JsonConverter(typeof(PartyIdJsonConverter))]
public readonly record struct PartyId(string Value)
{
    // ... existing body unchanged ...
}
```

### Step 5: Swap `LeasePartyRole.Party` field type

In `Models/LeasePartyRole.cs`, change the `Party` field from the local `PartyId` to the canonical type. Add a using alias at the top of the file to avoid ambiguity:

```csharp
using PeoplePartyId = Sunfish.Blocks.People.Foundation.PartyId;

namespace Sunfish.Blocks.Leases.Models;

public sealed record LeasePartyRole
{
    public required LeasePartyRoleId Id { get; init; }
    public required LeaseId Lease { get; init; }

    /// <summary>The party assigned this role. References <see cref="Sunfish.Blocks.People.Foundation.Party"/> by canonical ULID-backed ID.</summary>
    public required PeoplePartyId Party { get; init; }

    public required LeaseHolderRole Role { get; init; }
}
```

**Alias rationale:** The alias `PeoplePartyId` avoids any ambiguity with the deprecated local `PartyId` that still exists in the same namespace during the deprecation window. Use the alias consistently in this file. Do NOT use a global `using` alias in `_Imports.razor` or project-wide — the deprecation window requires the distinction to be visible at call sites.

### Step 6: Swap `Lease.Tenants` and `Lease.Landlord` field types

In `Models/Lease.cs`, add the same using alias and update the two `PartyId`-typed fields:

```csharp
using PeoplePartyId = Sunfish.Blocks.People.Foundation.PartyId;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Blocks.Leases.Models;

public sealed record Lease
{
    public required LeaseId Id { get; init; }
    public required EntityId UnitId { get; init; }

    /// <summary>All tenant parties on this lease. Each ID references a <see cref="Sunfish.Blocks.People.Foundation.Party"/>.</summary>
    public required IReadOnlyList<PeoplePartyId> Tenants { get; init; }

    /// <summary>The landlord party for this lease. References a <see cref="Sunfish.Blocks.People.Foundation.Party"/>.</summary>
    public required PeoplePartyId Landlord { get; init; }

    // ... remaining fields unchanged (StartDate, EndDate, MonthlyRent, Phase, PartyRoles,
    //     DocumentVersions, PartySignatures, LandlordAttestation) ...
}
```

### Step 7: Update `ILeaseService.RecordPartySignatureAsync` signature

In `Services/ILeaseService.cs`, add the same using alias and update the `party` parameter:

```csharp
using PeoplePartyId = Sunfish.Blocks.People.Foundation.PartyId;
// ... other usings ...

public interface ILeaseService
{
    // ... other methods unchanged ...

    /// <summary>
    /// W#27 Phase 3: records one party's signature on the latest
    /// document revision of the lease. The party MUST be in
    /// <see cref="Lease.Tenants"/>; the document version MUST be the
    /// latest. Returns the persisted lease with the new signature
    /// appended to <see cref="Lease.PartySignatures"/>.
    /// </summary>
    ValueTask<Lease> RecordPartySignatureAsync(LeaseId id, PeoplePartyId party, SignatureEventId signatureEvent, ActorId actor, CancellationToken ct = default);
}
```

### Step 8: Update `LeasePartySignature` (if it holds a PartyId field)

Check `Models/LeasePartySignature.cs`:

```bash
grep -n "PartyId" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/Models/LeasePartySignature.cs 2>/dev/null
```

If the `Party` field on `LeasePartySignature` is typed as `Sunfish.Blocks.Leases.Models.PartyId`, apply the same alias-and-swap pattern used in steps 5–7. If it is already typed differently, note the finding and do not touch it.

### Step 9: Update `InMemoryLeaseService`

`InMemoryLeaseService` currently has no `IPartyReadModel` injection. This step adds it for display-name resolution (used by PR 2's new method), but the existing method bodies change only in type references — no behavior changes.

**Constructor changes:** Add an overload (do NOT break the existing no-arg and 3-arg / 4-arg constructors):

```csharp
using PeoplePartyId = Sunfish.Blocks.People.Foundation.PartyId;
using Sunfish.Blocks.People.Foundation;
// ... existing usings ...

public sealed class InMemoryLeaseService : ILeaseService
{
    // ... existing fields unchanged ...
    private readonly IPartyReadModel? _partyReadModel;

    /// <summary>Creates the service with audit emission disabled and no party-display-name resolution.</summary>
    public InMemoryLeaseService() { }

    /// <summary>Creates the service with audit emission wired (no party-display-name resolution).</summary>
    public InMemoryLeaseService(IAuditTrail auditTrail, IOperationSigner signer, TenantId tenantId)
        : this(auditTrail, signer, tenantId, documentVersionLog: null, partyReadModel: null) { }

    /// <summary>Creates the service with audit emission + optional document-version log (W#27 Phase 2).</summary>
    public InMemoryLeaseService(IAuditTrail auditTrail, IOperationSigner signer, TenantId tenantId, ILeaseDocumentVersionLog? documentVersionLog)
        : this(auditTrail, signer, tenantId, documentVersionLog, partyReadModel: null) { }

    /// <summary>Creates the service with audit emission + document-version log + party-display-name resolution.</summary>
    public InMemoryLeaseService(IAuditTrail auditTrail, IOperationSigner signer, TenantId tenantId, ILeaseDocumentVersionLog? documentVersionLog, IPartyReadModel? partyReadModel)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        if (tenantId == default)
            throw new ArgumentException("TenantId is required for audit emission.", nameof(tenantId));
        _auditTrail = auditTrail;
        _signer = signer;
        _auditTenant = tenantId;
        _documentVersionLog = documentVersionLog;
        _partyReadModel = partyReadModel;
    }

    // ... existing method bodies unchanged; only internal PartyId usages update to PeoplePartyId ...
}
```

**Internal body updates (no behavior change):** Anywhere `InMemoryLeaseService` constructs or compares a `Sunfish.Blocks.Leases.Models.PartyId` (e.g., in `RecordPartySignatureAsync` where `lease.Tenants.Contains(party)` is called), the type is now `Sunfish.Blocks.People.Foundation.PartyId` — the alias resolves this transparently.

### Step 10: Update `LeasesServiceCollectionExtensions.AddInMemoryLeases()`

Add a `TryAddSingleton` for `IPartyReadModel` so that callers who do NOT register `AddBlocksPeopleFoundation()` (e.g., existing tests) still get an in-memory fallback:

```csharp
using Sunfish.Blocks.People.Foundation;
// ... existing usings ...

public static IServiceCollection AddInMemoryLeases(this IServiceCollection services)
{
    ArgumentNullException.ThrowIfNull(services);
    services.AddSingleton<ILeaseService, InMemoryLeaseService>();

    // Ensure IPartyReadModel is available. Callers who register AddBlocksPeopleFoundation()
    // will have already registered the canonical implementation; TryAddSingleton is a no-op
    // in that case. Callers who only register leases (test / demo) get an in-memory fallback
    // that returns null display names gracefully.
    services.TryAddSingleton<IPartyReadModel, InMemoryPartyRepository>();

    // Wave 2 Cluster C — Plan 2 Task 3.5: register the open-generic Sunfish localizer (unchanged).
    services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));

    return services;
}
```

**Note:** `InMemoryPartyRepository` is the in-memory implementation shipped by `blocks-people-foundation` PR 3. Verify the exact class name:

```bash
grep -rn "class InMemoryParty" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-foundation/ 2>/dev/null
```

If the class is named differently (e.g., `InMemoryPartyReadModel`), use the actual name. If no in-memory implementation exists, use `TryAddSingleton<IPartyReadModel>(_ => NullPartyReadModel.Instance)` where `NullPartyReadModel` is a local private class that returns `null` / empty for all methods — do NOT fail at startup for missing people-cluster registration. File a `cob-question-*` if this case arises; XO will rule on the fallback pattern.

### Step 11: Update existing tests for the PartyId type change

Tests in `packages/blocks-leases/tests/` currently create `PartyId` using `PartyId.NewId()` (the local type, Guid-backed). After the swap, they must use the canonical type.

**Pattern to replace** (search):

```bash
grep -rn "PartyId.NewId\(\)\|new PartyId\|Blocks\.Leases\.Models\.PartyId" \
  /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-leases/tests/
```

**Replacement pattern** — use the canonical factory:

```csharp
// Before (Guid-backed, deprecated):
var partyId = PartyId.NewId();                    // Sunfish.Blocks.Leases.Models.PartyId

// After (ULID-backed, canonical):
using PeoplePartyId = Sunfish.Blocks.People.Foundation.PartyId;
var partyId = PeoplePartyId.NewId();              // Sunfish.Blocks.People.Foundation.PartyId
```

If `Sunfish.Blocks.People.Foundation.PartyId.NewId()` uses `Ulid.NewUlid().ToString()` internally (verify from substrate verification step 6), then the factory call is the right idiom. If the canonical factory method has a different name, use the actual name.

**Test files to update** (verify list against actual grep output):

- `tests/InMemoryLeaseServiceTests.cs` — party ID creation in CreateLeaseRequest fixtures
- `tests/AuditEmissionTests.cs` — if party IDs appear in audit fixture setup
- `tests/DocumentVersionAndSignaturesTests.cs` — `RecordPartySignatureAsync` calls pass `PartyId`
- `tests/LeaseHolderRoleTests.cs` — if `LeasePartyRole` fixtures use `PartyId`

Do NOT change test assertions or test semantics — only the type of the `partyId` variable.

### PR 1 acceptance criteria

- `dotnet build packages/blocks-leases/Sunfish.Blocks.Leases.csproj` succeeds with zero errors. Deprecation warnings from the `[Obsolete]`-marked types are expected and acceptable; do NOT suppress them with `#pragma warning disable` — they should remain visible to future callers.
- `dotnet test packages/blocks-leases/tests/tests.csproj` passes all existing tests with zero failures.
- `grep -rn "Sunfish.Blocks.Leases.Models.PartyId" packages/blocks-leases/Models/Lease.cs packages/blocks-leases/Models/LeasePartyRole.cs packages/blocks-leases/Services/` returns zero matches (the canonical type is used in all production code paths; the legacy `PartyId.cs` file still EXISTS but is only referenced by its own `[Obsolete]` declaration and the JSON converter).
- `grep -n "struct PartyId" packages/blocks-people-foundation/` returns at least 1 match (confirming the canonical PartyId is present as a prerequisite).
- No `LeaseHolderRole.cs` diff in the PR — it must be untouched.

---

## PR 2 — Enriched read surface + docs + ledger note

**Estimated effort:** ~1–2h
**Tests:** ~5–8 tests
**Commit subject:** `feat(blocks-leases): add GetLeaseholderDisplaysAsync; update docs for canonical party types`
**Branch:** continues from PR 1 branch, or a separate `cob/blocks-leases-leaseholder-displays` branch off the merged PR 1 commit

### Step 1: Add `GetLeaseholderDisplaysAsync` to `ILeaseService`

```csharp
/// <summary>
/// Returns the display name and per-lease role for each tenant party on the specified lease,
/// resolved through <see cref="Sunfish.Blocks.People.Foundation.IPartyReadModel"/>.
/// Returns an empty list if the lease does not exist.
/// Display names that cannot be resolved (party not found in the people cluster)
/// are represented as <c>null</c> in the tuple.
/// </summary>
/// <param name="leaseId">The lease to query.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// One entry per <see cref="LeasePartyRole"/> on the lease, ordered by
/// <see cref="LeaseHolderRole"/> (PrimaryLeaseholder first, then CoLeaseholder, Occupant, Guarantor).
/// Includes only parties with an explicit <see cref="LeasePartyRole"/> binding;
/// parties in <see cref="Lease.Tenants"/> without a role binding are excluded.
/// </returns>
ValueTask<IReadOnlyList<LeaseholderDisplay>> GetLeaseholderDisplaysAsync(LeaseId leaseId, CancellationToken ct = default);
```

**`LeaseholderDisplay` value type** (new file `Models/LeaseholderDisplay.cs`):

```csharp
using PeoplePartyId = Sunfish.Blocks.People.Foundation.PartyId;

namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// A resolved view of one party's per-lease role binding, enriched with a display name
/// from the canonical people cluster.
/// </summary>
/// <param name="PartyId">Canonical party identifier.</param>
/// <param name="DisplayName">
/// Display name resolved via <see cref="Sunfish.Blocks.People.Foundation.IPartyReadModel"/>,
/// or <see langword="null"/> if the party was not found.
/// </param>
/// <param name="Role">The per-lease role this party holds.</param>
public sealed record LeaseholderDisplay(
    PeoplePartyId PartyId,
    string? DisplayName,
    LeaseHolderRole Role);
```

### Step 2: Implement `GetLeaseholderDisplaysAsync` in `InMemoryLeaseService`

```csharp
/// <inheritdoc />
public async ValueTask<IReadOnlyList<LeaseholderDisplay>> GetLeaseholderDisplaysAsync(
    LeaseId leaseId,
    CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();

    if (!_store.TryGetValue(leaseId, out var lease))
    {
        return Array.Empty<LeaseholderDisplay>();
    }

    // Only parties with an explicit LeasePartyRole binding are included.
    // Order: PrimaryLeaseholder (0) → CoLeaseholder (1) → Occupant (2) → Guarantor (3).
    var roleBindings = lease.PartyRoles
        .Select(roleId => /* resolve from a parallel role store if available */ (LeasePartyRole?)null)
        .Where(r => r is not null)
        .OrderBy(r => r!.Role)
        .ToList();

    // Implementation note: InMemoryLeaseService stores only LeasePartyRoleId references
    // on Lease.PartyRoles, not the full LeasePartyRole records. COB must verify whether a
    // separate LeasePartyRole store exists in the in-memory service (grep for _roleStore or
    // similar). If not, add one that stores LeasePartyRole records indexed by LeasePartyRoleId.
    // See §Halt-conditions H-PR2 below.

    var result = new List<LeaseholderDisplay>();
    foreach (var binding in roleBindings)
    {
        ct.ThrowIfCancellationRequested();
        string? displayName = null;
        if (_partyReadModel is not null)
        {
            displayName = await _partyReadModel.GetDisplayNameAsync(binding!.Party, ct).ConfigureAwait(false);
        }
        result.Add(new LeaseholderDisplay(binding!.Party, displayName, binding.Role));
    }

    return result;
}
```

**Halt condition for the role-store gap:** The current `InMemoryLeaseService` stores `Lease.PartyRoles` as `IReadOnlyList<LeasePartyRoleId>` (opaque IDs). To resolve them, there must be a way to look up the full `LeasePartyRole` record. If no such store exists in the in-memory service, COB must either:

1. Add a `ConcurrentDictionary<LeasePartyRoleId, LeasePartyRole> _roleStore` to `InMemoryLeaseService` and update the Phase 4 role-binding path to populate it, **or**
2. File a `cob-question-*` beacon explaining the gap and asking XO whether the enriched read method should instead iterate `Lease.Tenants` (not `Lease.PartyRoles`) to resolve display names, accepting that the `LeaseHolderRole` per party is not available in that path.

Option (1) is preferred if the W#27 Phase 4 code that created `LeasePartyRole` records also stored them somewhere accessible. Option (2) is the fallback if not.

**The `IPartyReadModel.GetDisplayNameAsync` signature** — verify the exact method name from the canonical interface before writing the call:

```bash
grep -n "GetDisplayNameAsync\|DisplayName" \
  /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-foundation/Services/IPartyReadModel.cs 2>/dev/null
```

If the method name differs (e.g., `GetDisplayName` without `Async`, or it returns a different type), use the actual name. Do NOT assume — this is a new interface; the implementation may differ from the convention document's TypeScript sketch.

### Step 3: Update `apps/docs/blocks/leases/overview.md`

Locate the existing docs page:

```bash
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/apps/docs/blocks/leases/ 2>/dev/null
```

If it does not exist yet, create it as a stub. The content to add (either as a new section or an update to an existing "Party Identity" section):

```markdown
## Party identity

As of the `blocks-property-leases-party-retrofit` workstream (2026-05-16):

- `Lease.Tenants` and `Lease.Landlord` reference `Sunfish.Blocks.People.Foundation.PartyId`
  (ULID-backed canonical Party identifiers from the `blocks-people-foundation` package).
- Per-lease role bindings (`LeasePartyRole`) use the same canonical `PartyId`.
- Display-name and contact-method resolution uses `IPartyReadModel.GetDisplayNameAsync`
  injected via `AddBlocksPeopleFoundation()` (or the no-op in-memory fallback registered
  by `AddInMemoryLeases()`).

### Deprecated types (will be removed in a future api-change release)

The following types in `Sunfish.Blocks.Leases.Models` are deprecated and should not be
used in new code:

| Type | Replacement |
|---|---|
| `Sunfish.Blocks.Leases.Models.Party` | `Sunfish.Blocks.People.Foundation.Party` via `IPartyReadModel` |
| `Sunfish.Blocks.Leases.Models.PartyKind` | `PartyRole.roleName` string codes: `"tenant"`, `"landlord"`, `"manager"`, `"guarantor"` |
| `Sunfish.Blocks.Leases.Models.PartyId` | `Sunfish.Blocks.People.Foundation.PartyId` (ULID-backed) |

`LeaseHolderRole` (`PrimaryLeaseholder`, `CoLeaseholder`, `Occupant`, `Guarantor`) is
**not deprecated** — it is the correct per-lease role discriminator and is orthogonal to
the global `PartyRole` registry.
```

### Step 4: W#27 ledger note

In `icm/_state/active-workstreams.md`, find the W#27 row. Add a prose note in the "Notes" or "History" cell (do NOT change the status — W#27 is already `built`):

> Party canonical retrofit shipped 2026-05-16: `blocks-leases.Party` / `PartyKind` / `PartyId` deprecated; `blocks-people-foundation.PartyId` adopted on `Lease`, `LeasePartyRole`, `ILeaseService`. See `blocks-property-leases-party-retrofit-stage06-handoff.md`.

If the ledger row format does not support prose notes, add a commit-message-only note in the PR body and skip the ledger edit.

### PR 2 acceptance criteria

- `dotnet build packages/blocks-leases/Sunfish.Blocks.Leases.csproj` succeeds.
- `dotnet test packages/blocks-leases/tests/tests.csproj` passes all tests (existing + new).
- New tests cover:
  - `GetLeaseholderDisplaysAsync` returns empty for an unknown `LeaseId`.
  - `GetLeaseholderDisplaysAsync` returns one entry per `LeasePartyRole` on a lease with 2 role bindings (PrimaryLeaseholder + CoLeaseholder).
  - `GetLeaseholderDisplaysAsync` returns `DisplayName = null` when `IPartyReadModel` is not registered (no-arg constructor path).
  - `GetLeaseholderDisplaysAsync` returns resolved display names when a mock `IPartyReadModel` is injected.
  - Order is `PrimaryLeaseholder` before `CoLeaseholder` before `Occupant` before `Guarantor`.
- `apps/docs/blocks/leases/overview.md` exists and contains the deprecation table.
- The W#27 ledger row has a prose note (or the PR body records the note if the ledger format doesn't support it).

---

## Tests — full specification

### PR 1 tests (~10–15 tests across existing test files)

All are modifications to existing tests, not new test files.

**`InMemoryLeaseServiceTests.cs` updates:**

1. `CreateLease_WithTenants_ReturnsDraftWithCorrectTenantIds` — change `PartyId.NewId()` to `PeoplePartyId.NewId()` in fixture; assert `lease.Tenants[0]` is the canonical type.
2. `CreateLease_WithLandlord_ReturnsDraftWithCorrectLandlord` — same factory change.
3. `RecordPartySignatureAsync_WithKnownParty_AppendsSignature` — the `party` argument is now `PeoplePartyId`.
4. `RecordPartySignatureAsync_WithUnknownParty_Throws` — same type change; guard logic unchanged.
5. `ListAsync_FilterByTenantId_ReturnsOnlyMatchingLeases` — `query.TenantId` becomes `PeoplePartyId`-typed; assert the filter still works.
6. All tests that construct `CreateLeaseRequest` with `Tenants` or `Landlord` fields — update the `PartyId` factory calls.

**`DocumentVersionAndSignaturesTests.cs` updates:**

7. Any test passing a `party` parameter to `RecordPartySignatureAsync` — update the type.
8. The `EnforceExecutedTransitionGuard` path that checks `lease.Tenants.Contains(party)` — verify the Contains call still works after the type swap (it will, since `PeoplePartyId` is a value type with structural equality on the string Value).

**`LeaseHolderRoleTests.cs` updates** (if `LeasePartyRole` fixtures use `PartyId`):

9. Any fixture constructing `new LeasePartyRole { Party = ... }` — update to `PeoplePartyId`.

**`AuditEmissionTests.cs` updates:**

10. Party-related fixtures in audit tests — update `PartyId` factory calls.

**New tests (if not already covered):**

11. `PartyId_LegacyType_IsMarkedObsolete` — reflection test confirming `typeof(Sunfish.Blocks.Leases.Models.PartyId).GetCustomAttributes<ObsoleteAttribute>()` is non-empty. Optional but useful as a regression guard.
12. `LeaseHolderRole_IsNotDeprecated` — reflection test confirming `typeof(LeaseHolderRole).GetCustomAttributes<ObsoleteAttribute>()` is empty (guards against accidental deprecation of this correct type).

### PR 2 tests (~5–8 new tests in a new file or appended to existing)

New test class: `LeaseholderDisplayTests.cs` (or appended to `InMemoryLeaseServiceTests.cs`).

1. `GetLeaseholderDisplaysAsync_UnknownLeaseId_ReturnsEmptyList`
2. `GetLeaseholderDisplaysAsync_LeaseWithNoRoleBindings_ReturnsEmptyList`
3. `GetLeaseholderDisplaysAsync_LeaseWithTwoRoleBindings_ReturnsBothWithDisplayNames` — inject a mock `IPartyReadModel` that returns known names; assert both entries present with correct `DisplayName` + `Role`.
4. `GetLeaseholderDisplaysAsync_NoPartyReadModel_ReturnsNullDisplayNames` — use the no-arg `InMemoryLeaseService()` constructor; assert `DisplayName == null` for each entry.
5. `GetLeaseholderDisplaysAsync_OrderedByRole_PrimaryFirst` — set up `CoLeaseholder` before `PrimaryLeaseholder` in the store; assert returned list starts with `PrimaryLeaseholder`.
6. `GetLeaseholderDisplaysAsync_PartyNotFoundInReadModel_ReturnsNullDisplayName` — inject a mock that returns `null` for a specific `PartyId`; assert `DisplayName == null`.
7. `LeaseholderDisplay_IsRecord_SupportsValueEquality` — sanity check on the new record type.

---

## Halt conditions

These are STOP signals. If any of the following is true, COB halts the workstream, files a `cob-question-*` beacon to the coordination inbox naming the specific condition, and does NOT continue until XO resolves it.

| # | Condition | Reason |
|---|---|---|
| H1 | `blocks-people-foundation` is not on `origin/main` when COB starts | This entire hand-off depends on the canonical `Sunfish.Blocks.People.Foundation.PartyId` type being available. Building against a local branch that hasn't merged creates rebase churn and integration risk. |
| H2 | `Sunfish.Blocks.People.Foundation.PartyId.NewId()` does NOT use ULID internally | If the canonical factory uses `Guid.NewGuid()` instead of `Ulid.NewUlid()`, the prerequisite hand-off deviated from the convention. The wire-format incompatibility is the entire point of this retrofit; if both types use Guid, the type swap is a naming-only change with no value. XO must rule on whether to proceed or fix the canonical type first. |
| H3 | `IPartyReadModel.GetDisplayNameAsync` does not exist with that exact name | PR 2's new method depends on this accessor. If the canonical interface ships a different name, the PR 2 implementation must use the actual name — halt to confirm before writing wrong code. |
| H4 | `LeaseHolderRole.cs` has any diff in a PR authored for this hand-off | This enum must not change. If COB accidentally touches it (e.g., a bulk find-replace on `PartyId`), the diff must be reverted before the PR is submitted. |
| H5 | A PR in this hand-off introduces a `sunfish-api-change` pipeline-level breaking change | Deleting the legacy `Party`, `PartyKind`, or `PartyId` types from `blocks-leases` would be a breaking change. This hand-off only DEPRECATES (marks `[Obsolete]`); it does NOT delete. Deletion is a separate `sunfish-api-change` pipeline pass. If COB finds itself deleting these files, halt and re-read this hand-off. |
| H6 | The `InMemoryLeaseService` has no accessible store of `LeasePartyRole` records (see PR 2 Step 2 implementation note) | PR 2's `GetLeaseholderDisplaysAsync` requires the ability to resolve `LeasePartyRoleId` → `LeasePartyRole` at runtime. If no such store exists, COB cannot implement the method correctly without adding it. The scope of adding a `_roleStore` is acceptable within this hand-off, but if that addition would require touching Phase 4 wire-up code in a way that feels risky, halt and get XO guidance on the fallback pattern (iterate `Lease.Tenants` instead of `Lease.PartyRoles`). |

---

## Acceptance criteria (full workstream)

All of the following must be true before this workstream is considered closed:

1. **PR 1 merged** — `dotnet build` + `dotnet test` green; `[Obsolete]` on all three legacy types; canonical `PeoplePartyId` used throughout production code paths; `LeaseHolderRole` untouched.
2. **PR 2 merged** — `GetLeaseholderDisplaysAsync` on `ILeaseService` + `InMemoryLeaseService`; `LeaseholderDisplay` record in `Models/`; all 5–8 new tests green; `apps/docs/blocks/leases/overview.md` updated.
3. **W#27 ledger note** present (in the PR body or the ledger row — either is acceptable).
4. **Zero deleted files** — the three deprecated files (`Party.cs`, `PartyKind.cs`, `PartyId.cs`) still exist; only their `[Obsolete]` attributes are new.
5. **Zero `LeaseHolderRole.cs` diff** in either PR.
6. **CI green** on both PRs (build + tests + lint + security scan).
7. **`blocks-people-foundation` is on `origin/main`** at the time both PRs merge (this is guaranteed by H1, but confirm at PR 2 open time as well).

---

## References

- `_shared/engineering/party-model-convention.md` §2 (Party canonical shape), §3 (PartyRole registry), §4 (cross-cluster read interface), §7 (PII/privacy), §9 (Stage 06 discipline checklist)
- `icm/02_architecture/blocks-property-party-alignment-review.md` §2.11 (leases pre-convention deviation), §6 (prerequisites)
- `icm/_state/handoffs/blocks-people-foundation-stage06-handoff.md` (predecessor)
- W#27 original hand-off (for Phase 4 `LeasePartyRole` context)
- `feedback_worktree_base_main_not_gitbutler.md` — always create worktrees from `main`, not GitButler HEAD
- `feedback_council_before_automerge.md` — not applicable (no council required for this hand-off); COB self-audit is sufficient
