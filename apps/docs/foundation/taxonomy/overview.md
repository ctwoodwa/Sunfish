# Foundation.Taxonomy substrate

`Sunfish.Foundation.Taxonomy` is the foundation-tier substrate for versioned reference-data taxonomies — the building block behind signature scopes, equipment classes, vendor specialties, jurisdiction policies, and inspection deficiency categories.

It implements [ADR 0056 — Foundation.Taxonomy substrate](../../../docs/adrs/0056-foundation-taxonomy-substrate.md).

## What it gives you

| Type | Role |
|---|---|
| `TaxonomyDefinition` | Top-level taxonomy product (versioned + governance-scoped). |
| `TaxonomyNode` | One entry within a definition version; root or child. |
| `TaxonomyClassification` | The reference primitive consumers store to point at a node — definition + code + pinned version + optional cached display. |
| `TaxonomyLineage` | Audit trail of how a definition was derived from an ancestor (Clone / Extend / Alter / InitialPublication). |
| `ITaxonomyRegistry` | CRUD + governance contract. Implementations enforce 5 governance rules. |
| `ITaxonomyResolver` | Read-side contract for resolving classifications back to nodes. |
| `InMemoryTaxonomyRegistry` / `InMemoryTaxonomyResolver` | Reference implementations; thread-safe; not durable. |

## Identity convention

Taxonomies are identified as `Vendor.Domain.TaxonomyName@Version`:

- `Sunfish.Signature.Scopes@1.0.0` — Sunfish-shipped signature scopes.
- `Acme.Equipment.Classes@1.2.0` — tenant fork of an equipment-class taxonomy.

Each token must match `[A-Za-z][A-Za-z0-9]*`. The version is semver: major bumps signal breaking node-set changes (renames, removals); minor bumps add nodes; patch bumps correct display labels.

## Governance regimes

| Regime | Mutability |
|---|---|
| `Authoritative` | Sunfish-shipped or compliance-source. Only `ActorId.Sunfish` may publish new versions or mutate nodes. Pinned-version references only. |
| `Enterprise` | Org-scoped. Owner approves derivations from the parent. |
| `Civilian` | Tenant-local. Clone, extend, and alter freely. |

## Lineage operations

| Op | Semantics |
|---|---|
| `InitialPublication` | The definition was created from scratch (no ancestor). |
| `Clone` | Copy of an ancestor with a new identity; node set initially identical. Tombstoned nodes are copied as-is (preserving audit trail). |
| `Extend` | Adds new nodes to an ancestor's set; ancestor nodes inherited unchanged. |
| `Alter` | Revises the ancestor's node set (renames, removals); breaks consumers pinned to the ancestor. Requires explicit reason. |

## Tombstoning

Tombstoning is monotonic: a tombstoned node may not return to active. Tombstoned nodes still **resolve** (consumers see `Status: Tombstoned`); how to render them is a UX call. To replace a tombstoned node, supply a `SuccessorCode` so consumers can migrate cleanly.

## API at a glance

```csharp
// Bootstrap the in-memory implementation
services.AddInMemoryTaxonomy();

// Seed a Sunfish-shipped Authoritative taxonomy
var registry = sp.GetRequiredService<ITaxonomyRegistry>();
await registry.RegisterCorePackageAsync(tenantId, TaxonomyCorePackages.SunfishSignatureScopes, ct);

// Resolve a classification
var resolver = sp.GetRequiredService<ITaxonomyResolver>();
var node = await resolver.ResolveAsync(tenantId, new TaxonomyClassification
{
    Definition = new TaxonomyDefinitionId("Sunfish", "Signature", "Scopes"),
    Code = "lease-execution",
    Version = TaxonomyVersion.V1_0_0,
}, ct);
// node.Display => "Lease Execution"
```

## Sunfish-shipped seed: `Sunfish.Signature.Scopes@1.0.0`

The first Authoritative taxonomy: 17 root nodes + 7 children covering lease execution, inspection acknowledgments, notary scopes, consent forms, and payment authorization. Used by [ADR 0054](../../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) Pattern E to type captured signatures.

Future starter taxonomies (per the v1.0 charters in PR #242):

- `Sunfish.Equipment.Classes@1.0.0`
- `Sunfish.Vendor.Specialties@1.0.0`
- `Sunfish.Inspection.DeficiencyCategories@1.0.0`
- `Sunfish.Contact.UseContexts@1.0.0`

## Audit emission

Each lifecycle operation emits an `AuditRecord` with one of nine new `AuditEventType` discriminators (added to `Sunfish.Kernel.Audit.AuditEventType`):

| Event type | Emitted by |
|---|---|
| `TaxonomyDefinitionCreated` | `CreateAsync` / `RegisterCorePackageAsync` |
| `TaxonomyVersionPublished` | `PublishVersionAsync` |
| `TaxonomyVersionRetired` | `RetireDefinitionVersionAsync` |
| `TaxonomyNodeAdded` | `AddNodeAsync` |
| `TaxonomyNodeDisplayRevised` | `ReviseDisplayAsync` |
| `TaxonomyNodeTombstoned` | `TombstoneNodeAsync` |
| `TaxonomyDefinitionCloned` | `CloneAsync` |
| `TaxonomyDefinitionExtended` | `ExtendAsync` |
| `TaxonomyDefinitionAltered` | `AlterAsync` |

Audit emission is opt-in: pass an `IAuditTrail` + `IOperationSigner` to the `InMemoryTaxonomyRegistry` constructor. Without them, audit records are silently skipped (useful for unit tests).

## Phase 1 scope (this package)

- Substrate types, contracts, and reference implementations.
- 9 new `AuditEventType` discriminators in `Sunfish.Kernel.Audit`.
- `Sunfish.Signature.Scopes@1.0.0` seed.
- DI extension (`AddInMemoryTaxonomy`).

Subsequent phases (per ADR 0056): durable backends, CRDT-backed multi-node sync (per ADR 0028), and marketplace distribution + clone/extend/alter governance enforcement.
