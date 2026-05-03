namespace Sunfish.UIAdapters.Blazor.Localization;

/// <summary>
/// Marker type for Sunfish.UIAdapters.Blazor's shared localized strings — adapter-level
/// generic phrases (severity badges, action-button labels, empty-state placeholders)
/// rendered by Blazor component implementations of the ui-core contracts. Resolves
/// <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/> against
/// <c>Resources/Localization/SharedResource.resx</c> embedded in this assembly.
/// </summary>
/// <remarks>
/// Pattern A (adapter with DI surface) per the Wave-2 cluster freeze: this cluster
/// also registers <c>ISunfishLocalizer&lt;&gt;</c> through
/// <c>RendererServiceCollectionExtensions</c> via <c>TryAddSingleton</c> so consumers
/// pick up the localizer transparently when they wire the renderer. The adapter does
/// NOT call <c>services.AddLocalization()</c> itself — that lives in consumer
/// composition roots (apps / accelerators) per the Cluster A sentinel ratification.
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
