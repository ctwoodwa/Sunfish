namespace Sunfish.Bridge.Client.Models.Settings;

public sealed record SettingsNavItem(
    string Label,
    string Href,
    string? Icon = null,
    string? Description = null,
    bool MatchPrefix = false);
