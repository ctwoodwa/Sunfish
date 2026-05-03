# Sunfish.Foundation.Taxonomy

Foundation-tier substrate for versioned reference-data taxonomies — the building block behind signature scopes, equipment classes, vendor specialties, jurisdiction policies, and inspection deficiency categories.

Implements [ADR 0056 — Foundation.Taxonomy substrate](../../docs/adrs/0056-foundation-taxonomy-substrate.md).

## What it ships (Phase 1)

- **Models:** `TaxonomyDefinition`, `TaxonomyDefinitionId`, `TaxonomyVersion`, `TaxonomyNode`, `TaxonomyNodeId`, `TaxonomyClassification`, `TaxonomyLineage`, `DisplayHistory` + 3 enums (`TaxonomyGovernanceRegime`, `TaxonomyNodeStatus`, `TaxonomyLineageOp`).
- **Contracts:** `ITaxonomyRegistry` (CRUD + governance) and `ITaxonomyResolver` (read-side classification → node).
- **In-memory reference implementations:** thread-safe via `ConcurrentDictionary`. Enforce 5 governance rules:
  1. Authoritative-regime mutations require `ActorId.Sunfish`.
  2. Node codes are immutable post-publish.
  3. Tombstoning is monotonic.
  4. Lineage is immutable.
  5. `Alter` requires an explicit reason.
- **Audit emission:** 9 new `AuditEventType` discriminators in `Sunfish.Kernel.Audit`; `TaxonomyAuditPayloadFactory` builds the payload bodies.
- **Sunfish-shipped seed:** `TaxonomyCorePackages.SunfishSignatureScopes` (`Sunfish.Signature.Scopes@1.0.0` — 17 root nodes + 7 children, per the v1.0 charter in PR #242).
- **DI extension:** `services.AddInMemoryTaxonomy()`.

## Quick start

```csharp
services.AddInMemoryTaxonomy();

var registry = sp.GetRequiredService<ITaxonomyRegistry>();
await registry.RegisterCorePackageAsync(tenantId, TaxonomyCorePackages.SunfishSignatureScopes, ct);

var resolver = sp.GetRequiredService<ITaxonomyResolver>();
var node = await resolver.ResolveAsync(tenantId, new TaxonomyClassification
{
    Definition = new TaxonomyDefinitionId("Sunfish", "Signature", "Scopes"),
    Code = "lease-execution",
    Version = TaxonomyVersion.V1_0_0,
}, ct);
```

## Audit emission

To wire audit emission, pass an `IAuditTrail` and `IOperationSigner` to the registry constructor:

```csharp
services.AddSingleton<ITaxonomyRegistry>(sp => new InMemoryTaxonomyRegistry(
    sp.GetRequiredService<IAuditTrail>(),
    sp.GetRequiredService<IOperationSigner>()));
```

Without these the registry still functions; audit records are silently skipped (useful for unit tests and bootstrap scenarios where signing isn't yet wired).

## Out of scope (Phase 1)

- Durable backends (Phase 2+; current substrate is in-memory only).
- CRDT-backed multi-node sync (per ADR 0028).
- Marketplace distribution + clone/extend/alter governance enforcement (Phase 4).
- Kitchen-sink demo page (deferred per the same pattern as Properties / Equipment / Inspections first-slice PRs).

## See also

- [Foundation.Taxonomy docs](../../apps/docs/foundation/taxonomy/overview.md)
- [ADR 0056](../../docs/adrs/0056-foundation-taxonomy-substrate.md)
- [Starter taxonomy charters](../../icm/00_intake/output/starter-taxonomies-v1-charters-2026-04-29.md)
- [W#31 hand-off](../../icm/_state/handoffs/foundation-taxonomy-phase1-stage06-handoff.md) + [addendum](../../icm/_state/handoffs/foundation-taxonomy-phase1-stage06-addendum.md)
