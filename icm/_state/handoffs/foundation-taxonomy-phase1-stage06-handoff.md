# Hand-off — Foundation.Taxonomy substrate Phase 1 (kernel-tier-adjacent foundation; registry + nodes + resolver)

**From:** research session (CTO)
**To:** sunfish-PM session
**Created:** 2026-04-29
**Status:** `ready-to-build`
**Spec source:** [ADR 0056](../../docs/adrs/0056-foundation-taxonomy-substrate.md) (Accepted 2026-04-29 via PR #243); [Starter taxonomy charters](../00_intake/output/starter-taxonomies-v1-charters-2026-04-29.md) (PR #242)
**Approval:** ADR 0056 Accepted by CTO 2026-04-29 (technical merit; council-review subagent dispatch waived per cluster decision-velocity preference); 8/8 pre-acceptance self-audit complete in ADR
**Estimated cost:** ~5–10 hours sunfish-PM (foundation-tier package scaffold + 8 entity types + 2 service interfaces + in-memory impl + 8 audit record types + ~30-40 tests + kitchen-sink seed + docs page)
**Pipeline:** `sunfish-feature-change`
**Audit before build:** `ls /Users/christopherwood/Projects/Sunfish/packages/ | grep -E "^foundation-taxonomy"` confirmed no collision (audit run 2026-04-29 by CTO)

---

## Context

Phase 1 lands the Foundation.Taxonomy substrate's core types, registry contract, and resolver. Subsequent phases ship: starter taxonomy seed loaders (Phase 2; gated on charter sign-off), CRDT-backed multi-node sync (Phase 3; ADR 0028 composition), marketplace distribution + clone/extend/alter governance enforcement (Phase 4).

This hand-off scope is **substrate types + service contracts + reference implementation + Sunfish.Signature.Scopes seed**. Concrete enough to unblock:

- ADR 0054 amendment implementation (SignatureScope = TaxonomyClassification list per Pattern E)
- ADR 0055 dynamic-forms substrate Phase 1 (Coding / CodeableConcept primitives reference Foundation.Taxonomy)
- Property cluster equipment migration (hardcoded enum → taxonomy ref)
- Future cluster module reframes (Receipts, Vendor specialties, Inspection deficiency categories)

---

## Files to create

### Package scaffold

```
packages/foundation-taxonomy/
├── Sunfish.Foundation.Taxonomy.csproj
├── README.md
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs       (AddInMemoryTaxonomy)
├── Models/
│   ├── TaxonomyDefinition.cs
│   ├── TaxonomyDefinitionId.cs
│   ├── TaxonomyNode.cs
│   ├── TaxonomyNodeId.cs                     (composite-key wrapping definition id + node code)
│   ├── TaxonomyClassification.cs
│   ├── TaxonomyVersion.cs
│   ├── TaxonomyGovernanceRegime.cs            (enum: Civilian / Enterprise / Authoritative)
│   ├── TaxonomyLineage.cs
│   ├── TaxonomyLineageOp.cs                   (enum: Clone / Extend / Alter / InitialPublication)
│   ├── TaxonomyNodeStatus.cs                  (enum: Active / Tombstoned)
│   └── DisplayHistory.cs                       (audit-trail of display revisions per node)
├── Services/
│   ├── ITaxonomyRegistry.cs                  (CRUD on definitions + nodes; governance-enforcing)
│   ├── ITaxonomyResolver.cs                  (resolve TaxonomyClassification → TaxonomyNode)
│   ├── InMemoryTaxonomyRegistry.cs           (reference implementation; thread-safe; in-process)
│   └── InMemoryTaxonomyResolver.cs            (reference implementation; reads from registry)
├── Audit/
│   └── TaxonomyAuditRecords.cs               (8 record types per ADR 0049)
└── tests/
    └── Sunfish.Foundation.Taxonomy.Tests.csproj
        ├── TaxonomyDefinitionTests.cs
        ├── TaxonomyNodeTests.cs
        ├── TaxonomyClassificationTests.cs
        ├── TaxonomyLineageTests.cs
        ├── ITaxonomyRegistryTests.cs           (CRUD + governance enforcement)
        ├── ITaxonomyResolverTests.cs           (resolution + tombstoning + version pinning)
        ├── TaxonomyAuditEmissionTests.cs       (8 record types emitted on lifecycle events)
        └── TaxonomySeedTests.cs                 (Sunfish.Signature.Scopes seed loads correctly)
```

### Type definitions (concrete shape — implement exactly)

```csharp
namespace Sunfish.Foundation.Taxonomy;

// Identity types
public readonly record struct TaxonomyDefinitionId(string Vendor, string Domain, string TaxonomyName)
{
    public override string ToString() => $"{Vendor}.{Domain}.{TaxonomyName}";
    public static TaxonomyDefinitionId Parse(string identity);   // validates "Vendor.Domain.TaxonomyName" shape
}

public readonly record struct TaxonomyVersion(int Major, int Minor, int Patch)
{
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
    public static TaxonomyVersion Parse(string semver);
    public static TaxonomyVersion V1_0_0 => new(1, 0, 0);
}

public readonly record struct TaxonomyNodeId(TaxonomyDefinitionId Definition, string Code);

// Definition (root product)
public sealed record TaxonomyDefinition
{
    public required TaxonomyDefinitionId Id { get; init; }
    public required TaxonomyVersion Version { get; init; }
    public required TaxonomyGovernanceRegime Governance { get; init; }
    public required string Description { get; init; }
    public required IdentityRef Owner { get; init; }                   // Sunfish for Authoritative; tenant-scoped IdentityRef otherwise
    public required DateTimeOffset PublishedAt { get; init; }
    public DateTimeOffset? RetiredAt { get; init; }                     // null for active versions
    public TaxonomyLineage? DerivedFrom { get; init; }                  // null for InitialPublication ops
}

public enum TaxonomyGovernanceRegime
{
    Civilian,        // tenant-local; clone/extend/alter freely
    Enterprise,      // org-scoped; owner approves derivations
    Authoritative    // Sunfish-shipped or compliance-source; pinned versions only
}

// Node (entry within a definition)
public sealed record TaxonomyNode
{
    public required TaxonomyNodeId Id { get; init; }                    // composite key
    public required TaxonomyVersion DefinitionVersion { get; init; }    // which version of the definition this node belongs to
    public required string Display { get; init; }                       // current display string
    public required string Description { get; init; }
    public string? ParentCode { get; init; }                            // null for root nodes; otherwise refers to sibling node within same definition
    public required TaxonomyNodeStatus Status { get; init; }
    public required DateTimeOffset PublishedAt { get; init; }           // first appearance of this node
    public DateTimeOffset? TombstonedAt { get; init; }
    public string? SuccessorCode { get; init; }                          // for tombstoned nodes; recommendations to consumers
    public string? DeprecationReason { get; init; }
    public IReadOnlyList<DisplayHistory> DisplayHistoryEntries { get; init; } = Array.Empty<DisplayHistory>();
}

public enum TaxonomyNodeStatus
{
    Active,           // valid for resolution + new classifications
    Tombstoned        // resolves but flagged; new classifications discouraged
}

public sealed record DisplayHistory
{
    public required string Display { get; init; }
    public required DateTimeOffset RevisedAt { get; init; }
    public string? RevisionReason { get; init; }
}

// The reference primitive — used by consuming records
public sealed record TaxonomyClassification
{
    public required TaxonomyDefinitionId Definition { get; init; }
    public required string Code { get; init; }                          // node's stable code
    public required TaxonomyVersion Version { get; init; }              // pinned version (mandatory for compliance/audit records)
    public string? DisplayCache { get; init; }                          // optional cached display for offline rendering
}

// Lineage (audit trail of definition derivation)
public sealed record TaxonomyLineage
{
    public required TaxonomyLineageOp Operation { get; init; }
    public required TaxonomyDefinitionId AncestorDefinition { get; init; }
    public required TaxonomyVersion AncestorVersion { get; init; }
    public required IdentityRef DerivedBy { get; init; }
    public required DateTimeOffset DerivedAt { get; init; }
    public required string Reason { get; init; }
}

public enum TaxonomyLineageOp
{
    InitialPublication,    // definition was created from scratch
    Clone,                 // copy of ancestor with new identity; node set initially identical
    Extend,                // child-of-parent with new added nodes; ancestor nodes inherited unchanged
    Alter                  // child-of-parent with revised node set (renames or removals); breaks consumers pinned to ancestor
}
```

### Service contracts

```csharp
namespace Sunfish.Foundation.Taxonomy;

public interface ITaxonomyRegistry
{
    // Definition lifecycle
    Task<TaxonomyDefinition> CreateAsync(
        TaxonomyDefinitionId id,
        TaxonomyVersion version,
        TaxonomyGovernanceRegime governance,
        string description,
        IdentityRef owner,
        TaxonomyLineage? derivedFrom,
        CancellationToken ct);

    Task<TaxonomyDefinition> PublishVersionAsync(
        TaxonomyDefinitionId id,
        TaxonomyVersion newVersion,
        IdentityRef publishedBy,
        CancellationToken ct);

    Task RetireDefinitionVersionAsync(
        TaxonomyDefinitionId id,
        TaxonomyVersion version,
        IdentityRef retiredBy,
        CancellationToken ct);

    // Node lifecycle (within a definition+version pair)
    Task<TaxonomyNode> AddNodeAsync(
        TaxonomyDefinitionId definition,
        TaxonomyVersion version,
        string code,
        string display,
        string description,
        string? parentCode,
        IdentityRef addedBy,
        CancellationToken ct);

    Task<TaxonomyNode> ReviseDisplayAsync(
        TaxonomyNodeId nodeId,
        string newDisplay,
        string? revisionReason,
        IdentityRef revisedBy,
        CancellationToken ct);

    Task TombstoneNodeAsync(
        TaxonomyNodeId nodeId,
        string deprecationReason,
        string? successorCode,
        IdentityRef tombstonedBy,
        CancellationToken ct);

    // Lineage operations (Clone/Extend/Alter)
    Task<TaxonomyDefinition> CloneAsync(
        TaxonomyDefinitionId source,
        TaxonomyVersion sourceVersion,
        TaxonomyDefinitionId newId,
        IdentityRef clonedBy,
        string reason,
        CancellationToken ct);

    Task<TaxonomyDefinition> ExtendAsync(
        TaxonomyDefinitionId source,
        TaxonomyVersion sourceVersion,
        TaxonomyDefinitionId newId,
        IdentityRef extendedBy,
        string reason,
        CancellationToken ct);

    Task<TaxonomyDefinition> AlterAsync(
        TaxonomyDefinitionId source,
        TaxonomyVersion sourceVersion,
        TaxonomyDefinitionId newId,
        IdentityRef alteredBy,
        string reason,
        CancellationToken ct);

    // Reads
    Task<TaxonomyDefinition?> GetDefinitionAsync(
        TaxonomyDefinitionId id,
        TaxonomyVersion version,
        CancellationToken ct);

    Task<IReadOnlyList<TaxonomyNode>> GetNodesAsync(
        TaxonomyDefinitionId definition,
        TaxonomyVersion version,
        CancellationToken ct);

    Task<TaxonomyNode?> GetNodeAsync(
        TaxonomyNodeId nodeId,
        TaxonomyVersion version,
        CancellationToken ct);

    Task<IReadOnlyList<TaxonomyDefinition>> ListDefinitionsAsync(
        TaxonomyDefinitionId? filterByVendor,
        CancellationToken ct);

    // Core-package bootstrap
    Task RegisterCorePackageAsync(
        TaxonomyCorePackage package,
        CancellationToken ct);
}

public interface ITaxonomyResolver
{
    Task<TaxonomyNode?> ResolveAsync(TaxonomyClassification classification, CancellationToken ct);
    Task<IReadOnlyList<TaxonomyNode>> ResolveAllAsync(IReadOnlyList<TaxonomyClassification> classifications, CancellationToken ct);
    Task<bool> IsActiveAsync(TaxonomyClassification classification, CancellationToken ct);   // false for tombstoned or unknown
}

public sealed record TaxonomyCorePackage
{
    public required TaxonomyDefinition Definition { get; init; }
    public required IReadOnlyList<TaxonomyNode> Nodes { get; init; }
}
```

### Governance enforcement rules

`InMemoryTaxonomyRegistry` MUST enforce these rules; tests cover each:

1. **Authoritative regime:** Only Sunfish-owned (`Owner` matches a Sunfish-issued IdentityRef) can call `Create` / `PublishVersion` / `AddNode` / `ReviseDisplay` / `TombstoneNode` directly. Civilian callers can call `Clone` (creates new Civilian-regime definition). Calls violating this throw `TaxonomyGovernanceException`.

2. **Node code immutability post-publication:** `AddNode` accepted; node-display revision via `ReviseDisplayAsync` accepted; renaming a `code` is a major-version event (must call `PublishVersion` first). Tests cover the rejection.

3. **Tombstoning is monotonic:** Tombstoned → Tombstoned (no-op); Tombstoned → Active is rejected (use a major version bump instead). Test covers.

4. **Lineage immutability:** Once `DerivedFrom` is set on a definition, it cannot change. Test covers.

5. **`Alter` requires explicit reason:** `AlterAsync` rejects empty / null `reason`. Test covers.

### Audit record types (per ADR 0049)

```csharp
namespace Sunfish.Foundation.Taxonomy.Audit;

public sealed record CreateDefinitionAuditRecord : IAuditRecord
{
    public required TaxonomyDefinitionId Definition { get; init; }
    public required TaxonomyVersion Version { get; init; }
    public required TaxonomyGovernanceRegime Governance { get; init; }
    public required IdentityRef Owner { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed record PublishVersionAuditRecord : IAuditRecord { /* ... */ }
public sealed record AddNodeAuditRecord : IAuditRecord { /* ... */ }
public sealed record ReviseDisplayAuditRecord : IAuditRecord { /* ... */ }
public sealed record TombstoneNodeAuditRecord : IAuditRecord { /* ... */ }
public sealed record CloneDerivationAuditRecord : IAuditRecord { /* ... */ }
public sealed record ExtendDerivationAuditRecord : IAuditRecord { /* ... */ }
public sealed record AlterDerivationAuditRecord : IAuditRecord { /* ... */ }
```

(8 record types total; mirror the `ITaxonomyRegistry` lifecycle methods 1:1; `RegisterCorePackage` and read methods do not emit audit records.)

### Sunfish.Signature.Scopes seed

The Sunfish.Signature.Scopes@1.0.0 starter taxonomy is seeded via `RegisterCorePackageAsync`. Use the charter authored in PR #242:

`icm/00_intake/output/starter-taxonomies-v1-charters-2026-04-29.md` §"Charter 1: Sunfish.Signature.Scopes@1.0.0"

Concretely: 17 root nodes + 7 children. Implement as a static factory `TaxonomyCorePackages.SunfishSignatureScopes` that returns the `TaxonomyCorePackage`. Test that loading + resolving + tombstone-rejection on Authoritative regime works.

### DI extension

```csharp
namespace Sunfish.Foundation.Taxonomy.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryTaxonomy(this IServiceCollection services)
    {
        services.AddSingleton<ITaxonomyRegistry, InMemoryTaxonomyRegistry>();
        services.AddSingleton<ITaxonomyResolver, InMemoryTaxonomyResolver>();
        return services;
    }
}
```

### Kitchen-sink demo

In `apps/kitchen-sink`, add a "Taxonomy seed demo" page that:
1. Registers `Sunfish.Signature.Scopes@1.0.0`
2. Lists all root nodes with their displays
3. Demonstrates resolving a `TaxonomyClassification` (e.g., `lease-execution`)
4. Demonstrates the tombstone path (rejection on Authoritative regime)

### apps/docs page

Create `apps/docs/foundation/taxonomy.md`:

- Substrate overview (link to ADR 0056)
- Identity convention (`Vendor.Domain.TaxonomyName@Version`)
- Governance regimes (Civilian / Enterprise / Authoritative)
- Versioning rules (semver applied to taxonomy products)
- Lineage operations (Clone / Extend / Alter / InitialPublication)
- Tombstoning semantics
- ITaxonomyRegistry / ITaxonomyResolver API
- Sunfish.Signature.Scopes example
- Future starter taxonomies (link to charters)

## Acceptance criteria

Stage 06 build is complete when:

- [ ] `packages/foundation-taxonomy/` package scaffolded; csproj references foundation-recovery + kernel-audit; targets net11 (preview)
- [ ] All 11 model types in `Models/` directory; full XML doc; nullability + `required` enforced
- [ ] `ITaxonomyRegistry` + `ITaxonomyResolver` interfaces with full XML doc on all methods + parameters
- [ ] `InMemoryTaxonomyRegistry` thread-safe (use `ConcurrentDictionary`); enforces 5 governance rules
- [ ] `InMemoryTaxonomyResolver` reads from registry; correct tombstone rejection; version-pinning honored
- [ ] 8 audit record types in `Audit/TaxonomyAuditRecords.cs` per ADR 0049
- [ ] `Sunfish.Signature.Scopes@1.0.0` seed factory implemented (`TaxonomyCorePackages.SunfishSignatureScopes`); 17 root + 7 children nodes match charter exactly
- [ ] `AddInMemoryTaxonomy` DI extension method
- [ ] **Tests (target ~30-40):**
  - [ ] CRUD on `TaxonomyDefinition` (Create + PublishVersion + Retire)
  - [ ] CRUD on `TaxonomyNode` (Add + ReviseDisplay + Tombstone)
  - [ ] Lineage operations (Clone / Extend / Alter / InitialPublication)
  - [ ] Governance regime enforcement (5 rules; one test per)
  - [ ] Resolution: TaxonomyClassification → TaxonomyNode (active + tombstoned + unknown)
  - [ ] Resolution batch (`ResolveAllAsync`) preserves order
  - [ ] Audit emission: 8 record types fire on appropriate lifecycle calls
  - [ ] Sunfish.Signature.Scopes seed loads + resolves
  - [ ] Concurrency (parallel `AddNode` calls don't lose nodes)
- [ ] Kitchen-sink "Taxonomy seed demo" page renders correctly; lists all 17 root nodes
- [ ] `apps/docs/foundation/taxonomy.md` published per template; covers all sections above
- [ ] `dotnet build` clean; no analyzer warnings
- [ ] `dotnet test` all green (target ~30-40 tests; ≥85% line coverage on the new package)

## Open questions sunfish-PM may encounter (preferred resolutions)

| OQ | Question | Preferred resolution (research session) |
|---|---|---|
| OQ-1 | `TaxonomyDefinitionId` parsing edge cases (e.g., dots in TaxonomyName?). | Reject any token containing `.`. Vendor + Domain + TaxonomyName must each match `[A-Za-z][A-Za-z0-9]*`. Throw `FormatException` on parse violation. |
| OQ-2 | What happens to existing `TaxonomyClassification` references when their target node is tombstoned post-creation? | They still resolve (return the tombstoned node + `Status: Tombstoned` flag); consumer decides UX. Test covers. |
| OQ-3 | Should `RegisterCorePackageAsync` be idempotent (calling twice with same data is no-op)? | YES. Re-registration of identical core package is a successful no-op (good for fresh-tenant init across multiple call sites). Re-registration with different data throws `TaxonomyConflictException`. |
| OQ-4 | Where does `IdentityRef` come from for a Sunfish-shipped Authoritative taxonomy? | Use a sentinel `IdentityRef.Sunfish` (foundation-tier constant; introduce if not present in Foundation.Identity). Authoritative-regime guard checks `owner == IdentityRef.Sunfish`. |
| OQ-5 | Should `ITaxonomyResolver.ResolveAsync` be cached? | NOT in Phase 1. In-memory registry is fast; caching is a Phase 2+ optimization. Test that cold resolution is sub-microsecond. |
| OQ-6 | Are read methods audit-emitted? | NO. Only writes emit audit records. Read paths are not audit-relevant for Phase 1. |
| OQ-7 | How does `Clone` behave when the source has tombstoned nodes? | Tombstoned nodes are copied as-is (preserves audit trail of inheritance). Cloned definition's owner can revise via Alter operation if desired. |
| OQ-8 | Cross-tenant ownership of taxonomies? | OUT OF SCOPE for Phase 1. Tenant-scope per IdentityRef is per ADR 0008 standard pattern; cross-tenant federation is post-MVP. |

If sunfish-PM encounters a different OQ not listed above, **HALT and write a memory note flagging to research session.** Do not guess.

## Sequencing for downstream consumers

After this Phase 1 lands, the following ADRs become buildable:

- **ADR 0054 amendments (Pattern E SignatureScope)** — `kernel-signatures` Stage 06 references `TaxonomyClassification` from this package
- **ADR 0055 dynamic-forms substrate Phase 1** — Coding/CodeableConcept primitives reference Foundation.Taxonomy nodes
- **Property cluster equipment migration** — hardcoded `EquipmentClass` enum migrates to taxonomy-backed reference
- **Inspections EXTEND deficiency-categories migration** — `Sunfish.Inspection.DeficiencyCategories@1.0.0` reference
- **Vendor Onboarding ADR (queued)** — `Sunfish.Vendor.Specialties@1.0.0` reference
- **ADR 0057 leasing-pipeline (queued for follow-up charter authoring)** — `Sunfish.Jurisdiction.US-States.Leasing@1.0.0` reference

## Halt conditions (sunfish-PM)

STOP and write a memory note if any of these surface:

- Existing `Sunfish.Foundation.Recovery.IdentityRef` doesn't have a `Sunfish` sentinel constant (research must amend Foundation.Recovery first)
- `ConcurrentDictionary` semantics insufficient for governance-rule enforcement (might need lock; research will recommend pattern)
- `IAuditRecord` interface from kernel-audit doesn't compose cleanly with the 8 new record types (research will reconcile)
- Charter node data (17 root + 7 children for Sunfish.Signature.Scopes) doesn't fit the schema as defined (e.g., a child has no parent — would indicate charter authoring error; research must amend charter)

## Definition of Done (research-session reviewer)

- All 11 model types match the spec; XML doc complete; nullability + `required` correct
- Both service interfaces have full XML doc + correct nullability
- In-memory implementations enforce 5 governance rules with tests covering each
- 8 audit record types emit correctly with tests covering each
- Sunfish.Signature.Scopes seed loads + 17 root + 7 children resolve
- ≥85% line coverage on the new package
- `dotnet build` + `dotnet test` clean
- Kitchen-sink demo + apps/docs page published
- No memory-note halt conditions left unresolved

After review-pass clean, sunfish-PM marks workstream `built` in active-workstreams.md row (research session adds the row entry on hand-off completion).

---

## Workstream ledger row addition (research session: add on hand-off acceptance)

| ID | Description | State | Owner | Spec | Notes |
|---|---|---|---|---|---|
| 31 | Foundation.Taxonomy substrate Phase 1 | `ready-to-build` | sunfish-PM | `icm/_state/handoffs/foundation-taxonomy-phase1-stage06-handoff.md` | Phase 1 of ADR 0056. Registry + nodes + resolver + Sunfish.Signature.Scopes seed + 8 audit types. ~5–10h. Unblocks: ADR 0054 Stage 06, ADR 0055 Phase 1, property cluster equipment migration, future cluster reframes. |
