namespace Sunfish.Blocks.Maintenance.Localization;

/// <summary>
/// Marker type for the blocks-maintenance shared localized strings —
/// package-scoped phrases shown in maintenance UI surfaces (vendors, requests,
/// RFQ/quote workflow, work orders). Resolves
/// <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/> against
/// <c>Resources/Localization/SharedResource.resx</c> embedded in this assembly.
/// </summary>
/// <remarks>
/// Wave 2 cluster-B skeleton bundle. Pattern A package —
/// <c>MaintenanceServiceCollectionExtensions.AddInMemoryMaintenance</c>
/// contributes the open-generic
/// <see cref="Sunfish.Foundation.Localization.ISunfishLocalizer{T}"/> binding
/// so consumers can resolve this <c>SharedResource</c>. The composition root
/// still owns <c>services.AddLocalization()</c>. Strings here override
/// Foundation's generic SharedResource when the maintenance-specific phrasing
/// is more useful (e.g., "Saving maintenance record…" instead of plain "Save").
/// Plan 6 fills in the remaining per-block UI copy.
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
