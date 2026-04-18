namespace Sunfish.Compat.Telerik.ThemeConstants;

/// <summary>
/// Mirrors Telerik.Blazor.ThemeConstants string constants. Literal values match Telerik
/// exactly so that existing consumer code referencing
/// <c>ThemeConstants.Button.ThemeColor.Primary</c> continues to resolve to the string
/// <c>"primary"</c> after migration.
/// Phase 6 ships only the subset of constants needed by the 12 wrappers; additional
/// sections are added by future wrapper PRs under the policy gate.
/// </summary>
public static class Size
{
    public const string Small = "sm";
    public const string Medium = "md";
    public const string Large = "lg";
}

public static class FillMode
{
    public const string Solid = "solid";
    public const string Outline = "outline";
    public const string Flat = "flat";
    public const string Link = "link";
    public const string Clear = "clear";
}

public static class Rounded
{
    public const string Small = "small";
    public const string Medium = "medium";
    public const string Large = "large";
    public const string Full = "full";
    public const string None = "none";
}

public static class Button
{
    public static class ThemeColor
    {
        public const string Base = "base";
        public const string Primary = "primary";
        public const string Secondary = "secondary";
        public const string Tertiary = "tertiary";
        public const string Info = "info";
        public const string Success = "success";
        public const string Warning = "warning";
        public const string Error = "error";
        public const string Dark = "dark";
        public const string Light = "light";
        public const string Inverse = "inverse";
    }
}

public static class Window
{
    public static class ThemeColor
    {
        public const string Base = "base";
        public const string Primary = "primary";
        public const string Info = "info";
        public const string Success = "success";
        public const string Warning = "warning";
        public const string Error = "error";
    }
}

public static class Notification
{
    public static class ThemeColor
    {
        public const string Base = "base";
        public const string Primary = "primary";
        public const string Info = "info";
        public const string Success = "success";
        public const string Warning = "warning";
        public const string Error = "error";
    }
}

public static class Grid
{
    public static class Selectable
    {
        public static class Mode
        {
            public const string Single = "single";
            public const string Multiple = "multiple";
        }
    }
}
