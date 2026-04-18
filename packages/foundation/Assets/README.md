# Sunfish.Foundation.Assets — Asset Modeling Kernel Primitives

> Spec §3 primitives 1–3 (Entity Store / Version Store / Audit Log) plus spec §5.6 / §8
> (temporal hierarchy, split / merge / re-parent). Shipped in **Platform Phase A**.

## Architecture

```
┌────────────────────────────────────────────────────────────────────────────┐
│  Sunfish.Foundation.Assets                                                  │
│                                                                             │
│    ┌─────────────┐   ┌──────────────┐   ┌────────────┐   ┌───────────────┐  │
│    │ IEntityStore│   │ IVersionStore│   │ IAuditLog  │   │IHierarchyServ.│  │
│    └──────┬──────┘   └──────┬───────┘   └─────┬──────┘   └──────┬────────┘  │
│           │                 │                 │                  │           │
│           └────────┬────────┴─────────────────┴──────────────────┘           │
│                    │                                                         │
│           ┌────────▼─────────┐          ┌──────────────────────┐             │
│           │ InMemoryAsset    │          │ HierarchyOperations  │             │
│           │ Storage (shared) │          │ (Split/Merge/Reparent│             │
│           └──────────────────┘          └──────────────────────┘             │
└────────────────────────────────────────────────────────────────────────────┘
```

- **Entities** carry a `SchemaId`, a materialized current body, and a pointer to the tip
  of the append-only version log.
- **Versions** form a SHA-256 hash chain — each version's hash is a function of
  `(parent_hash, canonical(body), valid_from)`. Inspired by Automerge's change model
  without taking a runtime dependency on Automerge (plan `D-CRDT-ROUTE`).
- **Audit log** is hash-chained per entity. Records carry a nullable `Signature`
  reserved for Phase B (Ed25519 signing); the SHA-256 chain is sufficient to
  detect tampering on its own.
- **Hierarchy** is materialized as temporal edges plus a closure table; every closure
  row carries a `(ValidFrom, ValidTo)` range. Read queries accept an optional `asOf`
  timestamp to walk the as-of tree (spec §8.1).

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;

var services = new ServiceCollection();
services.AddSunfishAssetsInMemory();
var sp = services.BuildServiceProvider();

var entities = sp.GetRequiredService<IEntityStore>();
var audit    = sp.GetRequiredService<IAuditLog>();
var hierarchy = sp.GetRequiredService<IHierarchyService>();
var ops      = sp.GetRequiredService<HierarchyOperations>();

using var body = JsonDocument.Parse("""{"floors":10,"address":"42 Main St"}""");
var id = await entities.CreateAsync(
    new SchemaId("building.v1"),
    body,
    new CreateOptions(
        Scheme: "building",
        Authority: "acme-rentals",
        Nonce: "42",
        Issuer: new ActorId("pm"),
        Tenant: new TenantId("acme")));
```

## Worked Example — Building Split (spec §8.2, §8.9)

The test fixture `BuildingSplitScenarioTests` exercises this end-to-end:

```
2020-01-01   Mint Building:42 (10 floors, 120 units).
2022-06-15   Roof replaced — Roof:42-v1 SupersededBy Roof:42-v2.
2024-03-10   Correction: floors 10 → 12 (UpdateAsync + Op.Correct audit).
2026-05-01   Split: Building:42 → Building:42-north (60 units) + Building:42-south (60 units).
```

Historical queries into this timeline return the right state:

- `GetAsync(building, AsOf: 2022-01-01)` → `floors == 10`, 120 children.
- `GetAsync(building, AsOf: 2024-12-31)` → `floors == 12`, 120 children.
- `GetAsync(building, AsOf: 2026-09-01)` → `null` (tombstoned); north + south each have 60 children.
- `audit.QueryAsync(Entity: building)` → `[Mint, Correct, Split]`.

## Relationship to `Sunfish.Foundation.Blobs`

Entity bodies store **CID references**, not inline bytes. The `BlobReferenceRoundTripTests`
fixture demonstrates the pattern: caller computes `Cid.FromBytes(content)`, stores the
bytes in the chosen blob store (filesystem today; S3/Postgres later), and embeds the
`cid` string inside the entity body JSON. Entity versions persist unchanged when only the
referenced blob changes — but updating an entity to point at a new CID produces a new
version, and both old and new blobs remain addressable.

## Forward-compatibility

| Seam | Phase A default | Later |
|---|---|---|
| `IEntityValidator` | `NullEntityValidator` (always passes) | Phase A2: schema registry (spec §3.4) |
| `IVersionObserver` | `NullVersionObserver` (ignores events) | Phase C: event bus + Automerge-style sync |
| `IAuditContextProvider` | Returns `ActorId.System` + `TenantId.Default` | Consumers wire HTTP middleware |
| `AuditRecord.Signature` | `null` | Phase B: Ed25519 signing |
| `Version.Diff` | `null` | Later: JSON Patch compact storage |
| `IVersionStore.BranchAsync` / `MergeAsync` | Throw `NotImplementedException` | Phase B: CRDT merge resolver |
| Postgres backend | Deferred (in-memory only in Phase A) | Subsequent phase: EF Core + Npgsql + Testcontainers |

## Testing

```bash
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Assets"
```

Phase A ships ~95 new tests:

- `Common/` — 6 (EntityId, JsonCanonicalizer)
- `Entities/` — 15 (InMemoryEntityStore)
- `Versions/` — 15 (InMemoryVersionStore, hash chain, observer)
- `Audit/` — 18 (HashChain, InMemoryAuditLog)
- `Hierarchy/` — 10 (InMemoryHierarchyService) + 11 (Split/Merge/Reparent)
- `Integration/` — 6 (cross-primitive flow) + 2 (blob roundtrip) + 4 (building-split scenario)
- DI + dependency-guard — 7 (DI extensions + no-Blazor invariant)
