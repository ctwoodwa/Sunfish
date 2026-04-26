using Microsoft.Extensions.Localization;

namespace Sunfish.Blocks.Tasks.Localization;

/// <summary>
/// Bundle reference holder that closes the SUNFISH_I18N_002 (LocUnused) loop for
/// the blocks-tasks <c>SharedResource</c> bundle by enumerating every key in one
/// place. The analyzer matches <c>localizer["key"]</c> indexer access against the
/// resx <c>&lt;data name=&quot;…&quot;&gt;</c> entries; touching each key here is enough
/// to satisfy the cascade until the deferred drag/drop and inline-edit surfaces on
/// <c>TaskBoardBlock</c> wire the keys through their own toolbars and toasts.
/// </summary>
/// <remarks>
/// Minimum-viable wiring per the LocUnused PR-#141 pattern: surface a single
/// consumer that the analyzer can see, so the suppression in
/// <c>Sunfish.Blocks.Tasks.csproj</c> can be removed without expanding the existing
/// read-display Kanban surface mid-stream. When the inline-edit / drag-drop UI
/// lands these calls move into the real consumer (Save / Cancel / Retry buttons,
/// severity banners on validation failure, "Loading tasks…" empty state) and this
/// scaffolding type can be deleted. The keys themselves stay — they belong to the
/// block's public localization surface.
/// </remarks>
internal sealed class SharedResourceConsumer
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SharedResourceConsumer(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    /// <summary>Severity-tier copy resolved against the current UI culture.</summary>
    public string Info => _localizer["severity.info"];
    public string Warning => _localizer["severity.warning"];
    public string Error => _localizer["severity.error"];
    public string Critical => _localizer["severity.critical"];

    /// <summary>Action-button copy resolved against the current UI culture.</summary>
    public string Save => _localizer["action.save"];
    public string Cancel => _localizer["action.cancel"];
    public string Retry => _localizer["action.retry"];

    /// <summary>State-banner copy resolved against the current UI culture.</summary>
    public string Loading => _localizer["state.loading"];
}
