# Hand-off — `blocks-financial-ledger` Chart-of-Accounts + Journal Core (Phase 1 foundational)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-16
**Status:** `ready-to-build`
**Workstream:** W#60 P4 — Path II native domain, first cluster unit (foundational)
**Spec source:** [`icm/02_architecture/blocks-financial-schema-design.md`](../../02_architecture/blocks-financial-schema-design.md) §3.1–§3.4, §6.1, §7
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) (Proposed; ratified by CO 2026-05-16)
**Ratifications:** `coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md` (Decisions 1 + 3)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~10–14h sunfish-PM (rename + schema extensions + posting algorithm + chart seed + importer entry-point + ~40–55 tests + docs)
**PR count:** 6 PRs (one mechanical rename + five additive feature PRs)
**Pre-merge council:** NOT required (substrate scope; mirrors the W#34/W#35/W#36 substrate-only pattern). Standard COB self-audit applies.
**Audit before build:** `ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-(financial|accounting)"` — confirms `blocks-financial-ledger/` exists (target of rename) and `blocks-financial-ledger/` does not yet exist.

---

## Context

### Path II reframe

W#60 originally planned to ship the financial layer on top of ERPNext (the
W#60 P1–P3 ERPNext detour). CO ratified Path II on 2026-05-16 via ADR 0088:
**Anchor is the all-in-one local-first runtime**; the financial domain is
implemented natively in Sunfish `blocks-*` clusters; no external engine. SQLite
is the primary store; Loro CRDT layers on top for peer-to-peer sync.

Native domain partitions into 7 clusters (ADR 0088 §1). The `blocks-financial-*`
cluster (`-ledger`, `-chart`, `-ar`, `-ap`, `-payments`, `-tax`, `-periods` for
Phase 1 core; `-budget`, `-forecast`, `-estimate`, `-bid` for Phase 3) is the
**second-most-foundational** cluster after `blocks-property-*`, and the
**ledger sub-cluster is the most foundational unit in the financial cluster** —
every other financial entity (Invoice, Bill, Payment, etc.) ultimately posts
through `JournalEntry`. This is therefore the first Stage 06 hand-off in the
Phase 1 critical path.

### Naming ratification (binding)

Per the 2026-05-16 ratification ruling:

- **Decision 1:** `blocks-financial-ledger` **renames to** `blocks-financial-ledger`.
  The existing C# implementation (`GLAccount`, `JournalEntry`,
  `JournalEntryLine`, `DepreciationSchedule`) is preserved and extended in
  place. **The first PR under this hand-off is the rename PR.** Subsequent
  PRs add schema extensions per `blocks-financial-schema-design.md`.
- **Decision 1 corollary:** Keep `GLAccount` name (do **NOT** rename to
  `Financial.Chart.Account`). The Stage 02 design's `Account` type is an
  alias / namespace-rename only; the canonical C# type is `GLAccount`. This
  closes open question §12.1 of `blocks-financial-schema-design.md`.
- **Decision 3:** Existing `blocks-rent-collection.Invoice` + `Payment`
  become non-breaking wrappers over the canonical financial-AR `Invoice` +
  `Payment` once those land. **Not in scope for this hand-off** (the
  wrapper relationship lands when `blocks-financial-ar` ships in a
  follow-on hand-off).

### Why ledger is the first cluster up

1. **Topological dependency.** Every other Phase 1 financial entity
   composes on top of `JournalEntry` (via `journalEntryId` FK fields on
   `Invoice` / `Bill` / `Payment` per `blocks-financial-schema-design.md`
   §3.5, §3.7, §3.9).
2. **Migration importer dependency.** The ERPNext migration importer
   (`_shared/engineering/erpnext-to-anchor-migration-importer-spec.md`)
   Pass 1 + Pass 3 require `GLAccount` and `JournalEntry` to be the
   first entities the importer can write to.
3. **Existing code reuse.** `blocks-financial-ledger/` is already on main with
   the canonical `JournalEntry` immutability + balance invariant
   (`Sunfish.Blocks.FinancialLedger.Models.JournalEntry`'s constructor enforces
   `Σ debits == Σ credits` via `ArgumentException`). Extension on top of
   working code beats greenfield greenwash.
4. **`apps/docs` precedent.** Cluster docs already follow the
   `apps/docs/{cluster}/overview.md` pattern; the ledger docs page is the
   first Phase 1 cluster doc.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify rename clean state.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep blocks-financial
   ```
   Expected: `blocks-financial-ledger/` exists; nothing matching `blocks-financial-*`.

2. **Read the naming-ratification ruling.**
   File: `coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md`.
   Confirm Decisions 1 + 3 are understood; Decision 2 (`blocks-tax-reporting →
   blocks-reports-tax`) is a sibling rename handled in a separate hand-off.

3. **Confirm ADR 0088 status.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```
   Expected: `status: Proposed` (CO ratified design 2026-05-16; status flip is
   a separate housekeeping PR). The hand-off is `ready-to-build` even with
   `status: Proposed` because the CO directive in the inbox is operative.

4. **Confirm no parallel-session PRs touch `blocks-financial-ledger/`.**
   ```bash
   gh pr list --state open --search "blocks-financial-ledger in:title,body"
   gh pr list --state open --search "blocks-financial in:title,body"
   ```
   Expected: empty. If anything is open, file `cob-question-*` before
   starting PR 1.

5. **Check existing consumers of `blocks-financial-ledger`.**
   ```bash
   grep -rln "Sunfish.Blocks.FinancialLedger" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ /Users/christopherwood/Projects/SunfishSoftware/Sunfish/apps/ /Users/christopherwood/Projects/SunfishSoftware/Sunfish/accelerators/
   grep -rln "blocks-financial-ledger" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/ --include="*.csproj"
   ```
   Expected: a small set of consumer projects (likely `blocks-rent-collection`,
   `accelerators/anchor/`, `apps/docs/`). PR 1 must update every match.

6. **Read the existing `blocks-financial-ledger/` types.**
   File: `packages/blocks-financial-ledger/Models/GLAccount.cs` (today's shape is
   minimal — `Id, Code, Name, Type, ParentAccountId`). Field extensions in
   PR 2 add the Stage 02 design's fields without breaking the existing
   constructor signature (positional record params remain; new params are
   optional with defaults).

7. **Confirm `but status` (or `git status`) is clean** and current branch
   is `main` (or a fresh worktree from `main`).

---

## Per-PR deliverables

This hand-off splits into **6 PRs** by responsibility. PRs 1 + 2 + 3 are
sequential (PR 2 can't compile without PR 1's rename; PR 3 builds on PR 2's
extended types). PR 4 + PR 5 can be parallelized after PR 3. PR 6 sequences
last.

---

### PR 1 — Rename `blocks-financial-ledger → blocks-financial-ledger` (mechanical)

**Estimated effort:** ~30–60 minutes
**Scope:** rename only; no behavior change; no new types; no new tests
**Commit subject:** `chore(blocks): rename blocks-financial-ledger to blocks-financial-ledger`
**Branch:** `cob/blocks-financial-ledger-rename`

#### File operations

```bash
# 1. Directory rename
git mv packages/blocks-financial-ledger packages/blocks-financial-ledger

# 2. csproj rename
git mv packages/blocks-financial-ledger/Sunfish.Blocks.FinancialLedger.csproj \
       packages/blocks-financial-ledger/Sunfish.Blocks.FinancialLedger.csproj
git mv packages/blocks-financial-ledger/tests/Sunfish.Blocks.FinancialLedger.Tests.csproj \
       packages/blocks-financial-ledger/tests/Sunfish.Blocks.FinancialLedger.Tests.csproj
```

#### Namespace updates

Rename `Sunfish.Blocks.FinancialLedger` → `Sunfish.Blocks.FinancialLedger` across:

- All `.cs` files inside `packages/blocks-financial-ledger/`
  (Models / Services / DependencyInjection / Localization / tests subfolders).
- The README.md inside the renamed package (update any cross-references).
- Every consumer that references the old namespace:
  - `packages/blocks-rent-collection/` (likely consumer — verify in pre-build
    step 5).
  - `accelerators/anchor/` (Anchor MAUI / Tauri shell — verify).
  - `apps/docs/` (cluster docs page if present — verify).
  - `tests/` projects that integration-test against the accounting types.

#### csproj reference updates

Every consumer `*.csproj` that has:

```xml
<ProjectReference Include="..\..\packages\blocks-financial-ledger\Sunfish.Blocks.FinancialLedger.csproj" />
```

becomes:

```xml
<ProjectReference Include="..\..\packages\blocks-financial-ledger\Sunfish.Blocks.FinancialLedger.csproj" />
```

#### Solution file (`Sunfish.sln` if present)

Update project paths + project names + GUIDs as needed via `dotnet sln` commands:

```bash
dotnet sln remove packages/blocks-financial-ledger/Sunfish.Blocks.FinancialLedger.csproj 2>/dev/null || true
dotnet sln add packages/blocks-financial-ledger/Sunfish.Blocks.FinancialLedger.csproj
# Repeat for the .Tests.csproj
```

#### Verification

- `dotnet build` succeeds across the solution.
- `dotnet test packages/blocks-financial-ledger/tests/` passes (existing
  tests should be unchanged in behavior).
- `grep -r "blocks-financial-ledger" packages/ accelerators/ apps/ tests/` returns
  zero hits (other than possibly a CHANGELOG-type historical reference, which
  is acceptable; archive/note in the PR description).
- `grep -r "Sunfish.Blocks.FinancialLedger" packages/ accelerators/ apps/ tests/`
  returns zero hits.

#### PR description template

```
Rename blocks-financial-ledger → blocks-financial-ledger per
xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md Decision 1.

This is a mechanical rename. No behavior changes. No new types. No new tests.

- `packages/blocks-financial-ledger/` → `packages/blocks-financial-ledger/`
- `Sunfish.Blocks.FinancialLedger.csproj` → `Sunfish.Blocks.FinancialLedger.csproj`
- Namespace `Sunfish.Blocks.FinancialLedger` → `Sunfish.Blocks.FinancialLedger`
- N consumers updated (list).

Brings the existing GL primitive package under the canonical
`blocks-financial-*` cluster naming per ADR 0088 §1. Subsequent PRs in
this hand-off extend the package per the Stage 02 schema design.

Refs: ADR 0088 §1; xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md
```

#### Do NOT in this PR

- Do NOT rename `GLAccount` → `Account`. The ratified naming is **keep
  GLAccount**. Open question §12.1 is closed via Decision 1 corollary.
- Do NOT extend any field shapes. That's PR 2.
- Do NOT add new tests. The PR's correctness is "build passes + existing
  tests pass."
- Do NOT bundle with feature work. Clean rename PR = reviewable diff.

---

### PR 2 — `GLAccount` schema extensions

**Estimated effort:** ~2–3h
**Scope:** add fields to `GLAccount` per Stage 02 §3.1; introduce
`AccountSubtype`, `NormalBalance`, `ChartOfAccounts`; preserve back-compat
on `GLAccount` constructor
**Commit subject:** `feat(blocks-financial-ledger): add ChartOfAccounts + GLAccount field extensions per Stage 02 §3.1`
**Depends on:** PR 1 merged
**Branch:** `cob/blocks-financial-ledger-glaccount-extensions`

#### New types

**`packages/blocks-financial-ledger/Models/ChartOfAccountsId.cs`** — ULID
strongly-typed id, mirrors the existing `GLAccountId` pattern.

**`packages/blocks-financial-ledger/Models/ChartOfAccounts.cs`** — record
per Stage 02 §3.2:

```csharp
public sealed record ChartOfAccounts(
    ChartOfAccountsId Id,
    LegalEntityId LegalEntityId,         // FK to foundation legal-entity registry
    string Name,                          // "Acero Properties LLC — Operating"
    string BaseCurrency,                  // ISO 4217 "USD"
    int FiscalYearStartMonth,             // 1..12
    int FiscalYearStartDay,               // 1..31
    GLAccountId? RetainedEarningsAccountId,
    bool IsActive,
    Instant CreatedAtUtc,
    Instant UpdatedAtUtc);
```

`LegalEntityId` may not yet exist on main; if absent, ship a stub
`Sunfish.Foundation.Identity.LegalEntityId` (ULID-typed) in
`packages/foundation-identity/` (out-of-scope micro-package) OR — if
foundation-identity doesn't exist either — ship as a local
`Sunfish.Blocks.FinancialLedger.LegalEntityId` placeholder with a
`// TODO: relocate to foundation-identity when that package lands` comment.
File a `cob-question-*` beacon if unsure which path to take.

**`packages/blocks-financial-ledger/Models/AccountSubtype.cs`** — enum per
Stage 02 §3.1:

```csharp
public enum AccountSubtype
{
    // Assets
    CurrentAsset, FixedAsset, BankAccount, AccountsReceivable,
    InventoryAsset, AccumulatedDepreciation, OtherAsset,
    // Liabilities
    CurrentLiability, AccountsPayable, LongTermLiability,
    TaxesPayable, OtherLiability,
    // Equity
    OwnersEquity, RetainedEarnings, Drawings,
    // Income
    OperatingIncome, OtherIncome,
    // Expense
    OperatingExpense, CostOfGoodsSold, InterestExpense,
    DepreciationExpense, OtherExpense,
}
```

**`packages/blocks-financial-ledger/Models/NormalBalance.cs`** — enum:
`Debit`, `Credit`.

#### Extended types

**`packages/blocks-financial-ledger/Models/GLAccount.cs`** — extend the
existing record with new optional-default fields per Stage 02 §3.1:

```csharp
public sealed record GLAccount(
    GLAccountId Id,
    string Code,
    string Name,
    GLAccountType Type,
    GLAccountId? ParentAccountId = null,
    // New fields below (PR 2 extensions per Stage 02 §3.1)
    ChartOfAccountsId? ChartId = null,
    AccountSubtype? Subtype = null,
    NormalBalance? NormalBalance = null,
    string? Description = null,
    string? Currency = null,
    bool IsActive = true,
    bool IsPostable = true,
    string? TaxLineMappingId = null,
    string? ExternalRef = null,
    Instant? CreatedAtUtc = null,
    Instant? UpdatedAtUtc = null);
```

All new fields default such that **existing callers compile unchanged**.
NormalBalance defaulting + back-compat handling is encapsulated in a
factory method (next sub-section).

#### Static factory: `GLAccount.Create`

To avoid forcing callers to author the `NormalBalance` correctly, add a
factory that derives it from `Type`:

```csharp
public static GLAccount Create(
    GLAccountId id,
    ChartOfAccountsId chartId,
    string code,
    string name,
    GLAccountType type,
    AccountSubtype subtype,
    string currency,
    GLAccountId? parentAccountId = null,
    bool isPostable = true,
    string? description = null,
    string? taxLineMappingId = null,
    string? externalRef = null,
    Instant? createdAtUtc = null)
{
    var normal = type switch
    {
        GLAccountType.Asset or GLAccountType.Expense => NormalBalance.Debit,
        _ => NormalBalance.Credit,
    };
    var now = createdAtUtc ?? Instant.Now;
    return new GLAccount(
        Id: id, Code: code, Name: name, Type: type,
        ParentAccountId: parentAccountId,
        ChartId: chartId, Subtype: subtype, NormalBalance: normal,
        Description: description, Currency: currency,
        IsActive: true, IsPostable: isPostable,
        TaxLineMappingId: taxLineMappingId, ExternalRef: externalRef,
        CreatedAtUtc: now, UpdatedAtUtc: now);
}
```

#### Validation helper

Add `GLAccount.Validate(parent: GLAccount? = null) → ValidationResult`
applying Stage 02 §3.1 rules:

1. NormalBalance matches Type (Asset/Expense → Debit; rest → Credit).
2. If `ParentAccountId` is non-null, parent's `Type` matches this account's
   `Type` and parent's `ChartId` matches this account's `ChartId`.
3. `Currency` is a 3-letter ISO 4217 code.

The validation helper is used by the (future) `InMemoryAccountingService`
when registering new accounts; existing callers of the constructor are not
broken (validation is opt-in via the helper, not a constructor invariant).

#### Tests (PR 2)

`packages/blocks-financial-ledger/tests/GLAccountExtensionsTests.cs`:

- `Create_DerivesNormalBalanceFromType_Asset` → `Debit`.
- `Create_DerivesNormalBalanceFromType_Liability` → `Credit`.
- `Create_DerivesNormalBalanceFromType_Equity` → `Credit`.
- `Create_DerivesNormalBalanceFromType_Revenue` → `Credit`.
- `Create_DerivesNormalBalanceFromType_Expense` → `Debit`.
- `Validate_ReturnsError_WhenParentTypeMismatch` (parent is Asset,
  child is Expense → error).
- `Validate_ReturnsError_WhenParentChartMismatch`.
- `Validate_Succeeds_OnWellFormedAccount`.
- `Validate_ReturnsError_OnInvalidCurrency` (e.g. "USDOLLAR" → 8-char
  fails ISO 4217 length check).
- `Constructor_BackCompat_WithOnlyOriginalArgs` (verify the original
  positional record construction still compiles + yields valid
  defaults — `IsActive=true`, `IsPostable=true`, etc.).

`packages/blocks-financial-ledger/tests/ChartOfAccountsTests.cs`:

- `Construction_PreservesAllFields`.
- (Light — `ChartOfAccounts` is a data record with no behavior yet; the
  full validation suite ships with PR 5.)

Total new tests this PR: ~10–12.

#### Verification

- `dotnet build` succeeds.
- `dotnet test packages/blocks-financial-ledger/tests/` passes (existing
  pre-PR-2 tests + ~10–12 new).
- No grep hits for old field absences across consumers (consumers don't
  touch the new fields; they're optional).

---

### PR 3 — `JournalEntry` + `JournalEntryLine` schema extensions

**Estimated effort:** ~2–3h
**Scope:** extend `JournalEntry` per Stage 02 §3.3 (status enum, sourceKind,
period, reversal, externalRef); extend `JournalEntryLine` per §3.4
(propertyId, classId, taxCodeId dimensional tags); preserve the existing
balance-check constructor invariant
**Commit subject:** `feat(blocks-financial-ledger): add JournalEntry status/source/period + dimensional line tags per Stage 02 §3.3–§3.4`
**Depends on:** PR 2 merged
**Branch:** `cob/blocks-financial-ledger-journalentry-extensions`

#### New enums

**`Models/JournalEntryStatus.cs`** per Stage 02 §3.3:

```csharp
public enum JournalEntryStatus { Draft, Posted, Reversed }
```

**`Models/JournalEntrySource.cs`** per Stage 02 §3.3:

```csharp
public enum JournalEntrySource
{
    Manual, Invoice, Bill, Payment, Receipt, Depreciation,
    Adjusting, Closing, Reversal, Migration,
}
```

**`Models/FiscalPeriodId.cs`** — ULID strongly-typed id.

(`FiscalPeriod` the entity is part of `blocks-financial-periods`; **not in
this hand-off**. The ID type lives here; the entity lands in a sibling
hand-off. `JournalEntry.PeriodId` is nullable in PR 3 — non-null becomes
mandatory once `-periods` ships.)

#### Extended types

**`Models/JournalEntry.cs`** — extend the existing record:

```csharp
public sealed record JournalEntry
{
    public JournalEntryId Id { get; }
    public DateOnly EntryDate { get; }
    public string Memo { get; }
    public IReadOnlyList<JournalEntryLine> Lines { get; }
    public Instant CreatedAtUtc { get; }
    public string? SourceReference { get; }

    // New PR 3 fields:
    public ChartOfAccountsId? ChartId { get; init; }
    public Instant? PostedAtUtc { get; init; }
    public JournalEntryStatus Status { get; init; } = JournalEntryStatus.Posted;
    public JournalEntrySource SourceKind { get; init; } = JournalEntrySource.Manual;
    public JournalEntryId? ReversalOf { get; init; }
    public JournalEntryId? ReversedBy { get; init; }
    public FiscalPeriodId? PeriodId { get; init; }
    public string? ExternalRef { get; init; }

    // Existing constructor preserved (positional):
    public JournalEntry(
        JournalEntryId id,
        DateOnly entryDate,
        string memo,
        IReadOnlyList<JournalEntryLine> lines,
        Instant createdAtUtc,
        string? sourceReference = null)
    {
        // ... existing balance-check invariant logic preserved verbatim ...
    }
}
```

**Important — preserve invariants:**

- The existing constructor's balance-check (`Σ debits == Σ credits`) MUST
  be retained verbatim.
- The existing `ArgumentException` thrown on imbalance MUST remain the
  exception type and message format (to avoid breaking any catch-clauses
  in consumers).
- New fields use `init;` accessors → the record is still immutable post-
  construction.

**`Models/JournalEntryLine.cs`** — extend with dimensional tags per
Stage 02 §3.4:

```csharp
public sealed record JournalEntryLine
{
    // existing fields preserved verbatim:
    public JournalEntryLineId Id { get; }
    public GLAccountId AccountId { get; }
    public decimal Debit { get; }
    public decimal Credit { get; }
    public string? LineMemo { get; }

    // new PR 3 fields:
    public int? LineNumber { get; init; }            // 1..n ordering within entry
    public string? PropertyId { get; init; }         // cost-center: which rental property
    public string? ClassificationId { get; init; }   // user-defined classification
    public string? TaxCodeId { get; init; }          // for tax-payable lines

    // existing constructor preserved
}
```

**Note on Money representation:** Stage 02 §7 prefers integer-minor-units
in the schema for new TypeScript-side fields; the existing C# implementation
uses `decimal` with two-decimal rounding. **Keep `decimal` here for
back-compat**; cross-implementation interop happens via canonical
serialization (out of scope for this hand-off).

#### `PostError` enum

Per Stage 02 §6.1 algorithm, posting can fail with structured errors.
Add the enum + Result wrapper:

```csharp
public enum PostError
{
    None,
    NotADraft,
    TooFewLines,
    Imbalanced,
    UnknownAccount,
    WrongChart,
    AccountNotPostable,
    CurrencyMismatch,
    NoPeriodForDate,
    PeriodLocked,
    PeriodSoftClosed,
}

public readonly record struct PostResult(
    JournalEntry? Entry,
    PostError Error,
    string? Detail);
```

The actual posting algorithm using these types ships in PR 4. PR 3 just
ships the types.

#### Tests (PR 3)

`packages/blocks-financial-ledger/tests/JournalEntryExtensionsTests.cs`:

- `Construction_StillEnforcesBalanceInvariant` (regression — pre-PR-2
  behavior unchanged).
- `Construction_WithDefaults_HasStatusPosted` (back-compat default).
- `WithStatus_Draft_ReturnsNewRecord` (init-only field assignability).
- `WithSourceKind_Migration_PreservesOtherFields`.
- `WithReversalOf_PopulatesField`.

`packages/blocks-financial-ledger/tests/JournalEntryLineExtensionsTests.cs`:

- `Construction_StillEnforcesDebitXorCredit` (regression).
- `WithPropertyId_PopulatesDimensionalTag`.
- `WithClassificationId_PopulatesDimensionalTag`.
- `WithTaxCodeId_PopulatesDimensionalTag`.

Total new tests this PR: ~9–10.

#### Verification

- `dotnet build` succeeds.
- All pre-PR-3 tests pass unchanged.
- New tests pass.
- The `JournalEntry` constructor signature is identical to pre-PR-3 (no
  breaking change to existing callers).

---

### PR 4 — Double-entry posting algorithm (atomic SQLite transaction)

**Estimated effort:** ~2–3h
**Scope:** implement `postJournalEntry` per Stage 02 §6.1; six-phase
algorithm (preconditions, balance, account-validity, period-gating, atomic
commit); SQLite transaction boundary; structured `PostError` results
**Commit subject:** `feat(blocks-financial-ledger): implement postJournalEntry atomic posting algorithm per Stage 02 §6.1`
**Depends on:** PR 3 merged
**Branch:** `cob/blocks-financial-ledger-posting-algorithm`

#### New service

**`Services/IJournalPostingService.cs`** — contract:

```csharp
public interface IJournalPostingService
{
    Task<PostResult> PostAsync(
        JournalEntry entry,
        CancellationToken cancellationToken = default);
}
```

**`Services/JournalPostingService.cs`** — implementation per Stage 02 §6.1:

```csharp
public sealed class JournalPostingService : IJournalPostingService
{
    private readonly IDbConnection _db;       // SQLite connection (injected)
    private readonly IAccountResolver _accounts;  // lookup GLAccount by id
    private readonly IPeriodResolver _periods;    // lookup FiscalPeriod by chart+date (stub OK for now)
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public async Task<PostResult> PostAsync(JournalEntry entry, CancellationToken ct)
    {
        // Phase 1 — preconditions
        if (entry.Status != JournalEntryStatus.Draft)
            return new PostResult(null, PostError.NotADraft, null);
        if (entry.Lines.Count < 2)
            return new PostResult(null, PostError.TooFewLines, null);

        // Phase 2 — balance (already enforced by ctor, but defense-in-depth)
        var debitSum = entry.Lines.Sum(l => l.Debit);
        var creditSum = entry.Lines.Sum(l => l.Credit);
        if (debitSum != creditSum)
            return new PostResult(null, PostError.Imbalanced,
                $"debits={debitSum:F2}, credits={creditSum:F2}");

        // Phase 3 — account validity
        foreach (var line in entry.Lines)
        {
            var acct = await _accounts.GetAsync(line.AccountId, ct);
            if (acct is null) return new PostResult(null, PostError.UnknownAccount, line.AccountId.Value);
            if (entry.ChartId is not null && acct.ChartId != entry.ChartId)
                return new PostResult(null, PostError.WrongChart, null);
            if (!acct.IsPostable)
                return new PostResult(null, PostError.AccountNotPostable, line.AccountId.Value);
            // Currency check elided in v1 (single-currency per chart);
            // re-add when multi-currency lands.
        }

        // Phase 4 — period gating
        if (entry.ChartId is not null)
        {
            var period = await _periods.ResolveAsync(entry.ChartId.Value, entry.EntryDate, ct);
            if (period is null) return new PostResult(null, PostError.NoPeriodForDate, null);
            if (period.Status == FiscalPeriodStatus.Locked)
                return new PostResult(null, PostError.PeriodLocked, null);
            if (period.Status == FiscalPeriodStatus.SoftClosed && !_user.HasRole("FinancialAdmin"))
                return new PostResult(null, PostError.PeriodSoftClosed, null);
        }

        // Phase 5 — atomic commit
        await using var tx = _db.BeginTransaction();
        try
        {
            var posted = entry with
            {
                Status = JournalEntryStatus.Posted,
                PostedAtUtc = _time.GetUtcNow(),
                // PeriodId set if resolvable
            };
            await _db.ExecuteAsync(/* INSERT INTO journal_entries ... */, posted, tx);
            foreach (var line in posted.Lines)
                await _db.ExecuteAsync(/* INSERT INTO journal_lines ... */, line, tx);
            tx.Commit();
            return new PostResult(posted, PostError.None, null);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
```

(Code is illustrative; COB to fit to project conventions —
`Microsoft.Data.Sqlite` directly OR via an existing repository pattern
already in the codebase if one is in use.)

#### Supporting stubs

`IAccountResolver` + `IPeriodResolver` may resolve to in-memory implementations
in PR 4 (the SQLite persistence layer is part of a future hand-off). The
stubs:

- `InMemoryAccountResolver` — backed by a `Dictionary<GLAccountId, GLAccount>`
  seeded by tests.
- `InMemoryPeriodResolver` — always returns an Open period. PR 4 just
  needs to compile + test against these; real implementations land with
  `blocks-financial-periods`.

`FiscalPeriodStatus` enum (local placeholder in this PR; relocated to
`blocks-financial-periods` when that ships):

```csharp
public enum FiscalPeriodStatus { Open, SoftClosed, Locked }
```

#### Tests (PR 4)

`packages/blocks-financial-ledger/tests/JournalPostingServiceTests.cs`:

- `Post_RejectsNonDraft` → `NotADraft`.
- `Post_RejectsSingleLine` → `TooFewLines`.
- `Post_RejectsImbalanced` → `Imbalanced` (with detail string).
- `Post_RejectsUnknownAccount` → `UnknownAccount`.
- `Post_RejectsWrongChart` → `WrongChart`.
- `Post_RejectsNonPostableAccount` → `AccountNotPostable`.
- `Post_RejectsLockedPeriod` → `PeriodLocked`.
- `Post_AllowsAdminToPostSoftClosed` (FinancialAdmin role bypass).
- `Post_RejectsSoftClosedForNonAdmin` → `PeriodSoftClosed`.
- `Post_ValidEntry_PromotesDraftToPosted_AndCommitsAtomically` (happy path).
- `Post_OnExceptionDuringCommit_RollsBack` (transaction rollback via
  in-memory SQLite + induced failure).
- `Post_PostedEntryHasPostedAtUtcSet`.
- `Post_BalanceArithmeticUsesInteger_NoFloatRoundoff` (regression — feed
  decimal values that would round-error under double; verify exact equality).

Total new tests this PR: ~12–14.

#### Verification

- All previous tests pass.
- New posting-service tests pass.
- Atomic-commit test demonstrably rolls back partial writes (verified by
  asserting no `journal_lines` rows exist post-failure).

---

### PR 5 — ChartOfAccounts seed templates + service surface

**Estimated effort:** ~2–3h
**Scope:** seed data for default chart-of-accounts templates per Stage 02
§3 (property-business-tuned chart); `IChartSeedingService` to seed a new
chart from a template; `apps/docs/blocks-financial-ledger/overview.md`
docs page
**Commit subject:** `feat(blocks-financial-ledger): seed default chart-of-accounts templates + chart-seeding service`
**Depends on:** PR 4 merged
**Branch:** `cob/blocks-financial-ledger-chart-seeding`

#### New types

**`Seeds/DefaultChartTemplates.cs`** — static catalogue:

```csharp
public static class DefaultChartTemplates
{
    public static readonly ChartTemplate RentalRealEstate = new(
        Name: "Rental Real Estate (US, single LLC)",
        Description: "Suitable for a property LLC with rental income, " +
                     "operating expenses, and Schedule E reporting needs.",
        Accounts: new[]
        {
            // 1xxx Assets
            new ChartTemplateAccount("1000", "Assets", GLAccountType.Asset, AccountSubtype.OtherAsset, IsPostable: false),
            new ChartTemplateAccount("1100", "Current Assets", GLAccountType.Asset, AccountSubtype.CurrentAsset, ParentCode: "1000", IsPostable: false),
            new ChartTemplateAccount("1110", "Operating Bank Account", GLAccountType.Asset, AccountSubtype.BankAccount, ParentCode: "1100"),
            new ChartTemplateAccount("1120", "Security Deposit Holding (Bank)", GLAccountType.Asset, AccountSubtype.BankAccount, ParentCode: "1100"),
            new ChartTemplateAccount("1130", "Accounts Receivable", GLAccountType.Asset, AccountSubtype.AccountsReceivable, ParentCode: "1100"),
            new ChartTemplateAccount("1500", "Fixed Assets", GLAccountType.Asset, AccountSubtype.FixedAsset, ParentCode: "1000", IsPostable: false),
            new ChartTemplateAccount("1510", "Buildings", GLAccountType.Asset, AccountSubtype.FixedAsset, ParentCode: "1500"),
            new ChartTemplateAccount("1520", "Land", GLAccountType.Asset, AccountSubtype.FixedAsset, ParentCode: "1500"),
            new ChartTemplateAccount("1530", "Equipment", GLAccountType.Asset, AccountSubtype.FixedAsset, ParentCode: "1500"),
            new ChartTemplateAccount("1590", "Accumulated Depreciation", GLAccountType.Asset, AccountSubtype.AccumulatedDepreciation, ParentCode: "1500"),

            // 2xxx Liabilities
            new ChartTemplateAccount("2000", "Liabilities", GLAccountType.Liability, AccountSubtype.OtherLiability, IsPostable: false),
            new ChartTemplateAccount("2100", "Current Liabilities", GLAccountType.Liability, AccountSubtype.CurrentLiability, ParentCode: "2000", IsPostable: false),
            new ChartTemplateAccount("2110", "Accounts Payable", GLAccountType.Liability, AccountSubtype.AccountsPayable, ParentCode: "2100"),
            new ChartTemplateAccount("2120", "Security Deposits Held", GLAccountType.Liability, AccountSubtype.CurrentLiability, ParentCode: "2100"),
            new ChartTemplateAccount("2130", "Sales Tax Payable", GLAccountType.Liability, AccountSubtype.TaxesPayable, ParentCode: "2100"),
            new ChartTemplateAccount("2500", "Long-Term Liabilities", GLAccountType.Liability, AccountSubtype.LongTermLiability, ParentCode: "2000", IsPostable: false),
            new ChartTemplateAccount("2510", "Mortgages Payable", GLAccountType.Liability, AccountSubtype.LongTermLiability, ParentCode: "2500"),

            // 3xxx Equity
            new ChartTemplateAccount("3000", "Equity", GLAccountType.Equity, AccountSubtype.OwnersEquity, IsPostable: false),
            new ChartTemplateAccount("3100", "Owner's Capital", GLAccountType.Equity, AccountSubtype.OwnersEquity, ParentCode: "3000"),
            new ChartTemplateAccount("3200", "Owner's Drawings", GLAccountType.Equity, AccountSubtype.Drawings, ParentCode: "3000"),
            new ChartTemplateAccount("3900", "Retained Earnings", GLAccountType.Equity, AccountSubtype.RetainedEarnings, ParentCode: "3000"),

            // 4xxx Revenue
            new ChartTemplateAccount("4000", "Revenue", GLAccountType.Revenue, AccountSubtype.OperatingIncome, IsPostable: false),
            new ChartTemplateAccount("4100", "Rental Income", GLAccountType.Revenue, AccountSubtype.OperatingIncome, ParentCode: "4000"),
            new ChartTemplateAccount("4200", "Late Fee Income", GLAccountType.Revenue, AccountSubtype.OperatingIncome, ParentCode: "4000"),
            new ChartTemplateAccount("4900", "Other Income", GLAccountType.Revenue, AccountSubtype.OtherIncome, ParentCode: "4000"),

            // 5xxx-6xxx Expenses (Schedule E line-mapped)
            new ChartTemplateAccount("5000", "Expenses", GLAccountType.Expense, AccountSubtype.OperatingExpense, IsPostable: false),
            new ChartTemplateAccount("5100", "Advertising", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),       // Schedule E Line 5
            new ChartTemplateAccount("5200", "Cleaning and Maintenance", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),  // Line 7
            new ChartTemplateAccount("5300", "Insurance", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),         // Line 9
            new ChartTemplateAccount("5400", "Legal and Professional Fees", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),  // Line 10
            new ChartTemplateAccount("5500", "Management Fees", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),   // Line 11
            new ChartTemplateAccount("5600", "Repairs", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),           // Line 14
            new ChartTemplateAccount("5700", "Supplies", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),          // Line 15
            new ChartTemplateAccount("5800", "Utilities", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),         // Line 17
            new ChartTemplateAccount("6100", "Property Tax", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),      // Line 16
            new ChartTemplateAccount("7110", "Mortgage Interest", GLAccountType.Expense, AccountSubtype.InterestExpense, ParentCode: "5000"),  // Line 12
            new ChartTemplateAccount("7200", "Depreciation Expense", GLAccountType.Expense, AccountSubtype.DepreciationExpense, ParentCode: "5000"),  // Line 18
        });

    public static readonly ChartTemplate SmallBusinessGeneral = new( /* ... */ );
}
```

**Note on Schedule E mapping:** the comments inline link the chart-of-
accounts codes to Schedule E lines per `blocks-reports-schema-design.md`
§8.1. This is the seed data that the `TaxFormLineMap` rows in
`blocks-reports-tax` will reference. Don't attempt to seed `TaxFormLineMap`
here — that's a separate `blocks-reports-*` hand-off.

#### New service

**`Services/IChartSeedingService.cs`**:

```csharp
public interface IChartSeedingService
{
    Task<ChartOfAccounts> SeedChartAsync(
        LegalEntityId legalEntityId,
        string chartName,
        ChartTemplate template,
        string baseCurrency = "USD",
        CancellationToken cancellationToken = default);
}
```

**`Services/InMemoryChartSeedingService.cs`** — in-memory implementation
that:

1. Creates a `ChartOfAccounts` record.
2. Topologically sorts template accounts by `ParentCode`.
3. Creates `GLAccount` records via `GLAccount.Create(...)`, resolving
   parent IDs as it goes.
4. Returns the new `ChartOfAccounts`.

#### Tests (PR 5)

`tests/DefaultChartTemplatesTests.cs`:

- `RentalRealEstate_AllAccountsHavePostableTrueExceptGroups`.
- `RentalRealEstate_ParentCodesAllResolve` (no dangling references).
- `RentalRealEstate_ScheduleELineCoverage` (verify every Schedule E line
  5–18 has at least one mapped account).

`tests/ChartSeedingServiceTests.cs`:

- `SeedChart_CreatesChartOfAccountsRecord`.
- `SeedChart_CreatesAllTemplateAccounts`.
- `SeedChart_ParentLinksResolveCorrectly`.
- `SeedChart_NormalBalanceDerivedFromType_ForAllSeededAccounts`.
- `SeedChart_SetsBaseCurrencyFromArg`.

Total new tests this PR: ~8.

#### Docs

**`apps/docs/blocks-financial-ledger/overview.md`** — cluster docs page
following the established convention (cite ADR 0088 §1; cite Stage 02
schema design §3.1–§3.4; cite Stage 02 §6.1 for posting algorithm; cite
ratified naming decision).

Structure:

```
# blocks-financial-ledger

Foundational journal-entry + chart-of-accounts package for the Sunfish
Anchor native financial domain.

## Overview

This package is the canonical general-ledger layer of the
`blocks-financial-*` cluster per ADR 0088 §1. It provides:

- `GLAccount` — a node in the chart of accounts.
- `ChartOfAccounts` — container for a set of `GLAccount` records.
- `JournalEntry` + `JournalEntryLine` — atomic double-entry transactions.
- `JournalPostingService` — six-phase posting algorithm enforcing the
  textbook double-entry invariant (Σ debits == Σ credits, exact, no
  tolerance).
- `DefaultChartTemplates` — Rental Real Estate + Small Business General
  starter charts.

## Naming

Per the 2026-05-16 naming-ratification ruling, the canonical entity is
`GLAccount` (not `Account` — the unqualified name is reserved for a
future identity-tier concept).

## Quickstart
... (10-15 lines, minimal example)

## Algorithms
... (link to Stage 02 §6.1)

## Related
- `blocks-financial-chart` (Phase 1 follow-on)
- `blocks-financial-ar` / `-ap` / `-payments` (Phase 1 follow-ons)
- `blocks-financial-periods` (Phase 1 follow-on — provides FiscalPeriod)
- `blocks-financial-tax` (Phase 1 follow-on)
- `blocks-rent-collection` (existing; will wrap the canonical AR Invoice
  when that lands per the 2026-05-16 ratification Decision 3)
```

#### Verification

- All previous tests pass.
- New tests pass.
- `apps/docs/blocks-financial-ledger/overview.md` renders without broken
  links (relative to ADR + Stage 02 doc paths).

---

### PR 6 — Migration importer entry-point (Pass 1 + Pass 3 hook)

**Estimated effort:** ~2–3h
**Scope:** add an `IFromErpnextImporter` interface in the ledger package
that the importer (in a future package) consumes; implement the Pass 1
account-import hook and the Pass 3 opening-balance-JE hook; idempotent
upsert on `externalRef`
**Commit subject:** `feat(blocks-financial-ledger): add ERPNext importer entry-points (Pass 1 account upsert + Pass 3 opening-balance JE post)`
**Depends on:** PR 5 merged
**Branch:** `cob/blocks-financial-ledger-importer-hooks`

#### New interfaces

**`Migration/IErpnextAccountImporter.cs`**:

```csharp
public interface IErpnextAccountImporter
{
    /// <summary>
    /// Upserts an account from an ERPNext source record. Idempotent on
    /// (source, externalRefId).
    /// </summary>
    /// <returns>The resolved GLAccount + an outcome marker.</returns>
    Task<ImportOutcome<GLAccount>> UpsertFromErpnextAsync(
        ErpnextAccountSource source,
        ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default);
}

public sealed record ErpnextAccountSource(
    string Name,                  // ERPNext "name" — the stable id
    string Modified,              // ERPNext "modified" — the version key
    string AccountName,
    string? AccountNumber,
    string? ParentAccountName,    // ERPNext parent reference (by name)
    string? AccountType,          // raw ERPNext account_type string
    bool IsGroup,
    bool Disabled);

public sealed record ImportOutcome<T>(
    T Record,
    ImportAction Action,          // Inserted | Updated | Skipped
    string? Detail);

public enum ImportAction { Inserted, Updated, Skipped }
```

**`Migration/IErpnextJournalEntryImporter.cs`**:

```csharp
public interface IErpnextJournalEntryImporter
{
    /// <summary>
    /// Upserts a journal entry from an ERPNext source record. Idempotent
    /// on (source, externalRefId). Posts via the canonical posting
    /// algorithm; opening-balance entries (IsOpening=true) bypass the
    /// entryDate-must-be-on-or-before-today check via sourceKind=Migration.
    /// </summary>
    Task<ImportOutcome<JournalEntry>> UpsertFromErpnextAsync(
        ErpnextJournalEntrySource source,
        ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default);
}

public sealed record ErpnextJournalEntrySource(
    string Name,                  // ERPNext "name"
    string Modified,
    DateOnly PostingDate,
    string Memo,
    string VoucherType,           // raw ERPNext voucher_type string
    bool IsOpening,
    int DocStatus,                // 0/1/2
    IReadOnlyList<ErpnextJournalEntryLineSource> Lines);

public sealed record ErpnextJournalEntryLineSource(
    string AccountName,           // ERPNext account reference (by name)
    decimal DebitInAccountCurrency,
    decimal CreditInAccountCurrency,
    string? CostCenter,
    string? UserRemark);
```

#### Implementations

**`Migration/ErpnextAccountImporter.cs`** — wraps `GLAccount.Create` with
the §3.2 enum mapping table (the migration-importer spec §3.2). Per-record
flow:

1. Look up existing `GLAccount` by `ExternalRef == source.Name`.
2. If exists and version unchanged → return `Skipped`.
3. If exists and version moved forward → update fields, return `Updated`.
4. If new → resolve parent (recursive lookup by parent's `ExternalRef`,
   which must be already-imported per topological sort), apply enum
   mapping, call `GLAccount.Create(...)`, persist, return `Inserted`.

**`Migration/ErpnextJournalEntryImporter.cs`** — wraps
`JournalPostingService.PostAsync` with the source-kind mapping per
the migration-importer spec §3.3. Per-record flow:

1. Look up existing `JournalEntry` by `ExternalRef == source.Name`.
2. If exists → return `Skipped` (posted entries are immutable; per the
   migration-importer spec §5.2 the importer warns rather than updates).
3. Map `VoucherType` → `JournalEntrySource` per the table.
4. If `IsOpening`, override `SourceKind = Migration` regardless of voucher
   type.
5. Build `JournalEntry` (Draft) + `JournalEntryLine[]` (resolved via
   `IAccountResolver.GetByExternalRefAsync(line.AccountName)`).
6. Post via `IJournalPostingService.PostAsync(...)`.
7. Return `Inserted` with the posted entry.

#### Tests (PR 6)

`tests/ErpnextAccountImporterTests.cs`:

- `Upsert_NewSource_InsertsGLAccount`.
- `Upsert_SameVersion_ReturnsSkipped`.
- `Upsert_HigherVersion_ReturnsUpdated`.
- `Upsert_LowerVersion_ReturnsSkipped` (clock-drift / re-export-of-old).
- `Upsert_MapsErpnextAccountTypeCorrectly_Bank` → `Asset/BankAccount`.
- `Upsert_MapsErpnextAccountTypeCorrectly_Receivable` → `Asset/AccountsReceivable`.
- `Upsert_MapsErpnextAccountTypeCorrectly_IncomeAccount` → `Revenue/OperatingIncome`.
- `Upsert_MapsErpnextAccountTypeCorrectly_ExpenseAccount` → `Expense/OperatingExpense`.
- `Upsert_UnknownAccountType_WalksUpParentTree`.
- `Upsert_UnknownAccountType_AfterParentWalk_Throws`.
- `Upsert_IsGroupTrue_SetsIsPostableFalse`.
- `Upsert_DisabledTrue_SetsIsActiveFalse`.
- `Upsert_ParentResolvesByExternalRef`.

`tests/ErpnextJournalEntryImporterTests.cs`:

- `Upsert_NewSource_PostsJournalEntry`.
- `Upsert_DuplicateSource_ReturnsSkipped`.
- `Upsert_MapsVoucherTypeCorrectly_OpeningEntry` → `Migration`.
- `Upsert_MapsVoucherTypeCorrectly_BankEntry` → `Payment`.
- `Upsert_MapsVoucherTypeCorrectly_DepreciationEntry` → `Depreciation`.
- `Upsert_IsOpeningTrue_OverridesSourceKindToMigration` (regardless of voucher).
- `Upsert_ImbalancedSource_ReturnsImbalancedError_RecordNotPersisted`.
- `Upsert_UnknownAccountNameInLine_ReturnsUnknownAccountError`.
- `Upsert_PreservesEntryDate_EvenIfBackdated_WhenSourceKindMigration`.

Total new tests this PR: ~22.

#### DI registration

`DependencyInjection/ServiceCollectionExtensions.cs` — extend the existing
`AddBlocksAccounting()` (renamed in PR 1 to `AddBlocksFinancialLedger()`)
to register the new services:

```csharp
public static IServiceCollection AddBlocksFinancialLedger(this IServiceCollection services)
{
    services.AddSingleton<IJournalPostingService, JournalPostingService>();
    services.AddSingleton<IChartSeedingService, InMemoryChartSeedingService>();
    services.AddSingleton<IErpnextAccountImporter, ErpnextAccountImporter>();
    services.AddSingleton<IErpnextJournalEntryImporter, ErpnextJournalEntryImporter>();
    services.AddSingleton<IAccountResolver, InMemoryAccountResolver>();
    services.AddSingleton<IPeriodResolver, InMemoryPeriodResolver>();
    // ... existing registrations preserved
    return services;
}
```

#### Verification

- All tests across PRs 1-6 pass (~55 tests total).
- The package builds standalone (no dependency on packages that haven't
  landed yet — `LegalEntityId` stub or relocation per PR 2 step).
- `apps/docs/blocks-financial-ledger/overview.md` references the
  importer surface (one-paragraph mention).

---

## CRDT-friendly schema conventions applied

This hand-off applies the cluster's CRDT-friendly conventions (per the
forthcoming `_shared/engineering/crdt-friendly-schema-conventions.md`
synthesis doc from a parallel XO subagent). The relevant patterns for
this hand-off:

### 1. Posted-then-immutable JournalEntry

Per Stage 02 §3.3 validation rule 6 and §3.3 workflow diagram: once a
`JournalEntry` transitions `Draft → Posted`, **it is immutable**. No
field mutation. No `lines[]` mutation. Corrections happen via a
reversal entry + new entry.

This is **the canonical CRDT-friendly pattern** for the ledger: the
journal becomes an append-only log of immutable entries. Conflict
resolution under Loro CRDT is trivial — every entry is content-addressed
by its `Id` (ULID) + `ExternalRef`; conflict = "both peers have an entry
with the same ID" which can only happen if the same source record was
imported twice in parallel (idempotency contract §5 catches this).

### 2. Draft-stage mutability

Drafts (`status: Draft`) are mutable. They are typed as "scratch space
not yet committed to the ledger." In the Loro CRDT layer, drafts are
expected to be peer-local and not synced across nodes (the
synchronization boundary is the `Posted` state transition). This means
draft-side conflicts cannot arise; the user's local draft is the only
draft.

### 3. ExternalRef as the idempotency key

Every persisted entity carries `ExternalRef` (optional; populated by the
migration importer). The `(externalRef.source, externalRef.id)` tuple is
the idempotency key. Re-import = look-up + skip-or-update. This pattern
applies across the cluster (Invoice, Bill, Payment all carry the same
field per Stage 02 §3.5, §3.7, §3.9).

### 4. Balance cache as derived state

Stage 02 §6.1 mentions an optional balance cache as a read-side optimization.
**Not built in this hand-off.** When built (a future hand-off), the
cache is **derived state** — fully rebuildable from primary `JournalLine`
data. Authoritative = `Σ JournalLine.debit - Σ JournalLine.credit`. The
cache is local-only (not synced via Loro); peers rebuild on receive.

### Open question Q10 (financial design) — Loro append-only constraint

Per the ratification ruling, Q10 of `blocks-financial-schema-design.md`
("Loro CRDT append-only constraint on posted journal entries — needs
`foundation-localfirst` owner coordination") remains **open** at the
hand-off cutoff. **The PRs in this hand-off do not depend on Q10 being
resolved** — they implement the immutable-after-post invariant at the
service layer (`JournalPostingService` rejects any
`UPDATE` on `status == Posted`), which is independent of Loro semantics.

When `foundation-localfirst` owner ratifies the Loro append-only
contract, the integration is additive (a future hand-off wires Loro
op-mapping into the SQLite write path). Until then, the SQLite-side
invariant is sufficient for Phase 1.

**Halt condition:** if cob hits a Loro-related decision that this
hand-off doesn't cover, file `cob-question-*` (see §Halt-conditions).

---

## License posture

### Borrowed-with-attribution (permissive)

- **Apache OFBiz** `accounting` and `party` modules (Apache 2.0) — the
  cluster's entity shapes (`GLAccount` hierarchy with `parentAccountId`
  + `isPostable`; the chart-of-accounts seed-template approach) derive
  from OFBiz's pattern per `blocks-financial-schema-design.md` §11.1.

**Attribution requirements:**

1. The package's `Sunfish.Blocks.FinancialLedger.csproj` carries a
   `<PropertyGroup>` `<NOTICEFile>NOTICE.md</NOTICEFile>` reference.
2. `packages/blocks-financial-ledger/NOTICE.md` (new file in PR 2)
   states:

```markdown
# NOTICE — Sunfish.Blocks.FinancialLedger

This package's entity shapes (GLAccount hierarchy with parent linkage +
isPostable; chart-of-accounts seeding pattern; invoice/bill skeleton)
derive from Apache OFBiz's `accounting` and `party` entity models
(<https://ofbiz.apache.org/>, Apache 2.0 license).

OFBiz version studied: v18.12.x (as of 2026-05-16).

The Sunfish implementation is original code, distributed under the
MIT License. The OFBiz entity-shape pattern is reproduced with
attribution per Apache 2.0 §4(c) of the OFBiz License.
```

3. Source-header comments on `GLAccount.cs`, `JournalEntry.cs`,
   `ChartOfAccounts.cs`, and `Seeds/DefaultChartTemplates.cs` reference
   OFBiz in a one-line comment.

### Clean-room only (copyleft)

Per `blocks-financial-schema-design.md` §11.2–§11.5, the following
sources were studied for understanding only and contribute NO code to
this hand-off:

- Beancount + ledger-cli (GPLv2) — textbook double-entry data model.
- GnuCash (GPLv2) — AR aging conventions (Phase 1 follow-on hand-off).
- Akaunting (GPLv3) — small-business AR/AP status vocabulary (Phase 1
  follow-on hand-off).
- ERPNext + Frappe (GPLv3) — DocType structure of the migration source
  side; consumed as a **data format**, not code.

**Discipline check before merging any PR in this hand-off:**

1. No copyleft code was opened in any editor session that produced this
   hand-off's PRs.
2. No identifier names from any GPL/AGPL source appear in the new code.
   (Spot-check by grep before merge.)
3. The clean-room schema in Stage 02 §3 is the source of truth for type
   shapes; deviations from Stage 02 require XO ratification.

### Sunfish output

**All code authored under this hand-off is MIT-licensed**, per ADR 0088
§2 and the project-wide license posture.

---

## Test plan

### Per-PR minima (summary; details under each PR above)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (rename) | 0 new (existing pass) | regression of existing |
| PR 2 (GLAccount extensions) | ~10–12 | new field defaults; NormalBalance derivation; validation rules |
| PR 3 (JE/Line extensions) | ~9–10 | back-compat preservation; init-only field assignability |
| PR 4 (posting algorithm) | ~12–14 | all 6 algorithm phases; happy + every failure path |
| PR 5 (chart seeding) | ~8 | template integrity; seeding service |
| PR 6 (importer hooks) | ~22 | account upsert; JE upsert; idempotency; enum mapping |
| **Total** | **~55** | |

### Cluster-level acceptance (PASS gate at end of PR 6)

**A1.** `dotnet build` succeeds on the renamed `Sunfish.Blocks.FinancialLedger`
package and every downstream consumer.

**A2.** `dotnet test packages/blocks-financial-ledger/tests/` passes ~55
tests across all 6 PRs.

**A3.** A seeded chart from `DefaultChartTemplates.RentalRealEstate`
contains:
- 1 `ChartOfAccounts` record.
- ~35 `GLAccount` records (counting both group and postable nodes).
- Every `parentAccountId` resolves to a present account in the same
  chart.
- Every `normalBalance` matches its `type` per Stage 02 §3.1 rule.

**A4.** A posted `JournalEntry` (via the new `JournalPostingService`)
with `Σ debits == Σ credits == $1,234.56`:
- Transitions `Draft → Posted` atomically.
- Surfaces `PostError.None`.
- Has `PostedAtUtc` populated.
- Is rejected on a subsequent attempt to mutate it (immutability).

**A5.** A reposted (same `externalRef`) ERPNext source record via
`IErpnextAccountImporter`:
- First call → `ImportAction.Inserted`.
- Second call (same version) → `ImportAction.Skipped`.
- Third call (higher version) → `ImportAction.Updated`.

**A6.** Balance sheet ties out: a seeded chart with no transactions
sums to `Asset == Liability + Equity` (zero on both sides; the equation
holds trivially). Sum is over `JournalLine.debit - JournalLine.credit`
grouped by `GLAccount.Type`.

**A7.** Migration importer integration: a small synthetic ERPNext export
(say, 3 accounts + 1 opening JE) processes through PR 6's importer hooks
without error; resulting records are queryable via the existing
`IAccountingService` interface (renamed in PR 1).

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*`
beacon to `coordination/inbox/`:

### 1. `LegalEntityId` placement (PR 2)

If `Sunfish.Foundation.Identity.LegalEntityId` doesn't exist yet and you
need to decide whether to ship a local placeholder vs author the
foundation-tier package, file
`cob-question-2026-05-XXTHH-MMZ-w60-p4-legal-entity-id-placement.md`.
Recommended fall-back: local placeholder in
`Sunfish.Blocks.FinancialLedger.LegalEntityId` with a TODO comment for
relocation when `foundation-identity` lands. (Stage 02 §3.2 specifies
the field but doesn't pin the namespace.)

### 2. Loro append-only constraint surfaces (any PR)

If during PR 3-6 you hit a question about how `JournalEntry` immutability
interacts with Loro CRDT operations (e.g., "should the SQLite write be
mirrored as a Loro op or skipped?"), this is Q10 of
`blocks-financial-schema-design.md`. **Skip Loro integration entirely in
this hand-off** (Phase 1 substrate is SQLite-only). File
`cob-question-2026-05-XXTHH-MMZ-w60-p4-loro-jp-append-only.md` only if
the question blocks compilation of a PR (it shouldn't — the SQLite-side
invariant is independent).

### 3. Migration importer dependency sequencing (PR 6)

PR 6 introduces `IErpnextAccountImporter` + `IErpnextJournalEntryImporter`
in the `blocks-financial-ledger` package. The **importer itself** (the
6-pass orchestrator per
`_shared/engineering/erpnext-to-anchor-migration-importer-spec.md`) lives
in a separate future package (`tooling/anchor-import` or similar).

If COB wants to scaffold the orchestrator alongside, file
`cob-question-*` — that's a separate hand-off (not in this scope).
This hand-off ships the **integration points** the orchestrator will
consume; not the orchestrator.

### 4. `FiscalPeriod` package not yet built (PR 4)

PR 4's posting algorithm references `FiscalPeriod` for period-gating
(Stage 02 §6.1 Phase 4). The `blocks-financial-periods` package is a
sibling Phase 1 hand-off that hasn't shipped yet. **Mitigation built
into PR 4:** ship `IPeriodResolver` + `InMemoryPeriodResolver` stub
(always returns Open period). When `-periods` lands in a follow-on,
the real resolver replaces the stub via DI swap; the
`JournalPostingService` interface doesn't change.

If COB feels strongly about gating PR 4 on `-periods` landing first,
file `cob-question-*`. XO recommendation: ship PR 4 with the stub now;
unblock the importer + downstream cluster hand-offs.

### 5. `LegalEntityId` foundation tier already exists

If `Sunfish.Foundation.Identity.LegalEntityId` (or equivalent — e.g.
`Sunfish.Foundation.Tenants.LegalEntityId`) already exists in some
foundation package, USE it (don't ship a new placeholder). The grep:

```bash
grep -r "LegalEntityId" packages/foundation-* tests/ 2>/dev/null | head -20
```

Verify before PR 2.

### 6. Loro / SQLite write-path conflict (any PR)

If PR 4-6 trips over an existing Loro CRDT integration in the
`blocks-financial-ledger → blocks-financial-ledger` package (post-rename),
note that **the existing `blocks-financial-ledger` is in-memory only as of
2026-05-16** — there is no SQLite persistence layer in the package
today. PR 4 introduces SQLite-side persistence via the
`JournalPostingService`. If a sibling PR (e.g., from a parallel session)
introduces a different SQLite write-path between this hand-off
authoring and the build, file `cob-question-*`.

### 7. `apps/docs` infrastructure absent

If `apps/docs/blocks-financial-ledger/` doesn't have the expected
directory structure (e.g., the docs site uses a different convention),
file `cob-question-*`. XO recommendation: follow the existing
`apps/docs/{cluster}/overview.md` pattern observed for other clusters
(`apps/docs/foundation-mission-space/overview.md`, etc.). If that
pattern itself has changed, surface to XO.

### 8. Account naming question resurfaces

The naming ratification (Decision 1 corollary) closed the
`Account` vs `GLAccount` question. **DO NOT rename `GLAccount` →
`Account` in any PR.** If a council reviewer suggests the rename,
respond with reference to
`coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md`
Decision 1 corollary. If the suggestion has new substance not addressed
in the ratification, file `cob-question-*`.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1-6 merged to main** (sequentially or with parallelization
   where allowed).
2. **Chart-of-accounts seeded:** `DefaultChartTemplates.RentalRealEstate`
   produces a ~35-account chart via `IChartSeedingService.SeedChartAsync(...)`.
3. **Journal entries postable:** the existing `JournalEntry` immutability
   invariant + new `JournalPostingService` produce balanced posted entries
   atomically (per acceptance test A4).
4. **Balance sheet ties out:** the acceptance test A6 passes (a seeded
   chart with no transactions sums to `Asset == Liability + Equity`).
5. **Migration importer reads ERPNext data successfully:** acceptance test
   A7 passes (synthetic 3-account + 1-opening-JE ERPNext export imports
   cleanly via the PR 6 hooks).
6. **Idempotency verified:** acceptance test A5 passes (re-importing the
   same source produces `Skipped`; re-importing with version-bump produces
   `Updated`).
7. **Tests pass:** ~55 tests across the package.
8. **`apps/docs/blocks-financial-ledger/overview.md` published.**
9. **`active-workstreams.md`** row for W#60 P4 / Path II cluster
   implementation updated with `built` status + the 6 PR numbers.

When the PASS gate is met, the next hand-offs in the Phase 1 critical
path can proceed:

- `blocks-financial-tax-stage06-handoff.md` (TaxCode / TaxRate /
  TaxJurisdiction; depends on this hand-off's `GLAccount.TaxLineMappingId`
  shape).
- `blocks-financial-periods-stage06-handoff.md` (FiscalYear /
  FiscalPeriod; replaces the PR 4 `IPeriodResolver` stub).
- `blocks-financial-ar-stage06-handoff.md` (Invoice + InvoiceLine; depends
  on this hand-off's GLAccount + JournalEntry posting).
- `blocks-financial-ap-stage06-handoff.md` (Bill + BillLine).
- `blocks-financial-payments-stage06-handoff.md` (Payment +
  PaymentApplication).
- `blocks-reports-tax-stage06-handoff.md` (TaxFormLineMap; references
  the Schedule E line annotations seeded in PR 5).
- `tooling-anchor-import-stage06-handoff.md` (the migration importer
  orchestrator; consumes this hand-off's `IErpnextAccountImporter` +
  `IErpnextJournalEntryImporter`).

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-16):**

- `packages/blocks-financial-ledger/` (target of PR 1 rename) ✓
- `Sunfish.Blocks.FinancialLedger.Models.GLAccount` (extended in PR 2) ✓
- `Sunfish.Blocks.FinancialLedger.Models.GLAccountType` (kept as-is) ✓
- `Sunfish.Blocks.FinancialLedger.Models.JournalEntry` (extended in PR 3) ✓
- `Sunfish.Blocks.FinancialLedger.Models.JournalEntryLine` (extended in PR 3) ✓
- `Sunfish.Blocks.FinancialLedger.Models.DepreciationSchedule` (preserved
  unchanged through rename — depreciation work is a separate hand-off) ✓
- ADR 0088 §1 (Path II + 7-cluster decomposition) ✓
- `coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md` ✓
- `icm/02_architecture/blocks-financial-schema-design.md` (§3.1–§3.4,
  §6.1, §7, §10, §11) ✓
- `_shared/engineering/erpnext-to-anchor-migration-importer-spec.md`
  (drafted 2026-05-16 by XO; lands as a sibling doc to this hand-off) ✓

**Introduced by this hand-off** (ship across PRs 2-6):

- Package rename: `blocks-financial-ledger → blocks-financial-ledger`
- New types: `ChartOfAccountsId`, `ChartOfAccounts`, `AccountSubtype`,
  `NormalBalance`, `FiscalPeriodId`, `JournalEntryStatus`,
  `JournalEntrySource`, `PostError`, `PostResult`, `ChartTemplate`,
  `ChartTemplateAccount`, `ImportOutcome<T>`, `ImportAction`,
  `ErpnextAccountSource`, `ErpnextJournalEntrySource`,
  `ErpnextJournalEntryLineSource`
- New services: `IJournalPostingService` + `JournalPostingService`,
  `IChartSeedingService` + `InMemoryChartSeedingService`,
  `IErpnextAccountImporter` + `ErpnextAccountImporter`,
  `IErpnextJournalEntryImporter` + `ErpnextJournalEntryImporter`,
  `IAccountResolver` + `InMemoryAccountResolver`,
  `IPeriodResolver` + `InMemoryPeriodResolver` (stub)
- Seed data: `DefaultChartTemplates.RentalRealEstate` + `.SmallBusinessGeneral`
- Docs: `apps/docs/blocks-financial-ledger/overview.md`
- Attribution: `packages/blocks-financial-ledger/NOTICE.md`

**Self-audit reminder (per ADR 0028-A10):** COB structurally verifies each
cited symbol by reading the actual file before declaring AP-21 clean.
Do not rely on grep-only verification.

---

## Cohort discipline

This hand-off is the **first Stage 06 hand-off under ADR 0088 Path II**
and the **first Phase 1 cluster implementation unit**. The COB self-audit
pattern applied to W#34 / W#35 / W#36 / W#39 / W#40 substrate hand-offs
applies here verbatim:

- Two-overload constructor (audit-disabled / audit-enabled both-or-neither)
  pattern for any DI extension that interacts with audit (PR 6 may need
  this for `IErpnextAccountImporter` if audit-logging is wired in).
- `AddBlocksFinancialLedger()` naming for the DI extension.
- `apps/docs/{cluster}/overview.md` page convention.
- README.md at the package root referencing Stage 02 design + ADR 0088.
- `ConcurrentDictionary` dedup for any cache (none introduced in this
  hand-off; flagged for future).

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w60-p4-financial-ledger-{slug}.md` in
  `/Users/christopherwood/Projects/SunfishSoftware/coordination/inbox/`.
- Halt the workstream + add a note in `active-workstreams.md` row for W#60.
- `ScheduleWakeup 1800s`.

If COB completes PR 6 + the PASS gate is met:

- Update `active-workstreams.md` (via the source W*.md file, not the
  ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`).
- Drop `cob-status-2026-05-XXTHH-MMZ-w60-p4-financial-ledger-built.md`
  to inbox.
- Continue with the next hand-off in the Phase 1 critical path (likely
  `blocks-financial-periods` or `blocks-financial-tax` — whichever XO
  has dropped next).

---

## Cross-references

- Spec source: `icm/02_architecture/blocks-financial-schema-design.md`
  §3.1–§3.4, §6.1, §7, §10, §11.
- Migration-importer spec (sibling deliverable, 2026-05-16):
  `_shared/engineering/erpnext-to-anchor-migration-importer-spec.md`.
- ADR 0088: `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md`.
- Ratification ruling:
  `coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md`.
- Sibling Stage 02 design docs (Phase 1 cluster context):
  - `blocks-reports-schema-design.md` (consumes this hand-off's
    GLAccount via Schedule E line annotations seeded in PR 5)
  - `blocks-people-schema-design.md` (provides the Party/Customer/Vendor
    surface that financial-AR / -AP will consume in follow-on hand-offs)
- Cohort precedent hand-offs (substrate-only shape):
  - `foundation-mission-space-stage06-handoff.md` (W#40 — 5-PR shape,
    DI extension pattern)
  - `foundation-versioning-stage06-handoff.md` (W#34 — substrate naming)
  - `foundation-migration-stage06-handoff.md` (W#35 — substrate sequencing)

---

**End of hand-off.**
