namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// Null-returning <see cref="IEntitlementResolver"/>. Default registration
/// until a bundle-manifest-backed resolver ships in P2 (<c>blocks-businesscases</c>).
/// </summary>
public sealed class NoOpEntitlementResolver : IEntitlementResolver
{
    /// <inheritdoc />
    public ValueTask<FeatureValue?> TryResolveAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<FeatureValue?>(null);
}
