using Sunfish.Blocks.BusinessCases.Services;
using Sunfish.Foundation.Catalog.Bundles;
using Sunfish.Foundation.FeatureManagement;

namespace Sunfish.Blocks.BusinessCases.FeatureManagement;

/// <summary>
/// Implements <see cref="IEntitlementResolver"/> by mapping
/// <c>(tenant) → (active bundle + edition) → (feature values)</c> using the
/// bundle manifest's <c>FeatureDefaults</c> and <c>EditionMappings</c>.
/// Returns <see langword="null"/> for any feature the entitlement layer does not
/// determine, so the feature pipeline can fall through to other resolvers.
/// </summary>
public sealed class BundleEntitlementResolver : IEntitlementResolver
{
    private const string ModulePrefix = "modules.";
    private const string EnabledSuffix = ".enabled";

    private readonly IBundleCatalog _catalog;
    private readonly IBusinessCaseService _businessCases;

    /// <summary>Creates a new resolver.</summary>
    public BundleEntitlementResolver(IBundleCatalog catalog, IBusinessCaseService businessCases)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _businessCases = businessCases ?? throw new ArgumentNullException(nameof(businessCases));
    }

    /// <inheritdoc />
    public async ValueTask<FeatureValue?> TryResolveAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TenantId is not { } tenantId)
        {
            return null;
        }

        var record = await _businessCases
            .GetActiveRecordAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        if (!_catalog.TryGet(record.BundleKey, out var manifest) || manifest is null)
        {
            return null;
        }

        var rawKey = key.Value;

        if (manifest.FeatureDefaults.TryGetValue(rawKey, out var featureDefault))
        {
            return FeatureValue.Of(featureDefault);
        }

        if (rawKey.StartsWith(ModulePrefix, StringComparison.Ordinal)
            && rawKey.EndsWith(EnabledSuffix, StringComparison.Ordinal))
        {
            var moduleKey = rawKey.Substring(
                ModulePrefix.Length,
                rawKey.Length - ModulePrefix.Length - EnabledSuffix.Length);

            if (string.IsNullOrEmpty(moduleKey))
            {
                return null;
            }

            if (IsModuleActive(manifest, record.Edition, moduleKey))
            {
                return FeatureValue.Of(true);
            }

            return FeatureValue.Of(false);
        }

        return null;
    }

    private static bool IsModuleActive(BusinessCaseBundleManifest manifest, string edition, string moduleKey)
    {
        foreach (var required in manifest.RequiredModules)
        {
            if (string.Equals(required, moduleKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (manifest.EditionMappings.TryGetValue(edition, out var mapped))
        {
            foreach (var module in mapped)
            {
                if (string.Equals(module, moduleKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
