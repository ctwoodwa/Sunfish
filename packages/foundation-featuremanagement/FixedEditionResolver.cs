using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// Returns a single configured edition key for every tenant. Useful for
/// demos and lite-mode deployments where edition selection is uniform.
/// </summary>
public sealed class FixedEditionResolver : IEditionResolver
{
    private readonly string _edition;

    /// <summary>Creates a resolver that always returns <paramref name="edition"/>.</summary>
    public FixedEditionResolver(string edition)
    {
        if (string.IsNullOrWhiteSpace(edition))
        {
            throw new ArgumentException("Edition must be non-empty.", nameof(edition));
        }

        _edition = edition;
    }

    /// <inheritdoc />
    public ValueTask<string?> ResolveEditionAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<string?>(_edition);
}
