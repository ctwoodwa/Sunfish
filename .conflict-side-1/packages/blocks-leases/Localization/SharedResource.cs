namespace Sunfish.Blocks.Leases.Localization;

/// <summary>
/// Marker type for <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/>
/// against the Sunfish.Blocks.Leases shared resource bundle. The type itself carries
/// no members — IStringLocalizer{T} only uses the generic parameter as a correlation
/// handle to find the matching <c>Resources/Localization/SharedResource.resx</c>.
/// </summary>
/// <remarks>
/// Wave 2 Cluster C skeleton (Plan 2 Task 3.5). The bundle ships an 8-key scaffold
/// (severity / action / state.loading) seeded from foundation. Plan 6 replaces the
/// scaffold pilot strings with block-scoped end-user content (lease list, lease
/// lifecycle, renewal / termination flows).
/// </remarks>
public sealed class SharedResource
{
}
