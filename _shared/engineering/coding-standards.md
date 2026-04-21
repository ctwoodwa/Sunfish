# Coding Standards

**Status:** Accepted
**Last reviewed:** 2026-04-19
**Governs:** Every `.cs` file in the repo.
**Companion docs:** [package-conventions.md](package-conventions.md), [testing-strategy.md](testing-strategy.md), [naming.md](../product/naming.md).
**Agent relevance:** Loaded by agents writing or editing `.cs` files. High-frequency for any C# work.

Sunfish is pre-1.0 C# on .NET 10. These are the patterns already established in recent packages (`foundation-catalog`, `foundation-multitenancy`, `foundation-featuremanagement`, `foundation-localfirst`, `foundation-integrations`). When in doubt, copy the shape of a type from one of those packages — this file codifies why those shapes were chosen.

## Non-negotiables from `Directory.Build.props`

The repo enforces these via MSBuild:

| Setting | Effect |
|---|---|
| `Nullable=enable` | Nullable reference types are on everywhere. |
| `ImplicitUsings=enable` | Common `System.*` usings are implicit. |
| `LangVersion=latest` | Newest C# features are available. |
| `GenerateDocumentationFile=true` | XML docs file is emitted; missing docs on public members raise CS1591. |
| `TreatWarningsAsErrors=true` | Warnings fail the build — including CS1591 (missing XML doc) and CS8600/8602/etc. (nullability). |

Consequence: **every public type and public member needs at least a one-line `/// <summary>`**, and **every nullability warning is a build error**. Fix at the source, don't suppress.

## File layout

### File-scoped namespaces

```csharp
namespace Sunfish.Foundation.Catalog.Bundles;

public sealed record BundleStatus { … }
```

Never braced namespaces in new code. The `;` form reduces indentation and matches the rest of the repo.

### One public type per file (usually)

Default to one public type per file. Small related types (an enum and its associated record, an interface and its single default implementation) can share a file when colocation makes the reader's job easier — `ITenantScoped.cs` groups `ITenantScoped`, `IMustHaveTenant`, and `IMayHaveTenant` because they're a trio.

### Using-directive order

Inside a namespace / above file-scoped namespace: `System.*` first, then `Microsoft.*`, then third-party, then `Sunfish.*`, each group alphabetical. Implicit usings handle the common `System.*` cases so individual files usually don't need them. When the compiler complains about an ambiguous reference, disambiguate with a using, not a fully-qualified type name.

## Nullable reference types

Fully adopted. Rules:

- **Public API parameters** are non-nullable unless explicitly optional (`string? displayName`).
- **Return types** are nullable when `null` is a legitimate outcome (`bool TryGet(… out TValue? value)`). Otherwise they're non-nullable.
- **Guard clauses** at the top of public methods use `ArgumentNullException.ThrowIfNull(arg)` for reference-type parameters:
  ```csharp
  public void Register(ExtensionFieldSpec spec)
  {
      ArgumentNullException.ThrowIfNull(spec);
      …
  }
  ```
- **`[NotNullWhen(true)]`** on `out` parameters in `Try*` methods:
  ```csharp
  public bool TryGet(string key, [NotNullWhen(true)] out ProviderDescriptor? descriptor)
  ```
- **Null-forgiving operator (`!`)** only when the type system genuinely can't prove non-null and you've verified the invariant. Explain with a comment.

## Records and classes

### Prefer records for DTOs, manifests, value objects

```csharp
public sealed record TenantMetadata
{
    public required TenantId Id { get; init; }
    public required string Name { get; init; }
    public TenantStatus Status { get; init; } = TenantStatus.Active;
    public string? DisplayName { get; init; }
}
```

- `sealed record` by default.
- **Init-only properties with `required`** for mandatory fields instead of positional records — plays better with `System.Text.Json` deserialization and gives readable construction syntax.
- Positional records are fine for tiny value types with 2–3 fields (`public readonly record struct TenantId(string Value)`).

### Classes when behavior lives with state

In-memory stores, evaluators, and registry implementations are classes, not records. `sealed` by default — inheritance is opt-in via an ADR or very specific justification.

```csharp
public sealed class InMemoryProviderRegistry : IProviderRegistry
{
    private readonly ConcurrentDictionary<string, ProviderDescriptor> _byKey = new(StringComparer.Ordinal);
    …
}
```

### Structs

Use a `readonly record struct` for identity value types (`TenantId`, `ExtensionFieldKey`, `FeatureKey`, `PeerId`). Never mutable structs.

## Interfaces

- **`I`-prefix** always.
- **One concern per interface.** Four-in-one interfaces like the legacy `Foundation.Authorization.ITenantContext` (tenant + user + roles + permissions) are a known anti-pattern; split.
- **Default-interface methods** are fine for derived properties (`bool IsResolved => Tenant is not null;`) but remember callers must hold the interface type to call them. Test projects using concrete types need to cast.
- **`/// <inheritdoc />`** on interface implementations that don't need to restate the contract.

## Enums

- PascalCase values. One concept per enum.
- `[JsonConverter(typeof(JsonStringEnumConverter))]` on every enum that crosses a JSON boundary (bundle manifests, feature values, health status):
  ```csharp
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public enum BundleCategory
  {
      Operations = 0,
      Diligence = 1,
      Finance = 2,
      Platform = 3,
  }
  ```
- Explicit integer values on new enums so reordering doesn't silently change serialization.
- `Unknown = 0` or a similar sentinel when zero-default is a meaningful state (`ProviderHealthStatus.Unknown`).

## Async

- **`ValueTask<T>`** for methods that may complete synchronously in common cases (catalog lookups, in-memory queue peeks, conflict-resolver that returns precomputed bytes).
- **`Task<T>`** when I/O is always involved (HTTP, real database round-trips).
- **`CancellationToken cancellationToken = default`** on every public async method.
- **`.ConfigureAwait(false)`** on library code:
  ```csharp
  var value = await _provider.TryGetAsync(key, ctx, cancellationToken).ConfigureAwait(false);
  ```
- **Completed-value wrappers:** `ValueTask.FromResult(x)`, `ValueTask.CompletedTask`. No `Task.Run` to wrap synchronous work.

## Collections

### Public API types

- **`IReadOnlyList<T>`** for ordered read-only sequences.
- **`IReadOnlyDictionary<TKey, TValue>`** for read-only maps.
- Avoid `IEnumerable<T>` in public API unless the caller genuinely shouldn't know the shape.
- Never expose `List<T>` or `Dictionary<K, V>` directly in a public surface — the caller can mutate.

### Internal implementations

- `ConcurrentDictionary<TKey, TValue>` for thread-safe in-memory registries.
- `List<T>` + a `lock` for ordered collections when order matters (registration-order enumeration).
- `StringComparer.Ordinal` for any dictionary keyed by identifiers (feature keys, provider keys, tenant ids). Case-insensitive keys only when the domain genuinely is case-insensitive.

## Dependency injection

- **Interface registration.** Register the interface; the concrete class is an implementation detail.
  ```csharp
  services.AddSingleton<IBundleCatalog, BundleCatalog>();
  ```
- **Shared singleton pattern** when one concrete type implements multiple interfaces:
  ```csharp
  services.AddSingleton<InMemoryTenantCatalog>();
  services.AddSingleton<ITenantCatalog>(sp => sp.GetRequiredService<InMemoryTenantCatalog>());
  services.AddSingleton<ITenantResolver>(sp => sp.GetRequiredService<InMemoryTenantCatalog>());
  ```
- **Sugar extension methods** named `AddSunfish<Concept>`:
  ```csharp
  public static IServiceCollection AddSunfishFeatureManagement(this IServiceCollection services)
  ```
  One per package. Opinionated defaults that consumers replace piecewise.

## Exceptions

- **`ArgumentNullException`** for null reference args (via `ArgumentNullException.ThrowIfNull`).
- **`ArgumentException`** for invalid values (empty strings, bad enum, out-of-range).
- **`InvalidOperationException`** for bad state or unregistered catalog keys.
- **`FileNotFoundException`** for missing embedded resources.
- **Never** swallow an exception silently. Log or rethrow.
- Exception messages cite the offending identifier: `$"Feature '{key}' is not registered in the catalog."`

## JSON

- `System.Text.Json` is the default. No Newtonsoft.
- Shared options expose via `internal static JsonSerializerOptions SerializerOptions { get; }` when a package does JSON in multiple places.
- `PropertyNameCaseInsensitive = true` for manifest loaders — tolerant to authoring.
- `ReadCommentHandling = JsonCommentHandling.Skip` and `AllowTrailingCommas = true` on human-edited JSON (bundle manifests, template fixtures).
- Enum serialization via `JsonStringEnumConverter` (see Enums above).

## String comparisons

Default to **ordinal** for identifiers and protocol strings:

```csharp
new ConcurrentDictionary<string, TenantMetadata>(StringComparer.Ordinal);
string.Equals(handler.ProviderKey, envelope.ProviderKey, StringComparison.Ordinal);
```

Case-insensitive only when the domain explicitly is (user email, sometimes). Never use culture-sensitive comparisons in identifiers.

## Resource disposal

- **`using var`** for disposables whose scope is the enclosing method.
  ```csharp
  using var doc = JsonDocument.Parse(payloadJson);
  return schema.Evaluate(doc.RootElement).IsValid;
  ```
- **`await using`** for async disposables.
- **`IDisposable` / `IAsyncDisposable` implementation** uses the standard pattern; sealed types implement the simpler form without the virtual `Dispose(bool disposing)` ceremony.

## XML documentation

Every public type and member carries at least a one-line `/// <summary>`. Minimum shape:

```csharp
/// <summary>Registers a feature. Duplicate keys throw.</summary>
public void Register(FeatureSpec spec);
```

### Rules

- **One sentence, ends with a period.**
- **Describes the contract, not the implementation.**
- **Parameters** get `/// <param name="arg">description.</param>` when their purpose isn't obvious from the name.
- **Return values** get `/// <returns>…</returns>` when there's a meaningful contract beyond the type.
- **`/// <inheritdoc />`** for interface implementations that don't need to restate the contract.
- **Record constructor params** use the positional-record comment style or init-property `<summary>` on each property, whichever reads better.

Do not write long docstrings on trivial members. Do not write a docstring that just restates the member name ("gets the name", for a property called `Name`).

## Common patterns to emulate

### Registry with registration-order enumeration

Shared pattern across `BundleCatalog`, `ExtensionFieldCatalog`, `InMemoryProviderRegistry`, `InMemoryTenantCatalog`, `InMemoryFeatureCatalog`:

```csharp
public sealed class InMemoryThingCatalog : IThingCatalog
{
    private readonly ConcurrentDictionary<TKey, TThing> _byKey = new(StringComparer.Ordinal);
    private readonly List<TKey> _registrationOrder = new();
    private readonly object _orderLock = new();

    public void Register(TThing thing)
    {
        if (!_byKey.TryAdd(thing.Key, thing))
            throw new InvalidOperationException($"… '{thing.Key}' is already registered.");
        lock (_orderLock) _registrationOrder.Add(thing.Key);
    }

    public IReadOnlyList<TThing> GetAll()
    {
        lock (_orderLock) return _registrationOrder.Select(k => _byKey[k]).ToArray();
    }
}
```

### `Try*` methods with `NotNullWhen`

```csharp
public bool TryGet(string key, [NotNullWhen(true)] out TThing? thing)
    => _byKey.TryGetValue(key, out thing);
```

### DI sugar extension

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSunfishThingCatalog(this IServiceCollection services)
    {
        services.AddSingleton<IThingCatalog, InMemoryThingCatalog>();
        return services;
    }
}
```

## Anti-patterns

- **Swallowed exceptions.** `catch { }` is always wrong in library code.
- **`Task.Run` wrapping synchronous work.** Just be synchronous.
- **`async void`** outside of event handlers.
- **Mutating structs.** `readonly struct` or `readonly record struct` always.
- **Public fields.** Properties, with get/init/set as appropriate.
- **Culture-sensitive string comparisons** in identifiers or protocol strings.
- **`#pragma warning disable`** for CS1591 or nullability. Fix at the source. *(Narrow exception: `Microsoft.NET.Sdk.Razor` projects may set `<NoWarn>CS1591</NoWarn>` at the project level because Razor-generated types cannot carry XML docs — see [package-conventions.md §Razor packages](package-conventions.md#razor-packages). New `.razor` component files still get XML docs where practical. No equivalent exception exists for nullability.)*
- **`dynamic`.** Not used anywhere in the repo; don't introduce it.
- **Reflection** for basic patterns. Enumerate via a catalog or a source-generated helper instead.

## Pitfalls from shipped work

- **Default interface methods aren't reachable through a concrete-type reference.** If you define `bool IsResolved => Tenant is not null;` on `ITenantContext`, a test that declares `var ctx = new TestContext();` cannot call `ctx.IsResolved`. Declare the variable as the interface (`ITenantContext ctx = new TestContext();`) or call through a cast.
- **xUnit `Assert.Equal(1, collection.Count)` triggers xUnit2013.** Use `Assert.Single(collection)` instead.
- **`JsonSchema.Net` global schema registry** rejects re-registering the same `$id`. Strip `$id` before `JsonSchema.FromText` in tests (or use distinct `$id`s per test).

## Cross-references

- [package-conventions.md](package-conventions.md) — csproj patterns that make `TreatWarningsAsErrors` + `GenerateDocumentationFile` enforceable.
- [testing-strategy.md](testing-strategy.md) — test-file style and assertion patterns.
- [naming.md](../product/naming.md) — identifier conventions (types, keys, JSON property names).
- `packages/foundation-multitenancy/` — a clean recent example in the shipped style.
