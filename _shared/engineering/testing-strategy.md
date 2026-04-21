# Testing Strategy

**Status:** Accepted
**Last reviewed:** 2026-04-19
**Governs:** Every test project and test file in the repo.
**Companion docs:** [package-conventions.md](package-conventions.md), [coding-standards.md](coding-standards.md), [adapter-parity.md](adapter-parity.md).

Sunfish's test suites are a green-bar: every push to main builds clean and passes tests. The strategy below codifies how we achieve that without over-engineering the test pyramid for a pre-1.0 codebase.

## Test framework stack

| Layer | Stack | Used for |
|---|---|---|
| Unit | xUnit + NSubstitute | Every package's own logic |
| Blazor component | bUnit | Razor components in `ui-adapters-blazor` and `blocks-*` that ship components |
| Integration | Testcontainers.PostgreSql | Anything that round-trips through Postgres or RabbitMQ |
| Performance | NBomber + NBomber.Http | Bridge load/perf (`accelerators/bridge/tests/Sunfish.Bridge.Tests.Performance/`) |
| Coverage | coverlet.collector | All test projects |

Versions are pinned centrally in [`Directory.Packages.props`](../../Directory.Packages.props) — **that file is authoritative**. Never version-specify in a test csproj, and don't duplicate version numbers into this table (they drift).

## What to test at each layer

### Unit tests (most of the pyramid)

Fast, in-memory, no network. One package's own types. Examples from recent work:

- **Record shape + defaults** — `TenantMetadataTests.TenantMetadata_defaults_to_active_status_and_empty_properties`.
- **Registry behavior** — register + retrieve, duplicate rejection, ordering.
- **Algorithm correctness** — `TemplateMergerTests` for RFC 7396 merge-patch semantics.
- **Contract composition** — `DefaultFeatureEvaluatorTests` for provider → entitlement → default chain.
- **JSON round-trip** — `BundleManifestLoaderTests` for manifest parsing.

### Integration tests (focused)

Only when a contract crosses a real boundary:

- **Postgres-backed stores** — `Sunfish.Foundation.Assets.Postgres.Tests` uses Testcontainers.
- **Kernel event bus with real transport** — when testing multi-peer scenarios.
- **Bridge end-to-end flows** — `Sunfish.Bridge.Tests.Integration` spins up the full Aspire host.

Don't add integration tests for logic that a unit test can cover. Integration tests exist to catch wire-format, serialization, and transaction-boundary bugs — not to re-test domain logic.

### Component tests (Blazor)

`bunit` tests in `ui-adapters-blazor/tests` verify component behavior:

- Parameter binding
- Event callbacks fire with expected arguments
- Render output contains expected markup
- Keyboard/mouse interactions update state

Current G37 `SunfishDataGrid` work is the richest example: `ColumnResizingTests`, `ColumnReorderingTests`, `RowDragDropTests`, `FrozenColumnsTests`, `ColumnMenuTests`, `CsvExportTests`, `JsInteropInfrastructureTests`.

### Performance tests

Lives in `accelerators/bridge/tests/Sunfish.Bridge.Tests.Performance/`. NBomber driven; does not run on every build. Triggered by dedicated CI job or manual run during perf-critical change reviews.

### Parity tests (future)

Per [ADR 0014](../../docs/adrs/0014-adapter-parity-policy.md), once `ui-adapters-react` exists, a cross-adapter parity harness verifies the same UI-Core contract produces equivalent observable behavior across adapters. Not yet built.

## Test project layout

Per [package-conventions.md](package-conventions.md):

```
packages/<pkg>/tests/
├── tests.csproj
├── GlobalUsings.cs
├── Fixtures/              ← optional; JSON, binary, etc.
│   └── Templates/
│       └── lease-renewal.schema.json
├── <GroupingFolder>/      ← mirrors library structure
│   └── <SubjectName>Tests.cs
└── …
```

Folder structure inside `tests/` mirrors the library. `Sunfish.Foundation.Catalog.Bundles` code → `tests/Bundles/*Tests.cs`.

## File and test naming

### File names

`<SubjectName>Tests.cs` — one test class per subject type. `InMemoryOfflineStoreTests.cs` tests `InMemoryOfflineStore`. `DefaultFeatureEvaluatorTests.cs` tests `DefaultFeatureEvaluator`.

### Test method names

`snake_case` sentences that describe behavior, not implementation:

- `Register_and_GetAll_preserve_order`
- `TryGet_returns_false_when_absent`
- `Resolve_rejects_overlay_whose_base_ref_does_not_match`
- `Every_shipped_bundle_manifest_validates_against_the_meta_schema`

Reads naturally in test output. Reviewers should be able to read the name and know the failure's meaning without opening the test.

### Theory test naming

`<BehaviorBeingTested>` with parameters varied via `[InlineData]` or `[MemberData]`. The name stays singular and descriptive; data variants are the loop.

## Arrange / Act / Assert

Recent tests use compact AAA without explicit comment headers — whitespace separates phases:

```csharp
[Fact]
public async Task ResolveAsync_matches_by_tenant_id_string()
{
    var catalog = new InMemoryTenantCatalog();
    var acme = Tenant("acme");
    catalog.Register(acme);

    var resolved = await catalog.ResolveAsync("acme");

    Assert.Same(acme, resolved);
}
```

Keep each phase tight. If a test needs more than ~20 lines of arrange, extract a helper or rethink the scope.

## Assertions

Stick to xUnit's own assertions. They interact well with xUnit's analyzers:

- **Equality:** `Assert.Equal(expected, actual)` — and `Assert.Same` for reference equality.
- **Collections:** `Assert.Empty`, `Assert.Single`, `Assert.Contains`, `Assert.DoesNotContain`. Never `Assert.Equal(1, collection.Count)` — xUnit2013 flags it.
- **Booleans:** `Assert.True`, `Assert.False`. Include a message when the failure isn't obvious.
- **Exceptions:** `Assert.Throws<T>`, `Assert.ThrowsAsync<T>`.
- **Null:** `Assert.Null`, `Assert.NotNull`.
- **Type:** `Assert.IsType<T>`, `Assert.IsAssignableFrom<T>`.

Don't introduce FluentAssertions or Shouldly. xUnit's own surface is adequate and keeps diagnostics consistent across packages.

## Mocking

Use NSubstitute. Prefer real in-memory implementations (`InMemoryFeatureProvider`, `InMemoryTenantCatalog`, `NoOpEntitlementResolver`) over mocks whenever one exists — they're less fragile and describe behavior more clearly.

When NSubstitute is the right tool:

```csharp
var entitlements = Substitute.For<IEntitlementResolver>();
entitlements.TryResolveAsync(Arg.Any<FeatureKey>(), Arg.Any<FeatureEvaluationContext>(), default)
    .Returns(ValueTask.FromResult<FeatureValue?>(FeatureValue.Of("enterprise")));
```

## Fixtures

### JSON or binary fixtures

Two patterns, both in use:

**Copy to output directory** (test-only fixtures, simple loading):

```xml
<ItemGroup>
  <Content Include="Fixtures/**/*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

Load at runtime via `File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Templates", "lease-renewal.schema.json"))`.

**Embedded in the library** (resources that ship with the product):

See [package-conventions.md §Embedded resources](package-conventions.md#embedded-resources-manifests-schemas-fixtures). Use `LogicalName` with forward slashes. Test code uses `BundleManifestLoader.LoadEmbeddedText("Bundles/x.bundle.json")` (or the equivalent helper).

### Global usings for tests

`tests/GlobalUsings.cs` carries common usings to reduce per-file noise:

```csharp
global using Xunit;
global using System.Text.Json;
global using System.Text.Json.Nodes;
```

Don't add project-specific usings here — only things used in nearly every test file.

## Test parallelism

xUnit runs test classes in parallel by default, methods within a class serially. That matches Sunfish's patterns: each `*Tests.cs` gets its own instance-per-test. If a test class shares state (a global registry, a Testcontainers instance), disable parallel execution on that class or use a `IClassFixture<T>`.

## Integration tests — Testcontainers

Pattern:

```csharp
public class PostgresIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().Build();

    public Task InitializeAsync() => _db.StartAsync();
    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task …
}
```

Keep Testcontainers tests in a dedicated `.Integration` test project so a unit-test-only CI job skips them. Never put Testcontainers usage in a fast-unit-test project.

### Sharing a container across test classes — `ICollectionFixture<T>`

`IAsyncLifetime` per test class spins up (and tears down) a container per class. That's fine for a handful of classes but gets expensive fast. When several test classes in the same project need the same Postgres / RabbitMQ instance, share it via an `ICollectionFixture<T>`:

```csharp
public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Db { get; } = new PostgreSqlBuilder().Build();
    public Task InitializeAsync() => Db.StartAsync();
    public Task DisposeAsync() => Db.DisposeAsync().AsTask();
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture> { }

[Collection("postgres")]
public class OfflineStoreRoundTripTests
{
    private readonly PostgresFixture _fx;
    public OfflineStoreRoundTripTests(PostgresFixture fx) => _fx = fx;
    …
}
```

Container starts once per collection, not once per class. Tests in the same collection run serially (xUnit's default for collection members) — write tests to tolerate that, or use per-test schemas/transactions for isolation.

Use `IClassFixture<T>` when the fixture is genuinely per-class; use `ICollectionFixture<T>` for cross-class sharing. Don't use `IAsyncLifetime` directly on test classes for containers unless the container is truly single-class-scoped.

## Bridge integration and performance

`accelerators/bridge/tests/` contains three projects:

- `Sunfish.Bridge.Tests.Unit` — Bridge-specific unit tests.
- `Sunfish.Bridge.Tests.Integration` — end-to-end via Aspire testing.
- `Sunfish.Bridge.Tests.Performance` — NBomber scenarios.

Treat Bridge's test projects as the reference for future accelerators.

## Coverage

`coverlet.collector` in every test csproj emits coverage on `dotnet test /p:CollectCoverage=true`. No target threshold enforced yet; coverage reports are informational. **[ci-quality-gates.md §Coverage](ci-quality-gates.md)** is the canonical policy: it describes the Codecov dashboard setup, the 5-percentage-point drop alert, and the per-package target ranges that serve as starting points once packages reach GA.

## Known test-env gotchas

### JsonSchema.Net global registry

`JsonSchema.FromText(jsonText)` registers the schema globally by its `$id`. Two tests parsing different content with the same `$id` throws `JsonSchemaException : Overwriting registered schemas is not permitted.`

**Fix:** strip `$id` before `FromText`, or give each schema a unique `$id`:

```csharp
private static bool Validate(string schemaJson, string payloadJson)
{
    var node = JsonNode.Parse(schemaJson)!.AsObject();
    node.Remove("$id");
    var schema = JsonSchema.FromText(node.ToJsonString());
    using var doc = JsonDocument.Parse(payloadJson);
    return schema.Evaluate(doc.RootElement).IsValid;
}
```

Used in `BundleManifestMetaSchemaTests` and `LeaseRenewalFormTests`.

### `Assert.Equal(1, bag.Count)` triggers xUnit2013

Use `Assert.Single(bag)` instead. The analyzer catches this at compile time and `TreatWarningsAsErrors=true` fails the build.

### Default-interface-method accessibility

```csharp
public interface ITenantContext
{
    TenantMetadata? Tenant { get; }
    bool IsResolved => Tenant is not null;    // DIM
}
```

Calling `IsResolved` requires the caller to hold `ITenantContext`, not a concrete type. In tests:

```csharp
// WRONG — ctx.IsResolved won't compile
var ctx = new FixedContext(tenant);

// Correct — interface variable type
ITenantContext ctx = new FixedContext(tenant);
Assert.True(ctx.IsResolved);
```

### Embedded-resource `LogicalName` forward slashes

See [package-conventions.md §Embedded resources](package-conventions.md#embedded-resources-manifests-schemas-fixtures). Tests that load embedded resources by logical name must match what MSBuild actually emits.

### Razor test projects and `Nullable`

bUnit + Razor test projects sometimes need `<Nullable>enable</Nullable>` explicitly in the csproj even though `Directory.Build.props` sets it; `.razor`-generated code can otherwise carry inconsistent nullability. Not an issue for plain test projects.

## Anti-patterns

- **Testing private methods** via reflection. If it's worth testing, it's a seam that should be public or internal with `InternalsVisibleTo`.
- **Over-mocking.** Mocking a type you could use directly (in-memory implementation) adds brittleness without value.
- **Shared mutable state across tests.** Registry-style tests use fresh instances per test.
- **Time-based sleeps** in tests. Use `async`/`await` correctly or inject a clock.
- **Snapshot testing** for JSON payloads when a structural assertion works. Structural assertions document intent; snapshots document happenstance.
- **Cross-project test references** that skip the boundary. A test project references its own library via `ProjectReference`; integration across packages happens in a dedicated integration project or in Bridge's test suite.

## Checklist — adding tests

1. **Project exists.** Per [package-conventions.md](package-conventions.md), every new code package gets a sibling `tests/` project.
2. **Global usings.** `tests/GlobalUsings.cs` contains at minimum `global using Xunit;`.
3. **File naming.** `<Subject>Tests.cs`, mirror library folder structure.
4. **One subject per class.** If a type needs more than 20 tests, split into logical subclasses (`<Subject>LoadTests`, `<Subject>SaveTests`).
5. **Snake-case method names** describing behavior.
6. **AAA with whitespace separation.** Keep arrange small; prefer helpers over long setup.
7. **xUnit asserts only.** No FluentAssertions.
8. **Real over mocks** when an in-memory impl exists.
9. **Integration tests in `.Integration` project**, not mingled with unit tests.
10. **Run green on main.** Verified via `dotnet test <tests.csproj>` before pushing.

## Cross-references

- [package-conventions.md](package-conventions.md) — csproj templates for test projects.
- [coding-standards.md](coding-standards.md) — C# style that tests follow.
- [adapter-parity.md](adapter-parity.md) — future parity-test harness policy.
- `packages/foundation-multitenancy/tests/` — a clean recent test project (10 tests).
- `packages/foundation-catalog/tests/` — larger (63 tests) showing fixtures, embedded resources, meta-schema validation.
