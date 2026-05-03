using System;

namespace Sunfish.Compat.Lucide;

/// <summary>
/// Translation helpers for <see cref="LucideIconName"/> → the canonical kebab-case
/// Lucide slug used as the <c>lucide-*</c> CSS class on the rendered element.
/// </summary>
public static class LucideIconNameExtensions
{
    /// <summary>
    /// Returns the canonical kebab-case Lucide slug for this enum value.
    /// </summary>
    /// <remarks>
    /// Lucide slugs are kebab-case (<c>chevron-up</c>, <c>alert-triangle</c>,
    /// <c>file-text</c>). Most are a direct PascalCase→kebab-case transform of the
    /// enum name, but a few canonical names preserve numeric suffixes without a
    /// separator (<c>trash-2</c>, <c>share-2</c>). This map is hand-authored rather
    /// than derived from <c>ToString()</c> to lock the exact upstream slug per enum
    /// value and to catch any future additions that need attention.
    /// </remarks>
    public static string ToSlug(this LucideIconName name) => name switch
    {
        // Core UI / navigation chrome
        LucideIconName.Home => "home",
        LucideIconName.Search => "search",
        LucideIconName.Settings => "settings",
        LucideIconName.User => "user",
        LucideIconName.Menu => "menu",
        LucideIconName.X => "x",

        // Checkmarks / direction
        LucideIconName.Check => "check",
        LucideIconName.ArrowLeft => "arrow-left",
        LucideIconName.ArrowRight => "arrow-right",
        LucideIconName.ArrowUp => "arrow-up",
        LucideIconName.ArrowDown => "arrow-down",
        LucideIconName.ChevronUp => "chevron-up",
        LucideIconName.ChevronDown => "chevron-down",
        LucideIconName.ChevronLeft => "chevron-left",
        LucideIconName.ChevronRight => "chevron-right",

        // Communication
        LucideIconName.Mail => "mail",
        LucideIconName.Phone => "phone",
        LucideIconName.Calendar => "calendar",
        LucideIconName.Clock => "clock",
        LucideIconName.MessageCircle => "message-circle",

        // Files / media
        LucideIconName.Folder => "folder",
        LucideIconName.FileText => "file-text",
        LucideIconName.Image => "image",
        LucideIconName.Video => "video",
        LucideIconName.Music => "music",

        // Editing actions
        LucideIconName.Save => "save",
        LucideIconName.Edit => "edit",
        LucideIconName.Trash2 => "trash-2",
        LucideIconName.Plus => "plus",
        LucideIconName.Minus => "minus",

        // Social / sharing
        LucideIconName.Heart => "heart",
        LucideIconName.Bookmark => "bookmark",
        LucideIconName.Share2 => "share-2",
        LucideIconName.Copy => "copy",
        LucideIconName.Printer => "printer",

        // Transfer
        LucideIconName.Download => "download",
        LucideIconName.Upload => "upload",

        // Status / alerts
        LucideIconName.Info => "info",
        LucideIconName.AlertTriangle => "alert-triangle",
        LucideIconName.AlertCircle => "alert-circle",
        LucideIconName.CheckCircle => "check-circle",

        // Data / layout
        LucideIconName.LayoutGrid => "layout-grid",
        LucideIconName.List => "list",
        LucideIconName.Filter => "filter",
        LucideIconName.ArrowDownUp => "arrow-down-up",

        // Media control
        LucideIconName.Play => "play",
        LucideIconName.Pause => "pause",
        LucideIconName.Square => "square",
        LucideIconName.Eye => "eye",
        LucideIconName.Lock => "lock",

        _ => throw new ArgumentOutOfRangeException(
            nameof(name), name,
            "LucideIconName value has no slug mapping. Add an entry to LucideIconNameExtensions.ToSlug under policy-gated review."),
    };
}
