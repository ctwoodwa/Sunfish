namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// Default <see cref="IFeatureEvaluator"/>. Chains provider → entitlements →
/// catalog default, throwing when no layer produces a value.
/// </summary>
public sealed class DefaultFeatureEvaluator : IFeatureEvaluator
{
    private readonly IFeatureCatalog _catalog;
    private readonly IFeatureProvider _provider;
    private readonly IEntitlementResolver _entitlements;

    /// <summary>Creates an evaluator over the supplied layers.</summary>
    public DefaultFeatureEvaluator(
        IFeatureCatalog catalog,
        IFeatureProvider provider,
        IEntitlementResolver entitlements)
    {
        _catalog = catalog;
        _provider = provider;
        _entitlements = entitlements;
    }

    /// <inheritdoc />
    public async ValueTask<FeatureValue> EvaluateAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_catalog.TryGetFeature(key, out var spec))
        {
            throw new InvalidOperationException(
                $"Feature '{key}' is not registered in the catalog. Register a FeatureSpec before evaluation.");
        }

        var overrideValue = await _provider.TryGetAsync(key, context, cancellationToken).ConfigureAwait(false);
        if (overrideValue is not null)
        {
            return overrideValue;
        }

        var entitled = await _entitlements.TryResolveAsync(key, context, cancellationToken).ConfigureAwait(false);
        if (entitled is not null)
        {
            return entitled;
        }

        if (spec.DefaultValue is not null)
        {
            return new FeatureValue { Raw = spec.DefaultValue };
        }

        throw new InvalidOperationException(
            $"Feature '{key}' has no provider override, no entitlement value, and no catalog default.");
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsEnabledAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var value = await EvaluateAsync(key, context, cancellationToken).ConfigureAwait(false);
        return value.AsBoolean();
    }
}
