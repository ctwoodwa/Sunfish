namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// Top-level feature-evaluation surface. Consumers call
/// <see cref="EvaluateAsync"/> for variant features and <see cref="IsEnabledAsync"/>
/// for boolean flags.
/// </summary>
public interface IFeatureEvaluator
{
    /// <summary>
    /// Evaluates a feature. Throws <see cref="InvalidOperationException"/> when
    /// the feature is not in the catalog or when no resolver produced a value
    /// and the spec has no default.
    /// </summary>
    ValueTask<FeatureValue> EvaluateAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Boolean-sugar over <see cref="EvaluateAsync"/>.</summary>
    ValueTask<bool> IsEnabledAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default);
}
