namespace Sunfish.Blocks.Subscriptions.Localization;

/// <summary>
/// Marker type for the blocks-subscriptions shared localized strings — package-scoped
/// phrases shown in subscription-management UI surfaces (plans, billing,
/// renewals, dunning). Resolves
/// <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/> against
/// <c>Resources/Localization/SharedResource.resx</c> embedded in this assembly.
/// </summary>
/// <remarks>
/// Wave 2 cluster-A skeleton bundle. Strings here override Foundation's generic
/// SharedResource when the subscriptions-specific phrasing is more useful (e.g.,
/// "Saving subscription…" instead of plain "Save"). Plan 6 fills in the remaining
/// per-block UI copy.
///
/// Authoring discipline (per spec §3A + ADR 0034):
///   • Stable dotted keys; never English-text-as-key.
///   • Every &lt;data&gt; entry MUST carry a non-empty &lt;comment&gt; for translator
///     context (enforced by SUNFISH_I18N_001).
///   • SmartFormat ICU-style placeholders ({count:plural:...}) per ADR 0036.
/// </remarks>
public sealed class SharedResource
{
}
