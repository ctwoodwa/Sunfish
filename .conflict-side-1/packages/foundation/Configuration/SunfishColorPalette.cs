namespace Sunfish.Foundation.Configuration;

public record SunfishColorPalette
{
    public string Primary { get; init; } = "#0078d4";
    public string Secondary { get; init; } = "#107c10";
    public string Danger { get; init; } = "#d13438";
    public string Warning { get; init; } = "#ffaa44";
    public string Info { get; init; } = "#00bcf2";
    public string Success { get; init; } = "#107c10";
    public string Neutral { get; init; } = "#605e5c";
    public string Background { get; init; } = "#ffffff";
    public string Surface { get; init; } = "#f3f2f1";
    public string OnPrimary { get; init; } = "#ffffff";
    public string OnBackground { get; init; } = "#323130";
    public SunfishColorPalette? Dark { get; init; }
}
