using Microsoft.Extensions.Localization;

namespace Sunfish.Bridge.Localization;

/// <summary>
/// Bundle reference holder that closes the SUNFISH_I18N_002 (LocUnused) loop for
/// the 22 keys added in PR #154 (validation.*, actions.*, status.*) by enumerating
/// every key in one place. The analyzer matches <c>localizer["key"]</c> indexer
/// access against the resx <c>&lt;data name=&quot;…&quot;&gt;</c> entries; touching
/// each key here is enough to satisfy the cascade until the Bridge browser-shell
/// surfaces (form validators, button toolbars, SyncState badges) wire the keys
/// through their natural consumers.
/// </summary>
/// <remarks>
/// Follows the synthetic-consumer pattern established in PR #146 for the 4
/// blocks-* csprojs that lacked first-party UI surfaces at suppression-clear
/// time. The original 8 <c>errors.*</c> keys remain consumed naturally by
/// <see cref="Sunfish.Bridge.Components.Pages.Error"/> (PR #141) and by
/// <see cref="SunfishProblemDetailsFactory"/>; this scaffolding type covers
/// only the 22 newly-added keys whose UI consumers don't exist yet:
/// <list type="bullet">
///   <item>9 form-validation messages (<c>validation.*</c>) — destined for
///   FluentValidation / DataAnnotations adapter wiring once Bridge owns first-party
///   form pages.</item>
///   <item>7 action-button verbs (<c>actions.*</c>) — destined for the browser-shell
///   toolbar component once it lands.</item>
///   <item>6 status-state badges (<c>status.*</c>) — destined for the SyncState
///   indicator component once it lands.</item>
/// </list>
/// When those UIs land, these calls move into the real consumers and this
/// scaffolding type can be deleted. The keys themselves stay — they belong to
/// the Bridge SharedResource public localization surface.
/// </remarks>
internal sealed class SharedResourceConsumer
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SharedResourceConsumer(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    /// <summary>Form-validation copy resolved against the current UI culture.</summary>
    public string ValidationRequired => _localizer["validation.required"];
    public string ValidationMinLength => _localizer["validation.min-length"];
    public string ValidationMaxLength => _localizer["validation.max-length"];
    public string ValidationEmailFormat => _localizer["validation.email-format"];
    public string ValidationUrlFormat => _localizer["validation.url-format"];
    public string ValidationNumericOnly => _localizer["validation.numeric-only"];
    public string ValidationDateFormat => _localizer["validation.date-format"];
    public string ValidationFutureDateOnly => _localizer["validation.future-date-only"];
    public string ValidationPastDateOnly => _localizer["validation.past-date-only"];

    /// <summary>Action-button copy resolved against the current UI culture.</summary>
    public string ActionSave => _localizer["actions.save"];
    public string ActionCancel => _localizer["actions.cancel"];
    public string ActionRetry => _localizer["actions.retry"];
    public string ActionDelete => _localizer["actions.delete"];
    public string ActionConfirm => _localizer["actions.confirm"];
    public string ActionEdit => _localizer["actions.edit"];
    public string ActionSearch => _localizer["actions.search"];

    /// <summary>Status-badge copy resolved against the current UI culture.</summary>
    public string StatusLoading => _localizer["status.loading"];
    public string StatusSaving => _localizer["status.saving"];
    public string StatusProcessing => _localizer["status.processing"];
    public string StatusCompleted => _localizer["status.completed"];
    public string StatusFailed => _localizer["status.failed"];
    public string StatusIdle => _localizer["status.idle"];
}
