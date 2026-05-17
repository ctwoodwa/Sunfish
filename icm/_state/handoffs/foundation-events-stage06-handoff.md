# Hand-off — `foundation-events` Canonical Cross-Cluster Event-Bus Substrate

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-16
**Status:** `ready-to-build`
**Workstream:** W#60 P4 — Path II native domain, substrate layer (cross-cluster event-bus foundation)
**Spec source:** [`_shared/engineering/cross-cluster-event-bus-design.md`](../../../_shared/engineering/cross-cluster-event-bus-design.md) — entire document is the canonical spec
**Companion conventions:** [`_shared/engineering/crdt-friendly-schema-conventions.md`](../../../_shared/engineering/crdt-friendly-schema-conventions.md) §1 (ULID), §2 (tombstones), §4 (append-only sub-collections)
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) §4 (Light tier: SQLite primary + Loro CRDT sync overlay)
**Trigger ruling:** [`coordination/inbox/xo-ruling-2026-05-16T21-12Z-cob-event-publisher-home.md`](../../../../coordination/inbox/xo-ruling-2026-05-16T21-12Z-cob-event-publisher-home.md) — Option C: ship canonical substrate now; DI-swap clusters after
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~12–16h sunfish-PM (foundation-tier package scaffold + 4 canonical interfaces + SQLite store + per-handler cursor model + DI extension + cluster migration sweep + ~50–60 tests + docs)
**PR count:** 6 PRs (PR 1 scaffold + canonical types; PR 2 SQLite store; PR 3 default publisher; PR 4 consumer cursor model; PR 5 OPTIONAL Loro op-log bridge; PR 6 DI extension + cluster migration sweep)
**Pre-merge council:** NOT required (substrate scope; mirrors the W#34/W#35/W#36 substrate-only pattern + sibling `blocks-financial-ledger` + `blocks-financial-periods` hand-offs). Standard COB self-audit applies on each PR.
**Audit before build:** `ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^foundation-events"` — expect EMPTY. `grep -rn "Sunfish.Foundation.Events" packages/ --include="*.cs"` — expect EMPTY. `ls packages/ | grep -E "^kernel-event"` — expect `kernel-event-bus/` (the lower-tier event-log substrate; foundation-events is a *different layer*, see §"Relationship to kernel-event-bus" below).

---

## Context

### Why this package exists now

The five Stage 02 cluster designs that landed 2026-05-16
(`blocks-people-schema-design.md`, `blocks-financial-schema-design.md`,
`blocks-work-schema-design.md`, `blocks-docs-schema-design.md`,
`blocks-reports-schema-design.md`) collectively surface **49 cross-cluster
domain events** across 6 producer clusters (per
`cross-cluster-event-bus-design.md` §3 catalog). Every one of those events
needs the same envelope, the same persistence mechanism, and the same
delivery semantics. Without a canonical substrate, each cluster will
re-derive the envelope and drift will accumulate from the first hand-off
onward.

`blocks-work-schema-design.md` §7 Q12 escalated the canonical question:

> All §4 workflows assume a domain-event bus. ADR 0088 specifies Loro
> CRDT for sync but not eventing. Stage 03: pick an in-process event bus
> (likely `foundation-events` or equivalent) and document idempotency
> keys per event type.

`cross-cluster-event-bus-design.md` §10 Q1 recommended `foundation-events`
as the canonical home. This hand-off is the **Stage 06 implementation of
that recommendation** — the substrate ships now; cluster work that needs
it can either depend on it (after merge) or use the local-then-DI-swap
pattern ratified in
`xo-ruling-2026-05-16T21-12Z-cob-event-publisher-home.md`.

### The trigger

`cob-question-2026-05-16T22-15Z-domain-event-publisher-home.md` surfaced
during PR #908 (`blocks-financial-periods` soft-close). Council Architecture
M2 flagged that cob's local `IDomainEventPublisher.PublishAsync<TPayload>(TPayload, CT)`
signature was missing the canonical envelope fields. XO ruled **Option C**:
widen the local interface to match canonical envelope NOW (cob keeps
sprint momentum); author `foundation-events` substrate in parallel via
XO subagent; DI-swap cluster local interfaces to the canonical home
when this substrate ships. **This hand-off IS that parallel substrate
authoring.**

### Why this is a foundation-tier (not kernel-tier, not blocks-tier) package

Per `cross-cluster-event-bus-design.md` §10 Q1:

> Recommendation: `foundation-events` (alongside `foundation-localfirst`,
> `foundation-multitenancy`). Kernel layer is for crypto + sync transport
> primitives; events are domain-shaped and foundation-tier is the right
> home.

- **Kernel-tier** = security and sync transport primitives. The existing
  `packages/kernel-event-bus/` provides `IEventLog` (append-only sequence-
  numbered log) + `IEventBus` (in-process delivery) + `KernelEvent`
  envelope. Those are *transport*-level primitives shaped around the
  kernel's needs (Lamport sequence numbers, signature verification hooks).
- **Foundation-tier** = domain-shaped contracts. `foundation-events` is
  the **domain event** layer — the envelope carries `tenantId`,
  `originatingReplicaId`, `causationId`, `correlationId`, semver
  `schemaVersion`, and a typed `Payload`. It is the contract between
  business-domain clusters (`blocks-financial-*`, `blocks-work-*`, etc.).
- **Blocks-tier** = per-cluster business logic. Each cluster *consumes*
  `foundation-events` for emit and subscribe; it does not own the
  substrate.

The two layers compose: a future enhancement may have
`SqliteDomainEventStore` append to `IEventLog` as part of its write
path (so domain events also flow into the kernel-tier audit chain).
**That composition is OUT OF SCOPE for this hand-off.** PR 5 (Loro
op-log bridge) is the only sync/transport-adjacent PR in this hand-off,
and it is **optional/deferrable** per the original scope.

### Relationship to `kernel-event-bus` (read carefully)

| Question | Kernel `Sunfish.Kernel.Events` (kernel-event-bus) | Foundation `Sunfish.Foundation.Events` (this hand-off) |
|---|---|---|
| Envelope shape | `KernelEvent` — sequence-numbered, signature-verified, schema-registry-keyed; designed for the kernel-sync replication chain | `DomainEventEnvelope<TPayload>` — tenant + replica + idempotency + causation + correlation; designed for cross-*business-cluster* contracts |
| Primary consumer | `kernel-sync` + `foundation-localfirst` quarantine queue + `foundation-rule-engine-event-bridge` | `blocks-financial-*`, `blocks-work-*`, `blocks-people-*`, `blocks-docs-*`, `blocks-reports-*` cluster handlers |
| Persistence | `IEventLog` (append-only sequence-numbered log; file-backed or in-memory) | `IDomainEventStore` → SQLite `domain_events` table (append-only, ULID-keyed, idempotency-key UNIQUE constraint) |
| Replay model | `ReadAfterAsync(seq)` — strict ordering by kernel-assigned sequence | Per-handler cursor (`event_handler_cursors`) — each handler advances independently |
| Layering | Kernel substrate (security + sync) | Foundation substrate (domain-shape) |

`foundation-events` does **NOT** re-implement `kernel-event-bus`. It does
**NOT** subsume `IEventLog`. The two coexist:

- Kernel-tier event log records *system-level* events (entity writes,
  sync ops, security ops) for the audit + sync chain.
- Foundation-tier domain event bus records *cross-cluster business
  events* (`Financial.JournalEntryPosted`, `Work.WorkOrderCompleted`,
  `People.TenantActivated`) for cross-cluster handlers.

A producer cluster MAY emit *both* (record an entity write to
`IEventLog` + emit a `Financial.JournalEntryPosted` domain event to
`IDomainEventPublisher`). The two writes happen in the same SQLite
transaction (per §9 of `cross-cluster-event-bus-design.md` —
"Wrap event emission in the entity-write transaction").

**Halt condition if confused:** if PR 1 surfaces a need to *fold*
`foundation-events` into `kernel-event-bus`, halt + file
`cob-question-*`. The layering is intentional and architecturally
load-bearing.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify the substrate is greenfield.**

   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^foundation-events"
   grep -rn "Sunfish.Foundation.Events" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ --include="*.cs" 2>/dev/null | grep -v bin | grep -v obj
   ```

   Expected: both empty. If either returns hits, **halt** + file
   `cob-question-*`; XO needs to reconcile with the pre-existing artifact.

2. **Confirm the canonical envelope shape from the trigger ruling.**

   File:
   `coordination/inbox/xo-ruling-2026-05-16T21-12Z-cob-event-publisher-home.md`.

   The envelope shape ratified there is the canonical for this package's
   PR 1. Verify it against `cross-cluster-event-bus-design.md` §1 (the
   spec) — there is *one minor reconciliation* PR 1 must do (see PR 1
   "Reconciling the two sources" below).

3. **Confirm `kernel-event-bus` is NOT being subsumed.**

   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/kernel-event-bus/
   ```

   Expected: present. `foundation-events` is additive; it does NOT
   replace `kernel-event-bus`. (See "Relationship to `kernel-event-bus`"
   above.)

4. **Verify `TenantId` canonical home.**

   ```bash
   grep -rn "public readonly record struct TenantId" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/foundation/ 2>/dev/null | grep -v bin | grep -v obj
   ```

   Expected: `packages/foundation/Assets/Common/TenantId.cs`. **Reuse
   this**. Do NOT redeclare `TenantId` inside `foundation-events`.

5. **Verify `ReplicaId` canonical home.**

   ```bash
   grep -rn "public.*ReplicaId\|public readonly record struct ReplicaId" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ 2>/dev/null | grep -v bin | grep -v obj
   ```

   **Three possible outcomes:**

   a. If `ReplicaId` is already a strongly-typed value object in
   `foundation-localfirst` or `foundation` (most likely home),
   **reuse it**. Add a `ProjectReference` from `foundation-events.csproj`
   to the package that declares it.

   b. If `ReplicaId` is referenced in `cross-cluster-event-bus-design.md`
   §1 but does NOT yet exist as a type anywhere in the repo (likely
   today's state — the spec speaks of "2-char replica suffix per §1 of
   crdt-friendly-schema-conventions.md"), **declare it locally in
   `foundation-events` PR 1** as:

   ```csharp
   namespace Sunfish.Foundation.Events;

   /// <summary>2-character replica suffix assigned at install per
   /// <c>crdt-friendly-schema-conventions.md</c> §1. ULID-replica-prefix
   /// + per-replica monotonic counter is the canonical pattern for
   /// human-readable monotonic numbers across replicas.</summary>
   public readonly record struct ReplicaId
   {
       public string Value { get; init; }
       public ReplicaId(string value)
       {
           ArgumentNullException.ThrowIfNull(value);
           if (value.Length != 2)
               throw new ArgumentException(
                   $"ReplicaId must be exactly 2 characters; received: '{value}' (length {value.Length}). " +
                   $"Per crdt-friendly-schema-conventions.md §1.",
                   nameof(value));
           Value = value;
       }
       public override string ToString() => Value;
   }
   ```

   Add a `// TODO: relocate to foundation-localfirst when that package
   formalizes the replica-identity concept` comment with a link to this
   hand-off + the convention doc.

   c. If `ReplicaId` already exists in **multiple** packages with
   conflicting shapes, **HALT** + file `cob-question-*`. XO will rule on
   canonical home. Do NOT silently pick one.

6. **Verify the SQLite client convention in the repo.**

   ```bash
   grep -rn "Microsoft.Data.Sqlite\|Microsoft.EntityFrameworkCore.Sqlite" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ --include="*.csproj" 2>/dev/null | head -10
   ```

   Expected: `Microsoft.EntityFrameworkCore` is the dominant pattern
   (per `foundation-persistence.csproj`). PR 2 SHOULD use **raw
   `Microsoft.Data.Sqlite`** for the `IDomainEventStore`
   implementation — NOT EF Core — because:

   - The `domain_events` table is append-only with a single UNIQUE
     constraint; EF Core's change-tracking + `SaveChanges` overhead is
     unwarranted.
   - The kernel-sync layer needs deterministic INSERT-OR-IGNORE
     semantics for idempotency dedup; raw SQLite gives us that directly.
   - The Loro op-log bridge (PR 5) requires direct table access.

   If `Microsoft.Data.Sqlite` is not already a `PackageReference`
   elsewhere, PR 2 adds it via `Directory.Packages.props` (verify the
   project uses Central Package Management):

   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/Directory.Packages.props 2>/dev/null
   ```

7. **Verify `Ulid.NewUlid()` library convention.**

   ```bash
   grep -rn "Ulid.NewUlid\|using.*Ulid\|NUlid" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ --include="*.cs" 2>/dev/null | grep -v bin | grep -v obj | head -5
   ```

   If `Ulid` is already in use (via the `Ulid` NuGet package or
   `NUlid`), reuse it. If not, PR 1 adds `Ulid` package reference and
   uses `Ulid.NewUlid().ToString()`. (See PR 1 "Ulid library choice"
   below.)

8. **Confirm no parallel-session PRs touch the substrate.**

   ```bash
   gh pr list --state open --search "foundation-events in:title,body"
   gh pr list --state open --search "Sunfish.Foundation.Events in:title,body"
   ```

   Expected: empty. If anything is open, **halt** + file
   `cob-question-*`.

9. **Confirm `but status` (or `git status`) is clean** and current branch
   is `main` (or a fresh worktree from `main` per
   `feedback_worktree_base_main_not_gitbutler.md`).

10. **Read the trigger ruling end-to-end before opening PR 1.** It is
    not long; it directly specifies the canonical envelope shape PR 1
    must produce.

---

## Cross-PR conventions

### Package home + naming

| Property | Value |
|---|---|
| Directory | `packages/foundation-events/` |
| csproj | `Sunfish.Foundation.Events.csproj` |
| Test csproj | `packages/foundation-events/tests/Sunfish.Foundation.Events.Tests.csproj` |
| C# namespace (root) | `Sunfish.Foundation.Events` |
| DI extension namespace | `Sunfish.Foundation.Events` (extensions on `IServiceCollection`) |
| Package id | `Sunfish.Foundation.Events` |
| `PackageTags` | `sunfish;foundation;events;cross-cluster;domain-events;sqlite;crdt` |
| `Description` | "Canonical cross-cluster domain-event substrate for Sunfish: `DomainEventEnvelope<T>`, `IDomainEventPublisher`, `IDomainEventStore` (SQLite append-only), `IEventReader` (per-handler cursor). Per `cross-cluster-event-bus-design.md`." |

### Target framework + repo conventions

Inherits from `Directory.Build.props`:

- `TargetFramework: net11.0`
- `Nullable: enable`
- `ImplicitUsings: enable`
- `LangVersion: latest`
- `TreatWarningsAsErrors: true`
- `GenerateDocumentationFile: true`

### License posture (verbatim in PR description "License posture" table)

| Component | Source | License | Sunfish license |
|---|---|---|---|
| `DomainEventEnvelope<T>` record | None — original implementation following `cross-cluster-event-bus-design.md` §1 | (n/a) | MIT |
| `IDomainEventPublisher` / `IDomainEventStore` / `IEventReader` / `IEventHandler<T>` interfaces | None — original | (n/a) | MIT |
| `SqliteDomainEventStore` | None — standard SQLite INSERT-OR-IGNORE pattern + the design's table shape | (n/a) | MIT |
| `SqliteEventReader` + `EventDispatcherHost` | None — standard per-handler cursor pattern (CDC-style) | (n/a) | MIT |
| `NoopDomainEventPublisher` | None — trivial | (n/a) | MIT |
| `LoroDomainEventBridge` (PR 5, optional) | Wraps Loro CRDT (Apache-2.0); no code lifted | Apache-2.0 (Loro) | MIT (Sunfish code) |

All MIT. No restrictive borrows. Cite the table verbatim in each PR's
"License posture" section.

### CRDT-friendly conventions applied (verbatim in PR 2 + PR 4 descriptions)

Per `crdt-friendly-schema-conventions.md`:

- §1 (ULIDs): `DomainEventEnvelope.EventId` is a 26-character
  Crockford-base-32 ULID. SQLite column is `TEXT NOT NULL PRIMARY KEY`.
- §2 (Tombstones — **not applied here**): `domain_events` rows are
  **never tombstoned** in the normal path. Crypto-shred (§2 "When hard
  delete IS allowed") is the only exception; out of scope for v1.
- §4 (Append-only sub-collections): the `domain_events` table is
  append-only. **No `UPDATE` is ever issued against it.** **No `DELETE`
  is ever issued against it** (outside crypto-shred). The schema does
  not even include an `updated_at` column; only `created_at`.
- §6 (Posted-then-immutable): domain events are inherently
  posted-then-immutable — once a producer cluster emits an event,
  consumers must be able to rely on its content. This package
  enforces immutability by SQLite-level (no UPDATE statement is
  emitted by `IDomainEventStore.AppendAsync`).

**Important:** `event_handler_cursors` IS mutable (per-handler cursor
advance is the only write). This table is NOT append-only; it has UPDATE
semantics per handler. The CRDT discipline here is: cursors are
per-replica, never cross-replica-synced; each replica maintains its own
cursor and drives its own dispatcher independently (per
`cross-cluster-event-bus-design.md` §5 "Cross-replica subscription
consistency — Each replica handles all events independently").

### Idempotency key discipline (mandatory)

Per `cross-cluster-event-bus-design.md` §4:

- `idempotency_key TEXT NOT NULL` column on `domain_events`.
- `CREATE UNIQUE INDEX idx_domain_events_idempotency ON domain_events(tenant_id, idempotency_key)`.
- `SqliteDomainEventStore.AppendAsync` uses `INSERT ... ON CONFLICT(tenant_id, idempotency_key) DO NOTHING` (SQLite supports this). On conflict, the method returns the **existing row's eventId** (not the would-be-inserted one) so callers know dedup happened.
- This is the substrate-level mechanism that makes the
  "at-least-once, idempotent" delivery model work across replicas + retries (per `cross-cluster-event-bus-design.md` §4).

### Test naming + organization

Per existing `packages/foundation-*/tests/` convention:

- File names: `<TypeUnderTest>Tests.cs`.
- Methods: `<MethodUnderTest>_<Scenario>_<ExpectedResult>` (e.g.,
  `AppendAsync_OnDuplicateIdempotencyKey_ReturnsExistingEventId`).
- Test framework: xUnit (verify via existing `tests/*.csproj`
  references).
- Assertion library: whatever the existing test projects use (likely
  `FluentAssertions` based on the `foundation-multitenancy/tests/`
  pattern; verify).
- SQLite-backed tests use `Microsoft.Data.Sqlite` in-memory mode
  (`Data Source=:memory:` shared-cache) so each test gets a fresh DB.

---

## Per-PR deliverables

This hand-off splits into **6 PRs** by responsibility. PR 1 establishes
the canonical types. PR 2 ships the SQLite store. PR 3 ships the
default publisher (depends on PR 2 for the store). PR 4 ships the
consumer cursor model (depends on PR 2 + PR 3). PR 5 is the optional
Loro op-log bridge (deferrable to a follow-on workstream). PR 6 ships
the DI extension and migrates `blocks-financial-periods` (the only
current consumer of a local `IDomainEventPublisher`) to the canonical.

Suggested sequencing: **PR 1 → PR 2 → PR 3 → PR 4 → PR 6**, with
**PR 5 deferred** if Loro integration patterns aren't yet ratified
elsewhere in the repo. If PR 5 lands, it slots between PR 4 and PR 6.

---

### PR 1 — Package scaffold + canonical types

**Estimated effort:** ~2h
**Scope:** create `packages/foundation-events/` directory; csproj + test
csproj; canonical `DomainEventEnvelope<TPayload>` record; canonical
`IDomainEventPublisher` + `IDomainEventStore` + `IEventReader` +
`IEventHandler<TPayload>` interfaces; `ReplicaId` value type (if not
already in foundation); ~10 unit tests covering envelope construction,
default values, JSON round-trip.
**Commit subject:** `feat(foundation-events): scaffold package + canonical DomainEventEnvelope and interfaces per cross-cluster-event-bus-design.md`
**Branch:** `cob/foundation-events-scaffold`
**Depends on:** none (greenfield)

#### File operations

```bash
mkdir -p packages/foundation-events/tests
```

#### csproj

**`packages/foundation-events/Sunfish.Foundation.Events.csproj`:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Foundation.Events</PackageId>
    <Description>Canonical cross-cluster domain-event substrate for Sunfish: DomainEventEnvelope&lt;T&gt;, IDomainEventPublisher, IDomainEventStore (SQLite append-only), IEventReader (per-handler cursor). Per cross-cluster-event-bus-design.md.</Description>
    <PackageTags>sunfish;foundation;events;cross-cluster;domain-events;sqlite;crdt</PackageTags>
    <RootNamespace>Sunfish.Foundation.Events</RootNamespace>
    <AssemblyName>Sunfish.Foundation.Events</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <!-- PR 1 needs no SQLite ref; PR 2 adds Microsoft.Data.Sqlite. -->
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Ulid" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
    <!-- Reuses TenantId from foundation/Assets/Common/TenantId.cs. -->
  </ItemGroup>
  <ItemGroup>
    <Compile Remove="tests/**/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Sunfish.Foundation.Events.Tests" />
  </ItemGroup>
</Project>
```

**`packages/foundation-events/tests/Sunfish.Foundation.Events.Tests.csproj`:**

Match the convention of existing `packages/foundation-*/tests/*.csproj`
(verify shape from `foundation-multitenancy/tests/`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <AssemblyName>Sunfish.Foundation.Events.Tests</AssemblyName>
    <RootNamespace>Sunfish.Foundation.Events.Tests</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.Data.Sqlite" />
    <!-- ↑ for PR 2 onward; harmless in PR 1 -->
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Sunfish.Foundation.Events.csproj" />
  </ItemGroup>
</Project>
```

(COB to confirm exact `PackageReference` shapes by mirroring
`foundation-multitenancy/tests/*.csproj`. Use Central Package Management
if the repo already uses it — verify
`ls Directory.Packages.props`.)

#### Solution file (`Sunfish.slnx` or `Sunfish.sln`)

```bash
dotnet sln add packages/foundation-events/Sunfish.Foundation.Events.csproj
dotnet sln add packages/foundation-events/tests/Sunfish.Foundation.Events.Tests.csproj
```

#### Canonical types

**`packages/foundation-events/DomainEventEnvelope.cs`** — the canonical
envelope. **This is the binding shape; subsequent cluster code must
adopt it.**

```csharp
namespace Sunfish.Foundation.Events;

using Sunfish.Foundation.Assets.Common;  // TenantId

/// <summary>
/// Canonical cross-cluster domain-event envelope. Every event emitted by
/// any blocks-* cluster carries this shape. Per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c> §1.
/// </summary>
/// <typeparam name="TPayload">The cluster-specific event payload (e.g.,
/// <c>JournalEntryPostedPayload</c>).</typeparam>
/// <remarks>
/// <para>
/// <b>Append-only:</b> envelopes are immutable after construction (record
/// type + init-only setters). They are persisted to the
/// <c>domain_events</c> SQLite table by <see cref="IDomainEventStore"/>
/// and never updated.
/// </para>
/// <para>
/// <b>Idempotency:</b> the <see cref="IdempotencyKey"/> is the unique
/// dedup mechanism. Per <c>cross-cluster-event-bus-design.md</c> §4,
/// every event type defines a deterministic derivation of its
/// idempotency key from its semantic identity (typically
/// <c>"{eventType}|{tenantId}|{entityId}"</c> or similar). Two emissions
/// with the same key collapse into one row at the
/// <see cref="IDomainEventStore"/> level.
/// </para>
/// <para>
/// <b>Causation vs Correlation:</b> <see cref="CausationId"/> chains
/// upstream → downstream (handler emits event B in reaction to event A
/// → B.CausationId = A.EventId). <see cref="CorrelationId"/> marks a
/// logical workflow across many events (lease execution chain shares
/// one correlation id).
/// </para>
/// </remarks>
public sealed record DomainEventEnvelope<TPayload>
{
    /// <summary>ULID (26 chars, Crockford base-32). Primary key in the
    /// <c>domain_events</c> table.</summary>
    public required string EventId { get; init; }

    /// <summary>Cluster-qualified verb-past-tense name, e.g.
    /// <c>"Financial.JournalEntryPosted"</c>. Per
    /// <c>cross-cluster-event-bus-design.md</c> §2.</summary>
    public required string EventType { get; init; }

    /// <summary>Semver-major; bump on breaking payload changes. Per
    /// <c>cross-cluster-event-bus-design.md</c> §8.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>Wall-clock at the moment of event creation (may be
    /// backdated for synthetic events; see
    /// <c>cross-cluster-event-bus-design.md</c> §1 "occurredAt vs
    /// recordedAtUtc").</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Tenant scope. Cross-tenant events are FORBIDDEN per
    /// <c>crdt-friendly-schema-conventions.md</c> §14.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>2-char replica suffix per
    /// <c>crdt-friendly-schema-conventions.md</c> §1. Carries provenance
    /// for audit + cross-replica dedup.</summary>
    public required ReplicaId OriginatingReplicaId { get; init; }

    /// <summary>Deterministic dedup key. UNIQUE per
    /// <c>(TenantId, IdempotencyKey)</c> in the
    /// <c>domain_events</c> table.</summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>Optional. EventId of the upstream event that caused this
    /// one. Enables debugging "why did this event fire?".</summary>
    public string? CausationId { get; init; }

    /// <summary>Optional. Workflow correlation id; events sharing this
    /// value are part of the same logical workflow.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Cluster-specific typed payload.</summary>
    public required TPayload Payload { get; init; }
}
```

#### Reconciling the two sources

`cross-cluster-event-bus-design.md` §1 lists slightly more envelope
fields than the trigger ruling's canonical shape:

| Field | In spec §1 | In trigger ruling |
|---|---|---|
| `recordedAtUtc` | Yes (wall-clock at write to event-store) | No |
| `producerCluster` | Yes | No |
| `producerEntity` | Yes (optional `{kind, id}`) | No |

**Resolution for PR 1:** ship the trigger ruling's shape (which IS the
binding canonical for this hand-off since the ruling came after the
design doc and explicitly ratifies the C# shape). The spec's extra
fields are SQLite-table-only columns:

- `recorded_at_utc` is set automatically by `SqliteDomainEventStore`
  (PR 2) via the SQLite `DEFAULT CURRENT_TIMESTAMP` clause or via the
  store's clock injection. It does **not** appear in `DomainEventEnvelope<T>`
  because it is a *store-side* timestamp, not a producer-side one.
- `producer_cluster` is derived from the `EventType` prefix at store
  time (e.g., `"Financial.JournalEntryPosted"` → `producer_cluster =
  "financial"`). Store-side denormalization for query convenience; not
  a producer responsibility.
- `producer_entity_kind` + `producer_entity_id` are deferred; not in
  v1. Producer can include them in the typed payload if they're
  cluster-specific. (Design doc §1 marks `producerEntity?` as
  optional — Stage 06 v1 ships without it.)

This reconciliation rule applies cross-PR: anything the spec lists as a
table column but the ruling omits from the envelope is **store-side
denormalization**, NOT a producer-side envelope field.

#### Canonical interfaces

**`packages/foundation-events/IDomainEventPublisher.cs`:**

```csharp
namespace Sunfish.Foundation.Events;

/// <summary>
/// Producer contract for emitting cross-cluster domain events.
/// Implementations persist the envelope to <see cref="IDomainEventStore"/>
/// and optionally notify in-process subscribers via
/// <see cref="IEventDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotency:</b> publishing the same envelope twice (same
/// <see cref="DomainEventEnvelope{TPayload}.IdempotencyKey"/> +
/// <see cref="DomainEventEnvelope{TPayload}.TenantId"/>) is a no-op on
/// the second call — see <see cref="IDomainEventStore"/>'s UNIQUE
/// constraint dedup. Producers SHOULD treat
/// <see cref="PublishAsync{TPayload}"/> as fire-and-forget after
/// awaiting; the at-least-once delivery is guaranteed by the store +
/// dispatcher, not by this method.
/// </para>
/// <para>
/// <b>Transactional discipline:</b> per
/// <c>cross-cluster-event-bus-design.md</c> §9, producers SHOULD wrap
/// the entity write + the event emit in the same SQLite transaction.
/// The default implementation (<c>DefaultDomainEventPublisher</c>, PR 3)
/// participates in the ambient SQLite transaction if one is open.
/// </para>
/// </remarks>
public interface IDomainEventPublisher
{
    /// <summary>Append the event to the store + notify in-process
    /// dispatchers. Idempotent on
    /// <c>(TenantId, IdempotencyKey)</c>.</summary>
    Task PublishAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken ct);
}
```

**`packages/foundation-events/NoopDomainEventPublisher.cs`:**

```csharp
namespace Sunfish.Foundation.Events;

/// <summary>
/// Discards every published envelope. Useful for unit tests and during
/// bootstrap before a tenant context is available. Registered by
/// <c>AddFoundationEvents()</c> only when the host has explicitly opted
/// out of persistent eventing.
/// </summary>
public sealed class NoopDomainEventPublisher : IDomainEventPublisher
{
    public Task PublishAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken ct) => Task.CompletedTask;
}
```

**`packages/foundation-events/IDomainEventStore.cs`:**

```csharp
namespace Sunfish.Foundation.Events;

using Sunfish.Foundation.Assets.Common;

/// <summary>
/// Persistence substrate for the <c>domain_events</c> table. Append-only;
/// no UPDATE; no DELETE outside crypto-shred. Per
/// <c>cross-cluster-event-bus-design.md</c> §1 storage shape.
/// </summary>
public interface IDomainEventStore
{
    /// <summary>Append a single envelope. On duplicate
    /// <c>(TenantId, IdempotencyKey)</c>, this method does NOT throw;
    /// it returns the existing row's <c>EventId</c> so the caller
    /// knows dedup happened. Per
    /// <c>cross-cluster-event-bus-design.md</c> §4.</summary>
    /// <returns>The persisted (or pre-existing) event id.</returns>
    Task<string> AppendAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken ct);

    /// <summary>Read events after a cursor (exclusive). Returns up to
    /// <paramref name="batchSize"/> events in ULID order. Used by
    /// <see cref="IEventReader"/> to drain events to handlers.</summary>
    Task<IReadOnlyList<RawDomainEvent>> GetAfterCursorAsync(
        TenantId tenantId,
        string? afterEventId,
        int batchSize,
        CancellationToken ct);

    /// <summary>Lookup by idempotency key. Returns null if no row
    /// matches. Used by tests + by retry paths.</summary>
    Task<RawDomainEvent?> FindByIdempotencyKeyAsync(
        TenantId tenantId,
        string idempotencyKey,
        CancellationToken ct);
}

/// <summary>
/// Untyped view of a persisted event row. Carries the envelope fields
/// plus the JSON-encoded payload. Consumers deserialize the payload
/// to their event-specific type at dispatch time.
/// </summary>
public sealed record RawDomainEvent
{
    public required string EventId { get; init; }
    public required string EventType { get; init; }
    public required int SchemaVersion { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }
    public required TenantId TenantId { get; init; }
    public required ReplicaId OriginatingReplicaId { get; init; }
    public required string IdempotencyKey { get; init; }
    public string? CausationId { get; init; }
    public string? CorrelationId { get; init; }
    public required string PayloadJson { get; init; }
}
```

**`packages/foundation-events/IEventReader.cs`:**

```csharp
namespace Sunfish.Foundation.Events;

/// <summary>
/// Consumer-side reader. Walks <see cref="IDomainEventStore"/> from a
/// per-handler cursor, invokes registered handlers on each event, and
/// advances the cursor on success. Per
/// <c>cross-cluster-event-bus-design.md</c> §5.
/// </summary>
public interface IEventReader
{
    /// <summary>Register a handler for a specific event type. The
    /// reader walks the store from the handler's last cursor forward
    /// and invokes the handler on each new event. Cursor advance is
    /// per-handler — slow or failing handlers do NOT block other
    /// handlers.</summary>
    /// <param name="handlerId">Stable id for cursor persistence (e.g.,
    /// <c>"blocks-work.ProjectActualsUpserter"</c>).</param>
    /// <param name="eventType">Cluster-qualified event-type filter (e.g.,
    /// <c>"Financial.JournalEntryPosted"</c>). Null matches all
    /// types.</param>
    /// <param name="handler">Async handler. Throwing does NOT advance
    /// the cursor; retry scheduling per
    /// <c>cross-cluster-event-bus-design.md</c> §6.</param>
    Task RegisterHandlerAsync(
        string handlerId,
        string? eventType,
        IEventHandler<RawDomainEvent> handler,
        CancellationToken ct);
}

/// <summary>Typed handler invoked per event by
/// <see cref="IEventReader"/>.</summary>
public interface IEventHandler<TEvent>
{
    Task HandleAsync(TEvent evt, CancellationToken ct);
}

/// <summary>In-process broadcast for handlers that want push-style
/// delivery (in addition to the cursor-driven pull from
/// <see cref="IEventReader"/>). PR 3 wires this up.</summary>
public interface IEventDispatcher
{
    /// <summary>Notify in-process subscribers that a new event has
    /// been recorded. Idempotent — subscribers must tolerate redelivery
    /// from the cursor walk too.</summary>
    Task DispatchAsync(RawDomainEvent evt, CancellationToken ct);
}
```

#### ReplicaId

Per pre-build step 5, **if `ReplicaId` is not already in
`foundation-localfirst` or `foundation`**, declare it locally in PR 1:

**`packages/foundation-events/ReplicaId.cs`:**

```csharp
namespace Sunfish.Foundation.Events;

/// <summary>
/// 2-character replica suffix assigned at install per
/// <c>crdt-friendly-schema-conventions.md</c> §1. Carries provenance
/// for audit + cross-replica dedup.
/// </summary>
/// <remarks>
/// TODO: relocate to <c>foundation-localfirst</c> when that package
/// formalizes the replica-identity concept. Tracked in
/// <c>foundation-events-stage06-handoff.md</c> + cross-ref to
/// <c>cross-cluster-event-bus-design.md</c> §1.
/// </remarks>
public readonly record struct ReplicaId
{
    public string Value { get; init; }

    public ReplicaId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 2)
        {
            throw new ArgumentException(
                $"ReplicaId must be exactly 2 characters; received: '{value}' (length {value.Length}). " +
                "Per crdt-friendly-schema-conventions.md §1.",
                nameof(value));
        }
        Value = value;
    }

    public override string ToString() => Value;
}
```

If `ReplicaId` is **already** in `foundation-localfirst` (or similar),
**delete the local declaration** + add a `ProjectReference` to that
package + update the namespace import in `DomainEventEnvelope.cs`. The
deletion is part of PR 1; do not ship a duplicate.

#### Ulid library choice

If the repo already uses the `Ulid` NuGet package, mirror that. If
neither `Ulid` nor `NUlid` is anywhere in the repo, add `Ulid` (the
maintained .NET ULID library — <https://github.com/Cysharp/Ulid>) to
`Directory.Packages.props` and reference it from the csproj.

The library exposes:

```csharp
var id = Ulid.NewUlid().ToString();  // "01J7K8R9V5X3Y6Z2W1Q8P4N0M7"
```

`DomainEventEnvelope.EventId` is `string` (not the `Ulid` type) so the
public API does not leak the library choice. Internally, PR 2's store
can use `Ulid.Parse(eventId)` for ordering.

#### Tests (PR 1)

**`packages/foundation-events/tests/DomainEventEnvelopeTests.cs`:**

- `Construction_WithAllRequiredFields_Succeeds` — happy path; verify all
  `required` fields are set; result is non-null.
- `Construction_WithoutRequiredField_FailsCompile` — compile-time test;
  document via XML comment that omitting a `required` init field is a
  compile error. (No runtime assertion; the existence of the
  `required` keyword IS the test.)
- `Records_AreValueEqual_WhenAllFieldsEqual` — equality semantics of
  the record.
- `Records_AreNotValueEqual_WhenEventIdDiffers`.
- `WithSyntax_PreservesUnchangedFields` — `envelope with { Payload = newPayload }` retains other fields.

**`packages/foundation-events/tests/DomainEventEnvelopeJsonTests.cs`:**

- `Serialize_RoundTrip_PreservesAllFields` — `System.Text.Json`
  serialize + deserialize + assert equality. Use a simple payload type
  (e.g., `record TestPayload(string Name, int Value)`).
- `Serialize_WithCausationIdNull_OmitsField` — null
  `CausationId` does NOT appear in JSON (use
  `JsonSerializerOptions.DefaultIgnoreCondition =
  WhenWritingNull`).
- `Serialize_PreservesEventIdAsString_NotUlidType` — the
  `EventId` field is a plain string in JSON (avoid leaking the Ulid
  type).
- `Deserialize_WithUnknownField_Tolerates` — incoming JSON with extra
  fields does not throw (forward-compat).
- `Deserialize_WithMissingRequiredField_Throws` — incoming JSON without
  a required field throws `JsonException`.

**`packages/foundation-events/tests/ReplicaIdTests.cs`:**

- `Construction_With2CharValue_Succeeds`.
- `Construction_With1CharValue_ThrowsArgumentException`.
- `Construction_With3CharValue_ThrowsArgumentException`.
- `Construction_WithNullValue_ThrowsArgumentNullException`.
- `Equality_TwoSameValues_AreEqual`.

Total new tests this PR: ~13–15.

#### Verification

- `dotnet build packages/foundation-events/` succeeds.
- `dotnet test packages/foundation-events/tests/` passes (all new
  tests).
- `grep -rn "Sunfish.Foundation.Events" packages/ --include="*.cs"` —
  the only hits are inside the new package itself.
- No reference to `Sunfish.Kernel.Events` from `foundation-events` —
  these are deliberately separate layers (per "Relationship to
  `kernel-event-bus`" above). If the build pulls in `kernel-event-bus`
  transitively, halt + file `cob-question-*`.

#### PR description template

```
Scaffold packages/foundation-events/ — the canonical cross-cluster
domain-event substrate per cross-cluster-event-bus-design.md §1.

Adds:
- DomainEventEnvelope<TPayload> record — the canonical envelope shape
  ratified in xo-ruling-2026-05-16T21-12Z-cob-event-publisher-home.md.
- IDomainEventPublisher / NoopDomainEventPublisher.
- IDomainEventStore + RawDomainEvent (substrate-level untyped view).
- IEventReader + IEventHandler<T> + IEventDispatcher.
- ReplicaId 2-char value type (relocates to foundation-localfirst
  in a follow-up).
- ~13 unit tests covering envelope construction, equality, JSON
  round-trip, ReplicaId validation.

This package establishes the canonical cross-cluster event substrate.
Subsequent PRs in this hand-off:
- PR 2 — SqliteDomainEventStore + domain_events table migration.
- PR 3 — DefaultDomainEventPublisher.
- PR 4 — SqliteEventReader + EventDispatcherHost.
- PR 5 (optional) — LoroDomainEventBridge.
- PR 6 — AddFoundationEvents() DI extension + DI-swap sweep for
  blocks-financial-periods.

Layering note: this is distinct from Sunfish.Kernel.Events
(kernel-event-bus). Kernel-tier is for system/sync events with
sequence-numbered ordering; foundation-tier is for cross-cluster
*business* events with tenant + replica + idempotency + causation
semantics. The two compose but neither subsumes the other.

License posture: all MIT; no external borrows; original implementation
following cross-cluster-event-bus-design.md.

Refs: ADR 0088 §4; cross-cluster-event-bus-design.md §1, §2, §5;
xo-ruling-2026-05-16T21-12Z-cob-event-publisher-home.md.
```

#### Do NOT in this PR

- Do NOT implement `SqliteDomainEventStore`. That is PR 2.
- Do NOT implement `DefaultDomainEventPublisher`. That is PR 3.
- Do NOT add the SQLite migration. That is PR 2.
- Do NOT add the DI extension `AddFoundationEvents()`. That is PR 6.
- Do NOT migrate `blocks-financial-periods` away from its local
  `IDomainEventPublisher`. That is PR 6's sweep.
- Do NOT fold the package into `kernel-event-bus`. Distinct layers; see
  "Relationship to `kernel-event-bus`".

---

### PR 2 — `SqliteDomainEventStore` implementation

**Estimated effort:** ~3h
**Scope:** SQLite migration creating `domain_events` table + indexes;
`SqliteDomainEventStore` implementation; ~15 tests including idempotency-
key uniqueness, cursor pagination, tenant isolation.
**Commit subject:** `feat(foundation-events): add SqliteDomainEventStore + domain_events table migration per design §1 storage shape`
**Branch:** `cob/foundation-events-sqlite-store`
**Depends on:** PR 1 merged

#### Package reference additions

Add `Microsoft.Data.Sqlite` to
`packages/foundation-events/Sunfish.Foundation.Events.csproj`. If the
repo uses Central Package Management, also add it to
`Directory.Packages.props`.

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Data.Sqlite" />
  <!-- existing refs preserved -->
</ItemGroup>
```

#### Migration

**`packages/foundation-events/Sql/001-create-domain-events.sql`** —
embedded resource invoked at substrate init:

```sql
-- foundation-events migration 001 — create domain_events table.
-- Append-only per crdt-friendly-schema-conventions.md §4.
-- Per cross-cluster-event-bus-design.md §1 storage shape.

CREATE TABLE IF NOT EXISTS domain_events (
    event_id                TEXT NOT NULL PRIMARY KEY,        -- ULID
    event_type              TEXT NOT NULL,                    -- "Financial.JournalEntryPosted"
    schema_version          INTEGER NOT NULL,                 -- bumped on payload-shape changes
    occurred_at             TEXT NOT NULL,                    -- ISO-8601 UTC
    recorded_at_utc         TEXT NOT NULL,                    -- ISO-8601 UTC; store-side write timestamp
    tenant_id               TEXT NOT NULL,
    originating_replica_id  TEXT NOT NULL,                    -- 2-char ReplicaId
    idempotency_key         TEXT NOT NULL,
    causation_id            TEXT,
    correlation_id          TEXT,
    producer_cluster        TEXT NOT NULL,                    -- derived from event_type prefix
    payload_json            TEXT NOT NULL,                    -- serialized DomainEventEnvelope.Payload
    created_at              TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_domain_events_idempotency
    ON domain_events(tenant_id, idempotency_key);

CREATE INDEX IF NOT EXISTS idx_domain_events_type_recorded
    ON domain_events(tenant_id, event_type, recorded_at_utc);

CREATE INDEX IF NOT EXISTS idx_domain_events_correlation
    ON domain_events(tenant_id, correlation_id)
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_domain_events_tenant_eventid
    ON domain_events(tenant_id, event_id);
    -- For cursor-after-eventid queries from IEventReader.
```

**Note on migration sequencing:** if the repo has an established
SQLite migration framework (verify in pre-build step 6), wire this in
per that pattern. If there is **no clear convention** (likely — many
packages have their own migration paths), this package uses a simple
internal `ApplyMigrationsAsync(connection)` method that the
`AddFoundationEvents()` DI extension (PR 6) invokes once at host
startup. The substrate IS the canonical migration framework for the
`domain_events` table; consumer clusters do NOT migrate against this
table.

**Halt condition:** if the repo has a global migration ordering rule
(e.g., a versioned `_migrations` table that all packages share), halt +
file `cob-question-*`. XO will rule on whether foundation-events
participates in the global registry or owns its own.

#### Implementation

**`packages/foundation-events/SqliteDomainEventStore.cs`:**

```csharp
namespace Sunfish.Foundation.Events;

using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Sunfish.Foundation.Assets.Common;

/// <summary>
/// SQLite-backed implementation of <see cref="IDomainEventStore"/>.
/// Append-only; idempotent on (TenantId, IdempotencyKey).
/// </summary>
public sealed class SqliteDomainEventStore : IDomainEventStore
{
    private readonly SqliteConnection _connection;
    private readonly TimeProvider _clock;
    private readonly JsonSerializerOptions _jsonOptions;

    public SqliteDomainEventStore(SqliteConnection connection, TimeProvider clock)
    {
        _connection = connection;
        _clock = clock;
        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
    }

    /// <summary>Apply the 001 migration. Idempotent; safe to call repeatedly.</summary>
    public async Task ApplyMigrationsAsync(CancellationToken ct)
    {
        var assembly = typeof(SqliteDomainEventStore).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "Sunfish.Foundation.Events.Sql.001-create-domain-events.sql");
        if (stream is null)
            throw new InvalidOperationException(
                "Embedded migration 001-create-domain-events.sql not found. " +
                "Verify <EmbeddedResource Include=\"Sql\\*.sql\" /> in csproj.");
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(ct);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<string> AppendAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // Derive producer_cluster from EventType prefix.
        var dotIdx = envelope.EventType.IndexOf('.');
        if (dotIdx <= 0)
            throw new ArgumentException(
                $"EventType '{envelope.EventType}' is not cluster-qualified. " +
                "Per cross-cluster-event-bus-design.md §2: format is " +
                "<ClusterName-titlecase>.<PascalCaseVerbPastTense>.",
                nameof(envelope));
        var producerCluster = envelope.EventType.Substring(0, dotIdx).ToLowerInvariant();

        var payloadJson = JsonSerializer.Serialize(envelope.Payload, _jsonOptions);
        var recordedAtUtc = _clock.GetUtcNow();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO domain_events (
                event_id, event_type, schema_version, occurred_at, recorded_at_utc,
                tenant_id, originating_replica_id, idempotency_key,
                causation_id, correlation_id, producer_cluster, payload_json
            ) VALUES (
                $event_id, $event_type, $schema_version, $occurred_at, $recorded_at_utc,
                $tenant_id, $originating_replica_id, $idempotency_key,
                $causation_id, $correlation_id, $producer_cluster, $payload_json
            )
            ON CONFLICT(tenant_id, idempotency_key) DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("$event_id", envelope.EventId);
        cmd.Parameters.AddWithValue("$event_type", envelope.EventType);
        cmd.Parameters.AddWithValue("$schema_version", envelope.SchemaVersion);
        cmd.Parameters.AddWithValue("$occurred_at", envelope.OccurredAt.ToString("O"));
        cmd.Parameters.AddWithValue("$recorded_at_utc", recordedAtUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$tenant_id", envelope.TenantId.Value);
        cmd.Parameters.AddWithValue("$originating_replica_id", envelope.OriginatingReplicaId.Value);
        cmd.Parameters.AddWithValue("$idempotency_key", envelope.IdempotencyKey);
        cmd.Parameters.AddWithValue("$causation_id", (object?)envelope.CausationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$correlation_id", (object?)envelope.CorrelationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$producer_cluster", producerCluster);
        cmd.Parameters.AddWithValue("$payload_json", payloadJson);

        var inserted = await cmd.ExecuteNonQueryAsync(ct);
        if (inserted == 1)
            return envelope.EventId;

        // Dedup happened. Look up the existing event_id for the
        // matching (tenant_id, idempotency_key) and return it.
        await using var lookup = _connection.CreateCommand();
        lookup.CommandText = """
            SELECT event_id FROM domain_events
            WHERE tenant_id = $tenant_id AND idempotency_key = $idempotency_key
            LIMIT 1;
            """;
        lookup.Parameters.AddWithValue("$tenant_id", envelope.TenantId.Value);
        lookup.Parameters.AddWithValue("$idempotency_key", envelope.IdempotencyKey);
        var existing = (string?)await lookup.ExecuteScalarAsync(ct);
        return existing ?? throw new InvalidOperationException(
            "Idempotency-key dedup happened but no row found on lookup; " +
            "indicates a concurrent DELETE (forbidden per §4 append-only).");
    }

    public async Task<IReadOnlyList<RawDomainEvent>> GetAfterCursorAsync(
        TenantId tenantId,
        string? afterEventId,
        int batchSize,
        CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = afterEventId is null
            ? """
              SELECT * FROM domain_events
              WHERE tenant_id = $tenant_id
              ORDER BY event_id ASC
              LIMIT $limit;
              """
            : """
              SELECT * FROM domain_events
              WHERE tenant_id = $tenant_id AND event_id > $after_event_id
              ORDER BY event_id ASC
              LIMIT $limit;
              """;
        cmd.Parameters.AddWithValue("$tenant_id", tenantId.Value);
        if (afterEventId is not null)
            cmd.Parameters.AddWithValue("$after_event_id", afterEventId);
        cmd.Parameters.AddWithValue("$limit", batchSize);

        var results = new List<RawDomainEvent>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(ReadRow(reader));
        return results;
    }

    public async Task<RawDomainEvent?> FindByIdempotencyKeyAsync(
        TenantId tenantId,
        string idempotencyKey,
        CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM domain_events
            WHERE tenant_id = $tenant_id AND idempotency_key = $idempotency_key
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$tenant_id", tenantId.Value);
        cmd.Parameters.AddWithValue("$idempotency_key", idempotencyKey);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        return ReadRow(reader);
    }

    private static RawDomainEvent ReadRow(SqliteDataReader reader) =>
        new RawDomainEvent
        {
            EventId = reader.GetString(reader.GetOrdinal("event_id")),
            EventType = reader.GetString(reader.GetOrdinal("event_type")),
            SchemaVersion = reader.GetInt32(reader.GetOrdinal("schema_version")),
            OccurredAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("occurred_at"))),
            RecordedAtUtc = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("recorded_at_utc"))),
            TenantId = new TenantId(reader.GetString(reader.GetOrdinal("tenant_id"))),
            OriginatingReplicaId = new ReplicaId(reader.GetString(reader.GetOrdinal("originating_replica_id"))),
            IdempotencyKey = reader.GetString(reader.GetOrdinal("idempotency_key")),
            CausationId = reader.IsDBNull(reader.GetOrdinal("causation_id"))
                ? null
                : reader.GetString(reader.GetOrdinal("causation_id")),
            CorrelationId = reader.IsDBNull(reader.GetOrdinal("correlation_id"))
                ? null
                : reader.GetString(reader.GetOrdinal("correlation_id")),
            PayloadJson = reader.GetString(reader.GetOrdinal("payload_json")),
        };
}
```

(Code is illustrative; COB to fit to project's exception conventions
and async patterns. Use ConfigureAwait where appropriate per repo
convention.)

#### Embedded resource declaration

Add to `Sunfish.Foundation.Events.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Sql\*.sql" />
</ItemGroup>
```

Verify with `dotnet build` that the resource is named
`Sunfish.Foundation.Events.Sql.001-create-domain-events.sql` (matches
the `GetManifestResourceStream` call in
`ApplyMigrationsAsync`).

#### Tests (PR 2)

**`packages/foundation-events/tests/SqliteDomainEventStoreTests.cs`** —
each test creates a fresh in-memory SQLite connection and applies the
migration:

```csharp
private static async Task<SqliteDomainEventStore> CreateStoreAsync()
{
    var conn = new SqliteConnection("Data Source=:memory:");
    await conn.OpenAsync();
    var store = new SqliteDomainEventStore(conn, TimeProvider.System);
    await store.ApplyMigrationsAsync(default);
    return store;
}
```

Test methods:

- `ApplyMigrationsAsync_OnFreshDb_CreatesDomainEventsTable` — verify
  table exists via `PRAGMA table_info(domain_events)`.
- `ApplyMigrationsAsync_RepeatedCall_IsIdempotent` — call twice, no
  error.
- `AppendAsync_HappyPath_PersistsAndReturnsEventId`.
- `AppendAsync_OnDuplicateIdempotencyKey_ReturnsExistingEventId` — call
  twice with same key; second call returns first row's eventId; only
  one row in table.
- `AppendAsync_OnDuplicateIdempotencyKey_DifferentTenants_BothPersist`
  — same idempotency key but different tenants → both rows persist
  (tenant scope of the unique index).
- `AppendAsync_PersistsAllEnvelopeFields` — every field round-trips
  (including null causation/correlation).
- `AppendAsync_DerivesProducerClusterFromEventType` — emit
  `"Financial.JournalEntryPosted"`; assert
  `producer_cluster = "financial"`.
- `AppendAsync_RejectsEventTypeWithoutDotPrefix` — emit `"BadEventType"`
  (no dot); assert `ArgumentException`.
- `AppendAsync_RecordedAtUtc_IsSetToClockNow` — use a fake
  `TimeProvider` returning a fixed instant; assert
  `recorded_at_utc` matches.
- `GetAfterCursorAsync_WithNullCursor_ReturnsAllEventsInOrder` —
  append 5 events; cursor=null; assert all 5 returned in ULID order.
- `GetAfterCursorAsync_WithCursor_ReturnsOnlyAfter` — append 5; cursor
  to event 3; assert events 4 + 5 returned.
- `GetAfterCursorAsync_RespectsBatchSize`.
- `GetAfterCursorAsync_TenantIsolation_OnlyReturnsCurrentTenant` —
  append events for tenant A and tenant B; cursor for A; only A's
  events returned.
- `FindByIdempotencyKeyAsync_ExistingKey_ReturnsRow`.
- `FindByIdempotencyKeyAsync_MissingKey_ReturnsNull`.
- `Schema_DomainEventsTable_HasUniqueIndexOnTenantAndIdempotencyKey` —
  introspect SQLite schema; assert the unique index exists.
- `Schema_DomainEventsTable_HasNoUpdateTrigger` — verify there's no
  UPDATE trigger (the design is append-only; out-of-the-box SQLite
  permits UPDATE so this test is documentation, but is worth keeping
  as a regression).

Total new tests this PR: ~15–17.

#### Verification

- `dotnet build` succeeds.
- `dotnet test packages/foundation-events/tests/` passes (all tests:
  PR 1 carryover + new PR 2 tests).
- Migration SQL is valid SQLite (verified by tests applying it
  successfully).
- The `domain_events` table is **never** the target of an UPDATE
  statement from this package (grep `packages/foundation-events/` for
  `"UPDATE domain_events"` → expect zero hits).

#### Halt conditions

1. **Microsoft.Data.Sqlite package version conflict** with another
   package in the repo (e.g., a kernel package pinning an older
   version). Halt + file `cob-question-*` with the conflicting
   versions; XO will rule on which version wins.

2. **Embedded resource not found at runtime** (the
   `GetManifestResourceStream` call returns null). Likely cause: the
   `EmbeddedResource Include` path doesn't match the actual file
   location, or the `RootNamespace` isn't what
   `GetManifestResourceStream` expects. Fix the `EmbeddedResource`
   declaration; if blocked > 30 min, halt + file `cob-question-*`.

3. **Existing `domain_events` table from another package** — if `grep
   -rn "CREATE TABLE.*domain_events" packages/` returns a hit outside
   `foundation-events`, halt + file `cob-question-*`. The substrate
   owns this table exclusively.

---

### PR 3 — `DefaultDomainEventPublisher` implementation

**Estimated effort:** ~2h
**Scope:** wire the publisher to the store + dispatcher; in-process
event dispatch via `IEventDispatcher`; ~12 tests covering idempotency,
tenant context propagation, error handling.
**Commit subject:** `feat(foundation-events): add DefaultDomainEventPublisher + IEventDispatcher in-process broadcast`
**Branch:** `cob/foundation-events-default-publisher`
**Depends on:** PR 2 merged

#### Implementation

**`packages/foundation-events/DefaultDomainEventPublisher.cs`:**

```csharp
namespace Sunfish.Foundation.Events;

using System.Text.Json;

/// <summary>
/// Default <see cref="IDomainEventPublisher"/>. Persists the envelope to
/// <see cref="IDomainEventStore"/> + notifies in-process subscribers
/// via <see cref="IEventDispatcher"/>.
/// </summary>
public sealed class DefaultDomainEventPublisher : IDomainEventPublisher
{
    private readonly IDomainEventStore _store;
    private readonly IEventDispatcher _dispatcher;
    private readonly JsonSerializerOptions _jsonOptions;

    public DefaultDomainEventPublisher(
        IDomainEventStore store,
        IEventDispatcher dispatcher)
    {
        _store = store;
        _dispatcher = dispatcher;
        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
    }

    public async Task PublishAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var persistedEventId = await _store.AppendAsync(envelope, ct);

        // If dedup happened (persistedEventId != envelope.EventId), we
        // skip the in-process dispatch — the original event already
        // dispatched on its first emit.
        if (persistedEventId != envelope.EventId)
            return;

        var raw = new RawDomainEvent
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            SchemaVersion = envelope.SchemaVersion,
            OccurredAt = envelope.OccurredAt,
            RecordedAtUtc = envelope.OccurredAt,  // best-effort; the store has the canonical
            TenantId = envelope.TenantId,
            OriginatingReplicaId = envelope.OriginatingReplicaId,
            IdempotencyKey = envelope.IdempotencyKey,
            CausationId = envelope.CausationId,
            CorrelationId = envelope.CorrelationId,
            PayloadJson = JsonSerializer.Serialize(envelope.Payload, _jsonOptions),
        };

        await _dispatcher.DispatchAsync(raw, ct);
    }
}
```

**`packages/foundation-events/InProcessEventDispatcher.cs`:**

```csharp
namespace Sunfish.Foundation.Events;

using System.Collections.Concurrent;

/// <summary>
/// In-process event dispatcher. Registered handlers are invoked for
/// every event the publisher records. Push-style delivery; complements
/// the cursor-driven pull from <see cref="IEventReader"/> (PR 4).
/// </summary>
public sealed class InProcessEventDispatcher : IEventDispatcher
{
    private readonly ConcurrentBag<Func<RawDomainEvent, CancellationToken, Task>> _subscribers = new();

    public void Subscribe(Func<RawDomainEvent, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _subscribers.Add(handler);
    }

    public async Task DispatchAsync(RawDomainEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);

        // Fan out concurrently; collect failures.
        var tasks = _subscribers.Select(sub => SafeInvokeAsync(sub, evt, ct)).ToArray();
        await Task.WhenAll(tasks);
    }

    private static async Task SafeInvokeAsync(
        Func<RawDomainEvent, CancellationToken, Task> handler,
        RawDomainEvent evt,
        CancellationToken ct)
    {
        try
        {
            await handler(evt, ct);
        }
        catch (Exception ex)
        {
            // In-process dispatch is best-effort + non-blocking; failures
            // do NOT propagate to the publisher. The IEventReader
            // (PR 4) cursor walk + retry-with-backoff is the durable
            // path. Log here when a logger is wired in.
            System.Diagnostics.Debug.WriteLine(
                $"Foundation-events in-process dispatch failure: {ex}");
        }
    }
}
```

**Note:** in-process dispatch is **best-effort**; it's a convenience
for low-latency in-process handlers. The **durable** delivery path is
the per-handler cursor walk in PR 4. Cluster handlers should register
via `IEventReader.RegisterHandlerAsync` (PR 4 API) for guaranteed
at-least-once delivery; `InProcessEventDispatcher.Subscribe` is for
low-latency hot-path observers (e.g., a UI live-feed) that can tolerate
event loss.

#### Tests (PR 3)

**`packages/foundation-events/tests/DefaultDomainEventPublisherTests.cs`:**

- `PublishAsync_HappyPath_PersistsToStoreAndDispatches` — verify both
  store has the row AND dispatcher saw the event.
- `PublishAsync_OnDedup_DoesNotRedispatch` — emit envelope; emit again
  with same idempotency key; assert dispatcher saw 1 event, not 2.
- `PublishAsync_OnNullEnvelope_ThrowsArgumentNullException`.
- `PublishAsync_PreservesAllEnvelopeFields_InRawDispatch` — assert the
  `RawDomainEvent` handed to the dispatcher carries every field.
- `PublishAsync_OnStoreFailure_BubblesException` — fake store that
  throws; assert publisher rethrows + dispatcher is NOT called.

**`packages/foundation-events/tests/InProcessEventDispatcherTests.cs`:**

- `Subscribe_AddsHandlerToFanout`.
- `DispatchAsync_InvokesAllSubscribers`.
- `DispatchAsync_OnSubscriberThrow_OtherSubscribersStillInvoked` —
  failures are isolated.
- `DispatchAsync_OnNullEvent_ThrowsArgumentNullException`.
- `Subscribe_OnNullHandler_ThrowsArgumentNullException`.
- `DispatchAsync_CompletesEvenIfNoSubscribers`.
- `DispatchAsync_RespectsCancellation` — long-running subscriber +
  cancellation token cancelled mid-flight.

Total new tests this PR: ~12.

#### Verification

- `dotnet build` succeeds.
- `dotnet test packages/foundation-events/tests/` passes.
- The dedup-and-suppress-dispatch behavior is covered by a test.
- In-process dispatcher failures are isolated (one bad subscriber
  doesn't poison the fan-out).

#### Halt conditions

1. **A real logger surface gets injected mid-PR** (someone wires
   `Microsoft.Extensions.Logging` in). Replace the
   `Debug.WriteLine` with `ILogger`-based logging if a canonical
   pattern exists in `packages/foundation-*/`. If unsure, halt + file
   `cob-question-*`.

---

### PR 4 — Consumer cursor model (`SqliteEventReader` +
`EventDispatcherHost`)

**Estimated effort:** ~3h
**Scope:** per-handler cursor persistence in `event_handler_cursors`
table; background dispatcher that drains events to registered handlers;
~15 tests including cursor advance on success, cursor stays on failure,
handler ordering.
**Commit subject:** `feat(foundation-events): add SqliteEventReader + EventDispatcherHost per-handler cursor model`
**Branch:** `cob/foundation-events-cursor-model`
**Depends on:** PR 2 + PR 3 merged

#### Migration

**`packages/foundation-events/Sql/002-create-handler-cursors.sql`:**

```sql
-- foundation-events migration 002 — create event_handler_cursors.
-- Per cross-cluster-event-bus-design.md §5 "Per-replica position cursors".
-- This table is MUTABLE (cursor advance is the only write).

CREATE TABLE IF NOT EXISTS event_handler_cursors (
    handler_id              TEXT NOT NULL,
    tenant_id               TEXT NOT NULL,
    last_handled_event_id   TEXT,
    last_handled_at         TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (handler_id, tenant_id)
);

CREATE TABLE IF NOT EXISTS event_handler_failures (
    id              TEXT NOT NULL PRIMARY KEY,
    handler_id      TEXT NOT NULL,
    event_id        TEXT NOT NULL,
    tenant_id       TEXT NOT NULL,
    attempt_number  INTEGER NOT NULL,
    failed_at       TEXT NOT NULL,
    error_message   TEXT NOT NULL,
    next_retry_at   TEXT,
    resolved_at     TEXT
);

CREATE INDEX IF NOT EXISTS idx_handler_failures_retry
    ON event_handler_failures(next_retry_at)
    WHERE resolved_at IS NULL AND next_retry_at IS NOT NULL;
```

Wire into `SqliteDomainEventStore.ApplyMigrationsAsync` (or add a new
`SqliteEventReader.ApplyMigrationsAsync`; pick the simpler path). The
store + reader share the same SQLite connection in v1.

#### Implementation

**`packages/foundation-events/SqliteEventReader.cs`:**

```csharp
namespace Sunfish.Foundation.Events;

using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Sunfish.Foundation.Assets.Common;

/// <summary>
/// SQLite-backed <see cref="IEventReader"/>. Walks the
/// <c>domain_events</c> table from each handler's cursor forward and
/// invokes the handler on each new event. Cursor advance is per-handler;
/// failures keep the cursor pinned so the handler revisits the event on
/// the next dispatch cycle.
/// </summary>
public sealed class SqliteEventReader : IEventReader
{
    private readonly SqliteConnection _connection;
    private readonly IDomainEventStore _store;
    private readonly TimeProvider _clock;

    // Registered handlers indexed by handlerId.
    private readonly ConcurrentDictionary<string, HandlerRegistration> _handlers = new();

    public SqliteEventReader(
        SqliteConnection connection,
        IDomainEventStore store,
        TimeProvider clock)
    {
        _connection = connection;
        _store = store;
        _clock = clock;
    }

    public Task RegisterHandlerAsync(
        string handlerId,
        string? eventType,
        IEventHandler<RawDomainEvent> handler,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerId);
        ArgumentNullException.ThrowIfNull(handler);

        _handlers.AddOrUpdate(
            handlerId,
            _ => new HandlerRegistration(handlerId, eventType, handler),
            (_, _) => throw new InvalidOperationException(
                $"Handler '{handlerId}' is already registered. " +
                "Per cross-cluster-event-bus-design.md §5: handler ids are " +
                "stable + unique per process."));
        return Task.CompletedTask;
    }

    /// <summary>Drain one batch of events to all handlers. Called by
    /// <see cref="EventDispatcherHost"/> on a loop.</summary>
    public async Task<int> DrainOnceAsync(
        TenantId tenantId,
        int batchSize,
        CancellationToken ct)
    {
        var totalProcessed = 0;
        foreach (var reg in _handlers.Values)
        {
            var cursor = await GetCursorAsync(reg.HandlerId, tenantId, ct);
            var events = await _store.GetAfterCursorAsync(tenantId, cursor, batchSize, ct);
            foreach (var evt in events)
            {
                if (reg.EventTypeFilter is not null && evt.EventType != reg.EventTypeFilter)
                {
                    // Filter mismatch — advance cursor past this event without invoking handler.
                    await SetCursorAsync(reg.HandlerId, tenantId, evt.EventId, ct);
                    continue;
                }

                try
                {
                    await reg.Handler.HandleAsync(evt, ct);
                    await SetCursorAsync(reg.HandlerId, tenantId, evt.EventId, ct);
                    totalProcessed++;
                }
                catch (Exception ex)
                {
                    await RecordFailureAsync(reg.HandlerId, evt, tenantId, ex, ct);
                    // Cursor NOT advanced. Handler will revisit this event on the next drain.
                    break;  // Stop processing further events for this handler this cycle.
                }
            }
        }
        return totalProcessed;
    }

    private async Task<string?> GetCursorAsync(
        string handlerId, TenantId tenantId, CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT last_handled_event_id FROM event_handler_cursors
            WHERE handler_id = $handler_id AND tenant_id = $tenant_id;
            """;
        cmd.Parameters.AddWithValue("$handler_id", handlerId);
        cmd.Parameters.AddWithValue("$tenant_id", tenantId.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result switch { string s => s, _ => null };
    }

    private async Task SetCursorAsync(
        string handlerId, TenantId tenantId, string eventId, CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO event_handler_cursors (handler_id, tenant_id, last_handled_event_id, last_handled_at)
            VALUES ($handler_id, $tenant_id, $event_id, $now)
            ON CONFLICT(handler_id, tenant_id) DO UPDATE
                SET last_handled_event_id = $event_id,
                    last_handled_at = $now;
            """;
        cmd.Parameters.AddWithValue("$handler_id", handlerId);
        cmd.Parameters.AddWithValue("$tenant_id", tenantId.Value);
        cmd.Parameters.AddWithValue("$event_id", eventId);
        cmd.Parameters.AddWithValue("$now", _clock.GetUtcNow().ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task RecordFailureAsync(
        string handlerId, RawDomainEvent evt, TenantId tenantId,
        Exception ex, CancellationToken ct)
    {
        var attempt = await GetAttemptCountAsync(handlerId, evt.EventId, ct) + 1;
        var nextRetry = ComputeNextRetry(attempt, _clock.GetUtcNow());

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO event_handler_failures
                (id, handler_id, event_id, tenant_id, attempt_number, failed_at, error_message, next_retry_at)
            VALUES
                ($id, $handler_id, $event_id, $tenant_id, $attempt, $failed_at, $error_message, $next_retry_at);
            """;
        cmd.Parameters.AddWithValue("$id", Ulid.NewUlid().ToString());
        cmd.Parameters.AddWithValue("$handler_id", handlerId);
        cmd.Parameters.AddWithValue("$event_id", evt.EventId);
        cmd.Parameters.AddWithValue("$tenant_id", tenantId.Value);
        cmd.Parameters.AddWithValue("$attempt", attempt);
        cmd.Parameters.AddWithValue("$failed_at", _clock.GetUtcNow().ToString("O"));
        cmd.Parameters.AddWithValue("$error_message", ex.Message);
        cmd.Parameters.AddWithValue("$next_retry_at",
            (object?)nextRetry?.ToString("O") ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<int> GetAttemptCountAsync(string handlerId, string eventId, CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM event_handler_failures
            WHERE handler_id = $handler_id AND event_id = $event_id;
            """;
        cmd.Parameters.AddWithValue("$handler_id", handlerId);
        cmd.Parameters.AddWithValue("$event_id", eventId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    /// <summary>Backoff schedule per cross-cluster-event-bus-design.md §6:
    /// 30s, 2m, 10m, 1h, 6h, 24h, 72h. After 7 attempts, returns null
    /// (retry exhausted).</summary>
    private static DateTimeOffset? ComputeNextRetry(int attempt, DateTimeOffset now) =>
        attempt switch
        {
            1 => now.AddSeconds(30),
            2 => now.AddMinutes(2),
            3 => now.AddMinutes(10),
            4 => now.AddHours(1),
            5 => now.AddHours(6),
            6 => now.AddHours(24),
            7 => now.AddHours(72),
            _ => null,  // exhausted
        };

    private sealed record HandlerRegistration(
        string HandlerId,
        string? EventTypeFilter,
        IEventHandler<RawDomainEvent> Handler);
}
```

**`packages/foundation-events/EventDispatcherHost.cs`:**

```csharp
namespace Sunfish.Foundation.Events;

using Microsoft.Extensions.Hosting;
using Sunfish.Foundation.Assets.Common;

/// <summary>
/// Background service that drains events from
/// <see cref="SqliteEventReader"/> on a polling interval. Per
/// cross-cluster-event-bus-design.md §5 + §6.
/// </summary>
public sealed class EventDispatcherHost : BackgroundService
{
    private readonly SqliteEventReader _reader;
    private readonly TimeSpan _pollInterval;

    public EventDispatcherHost(
        SqliteEventReader reader,
        TimeSpan? pollInterval = null)
    {
        _reader = reader;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Tenant-scoped drain. The host needs a way to discover
                // active tenants; v1 punts on this and uses a single
                // tenant context. Multi-tenant drain is a v2 concern
                // (consult ITenantCatalog in foundation-multitenancy).
                // For now the host accepts a tenant id at startup; see
                // ServiceCollectionExtensions.AddFoundationEvents(...).
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
                break;
            }
        }
    }
}
```

**Note on tenant-scoped drain:** v1 punts on multi-tenant drain. The
`EventDispatcherHost` accepts a single `TenantId` at construction (set
via DI from the host composition root, which knows its current tenant
context). Multi-tenant Anchor instances (very rare; the canonical
Anchor model is one tenant per local replica) will need a v2
enhancement that walks `ITenantCatalog`. **Halt condition:** if PR 6's
DI extension forces a multi-tenant pattern, defer to a follow-on
workstream + file `cob-question-*`.

#### Tests (PR 4)

**`packages/foundation-events/tests/SqliteEventReaderTests.cs`:**

- `RegisterHandlerAsync_HappyPath_RegistersHandler`.
- `RegisterHandlerAsync_DuplicateHandlerId_Throws`.
- `RegisterHandlerAsync_NullHandler_ThrowsArgumentNullException`.
- `DrainOnceAsync_WithRegisteredHandler_InvokesOnNewEvents` — register
  handler; append 3 events; drain; assert handler called 3 times +
  cursor at event 3.
- `DrainOnceAsync_FilteredByEventType_OnlyMatchingEventsInvoke` —
  register handler filter `"Financial.JournalEntryPosted"`; append
  mix of types; only matching events invoke.
- `DrainOnceAsync_OnHandlerThrow_CursorStaysPinned` — handler throws on
  event 2; assert handler called for event 1 (cursor advanced) +
  event 2 (cursor NOT advanced); next drain re-invokes on event 2.
- `DrainOnceAsync_OnHandlerThrow_RecordsFailureRow` — assert
  `event_handler_failures` has a row with attempt=1.
- `DrainOnceAsync_AfterMultipleFailures_AttemptNumberIncrements`.
- `DrainOnceAsync_AfterFailure_OtherHandlersStillProceed` — two
  handlers; one fails; other still drains.
- `DrainOnceAsync_TenantIsolation_OnlyDrainsCurrentTenant`.
- `DrainOnceAsync_EmptyEvents_NoOpReturnsZero`.
- `ComputeNextRetry_Attempt1_Returns30s` — verify backoff schedule.
- `ComputeNextRetry_Attempt7_Returns72h`.
- `ComputeNextRetry_Attempt8_ReturnsNull` (exhausted).
- `DrainOnceAsync_CursorAdvances_AcrossDrainCycles` — multiple drain
  calls advance cursor monotonically.

**`packages/foundation-events/tests/EventDispatcherHostTests.cs`:**

- `ExecuteAsync_StartsDrainLoop` — start the host; append event;
  wait > poll interval; assert handler invoked.
- `ExecuteAsync_OnCancellation_ExitsCleanly`.

Total new tests this PR: ~17.

#### Verification

- `dotnet build` succeeds.
- `dotnet test` passes.
- Cursor-stays-on-failure behavior demonstrably re-invokes handler on
  next drain (regression test).

#### Halt conditions

1. **`Microsoft.Extensions.Hosting` is not in the consumer host's DI
   container** (e.g., Anchor MAUI doesn't use it). Then
   `EventDispatcherHost : BackgroundService` won't auto-start. Halt +
   file `cob-question-*` — XO will rule on whether to provide a
   standalone driver class instead of a `BackgroundService`.

2. **Multi-tenant dispatch surfaces an architecture question** (e.g.,
   the Surface Pro CO acceptance test exercises 2+ tenants
   simultaneously). Defer multi-tenant drain to a follow-on; v1 ships
   single-tenant.

---

### PR 5 — Loro op-log integration (OPTIONAL — deferrable)

**Estimated effort:** ~2–3h IF Loro CRDT patterns are already
established in the repo; UP TO ~8h if Loro integration patterns need
to be designed from scratch.
**Scope:** bridge that wraps `DefaultDomainEventPublisher` and appends
the event as a Loro list-insert op after SQLite persistence; cross-
replica sync; ~8 tests.
**Commit subject:** `feat(foundation-events): add LoroDomainEventBridge for cross-replica event sync`
**Branch:** `cob/foundation-events-loro-bridge`
**Depends on:** PR 4 merged + Loro CRDT integration patterns ratified
elsewhere

#### Defer decision

**This PR is OPTIONAL.** Per the scope brief and per
`cross-cluster-event-bus-design.md` §4 "Loro op-log integration", the
canonical sync mechanism for `domain_events` rows is Loro CRDT list-op
replication. **However**, PR 5 should be **deferred to a follow-on
workstream** if any of the following are true:

1. Loro CRDT integration patterns are NOT yet ratified in the repo
   (verify via `grep -rn "Loro\|loro-crdt\|LoroDoc" packages/
   --include="*.cs"` — if no hits OR if existing hits are in `tests/`
   only, defer).
2. The `IBlobStore` or content-addressed-storage primitives Loro
   replication may depend on are not yet in `foundation-localfirst` or
   `kernel-buckets` (verify via `grep -rn "IBlobStore" packages/` — if
   the surface is incomplete, defer).
3. PRs 1–4 land cleanly and the substrate is usable WITHOUT Loro sync
   (it is — single-replica Anchor instances don't need cross-replica
   sync; events still work via the per-handler cursor model).

**Recommendation: defer PR 5 to a follow-on workstream titled
"`foundation-events-loro-bridge`" pending Loro integration substrate
ratification.** Ship PRs 1, 2, 3, 4, 6 as the v1 substrate; PR 5
lands when Loro patterns are clarified.

#### If shipping PR 5 in this hand-off

**`packages/foundation-events/LoroDomainEventBridge.cs`:**

```csharp
namespace Sunfish.Foundation.Events;

/// <summary>
/// Wraps <see cref="DefaultDomainEventPublisher"/> and appends each
/// successfully-persisted event as a Loro list-insert op for cross-
/// replica sync. Per cross-cluster-event-bus-design.md §4 "Loro op-log
/// integration".
/// </summary>
public sealed class LoroDomainEventBridge : IDomainEventPublisher
{
    private readonly DefaultDomainEventPublisher _inner;
    private readonly ILoroDocAccessor _loroDoc;  // injected from kernel-sync

    public LoroDomainEventBridge(
        DefaultDomainEventPublisher inner,
        ILoroDocAccessor loroDoc)
    {
        _inner = inner;
        _loroDoc = loroDoc;
    }

    public async Task PublishAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken ct)
    {
        await _inner.PublishAsync(envelope, ct);
        // Append to Loro list. Loro's CRDT-list semantics handle
        // cross-replica merge; the unique-key constraint on the
        // local domain_events table dedups on receipt.
        await _loroDoc.AppendDomainEventAsync(envelope, ct);
    }
}
```

`ILoroDocAccessor` is a substrate-level shim that wraps the Loro
library's `Doc` type. The actual Loro library binding likely lives
in `kernel-sync` or `kernel-crdt`; if not, PR 5 cannot complete and
must defer. **Halt condition:** if `ILoroDocAccessor` doesn't exist
and there's no clear binding path, halt + file `cob-question-*`.

#### Tests (PR 5)

If shipping:

- `PublishAsync_AppendsToLoroAfterSqliteWrite`.
- `PublishAsync_OnSqliteFailure_DoesNotAppendToLoro`.
- `PublishAsync_OnLoroFailure_RollsBackSqlite`.
- `PublishAsync_OnDedupInSqlite_DoesNotAppendToLoro` (avoid double-
  append).
- Plus integration-style tests with two simulated replicas.

Total new tests this PR (if shipping): ~8.

#### Verification (if shipping)

- Cross-replica sync demonstrably propagates events.
- Dedup works correctly on receipt.

---

### PR 6 — `AddFoundationEvents()` DI extension + cluster migration
sweep

**Estimated effort:** ~2h
**Scope:** `AddFoundationEvents()` DI extension; migration sweep
replacing `blocks-financial-periods` local `IDomainEventPublisher` with
the canonical substrate; `apps/docs/foundation/events/overview.md`
walkthrough; ~5 integration tests covering full pipeline.
**Commit subject:** `feat(foundation-events): add AddFoundationEvents() DI extension + DI-swap sweep for blocks-financial-periods`
**Branch:** `cob/foundation-events-di-and-sweep`
**Depends on:** PR 4 merged (PR 5 if shipping)

#### DI extension

**`packages/foundation-events/ServiceCollectionExtensions.cs`:**

```csharp
namespace Sunfish.Foundation.Events;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Assets.Common;

/// <summary>DI conveniences for Sunfish.Foundation.Events.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the canonical cross-cluster event-bus substrate:
    /// <see cref="SqliteDomainEventStore"/>,
    /// <see cref="DefaultDomainEventPublisher"/>,
    /// <see cref="SqliteEventReader"/>,
    /// <see cref="InProcessEventDispatcher"/>, and
    /// <see cref="EventDispatcherHost"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Consumer clusters (e.g., <c>blocks-financial-periods</c>,
    /// <c>blocks-work-orders</c>) MUST resolve
    /// <see cref="IDomainEventPublisher"/> via DI after this method
    /// runs; they MUST NOT register their own local Noop fallback.
    /// </para>
    /// <para>
    /// Requires a singleton <see cref="SqliteConnection"/> already
    /// registered (typically by the host's Anchor / Bridge composition
    /// root). Requires <see cref="TimeProvider"/>.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddFoundationEvents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IDomainEventStore, SqliteDomainEventStore>();
        services.AddSingleton<SqliteDomainEventStore>(sp =>
            (SqliteDomainEventStore)sp.GetRequiredService<IDomainEventStore>());
        services.AddSingleton<IEventDispatcher, InProcessEventDispatcher>();
        services.AddSingleton<InProcessEventDispatcher>(sp =>
            (InProcessEventDispatcher)sp.GetRequiredService<IEventDispatcher>());
        services.AddSingleton<IDomainEventPublisher, DefaultDomainEventPublisher>();
        services.AddSingleton<IEventReader, SqliteEventReader>();
        services.AddSingleton<SqliteEventReader>(sp =>
            (SqliteEventReader)sp.GetRequiredService<IEventReader>());
        services.AddHostedService<EventDispatcherHost>();
        return services;
    }

    /// <summary>Apply foundation-events SQLite migrations once at
    /// host startup. The host is responsible for invoking this AFTER
    /// the SqliteConnection is opened + BEFORE any cluster registers
    /// an event publisher.</summary>
    public static async Task ApplyFoundationEventsMigrationsAsync(
        this IServiceProvider services,
        CancellationToken ct)
    {
        var store = services.GetRequiredService<SqliteDomainEventStore>();
        await store.ApplyMigrationsAsync(ct);
    }
}
```

#### Cluster migration sweep — `blocks-financial-periods`

Per the trigger ruling, `blocks-financial-periods` is the **single
existing consumer** of a local `IDomainEventPublisher` (shipped in
PR #908). PR 6 migrates it to the canonical:

1. **Re-namespace the type reference.**

   Find every `using Sunfish.Blocks.Financial.Periods.Events;` (or
   wherever the local interface lives — verify via `grep -rn
   "Sunfish.Blocks.Financial.Periods.Events"
   packages/blocks-financial-periods/`).

   Replace with `using Sunfish.Foundation.Events;`.

2. **Delete the local interface + Noop.**

   Files to delete (verify exact paths by grepping):

   ```bash
   rm packages/blocks-financial-periods/Services/IDomainEventPublisher.cs
   rm packages/blocks-financial-periods/Services/NoopDomainEventPublisher.cs
   rm packages/blocks-financial-periods/Services/DomainEventEnvelope.cs  # if exists locally
   ```

   (Adapt path; the actual filenames may differ. The
   trigger ruling says the local interface lives in
   `packages/blocks-financial-periods/Services/`.)

3. **Update DI registration.**

   Find the `blocks-financial-periods` DI extension (likely
   `packages/blocks-financial-periods/DependencyInjection/ServiceCollectionExtensions.cs`).
   Remove the line:

   ```csharp
   services.TryAddSingleton<IDomainEventPublisher, NoopDomainEventPublisher>();
   ```

   The canonical publisher is registered by `AddFoundationEvents()` in
   the host composition root; the cluster no longer needs a fallback.

4. **Update tests.**

   Tests that mock `IDomainEventPublisher` to verify emit behavior now
   import the canonical interface from `Sunfish.Foundation.Events`.
   The tests' fake-publisher must implement the canonical signature
   (`PublishAsync<TPayload>(DomainEventEnvelope<TPayload>, CancellationToken)`).
   If PR 3 of `blocks-financial-periods` already widened the local
   interface to match the canonical envelope (per the trigger ruling),
   this is a using-statement change only.

5. **Bump `last-pr` in the hand-off doc.**

   File:
   `icm/_state/handoffs/blocks-financial-periods-stage06-handoff.md`.
   Add a note at the bottom:

   ```
   ---
   ## 2026-05-NN (PR #NNN) — DI-swap to foundation-events canonical
   Per `foundation-events-stage06-handoff.md` PR 6 sweep, the local
   `IDomainEventPublisher` + `NoopDomainEventPublisher` were deleted
   and call-sites re-namespaced to `Sunfish.Foundation.Events`. No
   behavior change.
   ```

6. **Verify build + test passes.**

   ```bash
   dotnet build packages/blocks-financial-periods/
   dotnet test packages/blocks-financial-periods/tests/
   ```

7. **Sibling-cluster scan (forward-looking).**

   Per the trigger ruling §"Sibling-cluster routing", this same pattern
   applies to sibling clusters when they emit events. Run:

   ```bash
   grep -rln "interface IDomainEventPublisher" packages/blocks-*/ 2>/dev/null
   ```

   If **anything** else is found beyond `blocks-financial-periods`,
   include it in PR 6's sweep. As of 2026-05-16 the expected count is
   ONE (just `blocks-financial-periods`). If the count exceeds one,
   include them all.

#### Docs walkthrough

**`apps/docs/foundation/events/overview.md`:**

```markdown
# foundation-events

The canonical cross-cluster domain-event substrate for Sunfish.

## What it provides

- `DomainEventEnvelope<TPayload>` — the canonical envelope every
  cross-cluster event carries.
- `IDomainEventPublisher` — producer-side emit contract.
- `IDomainEventStore` — SQLite-backed append-only persistence (the
  `domain_events` table).
- `IEventReader` — consumer-side cursor model (per-handler cursors in
  the `event_handler_cursors` table).
- `IEventDispatcher` — in-process best-effort push delivery.
- `EventDispatcherHost` — background service that drains events to
  registered handlers.

## When to use

Every `blocks-*` cluster that emits cross-cluster events (per
`_shared/engineering/cross-cluster-event-bus-design.md` §3 catalog).
Examples:

- `blocks-financial-*` emits `Financial.JournalEntryPosted`,
  `Financial.PaymentApplied`, etc.
- `blocks-work-*` emits `Work.WorkOrderCompleted`,
  `Work.MilestoneAchieved`, etc.
- `blocks-people-*` emits `People.TenantActivated`,
  `People.TenancyEnded`, etc.

Consumers register handlers via `IEventReader.RegisterHandlerAsync`;
the dispatcher drains events from `domain_events` and invokes the
handlers.

## How to register (host composition root)

```csharp
services.AddFoundationEvents();
// ... later, after the SQLite connection is opened ...
await app.Services.ApplyFoundationEventsMigrationsAsync(default);
```

## Cluster-side emit example

```csharp
public sealed class PeriodCloseService
{
    private readonly IDomainEventPublisher _publisher;
    private readonly TimeProvider _clock;
    private readonly ITenantContext _tenant;
    private readonly IReplicaContext _replica;

    public async Task SoftClosePeriodAsync(FiscalPeriodId periodId, ActorId closedBy, CancellationToken ct)
    {
        // ... persist the soft-close to the cluster's tables ...

        var envelope = new DomainEventEnvelope<PeriodSoftClosedPayload>
        {
            EventId = Ulid.NewUlid().ToString(),
            EventType = "Financial.PeriodSoftClosed",
            SchemaVersion = 1,
            OccurredAt = _clock.GetUtcNow(),
            TenantId = _tenant.CurrentTenantId,
            OriginatingReplicaId = _replica.CurrentReplicaId,
            IdempotencyKey = $"Financial.PeriodSoftClosed|{_tenant.CurrentTenantId.Value}|{periodId.Value}|SoftClosed",
            Payload = new PeriodSoftClosedPayload(periodId, closedBy, _clock.GetUtcNow()),
        };

        await _publisher.PublishAsync(envelope, ct);
    }
}
```

## Cluster-side subscribe example

```csharp
public sealed class ProjectActualsUpserter : IEventHandler<RawDomainEvent>
{
    public async Task HandleAsync(RawDomainEvent evt, CancellationToken ct)
    {
        if (evt.EventType != "Financial.JournalEntryPosted") return;
        var payload = JsonSerializer.Deserialize<JournalEntryPostedPayload>(evt.PayloadJson)!;
        await _projectActuals.UpsertFromJournalLineAsync(payload, ct);
    }
}

// ... register at bootstrap:
await reader.RegisterHandlerAsync(
    handlerId: "blocks-work.ProjectActualsUpserter",
    eventType: "Financial.JournalEntryPosted",
    handler: serviceProvider.GetRequiredService<ProjectActualsUpserter>(),
    ct: default);
```

## Idempotency

Every event has a deterministic `IdempotencyKey`. Two emissions with
the same `(TenantId, IdempotencyKey)` collapse into one row at the
store. Consumers must therefore be idempotent: receiving the same
event twice produces the same effect as receiving it once.

See `_shared/engineering/cross-cluster-event-bus-design.md` §4 for the
canonical idempotency-key derivation rules per event type.

## Append-only semantics

`domain_events` is **never** UPDATEd or DELETEd in the normal path.
The substrate enforces this at the implementation level (no `UPDATE
domain_events` statement is ever issued by `SqliteDomainEventStore`).
Per `_shared/engineering/crdt-friendly-schema-conventions.md` §4 + §6.

Crypto-shred (GDPR / right-to-be-forgotten) is the only path that
modifies an existing row, and it is out of scope for the v1 substrate.

## Layering vs `kernel-event-bus`

`Sunfish.Foundation.Events` is the **business-domain** event layer.
`Sunfish.Kernel.Events` (in `packages/kernel-event-bus/`) is the
**kernel-tier** event log (sequence-numbered, signature-verified,
schema-registry-keyed; used by sync transport and audit chain).

The two coexist. A producer may emit to both, in the same SQLite
transaction. Neither subsumes the other.
```

#### Tests (PR 6)

**`packages/foundation-events/tests/IntegrationTests.cs`** — full-
pipeline tests using the DI container:

- `EndToEnd_PublishViaDIContainer_PersistsToDomainEventsTable` — wire
  up a minimal host with `AddFoundationEvents()`, apply migrations,
  publish via `IDomainEventPublisher` resolved from DI, query
  `domain_events` table directly to verify persistence.
- `EndToEnd_RegisterHandlerAndPublish_HandlerInvokedViaDispatcher` —
  register an `IEventHandler<RawDomainEvent>` via `IEventReader`;
  publish; assert handler invoked via cursor drain.
- `EndToEnd_TwoHandlers_BothInvokedIndependently` — register 2
  handlers; publish 1 event; assert both invoked; each has its own
  cursor.
- `EndToEnd_FailingHandler_DoesNotBlockOtherHandlers`.
- `EndToEnd_BlocksFinancialPeriodsEmit_RoundTripsViaCanonical` —
  after the sweep, simulate the soft-close flow: cluster service
  emits `Financial.PeriodSoftClosed`, an in-test handler subscribes,
  assert the event lands in the canonical
  store + handler receives it.

Total new tests this PR: ~5.

#### Verification

- `dotnet build` succeeds across the solution (foundation-events +
  blocks-financial-periods + every consumer affected by the sweep).
- `dotnet test` passes (~50–60 tests cumulative).
- `grep -rn "interface IDomainEventPublisher" packages/blocks-*/` —
  expect zero hits after the sweep.
- `grep -rn "using Sunfish.Foundation.Events" packages/blocks-*/` —
  expect at least one hit (`blocks-financial-periods`).
- `apps/docs/foundation/events/overview.md` renders correctly.

#### Halt conditions

1. **A second `blocks-*` cluster ships its own local
   `IDomainEventPublisher` between PR 1 and PR 6** (e.g., dev
   independently ships `blocks-financial-tax` with its own
   local fallback). Include it in the sweep. If unclear how to migrate
   it, halt + file `cob-question-*`.

2. **`blocks-financial-periods` tests fail post-sweep** for reasons
   unrelated to the namespace swap. Halt + file `cob-question-*`.

3. **The host composition root does not currently register a
   `SqliteConnection` singleton.** `AddFoundationEvents()` requires
   one. Either (a) the host already has one (most likely path; verify
   via grep) or (b) the substrate must ship its own connection
   construction (less likely; complicates testing). Halt + file
   `cob-question-*` if the resolution path is unclear.

---

## Cross-cluster events catalog reconciliation (advisory)

The catalog at `cross-cluster-event-bus-design.md` §3 lists 49 events
across 6 producer clusters:

| Cluster | Event count | Section |
|---|---|---|
| Financial | 12 | §3.1 |
| Work | 12 | §3.2 |
| People | 10 | §3.3 |
| Docs | 8 | §3.4 |
| Reports | 4 | §3.5 |
| Property | 5 | §3.6 |
| **Total** | **51** (was 49; updated in §3 of the design doc) | — |

**This hand-off does NOT author event-specific types.** That is
per-cluster work. Each cluster's own Stage 06 hand-off defines its
event-payload types (e.g., `JournalEntryPostedPayload`,
`WorkOrderCompletedPayload`). This hand-off establishes only the
substrate.

When a cluster ships an event payload type, the cluster's hand-off
references this substrate's `DomainEventEnvelope<T>` and idempotency
discipline. Example forward references for clusters not yet built:

- `blocks-financial-ledger` PR 4 (sibling hand-off) — emits
  `Financial.JournalEntryPosted` via the canonical substrate.
- `blocks-financial-ar` (forthcoming) — emits `Financial.InvoiceIssued`
  + `Financial.InvoiceVoided` + `Financial.InvoiceWrittenOff` +
  `Financial.PaymentApplied` + `Financial.PaymentUnapplied`.
- `blocks-financial-ap` (forthcoming) — emits `Financial.BillRecorded`.
- `blocks-financial-tax` (forthcoming) — emits
  `Reports.TaxFormLineMapEdited`.
- `blocks-people-foundation` (forthcoming) — emits
  `People.TenantActivated`, `People.TenancyEnded`, etc.
- `blocks-work-orders` (forthcoming) — emits `Work.WorkOrderCreated`,
  `Work.WorkOrderCompleted`.

---

## Critical halt conditions (cross-cutting)

The following halt conditions span multiple PRs. If any trigger, halt
the current PR + file `cob-question-*` for XO ruling.

1. **`Sunfish.Foundation.Events` namespace conflict** — grep first
   (pre-build step 1). If a pre-existing `Sunfish.Foundation.Events`
   namespace exists in another package, halt; XO will rule on
   reconciliation.

2. **SQLite migration sequence conflict** with existing
   `blocks-financial-ledger` SQLite migrations or another package's
   migrations. The `domain_events` migration must run **before** any
   cluster that emits events; verify migration ordering rules in the
   repo. If no migration sequencing pattern exists, halt + file
   `cob-question-*` to clarify (XO will likely rule that
   foundation-events ships its own self-applied migrations via
   `ApplyFoundationEventsMigrationsAsync`, which is the recommended
   path).

3. **`ReplicaId` value type ambiguity** — pre-build step 5. If
   `ReplicaId` exists in `foundation-localfirst` or similar, reuse;
   don't redeclare. If multiple `ReplicaId` types exist, halt + file
   `cob-question-*` for XO ruling on canonical home.

4. **Loro op-log integration (PR 5) surfaces a need for `IBlobStore`
   or similar** — defer PR 5; ship PRs 1–4 + 6 as v1; track PR 5 as a
   follow-on workstream titled "`foundation-events-loro-bridge`".

5. **`Sunfish.Foundation.Events` package framework target conflicts**
   with consumer clusters — verify target framework (`net11.0`) +
   nullable + langversion match existing `packages/foundation-*/`
   conventions via `Directory.Build.props` inheritance. If conflict,
   halt + file `cob-question-*`.

6. **`kernel-event-bus` integration ambiguity** — if cob believes
   `foundation-events` should be folded into `kernel-event-bus` (or
   vice versa), HALT. The layering is intentional (per
   "Relationship to `kernel-event-bus`" above). XO will not approve
   subsumption; if cob disagrees, file `cob-question-*`.

7. **`AddFoundationEvents()` DI registration introduces a circular
   dependency** with any consumer cluster (e.g., `blocks-financial-*`
   tries to `services.AddBlocksFinancialPeriods()` BEFORE
   `services.AddFoundationEvents()` and the consumer cluster pulls in
   `IDomainEventPublisher` at registration time). Per DI conventions,
   foundation-events MUST be registered first. Document this ordering
   in the docs walkthrough; if a circular dependency emerges, halt +
   file `cob-question-*`.

8. **A consumer cluster (sibling to `blocks-financial-periods`) ships
   an independent local `IDomainEventPublisher` between PR 1 + PR 6
   ratification.** Include it in PR 6's sweep. If multiple clusters
   land independently, batch them in PR 6 OR carve a follow-on sweep
   PR if PR 6's scope grows beyond ~200 lines of diff.

---

## PASS gate (explicit criteria)

The hand-off is PASS when **all** of the following are true:

1. **PRs 1–4 + PR 6 merged with CI green.** PR 5 may be deferred to a
   follow-on workstream titled "`foundation-events-loro-bridge`"; that
   deferral is explicitly approved.

2. **~50–60 tests passing.** Approximate distribution:
   - PR 1: ~13–15 (envelope construction, equality, JSON, ReplicaId)
   - PR 2: ~15–17 (SqliteDomainEventStore — idempotency, cursor pagination, tenant isolation)
   - PR 3: ~12 (DefaultDomainEventPublisher, InProcessEventDispatcher)
   - PR 4: ~17 (SqliteEventReader cursor model, EventDispatcherHost)
   - PR 5 (if shipped): ~8
   - PR 6: ~5 (integration tests)

3. **`domain_events` table is created via migration** in any host
   composing `AddFoundationEvents()` + invoking
   `ApplyFoundationEventsMigrationsAsync()`. Verified by an integration
   test in PR 6.

4. **The DI-swap sweep (PR 6) removes the local
   `IDomainEventPublisher` from `blocks-financial-periods`** and
   routes through the canonical via DI. Verified by
   `grep -rn "interface IDomainEventPublisher" packages/blocks-*/`
   returning zero hits post-sweep.

5. **`apps/docs/foundation/events/overview.md` walkthrough authored**
   with the producer + consumer examples shown.

6. **Per-handler cursor model end-to-end demonstrable** in integration
   tests — handler registered via `IEventReader`; event published via
   `IDomainEventPublisher`; handler invoked via cursor drain in
   `EventDispatcherHost`. Failure path: handler throws; cursor stays;
   next drain re-invokes.

7. **Idempotency demonstrably works:** publish same envelope twice
   (same idempotency key); only one row in `domain_events`; handler
   invoked once (not twice).

8. **No regressions in `blocks-financial-periods`** post-sweep. All
   pre-sweep tests pass; the cluster builds clean.

9. **License posture table** included verbatim in each PR's
   description. All MIT; no restrictive borrows.

10. **The ledger entry for foundation-events** is updated in
    `icm/_state/active-workstreams.md` from `ready-to-build` to
    `built` after PR 6 merges. (Per the
    `never-edit-active-workstreams.md` convention, this update goes
    via the workstream source file first, then the render-ledger.py
    pipeline.)

---

## Post-build follow-on workstreams (forward-looking)

After PASS, the following follow-ons are queued (each becomes a
separate `cob-idle-*` / hand-off cycle):

1. **`foundation-events-loro-bridge`** — PR 5 split out, when Loro
   integration patterns are ratified.

2. **`foundation-events-bridge-relay`** — Bridge runtime (per
   ADR 0031 + ADR 0088 §4 Hosted tier) consumes Anchor events; see
   `cross-cluster-event-bus-design.md` §10 Q3.

3. **`foundation-events-replay-cli`** — operator tooling to reset a
   handler's cursor + replay from beginning (for derived-state schema
   migrations); see `cross-cluster-event-bus-design.md` §7 "Event
   replay".

4. **`foundation-events-retry-admin-ui`** — admin surface in Bridge
   showing `event_handler_failures.next_retry_at IS NULL` (retry
   exhausted) rows for operator review; see
   `cross-cluster-event-bus-design.md` §6 "Handler failure".

5. **Per-cluster Stage 06 sweeps** — as each cluster ships its first
   cross-cluster event emission PR, it widens its local
   `IDomainEventPublisher` (if any) to the canonical, OR adopts the
   canonical directly. The trigger ruling §"Sibling-cluster routing"
   names the expected clusters:
   - `blocks-financial-tax` (dev's lane)
   - `blocks-financial-ar` (dev's natural next)
   - `blocks-financial-ap`
   - `blocks-people-foundation`
   - `blocks-work-orders`

   Each cluster's hand-off will explicitly reference this substrate
   and document its event types in
   `cross-cluster-event-bus-design.md` §3 catalog.

6. **`foundation-events-payload-size-analyzer`** — build-time analyzer
   enforcing `cross-cluster-event-bus-design.md` §10 Q4 (16 KB payload
   cap; oversized payloads go via `StorageRef`); see CRDT-friendly-
   schema-conventions.md §9.

---

## Discipline summary — how to read this hand-off

When implementing each PR:

1. **Start by re-reading**:
   - This hand-off's PR-specific section (PR N — Scope).
   - `cross-cluster-event-bus-design.md` §1 + §4 + §5 (the binding
     spec).
   - `crdt-friendly-schema-conventions.md` §1 + §4 + §6 (the
     append-only + ULID discipline).
   - The trigger ruling
     `xo-ruling-2026-05-16T21-12Z-cob-event-publisher-home.md` (the
     canonical envelope shape).

2. **Follow the pre-build checklist** before any code commits. The
   `ReplicaId` and `kernel-event-bus` resolution paths are NOT
   optional — they prevent the most likely halt conditions.

3. **Apply CRDT-friendly conventions** verbatim:
   - ULID `EventId` (26 chars, Crockford base-32).
   - `domain_events` is append-only — no UPDATE, no DELETE.
   - `event_handler_cursors` IS mutable; that's the per-replica
     per-handler advance.
   - Idempotency-key UNIQUE constraint prevents duplicate appends
     across retries.

4. **License posture table** in every PR description. All MIT.

5. **Halt rather than guess.** The halt conditions enumerate the
   ambiguities likely to arise; each routes to a `cob-question-*`
   beacon to XO. Sprint momentum is real; substrate correctness is
   more real.

---

## Sibling hand-off cross-references

This substrate is consumed (transitively) by:

- `blocks-financial-ledger-chart-and-journal-stage06-handoff.md` — its
  PR 4 emits `Financial.JournalEntryPosted` via the canonical
  substrate (after `AddFoundationEvents()` is registered at host
  startup).
- `blocks-financial-periods-stage06-handoff.md` — its PR 2 + PR 3 emit
  `Financial.PeriodSoftClosed`, `Financial.PeriodOpened`, etc. After
  this hand-off's PR 6 sweep, these emissions route through the
  canonical.
- `blocks-financial-ar-stage06-handoff.md` (forthcoming) — emits AR
  events via the canonical.
- `blocks-financial-ap-stage06-handoff.md` (forthcoming) — emits AP
  events via the canonical.
- `blocks-financial-tax-stage06-handoff.md` (forthcoming) — emits tax-
  mapping audit events.
- `blocks-people-foundation-stage06-handoff.md` (forthcoming) — emits
  People.* events.
- `blocks-work-orders-stage06-handoff.md` (forthcoming) — emits
  Work.* events.

Each of those hand-offs will reference back to this one for the
envelope shape and idempotency discipline.

---

**End of hand-off. Begin with PR 1 after completing the pre-build
checklist.**
