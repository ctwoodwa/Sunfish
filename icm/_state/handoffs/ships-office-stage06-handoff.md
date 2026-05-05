# W#55 Stage 06 Hand-off — Ship's Office Content Aggregation Surface + Scribe Role

**Workstream:** W#55 — Ship's Office Content Aggregation Surface + Scribe Role
**ADR:** [ADR 0083](../../../docs/adrs/0083-ships-office-content-aggregation.md) — Proposed 2026-05-05 via PR #591
**Owner (implementation):** sunfish-PM (COB)
**Status:** `ready-to-build` (pending CO Status: Accepted on ADR 0083; ADR-Status flip is the formal gate)
**Effort estimate:** ~16–22h / 6 phases (Phase 5 deferred-conditional) / ~5–6 PRs
**Pipeline variant:** `sunfish-feature-change`
**W#35 cohort position:** follow-on #7 of 7 — the FINAL W#35 cohort ADR. After W#55 ships built, the cohort is fully closed (W#46/49/50/51/52/54/55 all built).

---

## Hard prerequisites

| # | Prereq | Verify on origin/main | Phase gated |
|---|---|---|---|
| **H1** | W#46 Phase 1 merged — `foundation-ship-common` package + `ShipRole.Scribe` enum value + `ShipLocation.ShipsOffice` + `IPermissionResolver` + `ShipAction` catalog | `ls packages/foundation-ship-common/` non-empty; `grep -l "Scribe" packages/foundation-ship-common/ShipRole.cs` | Phase 1 (`ShipAction` constants) + Phase 2 (`IPermissionResolver` use) |
| **H2** | W#46 Phase 3 merged — `IDiffPreview<TPath, TValue>` + `DiffPreviewView` in `Sunfish.UICore` | `grep -rl "IDiffPreview\|DiffPreviewView" packages/ui-core/` | Phase 2 `IDocumentDiffService` real impl (Phase 1 stub acceptable); Phase 3 `DocumentDiffPanel.razor` |
| **H3** | W#46 Phase 1 also ships `ISearchAsYouType<THit>` in `Sunfish.UICore` | `grep -rl "ISearchAsYouType" packages/ui-core/` | Phase 3 `ShipsOfficeSearchBar.razor` (acceptable stub if W#46 Phase 1 precedes Phase 3) |
| **H4** | ADR 0055 Status: Accepted (DynamicTemplate kind + `IFormSchemaStore`) | `grep "Status: Accepted" docs/adrs/0055-*.md` | Phase 5 ONLY (Phase 5 is conditional; ship Phases 1-4 + 6 if H4 not cleared) |
| **H5** | ADR 0065-A1 `IStandingOrderEventStream` ships | `grep -l "IStandingOrderEventStream" packages/foundation-wayfinder/` | NO HARD GATE — Phase 2 uses polling fallback (`SubscribeChangesAsync` 60s default); revisit-trigger when A1 ships |
| **H6** | ADR 0004 Stage 06 — `ISignatureEnvelopeStore` queryable surface | `grep -l "ISignatureEnvelopeStore" packages/` | NO HARD GATE — Phase 2 ships empty-list stub for `SignatureEnvelope` kind; revisit when store interface ships |

**NuGet binary halt:** This project has NOT shipped a NuGet binary (pre-v1). No binary-compat halt applies. Confirm before every Phase 1 PR: `find packages/ -name "*.nupkg" | wc -l` must be 0.

**Downstream consumers:** No active downstream workstreams depend on `foundation-ships-office` contracts. `IFirstAidSurface` from W#54 (Sick Bay) injection MAY be wired into Ship's Office UI for contextual help (surface key `"ships-office"`); this is a Phase 4 nice-to-have, not a blocker.

---

## Substrate verification (pre-Phase-1)

Run before writing a single line of Phase 1 code, in the worktree off origin/main:

```bash
# 1. Net-new packages — must NOT pre-exist
ls packages/foundation-ships-office/ 2>/dev/null && echo "PRE-EXISTS — halt; coordinate with parallel session" || echo "OK — net-new"
ls packages/blocks-ships-office/ 2>/dev/null     && echo "PRE-EXISTS — halt" || echo "OK — net-new"

# 2. Substrate symbols (must all be PRESENT on origin/main)
grep -l "ILeaseDocumentVersionLog"   packages/blocks-leases/Services/ILeaseDocumentVersionLog.cs   # W#22/W#27
grep -l "interface IW9DocumentService" packages/blocks-maintenance/Services/IW9DocumentService.cs # W#18
grep -l "class W9Document\|record W9Document" packages/blocks-maintenance/Models/W9Document.cs    # W#18
grep -l "SignatureEnvelope"          packages/foundation/Crypto/SignatureEnvelope.cs              # ADR 0021
grep -l "BusinessCaseBundleManifest" packages/foundation-catalog/Bundles/BusinessCaseBundleManifest.cs  # ADR 0007
grep -l "IMissionEnvelopeObserver"   packages/foundation-mission-space/Services/Contracts.cs       # ADR 0062
grep -l "IFieldDecryptor"            packages/foundation-recovery/Crypto/IFieldDecryptor.cs        # ADR 0046-A2
grep -l "IFieldEncryptor"            packages/foundation-recovery/Crypto/IFieldEncryptor.cs        # ADR 0046-A2
grep -l "TenantId"                   packages/foundation/Assets/Common/TenantId.cs                 # foundation
grep -l "ActorId"                    packages/foundation/Assets/Common/ActorId.cs                  # foundation
grep "AuditEventType " packages/kernel-audit/AuditEventType.cs | head -3                           # AuditEventType pattern
grep -l "IAuditTrail"  packages/kernel-audit/IAuditTrail.cs

# 3. H1 / H2 / H3 / H4 / H6 gate state
ls packages/foundation-ship-common/ 2>/dev/null && echo "H1 cleared" || echo "H1 BLOCKED — wait for W#46 P1"
grep -rl "IDiffPreview\b" packages/ui-core/ 2>/dev/null  && echo "H2 cleared" || echo "H2 PENDING — Phase 1 stub OK"
grep -rl "ISearchAsYouType" packages/ui-core/ 2>/dev/null && echo "H3 cleared" || echo "H3 PENDING — Phase 3 stub OK"
grep -l "Status: Accepted" docs/adrs/0055-*.md 2>/dev/null && echo "H4 cleared" || echo "H4 PENDING — defer Phase 5"
grep -rl "ISignatureEnvelopeStore" packages/ 2>/dev/null && echo "H6 cleared" || echo "H6 PENDING — empty-list stub"

# 4. AuditEventType collision sweep (all 6 names MUST be absent)
for n in ShipsOfficeDocumentViewed ShipsOfficeDocumentSearched \
         ShipsOfficeDocumentDiffViewed ShipsOfficeDocumentPublished \
         ShipsOfficeDocumentArchived ShipsOfficePublishRejected ; do
  grep -q "$n" packages/kernel-audit/ && echo "COLLISION: $n" || true
done

# 5. ShipAction collision sweep (4 names MUST be absent — assumes H1 cleared)
for n in ViewShipsOffice EditShipsOfficeDocument PublishShipsOfficeDocument ArchiveShipsOfficeDocument ; do
  grep -rq "$n" packages/foundation-ship-common/ 2>/dev/null && echo "COLLISION: $n" || true
done
```

If any check fails unexpectedly, stop and write a `cob-question-*.md` to `icm/_state/research-inbox/`.

---

## Phase 1 — `foundation-ships-office` substrate (contracts + data model)

**Effort:** ~3–4h | **PR:** 1 | **Review:** pre-merge council mandatory (standard 4-perspective adversarial)

**Gate:** H1 (W#46 Phase 1 on origin/main) is required for `ShipAction` constants. If H1 not cleared at Phase 1 start, COB has two options: (a) defer Phase 1 until W#46 P1 lands, OR (b) ship Phase 1 in two slices — slice-A = data model + interfaces (no `ShipAction` references; `IPermissionResolver` is a constructor dep but not yet invoked), slice-B = `ShipAction` constants once H1 clears. Document the choice in PR description.

### 1.1 Project file

`packages/foundation-ships-office/Sunfish.Foundation.ShipsOffice.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Sunfish.Foundation.ShipsOffice</RootNamespace>
    <AssemblyName>Sunfish.Foundation.ShipsOffice</AssemblyName>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NodaTime" Version="$(NodaTimeVersion)" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="$(MicrosoftExtensionsOptionsVersion)" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions"
                      Version="$(MicrosoftExtensionsDependencyInjectionAbstractionsVersion)" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
    <ProjectReference Include="..\kernel-audit\Sunfish.Kernel.Audit.csproj" />
    <ProjectReference Include="..\foundation-catalog\Sunfish.Foundation.Catalog.csproj" />
    <!-- foundation-ship-common added in slice-B once H1 clears -->
    <ProjectReference Include="..\foundation-ship-common\Sunfish.Foundation.Ship.Common.csproj" />
  </ItemGroup>
</Project>
```

**Architecture rule check (per ADR 0083 §3 tier discipline):** `foundation-ships-office` MUST NOT depend on `Sunfish.UICore`. The `IDocumentDiffService` interface (which returns `DiffPreviewView` from `Sunfish.UICore`) is declared in `blocks-ships-office`, NEVER in `foundation-ships-office`. This is a B-1 council finding from the ADR 0083 pre-merge council; treat it as a HARD architectural rule.

`foundation-ships-office` MUST NOT depend on `blocks-leases` or `blocks-maintenance` either — those dependencies live in `blocks-ships-office` (the data provider implementation). Foundation contracts must stay tier-clean.

### 1.2 Data model types (per ADR 0083 §1)

Create the following files in `packages/foundation-ships-office/`. All in namespace `Sunfish.Foundation.ShipsOffice`. Cite ADR 0083 §1 in file headers; record-types use `required` keyword consistently.

- **`ShipsOfficeDocumentId.cs`** — `public readonly record struct ShipsOfficeDocumentId(string Value);` Opaque; format internal to `IShipsOfficeDataProvider` impls. XML doc: "Do not parse or construct outside of `IShipsOfficeDataProvider` implementations."
- **`ShipsOfficeDocumentKind.cs`** — enum: `BundleManifest, LeaseDocument, VendorW9, SignatureEnvelope`. Note: `DynamicTemplate` is Phase 5 (gated on H4); ship Phase 1 WITHOUT this value.
- **`DocumentStatus.cs`** — enum: `Draft, Published, Archived, PendingSignature`.
- **`ShipsOfficeDocumentView.cs`** — record with `Id`, `Kind`, `Title`, `Status`, `UpdatedAt` (NodaTime.Instant), `LastModifiedBy` (`ActorId`), `VersionLabel` (string?). XML doc on the `Title` field MUST cite §Trust impact: "For `Kind == VendorW9`, `Title` is the vendor display name; the W9 TIN field is excluded from this view."
- **`ShipsOfficeSnapshot.cs`** — record with `Documents` (IReadOnlyList<ShipsOfficeDocumentView>), `TotalCount` (int), `AsOf` (NodaTime.Instant).
- **`ShipsOfficeSearchQuery.cs`** — record with `TextQuery` (string?), `KindFilter` (IReadOnlyList<ShipsOfficeDocumentKind>?), `StatusFilter` (DocumentStatus?), `PageSize` (int; default 50), `PageToken` (string?). Per ADR 0083 §1.

### 1.3 Provider + command + editor interfaces (per ADR 0083 §2 + §3)

- **`IShipsOfficeDataProvider.cs`** — three methods per §2:
  - `Task<ShipsOfficeSnapshot> GetSnapshotAsync(TenantId tenant, CancellationToken ct = default)`
  - `IAsyncEnumerable<ShipsOfficeDocumentView> SearchAsync(TenantId tenant, ShipsOfficeSearchQuery query, CancellationToken ct)`
  - `IAsyncEnumerable<ShipsOfficeDocumentView> SubscribeChangesAsync(TenantId tenant, CancellationToken ct)`
  - **XML doc MUST include the §2 caller-contract verbatim:** "CALLER CONTRACT: callers MUST verify `ShipAction.ViewShipsOffice` via `IPermissionResolver` before calling this method. The data provider does not re-verify role — it is the caller's (UI block's) responsibility to gate access. This contract is enforced by Roslyn analyzer SUNFISH_SHIPSOFFICE_PERM001 (Phase 2)."
  - **XML doc MUST also FORBID `IFieldDecryptor`:** "Implementations MUST NOT call `IFieldDecryptor` anywhere in the implementation chain — `W9Document` TIN is always redacted in browse view (§Trust impact)."
  - Recommended completion: ≤2s per `GetSnapshotAsync`; partial-snapshot-on-timeout posture.

- **`IShipsOfficeCommandService.cs`** — two methods per §2:
  - `Task PublishAsync(TenantId tenant, ShipsOfficeDocumentId id, CancellationToken ct = default)` — XML doc: requires `ShipAction.PublishShipsOfficeDocument` + XO+; audit-emission ordering per §5 (permission FIRST → audit pre-op → execute; on rejection emit `ShipsOfficePublishRejected`).
  - `Task ArchiveAsync(TenantId tenant, ShipsOfficeDocumentId id, CancellationToken ct = default)` — XML doc: requires `ShipAction.ArchiveShipsOfficeDocument` + XO+; on rejection THROW (no audit event for ArchiveAsync rejection per §5 informational-only path).

- **`IContentEditorSurface.cs`** — adapter contract per §3:
  - `Task<ContentEditorResult> EditAsync(TenantId tenant, ShipsOfficeDocumentId id, CancellationToken ct = default)`
  - XML doc: framework-specific editors (Blazor / React / MAUI) implement in `ui-adapters-blazor` / `ui-adapters-react`; consumers depend on this interface. Phase 1 stub (read-only); Phase 2 full markdown editor (deferred per Open Q2).

- **`ContentEditorResult.cs`** — record with `WasSaved` (bool), `NewVersionLabel` (string? — non-null when WasSaved=true and document kind supports versioning).

**`IDocumentDiffService` is NOT declared in `foundation-ships-office`** (per §3 tier-discipline note + B-1 council finding). It lives in `blocks-ships-office` — Phase 2 deliverable.

### 1.4 AuditEventType constants (per ADR 0083 §6)

In `packages/kernel-audit/AuditEventType.cs`, add 6 constants in a `// ── Ship's Office (ADR 0083) ──────────────` block. Pre-add grep MUST return zero hits per pre-flight collision sweep:

```csharp
// ── Ship's Office (ADR 0083) ────────────────────────────────────────────────
public static readonly AuditEventType ShipsOfficeDocumentViewed     = new("ships-office.doc.viewed");
public static readonly AuditEventType ShipsOfficeDocumentSearched   = new("ships-office.doc.searched");
public static readonly AuditEventType ShipsOfficeDocumentDiffViewed = new("ships-office.doc.diff-viewed");
public static readonly AuditEventType ShipsOfficeDocumentPublished  = new("ships-office.doc.published");
public static readonly AuditEventType ShipsOfficeDocumentArchived   = new("ships-office.doc.archived");
public static readonly AuditEventType ShipsOfficePublishRejected    = new("ships-office.doc.publish-rejected");
```

**Emission semantics (per ADR 0083 §6 verbatim):**
- `ShipsOfficeDocumentSearched`: fires on every `SearchAsync` call returning ≥1 result; SUPPRESSED for zero-result background polls (audit-noise control).
- `ShipsOfficePublishRejected`: fires when `PublishAsync` is denied by `IPermissionResolver` (XO+ check fails). NOT a generic "denial" event — Ship's-Office-specific.
- `ShipsOfficeDocumentViewed`: fires on first view per document-per-session (one event per open action, NOT per render — implementation MUST track per-session view state).

### 1.5 ShipAction additions (per ADR 0083 §4; gated on H1)

In `packages/foundation-ship-common/ShipAction.cs` (or wherever W#46 P1 places the catalog), add 4 `ShipAction` constants:

```csharp
// ── Ship's Office (ADR 0083) ────────────────────────────────────────────────
public static readonly ShipAction ViewShipsOffice            = new("view-ships-office");
public static readonly ShipAction EditShipsOfficeDocument    = new("edit-ships-office-doc");
public static readonly ShipAction PublishShipsOfficeDocument = new("publish-ships-office-doc");
public static readonly ShipAction ArchiveShipsOfficeDocument = new("archive-ships-office-doc");
```

Permission rules in `DefaultPermissionResolver` per §4 table:

| Action | Minimum role | Minimum deck |
|---|---|---|
| `ViewShipsOffice` | `Scribe` | `TopDeck` |
| `EditShipsOfficeDocument` | `Scribe` | `TopDeck` |
| `PublishShipsOfficeDocument` | `XO` | `MainDeck` |
| `ArchiveShipsOfficeDocument` | `XO` | `MainDeck` |

**`ShipRole.Scribe` and `ShipLocation.ShipsOffice` are introduced by ADR 0077 / W#46.** Verify their presence at Phase 1 start; if either is missing, slice-B the `ShipAction` constants until H1 clears.

### 1.6 DI registration (per ADR 0083 §7)

`packages/foundation-ships-office/ShipsOfficeServiceCollectionExtensions.cs`:

```csharp
public sealed class ShipsOfficeOptions
{
    public TimeSpan FallbackPollingInterval { get; set; } = TimeSpan.FromSeconds(60);
    public int       SnapshotPageSize        { get; set; } = 500;
    /// <summary>Phase 2 opt-in for regulated-industry tenants. Default false; A1 amendment governs activation.</summary>
    public bool      RequireSecondActorPublish { get; set; } = false;
}

public static class ShipsOfficeServiceCollectionExtensions
{
    public static IServiceCollection AddSunfishShipsOffice(
        this IServiceCollection services,
        Action<ShipsOfficeOptions>? configure = null)
    {
        services.AddOptions<ShipsOfficeOptions>().Configure(opts => configure?.Invoke(opts));

        // Phase 1 ships interface registrations only — implementations land in Phase 2.
        // services.TryAddSingleton<IShipsOfficeDataProvider, ShipsOfficeDataProvider>(); // Phase 2
        // services.TryAddSingleton<IShipsOfficeCommandService, ShipsOfficeCommandService>(); // Phase 2
        // services.TryAddSingleton<IContentEditorSurface, NoopContentEditorSurface>();    // Phase 2 (read-only stub)

        return services;
    }
}
```

### 1.7 Phase 1 tests

`packages/foundation-ships-office/tests/Sunfish.Foundation.ShipsOffice.Tests.csproj` (new project; same xUnit + NSubstitute versioning as cohort siblings).

```
ContractSurfaceTests:
  [Fact] IShipsOfficeDataProvider_has_required_members
  [Fact] IShipsOfficeCommandService_has_required_members
  [Fact] IContentEditorSurface_has_required_members
  [Fact] DataProvider_xml_doc_cites_caller_contract_for_ViewShipsOffice
  [Fact] DataProvider_xml_doc_cites_IFieldDecryptor_prohibition

DataModelTests:
  [Fact] ShipsOfficeDocumentKind_has_four_values_DynamicTemplate_absent_in_phase_1
  [Fact] DocumentStatus_has_four_values
  [Fact] ShipsOfficeSearchQuery_default_PageSize_is_50
  [Fact] ContentEditorResult_WasSaved_false_path

OptionsTests:
  [Fact] ShipsOfficeOptions_defaults_FallbackPollingInterval_60s
  [Fact] ShipsOfficeOptions_defaults_SnapshotPageSize_500
  [Fact] ShipsOfficeOptions_defaults_RequireSecondActorPublish_false

AuditEventTypeConstantsTests:
  [Fact] All_six_ShipsOffice_constants_present_and_kebab_case
```

### 1.8 Phase 1 halt conditions

| Halt | Action |
|---|---|
| **H1.A** — `foundation-ship-common` not on origin/main at Phase 1 start | Slice-A / slice-B split as described above; document choice in PR; file `cob-question-*.md` if deferring slice-B. |
| **H1.B** — `AuditEventType` collision found in collision sweep | STOP. Do NOT add duplicate constant. File `cob-question-*.md`. |
| **H1.C** — `ShipAction` collision (e.g., a sibling W# already added `ViewShipsOffice`) | STOP. Re-coordinate constant names. |
| **H1.D** — `foundation-ships-office` references `Sunfish.UICore` accidentally | STOP. Per B-1 council rule: foundation tier MUST NOT depend on ui-core. Move `IDocumentDiffService` to `blocks-ships-office` (Phase 2 deliverable). |

**Pre-merge council:** standard 4-perspective adversarial.

---

## Phase 2 — Reference implementation + permission gating + analyzer

**Effort:** ~5–6h | **PR:** 1 | **Review:** **security-engineering subagent MANDATORY** (per ADR 0083 §Trust + §Implementation checklist Phase 2)

**Gate:** Phase 1 merged. Cross-cutting deps: `ILeaseDocumentVersionLog` (W#22/W#27 — built ✓), `IW9DocumentService` (W#18 — built ✓), `BundleCatalog` (ADR 0007 — built ✓), `IMissionEnvelopeObserver` (ADR 0062 — built ✓).

### 2.1 `ShipsOfficeDataProvider : IShipsOfficeDataProvider`

Location: `packages/blocks-ships-office/ShipsOfficeDataProvider.cs` (UI block tier — depends on consumer-tier packages).

Per ADR 0083 §Implementation checklist Phase 2:

- **`BundleManifest`**: enumerate `BundleCatalog.GetAllAsync(tenant)` → map to `ShipsOfficeDocumentView` with `Title = manifest.DisplayName` (or equivalent), `Kind = BundleManifest`, `Status = manifest.BundleStatus → DocumentStatus` (map per ADR 0007 lifecycle). Verify exact API on origin/main; mirror.
- **`LeaseDocument`**: enumerate `ILeaseDocumentVersionLog.ListAsync` → take latest version per lease → map. `VersionLabel = "v{N}"`.
- **`VendorW9`**: enumerate `IW9DocumentService.GetAsync` (or list-equivalent) — NEVER call `GetWithDecryptedTinAsync`. `Title = vendor display name`; the redacted TIN field is EXCLUDED from `ShipsOfficeDocumentView` entirely (the view record has no TIN field). H4 reflection test enforces.
- **`SignatureEnvelope`**: H6 — no queryable store on origin/main yet. Phase 2 returns EMPTY LIST for this kind (forward-compatible; revisit when ADR 0004 Stage 06 ships).
- **`SearchAsync`**: in-memory linear scan over the `GetSnapshotAsync` projection (acceptable for ≤500 documents per `SnapshotPageSize`). Honor `KindFilter` + `StatusFilter` + `TextQuery` (case-insensitive substring match on `Title`). Pagination via `PageToken` (opaque continuation token; in-memory implementation can encode the offset).
- **`SubscribeChangesAsync`**: subscribe to `IMissionEnvelopeObserver` for envelope changes (proxy for "something changed somewhere"); poll `ShipsOfficeOptions.FallbackPollingInterval` for document-change detection (60s default). H5 revisit-trigger when `IStandingOrderEventStream` ships; remove polling fallback at that point.
- **FORBIDDEN**: NO `IFieldDecryptor` reference anywhere in the class graph. Verified by reflection test:

  ```csharp
  [Fact]
  public void ShipsOfficeDataProvider_DoesNotReference_IFieldDecryptor()
  {
      // Mirror the cohort-precedent reflection test pattern (W#52 Tactical / W#54 Sick Bay).
      // Walk SickBayDataProvider's compiled IL OR scan transitive ProjectReference graph.
      // Coordinate with security-engineering subagent on which utility to reuse.
  }
  ```

### 2.2 `ShipsOfficeCommandService : IShipsOfficeCommandService`

`PublishAsync` ordering per §5 audit-emission ordering rule (B-2 council finding from ADR 0083 pre-merge council):

```text
1. Resolve current actor + verify TenantId scope
2. Call IPermissionResolver.AuthorizeAsync(actor, ShipAction.PublishShipsOfficeDocument, ...)
3a. PASS: emit ShipsOfficeDocumentPublished pre-op  → execute state change → return
3b. FAIL: emit ShipsOfficePublishRejected           → throw UnauthorizedAccessException (no state change)
```

Rejected events MUST be auditable. NO phantom "success" events for rejected attempts.

`ArchiveAsync` ordering (no rejected-event audit per §5 informational-only path):

```text
1. Resolve current actor + verify TenantId scope
2. Call IPermissionResolver.AuthorizeAsync(actor, ShipAction.ArchiveShipsOfficeDocument, ...)
3a. PASS: emit ShipsOfficeDocumentArchived pre-op → execute state change → return
3b. FAIL: throw UnauthorizedAccessException (no audit event)
```

`RequireSecondActorPublish` (Phase 2 opt-in flag from §7):
- When `ShipsOfficeOptions.RequireSecondActorPublish == true` AND `document.LastModifiedBy == currentActor`: emit `ShipsOfficePublishRejected` + throw `InvalidOperationException("Self-publish rejected: RequireSecondActorPublish enabled.")`.
- When `RequireSecondActorPublish == false` (default): self-publish allowed (per Open Q4 deferral; A1 amendment in Phase 5 will revisit).

### 2.3 `IDocumentDiffService` declaration + stub impl (in `blocks-ships-office`)

Per ADR 0083 §3 tier-discipline rule: `IDocumentDiffService` is declared in `blocks-ships-office` (NOT `foundation-ships-office`) because it returns `DiffPreviewView` from `Sunfish.UICore`.

```csharp
namespace Sunfish.Blocks.ShipsOffice;

using Sunfish.UICore;          // DiffPreviewView (W#46 Phase 3 — H2)
using Sunfish.Foundation.ShipsOffice;
using Sunfish.Foundation.Assets.Common;

/// <summary>
/// Computes an accessible diff between two versions of a document.
/// Composes IDiffPreview&lt;string, string&gt; from ADR 0077 §6.4.
/// Phase 2 stub returns DiffPreviewView with AccessibleRows = ["Diff not yet available"]
/// when H2 (DiffPreviewView in Sunfish.UICore) has not cleared.
/// </summary>
public interface IDocumentDiffService
{
    Task<DiffPreviewView> ComputeDiffAsync(
        TenantId tenant,
        ShipsOfficeDocumentId baseId,
        ShipsOfficeDocumentId compareId,
        CancellationToken ct = default);
}

public sealed class DocumentDiffService : IDocumentDiffService
{
    public Task<DiffPreviewView> ComputeDiffAsync(
        TenantId tenant, ShipsOfficeDocumentId baseId, ShipsOfficeDocumentId compareId,
        CancellationToken ct = default)
    {
        // Phase 2 stub. Real diff computation deferred until H2 clears AND Phase 3 Blazor
        // diff panel is wired. Throw ArgumentException if the two documents are different
        // ShipsOfficeDocumentKind values (per ADR 0083 §3 cross-type-diff prohibition).
        var rows = new[] { "Diff not yet available" };  // IReadOnlyList<string>
        return Task.FromResult(new DiffPreviewView(rows));
    }
}
```

If H2 has cleared at Phase 2 build time, ship a real implementation that produces structural-metadata-only diffs (field path / old-value-summary / new-value-summary; NEVER raw document content per §5 redaction policy).

### 2.4 `SUNFISH_SHIPSOFFICE_PERM001` Roslyn analyzer

Per ADR 0083 §2 + Phase 2 checklist + W#48 SUNFISH_INTEGRATION_AUDIT001 cohort precedent.

Location: `packages/foundation-ships-office.analyzers/Sunfish.Foundation.ShipsOffice.Analyzers.csproj` (separate analyzer project; cohort precedent from W#48).

Diagnostic ID: `SUNFISH_SHIPSOFFICE_PERM001`
Severity: `Warning` (not Error — cohort default for analyzer findings unless escalated by council)
Title: "Ship's Office data-provider call lacks preceding permission check"
Description: warns on any call to `IShipsOfficeDataProvider.GetSnapshotAsync` or `IShipsOfficeDataProvider.SearchAsync` not preceded by a verifiable `IPermissionResolver.AuthorizeAsync(ShipAction.ViewShipsOffice, ...)` call site (per §2 caller-contract).

Test fixtures: positive (call-site WITH permission check) + negative (call-site WITHOUT permission check; analyzer flags). Mirror the W#48 SUNFISH_INTEGRATION_AUDIT001 test harness exactly.

### 2.5 `NoopContentEditorSurface : IContentEditorSurface`

Phase 2 read-only stub (per ADR 0083 §3 + Open Q2):

```csharp
public sealed class NoopContentEditorSurface : IContentEditorSurface
{
    public Task<ContentEditorResult> EditAsync(TenantId tenant, ShipsOfficeDocumentId id, CancellationToken ct = default)
        => Task.FromResult(new ContentEditorResult { WasSaved = false, NewVersionLabel = null });
}
```

Phase 2 implementation NOT YET ready; full markdown editor adapter wiring deferred to Phase 5 (or a follow-on workstream per Open Q2).

### 2.6 DI extension finalization

```csharp
services.TryAddSingleton<IShipsOfficeDataProvider, ShipsOfficeDataProvider>();
services.TryAddSingleton<IShipsOfficeCommandService, ShipsOfficeCommandService>();
services.TryAddSingleton<IContentEditorSurface, NoopContentEditorSurface>();
services.TryAddSingleton<IDocumentDiffService, DocumentDiffService>();
```

### 2.7 Phase 2 tests

```
ShipsOfficeDataProviderTests:
  [Fact] DoesNotReference_IFieldDecryptor                                  (H4 reflection test)
  [Fact] BundleManifest_kind_enumerates_BundleCatalog
  [Fact] LeaseDocument_kind_returns_latest_version_per_lease
  [Fact] VendorW9_kind_excludes_TIN_from_DocumentView
  [Fact] SignatureEnvelope_kind_returns_empty_list_phase_2_stub      (H6 forward-compat)
  [Fact] SearchAsync_filters_by_KindFilter
  [Fact] SearchAsync_filters_by_StatusFilter
  [Fact] SearchAsync_paginates_via_PageToken
  [Fact] SearchAsync_emits_ShipsOfficeDocumentSearched_when_results_nonempty
  [Fact] SearchAsync_does_not_emit_audit_when_zero_results

ShipsOfficeCommandServiceTests:
  [Fact] PublishAsync_permission_check_first_then_audit_pre_op
  [Fact] PublishAsync_emits_ShipsOfficePublishRejected_on_denial
  [Fact] PublishAsync_throws_UnauthorizedAccessException_on_denial
  [Fact] PublishAsync_no_state_change_on_denial
  [Fact] ArchiveAsync_no_audit_on_denial
  [Fact] ArchiveAsync_emits_ShipsOfficeDocumentArchived_pre_op_on_pass
  [Fact] PublishAsync_self_publish_rejected_when_RequireSecondActorPublish_true
  [Fact] PublishAsync_self_publish_allowed_when_RequireSecondActorPublish_false   (default)

DocumentDiffServiceTests:                          (only if H2 cleared)
  [Fact] ComputeDiffAsync_throws_ArgumentException_when_kinds_differ
  [Fact] ComputeDiffAsync_returns_AccessibleRows_with_field_path_only
  [Fact] ComputeDiffAsync_does_not_embed_raw_document_content_in_AccessibleRows

DocumentDiffServiceStubTests:                      (when H2 NOT cleared)
  [Fact] ComputeDiffAsync_returns_diff_not_yet_available_placeholder

SunfishShipsOfficePerm001Tests:                    (analyzer)
  [Fact] Warns_on_GetSnapshotAsync_without_permission_check
  [Fact] Warns_on_SearchAsync_without_permission_check
  [Fact] Does_not_warn_when_AuthorizeAsync_precedes_call
```

### 2.8 Phase 2 halt conditions

| Halt | Action |
|---|---|
| **H4** — security council finds `IFieldDecryptor` referenced in `ShipsOfficeDataProvider` (transitively) | STOP. Per §Trust impact: redesign. Mandatory pre-merge clearance. |
| **H6** — `ISignatureEnvelopeStore` exists on origin/main now | If H6 cleared: implement real `SignatureEnvelope` enumeration; remove empty-list stub; document the wire-up. If still absent: keep stub; document in PR. |
| **H2.A** — `DiffPreviewView` API differs from documented shape | Verify against origin/main `Sunfish.UICore`; mirror exactly. |
| **H2.B** — Roslyn analyzer false-positives on legitimate code | Tune the analyzer rule (per W#48 SUNFISH_INTEGRATION_AUDIT001 precedent); coordinate with security-engineering subagent. |

**Pre-merge council:** standard + **security-engineering MANDATORY** (IFieldDecryptor prohibition + audit-before-operation + permission-gate ordering + analyzer correctness).

---

## Phase 3 — `blocks-ships-office` Blazor UI

**Effort:** ~5–6h | **PR:** 1 | **Review:** **WCAG/a11y subagent MANDATORY** (per ADR 0083 §8 + Phase 3 checklist)

**Gate:** Phase 2 merged. H2 (`DiffPreviewView`) gates `DocumentDiffPanel.razor`; if H2 not yet cleared, defer the diff panel to a slice-B follow-up. H3 (`ISearchAsYouType`) gates `ShipsOfficeSearchBar.razor`; same slice-B option if not cleared.

Additional gate: W#46 Phase 3 (`ILiveAnnouncer`, `IFocusTrap`) — required for `aria-live` regions and the Permission-rejected publish-denial assertive announcement.

### 3.1 Project file

`packages/blocks-ships-office/Sunfish.Blocks.ShipsOffice.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Sunfish.Blocks.ShipsOffice</RootNamespace>
    <AssemblyName>Sunfish.Blocks.ShipsOffice</AssemblyName>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation-ships-office\Sunfish.Foundation.ShipsOffice.csproj" />
    <ProjectReference Include="..\foundation-ship-common\Sunfish.Foundation.Ship.Common.csproj" />
    <ProjectReference Include="..\ui-core\Sunfish.UICore.csproj" />
    <ProjectReference Include="..\ui-adapters-blazor\Sunfish.UIAdapters.Blazor.csproj" />
    <ProjectReference Include="..\blocks-leases\Sunfish.Blocks.Leases.csproj" />
    <ProjectReference Include="..\blocks-maintenance\Sunfish.Blocks.Maintenance.csproj" />
    <ProjectReference Include="..\foundation-catalog\Sunfish.Foundation.Catalog.csproj" />
  </ItemGroup>
</Project>
```

### 3.2 Components (per ADR 0083 §8 + Phase 3 checklist)

- **`ShipsOfficeBlock.razor`** — root composition: search bar (`ShipsOfficeSearchBar`) + kind-filter chips + document list (`DocumentList`) + selected-document detail drawer (`DocumentDetailDrawer`). Permission gating: `IPermissionResolver` check for `ViewShipsOffice` BEFORE rendering the block content (matches the §2 caller-contract).
- **`DocumentListItem.razor`** — per-row presentation of `ShipsOfficeDocumentView`. Kind badge: icon + text dual-encoding (SC 1.4.1). Status badge: same dual-encoding. Updated-at + LastModifiedBy. Keyboard-operable row (Enter = open detail drawer). SC 2.4.3 deterministic focus order.
- **`DocumentDetailDrawer.razor`** — slide-in panel; document metadata; action buttons gated by role (`PublishShipsOfficeDocument` button hidden when actor lacks XO+ permission; same for `ArchiveShipsOfficeDocument`). Read-only content view in v1 (Phase 5 will add edit mode via `IContentEditorSurface`).
- **`DocumentDiffPanel.razor`** (gated on H2) — diff table with `<caption>`; columns `Path | Prior | New` per ADR 0077 §6.4 `DiffPreviewView.AccessibleRows`. Added/removed rows labeled "Added:" / "Removed:" in text (SC 1.3.3). Color + text dual-encoding (SC 1.4.1).
- **`ShipsOfficeSearchBar.razor`** (gated on H3) — ARIA APG combobox pattern: `role="combobox"` + `aria-expanded` + `aria-activedescendant`. Result count announced via `aria-live="polite"` region (SC 4.1.3).

### 3.3 Live regions (per ADR 0083 §8 SC 4.1.3)

- Search result count: `aria-live="polite"` (user-initiated; expected outcome).
- Operation confirmations (Published / Archived): `aria-live="polite"` (user-initiated).
- Permission-rejected publish denial: `aria-live="assertive"` (unexpected outcome mid-task per ADR 0077 §3 convention; ADR 0083 §8 SC 4.1.3 row).

### 3.4 Phase 3 tests

```
ShipsOfficeBlockTests:
  [Fact] Block_renders_only_when_actor_has_ViewShipsOffice
  [Fact] Block_invokes_IPermissionResolver_before_calling_DataProvider
  [Fact] Publish_button_hidden_when_actor_lacks_PublishShipsOfficeDocument
  [Fact] Archive_button_hidden_when_actor_lacks_ArchiveShipsOfficeDocument

DocumentListItemTests:
  [Fact] Kind_badge_uses_icon_and_text_dual_encoding
  [Fact] Status_badge_uses_color_and_text_dual_encoding
  [Fact] Row_is_keyboard_operable_with_Enter
  [Fact] Tab_focus_order_is_deterministic

DocumentDiffPanelTests:                    (only if H2 cleared)
  [Fact] Diff_table_has_caption
  [Fact] Added_rows_labeled_Added_in_text                 (SC 1.3.3)
  [Fact] Removed_rows_labeled_Removed_in_text             (SC 1.3.3)
  [Fact] Diff_uses_color_and_text_dual_encoding           (SC 1.4.1)

ShipsOfficeSearchBarTests:                 (only if H3 cleared)
  [Fact] Combobox_has_role_aria_expanded_aria_activedescendant
  [Fact] Result_count_announced_via_aria_live_polite

LiveRegionTests:
  [Fact] Publish_confirmation_announces_via_aria_live_polite
  [Fact] Permission_rejected_announces_via_aria_live_assertive
```

### 3.5 Phase 3 halt conditions

| Halt | Action |
|---|---|
| **H2** — `DiffPreviewView` not on origin/main | Defer `DocumentDiffPanel.razor` to slice-B follow-up. |
| **H3** — `ISearchAsYouType` not on origin/main | Defer `ShipsOfficeSearchBar.razor` to slice-B; render a basic `<input type="search">` with a custom polite live region as fallback. |
| **H3.A** — W#46 Phase 3 (`ILiveAnnouncer` + `IFocusTrap`) not on origin/main | Defer live-region wiring to a Phase 3 addendum after W#46 P3 lands. |
| **H3.B** — WCAG/a11y subagent finds SC 1.3.3 / SC 1.4.1 / SC 1.4.4 / SC 1.4.12 / SC 4.1.3 violation | Apply fix in same PR; do NOT defer (long-form reading + diff are first-class Scribe workflows). |

**Pre-merge council:** standard 4-perspective + **WCAG/a11y MANDATORY** (12 SCs from ADR 0083 §8: SC 1.3.1, 1.3.3, 1.4.1, 1.4.3, 1.4.4, 1.4.12, 2.1.1, 2.4.3, 2.4.5, 2.4.6, 3.3.1, 4.1.3 — note SC 1.4.12 is WCAG 2.1 carried forward into the WCAG 2.2 baseline per B-3 council finding).

---

## Phase 4 — Anchor + Bridge wiring + apps/docs

**Effort:** ~3–4h | **PR:** 1 | **Review:** standard council; WCAG/a11y subagent recommended (apps/docs accessibility).

**Gate:** Phase 3 merged.

### 4.1 Anchor wiring

In `accelerators/anchor/MauiProgram.cs`:

```csharp
builder.Services.AddSunfishShipsOffice(opts =>
{
    opts.SnapshotPageSize         = 200;
    opts.FallbackPollingInterval  = TimeSpan.FromSeconds(60);
    opts.RequireSecondActorPublish = false;  // Phase 1 default; A1 amendment for regulated tenants
});
```

Add a Ship's Office tab/page in the Anchor demo shell.

### 4.2 Bridge wiring

In `accelerators/bridge/Program.cs`:

```csharp
services.AddSunfishShipsOffice(opts =>
{
    opts.SnapshotPageSize         = 500;          // Bridge multi-tenant scale
    opts.RequireSecondActorPublish = true;        // Bridge admin tenant: regulated default
});
```

If Bridge React Ship's Office UI is deferred, document and skip the React-side rendering.

### 4.3 apps/docs

- `apps/docs/blocks/ships-office/overview.md` — block consumer documentation.
- `apps/docs/foundation/ships-office/overview.md` — contract reference.
- `apps/docs/design-system/ships-office-wcag.md` — WCAG 2.2 AA declaration listing all 12 SCs from ADR 0083 §8 (call out SC 1.4.4 + SC 1.4.12 explicitly per W#35 §9.5 long-form reading mandate).

### 4.4 Phase 4 tests

```
AnchorShipsOfficeIntegrationTests:
  [Fact] Anchor_DI_resolves_IShipsOfficeDataProvider
  [Fact] Anchor_DI_resolves_IShipsOfficeCommandService
  [Fact] Ships_Office_demo_page_renders_without_throwing
```

**Pre-merge council:** standard 4-perspective.

---

## Phase 5 — DynamicTemplate kind + ADR 0055 integration (CONDITIONAL — gated on H4)

**Effort:** ~2–3h | **PR:** 1 | **Review:** standard 4-perspective

**Gate:** H4 — ADR 0055 Status: Accepted.

**If H4 NOT cleared at the time Phase 4 ships:** SKIP Phase 5 entirely; advance directly to Phase 6 (ledger flip + close). Phase 5 becomes a deferred follow-on workstream when ADR 0055 reaches Accepted status. This is the canonical pattern per ADR 0083 Open Q1 and Phase 5 conditional gating.

### 5.1 Add `ShipsOfficeDocumentKind.DynamicTemplate`

Uncomment the Phase 2 addition in `ShipsOfficeDocumentKind.cs`:

```csharp
public enum ShipsOfficeDocumentKind
{
    BundleManifest,
    LeaseDocument,
    VendorW9,
    SignatureEnvelope,
    DynamicTemplate,        // ADR 0055 — added in Phase 5 (gated on H4)
}
```

### 5.2 Wire `IFormSchemaStore`

In `ShipsOfficeDataProvider`, add a branch for the `DynamicTemplate` kind that enumerates from `IFormSchemaStore` (ADR 0055 type). Verify exact API on origin/main when H4 clears; mirror.

### 5.3 Wire full `IContentEditorSurface` implementation

Replace `NoopContentEditorSurface` with an adapter-specific markdown editor. Composes `IFormControlContract` from ADR 0077 §6 (W#46 component primitives).

### 5.4 Phase 5 tests

```
ShipsOfficeDataProviderDynamicTemplateTests:
  [Fact] DynamicTemplate_kind_enumerates_IFormSchemaStore
  [Fact] DynamicTemplate_view_has_no_TIN_or_PII

IContentEditorSurfaceTests:
  [Fact] EditAsync_returns_WasSaved_true_when_user_saves
  [Fact] EditAsync_returns_NewVersionLabel_for_versioned_kinds
```

### 5.5 Phase 5 halt conditions

| Halt | Action |
|---|---|
| **H4 not cleared** | SKIP Phase 5 entirely; advance to Phase 6. File a follow-on workstream when ADR 0055 reaches Accepted. |
| **`IFormSchemaStore` API differs from documented shape** | Verify against origin/main; mirror exactly. |

---

## Phase 6 — Ledger flip + memory + close

**Effort:** ~30 min | **PR:** 1 (or rolled into Phase 4/5 if scope is small enough)

### 6.1 Ledger flip

Edit `icm/_state/workstreams/W55-ships-office-content-aggregation-surface.md`:
- Set `status: "built"`
- Update `status_cell:` to `` "`built` (5/6 phases shipped — Phase 5 deferred pending ADR 0055; PR #NNN)" `` if H4 was not cleared, OR `` "`built` (6/6 phases shipped; PR #NNN)" `` if all phases shipped.
- Append a Notes paragraph summarizing PRs landed, halt-conditions cleared, deferred follow-ups.

Run `python3 tools/icm/render-ledger.py` to regenerate `active-workstreams.md`.
Verify `python3 tools/icm/render-ledger.py --check` exits 0.

### 6.2 XO project memory

Write `project_workstream_55_ships_office_built.md` (use the cohort `project_workstream_NN_*` naming convention).

### 6.3 W#35 cohort closure

W#55 is the FINAL W#35 Ship Architecture cohort follow-on. After W#55 ships built, update `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/MEMORY.md` Project-state index entry for W#35 to note: "**W#35 cohort COMPLETE — all 7 follow-on Stage 06 builds shipped (W#46/49/50/51/52/54/55)**." Coordinate with XO on the cohort-closure announcement (research-inbox `xo-cohort-complete-*.md`).

---

## Appendix A — Cited substrate symbols (§A0 self-audit)

All symbols verified on origin/main as of hand-off authoring (2026-05-05):

| Symbol | Location | Verified |
|---|---|---|
| `TenantId` | `packages/foundation/Assets/Common/TenantId.cs` (`Sunfish.Foundation.Assets.Common`) | ✓ |
| `ActorId` | `packages/foundation/Assets/Common/ActorId.cs` (`Sunfish.Foundation.Assets.Common`) | ✓ |
| `ILeaseDocumentVersionLog` | `packages/blocks-leases/Services/ILeaseDocumentVersionLog.cs` (`Sunfish.Blocks.Leases.Services`) | ✓ (W#22/W#27 built) |
| `IW9DocumentService` | `packages/blocks-maintenance/Services/IW9DocumentService.cs` | ✓ (W#18 built) |
| `W9Document` | `packages/blocks-maintenance/Models/W9Document.cs` | ✓ (W#18 built) |
| `W9DocumentView` | `packages/blocks-maintenance/Services/W9DocumentView.cs` | ✓ |
| `SignatureEnvelope` | `packages/foundation/Crypto/SignatureEnvelope.cs` | ✓ (Phase 0 stub; ADR 0021) |
| `BusinessCaseBundleManifest` | `packages/foundation-catalog/Bundles/BusinessCaseBundleManifest.cs` | ✓ (ADR 0007 built) |
| `IMissionEnvelopeObserver` | `packages/foundation-mission-space/Services/Contracts.cs:35` | ✓ (ADR 0062 built) |
| `IFieldDecryptor` | `packages/foundation-recovery/Crypto/IFieldDecryptor.cs` | ✓ (FORBIDDEN inside `IShipsOfficeDataProvider` impls) |
| `IFieldEncryptor` | `packages/foundation-recovery/Crypto/IFieldEncryptor.cs` | ✓ |
| `EncryptedField` | `packages/foundation-recovery/EncryptedField.cs` | ✓ (ADR 0046-A2 built) |
| `AuditEventType` | `packages/kernel-audit/AuditEventType.cs` | ✓ |
| `IAuditTrail` | `packages/kernel-audit/IAuditTrail.cs` | ✓ |
| `ShipRole.Scribe`, `ShipLocation.ShipsOffice`, `IPermissionResolver`, `ShipAction` | `packages/foundation-ship-common/` | **ABSENT** (W#46 Phase 1 not yet built — H1) |
| `IDiffPreview<TPath, TValue>`, `DiffPreviewView` | `packages/ui-core/` | **ABSENT** (W#46 Phase 3 not yet built — H2; Phase 1 stub OK) |
| `ISearchAsYouType<THit>` | `packages/ui-core/` | **ABSENT** (W#46 Phase 1 — H3; Phase 3 stub OK) |
| `IFormSchemaStore`, `DynamicTemplate` | (deferred — ADR 0055) | **ABSENT** (H4; Phase 5 conditional) |
| `ISignatureEnvelopeStore` | (does not exist) | **ABSENT** (H6; Phase 2 ships empty-list stub) |

§A0 directionality:
- **Negative existence verified:** `foundation-ships-office/` and `blocks-ships-office/` packages do NOT exist on origin/main; all 6 audit constants and 4 ShipAction names confirmed absent via grep sweeps; `ISignatureEnvelopeStore` confirmed absent.
- **Positive existence verified:** all substrate types listed above (Leases / Maintenance / Foundation Crypto / Catalog / MissionSpace / Recovery / Foundation Assets / Kernel Audit) confirmed PRESENT at the cited paths.
- **Structural-citation verified:** ADR 0083 §3 tier-discipline rule (`IDocumentDiffService` declared in blocks NOT foundation) cited verbatim per B-1 council finding; ADR 0083 §5 audit-emission ordering (permission FIRST → audit pre-op → execute) cited per B-2 council finding; ADR 0083 §8 SC 1.4.12 noted as WCAG 2.1 carried forward per B-3 council finding; W9 TIN redaction verified against `W9Document.cs` (TIN field exists on the model; `ShipsOfficeDocumentView` deliberately excludes it).

---

## Appendix B — Forward references resolved at each phase

| Phase | Forward-refs needed | Unblocking workstream |
|---|---|---|
| Phase 1 | `ShipAction` constants in `foundation-ship-common` | W#46 Phase 1 (H1; slice-A/slice-B if absent) |
| Phase 2 | `IPermissionResolver` (real wiring); `BundleCatalog`, `ILeaseDocumentVersionLog`, `IW9DocumentService` | W#46 P1 + already-built blocks |
| Phase 2 | Real `IDocumentDiffService` impl needs `DiffPreviewView` | W#46 Phase 3 (H2; stub acceptable) |
| Phase 2 | `ISignatureEnvelopeStore` for `SignatureEnvelope` kind | ADR 0004 Stage 06 (H6; empty-list stub acceptable) |
| Phase 3 | `DiffPreviewView`, `IDiffPreview<TPath, TValue>` | W#46 Phase 3 (H2) |
| Phase 3 | `ISearchAsYouType<THit>` | W#46 Phase 1 (H3) |
| Phase 3 | `ILiveAnnouncer`, `IFocusTrap` | W#46 Phase 3 (H3.A) |
| Phase 4 | None | — |
| Phase 5 | `IFormSchemaStore`, `DynamicTemplate` kind | ADR 0055 Status: Accepted (H4; conditional skip) |
| Phase 6 | None | — |

---

## Appendix C — Council subagent posture

| Phase | Subagents | Rationale |
|---|---|---|
| Phase 1 | Standard 4-perspective | Contracts + tier-discipline (B-1 rule); no security/a11y surface yet |
| Phase 2 | Standard + **security-engineering MANDATORY** | `IFieldDecryptor` prohibition (H4 reflection test) + audit-emission ordering (B-2) + permission-gate ordering + analyzer correctness |
| Phase 3 | Standard + **WCAG/a11y MANDATORY** | 12 SCs from ADR 0083 §8 (SC 1.3.1, 1.3.3, 1.4.1, 1.4.3, 1.4.4, 1.4.12, 2.1.1, 2.4.3, 2.4.5, 2.4.6, 3.3.1, 4.1.3); long-form reading + diff UX per W#35 §9.5 |
| Phase 4 | Standard 4-perspective | Demo wiring; no new security/a11y surface |
| Phase 5 | Standard 4-perspective | DynamicTemplate is composition with ADR 0055 (already-Accepted-elsewhere council depth) |
| Phase 6 | None (ledger flip) | Mechanical |

Cohort batting average per ADR 0069 D1 substrate-tier rule: pre-merge council canonical for every phase. Hand-offs (this document) are routine — no council required for the hand-off PR itself.

---

## Halt-conditions roll-up (counted)

Operational halt-conditions across the 6 phases:

1. **H1.A** (Phase 1 gate) — `foundation-ship-common` not on origin/main → slice-A/slice-B split.
2. **H1.B** (Phase 1) — AuditEventType collision found → STOP.
3. **H1.C** (Phase 1) — ShipAction collision → STOP.
4. **H1.D** (Phase 1) — `foundation-ships-office` accidentally references `Sunfish.UICore` → STOP, move `IDocumentDiffService` to blocks tier.
5. **H4** (Phase 2 mandatory check) — security council finds `IFieldDecryptor` referenced → STOP, redesign.
6. **H6** (Phase 2 informational) — `ISignatureEnvelopeStore` available → wire real impl; otherwise empty-list stub.
7. **H2.A** (Phase 2) — `DiffPreviewView` API drift → mirror exactly.
8. **H2.B** (Phase 2) — analyzer false-positives → tune rule.
9. **H2** (Phase 3 deferral) — `DiffPreviewView` not on origin/main → defer `DocumentDiffPanel.razor` to slice-B.
10. **H3** (Phase 3 deferral) — `ISearchAsYouType` not on origin/main → fallback `<input type="search">`.
11. **H3.A** (Phase 3) — W#46 Phase 3 a11y substrate not on origin/main → defer live-region wiring.
12. **H3.B** (Phase 3) — WCAG/a11y SC violation → fix in same PR.
13. **H4 not cleared** (Phase 5 conditional) — ADR 0055 not Accepted → SKIP Phase 5; defer to follow-on.
14. **H5** (informational; non-gating) — `IStandingOrderEventStream` ships → remove polling fallback in `SubscribeChangesAsync`.

(14 enumerated operational halt-conditions; the 6 ledger-level halts are H1–H6.)
