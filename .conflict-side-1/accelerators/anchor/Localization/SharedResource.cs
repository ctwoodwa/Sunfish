namespace Sunfish.Anchor.Localization;

/// <summary>
/// Marker type for Anchor's shared localized strings. Resolves
/// <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/> against
/// <c>Resources/Localization/SharedResource.resx</c> (and its locale satellites).
/// </summary>
/// <remarks>
/// Mirrors <c>Sunfish.Bridge.Localization.SharedResource</c> (Plan 2 Task 4.2).
/// Anchor is a MAUI Blazor Hybrid desktop app — there is no
/// <c>UseRequestLocalization</c> middleware; <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>
/// is initialized from the device's preferred UI language at startup
/// (see <c>MauiProgram.CreateMauiApp</c>) and the same <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/>
/// surface flows through to Blazor components in the WebView.
///
/// Authoring discipline (per spec §3A + ADR 0034):
///   • Stable dotted keys only; never English-text-as-key.
///   • Every &lt;data&gt; entry MUST carry a non-empty &lt;comment&gt; for translator
///     context. Once the loc-comments analyzer (SUNFISH_I18N_001) is wired in
///     this composition root, missing-comment entries become build errors.
///   • SmartFormat ICU-style placeholders ({count:plural:...}) per ADR 0036.
/// </remarks>
public sealed class SharedResource
{
}
