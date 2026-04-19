# Sunfish.Kernel — Kernel Contract Façade

`Sunfish.Kernel` is a thin façade package that exposes the Sunfish platform
spec §3 kernel primitives at the Layer 2 surface called out in §2.3 of the
architecture spec.

It ships as a **virtual package**: no primitive is (re)implemented here. The
already-shipped primitives living in `Sunfish.Foundation` are re-exposed via
`[assembly: TypeForwardedTo]`, and the two primitives that are not yet
implemented ship as empty stub interfaces so downstream work has a stable
landing zone.

This package closes gap **G1** from the platform gap analysis
(`icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md`).

## Why this package exists

Platform spec §2.3 names seven kernel primitives and places them at Layer 2
of the layered architecture. Five of those primitives already ship under
`Sunfish.Foundation.*` sub-namespaces. Two do not ship yet. Without this
package there is no single `packages/kernel/` entry point that corresponds
to the spec's Layer 2 — the kernel is "everywhere and nowhere" in Foundation.

The façade fixes the mismatch without moving any code, without breaking any
consumer, and without fabricating parallel types.

## The seven primitives — shipping status

| Spec § | Primitive            | Status            | Source                                                                          |
|--------|---------------------|-------------------|---------------------------------------------------------------------------------|
| §3.1   | Entity Store         | Forwarded         | `Sunfish.Foundation.Assets.Entities` (`IEntityStore`, `InMemoryEntityStore`, …) |
| §3.2   | Version Store        | Forwarded         | `Sunfish.Foundation.Assets.Versions` (`IVersionStore`, `InMemoryVersionStore`)  |
| §3.3   | Audit Log            | Forwarded         | `Sunfish.Foundation.Assets.Audit` (`IAuditLog`, `AuditRecord`, `HashChain`, …)  |
| §3.4   | Schema Registry      | **Stub** (gap G2) | `Sunfish.Kernel.Schema.ISchemaRegistry`                                          |
| §3.5   | Permission Evaluator | Forwarded         | `Sunfish.Foundation.PolicyEvaluator` (`IPermissionEvaluator`, `Decision`, …)    |
| §3.6   | Event Bus            | **Stub** (gap G3) | `Sunfish.Kernel.Events.IEventBus`                                                |
| §3.7   | Blob Store           | Forwarded         | `Sunfish.Foundation.Blobs` (`IBlobStore`, `Cid`, `FileSystemBlobStore`)         |

Supporting identity types used across primitives are also forwarded:
`EntityId`, `VersionId`, `Instant`, `ActorId`, `TenantId`, `SchemaId`,
`PrincipalId`, `Signature`, and `SignedOperation<T>`.

## How the forwarding works

Types are forwarded at their **shipped** fully-qualified names. A consumer
depending on `Sunfish.Kernel` who writes

```csharp
using Sunfish.Foundation.Assets.Entities;
IEntityStore store = new InMemoryEntityStore(/* … */);
```

picks the types up from the `Sunfish.Kernel` assembly via
`[TypeForwardedTo]`, because the CLR resolves forwarded types transparently.

**Why we did not rename the types into `Sunfish.Kernel.*`.** C#'s assembly
type-forwarding preserves a type's original namespace. Renaming would
require parallel empty interfaces (`Sunfish.Kernel.IEntityStore : Sunfish
.Foundation.Assets.Entities.IEntityStore`) which would force every
registration point in the solution to bridge between two equivalent contracts
— the opposite of "no consumer churn". The shipping Foundation names already
match the spec's short names (`IEntityStore`, `IVersionStore`, `IAuditLog`,
`IPermissionEvaluator`, `IBlobStore`); the only thing that differs is the
sub-namespace, which is noise the spec can be relaxed about. If a future spec
revision wants literally `Sunfish.Kernel.IEntityStore` as the canonical name,
that is a separate (breaking) change and belongs in its own PR.

## Consumer usage

```bash
dotnet add package Sunfish.Kernel
```

```csharp
using Sunfish.Foundation.Assets.Entities;          // Entity Store
using Sunfish.Foundation.Assets.Versions;          // Version Store
using Sunfish.Foundation.Assets.Audit;             // Audit Log
using Sunfish.Foundation.PolicyEvaluator;          // Permission Evaluator
using Sunfish.Foundation.Blobs;                    // Blob Store
using Sunfish.Kernel.Schema;                       // §3.4 stub (G2)
using Sunfish.Kernel.Events;                       // §3.6 stub (G3)
```

A consumer may depend on `Sunfish.Kernel` *or* `Sunfish.Foundation` —
both resolve to the same primitive types. Packages that want to communicate
"we only touch the kernel surface" should take the `Sunfish.Kernel`
dependency; packages that need Foundation's supporting infrastructure
(Crypto, Capabilities, Macaroons, Notifications, …) should depend on
`Sunfish.Foundation` directly.

## Relationship to `packages/foundation/`

- `Sunfish.Foundation` is the **implementation** surface. It ships the
  primitives plus supporting infrastructure (Crypto beyond the identity
  types, Capabilities, Macaroons, Notifications, Services, Data, etc.).
- `Sunfish.Kernel` is the **contract** surface. It exposes only the
  spec §3 primitives — the seven Layer 2 capabilities.
- The two packages **coexist** indefinitely. A future consolidation could
  move primitive implementations into `Sunfish.Kernel` and demote
  Foundation to "supporting infrastructure only", but that is out of scope
  for G1 and would be a breaking change.

## What is deliberately NOT in this package

- `Sunfish.Foundation.Crypto` beyond `PrincipalId`, `Signature`, and
  `SignedOperation<T>` — Crypto is supporting infrastructure per spec §3;
  re-exporting it wholesale would blur the kernel/foundation boundary.
- `Sunfish.Foundation.Capabilities` and `Sunfish.Foundation.Macaroons` —
  capability tokens are Layer 3 (authorization plane), not Layer 2.
- Postgres store implementations — those live in `Sunfish.Foundation
  .Assets.Postgres`, a separate assembly. A consumer that wants Postgres
  stores should add that package directly; forwarding across an additional
  assembly would create a transitive dependency inversion that Layer 2
  should not introduce.

## Links

- Platform spec §2.3 (layering) and §3 (kernel primitives)
- Gap analysis: `icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md`
  - **G1** — this package
  - **G2** — Schema Registry implementation (fills
    `Sunfish.Kernel.Schema.ISchemaRegistry`)
  - **G3** — Event Bus implementation (fills
    `Sunfish.Kernel.Events.IEventBus`)
