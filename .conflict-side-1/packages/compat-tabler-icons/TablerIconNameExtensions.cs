using System;

namespace Sunfish.Compat.TablerIcons;

/// <summary>
/// Translation helpers for <see cref="TablerIconName"/> → the canonical kebab-case
/// Tabler Icons slug used as the <c>tabler-*</c> CSS class on the rendered element.
/// </summary>
public static class TablerIconNameExtensions
{
    /// <summary>
    /// Returns the canonical kebab-case Tabler Icons slug for this enum value.
    /// </summary>
    /// <remarks>
    /// Tabler slugs are PascalCase→kebab-case transforms of the upstream icon name
    /// (<c>info-circle</c>, <c>device-floppy</c>, <c>player-play</c>, <c>menu-2</c>,
    /// <c>circle-check</c>, <c>arrows-sort</c>). This map is hand-authored rather than
    /// derived from <c>ToString()</c> to lock the exact upstream slug per enum value
    /// (preserving numeric suffixes like <c>menu-2</c>) and to catch any future
    /// additions that need attention.
    /// </remarks>
    public static string ToSlug(this TablerIconName name) => name switch
    {
        // Core UI / navigation chrome
        TablerIconName.Home => "home",
        TablerIconName.Search => "search",
        TablerIconName.Settings => "settings",
        TablerIconName.User => "user",
        TablerIconName.Menu2 => "menu-2",
        TablerIconName.X => "x",

        // Checkmarks / direction
        TablerIconName.Check => "check",
        TablerIconName.ChevronUp => "chevron-up",
        TablerIconName.ChevronDown => "chevron-down",
        TablerIconName.ChevronLeft => "chevron-left",
        TablerIconName.ChevronRight => "chevron-right",
        TablerIconName.ArrowUp => "arrow-up",
        TablerIconName.ArrowDown => "arrow-down",
        TablerIconName.ArrowLeft => "arrow-left",
        TablerIconName.ArrowRight => "arrow-right",

        // Communication
        TablerIconName.Mail => "mail",
        TablerIconName.Phone => "phone",
        TablerIconName.Calendar => "calendar",
        TablerIconName.Clock => "clock",
        TablerIconName.MessageCircle => "message-circle",

        // Files / media
        TablerIconName.Folder => "folder",
        TablerIconName.FileText => "file-text",
        TablerIconName.Photo => "photo",
        TablerIconName.Video => "video",
        TablerIconName.Music => "music",

        // Editing actions
        TablerIconName.DeviceFloppy => "device-floppy",
        TablerIconName.Pencil => "pencil",
        TablerIconName.Trash => "trash",
        TablerIconName.Plus => "plus",
        TablerIconName.Minus => "minus",

        // Social / sharing
        TablerIconName.Heart => "heart",
        TablerIconName.Bookmark => "bookmark",
        TablerIconName.Share => "share",
        TablerIconName.Copy => "copy",
        TablerIconName.Printer => "printer",

        // Transfer
        TablerIconName.Download => "download",
        TablerIconName.Upload => "upload",

        // Status / alerts
        TablerIconName.InfoCircle => "info-circle",
        TablerIconName.AlertTriangle => "alert-triangle",
        TablerIconName.AlertCircle => "alert-circle",
        TablerIconName.CircleCheck => "circle-check",

        // Data / layout
        TablerIconName.LayoutGrid => "layout-grid",
        TablerIconName.List => "list",
        TablerIconName.Filter => "filter",
        TablerIconName.ArrowsSort => "arrows-sort",

        // Media control
        TablerIconName.PlayerPlay => "player-play",
        TablerIconName.PlayerPause => "player-pause",
        TablerIconName.PlayerStop => "player-stop",
        TablerIconName.Eye => "eye",
        TablerIconName.EyeOff => "eye-off",
        TablerIconName.Lock => "lock",

        _ => throw new ArgumentOutOfRangeException(
            nameof(name), name,
            "TablerIconName value has no slug mapping. Add an entry to TablerIconNameExtensions.ToSlug under policy-gated review."),
    };
}
