namespace Sunfish.KitchenSink.Localization;

/// <summary>
/// Marker type for the kitchen-sink demo's localized strings — runner UI,
/// settings panel, demo prompts. Resolves
/// <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/> against
/// <c>Resources/Localization/SharedResource.resx</c> embedded in this assembly.
/// </summary>
/// <remarks>
/// Kitchen-sink is the Sunfish demo composition root. It owns the central
/// <c>services.AddLocalization()</c> call (Pattern B blocks like ui-core,
/// blocks-tasks, blocks-forms cannot self-register), and ships its own bundle
/// for kitchen-sink-specific copy distinct from the foundation bundle.
///
/// Authoring discipline (per spec §3A + ADR 0034):
///   • Stable dotted keys; never English-text-as-key.
///   • Every &lt;data&gt; entry MUST carry a non-empty &lt;comment&gt; for translator
///     context. Pilot rows here are flagged with the
///     <c>[scaffold-pilot — replace in Plan 6]</c> token; SUNFISH_I18N_001 enforces
///     the non-empty rule, and Plan 6 deliverables strip the pilot token once
///     translators ship the production strings.
///   • SmartFormat ICU-style placeholders ({count:plural:...}) per ADR 0036.
/// </remarks>
public sealed class SharedResource
{
}
