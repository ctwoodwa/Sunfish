# Hand-off — `blocks-financial-tax` (TaxCode + TaxRate + TaxJurisdiction + TaxFormLineMap)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-16
**Status:** `ready-to-build` (gated on `blocks-financial-ledger` PR 2 merged — see Pre-build checklist)
**Workstream:** W#60 P4 — Path II native domain, Phase 1 critical path, cluster unit #3 (after `-ledger` and `-periods`)
**Spec source:** [`icm/02_architecture/blocks-financial-schema-design.md`](../../02_architecture/blocks-financial-schema-design.md) §3.12–§3.14, §3.17, §5.1, §6.4 + [`icm/02_architecture/blocks-reports-schema-design.md`](../../02_architecture/blocks-reports-schema-design.md) §3 (`TaxFormLineMap`), §8 (Schedule E mapping), Q3 (ratification recommendation)
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) (Proposed; CO ratified 2026-05-16) §1 (7-cluster decomposition), §3 (clean-room discipline), Appendix B Phase 1
**CRDT conventions:** [`_shared/engineering/crdt-friendly-schema-conventions.md`](../../../_shared/engineering/crdt-friendly-schema-conventions.md) §1 (ULIDs), §2 (tombstones), §3 (version vectors), §4 (append-only), §5 (stable string codes), §6 (posted-then-immutable)
**Event bus:** [`_shared/engineering/cross-cluster-event-bus-design.md`](../../../_shared/engineering/cross-cluster-event-bus-design.md) §2 (naming), §3.1 (`Financial.*` catalog — new entries added by this hand-off)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~8–10h sunfish-PM (4 new entities + tax calculation engine + Schedule E seed + ledger wiring + ~30–35 tests + docs)
**PR count:** 5 PRs
**Pre-merge council:** NOT required (substrate scope; mirrors the `-ledger` hand-off pattern). Standard COB self-audit applies. **Security-engineering spot-check recommended on PR 3** (the tax-calculation engine has a banker's-rounding correctness path that has historically been a source of fiscal bugs in adjacent systems).
**Audit before build:** `ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-financial-"` — confirms `blocks-financial-ledger/` exists (renamed from `blocks-accounting/` in the ledger hand-off PR 1) and `blocks-financial-tax/` does NOT yet exist.

---

## Context

### Path II reframe (recap)

Per ADR 0088 (CO ratified 2026-05-16), Anchor is the all-in-one local-first runtime; the financial domain is implemented natively in `blocks-financial-*` clusters. SQLite is primary; Loro CRDT handles peer-to-peer sync. The cluster decomposes into 7 Phase 1 + Phase 3 packages per ADR 0088 §1 Table 2; `blocks-financial-tax` is one of the Phase 1 packages.

### Why `-tax` is the third cluster up

1. **Topological dependency.** `blocks-financial-tax` consumes:
   - `GLAccount` (from `-ledger`) for `TaxRate.payableAccountId` (the Liability account that collected tax accrues to) and for `TaxFormLineMap.accountSelectors` (which accounts roll up to each tax-form line).
   - `ChartOfAccountsId` (from `-ledger`) for tenant-scoping every tax entity.
   - `FiscalPeriodId` (from `-periods`) is **NOT** required by this hand-off — tax rates use their own `effectiveDate` + `expiryDate`; period gating happens at the journal-posting layer, not in tax calc.
2. **Downstream consumers blocked without it.** `blocks-financial-ar` (Invoice/InvoiceLine) and `blocks-financial-ap` (Bill/BillLine) both carry `taxCodeId` per Stage 02 §3.6 and §3.8 — they cannot ship until `TaxCode` exists. `blocks-reports-tax` (Schedule E generator) consumes `TaxFormLineMap`.
3. **MVP scope.** Schedule E generation is one of the seven Phase 1 MVP deliverables per ADR 0088 Appendix B. The mapping table (this hand-off) is its source of truth.
4. **No code exists yet.** Unlike `-ledger` (which renamed an existing `blocks-accounting/` package), `-tax` is greenfield. No rename PR is required.

### Naming alignment (binding)

Per the 2026-05-16 naming-ratification ruling `coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md`:

- **Decision 2:** `blocks-tax-reporting` (the existing package) **renames to** `blocks-reports-tax` in a sibling hand-off. **NOT in scope for this hand-off.** This hand-off ships `blocks-financial-tax` — the *calculation* + *jurisdiction* + *form-line-mapping* primitives. The *report-generation* layer (`blocks-reports-tax`) consumes the form-line-mapping primitive from here in a follow-on hand-off.
- **Decision 1 corollary** (from the ledger hand-off): the canonical C# type for chart-of-accounts nodes is `GLAccount` (not `Account`). This hand-off uses `GLAccountId` for the same reason.

Stage 02 §3.17 names the entity `TaxLineMapping`; Stage 02 of **reports** §3 names it `TaxFormLineMap`. These describe the same concept. **This hand-off uses `TaxFormLineMap`** because (a) the more-recent reports-schema design is the controlling reference for the form-mapping primitive's shape, and (b) the name is more discoverable for the Schedule E + 1099 consumers. The Stage 02 financial-schema §3.17 entry is treated as an early sketch that the reports-schema entry supersedes for naming.

### ONR research dependency (provisional Schedule E mapping)

The ONR session is producing `Sunfish/icm/02_architecture/regulatory-us-rental-tax-input.md` — a US-rental-property tax research output that will **ratify the canonical Schedule E line-mapping table** (and Pub 527 expense-category-to-account-class mapping, and 1099 threshold rules). At the time of this hand-off authoring (2026-05-16) the ONR output is **pending and not yet in repo**.

**Mitigation built into this hand-off:**

- PR 4 ships a **v1 Schedule E mapping** based on:
  - `blocks-reports-schema-design.md` §8.1 (Schedule E structure table — already-clean-room-extracted from IRS Pub 527).
  - `blocks-reports-schema-design.md` §8.2 (example mapping rows).
  - The chart-of-accounts seeded in `blocks-financial-ledger` PR 5 (`DefaultChartTemplates.RentalRealEstate` — already Schedule-E-line-annotated).
  - IRS Pub 527 + Schedule E form instructions (public domain — direct reference per ADR 0088 §3).
- Every seeded `TaxFormLineMap` row carries an `IsProvisional` flag (defaults to `true`) and a `ProvisionalRationale: "Pending ONR ratification per regulatory-us-rental-tax-input.md"` note.
- When ONR's research output lands, a **follow-on PR** (sibling hand-off `blocks-financial-tax-onr-ratification-addendum`) flips the `IsProvisional` bit, fills in any missed lines, and adds a Pub-527-citation column to the seed.
- **No production code (Stage 06 Anchor build) blocks on ONR landing.** v1 is good enough for Wave/Rentler/Mac-ERPNext replacement readiness — the seed is auditable, source-cited, and tenant-editable per Q3 of the reports-schema design.

### Property-tax handling (clarification)

Per the task brief and Stage 02 §3.17 + reports-schema §8.1 Line 16 ("Taxes"):

- **Sales/VAT/GST tax** = applied to *line items* on a transaction (Invoice/Bill). Computed by `ITaxCalculationService` (PR 3) at line-render time. Lives in `TaxCode` + `TaxRate`.
- **Property tax** = a recurring obligation paid to a county/city assessor. **NOT a transaction-line tax.** It is recorded as a journal entry on a recurring schedule (debit `Property Tax Expense`, credit `Cash` — or credit `Accrued Property Tax` + a later cash payment).
- **Wiring:** Property-tax bills flow through `blocks-financial-ap` (Bill → AP-AR journal posting) like any other vendor bill, with `accountId = ExpenseAccount("Property Tax")`. The Schedule E Line-16 mapping (this hand-off PR 4) selects on `accountCodePrefix: "6100"` or `accountTag: "property-tax"`.
- **Out of scope here:** automated property-tax-bill ingestion (per-property tax schedule), property-tax-prepayment proration. Those are Phase 2 polish concerns; the recurring-bill primitive (`blocks-financial-ap`) is sufficient for Phase 1.
- **TaxJurisdiction overlap:** the same `TaxJurisdiction` records used for sales-tax computation MAY be referenced from property-tax-related entities (e.g., a Property's assessing jurisdiction), but this hand-off does NOT add that linkage — it ships the jurisdiction primitive only. The `blocks-property-*` cluster may extend `Property` with a `TaxJurisdictionId` FK in a later hand-off.

### Cluster-internal dependencies summary

```
blocks-financial-ledger        (PR 2 merged — provides GLAccountId, ChartOfAccountsId, LegalEntityId or stub)
        │
        ▼
blocks-financial-tax           (THIS HAND-OFF)
        │
        ├─► blocks-financial-ar  (consumes TaxCode for Invoice tax computation)
        ├─► blocks-financial-ap  (consumes TaxCode for Bill tax computation)
        └─► blocks-reports-tax   (consumes TaxFormLineMap for Schedule E + 1099)

blocks-financial-periods       (NOT a dependency of this hand-off — tax rates are date-effective without FiscalPeriod)
```

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify ledger hand-off state.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/
   grep -l "GLAccountId" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/Models/*.cs
   grep -l "ChartOfAccountsId" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/Models/*.cs
   ```
   Expected: directory exists; `GLAccountId.cs` exists (pre-ledger-rename); `ChartOfAccountsId.cs` exists (ledger PR 2). If either is missing, the ledger hand-off PR 2 has not yet merged — file `cob-question-2026-05-XXTHH-MMZ-w60-p4-tax-blocked-on-ledger-pr2.md` and halt.

2. **Verify `LegalEntityId` resolution path.**
   This hand-off needs `LegalEntityId` for `TaxJurisdiction` tenant scoping (jurisdictions are conceptually shared across charts within a legal entity, but isolated across legal entities). Apply the same resolution rule the ledger hand-off applied in its PR 2 (use `Sunfish.Foundation.Identity.LegalEntityId` if it exists; else `Sunfish.Blocks.FinancialLedger.LegalEntityId` placeholder with TODO comment).
   ```bash
   grep -r "LegalEntityId" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/foundation-* /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/ 2>/dev/null | head -10
   ```
   If multiple definitions surface, USE the foundation-identity one; do NOT introduce a third.

3. **Confirm package name availability.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-financial-tax$|^blocks-tax-"
   ```
   Expected: empty. (`blocks-tax-reporting/` may exist — that's a *separate* package handled by the Decision-2 rename hand-off; **do not modify it in this hand-off**.)

4. **Confirm ADR 0088 status.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```
   Expected: `status: Proposed` (CO ratified design 2026-05-16; status flip is a separate housekeeping PR). Hand-off is `ready-to-build` regardless, per the same logic as the ledger hand-off.

5. **Confirm no parallel-session PRs touch the target area.**
   ```bash
   gh pr list --state open --search "blocks-financial-tax in:title,body"
   gh pr list --state open --search "TaxCode OR TaxRate OR TaxJurisdiction OR TaxFormLineMap in:title,body"
   ```
   Expected: empty. If anything is open, file `cob-question-*` before starting PR 1.

6. **Confirm ONR research output is NOT in repo.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/icm/02_architecture/regulatory-us-rental-tax-input.md 2>&1
   ```
   Expected: `No such file or directory`. **If the file exists**, the v1-provisional mapping in PR 4 still applies (don't try to rewrite the seed in-line); flag in the PR description that the ratification addendum is a follow-on hand-off, not part of this scope. If you can re-cite specific Schedule E lines from the ONR output safely in the seed comments without expanding scope, do so; otherwise leave the v1 seed unchanged.

7. **Confirm `but status` (or `git status`) is clean** and current branch is `main` (or a fresh worktree from `main`). Use the worktree-from-main pattern (`feedback_worktree_base_main_not_gitbutler`).

8. **Read the canonical references.** Before opening PR 1:
   - `icm/02_architecture/blocks-financial-schema-design.md` §3.12 (TaxCode), §3.13 (TaxRate), §3.14 (TaxJurisdiction), §6.4 (calculation algorithm).
   - `icm/02_architecture/blocks-reports-schema-design.md` §3 (`TaxFormLineMap`), §8 (Schedule E mapping).
   - `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §2, §4, §5, §6 (skim — the conventions section below cites the specific clauses applied).
   - The ledger hand-off (`blocks-financial-ledger-chart-and-journal-stage06-handoff.md`) for the pattern this hand-off mirrors — record/init-only field shape, two-overload DI extension, NOTICE.md attribution, `apps/docs/{cluster}/overview.md` docs.

---

## Per-PR deliverables

This hand-off splits into **5 PRs** by responsibility. PR 1 → PR 2 → PR 3 → PR 4 are sequential (each builds on the previous types). PR 5 sequences last and wires `blocks-financial-tax` into `blocks-financial-ledger`'s posting path + the ERPNext importer.

---

### PR 1 — Package scaffold + `TaxJurisdiction` + `TaxCode` entities

**Estimated effort:** ~2h
**Scope:** new package `packages/blocks-financial-tax/`; ID types; `TaxJurisdiction` entity with hierarchy (federal → state → county → city); `TaxCode` entity (without rates collection — that's PR 2); jurisdiction-tree resolver stub; package DI extension shell
**Commit subject:** `feat(blocks-financial-tax): scaffold package + TaxJurisdiction + TaxCode entities per Stage 02 §3.12–§3.14`
**Branch:** `cob/blocks-financial-tax-scaffold`
**Depends on:** ledger hand-off PR 2 merged (`ChartOfAccountsId` available)

#### File operations

```bash
mkdir -p packages/blocks-financial-tax/Models
mkdir -p packages/blocks-financial-tax/Services
mkdir -p packages/blocks-financial-tax/DependencyInjection
mkdir -p packages/blocks-financial-tax/Localization
mkdir -p packages/blocks-financial-tax/tests
```

#### Project files

**`packages/blocks-financial-tax/Sunfish.Blocks.FinancialTax.csproj`** — mirror the shape of `Sunfish.Blocks.FinancialLedger.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <RootNamespace>Sunfish.Blocks.FinancialTax</RootNamespace>
    <AssemblyName>Sunfish.Blocks.FinancialTax</AssemblyName>
    <NOTICEFile>NOTICE.md</NOTICEFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\blocks-financial-ledger\Sunfish.Blocks.FinancialLedger.csproj" />
    <!-- Foundation-identity if it exists; otherwise rely on ledger's LegalEntityId placeholder -->
  </ItemGroup>
</Project>
```

**`packages/blocks-financial-tax/tests/Sunfish.Blocks.FinancialTax.Tests.csproj`** — standard xUnit test project; reference the package + `xunit` + `FluentAssertions` (project convention).

**`packages/blocks-financial-tax/NOTICE.md`** (new file in PR 1):

```markdown
# NOTICE — Sunfish.Blocks.FinancialTax

This package's entity shapes (TaxCode → TaxRate → TaxJurisdiction
three-level decomposition; TaxFormLineMap account-selector pattern;
tax-rate effective-dating semantics) derive from Apache OFBiz's
`accounting/TaxAuthority`, `accounting/TaxAuthorityRateProduct`, and
`accounting/TaxAuthorityGlAccount` entity models
(<https://ofbiz.apache.org/>, Apache 2.0 license).

OFBiz version studied: v18.12.x (as of 2026-05-16).

The Schedule E tax-form line definitions are derived from IRS
Publication 527 (Residential Rental Property) and Schedule E
(Form 1040) instructions — public-domain US federal works.

The Sunfish implementation is original code, distributed under the
MIT License. The OFBiz entity-shape pattern is reproduced with
attribution per Apache 2.0 §4(c) of the OFBiz License.
```

**`packages/blocks-financial-tax/README.md`** (new file in PR 1) — package-root README referencing Stage 02 §3.12–§3.14 + §6.4 + ADR 0088 §1.

#### New ID types

**`Models/TaxCodeId.cs`** — ULID strongly-typed id (mirrors `GLAccountId` pattern in `blocks-financial-ledger`):

```csharp
namespace Sunfish.Blocks.FinancialTax.Models;

public readonly record struct TaxCodeId(string Value)
{
    public static TaxCodeId New() => new(Ulid.NewUlid().ToString());
    public override string ToString() => Value;
}
```

**`Models/TaxRateId.cs`** — same shape.

**`Models/TaxJurisdictionId.cs`** — same shape.

**`Models/TaxFormLineMapId.cs`** — same shape (placeholder; the entity ships in PR 4).

#### New enums (stable string codes per CRDT conventions §5)

Per CRDT-conventions §5, enums in this package are **stable string codes**, not integers, even though the C# surface uses `enum`. The convention is enforced by:

1. The enum names match canonical strings the storage layer will write (we use `enum.ToString()` for SQLite serialization in the persistence layer; never an int cast).
2. Members are append-only — never renumbered, never removed (per CRDT conventions §5 "Deprecating codes — never reuse").

**`Models/TaxKind.cs`** per Stage 02 §3.12:

```csharp
public enum TaxKind
{
    Sales,             // US-style sales tax
    VAT,               // Value-Added Tax (Europe etc.)
    GST,               // Goods & Services Tax (Canada, AU)
    WithholdingTax,    // tax withheld at source
    Use,               // self-assessed use tax
    Exempt,            // explicit zero — preserves audit trail vs. just omitting
    Other,
}
```

**`Models/TaxApplication.cs`** per Stage 02 §3.12:

```csharp
public enum TaxApplication
{
    OnSubtotal,    // tax = subtotal * rate
    Compound,      // tax = (subtotal + prior_tax) * rate
    Inclusive,     // line price already includes tax; back out
}
```

**`Models/JurisdictionLevel.cs`** per Stage 02 §3.14:

```csharp
public enum JurisdictionLevel
{
    Country,      // ordinal 0 — most senior
    Federal,      // ordinal 1
    State,        // ordinal 2
    County,       // ordinal 3
    City,         // ordinal 4
    District,     // ordinal 5
    Special,      // ordinal 6 — most local
}
```

Note ordering matters: `Compound` tax application in PR 3 walks jurisdictions outermost-first per Stage 02 §6.4 ("Order rates by jurisdiction level (federal → state → county → city → district)"). A `JurisdictionLevel.OrderIndex()` extension or `[JurisdictionLevelOrdinal]` attribute keeps this stable.

**`Models/JurisdictionLevelExtensions.cs`**:

```csharp
public static class JurisdictionLevelExtensions
{
    /// <summary>
    /// Ordering for compound-tax application — outermost jurisdiction first.
    /// Country/Federal apply before State; State before County; etc.
    /// Stable across enum-member additions per CRDT-conventions §5.
    /// </summary>
    public static int OrderIndex(this JurisdictionLevel level) => level switch
    {
        JurisdictionLevel.Country  => 0,
        JurisdictionLevel.Federal  => 1,
        JurisdictionLevel.State    => 2,
        JurisdictionLevel.County   => 3,
        JurisdictionLevel.City     => 4,
        JurisdictionLevel.District => 5,
        JurisdictionLevel.Special  => 6,
        _                          => 99,
    };
}
```

#### New entities

**`Models/TaxJurisdiction.cs`** per Stage 02 §3.14:

```csharp
public sealed record TaxJurisdiction(
    TaxJurisdictionId Id,
    JurisdictionLevel Level,
    string IsoCountry,                                  // ISO 3166-1: "US", "CA", "DE"
    string? Region,                                     // ISO 3166-2: "US-VA", "DE-BY"
    string? Locality,                                   // "Frederick County", "Winchester"
    string Name,                                        // display label
    TaxJurisdictionId? ParentJurisdictionId,
    string? Notes,
    bool IsActive = true,
    Instant? CreatedAtUtc = null,
    Instant? UpdatedAtUtc = null,
    Instant? DeletedAtUtc = null)                       // tombstone per CRDT §2
{
    public static TaxJurisdiction Create(
        JurisdictionLevel level,
        string isoCountry,
        string name,
        TaxJurisdictionId? parentJurisdictionId = null,
        string? region = null,
        string? locality = null,
        string? notes = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        return new TaxJurisdiction(
            Id: TaxJurisdictionId.New(),
            Level: level,
            IsoCountry: isoCountry,
            Region: region,
            Locality: locality,
            Name: name,
            ParentJurisdictionId: parentJurisdictionId,
            Notes: notes,
            IsActive: true,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }
}
```

**`Models/TaxCode.cs`** per Stage 02 §3.12 — note the `Rates` collection is **NOT embedded** on the entity (per CRDT-conventions §4 "Append-only sub-collections — never as an embedded array"). The `TaxRate` rows live as a separate table linked by `TaxRate.TaxCodeId` FK; the `Rates` accessor is a **derived view** computed by `ITaxRateLookup` (PR 2). PR 1 ships the entity without the `Rates` accessor; PR 2 adds it as an extension method or query.

```csharp
public sealed record TaxCode(
    TaxCodeId Id,
    ChartOfAccountsId ChartId,
    string Code,                            // "US-VA-SALES", "EU-DE-VAT19", "EXEMPT", "FREDERICK-COUNTY-LOCAL"
    string Name,
    TaxKind Kind,
    TaxApplication Application,
    bool IsActive = true,
    string? Notes = null,
    int Version = 1,                        // CRDT §3 — bumped on each successful upsert
    Instant? CreatedAtUtc = null,
    Instant? UpdatedAtUtc = null,
    Instant? DeletedAtUtc = null)           // tombstone per CRDT §2
{
    public static TaxCode Create(
        ChartOfAccountsId chartId,
        string code,
        string name,
        TaxKind kind,
        TaxApplication application,
        string? notes = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        return new TaxCode(
            Id: TaxCodeId.New(),
            ChartId: chartId,
            Code: code,
            Name: name,
            Kind: kind,
            Application: application,
            IsActive: true,
            Notes: notes,
            Version: 1,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }
}
```

#### Stub services (PR 1 — minimal interfaces; implementations land in PR 2+)

**`Services/ITaxJurisdictionResolver.cs`** — public contract for walking the jurisdiction tree from an address (consumed by PR 3 calculation engine):

```csharp
public interface ITaxJurisdictionResolver
{
    /// <summary>
    /// Resolves the set of applicable jurisdictions for a given address /
    /// property location. Returns from-most-local to most-senior order
    /// (City → County → State → Federal → Country); callers use
    /// JurisdictionLevelExtensions.OrderIndex to re-sort if a different
    /// order is needed (e.g., Compound tax wants outermost-first).
    /// </summary>
    Task<IReadOnlyList<TaxJurisdiction>> ResolveAsync(
        TaxLocationContext context,
        CancellationToken cancellationToken = default);
}

public sealed record TaxLocationContext(
    string IsoCountry,
    string? Region,
    string? Locality,
    string? PostalCode,                     // not used in PR 1; reserved for ZIP-lookup
    string? PropertyId);                     // when set, takes precedence over address fields
```

**`Services/InMemoryTaxJurisdictionResolver.cs`** — minimal in-memory implementation that queries an injected `ITaxJurisdictionStore` (also stubbed in this PR — see below). PR 1 only needs the resolver to compile + roundtrip a hard-coded jurisdiction set in tests.

**`Services/ITaxJurisdictionStore.cs`** — CRUD interface, in-memory for now:

```csharp
public interface ITaxJurisdictionStore
{
    Task<TaxJurisdiction?> GetAsync(TaxJurisdictionId id, CancellationToken ct = default);
    Task<IReadOnlyList<TaxJurisdiction>> GetByLevelAsync(JurisdictionLevel level, CancellationToken ct = default);
    Task<IReadOnlyList<TaxJurisdiction>> GetChildrenAsync(TaxJurisdictionId parentId, CancellationToken ct = default);
    Task UpsertAsync(TaxJurisdiction jurisdiction, CancellationToken ct = default);
}
```

**`Services/InMemoryTaxJurisdictionStore.cs`** — backed by `ConcurrentDictionary<TaxJurisdictionId, TaxJurisdiction>`; used in tests. SQLite-backed implementation lands in a later persistence-layer hand-off.

#### DI registration

**`DependencyInjection/ServiceCollectionExtensions.cs`** — apply the canonical two-overload pattern per the ledger hand-off cohort discipline (audit-disabled / audit-enabled both-or-neither):

```csharp
public static class FinancialTaxServiceCollectionExtensions
{
    public static IServiceCollection AddBlocksFinancialTax(this IServiceCollection services)
    {
        services.AddSingleton<ITaxJurisdictionStore, InMemoryTaxJurisdictionStore>();
        services.AddSingleton<ITaxJurisdictionResolver, InMemoryTaxJurisdictionResolver>();
        // PR 2 adds ITaxRateLookup; PR 3 adds ITaxCalculationService;
        // PR 4 adds ITaxFormLineMapStore; PR 5 adds the ledger-wiring service.
        return services;
    }
}
```

#### Tests (PR 1)

`packages/blocks-financial-tax/tests/TaxJurisdictionTests.cs`:

- `Create_PopulatesAllFields`.
- `Create_DefaultsIsActiveTrue`.
- `Create_SetsCreatedAndUpdatedToSameInstant`.
- `Create_Federal_WithoutParent_Succeeds`.
- `Create_City_WithParent_RecordsParentId`.

`packages/blocks-financial-tax/tests/TaxCodeTests.cs`:

- `Create_PopulatesAllFields`.
- `Create_VersionStartsAt1`.
- `Create_KindExempt_IsAllowed`.
- `Create_DefaultsIsActiveTrue`.

`packages/blocks-financial-tax/tests/JurisdictionLevelExtensionsTests.cs`:

- `OrderIndex_Federal_BeforeState`.
- `OrderIndex_State_BeforeCounty`.
- `OrderIndex_County_BeforeCity`.
- `OrderIndex_City_BeforeDistrict`.
- `OrderIndex_UnknownEnumValue_Returns99` (forward-compat for future enum members).

`packages/blocks-financial-tax/tests/InMemoryTaxJurisdictionResolverTests.cs`:

- `Resolve_USVAFrederick_ReturnsCityCountyStateFederal` (seed the store with the four; resolve a property at Frederick; assert order is City → County → State → Federal).
- `Resolve_USVAWithoutCity_OmitsCity`.
- `Resolve_NonUSCountry_ReturnsCountryOnly_WhenNoSubdivisionsSeeded`.

Total new tests this PR: ~12–14.

#### Verification

- `dotnet build` succeeds — the new package compiles and existing solution remains green.
- `dotnet test packages/blocks-financial-tax/tests/` passes ~12–14 new tests.
- `dotnet sln add` updates the solution file with the new project + test project.
- `grep -r "Sunfish.Blocks.FinancialTax" packages/ apps/ accelerators/ --include="*.csproj"` returns the new project file's existence; **no external consumers** reference the new package yet (they will, starting with `-ar` / `-ap` / `-reports-tax` follow-on hand-offs).

#### PR description template

```
Scaffold blocks-financial-tax package per Stage 02 §3.12–§3.14 +
ADR 0088 §1. Ships TaxJurisdiction + TaxCode primitives + jurisdiction
resolver stubs.

This is PR 1 of 5 under the blocks-financial-tax-stage06-handoff. No
external consumers wire to the new package in this PR — that comes in
PR 5 (ledger wiring) and follow-on hand-offs (-ar, -ap, -reports-tax).

CRDT conventions applied:
- ULID IDs (CRDT §1)
- Tombstone DeletedAtUtc field (CRDT §2)
- Version vector on TaxCode (CRDT §3)
- Append-only TaxRate (CRDT §4) — NOT embedded on TaxCode; comes in PR 2
- Stable string-code enums (CRDT §5)

License posture:
- Apache OFBiz entity-shape attribution in NOTICE.md (Apache 2.0).
- Sunfish output MIT per ADR 0088 §2.

Refs: ADR 0088 §1; Stage 02 §3.12–§3.14;
      _shared/engineering/crdt-friendly-schema-conventions.md §1, §2, §5.
```

#### Do NOT in this PR

- Do NOT ship `TaxRate` (that's PR 2).
- Do NOT ship the calculation algorithm (that's PR 3).
- Do NOT ship `TaxFormLineMap` (that's PR 4).
- Do NOT wire into `blocks-financial-ledger`'s posting path (that's PR 5).
- Do NOT seed any production jurisdictions or codes — only test-fixture data.

---

### PR 2 — `TaxRate` effective-dated entity + `ITaxRateLookup` service

**Estimated effort:** ~2h
**Scope:** `TaxRate` entity per Stage 02 §3.13; effective-date / expiry-date semantics; non-overlapping-range validation; `ITaxRateLookup` query service; `TaxCode.Rates` derived accessor
**Commit subject:** `feat(blocks-financial-tax): add TaxRate effective-dated history + ITaxRateLookup service per Stage 02 §3.13`
**Depends on:** PR 1 merged
**Branch:** `cob/blocks-financial-tax-rate-history`

#### New entity

**`Models/TaxRate.cs`** per Stage 02 §3.13 — note `TaxRate` is **append-only** per CRDT-conventions §4: rate changes are NEW rows with a new `Id` + new `EffectiveDate` + the old row's `ExpiryDate` populated. Editing in place is forbidden.

```csharp
public sealed record TaxRate(
    TaxRateId Id,
    TaxCodeId TaxCodeId,
    TaxJurisdictionId JurisdictionId,
    decimal RatePercent,                     // 0..100; 5-decimal precision (e.g. 8.25000)
    LocalDate EffectiveDate,
    LocalDate? ExpiryDate,                   // null = open-ended (active rate)
    GLAccountId PayableAccountId,            // Liability/TaxesPayable account this rate accrues to
    string? Description,
    Instant? CreatedAtUtc = null,
    Instant? DeletedAtUtc = null)            // tombstone for an erroneously-added rate (rare; audit-trail-preserving)
{
    public static TaxRate Create(
        TaxCodeId taxCodeId,
        TaxJurisdictionId jurisdictionId,
        decimal ratePercent,
        LocalDate effectiveDate,
        GLAccountId payableAccountId,
        LocalDate? expiryDate = null,
        string? description = null,
        Instant? createdAtUtc = null)
    {
        if (ratePercent < 0m || ratePercent > 100m)
            throw new ArgumentOutOfRangeException(
                nameof(ratePercent),
                ratePercent,
                "TaxRate.ratePercent must be in [0, 100].");
        if (expiryDate is not null && expiryDate < effectiveDate)
            throw new ArgumentException(
                $"TaxRate expiryDate ({expiryDate}) must be on or after effectiveDate ({effectiveDate}).",
                nameof(expiryDate));

        return new TaxRate(
            Id: TaxRateId.New(),
            TaxCodeId: taxCodeId,
            JurisdictionId: jurisdictionId,
            RatePercent: ratePercent,
            EffectiveDate: effectiveDate,
            ExpiryDate: expiryDate,
            PayableAccountId: payableAccountId,
            Description: description,
            CreatedAtUtc: createdAtUtc ?? Instant.Now);
    }

    /// <summary>True iff this rate applies on the given date.</summary>
    public bool IsActiveOn(LocalDate date) =>
        DeletedAtUtc is null
        && EffectiveDate <= date
        && (ExpiryDate is null || ExpiryDate >= date);
}
```

#### Validation rules (per Stage 02 §3.13)

1. `RatePercent` ∈ [0, 100]. Enforced in `Create` (above).
2. For a given (`TaxCodeId`, `JurisdictionId`), effective-date ranges MUST NOT overlap. **Enforced by `ITaxRateLookup.UpsertAsync` (see below)** — a service-layer invariant, not a constructor invariant, because validating non-overlap requires querying sibling rows.
3. `PayableAccountId` MUST be Liability-type, TaxesPayable-subtype. **Enforced by `ITaxRateLookup.UpsertAsync`** via an injected `IAccountResolver` (already in `blocks-financial-ledger`). Validation failure returns a structured `TaxRateValidationError`, not an exception.
4. Historical rates are retained — rate changes are new rows with new effective-dates. The application path: expire the current rate (set `ExpiryDate = newEffectiveDate.Minus(Period.FromDays(1))`), then insert the new rate. **`ITaxRateLookup.SupersedeAsync` (below) is the canonical operation** — it does both writes atomically.

#### New service

**`Services/ITaxRateLookup.cs`**:

```csharp
public interface ITaxRateLookup
{
    /// <summary>
    /// Returns the active rate(s) for a TaxCode on a given date,
    /// filtered to the given jurisdiction set (typically from
    /// ITaxJurisdictionResolver.ResolveAsync).
    /// </summary>
    Task<IReadOnlyList<TaxRate>> GetActiveRatesAsync(
        TaxCodeId taxCodeId,
        LocalDate date,
        IReadOnlyCollection<TaxJurisdictionId> jurisdictionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full effective-dated history for a (TaxCode,
    /// Jurisdiction) pair, ordered oldest-first.
    /// </summary>
    Task<IReadOnlyList<TaxRate>> GetHistoryAsync(
        TaxCodeId taxCodeId,
        TaxJurisdictionId jurisdictionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new TaxRate row; validates non-overlapping date range
    /// against existing rates for (TaxCode, Jurisdiction); validates
    /// payable account is TaxesPayable-subtype.
    /// </summary>
    Task<TaxRateUpsertResult> UpsertAsync(
        TaxRate candidate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supersedes the currently-active rate for (TaxCode, Jurisdiction)
    /// with a new rate effective on `newEffectiveDate`. Atomically:
    /// 1. Expires the current rate (sets ExpiryDate = newEffectiveDate - 1 day).
    /// 2. Inserts the new rate.
    /// Emits Financial.TaxRateAdded + Financial.TaxRateExpired events.
    /// </summary>
    Task<TaxRateSupersedeResult> SupersedeAsync(
        TaxCodeId taxCodeId,
        TaxJurisdictionId jurisdictionId,
        decimal newRatePercent,
        LocalDate newEffectiveDate,
        GLAccountId payableAccountId,
        CancellationToken cancellationToken = default);
}

public enum TaxRateValidationError
{
    None,
    DateRangeOverlap,
    PayableAccountNotFound,
    PayableAccountWrongType,
    PayableAccountWrongSubtype,
    NoActiveRateToSupersede,
}

public readonly record struct TaxRateUpsertResult(
    TaxRate? Rate,
    TaxRateValidationError Error,
    string? Detail);

public readonly record struct TaxRateSupersedeResult(
    TaxRate? OldRate,
    TaxRate? NewRate,
    TaxRateValidationError Error,
    string? Detail);
```

**`Services/InMemoryTaxRateLookup.cs`** — implementation backed by `ConcurrentDictionary<TaxRateId, TaxRate>` for PR 2. Validation:

1. **Date-range overlap check** (Stage 02 §3.13 rule 2): on `UpsertAsync`, query existing rates for the same `(TaxCodeId, JurisdictionId)`; reject if `candidate.EffectiveDate` falls inside any non-expired range. Return `DateRangeOverlap`.
2. **Payable-account validation** (rule 3): resolve `candidate.PayableAccountId` via `IAccountResolver` (from `blocks-financial-ledger`); reject if the account is not Type=Liability or Subtype != TaxesPayable. Return the specific error code.
3. **Supersede atomicity:** the in-memory implementation performs the expire-then-insert under a single `lock` per `(TaxCodeId, JurisdictionId)` key. The SQLite implementation (future hand-off) will use a transaction.

#### `TaxCode.Rates` accessor (extension method)

To preserve the Stage 02 §3.12 surface `TaxCode.rates: ReadonlyArray<TaxRate>` without embedding the array (CRDT §4 violation), add a query extension:

**`Models/TaxCodeRatesExtensions.cs`**:

```csharp
public static class TaxCodeRatesExtensions
{
    /// <summary>
    /// Resolves the full TaxRate history for this TaxCode (across all
    /// jurisdictions). Equivalent to the Stage 02 §3.12 `rates` accessor
    /// but query-based rather than embedded — preserves CRDT §4
    /// append-only-subcollection discipline.
    /// </summary>
    public static async Task<IReadOnlyList<TaxRate>> GetRatesAsync(
        this TaxCode code,
        ITaxRateLookup lookup,
        CancellationToken ct = default)
    {
        // Query implementation pulls all rates with TaxCodeId == code.Id;
        // a JurisdictionId-scoped variant lives on ITaxRateLookup itself.
        return await lookup.GetAllForTaxCodeAsync(code.Id, ct);
    }
}
```

Add the corresponding query method to `ITaxRateLookup` (`GetAllForTaxCodeAsync`) — single-arg variant that returns ALL rates (active + expired) for a TaxCode across all jurisdictions, ordered by (JurisdictionId, EffectiveDate).

#### DI registration

Extend `AddBlocksFinancialTax`:

```csharp
services.AddSingleton<ITaxRateLookup, InMemoryTaxRateLookup>();
```

#### Tests (PR 2)

`tests/TaxRateTests.cs`:

- `Create_RatePercentInRange_Succeeds`.
- `Create_RatePercentBelowZero_Throws`.
- `Create_RatePercentAbove100_Throws`.
- `Create_ExpiryBeforeEffective_Throws`.
- `Create_ExpiryEqualEffective_Succeeds` (single-day rate).
- `Create_ExpiryNull_SignifiesOpenEnded`.
- `IsActiveOn_DateBeforeEffective_ReturnsFalse`.
- `IsActiveOn_DateAfterExpiry_ReturnsFalse`.
- `IsActiveOn_DateInRange_ReturnsTrue`.
- `IsActiveOn_AfterTombstoned_ReturnsFalse`.

`tests/InMemoryTaxRateLookupTests.cs`:

- `Upsert_NewRate_NoConflict_Inserts`.
- `Upsert_OverlappingRange_ReturnsDateRangeOverlap`.
- `Upsert_PayableAccountIsAsset_ReturnsPayableAccountWrongType`.
- `Upsert_PayableAccountIsLiabilityButNotTaxesPayable_ReturnsPayableAccountWrongSubtype` (e.g. account subtype = AccountsPayable, not TaxesPayable).
- `Upsert_PayableAccountNotFound_ReturnsPayableAccountNotFound`.
- `GetActiveRates_ReturnsOnlyRatesActiveOnDate`.
- `GetActiveRates_FiltersByJurisdictionSet`.
- `GetHistory_ReturnsOldestFirst`.
- `Supersede_HappyPath_ExpiresOldRateAndInsertsNew_ReturnsBothInResult`.
- `Supersede_NoActiveRate_ReturnsNoActiveRateToSupersede`.
- `Supersede_AtomicityOnFailure_NeitherChangeIsPersisted` (induce a failure on the insert step; verify the old rate is still open-ended).

`tests/TaxCodeRatesExtensionsTests.cs`:

- `GetRatesAsync_ReturnsRatesAcrossAllJurisdictions_OrderedByJurisdictionThenEffectiveDate`.

Total new tests this PR: ~14–16.

#### Verification

- `dotnet build` succeeds.
- All PR 1 tests pass unchanged.
- New PR 2 tests pass.
- `IAccountResolver` integration verified — PR 2 references the `blocks-financial-ledger` resolver type; if not on `main` (the ledger PR 4 ships the in-memory resolver), use the ledger hand-off's recommendation (in-memory stub) to keep PR 2 unblocked.

#### Do NOT in this PR

- Do NOT add the calculation algorithm (PR 3).
- Do NOT add `TaxFormLineMap` (PR 4).
- Do NOT seed any production rates; tests use fixture-only data.
- Do NOT emit `Financial.TaxRate*` events yet — event emission lands in PR 5 (which wires `IDomainEventEmitter` from the ledger DI surface). Comments in PR 2 referencing event emission point at PR 5.

---

### PR 3 — `ITaxCalculationService` (OnSubtotal / Compound / Inclusive algorithm)

**Estimated effort:** ~2h
**Scope:** implement the per-line tax-calculation algorithm per Stage 02 §6.4; three application modes (OnSubtotal / Compound / Inclusive); banker's-rounding at minor-unit boundary; structured `TaxCalculationResult` with per-rate breakdown
**Commit subject:** `feat(blocks-financial-tax): ITaxCalculationService — OnSubtotal/Compound/Inclusive per Stage 02 §6.4`
**Depends on:** PR 2 merged
**Branch:** `cob/blocks-financial-tax-calculation-engine`
**Note:** This is the **fiscal-correctness-critical** PR of the hand-off. The rounding semantics matter; the per-rate breakdown matters; the compound-tax ordering matters. **Recommend a security-engineering subagent spot-review at PR-open time** before auto-merge enables. Acceptance test A4 (below) is the canonical regression battery.

#### New types

**`Services/ITaxCalculationService.cs`**:

```csharp
public interface ITaxCalculationService
{
    /// <summary>
    /// Computes tax for a single line (Invoice line, Bill line, or
    /// arbitrary subtotal+code pairing).
    /// </summary>
    Task<TaxCalculationResult> CalculateAsync(
        TaxCalculationInput input,
        CancellationToken cancellationToken = default);
}

public sealed record TaxCalculationInput(
    TaxCodeId TaxCodeId,
    decimal Subtotal,                       // line amount in major units (e.g. 100.00m USD)
    LocalDate TransactionDate,              // for rate-effectivity lookup
    TaxLocationContext Location);            // for jurisdiction resolution

public sealed record TaxCalculationResult(
    decimal SubtotalIn,                      // echo of input.Subtotal
    decimal TaxAmount,                       // total tax, rounded
    decimal TotalIn,                         // subtotal + tax (or just subtotal for Inclusive)
    IReadOnlyList<TaxRateBreakdownLine> Breakdown,
    TaxCalculationError Error,
    string? Detail);

public sealed record TaxRateBreakdownLine(
    TaxRateId TaxRateId,
    TaxJurisdictionId JurisdictionId,
    JurisdictionLevel JurisdictionLevel,
    decimal RatePercent,
    decimal TaxableBase,                     // for OnSubtotal this is Subtotal; for Compound it grows
    decimal TaxAmount,                       // this row's contribution to the total
    GLAccountId PayableAccountId);           // for downstream GL-posting splits

public enum TaxCalculationError
{
    None,
    TaxCodeNotFound,
    NoApplicableRates,                       // no rates active on date for the resolved jurisdictions
    InclusiveWithZeroSubtotal,               // /0 guard
    UnknownApplication,                       // forward-compat for new TaxApplication enum values
}
```

#### Algorithm implementation per Stage 02 §6.4

**`Services/TaxCalculationService.cs`**:

```csharp
public sealed class TaxCalculationService : ITaxCalculationService
{
    private readonly ITaxRateLookup _rates;
    private readonly ITaxJurisdictionResolver _jurisdictions;
    private readonly ITaxCodeStore _codes;                // new in PR 3 — see below

    public TaxCalculationService(
        ITaxRateLookup rates,
        ITaxJurisdictionResolver jurisdictions,
        ITaxCodeStore codes)
    {
        _rates = rates;
        _jurisdictions = jurisdictions;
        _codes = codes;
    }

    public async Task<TaxCalculationResult> CalculateAsync(
        TaxCalculationInput input,
        CancellationToken ct = default)
    {
        // Resolve the TaxCode.
        var code = await _codes.GetAsync(input.TaxCodeId, ct);
        if (code is null)
            return Fail(input, TaxCalculationError.TaxCodeNotFound, null);

        // Exempt code → zero result with empty breakdown.
        if (code.Kind == TaxKind.Exempt)
            return new TaxCalculationResult(
                SubtotalIn: input.Subtotal,
                TaxAmount: 0m,
                TotalIn: input.Subtotal,
                Breakdown: Array.Empty<TaxRateBreakdownLine>(),
                Error: TaxCalculationError.None,
                Detail: null);

        // Resolve jurisdictions for the location.
        var jurisdictions = await _jurisdictions.ResolveAsync(input.Location, ct);
        var jurisdictionIds = jurisdictions.Select(j => j.Id).ToList();

        // Resolve applicable rates.
        var applicableRates = await _rates.GetActiveRatesAsync(
            code.Id, input.TransactionDate, jurisdictionIds, ct);
        if (applicableRates.Count == 0)
            return Fail(input, TaxCalculationError.NoApplicableRates,
                $"No active rates for code {code.Code} on {input.TransactionDate} in jurisdictions [{string.Join(",", jurisdictions.Select(j => j.Name))}]");

        // Pair each rate with its jurisdiction for breakdown metadata.
        var jurisLookup = jurisdictions.ToDictionary(j => j.Id);
        IEnumerable<(TaxRate Rate, TaxJurisdiction Jur)> withJur = applicableRates
            .Select(r => (r, jurisLookup[r.JurisdictionId]));

        switch (code.Application)
        {
            case TaxApplication.OnSubtotal:
                return ApplyOnSubtotal(input, withJur);

            case TaxApplication.Compound:
                return ApplyCompound(input, withJur);

            case TaxApplication.Inclusive:
                if (input.Subtotal == 0m)
                    return Fail(input, TaxCalculationError.InclusiveWithZeroSubtotal, null);
                return ApplyInclusive(input, withJur);

            default:
                return Fail(input, TaxCalculationError.UnknownApplication,
                    $"TaxApplication.{code.Application} not implemented");
        }
    }

    private static TaxCalculationResult ApplyOnSubtotal(
        TaxCalculationInput input,
        IEnumerable<(TaxRate Rate, TaxJurisdiction Jur)> rates)
    {
        var breakdown = new List<TaxRateBreakdownLine>();
        decimal totalTax = 0m;
        foreach (var (r, j) in rates)
        {
            var taxAmt = RoundMinor(input.Subtotal * (r.RatePercent / 100m));
            breakdown.Add(new TaxRateBreakdownLine(
                TaxRateId: r.Id,
                JurisdictionId: j.Id,
                JurisdictionLevel: j.Level,
                RatePercent: r.RatePercent,
                TaxableBase: input.Subtotal,
                TaxAmount: taxAmt,
                PayableAccountId: r.PayableAccountId));
            totalTax += taxAmt;
        }
        return new TaxCalculationResult(
            SubtotalIn: input.Subtotal,
            TaxAmount: totalTax,
            TotalIn: input.Subtotal + totalTax,
            Breakdown: breakdown,
            Error: TaxCalculationError.None,
            Detail: null);
    }

    private static TaxCalculationResult ApplyCompound(
        TaxCalculationInput input,
        IEnumerable<(TaxRate Rate, TaxJurisdiction Jur)> rates)
    {
        // Order rates outermost-first per Stage 02 §6.4 + JurisdictionLevel.OrderIndex.
        var ordered = rates.OrderBy(t => t.Jur.Level.OrderIndex()).ToList();

        var breakdown = new List<TaxRateBreakdownLine>();
        decimal running = input.Subtotal;
        decimal totalTax = 0m;
        foreach (var (r, j) in ordered)
        {
            var taxAmt = RoundMinor(running * (r.RatePercent / 100m));
            breakdown.Add(new TaxRateBreakdownLine(
                TaxRateId: r.Id,
                JurisdictionId: j.Id,
                JurisdictionLevel: j.Level,
                RatePercent: r.RatePercent,
                TaxableBase: running,
                TaxAmount: taxAmt,
                PayableAccountId: r.PayableAccountId));
            totalTax += taxAmt;
            running += taxAmt;             // compound on top
        }
        return new TaxCalculationResult(
            SubtotalIn: input.Subtotal,
            TaxAmount: totalTax,
            TotalIn: input.Subtotal + totalTax,
            Breakdown: breakdown,
            Error: TaxCalculationError.None,
            Detail: null);
    }

    private static TaxCalculationResult ApplyInclusive(
        TaxCalculationInput input,
        IEnumerable<(TaxRate Rate, TaxJurisdiction Jur)> rates)
    {
        var list = rates.ToList();
        var totalRatePct = list.Sum(t => t.Rate.RatePercent);
        var totalRate = totalRatePct / 100m;

        // Back out: tax = subtotal * rate / (1 + rate); preTaxBase = subtotal - tax
        var totalTax = RoundMinor(input.Subtotal * totalRate / (1m + totalRate));
        var preTaxBase = input.Subtotal - totalTax;

        // Pro-rate the per-rate breakdown by share-of-total-rate.
        var breakdown = new List<TaxRateBreakdownLine>();
        decimal allocated = 0m;
        for (int i = 0; i < list.Count; i++)
        {
            var (r, j) = list[i];
            decimal share;
            if (i == list.Count - 1)
            {
                // Last row catches any rounding residual to keep totals exact.
                share = totalTax - allocated;
            }
            else
            {
                share = RoundMinor(totalTax * (r.RatePercent / totalRatePct));
                allocated += share;
            }
            breakdown.Add(new TaxRateBreakdownLine(
                TaxRateId: r.Id,
                JurisdictionId: j.Id,
                JurisdictionLevel: j.Level,
                RatePercent: r.RatePercent,
                TaxableBase: preTaxBase,
                TaxAmount: share,
                PayableAccountId: r.PayableAccountId));
        }

        return new TaxCalculationResult(
            SubtotalIn: input.Subtotal,
            TaxAmount: totalTax,
            TotalIn: input.Subtotal,           // inclusive: total IS the subtotal
            Breakdown: breakdown,
            Error: TaxCalculationError.None,
            Detail: null);
    }

    /// <summary>
    /// Banker's rounding (MidpointRounding.ToEven) at the minor-unit
    /// boundary (2 decimals for USD/EUR). Prevents systematic over- or
    /// under-collection per Stage 02 §6.4.
    /// </summary>
    private static decimal RoundMinor(decimal value) =>
        Math.Round(value, 2, MidpointRounding.ToEven);

    private static TaxCalculationResult Fail(
        TaxCalculationInput input,
        TaxCalculationError error,
        string? detail) =>
        new(SubtotalIn: input.Subtotal,
            TaxAmount: 0m,
            TotalIn: input.Subtotal,
            Breakdown: Array.Empty<TaxRateBreakdownLine>(),
            Error: error,
            Detail: detail);
}
```

#### Supporting types

**`Services/ITaxCodeStore.cs`** (new in PR 3 — sibling to `ITaxJurisdictionStore`):

```csharp
public interface ITaxCodeStore
{
    Task<TaxCode?> GetAsync(TaxCodeId id, CancellationToken ct = default);
    Task<TaxCode?> GetByCodeAsync(ChartOfAccountsId chartId, string code, CancellationToken ct = default);
    Task<IReadOnlyList<TaxCode>> GetByChartAsync(ChartOfAccountsId chartId, CancellationToken ct = default);
    Task UpsertAsync(TaxCode taxCode, CancellationToken ct = default);
    Task SoftDeleteAsync(TaxCodeId id, Instant deletedAtUtc, CancellationToken ct = default);
}
```

**`Services/InMemoryTaxCodeStore.cs`** — `ConcurrentDictionary<TaxCodeId, TaxCode>` implementation.

#### DI registration

Extend `AddBlocksFinancialTax`:

```csharp
services.AddSingleton<ITaxCodeStore, InMemoryTaxCodeStore>();
services.AddSingleton<ITaxCalculationService, TaxCalculationService>();
```

#### Tests (PR 3)

`tests/TaxCalculationServiceTests.cs` — **mandatory regression battery**:

- `Calculate_TaxCodeNotFound_ReturnsTaxCodeNotFound`.
- `Calculate_ExemptCode_ReturnsZeroTaxAndEmptyBreakdown`.
- `Calculate_NoApplicableRates_ReturnsNoApplicableRates`.

**OnSubtotal cases:**

- `Calculate_OnSubtotal_SingleRate_Computes_5p3pctOf100_Equals5p30`.
- `Calculate_OnSubtotal_TwoRates_Sums_4p0pctStatePlus2p0pctCounty_On100_Equals6p00`.
- `Calculate_OnSubtotal_RoundsBankersHalfToEven_At2DecimalBoundary` (regression — feed 100.00 * 4.125% = 4.125 → rounds to 4.12, not 4.13, because 2 is even).
- `Calculate_OnSubtotal_PerRateBreakdown_PreservesPayableAccountIds`.

**Compound cases:**

- `Calculate_Compound_TwoRates_FederalThenState_AppliesStateOnSubtotalPlusFederal` (regression — federal 5% on 100.00 = 5.00; state 4% on (100 + 5) = 4.20; total = 9.20).
- `Calculate_Compound_OrderedByJurisdictionLevel_FederalBeforeStateBeforeCity` (assert the breakdown order matches the level-ordinal sort).
- `Calculate_Compound_RoundingAccumulatesCorrectly_NoFloatRoundoffDrift`.

**Inclusive cases:**

- `Calculate_Inclusive_SingleRate_BacksOutCorrectly_FromGrossOf108_AtRate8pct_Yields100p00BaseAnd8p00Tax`.
- `Calculate_Inclusive_TwoRates_ProratesByRateShare`.
- `Calculate_Inclusive_LastRowAbsorbsRoundingResidual_TotalIsExact` (regression — feed three 2.33% rates summing to 6.99% on 100.00; verify the sum of the three breakdown amounts equals the total tax to the cent).
- `Calculate_Inclusive_ZeroSubtotal_ReturnsInclusiveWithZeroSubtotalError`.

**Property-test-style edge case:**

- `Calculate_OnSubtotal_RandomFuzz_TotalEqualsSumOfBreakdown_Always` (a small fuzz: 100 random subtotals + 1–3 random rates; assert `result.TaxAmount == result.Breakdown.Sum(b => b.TaxAmount)` always to the cent).

Total new tests this PR: ~14–16.

#### Verification

- `dotnet build` succeeds.
- All previous tests pass.
- New PR 3 tests pass.
- **Fiscal correctness spot-check:** COB manually traces one OnSubtotal + one Compound + one Inclusive example through the algorithm by hand; verifies the test assertion matches. (No new test for this; it's a self-audit step.)

#### Do NOT in this PR

- Do NOT add `TaxFormLineMap` (PR 4).
- Do NOT wire `ITaxCalculationService` into `JournalPostingService` — that's PR 5.
- Do NOT introduce multi-currency conversion (out of scope; v1 assumes single-currency-per-chart per the ledger hand-off rationale).
- Do NOT add caching of tax-rate lookups; the in-memory store IS the cache for now. A future hand-off may add a memoization layer if hot-path performance requires.

---

### PR 4 — `TaxFormLineMap` entity + Schedule E seed (v1, provisional)

**Estimated effort:** ~2h
**Scope:** `TaxFormLineMap` entity per `blocks-reports-schema-design.md` §3 + §8; `TaxAccountSelector` shape; `IsProvisional` flag (ONR ratification pending); seed data for IRS Schedule E 2026 form lines 3–22 referencing the chart-of-accounts seeded in the ledger hand-off PR 5; `ITaxFormLineMapStore` query service
**Commit subject:** `feat(blocks-financial-tax): add TaxFormLineMap + Schedule E 2026 v1 seed per reports-schema §8 + IRS Pub 527`
**Depends on:** PR 3 merged + ledger hand-off PR 5 merged (`DefaultChartTemplates.RentalRealEstate` exists with Schedule-E-line-annotated account codes)
**Branch:** `cob/blocks-financial-tax-form-line-map`

#### New entity

**`Models/TaxFormKind.cs`** per reports-schema §3 — stable string codes per CRDT §5; **NOTE** the reports-schema uses kebab-case in the TypeScript surface (`'schedule-e'`), but the C# enum members are CamelCase. The serialization layer translates (a project-wide convention). For Loro storage, the canonical string is the kebab-case form.

```csharp
public enum TaxFormKind
{
    ScheduleE,         // Form 1040 Schedule E (rental real estate); Loro string: "schedule-e"
    Form1099Nec,       // Nonemployee Compensation; Loro string: "1099-nec"
    Form1099Misc,      // Misc income; Loro string: "1099-misc"
    ScheduleC,         // self-employed; Loro string: "schedule-c"
    Form1065K1,        // partnership K-1; Loro string: "form-1065-k1"
    StateRental,       // state-specific; Loro string: "state-rental"
}
```

Pair with an extension method for the canonical Loro/kebab string:

```csharp
public static class TaxFormKindExtensions
{
    public static string ToCanonical(this TaxFormKind kind) => kind switch
    {
        TaxFormKind.ScheduleE     => "schedule-e",
        TaxFormKind.Form1099Nec   => "1099-nec",
        TaxFormKind.Form1099Misc  => "1099-misc",
        TaxFormKind.ScheduleC     => "schedule-c",
        TaxFormKind.Form1065K1    => "form-1065-k1",
        TaxFormKind.StateRental   => "state-rental",
        _                          => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
```

**`Models/TaxAccountSelector.cs`** per reports-schema §3:

```csharp
public sealed record TaxAccountSelector(
    string? AccountCode = null,              // exact CoA code, e.g., "5100-Advertising"
    string? AccountCodePrefix = null,        // prefix, e.g., "61" = all utilities
    string? AccountTag = null,               // tag/category lookup
    bool Invert = false)                     // true = exclude from this line
{
    /// <summary>True iff this selector matches the given account.</summary>
    public bool Matches(GLAccountReference account)
    {
        bool hit = false;
        if (AccountCode is not null && account.Code == AccountCode) hit = true;
        else if (AccountCodePrefix is not null && account.Code.StartsWith(AccountCodePrefix, StringComparison.Ordinal)) hit = true;
        else if (AccountTag is not null && account.Tags.Contains(AccountTag, StringComparer.Ordinal)) hit = true;
        return Invert ? !hit : hit;
    }
}

public sealed record GLAccountReference(string Code, IReadOnlyCollection<string> Tags);
```

(`GLAccountReference` is a minimal projection that decouples the selector from the full `GLAccount` shape; the caller materializes it from a `GLAccount` row.)

**`Models/TaxFormLineMap.cs`** per reports-schema §3 — note the **`IsProvisional`** field added by this hand-off for ONR ratification pending:

```csharp
public sealed record TaxFormLineMap(
    TaxFormLineMapId Id,
    ChartOfAccountsId ChartId,
    TaxFormKind FormKind,
    int TaxYear,                              // 2026, 2027, etc. — IRS forms change year-over-year
    string Line,                              // "Line3", "Line5", ..., "Line22"
    string Description,                       // human label: "Rents received"
    IReadOnlyList<TaxAccountSelector> AccountSelectors,
    bool PerPropertyDimension,                // Schedule E lines aggregate per-property
    bool IsProvisional,                       // true = seeded pending ONR ratification; user-editable + audit-trailed
    string? ProvisionalRationale,             // e.g., "Pending ONR ratification per regulatory-us-rental-tax-input.md"
    string? CitationSource,                   // e.g., "IRS Pub 527 (2026), Schedule E line 5"
    bool IsActive = true,
    int Version = 1,                          // CRDT §3 — bumped on each user edit
    Instant? CreatedAtUtc = null,
    Instant? UpdatedAtUtc = null,
    Instant? DeletedAtUtc = null)
{
    public static TaxFormLineMap Create(
        ChartOfAccountsId chartId,
        TaxFormKind formKind,
        int taxYear,
        string line,
        string description,
        IReadOnlyList<TaxAccountSelector> selectors,
        bool perPropertyDimension,
        bool isProvisional,
        string? provisionalRationale = null,
        string? citationSource = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        return new TaxFormLineMap(
            Id: TaxFormLineMapId.New(),
            ChartId: chartId,
            FormKind: formKind,
            TaxYear: taxYear,
            Line: line,
            Description: description,
            AccountSelectors: selectors,
            PerPropertyDimension: perPropertyDimension,
            IsProvisional: isProvisional,
            ProvisionalRationale: provisionalRationale,
            CitationSource: citationSource,
            IsActive: true,
            Version: 1,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }
}
```

#### Schedule E 2026 seed

**`Seeds/DefaultTaxFormLineMap.cs`** — provisional v1 seed (every row `IsProvisional = true`). Citations are inline; line numbers per IRS Schedule E (Form 1040) 2026 layout.

```csharp
public static class DefaultTaxFormLineMap
{
    /// <summary>
    /// Returns a v1 provisional Schedule E line mapping seeded against the
    /// `DefaultChartTemplates.RentalRealEstate` chart shape (see
    /// `blocks-financial-ledger` PR 5 — account codes 1000–7200).
    ///
    /// IMPORTANT: Every row has `IsProvisional = true` and is pending
    /// ratification by ONR research per
    /// `icm/02_architecture/regulatory-us-rental-tax-input.md` (forthcoming
    /// at hand-off authoring 2026-05-16).
    /// </summary>
    public static IReadOnlyList<TaxFormLineMap> ScheduleE(
        ChartOfAccountsId chartId,
        int taxYear = 2026,
        Instant? createdAtUtc = null)
    {
        const string prov = "Pending ONR ratification per regulatory-us-rental-tax-input.md (2026-05-16).";

        TaxFormLineMap Row(string line, string desc, TaxAccountSelector[] sel, string irsCite, bool perProp = true) =>
            TaxFormLineMap.Create(
                chartId: chartId,
                formKind: TaxFormKind.ScheduleE,
                taxYear: taxYear,
                line: line,
                description: desc,
                selectors: sel,
                perPropertyDimension: perProp,
                isProvisional: true,
                provisionalRationale: prov,
                citationSource: $"IRS Pub 527 ({taxYear}); Schedule E (Form 1040) {taxYear}; {irsCite}",
                createdAtUtc: createdAtUtc);

        return new[]
        {
            // Income lines (per IRS Schedule E 2026 — confirmed line numbers may shift; flag is IsProvisional=true).
            Row("Line3", "Rents received",
                new[] { new TaxAccountSelector(AccountCodePrefix: "41") },                     // Rental Income (4100), Late Fees (4200)
                "Line 3 (Rents received)"),

            Row("Line4", "Royalties received",
                new[] { new TaxAccountSelector(AccountTag: "royalty-income") },                // No account in default chart; tag-only
                "Line 4 (Royalties received)"),

            // Expense lines 5–19.
            Row("Line5", "Advertising",
                new[] { new TaxAccountSelector(AccountCode: "5100") },
                "Line 5 (Advertising)"),

            Row("Line6", "Auto and travel",
                new[] { new TaxAccountSelector(AccountTag: "auto-travel") },                   // No account in default chart; tag-only
                "Line 6 (Auto and travel)"),

            Row("Line7", "Cleaning and maintenance",
                new[] { new TaxAccountSelector(AccountCode: "5200") },
                "Line 7 (Cleaning and maintenance)"),

            Row("Line8", "Commissions",
                new[] { new TaxAccountSelector(AccountTag: "commissions") },                   // tag-only
                "Line 8 (Commissions)"),

            Row("Line9", "Insurance",
                new[] { new TaxAccountSelector(AccountCode: "5300") },
                "Line 9 (Insurance)"),

            Row("Line10", "Legal and other professional fees",
                new[] { new TaxAccountSelector(AccountCode: "5400") },
                "Line 10 (Legal and other professional fees)"),

            Row("Line11", "Management fees",
                new[] { new TaxAccountSelector(AccountCode: "5500") },
                "Line 11 (Management fees)"),

            Row("Line12", "Mortgage interest paid to banks",
                new[] { new TaxAccountSelector(AccountCode: "7110") },
                "Line 12 (Mortgage interest paid to banks)"),

            Row("Line13", "Other interest",
                new[] { new TaxAccountSelector(AccountTag: "other-interest") },                // tag-only
                "Line 13 (Other interest)"),

            Row("Line14", "Repairs",
                new[] { new TaxAccountSelector(AccountCode: "5600") },
                "Line 14 (Repairs)"),

            Row("Line15", "Supplies",
                new[] { new TaxAccountSelector(AccountCode: "5700") },
                "Line 15 (Supplies)"),

            Row("Line16", "Taxes",
                new[] { new TaxAccountSelector(AccountCode: "6100") },                          // Property Tax
                "Line 16 (Taxes — primarily property tax)"),

            Row("Line17", "Utilities",
                new[] { new TaxAccountSelector(AccountCode: "5800") },
                "Line 17 (Utilities)"),

            Row("Line18", "Depreciation expense or depletion",
                new[] { new TaxAccountSelector(AccountCode: "7200") },
                "Line 18 (Depreciation expense or depletion)"),

            Row("Line19", "Other (with description)",
                new[] { new TaxAccountSelector(AccountTag: "schedule-e-line-19-other") },      // tag-only catch-all
                "Line 19 (Other)"),

            // Total + result lines (computed by Schedule E generator; mapping for completeness).
            Row("Line20", "Total expenses",
                Array.Empty<TaxAccountSelector>(),                                              // computed: Σ lines 5–19
                "Line 20 (Total expenses — sum lines 5–19)"),

            Row("Line21", "Income or loss",
                Array.Empty<TaxAccountSelector>(),                                              // computed: Line 3 − Line 20 + Line 4
                "Line 21 (Income or loss — Line 3 + Line 4 − Line 20)"),

            Row("Line22", "Deductible rental real estate loss after limitation",
                Array.Empty<TaxAccountSelector>(),                                              // computed: passive-activity adjusted
                "Line 22 (Deductible rental real estate loss after limitation)"),
        };
    }
}
```

**Provisional discipline:** every seeded row has `IsProvisional = true` and a `ProvisionalRationale` pointing at the ONR file path. When ONR's `regulatory-us-rental-tax-input.md` lands, a follow-on hand-off (`blocks-financial-tax-onr-ratification-addendum-stage06-handoff.md`) will (a) flip `IsProvisional` to `false` on every row that ONR confirms, (b) add/remove/edit any rows ONR specifies, (c) bump `Version` on every changed row, (d) emit `Reports.TaxFormLineMapEdited` events per the event-bus design §3.5.

#### Service

**`Services/ITaxFormLineMapStore.cs`**:

```csharp
public interface ITaxFormLineMapStore
{
    Task<TaxFormLineMap?> GetAsync(TaxFormLineMapId id, CancellationToken ct = default);
    Task<IReadOnlyList<TaxFormLineMap>> GetForFormAsync(
        ChartOfAccountsId chartId, TaxFormKind formKind, int taxYear,
        CancellationToken ct = default);
    Task UpsertAsync(TaxFormLineMap map, CancellationToken ct = default);
    Task<int> SeedScheduleEAsync(
        ChartOfAccountsId chartId, int taxYear,
        CancellationToken ct = default);    // returns number of rows seeded
    Task SoftDeleteAsync(TaxFormLineMapId id, Instant deletedAtUtc, CancellationToken ct = default);
}
```

**`Services/InMemoryTaxFormLineMapStore.cs`** — implementation. `SeedScheduleEAsync` is idempotent: if rows already exist for `(chartId, ScheduleE, taxYear)`, it returns `0` and does NOT overwrite (the user may have edited them per Q3 mutability). Test-only `SeedScheduleEAsync(force: true)` override is acceptable for fixtures.

#### DI registration

Extend `AddBlocksFinancialTax`:

```csharp
services.AddSingleton<ITaxFormLineMapStore, InMemoryTaxFormLineMapStore>();
```

#### Tests (PR 4)

`tests/TaxAccountSelectorTests.cs`:

- `Matches_ExactCode_Hits`.
- `Matches_Prefix_Hits`.
- `Matches_Tag_Hits`.
- `Matches_Invert_TogglesResult`.
- `Matches_NoSelector_DoesNotMatchAnything`.

`tests/TaxFormLineMapTests.cs`:

- `Create_DefaultsIsActiveTrueIsProvisionalAsGiven`.
- `Create_PreservesAllFields`.

`tests/DefaultTaxFormLineMapTests.cs`:

- `ScheduleE_2026_Returns19RowsAcrossLines3Through22`.
- `ScheduleE_AllRowsAreProvisional`.
- `ScheduleE_AllRowsHaveCitationSource`.
- `ScheduleE_PerPropertyDimensionTrueForAllRevenueAndExpenseLines`.
- `ScheduleE_Line5_AdvertisingMapsToAccountCode5100`.
- `ScheduleE_Line14_RepairsMapsToAccountCode5600`.
- `ScheduleE_Line16_TaxesMapsToAccountCode6100_PropertyTax` (regression — verifies the property-tax-on-schedule-E clarification in the Context section).
- `ScheduleE_Line18_DepreciationMapsToAccountCode7200`.
- `ScheduleE_Line20And21And22_HaveEmptyAccountSelectors_BecauseComputed`.

`tests/InMemoryTaxFormLineMapStoreTests.cs`:

- `SeedScheduleE_OnEmptyStore_Inserts19Rows`.
- `SeedScheduleE_OnPreSeededStore_ReturnsZeroAndPreservesExisting` (idempotency).
- `GetForForm_ScheduleE_2026_Returns19Rows`.
- `GetForForm_ScheduleE_2027_OnUnseededYear_ReturnsEmpty`.
- `Upsert_BumpsVersion_OnReUpsert`.

Total new tests this PR: ~18–20.

#### Verification

- `dotnet build` succeeds.
- All previous tests pass.
- New PR 4 tests pass.
- The seed data references account codes that **actually exist** in `blocks-financial-ledger.DefaultChartTemplates.RentalRealEstate`. Add a cross-package integration test in `tests/Integration/ScheduleESeedAgainstChartTests.cs`:
  - `ScheduleE_AllExactCodeSelectors_ResolveAgainstRentalRealEstateChart` — seed both the chart (via `IChartSeedingService`) and the Schedule E map; for every `TaxFormLineMap` whose `AccountSelectors` contain an `AccountCode` (exact code), verify the chart has an account with that code.

#### Do NOT in this PR

- Do NOT seed 1099 mappings — that's a follow-on hand-off (after ONR; the 1099 threshold rules + vendor-eligibility logic are part of `blocks-people-*` + ONR scope).
- Do NOT generate the Schedule E PDF — that's `blocks-reports-tax`.
- Do NOT auto-seed 2027 mappings — Q4 of the reports-schema design recommends manual seeding for the first year transition.
- Do NOT add the Q3 audit-trail event emission yet — that's PR 5 (`Reports.TaxFormLineMapEdited` event).

---

### PR 5 — Ledger wiring + ERPNext importer integration + event emission

**Estimated effort:** ~2h
**Scope:** wire `ITaxCalculationService` into `JournalPostingService` to auto-generate tax-payable lines on Invoice/Bill posting; ERPNext importer integration (Pass 2 — tax codes); event emission for `Financial.TaxCodeAdded`, `Financial.TaxRateAdded`, `Financial.TaxRateExpired`, `Reports.TaxFormLineMapEdited`; `apps/docs/blocks-financial-tax/overview.md` docs page
**Commit subject:** `feat(blocks-financial-tax): wire calculation into journal posting + ERPNext importer hooks + event emission`
**Depends on:** PR 4 merged
**Branch:** `cob/blocks-financial-tax-ledger-wiring`

#### New types — ledger integration

**`Services/IJournalTaxLineGenerator.cs`** — service that takes a list of pre-tax `JournalEntryLine` candidates with `TaxCodeId` annotations, computes the tax, and returns the augmented line set (originals + tax-payable lines):

```csharp
public interface IJournalTaxLineGenerator
{
    /// <summary>
    /// Given a set of pre-tax journal lines (each may carry a TaxCodeId),
    /// expand them with tax-payable lines per the resolved rates. Each
    /// per-rate breakdown line produces one additional JournalEntryLine
    /// against the rate's PayableAccountId, preserving the per-jurisdiction
    /// audit trail per Stage 02 §6.4 ("the per-rate breakdown is stored on
    /// the line so that GL posting can split tax-payable into one line per
    /// jurisdiction").
    /// </summary>
    Task<JournalTaxLineGenerationResult> GenerateAsync(
        IReadOnlyList<JournalEntryLine> preTaxLines,
        LocalDate transactionDate,
        TaxLocationContext location,
        CancellationToken cancellationToken = default);
}

public sealed record JournalTaxLineGenerationResult(
    IReadOnlyList<JournalEntryLine> AllLines,        // pre-tax + new tax-payable lines, balanced
    decimal TotalTaxAmount,
    IReadOnlyList<TaxCalculationResult> PerLineResults,
    TaxCalculationError? FirstError,
    string? Detail);
```

**`Services/JournalTaxLineGenerator.cs`** — implementation that:

1. For each pre-tax line with non-null `TaxCodeId`, calls `ITaxCalculationService.CalculateAsync`.
2. For each `TaxRateBreakdownLine` in the result, generates a new `JournalEntryLine` with:
   - `AccountId = breakdown.PayableAccountId`
   - `Credit = breakdown.TaxAmount` (tax-payable is a Liability — credited when accrued)
   - `Debit = 0`
   - `LineMemo = $"Tax — {jurisdiction.Name} {breakdown.RatePercent:F4}%"`
   - `TaxCodeId = original line's TaxCodeId` (propagated for audit)
3. Returns the union of pre-tax lines + tax-payable lines. Caller is responsible for adding a corresponding offsetting Debit on the AR/AP account (the Invoice/Bill posting flow does this).
4. If any tax calculation fails (`TaxCodeNotFound`, etc.), aggregates the first error in `FirstError` for the caller's structured error handling.

#### Event types

**`Models/Events/FinancialTaxEvents.cs`** per event-bus design §3.1. Note: the events here are emitted via `IDomainEventEmitter` (already exists in the ledger or foundation tier; if not, file `cob-question-*`). Each event implements the canonical envelope per event-bus §1.

```csharp
public sealed record TaxCodeAddedEvent(
    TaxCodeId TaxCodeId,
    ChartOfAccountsId ChartId,
    string Code,
    TaxKind Kind,
    TaxApplication Application);

public sealed record TaxCodeUpdatedEvent(
    TaxCodeId TaxCodeId,
    ChartOfAccountsId ChartId,
    int NewVersion);

public sealed record TaxRateAddedEvent(
    TaxRateId TaxRateId,
    TaxCodeId TaxCodeId,
    TaxJurisdictionId JurisdictionId,
    decimal RatePercent,
    LocalDate EffectiveDate,
    GLAccountId PayableAccountId);

public sealed record TaxRateExpiredEvent(
    TaxRateId TaxRateId,
    TaxCodeId TaxCodeId,
    TaxJurisdictionId JurisdictionId,
    LocalDate ExpiryDate);

public sealed record TaxFormLineMapEditedEvent(
    TaxFormLineMapId MapId,
    ChartOfAccountsId ChartId,
    TaxFormKind FormKind,
    int TaxYear,
    string Line,
    IReadOnlyList<TaxAccountSelector> PriorSelectors,
    IReadOnlyList<TaxAccountSelector> NewSelectors,
    int NewVersion,
    string? EditedByPrincipalId);
```

Event canonical names (per event-bus §2):

- `Financial.TaxCodeAdded`
- `Financial.TaxCodeUpdated`
- `Financial.TaxRateAdded`
- `Financial.TaxRateExpired`
- `Reports.TaxFormLineMapEdited` — note: `Reports.*` prefix is correct per event-bus §3.5 (the mapping is *consumed* by reports-tax; the producer-prefix rule applies even though the storage is in `blocks-financial-tax`. Acceptable per event-bus §2 because the `TaxFormLineMap` is a *reports-domain* concept storage-collocated here for convenience — flag in the docs).

#### Event emission wiring

Update the in-memory stores to emit events on each successful write:

- `InMemoryTaxCodeStore.UpsertAsync` → emit `Financial.TaxCodeAdded` on insert, `Financial.TaxCodeUpdated` on version-bump update.
- `InMemoryTaxRateLookup.UpsertAsync` → emit `Financial.TaxRateAdded` on insert.
- `InMemoryTaxRateLookup.SupersedeAsync` → emit `Financial.TaxRateExpired` (for the superseded row) + `Financial.TaxRateAdded` (for the new row).
- `InMemoryTaxFormLineMapStore.UpsertAsync` → emit `Reports.TaxFormLineMapEdited` if `priorVersion < newVersion` (i.e., on real edits, not first-insert).

The emission goes through an injected `IDomainEventEmitter`. If that interface doesn't yet exist in the ledger or foundation tier, ship a local placeholder `IFinancialTaxEventEmitter` in this PR with a `NullFinancialTaxEventEmitter` default implementation, and add a TODO comment to migrate to the canonical `IDomainEventEmitter` when it lands.

#### ERPNext importer integration

**`Migration/IErpnextTaxImporter.cs`** — Pass 2 of the migration importer (per `_shared/engineering/erpnext-to-anchor-migration-importer-spec.md`):

```csharp
public interface IErpnextTaxImporter
{
    /// <summary>
    /// Upserts a TaxCode + its TaxRate history from an ERPNext source
    /// record. Idempotent on (source, externalRef). Maps ERPNext
    /// "Sales Taxes and Charges Template" + "Account Tax" rows to the
    /// (TaxCode, TaxRate) pair.
    /// </summary>
    Task<ImportOutcome<TaxCode>> UpsertFromErpnextAsync(
        ErpnextTaxSource source,
        ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default);
}

public sealed record ErpnextTaxSource(
    string Name,                            // ERPNext "name" — stable id
    string Modified,                        // ERPNext "modified" — version key
    string TaxName,
    string TaxRateRowsJson,                 // ERPNext taxes table: account_head, rate, included_in_print_rate
    bool Disabled);
```

**`Migration/ErpnextTaxImporter.cs`** — implementation:

1. Look up existing `TaxCode` by `ExternalRef == source.Name` (the `TaxCode.Notes` field can carry `externalRef:<source.Name>` for now; a proper `ExternalRef` field can be added in a later schema iteration).
2. If exists and version matches → return `Skipped`.
3. If new → parse `TaxRateRowsJson`; for each row resolve `account_head` to a `GLAccountId` via the ledger's `IAccountResolver.GetByExternalRefAsync`; classify by sign (positive rate → `Sales`, etc.); call `TaxCode.Create` + `TaxRate.Create` for each row; persist; return `Inserted`.

This is **not the primary tax-import path** for Wave/Rentler/Mac (those source systems don't model tax the way ERPNext does); it exists for completeness against the migration-importer spec.

#### DI registration

Extend `AddBlocksFinancialTax`:

```csharp
services.AddSingleton<IJournalTaxLineGenerator, JournalTaxLineGenerator>();
services.AddSingleton<IErpnextTaxImporter, ErpnextTaxImporter>();
services.AddSingleton<IFinancialTaxEventEmitter, NullFinancialTaxEventEmitter>();  // placeholder; replace with IDomainEventEmitter integration when available
```

#### Docs

**`apps/docs/blocks-financial-tax/overview.md`** — cluster docs page following the convention from the ledger hand-off:

Structure (target ~150 lines):

- **Overview** — what this package provides; ADR 0088 §1 citation; relationship to `-ledger` / `-ar` / `-ap` / `-reports-tax`.
- **Entities** — bullet list with one-line each: `TaxJurisdiction`, `TaxCode`, `TaxRate`, `TaxFormLineMap`.
- **Tax calculation** — short explanation of the three applications (OnSubtotal / Compound / Inclusive); link to Stage 02 §6.4 for the algorithm; banker's-rounding note.
- **Jurisdiction hierarchy** — example tree (US → US-VA → Frederick County → Winchester City); how `ITaxJurisdictionResolver` walks it.
- **Schedule E mapping** — the v1 provisional seed; `IsProvisional` discipline; ONR ratification pending; per-property dimension; tenant-editable per Q3 of reports-schema design.
- **Property tax handling** — short note: property tax is NOT a tax-code-on-transaction; it's a journal-entry on a recurring schedule via `blocks-financial-ap`; the Schedule E Line-16 mapping (`accountCode: "6100"`) is the touchpoint.
- **Events** — bullet list of the five events emitted; consumers (`blocks-reports-tax` for Schedule E generation).
- **Quickstart** — 10–15-line snippet: seed a US-VA jurisdiction, create a 5.3% sales-tax `TaxCode`, calculate tax on a $100 line.
- **Related** — `blocks-financial-ledger`, `blocks-financial-ar`, `blocks-financial-ap`, `blocks-financial-periods`, `blocks-reports-tax`.
- **Known limitations / pending work:**
  - v1 Schedule E mapping is provisional (ONR ratification pending).
  - No automated property-tax-bill ingestion (Phase 2).
  - No multi-currency conversion in tax calc (v1 = single currency per chart).
  - SQLite persistence is in-memory in v1; persistent store lands with the foundation-localfirst SQLite write-path hand-off.

#### Tests (PR 5)

`tests/JournalTaxLineGeneratorTests.cs`:

- `Generate_NoTaxCodes_ReturnsLinesUnchanged`.
- `Generate_OneLineWithTaxCode_AddsOneTaxPayableLine` (single-rate scenario).
- `Generate_OneLineWithCompoundTaxCode_AddsOneTaxPayableLinePerJurisdiction` (compound w/ 2 rates → 2 tax-payable lines).
- `Generate_TwoLinesSameTaxCode_AggregatesPayableLinesByPayableAccountId` (or keeps per-line; verify the design decision in the test — XO recommendation: keep per-line for audit trail; aggregation is a reports-layer concern).
- `Generate_TaxCodeNotFound_ReturnsFirstErrorWithDetail`.
- `Generate_TaxPayableLinesCarryOriginalLineTaxCodeId_ForAuditTrail`.

`tests/EventEmissionTests.cs`:

- `UpsertTaxCode_FirstInsert_Emits_TaxCodeAddedEvent`.
- `UpsertTaxCode_VersionBump_Emits_TaxCodeUpdatedEvent`.
- `UpsertTaxRate_NewRow_Emits_TaxRateAddedEvent`.
- `SupersedeTaxRate_HappyPath_Emits_TaxRateExpired_Then_TaxRateAdded_InOrder`.
- `UpsertTaxFormLineMap_EditedRow_Emits_TaxFormLineMapEditedEvent_WithPriorAndNewSelectors`.
- `UpsertTaxFormLineMap_InitialSeed_DoesNotEmitEditedEvent` (only real edits emit).

`tests/ErpnextTaxImporterTests.cs`:

- `Upsert_NewSource_InsertsTaxCodeAndRates`.
- `Upsert_SameVersion_ReturnsSkipped`.
- `Upsert_TwoRateRows_CreatesTwoTaxRateRecords`.
- `Upsert_DisabledTrue_SetsTaxCodeIsActiveFalse`.

Total new tests this PR: ~14–16.

#### Verification

- `dotnet build` succeeds.
- All previous tests across PRs 1–5 pass (~75–85 tests total — exceeds the ~30–35 target because we factored breakdown tests granularly; that's acceptable).
- The new package + tests are added to the solution.
- `apps/docs/blocks-financial-tax/overview.md` renders without broken links.

#### Do NOT in this PR

- Do NOT modify `JournalPostingService` directly — `IJournalTaxLineGenerator` is a separate service that callers invoke BEFORE constructing the final `JournalEntry`. The posting service remains agnostic to tax. (This preserves the ledger hand-off's posting-algorithm scope; tax is a layer above.)
- Do NOT seed any production `TaxCode` / `TaxRate` / `TaxJurisdiction` records. Production seeding (US-VA + Frederick County + Winchester City for the Acero Properties chart, for example) lands in a follow-on operational-config hand-off, not in this Stage 06 substrate hand-off.
- Do NOT add Schedule E PDF rendering — that's `blocks-reports-tax`.
- Do NOT ratify the provisional flags — that's the ONR ratification addendum hand-off.

---

## CRDT-friendly schema conventions applied

This hand-off applies the cluster's CRDT-friendly conventions per `_shared/engineering/crdt-friendly-schema-conventions.md`.

### 1. ULID identifiers (§1)

All entity IDs (`TaxCodeId`, `TaxRateId`, `TaxJurisdictionId`, `TaxFormLineMapId`) use ULIDs per §1: lexicographically sortable, time-prefixed, globally unique without coordination. Critical for offline-first creation: a peer creating a `TaxRate` while disconnected won't collide with another peer doing the same. No autoincrement counters.

### 2. Soft-delete via tombstones (§2)

`TaxCode`, `TaxJurisdiction`, `TaxRate`, `TaxFormLineMap` all carry `DeletedAtUtc` (nullable). Hard-delete is never permitted by the in-memory stores; the SQLite-layer write-path (future hand-off) will enforce the same. Historical records are auditable forever — necessary for tax-form regeneration ("what was the rate in tax year 2025?").

### 3. Version vectors (§3)

`TaxCode` and `TaxFormLineMap` carry a `Version` integer that the upsert path bumps on every successful write. (`TaxJurisdiction` is treated as effectively immutable post-create — jurisdictions rarely change. `TaxRate` is append-only and never versions in place.) Under Loro, concurrent edits to the same `TaxCode` produce a conflict that the application layer resolves by accepting the higher-version write; ties are CO-or-COB-arbitrated.

### 4. Append-only sub-collections (§4)

**`TaxRate` is append-only** per CRDT §4. The Stage 02 §3.12 surface `TaxCode.rates: ReadonlyArray<TaxRate>` is **NOT** an embedded array — it's a derived view computed by `ITaxRateLookup.GetAllForTaxCodeAsync(taxCodeId)`. Rate changes are NEW rows (with the prior row's `ExpiryDate` set on supersession), never in-place edits. This makes Loro merge of two peers' simultaneous rate changes well-defined (both inserts succeed; the application layer must reconcile any overlapping-range error post-merge).

### 5. Stable string codes (§5)

`TaxKind`, `TaxApplication`, `JurisdictionLevel`, `TaxFormKind` all use C# enums whose **member names are the canonical Loro/SQLite serialization form** (with the kebab-case exception for `TaxFormKind` exposed via `TaxFormKindExtensions.ToCanonical()`). Members are append-only — new kinds (`TaxKind.Stamp`, `JurisdictionLevel.Tribal`) can be added without renumbering. Retired members (none yet) would stay in the enum with a `[Obsolete]` marker per §5 "Deprecating codes — never reuse."

### 6. Posted-then-immutable pattern (§6) — partial application

`TaxRate` is **append-only-then-immutable** — once persisted, the only allowed mutation is setting `ExpiryDate` (via supersession) and `DeletedAtUtc` (via tombstone). `RatePercent`, `EffectiveDate`, `PayableAccountId` are immutable after insert. This pattern matches §6 ("the posted-then-immutable pattern") even though `TaxRate` doesn't have an explicit status transition — the immutability is by construction (record type with no `with` exposures in the upsert path; `ITaxRateLookup.UpsertAsync` constructs new records, never mutates existing).

### 7. Loro append-only constraint (open question, deferred)

Per the ledger hand-off, Q10 of `blocks-financial-schema-design.md` ("Loro append-only constraint on posted journal entries — needs `foundation-localfirst` owner coordination") remains open. **The PRs in this hand-off do not depend on Q10's resolution** — they implement immutability at the service layer; the Loro-side enforcement is independent. When `foundation-localfirst` ratifies the Loro op-mapping, the integration is additive.

---

## Event-bus catalog additions

This hand-off adds the following events to the canonical catalog per `_shared/engineering/cross-cluster-event-bus-design.md` §3.1 and §3.5. The catalog upkeep rule (§3 "Catalog upkeep") requires the cross-cluster-event-bus-design.md file to be updated in the same PR that ships the event types — **do that in PR 5**.

| Event name | Consumers (non-exhaustive) | Payload shape | Idempotency key |
|---|---|---|---|
| `Financial.TaxCodeAdded` | reports, ar, ap | `{ taxCodeId, chartId, code, kind, application }` | `tax-code-added:{taxCodeId}` |
| `Financial.TaxCodeUpdated` | reports, ar, ap | `{ taxCodeId, chartId, newVersion }` | `tax-code-updated:{taxCodeId}:{newVersion}` |
| `Financial.TaxRateAdded` | reports, ar, ap, audit | `{ taxRateId, taxCodeId, jurisdictionId, ratePercent, effectiveDate, payableAccountId }` | `tax-rate-added:{taxRateId}` |
| `Financial.TaxRateExpired` | reports, ar, ap, audit | `{ taxRateId, taxCodeId, jurisdictionId, expiryDate }` | `tax-rate-expired:{taxRateId}:{expiryDate}` |
| `Reports.TaxFormLineMapEdited` | (audit; reports for cache invalidation) | `{ mapId, chartId, formKind, taxYear, line, priorSelectors, newSelectors, newVersion, editedByPrincipalId? }` | `tax-map-edited:{mapId}:{newVersion}` |

PR 5 includes a one-line update to `_shared/engineering/cross-cluster-event-bus-design.md` §3.1 + §3.5 appending these rows (small additive doc change in the same PR; no separate doc PR needed).

---

## License posture

### Borrowed-with-attribution (permissive)

- **Apache OFBiz** (Apache 2.0) — the cluster's entity shapes (`TaxCode → TaxRate → TaxJurisdiction` three-level decomposition; `TaxAuthorityGlAccount` shape inspiration for `TaxFormLineMap`; effective-dated rate-history pattern) derive from OFBiz's `accounting/TaxAuthority`, `accounting/TaxAuthorityRateProduct`, and `accounting/TaxAuthorityGlAccount` entity models per `blocks-financial-schema-design.md` §11.1 + §1 license posture.

**Attribution requirements (same shape as ledger hand-off):**

1. `Sunfish.Blocks.FinancialTax.csproj` carries `<NOTICEFile>NOTICE.md</NOTICEFile>`.
2. `packages/blocks-financial-tax/NOTICE.md` (ships in PR 1; full text reproduced in PR 1's File operations section above).
3. Source-header comments on `TaxCode.cs`, `TaxRate.cs`, `TaxJurisdiction.cs`, `TaxFormLineMap.cs`, and `Seeds/DefaultTaxFormLineMap.cs` reference OFBiz or IRS Pub 527 in a one-line comment.

### Public-domain references (direct citation)

- **IRS Publication 527** (Residential Rental Property; current edition).
- **IRS Schedule E (Form 1040)** instructions.
- **IRS Form 1099-NEC / 1099-MISC** instructions (for forthcoming 1099 mapping hand-off).

US federal works are public domain; no license entry needed. Cite inline in code comments and in the seed `CitationSource` field.

### Clean-room only (copyleft)

Per `blocks-financial-schema-design.md` §11.2–§11.5 + `blocks-reports-schema-design.md` §2:

- **GnuCash** (GPLv2) — tax-line mapping table studied for understanding; facts (line numbers ↔ account categories) are uncopyrightable per *Feist v. Rural Telephone*; no code transferred.
- **ERPNext / Frappe** (GPLv3) — tax-account DocType structure consumed as a *data format* for the importer integration; no code transferred.
- **Beancount / ledger-cli** (GPLv2) — read for understanding only; no code transferred.

**Discipline check before merging any PR in this hand-off:**

1. No copyleft code was opened in any editor session that produced this hand-off's PRs.
2. No identifier names from any GPL/AGPL source appear in the new code (spot-check by grep before merge — names like `account_head`, `tax_rate`, `included_in_print_rate` MAY appear as ERPNext source-format strings in the importer, but only as string literals capturing the external schema, not as Sunfish identifiers).
3. Stage 02 §3.12–§3.14 + reports-schema §3 + §8 are the source of truth for type shapes; deviations from those require XO ratification.

### Sunfish output

**All code authored under this hand-off is MIT-licensed** per ADR 0088 §2 and the project-wide license posture.

---

## Test plan

### Per-PR minima (summary; details under each PR above)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (scaffold + TaxJurisdiction + TaxCode) | ~12–14 | entity construction; jurisdiction-level ordering; resolver fixture roundtrip |
| PR 2 (TaxRate + ITaxRateLookup) | ~14–16 | rate-range validation; date-effective lookup; supersede atomicity; payable-account-subtype gate |
| PR 3 (ITaxCalculationService) | ~14–16 | OnSubtotal / Compound / Inclusive correctness; banker's rounding; per-rate breakdown; fuzz |
| PR 4 (TaxFormLineMap + seed) | ~18–20 | selector match logic; Schedule E seed integrity; provisional flag; idempotent seeding |
| PR 5 (ledger wiring + events + importer) | ~14–16 | tax-line generation; event emission; ERPNext importer mapping |
| **Total** | **~72–82** | (target was 30–35; the hand-off factored tests granularly per the security-criticality of the calculation engine — acceptable scope creep) |

### Cluster-level acceptance (PASS gate at end of PR 5)

**A1.** `dotnet build` succeeds on the new `Sunfish.Blocks.FinancialTax` package and every existing consumer (none at hand-off end; the package is leaf in PR 5).

**A2.** `dotnet test packages/blocks-financial-tax/tests/` passes ~72–82 tests across all 5 PRs.

**A3.** A US-VA-Frederick-Winchester jurisdiction tree seeded via `InMemoryTaxJurisdictionStore` resolves correctly through `ITaxJurisdictionResolver.ResolveAsync`:
- A property in Winchester returns `[Winchester (City), Frederick (County), Virginia (State), United States (Federal)]` in level order.
- A property in unincorporated Frederick County returns `[Frederick (County), Virginia (State), United States (Federal)]` (no city).

**A4.** Tax calculation correctness (the canonical fiscal-correctness regression battery):

- **OnSubtotal:** TaxCode = `US-VA-SALES` (rate 5.3% on a single State rate), Subtotal = `$100.00`, transactionDate = `2026-01-01` → result `{ TaxAmount: $5.30, TotalIn: $105.30, Breakdown: 1 line at $5.30 }`.
- **Compound:** TaxCode = `US-VA-FREDERICK-COMPOUND` (Federal 1% + State 4% + County 1%, Compound), Subtotal = `$100.00` → Federal $1.00 first; State 4% × $101.00 = $4.04; County 1% × $105.04 = $1.05; total $6.09; breakdown order matches level-ordinal.
- **Inclusive:** TaxCode = `EU-DE-VAT19` (Inclusive 19% VAT), Subtotal = `$119.00` (the gross) → result `{ TaxAmount: $19.00, TotalIn: $119.00, Breakdown: 1 line at $19.00, TaxableBase $100.00 }`.
- **Exempt:** TaxCode = `EXEMPT`, any subtotal → result `{ TaxAmount: $0, TotalIn: $100.00, Breakdown: [] }`.

**A5.** Effective-date semantics: a `TaxCode` with two rate rows for the same jurisdiction — one expired (2025-01-01 → 2025-12-31 at 5.0%) and one active (2026-01-01 → null at 5.3%) — produces:
- `GetActiveRatesAsync(date: 2025-06-15, ...)` → returns the 5.0% rate.
- `GetActiveRatesAsync(date: 2026-06-15, ...)` → returns the 5.3% rate.
- `GetActiveRatesAsync(date: 2025-12-31, ...)` → returns the 5.0% rate (inclusive end-of-range).
- `GetActiveRatesAsync(date: 2026-01-01, ...)` → returns the 5.3% rate (inclusive start-of-range).

**A6.** Schedule E v1 seed integrity:
- `DefaultTaxFormLineMap.ScheduleE(chartId, 2026)` returns exactly 19 rows (Lines 3, 4, 5–19, 20, 21, 22).
- Every row has `IsProvisional = true`.
- Every row has a non-null `CitationSource`.
- Every exact-code selector (e.g., `AccountCode: "5100"`) resolves to an account present in `DefaultChartTemplates.RentalRealEstate` (cross-package verification).

**A7.** Property tax discipline: the Schedule E Line-16 (`Taxes`) seed row selects on account code `6100` (Property Tax) — verifying that property tax flows through Schedule E via the chart-of-accounts, NOT via `TaxCalculationService`. (Regression test guards against future refactors that might mis-route property tax.)

**A8.** Event emission: upserting a `TaxCode` emits exactly one `Financial.TaxCodeAdded` event with the correct payload; adding a rate emits `Financial.TaxRateAdded`; superseding emits both `Financial.TaxRateExpired` and `Financial.TaxRateAdded` in order; editing a `TaxFormLineMap` emits `Reports.TaxFormLineMapEdited` with prior + new selectors.

**A9.** ERPNext importer roundtrip: a synthetic ERPNext "Sales Taxes and Charges Template" record with two rate rows imports into one `TaxCode` + two `TaxRate` records via `IErpnextTaxImporter.UpsertFromErpnextAsync`; re-importing the same source returns `Skipped`.

**A10.** Cross-cluster docs page: `apps/docs/blocks-financial-tax/overview.md` exists, references ADR 0088 §1, links to Stage 02 §3.12–§3.14 + §6.4, includes the property-tax clarification paragraph, and lists the v1-provisional Schedule E caveat.

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*` beacon to `coordination/inbox/`.

### 1. Ledger hand-off PR 2 not merged (PR 1 of this hand-off)

If `ChartOfAccountsId` doesn't exist in `blocks-financial-ledger/` at PR 1 start, the ledger hand-off PR 2 hasn't shipped yet. File `cob-question-2026-05-XXTHH-MMZ-w60-p4-tax-blocked-on-ledger-pr2.md`. Recommended action: pause `-tax` work until ledger PR 2 merges; do NOT scaffold a local copy of `ChartOfAccountsId` in this package.

### 2. `LegalEntityId` placement (PR 1)

Same situation as the ledger hand-off (whose PR 2 made the call). Use whatever the ledger hand-off chose (foundation-identity path if it landed, else the local placeholder). Do NOT introduce a third copy. If unclear, file `cob-question-*`.

### 3. ONR research output lands mid-build (any PR)

If `icm/02_architecture/regulatory-us-rental-tax-input.md` appears in repo during PR 1–5 of this hand-off, **do not in-line the ratifications** in this hand-off's PRs. The ONR ratification is a separate follow-on hand-off (`blocks-financial-tax-onr-ratification-addendum-stage06-handoff.md`). Continue PR 1–5 with the v1 provisional seed unchanged. The follow-on hand-off will:
- Set `IsProvisional = false` on ratified rows.
- Add new selectors / new rows ONR specifies.
- Bump `Version` and emit `Reports.TaxFormLineMapEdited` per row.
- Add a `CitationSource` referencing the ONR file path.

If the COB feels the ONR output is small enough to absorb in PR 4 (the seed PR), file `cob-question-*` first — XO recommendation is to ship v1 unchanged and let the addendum hand-off do the ratification cleanly with audit-trail events.

### 4. Multi-jurisdiction precedence ambiguity (PR 3)

The compound-tax ordering is well-defined by `JurisdictionLevel.OrderIndex()` for the cases in the seeded enum. **Edge case:** if a TaxCode has rates at the same level (two County rates, for example) on overlapping but non-identical jurisdictions, the OrderIndex is identical and the secondary ordering is undefined. XO ruling: secondary-order tie-break by `TaxJurisdictionId` (ULID-sortable, deterministic). If a real-world scenario emerges where this matters (e.g., a city-overlapping-special-district produces different totals depending on order), file `cob-question-*` for an explicit precedence rule.

### 5. Property tax slipping into TaxCalculationService (any PR)

If during PR 1–5 a council reviewer suggests modeling property tax as a `TaxCode` ("but it's a tax!"), respond with reference to:
- This hand-off Context §"Property-tax handling (clarification)" — property tax is NOT a line-item tax; it's a recurring journal entry.
- Stage 02 reports-schema §8.1 — Schedule E Line 16 captures property tax via the chart-of-accounts code 6100, NOT via a TaxCode.

**Do NOT add `TaxKind.PropertyTax`** to the `TaxKind` enum. If the council pushes back and the rationale has substance not addressed above, file `cob-question-*`.

### 6. Event emitter interface not yet available (PR 5)

If `IDomainEventEmitter` (or equivalent — `IEventBus`, `IFinancialEventEmitter`) doesn't yet exist in the ledger or foundation tier at PR 5 start, ship the local placeholder `IFinancialTaxEventEmitter` + `NullFinancialTaxEventEmitter` per PR 5's spec. Add a TODO comment per file. Do NOT block PR 5 on the cross-cluster event-bus implementation landing — the placeholder is sufficient for the substrate.

If a parallel session has shipped the canonical emitter between hand-off authoring (2026-05-16) and PR 5 work, USE it instead of the placeholder. The grep:

```bash
grep -r "IDomainEventEmitter\|IEventBus\|IFinancialEventEmitter" packages/foundation-* packages/blocks-financial-ledger/ 2>/dev/null | head -10
```

### 7. `apps/docs` infrastructure absent

If `apps/docs/blocks-financial-tax/` doesn't have the expected directory structure (e.g., the docs site uses a different convention), file `cob-question-*`. XO recommendation: follow the existing `apps/docs/{cluster}/overview.md` pattern observed for `apps/docs/blocks-financial-ledger/` (which the ledger hand-off seeded). If that pattern itself has changed, surface to XO.

### 8. Tax-form-mapping mutability concerns (PR 4)

Reports-schema Q3 recommends: editable, with audit trail. This hand-off ships exactly that. If a council reviewer challenges the editability (proposing read-only system-managed mappings), respond with reference to Q3 of `blocks-reports-schema-design.md`. **DO NOT** silently flip to read-only — surface the concern to XO via `cob-question-*` first.

### 9. SQLite persistence layer encountered mid-build

The hand-off assumes in-memory implementations for all stores. If a parallel session has wired a SQLite-backed implementation for any sibling package and the COB is tempted to mirror the pattern in `-tax`, **do NOT do that in this hand-off**. SQLite persistence is a separate `foundation-localfirst`-driven hand-off. File `cob-question-*` to confirm sequencing.

### 10. `TaxLineMapping` vs `TaxFormLineMap` naming question resurfaces

Stage 02 §3.17 calls it `TaxLineMapping`; reports-schema §3 calls it `TaxFormLineMap`. This hand-off uses `TaxFormLineMap`. If a council reviewer or sibling hand-off references `TaxLineMapping` and points at Stage 02 §3.17, the reports-schema-aligned name wins per this hand-off's authoring decision (reports-schema is the more recent and more specific reference). If the naming is contested by CO or a sibling XO output, file `cob-question-*`.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–5 merged to main** (sequentially).
2. **`packages/blocks-financial-tax/` exists** with: `Models/`, `Services/`, `Migration/`, `Seeds/`, `DependencyInjection/`, `Localization/`, `tests/`, `NOTICE.md`, `README.md`, `.csproj`.
3. **All four entities present:** `TaxCode`, `TaxRate`, `TaxJurisdiction`, `TaxFormLineMap` (with `IsProvisional` discipline).
4. **Tax calculation engine operates per Stage 02 §6.4:** OnSubtotal / Compound / Inclusive all pass the A4 acceptance suite.
5. **Schedule E v1 seed populated and idempotent:** acceptance tests A6 + A7 pass.
6. **Event emission wired:** A8 passes — all five `Financial.*` / `Reports.*` events fire on the expected upserts.
7. **ERPNext importer roundtrip works:** A9 passes.
8. **Tests pass:** ~72–82 tests across the package.
9. **`apps/docs/blocks-financial-tax/overview.md` published** with the property-tax clarification and the v1-provisional caveat.
10. **`active-workstreams.md`** row for W#60 P4 / blocks-financial-tax updated with `built` status + the 5 PR numbers (via the source `W#60.md` file per `feedback_never_add_workstream_rows_directly_to_ledger`).
11. **NOTICE.md attribution present** in the package root.
12. **Cross-cluster event-bus design doc updated** (PR 5 appends the five new events to `_shared/engineering/cross-cluster-event-bus-design.md` §3.1 + §3.5).

When the PASS gate is met, the next hand-offs unblock:

- `blocks-financial-ar-stage06-handoff.md` (Invoice + InvoiceLine; consumes `TaxCode` + `ITaxCalculationService` + `IJournalTaxLineGenerator`).
- `blocks-financial-ap-stage06-handoff.md` (Bill + BillLine; same consumption).
- `blocks-reports-tax-stage06-handoff.md` (Schedule E generator + 1099 generator; consumes `TaxFormLineMap` + `ITaxFormLineMapStore`).
- `blocks-financial-tax-onr-ratification-addendum-stage06-handoff.md` (flips `IsProvisional` and adds ONR-ratified rows; ships once ONR research lands).
- `tooling-anchor-import-stage06-handoff.md` (the migration importer orchestrator — adds tax-import Pass 2 to its pass sequence using `IErpnextTaxImporter`).

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-16):**

- `packages/blocks-financial-ledger/` (consumed for `GLAccountId`, `ChartOfAccountsId`, `IAccountResolver`) ✓ — assumes ledger PR 2 merged; verify via Pre-build checklist step 1
- `Sunfish.Blocks.FinancialLedger.Models.GLAccount` (consumed) ✓
- `Sunfish.Blocks.FinancialLedger.Models.GLAccountId` (consumed) ✓
- `Sunfish.Blocks.FinancialLedger.Models.ChartOfAccountsId` (consumed; ledger PR 2 introduced) ✓
- `Sunfish.Blocks.FinancialLedger.Services.IAccountResolver` (consumed; ledger PR 4 introduced) ✓
- ADR 0088 §1 (Path II + 7-cluster decomposition) ✓
- `coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md` (Decision 2 = `blocks-reports-tax` rename, NOT in scope here) ✓
- `icm/02_architecture/blocks-financial-schema-design.md` §3.12–§3.14, §3.17, §6.4 ✓
- `icm/02_architecture/blocks-reports-schema-design.md` §3 (`TaxFormLineMap`), §8 (Schedule E) ✓
- `_shared/engineering/crdt-friendly-schema-conventions.md` (consumed for §1, §2, §4, §5, §6) ✓
- `_shared/engineering/cross-cluster-event-bus-design.md` (consumed for §1, §2, §3.1, §3.5; PR 5 updates §3.1 + §3.5) ✓
- `_shared/engineering/erpnext-to-anchor-migration-importer-spec.md` (consumed for Pass 2 contract — verify path; if not yet on main, the local ERPNext-source-record shape in this hand-off is the source of truth) — flag-and-verify in Pre-build

**Pending (not in repo at hand-off authoring 2026-05-16):**

- `icm/02_architecture/regulatory-us-rental-tax-input.md` — ONR research output; pending. Hand-off ships v1 provisional seed without it; ratification addendum hand-off ships once it lands.

**Introduced by this hand-off** (across PRs 1–5):

- New package: `packages/blocks-financial-tax/`
- New ID types: `TaxCodeId`, `TaxRateId`, `TaxJurisdictionId`, `TaxFormLineMapId`
- New entities: `TaxJurisdiction`, `TaxCode`, `TaxRate`, `TaxFormLineMap`
- New enums: `TaxKind`, `TaxApplication`, `JurisdictionLevel`, `TaxFormKind`, `TaxCalculationError`, `TaxRateValidationError`
- New services: `ITaxJurisdictionResolver` + `InMemoryTaxJurisdictionResolver`, `ITaxJurisdictionStore` + `InMemoryTaxJurisdictionStore`, `ITaxRateLookup` + `InMemoryTaxRateLookup`, `ITaxCodeStore` + `InMemoryTaxCodeStore`, `ITaxCalculationService` + `TaxCalculationService`, `ITaxFormLineMapStore` + `InMemoryTaxFormLineMapStore`, `IJournalTaxLineGenerator` + `JournalTaxLineGenerator`, `IErpnextTaxImporter` + `ErpnextTaxImporter`, `IFinancialTaxEventEmitter` + `NullFinancialTaxEventEmitter` (placeholder)
- New result/error types: `TaxCalculationResult`, `TaxRateBreakdownLine`, `TaxRateUpsertResult`, `TaxRateSupersedeResult`, `TaxCalculationInput`, `TaxLocationContext`, `JournalTaxLineGenerationResult`, `ErpnextTaxSource`
- New events: `TaxCodeAddedEvent`, `TaxCodeUpdatedEvent`, `TaxRateAddedEvent`, `TaxRateExpiredEvent`, `TaxFormLineMapEditedEvent`
- New seeds: `DefaultTaxFormLineMap.ScheduleE(...)` (v1 provisional, 19 rows)
- New extension methods: `JurisdictionLevelExtensions.OrderIndex`, `TaxFormKindExtensions.ToCanonical`, `TaxCodeRatesExtensions.GetRatesAsync`, `TaxAccountSelector.Matches`
- New event-bus catalog rows in `_shared/engineering/cross-cluster-event-bus-design.md` §3.1 + §3.5 (PR 5)
- Docs: `apps/docs/blocks-financial-tax/overview.md`
- Attribution: `packages/blocks-financial-tax/NOTICE.md`

**Self-audit reminder (per ADR 0028-A10):** COB structurally verifies each cited symbol by reading the actual file before declaring AP-21 clean. Do not rely on grep-only verification — open `packages/blocks-financial-ledger/Models/ChartOfAccountsId.cs` before assuming the type exists in the shape this hand-off expects.

---

## Cohort discipline

This hand-off is the **second Stage 06 hand-off under ADR 0088 Path II** (after `-ledger`) and the **third Phase 1 cluster implementation unit** (after `-ledger` + the still-to-author `-periods`). The COB self-audit pattern from W#34 / W#35 / W#36 / W#39 / W#40 / the ledger hand-off applies verbatim:

- **Two-overload DI extension pattern** for `AddBlocksFinancialTax()` — audit-disabled / audit-enabled both-or-neither. The PR 5 event-emitter wiring is the right place to apply this if `IDomainEventEmitter` lands during the hand-off; otherwise the single-overload form in PR 1 is fine until then.
- **`apps/docs/{cluster}/overview.md`** page convention (PR 5).
- **`README.md`** at the package root referencing Stage 02 design + ADR 0088 (PR 1).
- **`ConcurrentDictionary` for any in-memory cache/store** (already specified for `InMemoryTaxJurisdictionStore`, `InMemoryTaxCodeStore`, `InMemoryTaxRateLookup`, `InMemoryTaxFormLineMapStore`).
- **`NOTICE.md` at package root** (PR 1) — Apache 2.0 attribution.
- **Stable string-code enums per CRDT §5** — applied to all four enums in this hand-off.

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w60-p4-financial-tax-{slug}.md` in `/Users/christopherwood/Projects/SunfishSoftware/coordination/inbox/`.
- Halt the workstream + add a note in `active-workstreams.md` row for W#60 P4 (via the source `W#60.md` file).
- `ScheduleWakeup 1800s`.

If COB completes PR 5 + the PASS gate is met:

- Update `active-workstreams.md` (via the source W*.md file).
- Drop `cob-status-2026-05-XXTHH-MMZ-w60-p4-financial-tax-built.md` to the inbox.
- Continue with the next hand-off in the Phase 1 critical path — typical sequence is `blocks-financial-periods` → `blocks-financial-ar` → `blocks-financial-ap` → `blocks-financial-payments` → `blocks-reports-tax` → ONR-ratification addendum. Check `icm/_state/handoffs/` for the next `ready-to-build` candidate.

---

## Cross-references

- Spec sources:
  - `icm/02_architecture/blocks-financial-schema-design.md` §3.12 (TaxCode), §3.13 (TaxRate), §3.14 (TaxJurisdiction), §3.17 (TaxLineMapping — superseded for naming by reports-schema), §6.4 (calculation algorithm), §11 (license posture).
  - `icm/02_architecture/blocks-reports-schema-design.md` §3 (`TaxFormLineMap` + `TaxAccountSelector`), §8 (Schedule E mapping), Q3 (mutability + audit trail), Q4 (year-over-year carry-forward — deferred).
- ADR: `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md` §1, §2, §3, Appendix B.
- Ratification ruling: `coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md` (Decision 2 = `blocks-tax-reporting → blocks-reports-tax`, NOT in scope here).
- CRDT conventions: `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §2, §3, §4, §5, §6.
- Event bus: `_shared/engineering/cross-cluster-event-bus-design.md` §1 (envelope), §2 (naming), §3.1 + §3.5 (catalog).
- Migration importer: `_shared/engineering/erpnext-to-anchor-migration-importer-spec.md` Pass 2 (tax-codes pass).
- ONR research (pending): `icm/02_architecture/regulatory-us-rental-tax-input.md` (not yet in repo; ratification addendum hand-off ships when it lands).
- Cohort precedent hand-offs:
  - `blocks-financial-ledger-chart-and-journal-stage06-handoff.md` (cluster sibling; format template for this hand-off; consumed dependency)
  - `foundation-mission-space-stage06-handoff.md` (W#40 — DI extension pattern)
  - `foundation-versioning-stage06-handoff.md` (W#34 — substrate naming)
  - `foundation-migration-stage06-handoff.md` (W#35 — substrate sequencing)
- Related Stage 02 design docs:
  - `blocks-financial-schema-design.md` (sibling cluster context; downstream `-ar` / `-ap` / `-payments` consume this hand-off's `TaxCode`)
  - `blocks-people-schema-design.md` (vendor TIN + 1099-eligible flag — gates 1099 reporting in a later hand-off)
  - `blocks-property-schema-design.md` (property `TaxJurisdictionId` linkage — added in a later property-cluster hand-off, NOT here)

---

**End of hand-off.**
