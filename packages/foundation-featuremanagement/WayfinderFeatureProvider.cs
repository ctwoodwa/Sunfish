using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Wayfinder;

namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// <see cref="IFeatureProvider"/> backed by the Wayfinder/Atlas projection.
/// Resolves operator-issued feature toggles from the per-tenant Standing Order
/// log via <see cref="IAtlasProjector"/>. Returns <c>null</c> for any feature
/// key that has no Standing Order at the canonical path
/// <c>features.{key}</c> under <see cref="StandingOrderScope.Tenant"/>.
/// Per ADR 0009 §A1.4.
/// </summary>
/// <remarks>
/// <see cref="AtlasView.SettingsByPath"/> uses composite keys
/// <c>"&lt;scope-lower&gt;:&lt;path&gt;"</c>. The lookup key for an operator
/// toggle on <c>features.{key}</c> is therefore <c>"tenant:features.{key}"</c>.
/// </remarks>
public sealed class WayfinderFeatureProvider : IFeatureProvider
{
    private readonly IAtlasProjector _projector;

    /// <summary>
    /// Creates a provider bound to the supplied projector.
    /// </summary>
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

        // Operator feature toggles live under StandingOrderScope.Tenant
        // at path "features.{key}", composite key "tenant:features.{key}".
        var compositeKey = $"tenant:features.{key.Value}";

        var atlasView = await _projector.ProjectAsync(
            tenantId,
            scopeFilter: StandingOrderScope.Tenant,
            cancellationToken).ConfigureAwait(false);

        if (!atlasView.SettingsByPath.TryGetValue(compositeKey, out var snapshot))
            return null;

        if (snapshot.CurrentValue is null)
        {
            // Path exists in the log but was rescinded (null NewValue triple).
            return null;
        }

        // JsonNode.ToString() on boolean nodes produces lowercase "true"/"false";
        // FeatureValue.AsBoolean() consumes via bool.Parse — round-trip correct.
        var raw = snapshot.CurrentValue.ToString();
        return new FeatureValue { Raw = raw };
    }
}
