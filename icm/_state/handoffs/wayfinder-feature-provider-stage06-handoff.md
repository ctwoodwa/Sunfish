---
workstream: W#43
title: WayfinderFeatureProvider — ADR 0009 5th-concept Wayfinder Consumer
pipeline-variant: sunfish-api-change
stage: 06_build
authored: 2026-05-05
author: XO
adr: docs/adrs/0009-foundation-featuremanagement.md (Amendment A1)
status: ready-to-build
estimated-effort: ~3-5h sunfish-PM
prs: 1 (additive; no behavior change to existing path; + ledger flip commit)
---

# W#43 Stage 06 — WayfinderFeatureProvider

## Purpose

ADR 0009 Amendment A1 (merged PR #486, 2026-05-02) specifies the **fifth concept**
in `Sunfish.Foundation.FeatureManagement`: operator-issued feature toggles backed
by the Wayfinder substrate. This hand-off covers the implementation — one new file,
two new DI extension overloads, a csproj reference addition, and 8 tests.

**Prerequisites confirmed on `origin/main`:**
- `foundation-wayfinder` package: **present** (`git ls-tree origin/main packages/foundation-wayfinder/` ✓)
- `IAtlasProjector` + `AtlasView` + `AtlasSettingSnapshot` + `StandingOrderScope.Tenant`: **verified present**
- `IFeatureProvider`, `FeatureKey`, `FeatureValue`, `FeatureEvaluationContext`, `ServiceCollectionExtensions`: **verified present**

---

## §A0 cited-symbol audit

| Symbol / Path | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.FeatureManagement.IFeatureProvider` | Existing | yes — `packages/foundation-featuremanagement/IFeatureProvider.cs` |
| `Sunfish.Foundation.FeatureManagement.FeatureKey` (record struct, `.Value` property) | Existing | yes — `packages/foundation-featuremanagement/FeatureKey.cs` |
| `Sunfish.Foundation.FeatureManagement.FeatureValue` (`required string Raw { get; init; }`) | Existing | yes — `packages/foundation-featuremanagement/FeatureValue.cs` |
| `Sunfish.Foundation.FeatureManagement.FeatureEvaluationContext.TenantId` (`TenantId?`) | Existing | yes — `packages/foundation-featuremanagement/FeatureEvaluationContext.cs` |
| `Sunfish.Foundation.FeatureManagement.ServiceCollectionExtensions.AddSunfishFeatureManagement()` | Existing | yes — `packages/foundation-featuremanagement/ServiceCollectionExtensions.cs` |
| `Sunfish.Foundation.Assets.Common.TenantId` | Existing | yes — `packages/foundation/` (transitive via foundation-featuremanagement) |
| `Sunfish.Foundation.Wayfinder.IAtlasProjector` | Existing | yes — `packages/foundation-wayfinder/IAtlasProjector.cs` |
| `Sunfish.Foundation.Wayfinder.IAtlasProjector.ProjectAsync(TenantId, StandingOrderScope?, CancellationToken)` | Existing | yes — signature verified: param names `tenantId`, `scopeFilter`, `ct` |
| `Sunfish.Foundation.Wayfinder.AtlasView.SettingsByPath` (`IReadOnlyDictionary<string, AtlasSettingSnapshot>`) | Existing | yes — composite key format `"<scope-lower>:<path>"` |
| `Sunfish.Foundation.Wayfinder.AtlasSettingSnapshot.CurrentValue` (`JsonNode?`) | Existing | yes — `packages/foundation-wayfinder/AtlasSettingSnapshot.cs` |
| `Sunfish.Foundation.Wayfinder.StandingOrderScope.Tenant` | Existing | yes — `packages/foundation-wayfinder/StandingOrderScope.cs` |
| `Sunfish.Foundation.FeatureManagement.WayfinderFeatureProvider` | **Introduced** | n/a |
| `ServiceCollectionExtensions.AddSunfishFeatureManagementWithWayfinder()` | **Introduced** | n/a |
| `ServiceCollectionExtensions.AddWayfinderFeatureProvider()` | **Introduced** | n/a |

**Critical composite-key correction (§A0.3 structural finding):**
The ADR 0009-A1 code sample (§A1.4) contains a bug:
```csharp
// ADR text — WRONG:
var path = $"features.{key.Value}";
atlasView.SettingsByPath.TryGetValue(path, ...)
```
`AtlasView.SettingsByPath` uses **composite keys** `"<scope-lower>:<path>"` —
verified in `DefaultAtlasProjector.cs`:
```csharp
var compositeKey = $"{scope.ToString().ToLowerInvariant()}:{path}";
```
The correct lookup key for `StandingOrderScope.Tenant` + path `features.{key}` is
`"tenant:features.{key.Value}"`. The implementation below uses the correct form.
This is a mechanical correction to the ADR spec — no design change.

---

## Phase 1 — Implementation (1 PR; ~3-5h)

### 1.1 New file: `packages/foundation-featuremanagement/WayfinderFeatureProvider.cs`

```csharp
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Wayfinder;

namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// <see cref="IFeatureProvider"/> backed by the Wayfinder/Atlas projection.
/// Resolves operator-issued feature toggles from the per-tenant Standing Order log
/// via <see cref="IAtlasProjector"/>. Returns <c>null</c> for any feature key that
/// has no Standing Order at the canonical path <c>features.{key}</c>.
/// Per ADR 0009 §A1.4.
/// </summary>
public sealed class WayfinderFeatureProvider : IFeatureProvider
{
    private readonly IAtlasProjector _projector;

    /// <param name="projector">Atlas projector; registered via <c>AddSunfishWayfinder()</c>.</param>
    public WayfinderFeatureProvider(IAtlasProjector projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        _projector = projector;
    }

    /// <inheritdoc />
    public async ValueTask<FeatureValue?> TryGetAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.TenantId is not { } tenantId)
        {
            // No tenant context — operator toggles are per-tenant; pass through.
            return null;
        }

        // AtlasView.SettingsByPath uses composite keys "<scope-lower>:<path>".
        // Operator feature toggles live under StandingOrderScope.Tenant
        // at path "features.{key}", so the composite key is "tenant:features.{key}".
        var compositeKey = $"tenant:features.{key.Value}";

        var atlasView = await _projector.ProjectAsync(
            tenantId,
            scopeFilter: StandingOrderScope.Tenant,
            cancellationToken).ConfigureAwait(false);

        if (!atlasView.SettingsByPath.TryGetValue(compositeKey, out var snapshot))
        {
            return null;
        }

        if (snapshot.CurrentValue is null)
        {
            // Path exists in the log but was rescinded (null NewValue triple).
            return null;
        }

        // JsonNode.ToString() on boolean nodes produces lowercase "true"/"false",
        // which FeatureValue.AsBoolean() consumes via bool.Parse — round-trip correct.
        var raw = snapshot.CurrentValue.ToString();
        return new FeatureValue { Raw = raw };
    }
}
```

### 1.2 Update `packages/foundation-featuremanagement/ServiceCollectionExtensions.cs`

Add two overloads after the existing `AddSunfishFeatureManagement()` method:

```csharp
/// <summary>
/// Registers the full feature-management stack with <see cref="WayfinderFeatureProvider"/>
/// as the active <see cref="IFeatureProvider"/>.
/// Requires <c>AddSunfishWayfinder()</c> on the same <see cref="IServiceCollection"/>.
/// Per ADR 0009 §A1.4.
/// </summary>
public static IServiceCollection AddSunfishFeatureManagementWithWayfinder(
    this IServiceCollection services)
{
    services.AddSunfishFeatureManagement();
    // Re-register IFeatureProvider with WayfinderFeatureProvider.
    // AddSingleton uses last-wins semantics for a single instance registration
    // of the same service type, so this replaces InMemoryFeatureProvider.
    services.AddSingleton<IFeatureProvider, WayfinderFeatureProvider>();
    return services;
}

/// <summary>
/// Registers only <see cref="WayfinderFeatureProvider"/> as the
/// <see cref="IFeatureProvider"/>. Use when <see cref="AddSunfishFeatureManagement"/>
/// was already called and only the provider needs to be swapped.
/// Per ADR 0009 §A1.4.
/// </summary>
public static IServiceCollection AddWayfinderFeatureProvider(
    this IServiceCollection services)
{
    services.AddSingleton<IFeatureProvider, WayfinderFeatureProvider>();
    return services;
}
```

**Note on DI last-wins:** `AddSunfishFeatureManagementWithWayfinder()` calls
`AddSunfishFeatureManagement()` (which registers `InMemoryFeatureProvider`) then
immediately re-registers `IFeatureProvider → WayfinderFeatureProvider`. Microsoft DI
resolves the last `AddSingleton` for the same service type when asked for a single
instance — `WayfinderFeatureProvider` is active. This mirrors the override pattern
used throughout Sunfish.

### 1.3 Update `packages/foundation-featuremanagement/Sunfish.Foundation.FeatureManagement.csproj`

Add to the `<ItemGroup>` with `foundation` reference:

```xml
<ProjectReference Include="..\foundation-wayfinder\Sunfish.Foundation.Wayfinder.csproj" />
```

**Packaging note:** Adding this unconditionally means every consumer of
`foundation-featuremanagement` takes the `foundation-wayfinder` transitive dep. For the
mono-repo build phase this is correct. A future NuGet-split (optional-dep packaging) can
introduce a companion `Sunfish.Foundation.FeatureManagement.Wayfinder` package when
Sunfish publishes to nuget.org — that concern is deferred per the pre-release policy.

### 1.4 Update `packages/foundation-featuremanagement/tests/tests.csproj`

Add `foundation-wayfinder` reference so tests can construct stub `IAtlasProjector`:

```xml
<ProjectReference Include="..\..\foundation-wayfinder\Sunfish.Foundation.Wayfinder.csproj" />
```

### 1.5 New test file: `packages/foundation-featuremanagement/tests/WayfinderFeatureProviderTests.cs`

8 tests per ADR 0009 §A1.7. Use a hand-rolled stub (no NSubstitute — consistent with
existing test style in `DefaultFeatureEvaluatorTests.cs`).

**Stub + test outline:**

```csharp
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Wayfinder;

namespace Sunfish.Foundation.FeatureManagement.Tests;

public class WayfinderFeatureProviderTests
{
    private static readonly TenantId Tenant = new("tenant-1");
    private static readonly FeatureKey Key = FeatureKey.Of("sunfish.blocks.leases.renewals.autoReminders");
    private static readonly string CompositeKey = $"tenant:features.{Key.Value}";

    // Test 1: returns FeatureValue when Standing Order covers "tenant:features.{key}" for the tenant
    [Fact]
    public async Task TryGetAsync_returns_value_when_standing_order_covers_feature_path()
    {
        var projector = new StubAtlasProjector(Tenant, CompositeKey, JsonValue.Create(true));
        var provider = new WayfinderFeatureProvider(projector);

        var result = await provider.TryGetAsync(Key, CtxFor(Tenant));

        Assert.NotNull(result);
        Assert.True(result.AsBoolean());
    }

    // Test 2: returns null when path not in AtlasView
    [Fact]
    public async Task TryGetAsync_returns_null_when_path_not_in_atlas_view()
    {
        var projector = new StubAtlasProjector(Tenant, "tenant:features.other.key", JsonValue.Create(true));
        var provider = new WayfinderFeatureProvider(projector);

        var result = await provider.TryGetAsync(Key, CtxFor(Tenant));

        Assert.Null(result);
    }

    // Test 3: returns null when TenantId is null
    [Fact]
    public async Task TryGetAsync_returns_null_when_tenant_id_is_null()
    {
        var projector = new StubAtlasProjector(Tenant, CompositeKey, JsonValue.Create(true));
        var provider = new WayfinderFeatureProvider(projector);

        var result = await provider.TryGetAsync(Key, new FeatureEvaluationContext());

        Assert.Null(result);
    }

    // Test 4: returns null when CurrentValue is null (rescinded toggle)
    [Fact]
    public async Task TryGetAsync_returns_null_when_current_value_is_null()
    {
        var projector = new StubAtlasProjector(Tenant, CompositeKey, null);
        var provider = new WayfinderFeatureProvider(projector);

        var result = await provider.TryGetAsync(Key, CtxFor(Tenant));

        Assert.Null(result);
    }

    // Test 5: AsBoolean() on returned value is correct for JsonNode boolean serialization
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TryGetAsync_boolean_round_trip_is_correct(bool value)
    {
        var projector = new StubAtlasProjector(Tenant, CompositeKey, JsonValue.Create(value));
        var provider = new WayfinderFeatureProvider(projector);

        var result = await provider.TryGetAsync(Key, CtxFor(Tenant));

        Assert.NotNull(result);
        Assert.Equal(value, result.AsBoolean());
    }

    // Test 6: evaluator chain resolves via Wayfinder then falls through to entitlement when no toggle
    [Fact]
    public async Task Evaluator_falls_through_to_entitlement_when_wayfinder_returns_null()
    {
        var projector = new StubAtlasProjector(Tenant, "tenant:features.unrelated", JsonValue.Create(true));
        var provider = new WayfinderFeatureProvider(projector);
        var catalog = new InMemoryFeatureCatalog();
        catalog.Register(new FeatureSpec
        {
            Key = Key,
            Kind = FeatureValueKind.String,
            DefaultValue = "catalog-default",
        });
        var evaluator = new DefaultFeatureEvaluator(catalog, provider, new NoOpEntitlementResolver());

        var result = await evaluator.EvaluateAsync(Key, CtxFor(Tenant));

        Assert.Equal("catalog-default", result.AsString());
    }

    // Test 7: AddSunfishFeatureManagementWithWayfinder() registers WayfinderFeatureProvider as active IFeatureProvider
    [Fact]
    public void AddSunfishFeatureManagementWithWayfinder_registers_wayfinder_provider_as_active()
    {
        var services = new ServiceCollection();
        // Register a stub IAtlasProjector so the container can resolve WayfinderFeatureProvider.
        services.AddSingleton<IAtlasProjector>(
            new StubAtlasProjector(Tenant, CompositeKey, null));
        services.AddSunfishFeatureManagementWithWayfinder();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<WayfinderFeatureProvider>(provider.GetRequiredService<IFeatureProvider>());
    }

    // Test 8: missing IAtlasProjector registration throws at resolution time with clear message
    [Fact]
    public void AddSunfishFeatureManagementWithWayfinder_throws_at_resolution_when_atlas_projector_missing()
    {
        var services = new ServiceCollection();
        services.AddSunfishFeatureManagementWithWayfinder();
        // IAtlasProjector NOT registered.

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IFeatureProvider>());
    }

    // --- stubs ---

    private static FeatureEvaluationContext CtxFor(TenantId tenantId) =>
        new() { TenantId = tenantId };

    private sealed class StubAtlasProjector(
        TenantId tenantId,
        string compositeKey,
        JsonNode? value)
        : IAtlasProjector
    {
        public ValueTask<AtlasView> ProjectAsync(
            TenantId t,
            StandingOrderScope? scopeFilter,
            CancellationToken ct)
        {
            if (t != tenantId)
            {
                return ValueTask.FromResult(
                    new AtlasView(t, DateTimeOffset.UtcNow,
                        new Dictionary<string, AtlasSettingSnapshot>()));
            }

            var snapshot = new AtlasSettingSnapshot(
                Path: compositeKey.Contains(':') ? compositeKey.Split(':')[1] : compositeKey,
                CurrentValue: value,
                LastIssuedBy: new StandingOrderId(Guid.NewGuid()),
                LastIssuedAt: DateTimeOffset.UtcNow,
                Schema: new AtlasSchemaDescriptor("Test", AtlasSettingKind.Boolean, null));

            var dict = new Dictionary<string, AtlasSettingSnapshot>
            {
                [compositeKey] = snapshot,
            };

            return ValueTask.FromResult(
                new AtlasView(t, DateTimeOffset.UtcNow, dict));
        }

        public IAsyncEnumerable<AtlasSearchHit> SearchAsync(
            TenantId t, string query, int limit, CancellationToken ct) =>
            AsyncEnumerable.Empty<AtlasSearchHit>();
    }
}
```

**Verify `AtlasSchemaDescriptor` constructor shape before writing test:**
Run `git show origin/main:packages/foundation-wayfinder/AtlasSchemaDescriptor.cs` to confirm
the exact constructor signature. The stub above uses `(string DisplayName, AtlasSettingKind Kind, string? Description)` — adjust if actual shape differs.

---

## Phase 2 — Ledger flip (same PR or a trivial follow-up commit)

Update `icm/_state/active-workstreams.md`:
- W#43 row: `design-in-flight` → `built`
- Add note: "WayfinderFeatureProvider shipped; ADR 0009-A1 fully implemented; W#44 now unblocked."
- W#44 row: update note to reflect W#43 is built and W#44 intake can now be filed.

---

## Acceptance criteria

1. `packages/foundation-featuremanagement/WayfinderFeatureProvider.cs` exists, compiles, passes `dotnet build`.
2. `AddSunfishFeatureManagementWithWayfinder()` + `AddWayfinderFeatureProvider()` present in `ServiceCollectionExtensions.cs`.
3. `Sunfish.Foundation.FeatureManagement.csproj` has a `<ProjectReference>` to `foundation-wayfinder`.
4. 8 tests pass (`dotnet test packages/foundation-featuremanagement/tests/`).
5. Pre-merge council dispatched (see halt-condition A below); amendments applied before auto-merge enabled.
6. W#43 ledger row flipped to `built`.

---

## Halt conditions

**A — Pre-merge council (MANDATORY for api-change pipeline):**
Do NOT enable auto-merge until council returns verdict. Dispatch Opus + xhigh council
subagent with these structural-pressure-test points:
- Verify composite key `"tenant:features.{key.Value}"` is correct against the actual
  `DefaultAtlasProjector.cs` key construction (confirmed in this hand-off; council
  re-verify).
- Verify `AddSingleton` last-wins semantics for `IFeatureProvider` — is this documented
  behavior or an implementation detail that could break?
- Verify `StubAtlasProjector` in tests compiles against the actual `AtlasSchemaDescriptor`
  and `StandingOrderId` constructors on `origin/main`.

**B — IAtlasProjector signature drift:**
If `IAtlasProjector.ProjectAsync` signature on `origin/main` differs from what this
hand-off shows (param names `tenantId`, `scopeFilter`, `ct`), fix the call site in
`WayfinderFeatureProvider.cs` before the PR.

**C — AtlasSchemaDescriptor constructor:**
The `StubAtlasProjector` in the tests uses an assumed constructor shape. Run
`git show origin/main:packages/foundation-wayfinder/AtlasSchemaDescriptor.cs` at build
time and adjust the stub if needed.

**D — `AddSingleton` last-wins behavior (test 7):**
Test 7 assumes Microsoft DI resolves the last `AddSingleton` when there are two
registrations of the same service type as a single instance. Verify this by running
the test — if it fails, use `services.Replace(ServiceDescriptor.Singleton<IFeatureProvider, WayfinderFeatureProvider>())` in the extension method instead.

---

## Follow-ups (do not block this PR)

1. **W#44** — `ExtensionFields` feature-evaluation hook. W#43 built is the gate condition.
   XO to file W#44 intake after this PR merges.
2. **Atlas schema registration for feature keys** (ADR 0009 §A1.6 item 3) — a
   `WayfinderFeatureSchemaRegistrar` that reads `IFeatureCatalog` at startup and
   registers one `AtlasSchemaDescriptor` per `FeatureSpec` under `features.{key}`.
   Deferred; file as a follow-on workstream when W#42 Phase 3b ships.
3. **apps/docs cross-link** (ADR 0009 §A1.6 item 2) — add a "Operator-issued toggles
   via Wayfinder" section to `apps/docs/blocks/foundation-featuremanagement.md`.
   Deferred to docs workstream.
