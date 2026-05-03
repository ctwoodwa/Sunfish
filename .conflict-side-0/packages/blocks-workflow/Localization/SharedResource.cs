namespace Sunfish.Blocks.Workflow.Localization;

/// <summary>
/// Marker type for <c>Microsoft.Extensions.Localization.IStringLocalizer&lt;T&gt;</c>
/// (and the Sunfish-internal <c>ISunfishLocalizer&lt;T&gt;</c> wrapper) against the
/// Sunfish.Blocks.Workflow shared resource bundle. The type itself carries no
/// members — the localizer types only use the generic parameter as a correlation
/// handle to find the matching <c>Resources/Localization/SharedResource.resx</c>.
/// </summary>
/// <remarks>
/// Wave 2 Cluster C skeleton (Plan 2 Task 3.5). The bundle ships an 8-key scaffold
/// (severity / action / state.loading) seeded from foundation. Plan 6 replaces the
/// scaffold pilot strings with workflow-block end-user content (workflow runtime,
/// instance lifecycle, transition errors, completion notifications).
///
/// blocks-workflow uses a non-standard project layout — runtime classes and the
/// service-collection extensions live in <c>src/</c> rather than separate
/// <c>DependencyInjection/</c> + root folders. The Localization/ folder pair follows
/// the canonical Bridge / foundation convention regardless, so embedded-resource
/// resolution (<c>Resources.Localization.SharedResource</c>) lines up the same way.
/// </remarks>
public sealed class SharedResource
{
}
