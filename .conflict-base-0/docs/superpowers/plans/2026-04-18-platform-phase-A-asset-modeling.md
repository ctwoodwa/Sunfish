# Platform Phase A: Asset Modeling — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

---

## Platform Context

> **⚠ Read this before executing.** Platform Phase A is the **first post-migration platform phase**. The migration phases (1–9) brought Marilo into Sunfish as a renamed codebase with framework-agnostic foundation, UI-Core contracts, Blazor adapter, provider/icon packages, app shell, domain blocks, compat-telerik, kitchen-sink, docs, and the Bridge accelerator. The kernel primitives described in `docs/specifications/sunfish-platform-specification.md` §3 were **not** part of any migration phase — they are forward-looking platform work.
>
> Phase A implements the three kernel primitives most closely tied to **asset modeling** (spec §8): Entity Store (§3.1), Version Store (§3.2), Audit Log (§3.3). It also realizes the asset-hierarchy data model (§5.6, §8.1) and temporal query semantics (§8.6) needed for split / merge / re-parent operations on real-world assets.
>
> **What Phase A does NOT deliver** (and why):
> - **Decentralization** (Ed25519 signing, Keyhive-style capability graphs, federation) — Platform Phase B.
> - **Input modalities** (voice transcription, drone imagery, sensor ingestion) — Platform Phase C.
> - **Federation** (peer-to-peer sync via Automerge-style protocol) — Platform Phase D.
> - **Schema registry (§3.4)** — folded out of Phase A. Reason: the schema registry is logically a separate primitive (its own persistence, its own validator, its own migration engine) and its surface area alone warrants a dedicated plan. Phase A treats schema IDs as opaque strings and defers the registry to Platform Phase A2 or a sibling plan.
> - **Permission Evaluator (§3.5)** — deferred. Phase A's reads and writes go through a null/allow-all evaluator; real authorization is Platform Phase B concern (it must integrate with the Keyhive capability graph anyway, so it is natural to plan alongside the crypto primitives).
> - **Event Bus (§3.6)** — already partially present via migration Phase 5 `blocks-scheduling` / `blocks-tasks`; Phase A does not rewire it. The Version Store emits change notifications via a minimal in-proc `IVersionObserver` seam so Phase C can wire Automerge-style sync later without rework.
> - **BIM integration (§9)** — independent follow-up after Phase A.
>
> **Alignment with spec §4.4 (Phase 2 — Asset Modeling):** spec §4.4 lumps decentralization, attachment store, and asset hierarchy into a single phase. This plan **splits** that phase: Phase A = entity + version + audit + hierarchy + temporal + **existing blob primitive integration** (blobs already shipped as `Sunfish.Foundation.Blobs`); Phase B = crypto + ownership + federation. This split reflects the repo reality that `Sunfish.Foundation.Blobs` shipped on `feat/platform-foundation-blobs` and crypto is genuinely separate work.

**Goal:** Deliver the asset-modeling kernel primitives (Entity Store, Version Store, Audit Log) as a new `Sunfish.Foundation.Assets` namespace within the existing `Sunfish.Foundation` package. Two backend implementations: zero-dependency **in-memory** (for tests and dev) and **PostgreSQL via EF Core** (for production). Support temporal queries (`asOf`) on all reads, append-only versioning, hash-chained audit trails, and parent-child temporal edges with split / merge / re-parent operations. Integrate the already-shipped `Sunfish.Foundation.Blobs` primitive by referencing blobs via `Cid` from entity bodies rather than inlining binary content.

**Architecture:** Keep additions inside `packages/foundation/` as a new `Assets/` subtree (mirrors the existing `Blobs/`, `Notifications/`, `Authorization/` subtrees). All new code is framework-agnostic .NET — **no Blazor, no ASP.NET**, preserving the Phase 2 `HasNoBlazorDependency` invariant. EF Core + Npgsql is added as a transitive dependency only when the `Sunfish.Foundation.Assets.Postgres` code paths are used; the in-memory backend has zero external dependencies beyond what Foundation already ships.

**Tech Stack:** .NET 10, C# 13, System.Text.Json (entity bodies), System.Security.Cryptography (hashing + hash-chain verification), EF Core 10 (Npgsql provider 10.x), PostgreSQL 16+ with `tstzrange` temporal support, Testcontainers.PostgreSQL (integration tests), xUnit 2.9.x, NSubstitute 5.3.x (optional — for any service mocks).

**Prerequisites:**
- `Sunfish.Foundation` package green-building with 16 tests (post-Blobs) on `main`.
- `Directory.Build.props` enforces `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — all new code must compile warning-free.
- `Sunfish.Foundation.Blobs` is already present (`Blobs/Cid.cs`, `Blobs/IBlobStore.cs`, `Blobs/FileSystemBlobStore.cs`).
- Root `Sunfish.slnx` registers `packages/foundation/Sunfish.Foundation.csproj` + `packages/foundation/tests/tests.csproj` — no solution changes needed.
- Docker Desktop (or podman) available on dev machine + CI for Testcontainers to run Postgres.

---

## Scope

**In scope:**
- `Sunfish.Foundation.Assets.Entities` — `IEntityStore` + DTOs + two backends.
- `Sunfish.Foundation.Assets.Versions` — `IVersionStore` + DTOs + two backends.
- `Sunfish.Foundation.Assets.Audit` — `IAuditLog` + DTOs + two backends (hash-chained).
- `Sunfish.Foundation.Assets.Hierarchy` — temporal parent-child edges + closure-table materialization + split / merge / re-parent operations.
- `Sunfish.Foundation.Assets.Temporal` — `VersionSelector`, `AsOf` helpers, `Instant` wrapper (if DateTimeOffset is insufficient).
- `Sunfish.Foundation.Assets.Postgres` — EF Core DbContext, entity type configurations, migrations.
- `Sunfish.Foundation.Assets.InMemory` — in-memory backends (for tests and dev).
- Integration with `Sunfish.Foundation.Blobs.Cid`: entity bodies containing large binary references carry CID strings.
- Test suites: in-memory unit tests (~80 tests across the four primitives) + Postgres integration tests via Testcontainers (~25 tests).
- Documentation: `packages/foundation/Assets-README.md` with primitive walk-through + the worked "building split" example.

**Out of scope — deferred:**
- **Cryptographic signing of audit records.** Phase A's hash chain is SHA-256-only (`prev_hash` → `record_hash`). Ed25519 signatures are Phase B. The `AuditRecord` DTO reserves a `Signature?` field (nullable byte[]) for forward compatibility.
- **Schema validation on entity write.** Phase A stores `body` as opaque `JsonDocument`; no JSON-Schema validation. The schema registry (§3.4) will add this later as a pre-commit interceptor.
- **Authorization checks.** No `IPermissionEvaluator` wiring; callers with repository access can mutate anything. Phase B adds this.
- **Event publishing.** No `IEventBus` integration. The Version Store exposes an `IVersionObserver` hook that Phase C can wire to the event bus.
- **JSON Patch diffs.** Spec §3.1 calls for `UpdateAsync(id, JsonPatch patch, ...)`. Phase A stores the **full body** on each version; the diff field (`entity_versions.diff`) is left nullable and unpopulated. JSON Patch is a compact-storage optimization, deferred.
- **Branch / Merge beyond linear history.** Spec §3.2 mentions `BranchAsync` + `MergeAsync`. Phase A implements linear append-only versioning. Branching surfaces as API stubs that throw `NotImplementedException` ("Planned for Platform Phase B — see §8 CRDT-inspired merge semantics"). Rationale: a usable branch/merge requires the CRDT-lite change log (D-CRDT-ROUTE below), which is a meaningful design effort on its own.
- **Tenant isolation via Postgres RLS.** Spec §5.1 mandates row-level security policies. Phase A adds a `tenant_id` column and `WHERE tenant_id = @current_tenant` filter at the application layer; RLS policies come with Phase B.
- **Cold-storage tiering** (spec §8.8). Phase A stores all history in the same Postgres tables.
- **Bloom filters for "has-this-hash-ever-existed"** (spec §8.8). Deferred.
- **Non-PostgreSQL production backends.** No SQLite, SQL Server, or document-DB backends in this phase.

---

## Key Decisions

**D-CRDT-ROUTE** (locked by `docs/specifications/research-notes/automerge-evaluation.md`, §3.1 + §8): **Build Sunfish-native change-log semantics inspired by Automerge; do NOT integrate the Automerge library.** Four rationale points from the evaluation:

1. No first-class .NET binding for Automerge (as of April 2026) — integration is P/Invoke over `automerge-c` or a Node.js sidecar, neither of which matches Foundation's zero-external-runtime profile.
2. The spec's temporal model (every version has a `valid_from` / `valid_to`) is richer than Automerge's "change N" model; Automerge treats all history as a DAG but doesn't carry temporal validity on its own.
3. Bridge is server-authoritative, not local-first. Automerge's sync protocol benefit (delta-sync between peers) is not exercised by Phase A's single-node assumption.
4. Integration cost is meaningful; the evaluation explicitly recommends "adopt the ideas, not the library."

**Practical consequence:** Phase A stores each version as `(entity_id, sequence, parent_seq, hash, body, valid_from, valid_to)`. Hashes are deterministic SHA-256 over `(parent_hash, canonical-json(body), valid_from-iso)`. Merge semantics — when Phase B introduces branching — will be: the caller supplies a `MergeResolver` delegate; Phase A ships `ThreeWayJsonPatchResolver` as the default reference resolver. No library dependency.

**D-VERSION-STORE-SHAPE** (spec §3.8 hint): **Append-only change log + materialized "current body" cache**, not pure event-sourced with projection-on-read. Rationale:

1. Spec §3.8 is explicit: *"Current body materialized on entities row for fast reads."*
2. Property-management workloads are read-heavy (renewals, inspections browsing history). Reconstructing body from a change log on every read adds latency.
3. The materialization is an optimization; the change log is still the authoritative log. A rebuild from change log is always possible (covered in a rebuild tool shipped in Task 7).

**Shape:**
- `entities` table: `id`, `schema_id`, `tenant_id`, `current_version`, `body` (JSONB, current), `created_at`, `updated_at`, `deleted_at`.
- `entity_versions` table: `entity_id`, `sequence`, `parent_seq`, `hash`, `body` (JSONB, authoritative snapshot at that version), `valid_from`, `valid_to`, `author`, `signature` (nullable until Phase B).
- On `UpdateAsync`: insert a new row in `entity_versions`, UPDATE `entities.body` + `entities.current_version` + `entities.updated_at` in the same transaction.

**D-TEMPORAL-QUERIES**: **PostgreSQL `tstzrange` for Postgres backend; application-level `valid_from` / `valid_to` tuple for in-memory.** Rationale:

1. `tstzrange` is a first-class Postgres type with GIST indexing (`ix_versions_validity` in spec §5.1). Queries like `WHERE valid_range @> now()` are log-N.
2. In-memory has no index engine; application-level filtering is `O(n)` per entity's history but entities typically have <100 versions in memory, so this is trivially fast.
3. EF Core 10 with Npgsql supports `NpgsqlRange<DateTime>` via `HasColumnType("tstzrange")`.

**Design:** Keep the DTO surface as `(DateTimeOffset ValidFrom, DateTimeOffset? ValidTo)` at the public API; the Postgres backend translates to `NpgsqlRange<DateTime>` in a `HasConversion` mapping. Public API never exposes Npgsql types.

**D-HIERARCHY**: **Materialized-path + closure table for read efficiency; temporal edges carry `(valid_from, valid_to)` on closure rows.** Rationale:

1. Adjacency-list alone: cheap writes, O(depth) reads for ancestor queries. Spec §8's "as-of tree" query requires fetching all descendants efficiently.
2. Nested-set: cheap reads, horrific writes on split / merge / re-parent.
3. Closure table: every ancestor-descendant pair is materialized. Writes scale O(descendants × ancestors) but splits / merges are bounded (`N`-subtree, typically ≤ 120 units per building per spec §8 benchmark) and runtime-cheap.
4. Materialized-path on the edge itself gives a free ancestor-chain display string ("Site:42/Building:42/Floor:3/Unit:3b") for UI breadcrumbs.

**Temporal closure:** each closure row carries `(valid_from, valid_to)`. A split operation (a) sets `valid_to = now()` on all closure rows where the old parent is an ancestor, and (b) inserts new closure rows for the new parent. This is O(descendants × ancestors) per split — typically ≤ 120 × 4 = 480 rows for a building split — well within acceptable.

**D-EF-CORE**: **EF Core 10 + Npgsql provider 10.x**. Matches the rest of the repo (Bridge uses EF Core 10). Migrations live in `packages/foundation/Assets.Postgres/Migrations/`. Use `dotnet ef migrations add` via a **DesignTimeDbContextFactory** pattern (same pattern as Bridge's `Sunfish.Bridge.Data`).

**Versions added to `Directory.Packages.props`:**
- `Microsoft.EntityFrameworkCore` 10.0.6
- `Microsoft.EntityFrameworkCore.Design` 10.0.6
- `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.x (latest stable at plan execution time)
- `Testcontainers.PostgreSql` 4.x (for integration tests)

**D-TEST-STRATEGY**: **In-memory backend unit-tested directly; Postgres backend integration-tested via Testcontainers.** Rationale:

1. Testcontainers spins up an ephemeral Postgres 16 container per test fixture. Ships the container image as part of the test run; no shared CI database to pollute.
2. The in-memory backend is the primary API surface for tests in *other* packages (Bridge, kitchen-sink). It gets ~80 direct tests + is battle-tested by every downstream consumer.
3. The Postgres backend gets ~25 targeted integration tests that cover: migration up/down, CRUD, temporal queries, closure-table maintenance, hash-chain verification, Testcontainers fixture lifecycle.

**Alternative considered:** a hardcoded test connection string via environment variable. Rejected: parallel test runs collide; CI needs a managed Postgres; developer setup cost is higher than `docker pull postgres:16`.

**D-NULLABLE-SIGNATURES**: Phase A leaves `AuditRecord.Signature` nullable and does not sign records. Rationale:

1. Signing requires a key-management story (Keyhive or PKI). That's Phase B.
2. Hash-chain integrity is still enforceable without signatures (tamper = hash mismatch).
3. Forward-compatibility: Phase B populates the field; Phase A records are retroactively signable via a "sign-all-historical" migration.

**D-ENTITY-ID-SHAPE**: Adopt spec §3.1's `EntityId(Scheme, Authority, LocalPart)` record struct. Canonical string form: `"{Scheme}:{Authority}/{LocalPart}"`. Example: `"entity:acme-rentals/building-42"`. Postgres stores as `TEXT PRIMARY KEY`; in-memory uses the record struct directly as a dictionary key.

**D-NO-BLAZOR**: Reaffirm the Phase 2 invariant. `Sunfish.Foundation` must not reference any `Microsoft.AspNetCore.*` or `Microsoft.JSInterop.*` package. The integration test in `packages/ui-core/tests/` that filters on `HasNoBlazorDependency` must continue to pass — add a parallel assertion in `packages/foundation/tests/` to be safe.

**D-ASYNC-ENUMERABLE-POSTGRES**: Spec §3.1 uses `IAsyncEnumerable<Entity> QueryAsync(...)`. EF Core 10's `AsAsyncEnumerable()` surfaces query results via `IAsyncEnumerable`. This maps cleanly. For in-memory, implement via `async IAsyncEnumerable<T>` iterator method over the in-memory dictionary snapshot.

**D-EXTENSIBILITY-SEAMS**: Reserve three extension-points as nullable services injected via DI:
1. `IEntityValidator?` — pre-commit body validation (will be wired to schema registry in the future).
2. `IVersionObserver?` — post-commit hook for version changes (will be wired to event bus).
3. `IAuditContextProvider?` — supplies `ActorId` + `TenantId` from ambient request context (will be wired to HTTP middleware by consumers).

Phase A ships null-object defaults for each; consumers can override via DI.

**D-INSTANT-TYPE**: Use `DateTimeOffset` for all temporal fields (`valid_from`, `valid_to`, `at`). Rationale:

1. NodaTime's `Instant` (referenced in spec §3.1) is a defensible choice but adds a transitive dependency.
2. `DateTimeOffset` is built-in, preserves timezone, round-trips cleanly to `timestamptz`.
3. If NodaTime is later demanded, the public DTOs can grow an overload without breaking callers.

Alias: `public readonly record struct Instant(DateTimeOffset Value)` — thin wrapper to match spec vocabulary, implicit converters both ways. This lets downstream docs/code read spec-idiomatically while the runtime type is `DateTimeOffset`.

---

## File Structure

```
packages/foundation/
  Assets/                                                    ← NEW: asset-modeling kernel primitives
    README.md                                                ← overview + worked "building split" example
    Assets.csproj                                            ← NOT a new csproj — Assets is folder-only within Sunfish.Foundation

    Common/
      EntityId.cs                                            ← record struct (Scheme, Authority, LocalPart)
      VersionId.cs                                           ← record struct (EntityId, Sequence, Hash)
      Instant.cs                                             ← thin DateTimeOffset wrapper
      JsonCanonicalizer.cs                                   ← deterministic JSON canonicalization for hashing
      SchemaId.cs                                            ← opaque string wrapper (registry lands later)
      ActorId.cs                                             ← opaque string wrapper
      TenantId.cs                                            ← opaque string wrapper

    Entities/
      IEntityStore.cs                                        ← contract (spec §3.1)
      Entity.cs                                              ← DTO
      EntityQuery.cs                                         ← filter DTO
      CreateOptions.cs, UpdateOptions.cs, DeleteOptions.cs   ← option records
      VersionSelector.cs                                     ← (Explicit?, AsOf?, Latest)
      IEntityValidator.cs                                    ← nullable extension seam (D-EXTENSIBILITY-SEAMS)
      NullEntityValidator.cs                                 ← null-object default
      InMemoryEntityStore.cs                                 ← zero-dep backend (Task 1)

    Versions/
      IVersionStore.cs                                       ← contract (spec §3.2)
      Version.cs                                             ← DTO
      BranchOptions.cs, MergeOptions.cs                      ← option records (branch/merge = NotImplementedException in Phase A)
      IVersionObserver.cs                                    ← nullable post-commit hook
      NullVersionObserver.cs
      InMemoryVersionStore.cs

    Audit/
      IAuditLog.cs                                           ← contract (spec §3.3)
      AuditRecord.cs                                         ← DTO
      AuditQuery.cs                                          ← filter DTO
      AuditId.cs                                             ← record struct (long, base32 form)
      Op.cs                                                  ← enum: Mint, Read, Write, Delete, Transfer, Delegate, Revoke, Attest, Correct, Split, Merge, Reparent
      HashChain.cs                                           ← compute/verify helpers
      IAuditContextProvider.cs                               ← nullable ambient-context source
      NullAuditContextProvider.cs
      InMemoryAuditLog.cs

    Hierarchy/
      IHierarchyService.cs                                   ← public façade over edges + closure
      EntityEdge.cs                                          ← DTO
      EdgeKind.cs                                            ← enum: ChildOf, References, SupersededBy
      ClosureEntry.cs                                        ← DTO (ancestor, descendant, depth, valid_from, valid_to)
      HierarchyOperations.cs                                 ← SplitAsync, MergeAsync, ReparentAsync helpers
      TemporalSnapshot.cs                                    ← "as-of tree" projection DTO
      InMemoryHierarchyService.cs

    Temporal/
      TemporalRange.cs                                       ← (ValidFrom, ValidTo?) struct
      AsOfExtensions.cs                                      ← helpers: IsValidAt, OverlapsWith

    Postgres/                                                ← EF Core backend (Task 6)
      SunfishAssetsDbContext.cs
      DesignTimeDbContextFactory.cs
      Configurations/
        EntityConfiguration.cs
        EntityVersionConfiguration.cs
        AuditRecordConfiguration.cs
        EntityEdgeConfiguration.cs
        ClosureEntryConfiguration.cs
      PostgresEntityStore.cs
      PostgresVersionStore.cs
      PostgresAuditLog.cs
      PostgresHierarchyService.cs
      Migrations/
        YYYYMMDDHHMMSS_InitialAssetsSchema.cs
        YYYYMMDDHHMMSS_InitialAssetsSchema.Designer.cs
        SunfishAssetsDbContextModelSnapshot.cs
      NpgsqlConverters.cs                                    ← TemporalRange ↔ NpgsqlRange<DateTime>

    ServiceCollectionExtensions.cs                           ← AddSunfishAssetsInMemory(), AddSunfishAssetsPostgres(connStr)

  tests/
    Assets/                                                  ← NEW: all Phase A tests live here
      Common/
        EntityIdTests.cs
        JsonCanonicalizerTests.cs
      Entities/
        InMemoryEntityStoreTests.cs                          ← ~15-20 tests
      Versions/
        InMemoryVersionStoreTests.cs                         ← ~15 tests
      Audit/
        InMemoryAuditLogTests.cs                             ← ~12 tests
        HashChainTests.cs                                    ← ~6 tests
      Hierarchy/
        InMemoryHierarchyServiceTests.cs                     ← ~10 tests
        SplitMergeReparentTests.cs                           ← ~8 tests (spec §8 scenarios)
      Integration/
        EntityVersionAuditFlowTests.cs                       ← ~6 tests (cross-primitive)
        BuildingSplitScenarioTests.cs                        ← ~4 tests (spec §8.2 worked example)
      Postgres/                                              ← ~25 Testcontainers tests
        PostgresTestFixture.cs                               ← Testcontainers startup / teardown
        PostgresEntityStoreTests.cs
        PostgresVersionStoreTests.cs
        PostgresAuditLogTests.cs
        PostgresHierarchyServiceTests.cs
        MigrationRoundTripTests.cs
```

**Files to update outside `packages/foundation/Assets/`:**
- `Directory.Packages.props` — add EF Core 10, Npgsql EF provider, Testcontainers.PostgreSql versions.
- `packages/foundation/Sunfish.Foundation.csproj` — add EF Core + Npgsql `<PackageReference>` (optional via multi-targeting; see Task 6 Step 1).
- `packages/foundation/tests/tests.csproj` — add Testcontainers.PostgreSql reference; enable parallel test collection.
- `README.md` (repo root) — add a one-line reference to `packages/foundation/Assets/README.md`.
- `Sunfish.slnx` — no changes (Foundation is already registered).

---

## Task 0 — Branch setup

- [ ] **Step 1:** Branch off `main` (or the latest shipped branch containing the Blobs work).

```bash
cd C:/Projects/Sunfish
git fetch origin
git checkout -b feat/platform-phase-A-asset-modeling origin/main
# If the Blobs branch hasn't merged to main yet, use it instead:
# git checkout -b feat/platform-phase-A-asset-modeling origin/feat/platform-foundation-blobs
```

- [ ] **Step 2:** Verify baseline build + tests.

```bash
dotnet build C:/Projects/Sunfish/Sunfish.slnx
dotnet test C:/Projects/Sunfish/packages/foundation/tests/tests.csproj --no-build
```

Expected: build 0 errors, 16 foundation tests green.

- [ ] **Step 3:** Add the three EF Core / testing packages to central version file.

Update `Directory.Packages.props`:

```xml
<PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.6" />
<PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.6" />
<PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.6" />
<PackageVersion Include="Testcontainers.PostgreSql" Version="4.4.0" />
```

(Verify the Npgsql EF provider's latest compatible version at the time of execution via `dotnet list package --outdated` — the version above is indicative.)

- [ ] **Step 4:** Commit scaffolding.

```bash
git add Directory.Packages.props
git commit -m "chore(foundation): add EF Core 10 + Npgsql + Testcontainers package pins for phase A"
```

---

## Task 1 — IEntityStore + InMemoryEntityStore + tests (~15-20 tests)

**Files:**
- Create: `packages/foundation/Assets/Common/EntityId.cs`
- Create: `packages/foundation/Assets/Common/Instant.cs`
- Create: `packages/foundation/Assets/Common/SchemaId.cs`
- Create: `packages/foundation/Assets/Common/ActorId.cs`
- Create: `packages/foundation/Assets/Common/TenantId.cs`
- Create: `packages/foundation/Assets/Common/JsonCanonicalizer.cs`
- Create: `packages/foundation/Assets/Entities/IEntityStore.cs`
- Create: `packages/foundation/Assets/Entities/Entity.cs`
- Create: `packages/foundation/Assets/Entities/EntityQuery.cs`
- Create: `packages/foundation/Assets/Entities/VersionSelector.cs`
- Create: `packages/foundation/Assets/Entities/CreateOptions.cs`
- Create: `packages/foundation/Assets/Entities/UpdateOptions.cs`
- Create: `packages/foundation/Assets/Entities/DeleteOptions.cs`
- Create: `packages/foundation/Assets/Entities/IEntityValidator.cs` + `NullEntityValidator.cs`
- Create: `packages/foundation/Assets/Entities/InMemoryEntityStore.cs`
- Create: `packages/foundation/tests/Assets/Common/EntityIdTests.cs`
- Create: `packages/foundation/tests/Assets/Common/JsonCanonicalizerTests.cs`
- Create: `packages/foundation/tests/Assets/Entities/InMemoryEntityStoreTests.cs`

- [ ] **Step 1:** Create `Common/EntityId.cs` — `readonly record struct EntityId(string Scheme, string Authority, string LocalPart)` with `ToString()` = `"{Scheme}:{Authority}/{LocalPart}"` and a `Parse(string)` inverse. Throws `FormatException` on malformed input.

- [ ] **Step 2:** Create `Common/Instant.cs`, `SchemaId.cs`, `ActorId.cs`, `TenantId.cs` — minimal record-struct wrappers around `DateTimeOffset` or `string` with implicit converters + `ToString()`.

- [ ] **Step 3:** Create `Common/JsonCanonicalizer.cs`. Purpose: byte-stable serialization of a `JsonDocument` for hash computation. Algorithm: sorted keys, no whitespace, UTF-8. Single method `byte[] ToCanonicalBytes(JsonDocument doc)`. Same JSON value → same bytes regardless of input key order or whitespace.

- [ ] **Step 4:** Create `Entities/IEntityStore.cs` matching spec §3.1 with the full-body `UpdateAsync` deviation (D-VERSION-STORE-SHAPE). Full signature:

```csharp
public interface IEntityStore
{
    Task<Entity?> GetAsync(EntityId id, VersionSelector version = default, CancellationToken ct = default);
    Task<EntityId> CreateAsync(SchemaId schema, JsonDocument body, CreateOptions options, CancellationToken ct = default);
    Task<VersionId> UpdateAsync(EntityId id, JsonDocument newBody, UpdateOptions options, CancellationToken ct = default);
    Task DeleteAsync(EntityId id, DeleteOptions options, CancellationToken ct = default);
    IAsyncEnumerable<Entity> QueryAsync(EntityQuery query, CancellationToken ct = default);
}
```

Document the spec-deviation in a `<remarks>` block referencing this plan's D-VERSION-STORE-SHAPE.

- [ ] **Step 5:** Implement `InMemoryEntityStore`.

Structure:
- `ConcurrentDictionary<EntityId, EntityRecord>` where `EntityRecord` holds current body + full version history list.
- `CreateAsync`: derive `EntityId` deterministically from `(schema, nonce, issuer)` per spec §3.1 semantic rule (SHA-256 of the concatenation, take 16 bytes, base32-encode for `LocalPart`). Idempotent: returning the same EntityId on a second call with the same triple is not an error — it returns the existing ID. A different body with the same triple throws `IdempotencyConflictException`.
- `UpdateAsync`: append a new version, update current body. Use `lock` or `ConcurrentDictionary.AddOrUpdate` to serialize writes per entity.
- `DeleteAsync`: insert a tombstone version (body = `{}`, `deleted_at` set) — preserves history.
- `QueryAsync`: naive scan over dictionary + filter — acceptable for in-memory.

Each mutation triggers an internal `EntityVersionAppended` event the accompanying `InMemoryVersionStore` subscribes to (Task 2). Use an `IVersionObserver` injected via constructor; default null-object in DI.

- [ ] **Step 6:** Write tests in `tests/Assets/Entities/InMemoryEntityStoreTests.cs`. Cover:

1. `CreateAsync_ReturnsIdempotentId_OnSameNonceAndSchema`
2. `CreateAsync_ThrowsIdempotencyConflict_OnDifferentBodyWithSameNonce`
3. `CreateAsync_GeneratesDeterministicId_FromSchemaNonceIssuer`
4. `GetAsync_ReturnsNull_OnUnknownId`
5. `GetAsync_ReturnsLatestByDefault`
6. `GetAsync_ReturnsExplicitVersion_WhenSpecified`
7. `GetAsync_ReturnsAsOfVersion_WhenAsOfSpecified`
8. `UpdateAsync_AppendsNewVersion`
9. `UpdateAsync_IncrementsSequence`
10. `UpdateAsync_UpdatesCurrentBody`
11. `UpdateAsync_ThrowsConcurrencyException_OnOptimisticLockFailure` (if `UpdateOptions.ExpectedVersion` is provided and mismatched)
12. `DeleteAsync_InsertsTombstoneVersion`
13. `DeleteAsync_MakesGetReturnNull_ForLatest`
14. `DeleteAsync_StillReturnsEntity_ForAsOfBeforeDelete`
15. `QueryAsync_FiltersByTenant`
16. `QueryAsync_FiltersBySchema`
17. `QueryAsync_ExcludesDeleted_ByDefault`
18. `QueryAsync_IncludesDeleted_WhenRequested`
19. `EntityId_Parse_RoundTripsToString` (in `EntityIdTests.cs`)
20. `JsonCanonicalizer_ProducesSameBytes_ForSameLogicalJsonWithDifferentKeyOrder` (in `JsonCanonicalizerTests.cs`)

Expected: 18 `InMemoryEntityStore` tests + 2 supporting = **20 tests**.

- [ ] **Step 7:** Build + test.

```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Assets.Entities|Assets.Common"
```

Expected: 0 errors, 0 warnings, 20 new tests green. Baseline 16 tests also still green (total 36).

- [ ] **Step 8:** Commit.

```bash
git add packages/foundation/Assets/Common/ packages/foundation/Assets/Entities/ \
        packages/foundation/tests/Assets/Common/ packages/foundation/tests/Assets/Entities/
git commit -m "feat(foundation/assets): add IEntityStore + InMemoryEntityStore (spec §3.1)"
```

---

## Task 2 — IVersionStore + InMemoryVersionStore + tests (~15 tests)

**Files:**
- Create: `packages/foundation/Assets/Common/VersionId.cs`
- Create: `packages/foundation/Assets/Versions/IVersionStore.cs`
- Create: `packages/foundation/Assets/Versions/Version.cs`
- Create: `packages/foundation/Assets/Versions/BranchOptions.cs`
- Create: `packages/foundation/Assets/Versions/MergeOptions.cs`
- Create: `packages/foundation/Assets/Versions/IVersionObserver.cs` + `NullVersionObserver.cs`
- Create: `packages/foundation/Assets/Versions/InMemoryVersionStore.cs`
- Create: `packages/foundation/tests/Assets/Versions/InMemoryVersionStoreTests.cs`

- [ ] **Step 1:** Create `Common/VersionId.cs` — `readonly record struct VersionId(EntityId Entity, int Sequence, string Hash)` with `ToString()` = `"{Entity}@{Sequence}:{Hash[..12]}"` (short hash for log readability).

- [ ] **Step 2:** Create `Versions/IVersionStore.cs` matching spec §3.2: `GetVersionAsync(VersionId)` → `Version?`, `GetHistoryAsync(EntityId)` → `IAsyncEnumerable<Version>`, `GetAsOfAsync(EntityId, Instant)` → `Version?`, `BranchAsync(VersionId, BranchOptions)` → `VersionId` (throws `NotImplementedException` in Phase A), `MergeAsync(VersionId, VersionId, MergeOptions)` → `VersionId` (throws in Phase A).

- [ ] **Step 3:** Create `Versions/Version.cs` — `sealed record Version(VersionId Id, VersionId? ParentId, JsonDocument Body, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo, ActorId Author, byte[]? Signature, JsonDocument? Diff)`. `Signature` null in Phase A → Phase B; `Diff` null in Phase A → reserved for JSON Patch optimization.

- [ ] **Step 4:** Implement `InMemoryVersionStore`.

Key algorithm — **version hash**:

```
hash(v) = SHA256(
    parent_hash (empty-string if root) ||
    canonicalJson(body) ||
    valid_from.ToString("O")
)
```

The hash is deterministic: same inputs → same hash. This matches Automerge's "change N is identified by the hash of change N-1 concat the change itself" pattern (D-CRDT-ROUTE).

Storage: `ConcurrentDictionary<EntityId, ImmutableList<Version>>`.

`GetAsOfAsync(entity, t)`: linear scan of history where `ValidFrom <= t < (ValidTo ?? MaxValue)`. O(n) per call — trivially fast for in-memory.

`BranchAsync`, `MergeAsync`: throw `NotImplementedException("Phase A ships linear history only. Branch/merge lands in Platform Phase B; see plan D-CRDT-ROUTE.")`.

- [ ] **Step 5:** Wire `InMemoryEntityStore` ↔ `InMemoryVersionStore` via a shared `InMemoryAssetStorage` container object. Both stores hold references to the same underlying `ConcurrentDictionary` so the "current body" cache and "version history" list stay in sync by construction.

- [ ] **Step 6:** Write tests in `InMemoryVersionStoreTests.cs`:

1. `GetVersionAsync_ReturnsVersion_ForValidId`
2. `GetVersionAsync_ReturnsNull_ForUnknownId`
3. `GetHistoryAsync_ReturnsAllVersions_InOrder`
4. `GetHistoryAsync_ReturnsEmptyForUnknownEntity`
5. `GetAsOfAsync_ReturnsVersionValidAtInstant`
6. `GetAsOfAsync_ReturnsEarliestVersionAcrossMultiple_WhenExactMatch`
7. `GetAsOfAsync_ReturnsNull_WhenAsOfIsBeforeFirstVersion`
8. `GetAsOfAsync_ReturnsLatestVersion_WhenAsOfIsAfterAllVersions`
9. `VersionHash_IsDeterministic_ForSameInputs`
10. `VersionHash_DiffersFromParent_ForAnyBodyChange`
11. `ParentHash_IsCorrectlyChained_AcrossMultipleVersions`
12. `BranchAsync_ThrowsNotImplemented`
13. `MergeAsync_ThrowsNotImplemented`
14. `ValidityRange_IsContiguous_AcrossVersions` (when version N+1 is created, version N's `ValidTo` becomes version N+1's `ValidFrom`)
15. `Observer_IsNotified_OnVersionAppend`

Expected: **15 tests**.

- [ ] **Step 7:** Build + test.

```bash
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Assets.Versions"
```

Expected: 15 new tests green. Running total after Task 1 + 2: 36 + 15 = **51 tests**.

- [ ] **Step 8:** Commit.

```bash
git add packages/foundation/Assets/Common/VersionId.cs packages/foundation/Assets/Versions/ \
        packages/foundation/tests/Assets/Versions/
git commit -m "feat(foundation/assets): add IVersionStore + InMemoryVersionStore (spec §3.2)"
```

---

## Task 3 — IAuditLog + InMemoryAuditLog + hash-chain verification (~18 tests)

**Files:**
- Create: `packages/foundation/Assets/Audit/IAuditLog.cs`
- Create: `packages/foundation/Assets/Audit/AuditRecord.cs`
- Create: `packages/foundation/Assets/Audit/AuditId.cs`
- Create: `packages/foundation/Assets/Audit/AuditQuery.cs`
- Create: `packages/foundation/Assets/Audit/Op.cs`
- Create: `packages/foundation/Assets/Audit/HashChain.cs`
- Create: `packages/foundation/Assets/Audit/IAuditContextProvider.cs` + `NullAuditContextProvider.cs`
- Create: `packages/foundation/Assets/Audit/InMemoryAuditLog.cs`
- Create: `packages/foundation/tests/Assets/Audit/InMemoryAuditLogTests.cs`
- Create: `packages/foundation/tests/Assets/Audit/HashChainTests.cs`

- [ ] **Step 1:** Create `Audit/Op.cs` — `enum Op { Mint, Read, Write, Delete, Transfer (Phase B), Delegate (Phase B), Revoke (Phase B), Attest (Phase B), Correct (spec §8.5), Split (§8.2), Merge (§8.3), Reparent (§8.4) }`.

- [ ] **Step 2:** Create `Audit/AuditRecord.cs`:

```csharp
public sealed record AuditRecord(
    AuditId Id, EntityId EntityId, VersionId? VersionId, Op Op,
    ActorId Actor, TenantId Tenant, DateTimeOffset At,
    string? Justification, JsonDocument Payload,
    byte[]? Signature,  // nullable Phase A, populated Phase B (D-NULLABLE-SIGNATURES)
    AuditId? Prev,
    string Hash);       // SHA-256 over (prev.Hash || canonical(fields))
```

- [ ] **Step 3:** Create `Audit/IAuditLog.cs` matching spec §3.3: `AppendAsync(AuditRecord)` → `AuditId`; `QueryAsync(AuditQuery)` → `IAsyncEnumerable<AuditRecord>`; `VerifyChainAsync(EntityId)` → `bool` (walks per-entity hash chain; true iff every `Prev` link matches).

- [ ] **Step 4:** Create `Audit/HashChain.cs` with two static methods: `string ComputeHash(AuditRecord record, string? prevHash)` and `bool Verify(IReadOnlyList<AuditRecord> orderedByAt)`. Hash input = `prevHash ?? "" || EntityId || Op (int32) || Actor || Tenant || At (ISO-8601) || canonical(Payload)`, digested with SHA-256, hex-encoded lowercase.

- [ ] **Step 5:** Implement `InMemoryAuditLog`.

- `ConcurrentDictionary<EntityId, ImmutableList<AuditRecord>>` — per-entity chain.
- `AppendAsync`: look up the current chain tail, compute `prev_hash = tail?.Hash ?? ""`, compute the new record's `Hash`, insert atomically.
- `VerifyChainAsync`: delegate to `HashChain.Verify`.
- `QueryAsync`: filter by entity / actor / tenant / time range.

- [ ] **Step 6:** Write hash-chain tests in `HashChainTests.cs`:

1. `ComputeHash_IsDeterministic_ForSameInputs`
2. `ComputeHash_ChangesWhenPrevHashChanges`
3. `ComputeHash_ChangesWhenPayloadChanges`
4. `Verify_ReturnsTrue_ForValidChain`
5. `Verify_ReturnsFalse_WhenPrevLinkIsBroken`
6. `Verify_ReturnsFalse_WhenRecordHashIsTampered`

Expected: **6 tests**.

- [ ] **Step 7:** Write audit-log tests in `InMemoryAuditLogTests.cs`:

1. `AppendAsync_StoresRecord`
2. `AppendAsync_LinksPrevToPreviousRecord`
3. `AppendAsync_ComputesHashCorrectly`
4. `QueryAsync_FiltersByEntity`
5. `QueryAsync_FiltersByActor`
6. `QueryAsync_FiltersByTenant`
7. `QueryAsync_FiltersByTimeRange`
8. `VerifyChainAsync_ReturnsTrue_ForUnalteredChain`
9. `VerifyChainAsync_ReturnsFalse_AfterExternalMutation` (simulate tamper)
10. `AppendAsync_IsThreadSafe_UnderConcurrency` (100 parallel appends → chain intact)
11. `AppendAsync_RejectsDuplicateAuditId`
12. `ReadRecording_IsOptIn_PerSchemaConfiguration`

Expected: **12 tests**.

Running total: 51 + 18 = **69 tests**.

- [ ] **Step 8:** Build + test + commit.

```bash
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Assets.Audit"
git add packages/foundation/Assets/Audit/ packages/foundation/tests/Assets/Audit/
git commit -m "feat(foundation/assets): add IAuditLog + InMemoryAuditLog with SHA-256 hash chain (spec §3.3)"
```

---

## Task 4 — Hierarchy + temporal edge primitives (~10 tests)

**Files:**
- Create: `packages/foundation/Assets/Hierarchy/IHierarchyService.cs`
- Create: `packages/foundation/Assets/Hierarchy/EntityEdge.cs`
- Create: `packages/foundation/Assets/Hierarchy/EdgeKind.cs`
- Create: `packages/foundation/Assets/Hierarchy/ClosureEntry.cs`
- Create: `packages/foundation/Assets/Hierarchy/TemporalSnapshot.cs`
- Create: `packages/foundation/Assets/Hierarchy/InMemoryHierarchyService.cs`
- Create: `packages/foundation/Assets/Temporal/TemporalRange.cs`
- Create: `packages/foundation/Assets/Temporal/AsOfExtensions.cs`
- Create: `packages/foundation/tests/Assets/Hierarchy/InMemoryHierarchyServiceTests.cs`

- [ ] **Step 1:** Create `Temporal/TemporalRange.cs` — `readonly record struct TemporalRange(DateTimeOffset ValidFrom, DateTimeOffset? ValidTo)` with `IsValidAt(DateTimeOffset)` and `OverlapsWith(TemporalRange)` helpers.

- [ ] **Step 2:** Create `Hierarchy/EdgeKind.cs` (`enum { ChildOf, References, SupersededBy }`) + `Hierarchy/EntityEdge.cs` (`sealed record EntityEdge(long Id, EntityId From, EntityId To, EdgeKind Kind, TemporalRange Validity, JsonDocument? Metadata)`).

- [ ] **Step 3:** Create `Hierarchy/ClosureEntry.cs` — `sealed record ClosureEntry(EntityId Ancestor, EntityId Descendant, int Depth, TemporalRange Validity)`. Depth 0 = self; 1 = direct parent; etc.

- [ ] **Step 4:** Create `Hierarchy/IHierarchyService.cs` with mutation methods (`AddEdgeAsync`, `InvalidateEdgeAsync`) and temporal read methods (`GetChildrenAsync(parent, asOf?)`, `GetParentsAsync(child, asOf?)`, `GetSubtreeAsync(root, asOf?)`, `GetAncestorsAsync(descendant, asOf?)`, `GetDescendantsAsync(ancestor, asOf?)`). All read methods accept an optional `DateTimeOffset? asOf` — null = now. Edge reads return `IAsyncEnumerable<EntityEdge>`; closure reads return `IAsyncEnumerable<ClosureEntry>`.

- [ ] **Step 5:** Implement `InMemoryHierarchyService`.

Storage:
- `List<EntityEdge>` — append-only.
- `List<ClosureEntry>` — maintained synchronously on every `AddEdgeAsync`.

`AddEdgeAsync(child, parent, ChildOf, t)`:
1. Insert direct edge.
2. Insert closure self-entry (depth 0) for child if not present.
3. For every ancestor `A` of `parent` (from existing closure rows where `Descendant == parent`), insert `(A, child, depth(A→parent)+1, validFrom=t)`.
4. Insert `(parent, child, 1, t)`.
5. For every existing descendant `D` of `child`, insert closure rows `(A, D, depth+1, ...)` for each new ancestor `A`.

`InvalidateEdgeAsync(edgeId, validTo)`:
1. Update the edge's `ValidTo`.
2. For every closure row touching this edge (recompute via the edge's ancestors/descendants), set `ValidTo = validTo`.

`GetChildrenAsync(parent, asOf)`:
```
SELECT edges where From = parent AND Kind = ChildOf AND validity.IsValidAt(asOf ?? now)
```

- [ ] **Step 6:** Write tests:

1. `AddEdgeAsync_CreatesDirectEdge`
2. `AddEdgeAsync_CreatesClosureRow_ForDirectParent`
3. `AddEdgeAsync_CreatesClosureRows_ForGrandparents`
4. `AddEdgeAsync_CreatesClosureRows_ForExistingDescendantsOfChild`
5. `GetChildrenAsync_ReturnsOnlyActiveEdges_ByDefault`
6. `GetChildrenAsync_ReturnsEdgesValidAtAsOfInstant`
7. `GetAncestorsAsync_ReturnsClosureRowsOrderedByDepth`
8. `InvalidateEdgeAsync_SetsValidToOnDirectEdge`
9. `InvalidateEdgeAsync_SetsValidToOnClosureRows`
10. `GetSubtreeAsync_ReturnsCorrectTree_AsOfPastInstant` (covers §8.1 temporal-graph semantics)

Expected: **10 tests**. Running total: 69 + 10 = **79 tests**.

- [ ] **Step 7:** Build + test + commit.

```bash
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Assets.Hierarchy|Assets.Temporal"
git add packages/foundation/Assets/Hierarchy/ packages/foundation/Assets/Temporal/ \
        packages/foundation/tests/Assets/Hierarchy/
git commit -m "feat(foundation/assets): add temporal hierarchy with closure-table edges (spec §5.6, §8.1)"
```

---

## Task 5 — Split / Merge / Re-parent operations with tests (~12 tests including the spec §8 building-split worked example)

**Files:**
- Create: `packages/foundation/Assets/Hierarchy/HierarchyOperations.cs`
- Create: `packages/foundation/tests/Assets/Hierarchy/SplitMergeReparentTests.cs`
- Create: `packages/foundation/tests/Assets/Integration/BuildingSplitScenarioTests.cs`

Operations are **transactional composites** that wrap multiple primitive calls into a single logical mutation. Phase A ships these as synchronous composition in code; Phase B will wrap them in a real transaction when the Postgres backend lands.

- [ ] **Step 1:** Create `Hierarchy/HierarchyOperations.cs` — sealed class taking `IEntityStore`, `IHierarchyService`, `IAuditLog` in its constructor. Three public methods:

- `Task<SplitResult> SplitAsync(EntityId oldEntity, IReadOnlyList<(SchemaId schema, JsonDocument body, CreateOptions options)> newEntities, IReadOnlyDictionary<EntityId, EntityId> childReassignments, string justification, ActorId actor, DateTimeOffset effectiveAt, CancellationToken ct = default)`
- `Task<MergeResult> MergeAsync(IReadOnlyList<EntityId> oldEntities, SchemaId newSchema, JsonDocument newBody, CreateOptions newOptions, string justification, ActorId actor, DateTimeOffset effectiveAt, CancellationToken ct = default)`
- `Task ReparentAsync(EntityId child, EntityId oldParent, EntityId newParent, string justification, ActorId actor, DateTimeOffset effectiveAt, CancellationToken ct = default)`

**SplitAsync algorithm** (mirrors spec §8.2, steps 1-6):

1. Mint each new entity via `_entities.CreateAsync(...)`.
2. For every current `ChildOf` edge where `To == oldEntity`:
   a. Look up `childReassignments[child]` to find the new parent.
   b. Invalidate the old edge via `_hierarchy.InvalidateEdgeAsync(edgeId, effectiveAt)`.
   c. Add new edge `_hierarchy.AddEdgeAsync(child, newParent, ChildOf, effectiveAt)`.
3. For each new entity, add a `SupersededBy` edge from `oldEntity`.
4. Tombstone `oldEntity` via `_entities.DeleteAsync(...)`.
5. Append a `Split` audit record with `Justification`, listing old + new IDs in the `Payload`.

**ReparentAsync**:
1. Invalidate old `ChildOf` edge.
2. Add new `ChildOf` edge.
3. Append `Reparent` audit record.

**MergeAsync**: inverse of Split.

- [ ] **Step 2:** Write `SplitMergeReparentTests.cs`:

1. `SplitAsync_MintsNewEntities`
2. `SplitAsync_InvalidatesOldChildEdges_AtEffectiveAt`
3. `SplitAsync_CreatesNewChildEdges_WithCorrectParent`
4. `SplitAsync_MarksOldEntityAsSuperseded`
5. `SplitAsync_TombstonesOldEntity`
6. `SplitAsync_EmitsSplitAuditRecord_WithJustification`
7. `MergeAsync_MintsMergedEntity`
8. `MergeAsync_MovesChildrenToMergedEntity`
9. `MergeAsync_EmitsMergeAuditRecord`
10. `ReparentAsync_InvalidatesOldEdge_CreatesNew`
11. `ReparentAsync_EmitsReparentAuditRecord`
12. `ReparentAsync_PreservesHistoricalQueries_BeforeEffectiveAt`

Expected: **12 tests**.

- [ ] **Step 3:** Write `BuildingSplitScenarioTests.cs` — the full spec §8.9 worked example. Setup:

- `Building:42` minted at `2020-01-01` — 10 floors, 120 unit children
- Roof replaced at `2022-06-15` — `Roof:42-v2` supersedes `Roof:42-v1`
- Floor count corrected at `2024-03-10` to 12 (`Op.Correct` audit)
- At `2026-05-01` split into `Building:42-north` (60 units) + `Building:42-south` (60 units)

Four assertions against this timeline:

1. `QueryAsOf_2024_12_31_ReturnsCorrectedFloors_WithOriginalBuilding` — `floors == 12`, 120 children present.
2. `QueryAsOf_2022_01_01_ReturnsOriginalFloors_10` — `floors == 10` (pre-correction).
3. `QueryAsOf_2026_09_01_ReturnsTwoPeerBuildings_AfterSplit` — `Building:42` is tombstoned; `Building:42-north` and `Building:42-south` each have 60 children.
4. `AuditTrail_ShowsFullEvolution_FromMintThroughSplit` — audit ops sequence = `[Mint, Write, Correct, Split]`.

Each test asserts via `ctx.Entities.GetAsync(id, new VersionSelector(AsOf: new Instant(date)))` and `ctx.Hierarchy.GetChildrenAsync(id, date).ToListAsync()`.

Expected: **4 tests**. Running total: 79 + 12 + 4 = **95 tests**.

- [ ] **Step 4:** Build + test + commit.

```bash
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~SplitMerge|BuildingSplit"
git add packages/foundation/Assets/Hierarchy/HierarchyOperations.cs \
        packages/foundation/tests/Assets/Hierarchy/SplitMergeReparentTests.cs \
        packages/foundation/tests/Assets/Integration/BuildingSplitScenarioTests.cs
git commit -m "feat(foundation/assets): add Split/Merge/Reparent hierarchy operations (spec §8.2-8.4)"
```

---

## Task 6 — PostgreSQL backend: DbContext, mappings, migrations, Testcontainers integration tests

**Files:**
- Create: `packages/foundation/Assets/Postgres/SunfishAssetsDbContext.cs`
- Create: `packages/foundation/Assets/Postgres/DesignTimeDbContextFactory.cs`
- Create: `packages/foundation/Assets/Postgres/Configurations/` (5 configuration classes)
- Create: `packages/foundation/Assets/Postgres/PostgresEntityStore.cs`
- Create: `packages/foundation/Assets/Postgres/PostgresVersionStore.cs`
- Create: `packages/foundation/Assets/Postgres/PostgresAuditLog.cs`
- Create: `packages/foundation/Assets/Postgres/PostgresHierarchyService.cs`
- Create: `packages/foundation/Assets/Postgres/Migrations/` (initial migration + snapshot)
- Create: `packages/foundation/Assets/Postgres/NpgsqlConverters.cs`
- Create: `packages/foundation/tests/Assets/Postgres/PostgresTestFixture.cs`
- Create: `packages/foundation/tests/Assets/Postgres/PostgresEntityStoreTests.cs`
- Create: `packages/foundation/tests/Assets/Postgres/PostgresVersionStoreTests.cs`
- Create: `packages/foundation/tests/Assets/Postgres/PostgresAuditLogTests.cs`
- Create: `packages/foundation/tests/Assets/Postgres/PostgresHierarchyServiceTests.cs`
- Create: `packages/foundation/tests/Assets/Postgres/MigrationRoundTripTests.cs`

- [ ] **Step 1:** Update `packages/foundation/Sunfish.Foundation.csproj` to add EF Core + Npgsql package references.

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
</ItemGroup>
```

**Compliance note (D-NO-BLAZOR):** EF Core + Npgsql do NOT transitively pull in any `Microsoft.AspNetCore.*` or `Microsoft.JSInterop.*` assembly. Verified by running `dotnet list package --include-transitive` after adding. If a future EF Core version pulls ASP.NET transitively, we must extract `Sunfish.Foundation.Assets.Postgres` into its own package — logged as a Parking Lot item at the end of this plan.

- [ ] **Step 2:** Create `SunfishAssetsDbContext.cs` — `sealed class SunfishAssetsDbContext : DbContext` with five `DbSet`s: `Entities`, `EntityVersions`, `AuditRecords`, `EntityEdges`, `ClosureEntries`. `OnModelCreating` sets `HasDefaultSchema("sunfish_assets")` and calls `ApplyConfigurationsFromAssembly`. `*Row` types are thin persistence records mirroring the DTO shape but using EF-idiomatic types (`string` for `EntityId`, `long` for `AuditId`, `NpgsqlRange<DateTime>` for `TemporalRange`); converters in `NpgsqlConverters.cs` translate to/from public DTOs.

- [ ] **Step 3:** Create the five `*Configuration` classes matching spec §5.1 DDL.

`EntityConfiguration.cs`:
```csharp
public sealed class EntityConfiguration : IEntityTypeConfiguration<EntityRow>
{
    public void Configure(EntityTypeBuilder<EntityRow> b)
    {
        b.ToTable("entities");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasColumnType("text");
        b.Property(e => e.SchemaId).HasColumnName("schema_id").IsRequired();
        b.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        b.Property(e => e.CurrentVersion).HasColumnName("current_version");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
        b.Property(e => e.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamptz");
        b.Property(e => e.Body).HasColumnName("body").HasColumnType("jsonb").IsRequired();

        b.HasIndex(e => e.SchemaId).HasDatabaseName("ix_entities_schema");
        b.HasIndex(e => e.TenantId).HasDatabaseName("ix_entities_tenant");
        // JSONB GIN indexes are added via raw SQL in the migration — EF Core doesn't natively express USING GIN (body jsonb_path_ops)
    }
}
```

`EntityVersionConfiguration.cs` maps the `entity_versions` table. Key decision: use `NpgsqlRange<DateTime>` for `valid_range`; translate to the public `TemporalRange` DTO in `NpgsqlConverters.cs`. This gives Postgres-side GiST indexing for free.

`AuditRecordConfiguration.cs` maps `audit_log` with `prev_id` as a self-FK.

`EntityEdgeConfiguration.cs` maps `entity_edges` with `tstzrange` validity.

`ClosureEntryConfiguration.cs` maps `entity_closure` — this is a **new** table not in the spec (spec §5.6 doesn't explicitly mention closure), added per D-HIERARCHY. Composite PK: `(ancestor, descendant, depth, valid_from)`.

- [ ] **Step 4:** Create `DesignTimeDbContextFactory.cs` implementing `IDesignTimeDbContextFactory<SunfishAssetsDbContext>` — returns a DbContext wired to `Host=localhost;Database=sunfish_assets_design;Username=postgres;Password=postgres` for `dotnet ef migrations` tooling only.

- [ ] **Step 5:** Generate the initial migration.

```bash
cd C:/Projects/Sunfish
dotnet ef migrations add InitialAssetsSchema \
  --project packages/foundation/Sunfish.Foundation.csproj \
  --output-dir Assets/Postgres/Migrations \
  --context SunfishAssetsDbContext
```

Inspect the generated migration. Confirm it creates:
- `entities`, `entity_versions`, `audit_log`, `entity_edges`, `entity_closure` tables.
- Indexes as per spec §5.1.

Append raw-SQL statements for the GIN JSONB indexes that EF doesn't emit:

```csharp
migrationBuilder.Sql("""
    CREATE INDEX ix_entities_body_gin ON sunfish_assets.entities USING GIN (body jsonb_path_ops);
    CREATE INDEX ix_edges_validity_gist ON sunfish_assets.entity_edges USING GIST (validity);
    CREATE INDEX ix_versions_validity_gist ON sunfish_assets.entity_versions USING GIST (validity);
    CREATE INDEX ix_closure_validity_gist ON sunfish_assets.entity_closure USING GIST (validity);
""");
```

- [ ] **Step 6:** Implement `PostgresEntityStore`, `PostgresVersionStore`, `PostgresAuditLog`, `PostgresHierarchyService` as EF-backed implementations of the four public interfaces.

Key patterns:
- **Current-body materialization (D-VERSION-STORE-SHAPE):** `Update` is a single transaction: INSERT version row + UPDATE entity row. Use `context.Database.BeginTransactionAsync()` for both writes.
- **Temporal queries:** use `EF.Functions.Contains(ent.ValidRange, asOf)` where Npgsql translates to the `@>` operator for `tstzrange`.
- **Hash chain lookup:** `AuditLog.AppendAsync` uses `FOR UPDATE` locking on the entity's latest audit row to serialize chain extension. Use `context.AuditRecords.Where(...).OrderByDescending(r => r.Id).Take(1).ForUpdate()`.
- **Async enumerables:** `QueryAsync` returns `context.Entities.Where(...).AsAsyncEnumerable()`.

- [ ] **Step 7:** Create `Postgres/NpgsqlConverters.cs` with `TemporalRange ↔ NpgsqlRange<DateTime>` extensions. `ToNpgsql()`: `lowerBound = ValidFrom.UtcDateTime` (inclusive); upper = `ValidTo?.UtcDateTime ?? DateTime.MaxValue` (inclusive iff null). `ToDto()`: inverse, returning `TemporalRange(DateTimeOffset, DateTimeOffset?)` with `ValidTo = null` when upper bound is infinite.

- [ ] **Step 8:** Create `tests/Assets/Postgres/PostgresTestFixture.cs` — `sealed class PostgresTestFixture : IAsyncLifetime` that spins up a `PostgreSqlBuilder().WithImage("postgres:16").WithDatabase("sunfish_assets_test").Build()` container, runs `Database.MigrateAsync()`, exposes `ConnectionString` + `CreateContext()`. Mark as `[CollectionDefinition("Postgres")]` so fixtures share per test-class collection (faster).

- [ ] **Step 9:** Write integration tests in each `Postgres*Tests.cs` file — parity suites of the in-memory tests, adapted to run against the containerized Postgres.

Counts:
- `PostgresEntityStoreTests.cs`: 6 key tests (CRUD + asOf + tombstone + concurrency).
- `PostgresVersionStoreTests.cs`: 5 tests (history, asOf, hash determinism, observer fires, tstzrange roundtrip).
- `PostgresAuditLogTests.cs`: 5 tests (append, query, chain verify, tamper detection, parallel appends).
- `PostgresHierarchyServiceTests.cs`: 5 tests (edge CRUD, closure maintenance, as-of subtree, split scenario on DB).
- `MigrationRoundTripTests.cs`: 4 tests (migrate up creates tables; migrate down drops them; idempotent re-up; GIN indexes exist via `pg_indexes` query).

Expected: **25 Postgres tests**.

- [ ] **Step 10:** Update `packages/foundation/tests/tests.csproj` to reference Testcontainers and set up the fixture collection.

```xml
<ItemGroup>
  <PackageReference Include="Testcontainers.PostgreSql" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
</ItemGroup>
```

- [ ] **Step 11:** Build + test.

```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Assets.Postgres"
```

Expected: 0 errors, 25 Postgres tests green (Docker required — CI runners must have Docker available). Running total: 95 + 25 = **120 tests**.

- [ ] **Step 12:** Commit.

```bash
git add packages/foundation/Assets/Postgres/ packages/foundation/tests/Assets/Postgres/ \
        packages/foundation/Sunfish.Foundation.csproj packages/foundation/tests/tests.csproj
git commit -m "feat(foundation/assets): add PostgreSQL backend via EF Core 10 + Testcontainers integration tests (spec §5.1)"
```

---

## Task 7 — Cross-primitive integration tests (~6 tests)

**Files:**
- Create: `packages/foundation/tests/Assets/Integration/EntityVersionAuditFlowTests.cs`
- Create: `packages/foundation/tests/Assets/Integration/BlobReferenceRoundTripTests.cs`

- [ ] **Step 1:** Write `EntityVersionAuditFlowTests.cs` — tests that span all three primitives.

1. `FullFlow_CreateEntity_AppendsMintAuditRecord_AndFirstVersion`
2. `FullFlow_UpdateEntity_AppendsWriteAudit_AndBumpsVersion`
3. `FullFlow_DeleteEntity_AppendsDeleteAudit_AndTombstonesLatestVersion`
4. `FullFlow_QueryAsOf_ReturnsConsistentEntity_AcrossEntityStoreAndVersionStore`
5. `FullFlow_AuditChain_VerifiesAfter_100MutationsToSameEntity`
6. `FullFlow_TemporalSplit_AppendsSplitAudit_AndUpdatesHierarchyAndEntities_AtomicallyOnReadSide` (no atomicity guarantee — just read-side consistency)

Each test uses the InMemory backends by default + also runs as a `[Theory]` against Postgres when the `PostgresTestFixture` is available.

- [ ] **Step 2:** Write `BlobReferenceRoundTripTests.cs` — integration of Blobs primitive with entity bodies.

Scenario: create an entity whose body carries a `Cid` reference. Put bytes into the blob store. Read the entity back via the EntityStore. Follow the CID to fetch the blob bytes. Verify roundtrip.

Tests:

1. `EntityBody_CarriesCidReference_AndBlobIsReachableByCid`
2. `UpdateEntity_ChangingCidReference_TriggersNewEntityVersion_LeavesOldBlobReachable`

Expected: **2 tests** + 6 flow tests = **8 tests**.

Running total: 120 + 8 = **128 tests**.

Note: the Blobs package is already present and tested (3 blob tests baseline). This task adds integration tests showing `Assets` ⇄ `Blobs` interop.

- [ ] **Step 3:** Commit.

```bash
git add packages/foundation/tests/Assets/Integration/
git commit -m "test(foundation/assets): cross-primitive integration + Blob reference roundtrip"
```

---

## Task 8 — Solution + DI registration + no-Blazor guard

- [ ] **Step 1:** Verify `Sunfish.slnx` already includes `packages/foundation/Sunfish.Foundation.csproj` + `packages/foundation/tests/tests.csproj`.

```bash
grep -E "foundation" C:/Projects/Sunfish/Sunfish.slnx
```

Expected: two lines. No solution change needed.

- [ ] **Step 2:** Create `packages/foundation/Assets/ServiceCollectionExtensions.cs` exposing two static methods:

- `AddSunfishAssetsInMemory(this IServiceCollection)` — registers a shared `InMemoryAssetStorage` + `IEntityStore` / `IVersionStore` / `IAuditLog` / `IHierarchyService` / `HierarchyOperations` as singletons, plus `TryAddSingleton` null-object defaults for `IEntityValidator` / `IVersionObserver` / `IAuditContextProvider`.
- `AddSunfishAssetsPostgres(this IServiceCollection, string connectionString)` — calls `AddDbContext<SunfishAssetsDbContext>(o => o.UseNpgsql(connectionString))`, registers the four Postgres primitives + `HierarchyOperations` as **scoped** (DbContext lifetime), `TryAddScoped` null-object defaults. Caller is responsible for `Database.Migrate()` at startup (or via a dedicated migration-service project).

- [ ] **Step 3:** Add a parallel `HasNoBlazorDependency` test inside `packages/foundation/tests/Assets/FoundationDependencyGuardTests.cs`. `[Theory]` over the forbidden names (`Microsoft.AspNetCore.Components`, `Microsoft.AspNetCore.Components.Web`, `Microsoft.JSInterop`, `Microsoft.JSInterop.WebAssembly`); the test asserts `typeof(EntityId).Assembly.GetReferencedAssemblies().Select(a => a.Name)` does not contain any forbidden name.

- [ ] **Step 4:** Full-solution build + test.

```bash
dotnet build C:/Projects/Sunfish/Sunfish.slnx
dotnet test C:/Projects/Sunfish/Sunfish.slnx --no-build
```

Expected: 0 errors, 0 warnings. All 128 Assets tests green + 16 baseline foundation + existing ui-core/Blazor tests — the root-level test count should go up by ~128 (delta matching the sum across tasks 1-7). The `HasNoBlazorDependency` guards (ui-core + foundation) both pass.

- [ ] **Step 5:** Commit.

```bash
git add packages/foundation/Assets/ServiceCollectionExtensions.cs \
        packages/foundation/tests/Assets/FoundationDependencyGuardTests.cs
git commit -m "feat(foundation/assets): add DI registration extensions + no-Blazor guard"
```

---

## Task 9 — Documentation

**Files:**
- Create: `packages/foundation/Assets/README.md`
- Update: `packages/foundation/README.md` (if exists) to cross-link Assets.
- Update: repo-root `README.md` to mention asset modeling.

- [ ] **Step 1:** Create `packages/foundation/Assets/README.md` with these sections:

1. **Header / intro** — "Sunfish.Foundation.Assets — Asset Modeling Kernel Primitives" + overview linking back to spec §3 (primitives 1–3) + §5.6 + §8.
2. **Architecture** — system diagram or prose. Entities carry schema refs + ownership; versions append-only; audit hash-chained per entity; edges temporal with closure materialization.
3. **Quick Start** — code samples for `AddSunfishAssetsInMemory()` and `AddSunfishAssetsPostgres(connStr)`.
4. **Worked Example — Building Split (spec §8.2, §8.9)** — full walkthrough of the 2020-01-01 → 2026-05-01 timeline with code snippets showing `CreateAsync` → `UpdateAsync` (correction) → `HierarchyOperations.SplitAsync` + the three asOf query assertions.
5. **Relationship to `Sunfish.Foundation.Blobs`** — entity bodies reference blobs by `Cid`, not inlined bytes; link to `BlobReferenceRoundTripTests`.
6. **Forward-compatibility notes** — nullable `Signature` (→ Phase B), stubbed `BranchAsync`/`MergeAsync` (→ Phase B CRDT), null-object `IEntityValidator` (→ schema registry phase).
7. **Testing** — one-line `dotnet test` filter + counts.

- [ ] **Step 2:** Update repo-root `README.md` with a one-line reference to the new Assets subtree.

- [ ] **Step 3:** Commit.

```bash
git add packages/foundation/Assets/README.md README.md
git commit -m "docs(foundation/assets): add asset-modeling README with worked building-split example"
```

---

## Task 10 — Final verification and branch push

- [ ] **Step 1:** Full solution build.

```bash
dotnet build C:/Projects/Sunfish/Sunfish.slnx --warnaserror
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 2:** Full solution test.

```bash
dotnet test C:/Projects/Sunfish/Sunfish.slnx --no-build
```

Expected: all tests green. Phase A contributes ~128 new tests. Baseline foundation 16 + ui-core 13 + Blazor adapter 29 = 58 before Phase A. New total ≈ 186.

- [ ] **Step 3:** No-Blazor invariant re-check.

```bash
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~HasNoBlazorDependency"
dotnet test packages/ui-core/tests/tests.csproj --filter "FullyQualifiedName~HasNoBlazorDependency"
```

Expected: all green. Foundation has no ASP.NET / JSInterop references even with EF Core + Npgsql added.

- [ ] **Step 4:** Contamination sweep.

```bash
# Confirm no stray Blazor / JSInterop usings sneaked into the Assets subtree
grep -rE "Microsoft\.(AspNetCore|JSInterop)" packages/foundation/Assets/ || echo "OK — none"
```

- [ ] **Step 5:** Testcontainers smoke run (manual / first-run only).

```bash
# Ensure Docker is up
docker ps
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Assets.Postgres" --logger "console;verbosity=normal"
```

Expected: container starts, migration runs, 25 tests pass, container disposed.

- [ ] **Step 6:** Push branch.

```bash
git push -u origin feat/platform-phase-A-asset-modeling
```

- [ ] **Step 7 (optional):** Open a PR referencing this plan.

PR body: link to `docs/superpowers/plans/2026-04-18-platform-phase-A-asset-modeling.md`; summarize deliverables per Task 0-9; list the 9 Key Decisions (D-CRDT-ROUTE through D-INSTANT-TYPE).

---

## Self-Review Checklist

**Coverage of spec sections:**

- [ ] §3.1 Entity Store — `IEntityStore` + `InMemoryEntityStore` + `PostgresEntityStore` shipped; `CreateAsync`/`GetAsync`/`UpdateAsync`/`DeleteAsync`/`QueryAsync` all present
- [ ] §3.2 Version Store — `IVersionStore` + two backends; `GetVersionAsync`/`GetHistoryAsync`/`GetAsOfAsync` shipped; `BranchAsync`/`MergeAsync` stubbed with explicit `NotImplementedException`
- [ ] §3.3 Audit Log — `IAuditLog` + two backends; SHA-256 hash chain; `VerifyChainAsync` works
- [ ] §3.8 materialized current-body optimization present (D-VERSION-STORE-SHAPE)
- [ ] §5.1 SQL schema — `entities`, `entity_versions`, `audit_log`, `entity_edges` tables all generated by EF migration; `entity_closure` added per D-HIERARCHY; GIN JSONB + GiST tstzrange indexes emitted via raw SQL in migration
- [ ] §5.6 temporal edges — `EntityEdge` + `TemporalRange` with `tstzrange` Postgres mapping
- [ ] §8.1 temporal graph projection — `GetSubtreeAsync(root, asOf)` returns correct tree at arbitrary instants
- [ ] §8.2 split — `HierarchyOperations.SplitAsync` + full worked-example test
- [ ] §8.3 merge — `MergeAsync` + tests
- [ ] §8.4 re-parent — `ReparentAsync` + tests
- [ ] §8.5 correction flag — supported via `Op.Correct` audit value
- [ ] §8.6 temporal query API — every read accepts `asOf` / `VersionSelector`

**Invariants:**

- [ ] `TreatWarningsAsErrors` kept (zero warnings in the Phase A deltas)
- [ ] Phase 2 `HasNoBlazorDependency` passing on foundation + ui-core
- [ ] Foundation assembly references only Microsoft.AspNetCore.SignalR.Client (pre-existing), EF Core, Npgsql — no ASP.NET or JSInterop
- [ ] Blobs primitive (already-shipped) referenced via `Cid` from entity bodies; no new copies or forks

**Tests:**

- [ ] Task 1: 20 tests (EntityStore + Common)
- [ ] Task 2: 15 tests (VersionStore)
- [ ] Task 3: 18 tests (AuditLog + HashChain)
- [ ] Task 4: 10 tests (Hierarchy)
- [ ] Task 5: 16 tests (Split/Merge/Reparent + BuildingSplit scenario)
- [ ] Task 6: 25 tests (Postgres via Testcontainers)
- [ ] Task 7: 8 tests (cross-primitive integration + Blob roundtrip)
- [ ] Task 8: 4 tests (dependency guards)
- [ ] **Phase A total: ~116 new tests** (sum above; actual counts may vary ±10%)
- [ ] Baseline tests still green
- [ ] `HasNoBlazorDependency` passes on both foundation and ui-core

**Process:**

- [ ] All commits on `feat/platform-phase-A-asset-modeling`
- [ ] Branch pushed to origin
- [ ] PR opened (optional; `git push -u` sufficient if not yet PR-stage)
- [ ] `packages/foundation/Assets/README.md` exists and includes the spec §8.2 / §8.9 building-split worked example

---

## Known risks and mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| EF Core + Npgsql transitively pull in ASP.NET → breaks `HasNoBlazorDependency` | Low | High | Task 6 Step 1 verifies via `dotnet list package --include-transitive`. Fallback: extract Assets.Postgres into its own csproj. |
| Testcontainers doesn't start on CI runners without Docker | Medium | Medium | Tag Postgres tests with `[Trait("Category", "RequiresDocker")]`; CI without Docker runs the in-memory suite only. |
| JSONB canonicalization mismatches between System.Text.Json and Postgres `jsonb` | Medium | Medium | `JsonCanonicalizer` computes hashes on .NET-side canonical bytes BEFORE storage; Postgres never re-canonicalizes for hash purposes. |
| Concurrent Update + Append race | Low | Medium | Single `BeginTransactionAsync` spanning version insert + entity update + audit append. |
| Closure table explodes for wide hierarchies | Low (PM ≤ 120 descendants/split) | Low | Benchmark in Task 4 with 1000 descendants to confirm. Swap to adjacency+BFS if needed. |
| Spec §3.1 `JsonPatch patch` vs. Phase A full-body `UpdateAsync` | Low | Medium | Phase A is the first consumer — no existing consumers of the patch form. Document the deviation; add patch overload in Phase B. |
| `EntityId` SHA-256 collisions | Near-zero | Catastrophic | 16 bytes → 128-bit space. Sufficient per NIST. |
| Hash-chain verification slow for 10k+ records | Medium | Low | Acceptable for PM scale (≤ 100 mutations/entity typical); cache last verified position if it becomes a hot path. |
| Bridge's current DbContext collides with `sunfish_assets` schema | Medium | Medium | Separate Postgres schema — no table collisions. Bridge stays on PmDemo-shaped context until explicit migration. |

---

## Parking lot — follow-up items for later phases

1. **Ed25519 signatures on audit records** — Phase B. `AuditRecord.Signature` nullable / forward-compatible.
2. **CRDT merge resolver / `BranchAsync` / `MergeAsync`** — Phase B. Stubs throw in Phase A.
3. **Schema registry (§3.4)** — Phase A2 or folded into B. `IEntityValidator` is the seam.
4. **JSON Patch diff storage** — compact-storage optimization; `entity_versions.diff` reserved.
5. **Postgres RLS tenant isolation (§5.1)** — Phase B. App-layer filter for now.
6. **Event bus wiring (§3.6)** — Phase C. `IVersionObserver` is the seam.
7. **BIM (§9), cold-storage tiering + Bloom filters (§8.8)** — deferred.
8. **Fallback csproj split** — if EF Core/Npgsql later pull in ASP.NET transitively, extract `Sunfish.Foundation.Assets.Postgres` into its own csproj.
9. **PostgresBlobStore backend** — spec §3.7 lists it; not required for Phase A. Track separately.
10. **Bridge accelerator kernel migration** — Bridge's PmDemo-shaped entities get swapped for `IEntityStore` + schema IDs in a future phase. Big refactor; out of scope here.
11. **NodaTime `Instant`** — Phase A uses `DateTimeOffset` under a thin wrapper; swap if later demanded.
12. **`EdgeKind` extensibility** — enum works for Phase A; consider string-based + constants class if new verticals need custom kinds.
13. **Payload wire format** — JSONB for Phase A; event-bus cross-process transport may later want protobuf/CBOR.
14. **Hash-chain verification performance** — O(n) walk acceptable for PM scale; cache the last verified position if verification becomes a hot path.

---

## Execution notes

- **Expected total effort:** 10-14 working days for an experienced .NET engineer. Task 6 (Postgres backend) is the largest single chunk at ~3-4 days; the in-memory tasks 1-5 are ~1-2 days each; integration + docs are ~1 day each.
- **Parallelism:** Tasks 1 → 2 → 3 → 4 → 5 are sequential (each depends on the DTOs / storage container from the previous). Task 6 can start in parallel with Task 5 once the InMemory storage contract is stable after Task 4. Task 7 depends on 1-5. Task 8-9 depend on 1-7.
- **Review checkpoints recommended:** After Task 3 (all three primitives in memory), after Task 5 (asset evolution scenarios working), after Task 6 (Postgres parity). Each is a natural PR if the plan is split.
- **Critical review items before shipping:**
  - `HashChain.ComputeHash` deterministic and stable across .NET versions (canonicalization critical).
  - Closure-table maintenance in split/merge is transactional (no partial updates).
  - `tstzrange` GiST indexes confirmed via `pg_indexes` after migration.
  - `HasNoBlazorDependency` guard still green with EF Core + Npgsql added.

---

## Tensions flagged with platform specification

For spec maintainer reconciliation in a v0.3 revision:

1. **§3.1 `UpdateAsync(JsonPatch patch)` vs Phase A full-body signature.** Phase A stores full bodies (D-VERSION-STORE-SHAPE); patch is a compact-storage optimization deferred. Suggest spec make full-body the baseline + patch a convenience overload.
2. **§3.2 `BranchAsync`/`MergeAsync` as first-class.** Unusable without CRDT merge (Phase B). Phase A stubs. Spec should acknowledge staging.
3. **§3.3 `Signature` as mandatory.** Phase A ships unsigned (signing requires Phase B crypto). Suggest spec clarify: hash chain mandatory; signatures mandatory after Phase B.
4. **§5.1 no schema prefix.** Phase A uses `sunfish_assets` schema to isolate from future primitives. Suggest spec add a schema convention.
5. **§5.6 asset hierarchy omits closure table.** Phase A adds one (D-HIERARCHY). Spec should mention this vs. adjacency/nested-set trade-off.
6. **§4.4 Phase 2 bundles crypto + federation + attachment store** with asset modeling. Phase A scopes to entity + version + audit + hierarchy + already-shipped Blobs; crypto/federation defer to Phase B. Spec should reconcile the split.
7. **§3.1 `Entity` record shape not specified.** Phase A: `Entity(EntityId Id, SchemaId Schema, TenantId Tenant, VersionId CurrentVersion, JsonDocument Body, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? DeletedAt)`. Suggest spec formalize.

---

## Summary — What This Plan Produces

| Deliverable | Location |
|---|---|
| `IEntityStore` + DTOs | `packages/foundation/Assets/Entities/` |
| `IVersionStore` + DTOs | `packages/foundation/Assets/Versions/` |
| `IAuditLog` + DTOs + hash chain | `packages/foundation/Assets/Audit/` |
| Hierarchy with temporal edges + closure | `packages/foundation/Assets/Hierarchy/` |
| Temporal range + asOf helpers | `packages/foundation/Assets/Temporal/` |
| Common value types (`EntityId`, `VersionId`, `Instant`, `SchemaId`, `ActorId`, `TenantId`) | `packages/foundation/Assets/Common/` |
| In-memory backends (zero-dependency) | `packages/foundation/Assets/{Entities,Versions,Audit,Hierarchy}/InMemory*.cs` |
| PostgreSQL backend via EF Core 10 + Npgsql | `packages/foundation/Assets/Postgres/` |
| EF Core migrations | `packages/foundation/Assets/Postgres/Migrations/` |
| DI registration helpers | `packages/foundation/Assets/ServiceCollectionExtensions.cs` |
| In-memory unit tests (~90) | `packages/foundation/tests/Assets/{Common,Entities,Versions,Audit,Hierarchy}/` |
| Cross-primitive integration tests (~8) | `packages/foundation/tests/Assets/Integration/` |
| Postgres Testcontainers integration tests (~25) | `packages/foundation/tests/Assets/Postgres/` |
| No-Blazor invariant guard for foundation | `packages/foundation/tests/Assets/FoundationDependencyGuardTests.cs` |
| Asset-modeling README with worked building-split example | `packages/foundation/Assets/README.md` |
| Directory.Packages.props version pins for EF + Npgsql + Testcontainers | `Directory.Packages.props` |

Outcome: the `Sunfish.Foundation` package now ships three of the seven kernel primitives from spec §3, plus the asset hierarchy and temporal query semantics needed for real-world asset evolution (spec §8). Bridge accelerator graduation from PmDemo-shaped entities to kernel-backed entities becomes a follow-up phase with a clear target API.
