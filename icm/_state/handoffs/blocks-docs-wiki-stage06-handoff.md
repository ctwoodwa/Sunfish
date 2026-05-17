# Stage 06 Hand-off: `blocks-docs-wiki`

**Workstream:** W#70  
**Status gate:** `blocks-docs-core` (W#69) all PRs merged — `WikiPage.documentId` is a FK to `Document`.  
**Effort:** ~8-12h | **PRs:** 4  
**Owner:** dev (sunfish-PM overflow)  
**Council:** No mandatory council — no new auth-adjacent surface. Security spot-check at PR 3 (`IPolicyCommandService` + `PolicyAcknowledgment`) before arming auto-merge.

---

## Spec reference

`icm/02_architecture/blocks-docs-schema-design.md` §3.2 — Wiki / policies / procedures.

Attribution: **Bookstack (MIT)** — `WikiSpace → WikiBook → WikiPage` hierarchy (clean-room shape adaptation only; no code paste). NOTICE entry required in package root.

---

## Package location

`packages/blocks-docs-wiki/`

### csproj dependencies

```xml
<ProjectReference Include="..\..\foundation\foundation.csproj" />
<ProjectReference Include="..\..\foundation-events\foundation-events.csproj" />
<ProjectReference Include="..\blocks-docs-core\blocks-docs-core.csproj" />
<ProjectReference Include="..\blocks-people-foundation\blocks-people-foundation.csproj" />
```

---

## PR 1 — Wiki hierarchy scaffold (~2-3h)

### Entities

**`WikiSpace`**
```csharp
public sealed record WikiSpace
{
    public required WikiSpaceId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }           // unique per tenant
    public string? Description { get; init; }
    public required WikiVisibility Visibility { get; init; }
    public required bool RequiresApproval { get; init; }
    public RetentionPolicyId? DefaultRetentionPolicyId { get; init; }
    public DateTimeOffset? ArchivedAt { get; init; }
}
```

**`WikiBook`**
```csharp
public sealed record WikiBook
{
    public required WikiBookId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required WikiSpaceId SpaceId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }           // unique per space
    public string? Description { get; init; }
    public StorageRef? CoverImageRef { get; init; }
    public required int SortOrder { get; init; }
    public DateTimeOffset? ArchivedAt { get; init; }
}
```

**`WikiChapter`**
```csharp
public sealed record WikiChapter
{
    public required WikiChapterId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required WikiBookId BookId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
    public required int SortOrder { get; init; }
}
```

### IDs (ULID-string pattern)

```csharp
public readonly record struct WikiSpaceId(string Value);
public readonly record struct WikiBookId(string Value);
public readonly record struct WikiChapterId(string Value);
```

### Enum

```csharp
public enum WikiVisibility { Tenant, Restricted }
```

### Repository contracts

```csharp
public interface IWikiSpaceRepository
{
    Task<WikiSpace?> GetByIdAsync(WikiSpaceId id, TenantId tenantId, CancellationToken ct);
    Task<WikiSpace?> GetBySlugAsync(string slug, TenantId tenantId, CancellationToken ct);
    Task<IReadOnlyList<WikiSpace>> ListAsync(TenantId tenantId, CancellationToken ct);
    Task AddAsync(WikiSpace space, CancellationToken ct);
    Task UpdateAsync(WikiSpace space, CancellationToken ct);
}

public interface IWikiBookRepository
{
    Task<WikiBook?> GetByIdAsync(WikiBookId id, TenantId tenantId, CancellationToken ct);
    Task<WikiBook?> GetBySlugAsync(WikiSpaceId spaceId, string slug, TenantId tenantId, CancellationToken ct);
    Task<IReadOnlyList<WikiBook>> ListBySpaceAsync(WikiSpaceId spaceId, TenantId tenantId, CancellationToken ct);
    Task AddAsync(WikiBook book, CancellationToken ct);
    Task UpdateAsync(WikiBook book, CancellationToken ct);
}

public interface IWikiChapterRepository
{
    Task<WikiChapter?> GetByIdAsync(WikiChapterId id, TenantId tenantId, CancellationToken ct);
    Task<IReadOnlyList<WikiChapter>> ListByBookAsync(WikiBookId bookId, TenantId tenantId, CancellationToken ct);
    Task AddAsync(WikiChapter chapter, CancellationToken ct);
    Task UpdateAsync(WikiChapter chapter, CancellationToken ct);
}
```

Provide `InMemoryWikiSpaceRepository`, `InMemoryWikiBookRepository`, `InMemoryWikiChapterRepository`.

### DI

```csharp
public static IServiceCollection AddBlocksDocsWiki(this IServiceCollection services)
```

Register all in-memory repositories as Scoped. Expand in subsequent PRs.

### Tests (PR 1)

Minimum 3 tests per repository (add, get-by-id, slug-uniqueness constraint per tenant).

---

## PR 2 — WikiPage + Policy + Procedure (~2-3h)

### `WikiPage`

A `WikiPage` IS a `Document` (FK). `Document.DocumentType` must be `WikiPage`, `Policy`, or `Procedure` for the associated document.

```csharp
public sealed record WikiPage
{
    public required WikiPageId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required DocumentId DocumentId { get; init; }   // FK blocks-docs-core
    public required WikiBookId BookId { get; init; }
    public WikiChapterId? ChapterId { get; init; }         // null = directly under book
    public WikiPageId? ParentPageId { get; init; }         // nested-page; depth ≤ 4
    public required int SortOrder { get; init; }
    public required string MarkdownBody { get; init; }
    public string? RenderedHtml { get; init; }             // cached render; nullable
    public required IReadOnlyList<WikiPageId> Backlinks { get; init; }   // computed
    public required IReadOnlyList<WikiPageId> ForwardLinks { get; init; } // computed
}
```

**Invariants (enforced in command service, not just validation):**
- `ParentPageId` chain must terminate in ≤ 4 hops (no cycles; no depth > 4).
- Slug uniqueness: `(BookId, ChapterId, slug)` — slug comes from the linked Document.

### `Policy` overlay

```csharp
public sealed record Policy
{
    public required PolicyId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required WikiPageId WikiPageId { get; init; }
    public required string PolicyNumber { get; init; }     // unique per tenant
    public required string Category { get; init; }         // 'HR' | 'Safety' | 'Compliance' | ...
    public required IReadOnlyList<string> AppliesToRoles { get; init; }
    public required IReadOnlyList<string> AppliesToDepartments { get; init; }
    public required PolicyReviewCadence ReviewCadence { get; init; }
    public DateTimeOffset? NextReviewDue { get; init; }
    public required IReadOnlyList<string> ApproverIds { get; init; }
}
```

### `Procedure` overlay

```csharp
public sealed record Procedure
{
    public required ProcedureId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required WikiPageId WikiPageId { get; init; }
    public required string ProcedureNumber { get; init; }  // unique per tenant
    public required string Category { get; init; }
    public PolicyId? ParentPolicyId { get; init; }
    public int? EstimatedDurationMinutes { get; init; }
    public required IReadOnlyList<string> ToolingRequirements { get; init; }
}
```

### IDs + Enum

```csharp
public readonly record struct WikiPageId(string Value);
public readonly record struct PolicyId(string Value);
public readonly record struct ProcedureId(string Value);

public enum PolicyReviewCadence { Monthly, Quarterly, Annually, Biennially, AdHoc }
```

### Repository contracts

```csharp
public interface IWikiPageRepository
{
    Task<WikiPage?> GetByIdAsync(WikiPageId id, TenantId tenantId, CancellationToken ct);
    Task<IReadOnlyList<WikiPage>> ListByBookAsync(WikiBookId bookId, TenantId tenantId, CancellationToken ct);
    Task<IReadOnlyList<WikiPage>> ListByChapterAsync(WikiChapterId chapterId, TenantId tenantId, CancellationToken ct);
    Task AddAsync(WikiPage page, CancellationToken ct);
    Task UpdateAsync(WikiPage page, CancellationToken ct);
}

public interface IPolicyRepository
{
    Task<Policy?> GetByIdAsync(PolicyId id, TenantId tenantId, CancellationToken ct);
    Task<Policy?> GetByNumberAsync(string policyNumber, TenantId tenantId, CancellationToken ct);
    Task<IReadOnlyList<Policy>> ListAsync(TenantId tenantId, CancellationToken ct);
    Task AddAsync(Policy policy, CancellationToken ct);
    Task UpdateAsync(Policy policy, CancellationToken ct);
}

public interface IProcedureRepository
{
    Task<Procedure?> GetByIdAsync(ProcedureId id, TenantId tenantId, CancellationToken ct);
    Task<IReadOnlyList<Procedure>> ListByPolicyAsync(PolicyId policyId, TenantId tenantId, CancellationToken ct);
    Task AddAsync(Procedure procedure, CancellationToken ct);
    Task UpdateAsync(Procedure procedure, CancellationToken ct);
}
```

Provide in-memory impls for all three.

### Tests (PR 2)

- `WikiPage` depth invariant: depth-5 nest returns error; depth-4 succeeds.
- `WikiPage` cycle detection: A → B → A returns error.
- Policy number uniqueness per tenant.
- Procedure FK to parentPolicy resolves correctly in in-memory.

---

## PR 3 — Policy versioning + acknowledgment + command services (~3-4h)

### `PolicyVersion`

```csharp
public sealed record PolicyVersion
{
    public required PolicyVersionId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required PolicyId PolicyId { get; init; }
    public required DocumentVersionId DocumentVersionId { get; init; }  // FK blocks-docs-core
    public required string VersionLabel { get; init; }      // e.g. "2026.Q2"
    public required PolicyEffectiveDateId EffectiveDateId { get; init; }
    public required IReadOnlyList<string> ApprovedBy { get; init; }
    public required DateTimeOffset ApprovedAt { get; init; }
    public required bool AcknowledgmentRequired { get; init; }
    public DateTimeOffset? AcknowledgmentDeadline { get; init; }
}
```

### `PolicyEffectiveDate`

```csharp
public sealed record PolicyEffectiveDate
{
    public required PolicyEffectiveDateId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required PolicyId PolicyId { get; init; }
    public required DateTimeOffset EffectiveFrom { get; init; }
    public DateTimeOffset? EffectiveUntil { get; init; }    // null = open-ended
    public PolicyVersionId? SupersededByVersionId { get; init; }
}
```

### `PolicyAcknowledgment`

```csharp
public sealed record PolicyAcknowledgment
{
    public required PolicyAcknowledgmentId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required PolicyId PolicyId { get; init; }
    public required PolicyVersionId PolicyVersionId { get; init; }
    public required EmployeeId EmployeeId { get; init; }    // FK blocks-people-foundation
    public required AcknowledgmentStatus Status { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public required AcknowledgmentChannel Channel { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? SignatureId { get; init; }               // FK future blocks-docs-signing
    public string? DeclineReason { get; init; }
}
```

### IDs + Enums

```csharp
public readonly record struct PolicyVersionId(string Value);
public readonly record struct PolicyEffectiveDateId(string Value);
public readonly record struct PolicyAcknowledgmentId(string Value);

public enum AcknowledgmentStatus { Pending, Acknowledged, Declined, Expired }
public enum AcknowledgmentChannel { WebUi, EmailLink, OnboardingFlow, Mobile }
```

### `(PolicyVersionId, EmployeeId)` uniqueness

Enforce in `IPolicyAcknowledgmentRepository.AddAsync` — throw if combination already exists.

### Command services

**`IWikiCommandService`**
```csharp
public interface IWikiCommandService
{
    Task<WikiPage> CreatePageAsync(CreateWikiPageCommand cmd, CancellationToken ct);
    Task<WikiPage> PublishPageAsync(WikiPageId id, TenantId tenantId, CancellationToken ct);
    Task<WikiPage> ArchivePageAsync(WikiPageId id, TenantId tenantId, CancellationToken ct);
    Task<WikiPage> RestorePageAsync(WikiPageId id, TenantId tenantId, CancellationToken ct);
}
```

`CreatePageAsync` enforces: parentPage depth ≤ 4; cycle detection (walk up chain, fail if `id` appears); book/chapter ownership match.

**`IPolicyCommandService`**
```csharp
public interface IPolicyCommandService
{
    Task<PolicyVersion> PublishVersionAsync(PublishPolicyVersionCommand cmd, CancellationToken ct);
    Task<IReadOnlyList<PolicyAcknowledgment>> RequireAcknowledgmentAsync(
        PolicyVersionId versionId, IReadOnlyList<EmployeeId> employeeIds, TenantId tenantId, CancellationToken ct);
    Task<PolicyAcknowledgment> RecordAcknowledgmentAsync(RecordAcknowledgmentCommand cmd, CancellationToken ct);
}
```

`RecordAcknowledgmentAsync` transition rules: `Pending → Acknowledged` or `Pending → Declined`. `Declined → Pending` requires manager-override flag in the command.

**`IWikiLinkIntegrityService`** (simple forward/backlink registry)
```csharp
public interface IWikiLinkIntegrityService
{
    Task RegisterLinksAsync(WikiPageId sourceId, IReadOnlyList<WikiPageId> targetIds, TenantId tenantId, CancellationToken ct);
    Task DeregisterLinksAsync(WikiPageId sourceId, TenantId tenantId, CancellationToken ct);
    Task<IReadOnlyList<WikiPageId>> GetBacklinksAsync(WikiPageId targetId, TenantId tenantId, CancellationToken ct);
    Task<IReadOnlyList<WikiPageId>> GetForwardLinksAsync(WikiPageId sourceId, TenantId tenantId, CancellationToken ct);
}
```

Provide `InMemoryWikiLinkIntegrityService`. No background sweep in this package — link integrity is maintained on page save.

### DI (complete)

```csharp
services.AddSingleton<IWikiLinkIntegrityService, InMemoryWikiLinkIntegrityService>();
services.AddScoped<IWikiCommandService, DefaultWikiCommandService>();
services.AddScoped<IPolicyCommandService, DefaultPolicyCommandService>();
// + all remaining repositories
```

### Security spot-check criteria

Before arming auto-merge on PR 3, verify:
- `RecordAcknowledgmentAsync` cannot be called by an employee for another employee's record (tenantId scoping must match `PolicyAcknowledgment.EmployeeId` ownership — this is a domain-level guard; the actor-authentication guard is at the API boundary)
- `RequireAcknowledgmentAsync` validates all EmployeeIds are in the same tenant
- `PolicyAcknowledgment.IpAddress` / `UserAgent` are stored as-is (no PII enrichment); null is valid

### Tests (PR 3)

- `PublishVersionAsync` creates effective-date record; previous open record gets `EffectiveUntil` set.
- `RecordAcknowledgmentAsync` success path: Pending → Acknowledged.
- `RecordAcknowledgmentAsync` wrong employee: returns error (tenant isolation).
- `RecordAcknowledgmentAsync` Declined → Pending without manager-override: returns error.
- Link integrity: register A→{B,C}; get backlinks of B returns [A].
- Total: minimum 8 tests.

---

## PR 4 — apps/docs + EFCore + ledger flip (~1-2h)

### Docs page

```
apps/docs/blocks/docs-wiki/README.md
```

Mirror the layout of the nearest completed block docs page (check `apps/docs/blocks/` for the current template).

### EFCore entity configurations

If EFCore entity configurations exist for other `blocks-docs-*` packages on main, add configurations for WikiSpace, WikiBook, WikiChapter, WikiPage, Policy, Procedure, PolicyVersion, PolicyEffectiveDate, PolicyAcknowledgment to `packages/blocks-docs-wiki/Infrastructure/BlocksDocsWikiEntityConfigurations.cs`.

If blocks-docs-core has not yet shipped EFCore config (StorageRef is still a placeholder), defer EFCore for wiki too and note in PR description.

### Ledger flip

Update `icm/_state/workstreams/W70-blocks-docs-wiki.md` → `status: "built"`. Run `python3 tools/icm/render-ledger.py`. Include in this PR.

---

## Acceptance criteria

- [ ] All 9 entity types implemented (WikiSpace, WikiBook, WikiChapter, WikiPage, Policy, Procedure, PolicyVersion, PolicyEffectiveDate, PolicyAcknowledgment)
- [ ] All 3 enums (WikiVisibility, PolicyReviewCadence, AcknowledgmentStatus, AcknowledgmentChannel) — 4 enums total
- [ ] All IDs as ULID-string records (7: WikiSpaceId, WikiBookId, WikiChapterId, WikiPageId, PolicyId, ProcedureId, PolicyVersionId, PolicyEffectiveDateId, PolicyAcknowledgmentId — 9 IDs)
- [ ] All repository contracts + in-memory impls
- [ ] `IWikiCommandService` + `IPolicyCommandService` + `IWikiLinkIntegrityService`
- [ ] depth ≤ 4 and cycle-detection enforced in `CreatePageAsync`
- [ ] Acknowledgment transition rules enforced in `RecordAcknowledgmentAsync`
- [ ] NOTICE entry: Bookstack (MIT) in package root
- [ ] `dotnet test packages/blocks-docs-wiki/tests/` green
- [ ] Ledger flipped to `built` in PR 4

---

## What's next after W#70

`blocks-docs-templates` (W#71) — contract templates, ContractTemplateField, ContractTemplateClause, TemplateRenderJob, ContractInstance; lease/notice/NDA template support. XO will author hand-off when W#70 PR 1 merges.
