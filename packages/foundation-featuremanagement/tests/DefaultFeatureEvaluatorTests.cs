using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.FeatureManagement;

namespace Sunfish.Foundation.FeatureManagement.Tests;

public class DefaultFeatureEvaluatorTests
{
    private static readonly FeatureKey SampleKey = FeatureKey.Of("sunfish.blocks.leases.renewals.autoReminders");

    [Fact]
    public async Task Throws_when_feature_is_not_in_catalog()
    {
        var (evaluator, _, _) = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await evaluator.EvaluateAsync(SampleKey, new FeatureEvaluationContext()));
    }

    [Fact]
    public async Task Returns_catalog_default_when_nothing_else_resolves()
    {
        var (evaluator, catalog, _) = Build();
        catalog.Register(new FeatureSpec
        {
            Key = SampleKey,
            Kind = FeatureValueKind.Boolean,
            DefaultValue = "false",
        });

        var result = await evaluator.EvaluateAsync(SampleKey, new FeatureEvaluationContext());

        Assert.False(result.AsBoolean());
    }

    [Fact]
    public async Task Provider_override_wins_over_catalog_default()
    {
        var (evaluator, catalog, provider) = Build();
        catalog.Register(new FeatureSpec
        {
            Key = SampleKey,
            Kind = FeatureValueKind.Boolean,
            DefaultValue = "false",
        });
        provider.SetOverride(SampleKey, FeatureValue.Of(true));

        var result = await evaluator.EvaluateAsync(SampleKey, new FeatureEvaluationContext());

        Assert.True(result.AsBoolean());
    }

    [Fact]
    public async Task Entitlement_resolver_wins_over_catalog_default_but_loses_to_provider()
    {
        var catalog = new InMemoryFeatureCatalog();
        var provider = new InMemoryFeatureProvider();
        var entitlements = new StubEntitlementResolver(FeatureValue.Of("entitled"));
        var evaluator = new DefaultFeatureEvaluator(catalog, provider, entitlements);

        catalog.Register(new FeatureSpec
        {
            Key = SampleKey,
            Kind = FeatureValueKind.String,
            DefaultValue = "default",
        });

        // Entitlement value wins over default
        var entitledResult = await evaluator.EvaluateAsync(SampleKey, new FeatureEvaluationContext());
        Assert.Equal("entitled", entitledResult.AsString());

        // Provider value wins over entitlement
        provider.SetOverride(SampleKey, FeatureValue.Of("override"));
        var overrideResult = await evaluator.EvaluateAsync(SampleKey, new FeatureEvaluationContext());
        Assert.Equal("override", overrideResult.AsString());
    }

    [Fact]
    public async Task Throws_when_no_layer_produces_a_value_and_spec_has_no_default()
    {
        var (evaluator, catalog, _) = Build();
        catalog.Register(new FeatureSpec
        {
            Key = SampleKey,
            Kind = FeatureValueKind.Boolean,
        });

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await evaluator.EvaluateAsync(SampleKey, new FeatureEvaluationContext()));
    }

    [Fact]
    public async Task IsEnabledAsync_converts_boolean_values()
    {
        var (evaluator, catalog, _) = Build();
        catalog.Register(new FeatureSpec
        {
            Key = SampleKey,
            Kind = FeatureValueKind.Boolean,
            DefaultValue = "true",
        });

        Assert.True(await evaluator.IsEnabledAsync(SampleKey, new FeatureEvaluationContext()));
    }

    [Fact]
    public void AddSunfishFeatureManagement_wires_default_stack()
    {
        var services = new ServiceCollection();
        services.AddSunfishFeatureManagement();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IFeatureCatalog>());
        Assert.NotNull(provider.GetRequiredService<IFeatureProvider>());
        Assert.NotNull(provider.GetRequiredService<IEntitlementResolver>());
        Assert.NotNull(provider.GetRequiredService<IFeatureEvaluator>());
    }

    private static (DefaultFeatureEvaluator Evaluator, InMemoryFeatureCatalog Catalog, InMemoryFeatureProvider Provider) Build()
    {
        var catalog = new InMemoryFeatureCatalog();
        var provider = new InMemoryFeatureProvider();
        var evaluator = new DefaultFeatureEvaluator(catalog, provider, new NoOpEntitlementResolver());
        return (evaluator, catalog, provider);
    }

    private sealed class StubEntitlementResolver(FeatureValue value) : IEntitlementResolver
    {
        public ValueTask<FeatureValue?> TryResolveAsync(
            FeatureKey key,
            FeatureEvaluationContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<FeatureValue?>(value);
    }
}
