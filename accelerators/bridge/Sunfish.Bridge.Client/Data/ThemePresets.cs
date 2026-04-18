using Marilo.Core.Configuration;

namespace Marilo.PmDemo.Client.Data;

public static class ThemePresets
{
    public record ThemePreset(string Name, MariloTheme Theme);

    public static readonly ThemePreset[] All =
    [
        new("Default", new MariloTheme
        {
            Colors = new MariloColorPalette
            {
                Dark = new MariloColorPalette
                {
                    Primary = "#60cdff",
                    Secondary = "#6bd18a",
                    Danger = "#ff6347",
                    Warning = "#fce100",
                    Info = "#60cdff",
                    Success = "#6ccb5f",
                    Neutral = "#8a8886",
                    Background = "#1b1a19",
                    Surface = "#252423",
                    OnPrimary = "#003a5c",
                    OnBackground = "#ffffff"
                }
            }
        }),

        new("Ocean", new MariloTheme
        {
            Colors = new MariloColorPalette
            {
                Primary = "#0077b6",
                Secondary = "#00b4d8",
                Danger = "#e63946",
                Warning = "#f4a261",
                Info = "#48cae4",
                Success = "#2a9d8f",
                Neutral = "#6c757d",
                Background = "#ffffff",
                Surface = "#f0f4f8",
                OnPrimary = "#ffffff",
                OnBackground = "#1d3557",
                Dark = new MariloColorPalette
                {
                    Primary = "#48cae4",
                    Secondary = "#90e0ef",
                    Danger = "#ff6b6b",
                    Warning = "#f4a261",
                    Info = "#48cae4",
                    Success = "#52b788",
                    Neutral = "#adb5bd",
                    Background = "#0d1b2a",
                    Surface = "#1b2838",
                    OnPrimary = "#0d1b2a",
                    OnBackground = "#e0e1dd"
                }
            },
            Shape = new MariloShape { BorderRadius = "6px", BorderRadiusLarge = "12px" }
        }),

        new("Forest", new MariloTheme
        {
            Colors = new MariloColorPalette
            {
                Primary = "#2d6a4f",
                Secondary = "#52b788",
                Danger = "#c1121f",
                Warning = "#e9c46a",
                Info = "#219ebc",
                Success = "#40916c",
                Neutral = "#6c757d",
                Background = "#ffffff",
                Surface = "#f1f7f0",
                OnPrimary = "#ffffff",
                OnBackground = "#1b4332",
                Dark = new MariloColorPalette
                {
                    Primary = "#52b788",
                    Secondary = "#95d5b2",
                    Danger = "#ff6b6b",
                    Warning = "#e9c46a",
                    Info = "#48cae4",
                    Success = "#74c69d",
                    Neutral = "#adb5bd",
                    Background = "#1a1f1a",
                    Surface = "#252d25",
                    OnPrimary = "#1a1f1a",
                    OnBackground = "#d8f3dc"
                }
            },
            Shape = new MariloShape { BorderRadius = "8px", BorderRadiusLarge = "16px" }
        }),

        new("Sunset", new MariloTheme
        {
            Colors = new MariloColorPalette
            {
                Primary = "#e76f51",
                Secondary = "#f4a261",
                Danger = "#d62828",
                Warning = "#fcbf49",
                Info = "#4895ef",
                Success = "#2a9d8f",
                Neutral = "#6c757d",
                Background = "#ffffff",
                Surface = "#fdf6f0",
                OnPrimary = "#ffffff",
                OnBackground = "#3d2c2c",
                Dark = new MariloColorPalette
                {
                    Primary = "#f4845f",
                    Secondary = "#f4a261",
                    Danger = "#ff6b6b",
                    Warning = "#fcbf49",
                    Info = "#7ec8e3",
                    Success = "#52b788",
                    Neutral = "#adb5bd",
                    Background = "#1f1518",
                    Surface = "#2c1e22",
                    OnPrimary = "#1f1518",
                    OnBackground = "#f2e8de"
                }
            },
            Shape = new MariloShape { BorderRadius = "12px", BorderRadiusLarge = "20px" }
        }),

        new("Minimal", new MariloTheme
        {
            Colors = new MariloColorPalette
            {
                Primary = "#333333",
                Secondary = "#666666",
                Danger = "#cc0000",
                Warning = "#cc8800",
                Info = "#0066cc",
                Success = "#008800",
                Neutral = "#888888",
                Background = "#ffffff",
                Surface = "#fafafa",
                OnPrimary = "#ffffff",
                OnBackground = "#111111",
                Dark = new MariloColorPalette
                {
                    Primary = "#cccccc",
                    Secondary = "#999999",
                    Danger = "#ff6666",
                    Warning = "#ffbb55",
                    Info = "#6699ff",
                    Success = "#66cc66",
                    Neutral = "#888888",
                    Background = "#111111",
                    Surface = "#1a1a1a",
                    OnPrimary = "#111111",
                    OnBackground = "#eeeeee"
                }
            },
            Typography = new MariloTypographyScale
            {
                FontFamily = "'Inter', system-ui, sans-serif",
                FontSizeBase = "14px"
            },
            Shape = new MariloShape { BorderRadius = "2px", BorderRadiusLarge = "4px" }
        })
    ];

    public static ThemePreset GetByName(string name) =>
        All.FirstOrDefault(p => p.Name == name) ?? All[0];
}
