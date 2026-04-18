namespace Marilo.PmDemo.Client.Models.Settings;

public sealed record SettingsNavGroup(
    string Title,
    IReadOnlyList<SettingsNavItem> Items);
