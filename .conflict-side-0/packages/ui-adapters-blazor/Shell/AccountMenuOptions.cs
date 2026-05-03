namespace Sunfish.UIAdapters.Blazor.Shell;

/// <summary>
/// Configuration for a single entry in the account menu (label, icon, navigation target,
/// click handler, and visibility).
/// </summary>
public class AccountMenuItemOptions
{
    /// <summary>Whether the item is rendered. Items with <c>Visible = false</c> are omitted from the menu.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Display label shown to the user.</summary>
    public string Label { get; set; } = "";

    /// <summary>Icon name (provider-resolved) rendered next to the label.</summary>
    public string Icon { get; set; } = "";

    /// <summary>Optional navigation target. When set, activating the item navigates to this URL.</summary>
    public string? Href { get; set; }

    /// <summary>Optional click handler. Invoked when the item is activated; runs in addition to navigation if both are set.</summary>
    public Func<Task>? Action { get; set; }

    /// <summary>Optional secondary text rendered to the right of the label (for example, the active language).</summary>
    public string? CurrentValue { get; set; }

    /// <summary>Whether the item is rendered in a disabled (non-interactive) state.</summary>
    public bool Disabled { get; set; }

    /// <summary>Optional <c>data-testid</c> attribute for end-to-end tests.</summary>
    public string? TestId { get; set; }
}

/// <summary>
/// Aggregate options describing the standard sections of the Sunfish account menu
/// (settings, upgrade, install, submenus, sign-out). Each property is an
/// <see cref="AccountMenuItemOptions"/> that consumers may override or hide.
/// </summary>
/// <remarks>
/// The shipped defaults populate labels, icons, and hrefs that target the standard
/// <c>/account/*</c> routes. Set <c>Visible = false</c> on any item to omit it from
/// the rendered menu without removing it from the model.
/// </remarks>
public class AccountMenuOptions
{
    // ── Group 1: Core Settings ──

    /// <summary>"Account" entry — links to the user's account details by default.</summary>
    public AccountMenuItemOptions Account { get; set; } = new()
        { Label = "Account", Icon = "user-circle", Href = "/account/details" };

    /// <summary>"Preferences" entry — links to the user preferences page by default.</summary>
    public AccountMenuItemOptions Preferences { get; set; } = new()
        { Label = "Preferences", Icon = "sliders", Href = "/account/preferences" };

    /// <summary>"Personalization" entry — links to the personalization page by default.</summary>
    public AccountMenuItemOptions Personalization { get; set; } = new()
        { Label = "Personalization", Icon = "palette", Href = "/account/personalization" };

    /// <summary>"Shortcuts" entry — links to the keyboard-shortcuts page by default.</summary>
    public AccountMenuItemOptions Shortcuts { get; set; } = new()
        { Label = "Shortcuts", Icon = "keyboard", Href = "/account/shortcuts" };

    /// <summary>"Usage and credits" entry — hidden by default; surface for billing-aware deployments.</summary>
    public AccountMenuItemOptions UsageAndCredits { get; set; } = new()
        { Label = "Usage and credits", Icon = "gauge", Visible = false };

    /// <summary>"Connectors" entry — hidden by default; surface for deployments exposing third-party integrations.</summary>
    public AccountMenuItemOptions Connectors { get; set; } = new()
        { Label = "Connectors", Icon = "webhook", Visible = false };

    /// <summary>"All settings" entry — links to the consolidated settings page by default.</summary>
    public AccountMenuItemOptions AllSettings { get; set; } = new()
        { Label = "All settings", Icon = "settings", Href = "/account/details" };

    // ── Group 2: Upgrade ──

    /// <summary>"Upgrade plan" entry — hidden by default; surface for tiered/SaaS deployments.</summary>
    public AccountMenuItemOptions UpgradePlan { get; set; } = new()
        { Label = "Upgrade plan", Icon = "zap", Visible = false };

    // ── Group 3: Install ──

    /// <summary>"Install apps" entry — hidden by default; surface to advertise PWA/desktop installers.</summary>
    public AccountMenuItemOptions InstallApps { get; set; } = new()
        { Label = "Install apps", Icon = "download", Visible = false };

    // ── Group 4: Submenus ──

    /// <summary>"Appearance" submenu trigger — opens the light/dark/system selector.</summary>
    public AccountMenuItemOptions Appearance { get; set; } = new()
        { Label = "Appearance", Icon = "sun" };

    /// <summary>"Language" submenu trigger — opens the language selector with the current language as <see cref="AccountMenuItemOptions.CurrentValue"/>.</summary>
    public AccountMenuItemOptions Language { get; set; } = new()
        { Label = "Language", Icon = "globe", CurrentValue = "Default" };

    /// <summary>"Help" submenu trigger — opens the help/support items.</summary>
    public AccountMenuItemOptions Help { get; set; } = new()
        { Label = "Help", Icon = "help-circle" };

    // ── Group 5: Sign Out ──

    /// <summary>"Sign out" entry — separated from the other groups; consumers wire <see cref="AccountMenuItemOptions.Action"/> to perform the sign-out.</summary>
    public AccountMenuItemOptions SignOut { get; set; } = new()
        { Label = "Sign out", Icon = "log-out" };

    // ── Submenu Defaults ──

    /// <summary>Available appearance modes shown in the "Appearance" submenu. Defaults to Light / Dark / System.</summary>
    public List<string> AppearanceModes { get; set; } = ["Light", "Dark", "System"];

    /// <summary>Initial selected appearance mode. Should be one of <see cref="AppearanceModes"/>.</summary>
    public string DefaultAppearanceMode { get; set; } = "System";

    /// <summary>Available languages shown in the "Language" submenu. Defaults to Default / English.</summary>
    public List<string> Languages { get; set; } = ["Default", "English"];

    /// <summary>Initial selected language. Should be one of <see cref="Languages"/>.</summary>
    public string DefaultLanguage { get; set; } = "Default";
}
