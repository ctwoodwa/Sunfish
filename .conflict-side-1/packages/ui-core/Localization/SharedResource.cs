namespace Sunfish.UICore.Localization;

/// <summary>
/// Marker type for Sunfish.UICore's shared localized strings — the framework-agnostic
/// UI-contract layer's generic phrases (severity labels, action verbs, empty-state
/// defaults) surfaced through ui-core's renderer / icon / CSS contracts before any
/// adapter (Blazor / React / future native) is chosen. Resolves
/// <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/> against
/// <c>Resources/Localization/SharedResource.resx</c> embedded in this assembly.
/// </summary>
/// <remarks>
/// Pattern B (contracts-only library) per the Wave-2 cluster freeze: ui-core has no DI
/// surface of its own — there is no <c>Program.cs</c> and no <c>ServiceCollectionExtensions</c>.
/// The marker type and resx bundle ship here so the cascade can place strings on the
/// framework-agnostic seam, but consumers (apps / accelerators / adapters) wire
/// <c>ISunfishLocalizer&lt;SharedResource&gt;</c> in their own composition root via the
/// foundation-provided registration pattern.
///
/// Authoring discipline (per spec §3A + ADR 0034):
///   • Stable dotted keys; never English-text-as-key.
///   • Every &lt;data&gt; entry MUST carry a non-empty &lt;comment&gt; — enforced by the
///     SUNFISH_I18N_001 analyzer auto-wired through Directory.Build.props when a
///     project contains <c>Resources/Localization/</c>.
///   • SmartFormat ICU-style placeholders ({count:plural:...}) per ADR 0036.
/// </remarks>
public sealed class SharedResource
{
}
