namespace Sunfish.Foundation.Localization;

/// <summary>
/// Marker type for Foundation's shared localized strings — generic phrases
/// that any higher Sunfish layer (ui-core, ui-adapters-*, blocks-*, accelerators)
/// might surface to end users (severity labels, common verbs, empty-state defaults).
/// Resolves <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/> against
/// <c>Resources/Localization/SharedResource.resx</c> embedded in this assembly.
/// </summary>
/// <remarks>
/// Foundation is the deepest layer in the layered architecture (no UI dependencies),
/// so a localization bundle here is the proof-of-cascade for the layered package
/// model: if Foundation can ship localized strings, every higher layer can too.
///
/// The strings here are intentionally generic — domain-shared phrases like severity
/// labels and common action verbs. Block- or accelerator-specific strings live in
/// their own SharedResource per-package (cf. Sunfish.Bridge.Localization.SharedResource,
/// Sunfish.Anchor.Localization.SharedResource).
///
/// Authoring discipline (per spec §3A + ADR 0034):
///   • Stable dotted keys; never English-text-as-key.
///   • Every &lt;data&gt; entry MUST carry a non-empty &lt;comment&gt; for translator
///     context. Once SUNFISH_I18N_001 is wired in this composition root, missing
///     comments become build errors.
///   • SmartFormat ICU-style placeholders ({count:plural:...}) per ADR 0036.
/// </remarks>
public sealed class SharedResource
{
}
