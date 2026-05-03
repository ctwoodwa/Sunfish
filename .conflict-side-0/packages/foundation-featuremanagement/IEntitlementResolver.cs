namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// Resolves feature values from tenant entitlements — bundle activations,
/// edition mappings, and add-ons. Returns <c>null</c> when entitlement
/// rules do not determine a value for this feature in this context.
/// </summary>
public interface IEntitlementResolver
{
    /// <summary>
    /// Attempts to resolve a feature value from the tenant's entitlement state.
    /// </summary>
    ValueTask<FeatureValue?> TryResolveAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default);
}
