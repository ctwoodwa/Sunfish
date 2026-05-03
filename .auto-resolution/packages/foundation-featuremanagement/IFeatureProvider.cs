namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// OpenFeature-style provider seam. An adapter returns a value if it has an
/// opinion on the feature in this context, <c>null</c> otherwise. Sunfish
/// never depends on any specific provider — OpenFeature, Microsoft.FeatureManagement,
/// LaunchDarkly, flagd, and bespoke backends all plug in here.
/// </summary>
public interface IFeatureProvider
{
    /// <summary>
    /// Returns a value for the given feature in the given context, or
    /// <c>null</c> when this provider declines to evaluate.
    /// </summary>
    ValueTask<FeatureValue?> TryGetAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default);
}
