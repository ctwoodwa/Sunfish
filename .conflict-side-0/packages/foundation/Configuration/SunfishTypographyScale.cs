namespace Sunfish.Foundation.Configuration;

public record SunfishTypographyScale
{
    public string FontFamily { get; init; } = "'Segoe UI', system-ui, sans-serif";
    public string FontSizeBase { get; init; } = "14px";
    public string LineHeight { get; init; } = "1.5";
    public string H1Size { get; init; } = "32px";
    public string H2Size { get; init; } = "24px";
    public string H3Size { get; init; } = "20px";
    public string H4Size { get; init; } = "16px";
}
