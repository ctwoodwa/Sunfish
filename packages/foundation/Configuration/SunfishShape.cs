namespace Sunfish.Foundation.Configuration;

public record SunfishShape
{
    public string BorderRadius { get; init; } = "4px";
    public string BorderRadiusLarge { get; init; } = "8px";
    public string Elevation1 { get; init; } = "0 2px 4px rgba(0,0,0,0.1)";
    public string Elevation2 { get; init; } = "0 4px 8px rgba(0,0,0,0.12)";
    public string Elevation3 { get; init; } = "0 8px 16px rgba(0,0,0,0.14)";
}
