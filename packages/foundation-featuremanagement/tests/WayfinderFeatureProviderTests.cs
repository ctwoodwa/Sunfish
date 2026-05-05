using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Wayfinder;
using Xunit;

namespace Sunfish.Foundation.FeatureManagement.Tests;

public class WayfinderFeatureProviderTests
{
    private static readonly TenantId Tenant = new("tenant-1");
    private static readonly FeatureKey Key = FeatureKey.Of("sunfish.blocks.leases.renewals.autoReminders");
    private static readonly string CompositeKey = $"tenant:features.{Key.Value}";

    [Fact]
    public async Task TryGetAsync_ReturnsValue_WhenStandingOrderCoversFeaturePath()
    {
        var projector = new StubAtlasProjector(Tenant, CompositeKey, JsonValue.Create(true));
        var provider = new WayfinderFeatureProvider(projector);

        var result = await provider.TryGetAsync(Key, CtxFor(Tenant));

        Assert.NotNull(result);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public async Task TryGetAsync_ReturnsNull_WhenPathNotInAtlasView()
    {
        var projector = new StubAtlasProjector(Tenant, "tenant:features.other.key", JsonValue.Create(true));
        var provider = new WayfinderFeatureProvider(projector);

        var result = await provider.TryGetAsync(Key, CtxFor(Tenant));

        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetAsync_ReturnsNull_WhenTenantIdIsNull()
    {
        var projector = new StubAtlasProjector(Tenant, CompositeKey, JsonValue.Create(true));
        var provider = new WayfinderFeatureProvider(projector);

        var result = await provider.TryGetAsync(Key, new FeatureEvaluationContext());

        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetAsync_ReturnsNull_WhenCurrentValueIsNullForRescindedToggle()
    {
        var projector = new StubAtlasProjector(Tenant, CompositeKey, currentValue: null);
        var provider = new WayfinderFeatureProvider(projector);

        var result = await provider.TryGetAsync(Key, CtxFor(Tenant));

        Assert.Null(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TryGetAsync_BooleanRoundTripIsCorrect(bool value)
    {
        var projector = new StubAtlasProjector(Tenant, CompositeKey, JsonValue.Create(value));
        var provider = new WayfinderFeatureProvider(projector);

        var result = await provider.TryGetAsync(Key, CtxFor(Tenant));

        Assert.NotNull(result);
        Assert.Equal(value, result.AsBoolean());
    }

    [Fact]
    public async Task Evaluator_FallsThroughToCatalogDefault_WhenWayfinderReturnsNull()
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

    [Fact]
    public void AddSunfishFeatureManagementWithWayfinder_RegistersWayfinderProviderAsActive()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAtlasProjector>(
            new StubAtlasProjector(Tenant, CompositeKey, currentValue: null));
        services.AddSunfishFeatureManagementWithWayfinder();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<WayfinderFeatureProvider>(provider.GetRequiredService<IFeatureProvider>());
    }

    [Fact]
    public void AddSunfishFeatureManagementWithWayfinder_ThrowsAtResolution_WhenAtlasProjectorMissing()
    {
        var services = new ServiceCollection();
        services.AddSunfishFeatureManagementWithWayfinder();
        // IAtlasProjector intentionally NOT registered.

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IFeatureProvider>());
    }

    private static FeatureEvaluationContext CtxFor(TenantId tenantId) =>
        new() { TenantId = tenantId };

    private sealed class StubAtlasProjector : IAtlasProjector
    {
        private readonly TenantId _tenantId;
        private readonly string _compositeKey;
        private readonly JsonNode? _currentValue;

        public StubAtlasProjector(TenantId tenantId, string compositeKey, JsonNode? currentValue)
        {
            _tenantId = tenantId;
            _compositeKey = compositeKey;
            _currentValue = currentValue;
        }

        public ValueTask<AtlasView> ProjectAsync(
            TenantId tenantId,
            StandingOrderScope? scopeFilter,
            CancellationToken ct)
        {
            if (tenantId != _tenantId)
            {
                return ValueTask.FromResult(
                    new AtlasView(tenantId, DateTimeOffset.UtcNow, new Dictionary<string, AtlasSettingSnapshot>()));
            }

            // Strip the "<scope>:" prefix from compositeKey to recover the raw path
            // for AtlasSettingSnapshot.Path (per AtlasView.SettingsByPath docstring).
            var rawPath = _compositeKey.Contains(':') ? _compositeKey.Split(':', 2)[1] : _compositeKey;
            var schema = new AtlasSchemaDescriptor(
                JsonSchema: JsonNode.Parse("{\"type\":\"boolean\"}")!,
                DisplayName: "Auto-renewal reminders",
                DescriptionMarkdown: "Test feature toggle.",
                Kind: AtlasSettingKind.Boolean);
            var snapshot = new AtlasSettingSnapshot(
                Path: rawPath,
                CurrentValue: _currentValue,
                LastIssuedBy: new StandingOrderId(Guid.NewGuid()),
                LastIssuedAt: DateTimeOffset.UtcNow,
                Schema: schema);
            var dict = new Dictionary<string, AtlasSettingSnapshot> { [_compositeKey] = snapshot };
            return ValueTask.FromResult(new AtlasView(tenantId, DateTimeOffset.UtcNow, dict));
        }

        public IAsyncEnumerable<AtlasSearchHit> SearchAsync(
            TenantId tenantId, string query, int limit, CancellationToken ct)
            => AsyncEnumerable.Empty<AtlasSearchHit>();
    }
}
