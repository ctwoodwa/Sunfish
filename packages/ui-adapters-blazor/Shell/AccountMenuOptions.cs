namespace Sunfish.UIAdapters.Blazor.Shell;

public class AccountMenuItemOptions
{
    public bool Visible { get; set; } = true;
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public string? Href { get; set; }
    public Func<Task>? Action { get; set; }
    public string? CurrentValue { get; set; }
    public bool Disabled { get; set; }
    public string? TestId { get; set; }
}

public class AccountMenuOptions
{
    // ── Group 1: Core Settings ──
    public AccountMenuItemOptions Account { get; set; } = new()
        { Label = "Account", Icon = "user-circle", Href = "/account/details" };

    public AccountMenuItemOptions Preferences { get; set; } = new()
        { Label = "Preferences", Icon = "sliders", Href = "/account/preferences" };

    public AccountMenuItemOptions Personalization { get; set; } = new()
        { Label = "Personalization", Icon = "palette", Href = "/account/personalization" };

    public AccountMenuItemOptions Shortcuts { get; set; } = new()
        { Label = "Shortcuts", Icon = "keyboard", Href = "/account/shortcuts" };

    public AccountMenuItemOptions UsageAndCredits { get; set; } = new()
        { Label = "Usage and credits", Icon = "gauge", Visible = false };

    public AccountMenuItemOptions Connectors { get; set; } = new()
        { Label = "Connectors", Icon = "webhook", Visible = false };

    public AccountMenuItemOptions AllSettings { get; set; } = new()
        { Label = "All settings", Icon = "settings", Href = "/account/details" };

    // ── Group 2: Upgrade ──
    public AccountMenuItemOptions UpgradePlan { get; set; } = new()
        { Label = "Upgrade plan", Icon = "zap", Visible = false };

    // ── Group 3: Install ──
    public AccountMenuItemOptions InstallApps { get; set; } = new()
        { Label = "Install apps", Icon = "download", Visible = false };

    // ── Group 4: Submenus ──
    public AccountMenuItemOptions Appearance { get; set; } = new()
        { Label = "Appearance", Icon = "sun" };

    public AccountMenuItemOptions Language { get; set; } = new()
        { Label = "Language", Icon = "globe", CurrentValue = "Default" };

    public AccountMenuItemOptions Help { get; set; } = new()
        { Label = "Help", Icon = "help-circle" };

    // ── Group 5: Sign Out ──
    public AccountMenuItemOptions SignOut { get; set; } = new()
        { Label = "Sign out", Icon = "log-out" };

    // ── Submenu Defaults ──
    public List<string> AppearanceModes { get; set; } = ["Light", "Dark", "System"];
    public string DefaultAppearanceMode { get; set; } = "System";
    public List<string> Languages { get; set; } = ["Default", "English"];
    public string DefaultLanguage { get; set; } = "Default";
}
