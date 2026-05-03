using System;

namespace Sunfish.Compat.Heroicons;

/// <summary>
/// Translation helpers for <see cref="HeroiconName"/> → the canonical kebab-case
/// Heroicons slug used as the <c>heroicon-*</c> CSS class on the rendered element.
/// </summary>
public static class HeroiconNameExtensions
{
    /// <summary>
    /// Returns the canonical kebab-case Heroicons slug for this enum value.
    /// </summary>
    /// <remarks>
    /// Heroicons slugs are kebab-case (<c>magnifying-glass</c>, <c>cog-6-tooth</c>,
    /// <c>bars-3</c>, <c>squares-2x2</c>). This map is hand-authored rather than derived
    /// from <c>ToString()</c> because several canonical names would not survive a naive
    /// PascalCase→kebab transform — upstream uses numeric tokens that would mis-split
    /// (<c>Cog6Tooth</c> → <c>cog-6-tooth</c>, not <c>cog6-tooth</c>), and some names
    /// deliberately preserve compound numeric-letter segments (<c>Squares2x2</c> →
    /// <c>squares-2x2</c>, not <c>squares-2-x-2</c>).
    /// </remarks>
    public static string ToSlug(this HeroiconName name) => name switch
    {
        // Navigation & chrome
        HeroiconName.Home => "home",
        HeroiconName.MagnifyingGlass => "magnifying-glass",
        HeroiconName.Cog6Tooth => "cog-6-tooth",
        HeroiconName.User => "user",
        HeroiconName.Bars3 => "bars-3",
        HeroiconName.XMark => "x-mark",
        HeroiconName.Check => "check",

        // Arrows
        HeroiconName.ArrowLeft => "arrow-left",
        HeroiconName.ArrowRight => "arrow-right",
        HeroiconName.ArrowUp => "arrow-up",
        HeroiconName.ArrowDown => "arrow-down",
        HeroiconName.ChevronUp => "chevron-up",
        HeroiconName.ChevronDown => "chevron-down",
        HeroiconName.ChevronLeft => "chevron-left",
        HeroiconName.ChevronRight => "chevron-right",

        // Communication
        HeroiconName.Envelope => "envelope",
        HeroiconName.Phone => "phone",
        HeroiconName.Calendar => "calendar",
        HeroiconName.Clock => "clock",
        HeroiconName.ChatBubbleLeft => "chat-bubble-left",

        // Content types
        HeroiconName.Folder => "folder",
        HeroiconName.Document => "document",
        HeroiconName.Photo => "photo",
        HeroiconName.Film => "film",
        HeroiconName.MusicalNote => "musical-note",
        HeroiconName.BookmarkSquare => "bookmark-square",

        // Editing actions
        HeroiconName.Pencil => "pencil",
        HeroiconName.Trash => "trash",
        HeroiconName.Plus => "plus",
        HeroiconName.Minus => "minus",

        // Social / sharing
        HeroiconName.Heart => "heart",
        HeroiconName.Bookmark => "bookmark",
        HeroiconName.Share => "share",
        HeroiconName.DocumentDuplicate => "document-duplicate",
        HeroiconName.Printer => "printer",

        // Transfer
        HeroiconName.ArrowDownTray => "arrow-down-tray",
        HeroiconName.ArrowUpTray => "arrow-up-tray",

        // Status / alerts
        HeroiconName.InformationCircle => "information-circle",
        HeroiconName.ExclamationTriangle => "exclamation-triangle",
        HeroiconName.XCircle => "x-circle",
        HeroiconName.CheckCircle => "check-circle",

        // Data / layout
        HeroiconName.Squares2x2 => "squares-2x2",
        HeroiconName.ListBullet => "list-bullet",
        HeroiconName.Funnel => "funnel",
        HeroiconName.ArrowsUpDown => "arrows-up-down",

        // Media control
        HeroiconName.Play => "play",
        HeroiconName.Pause => "pause",
        HeroiconName.Stop => "stop",

        // Visibility & security
        HeroiconName.Eye => "eye",
        HeroiconName.LockClosed => "lock-closed",

        _ => throw new ArgumentOutOfRangeException(
            nameof(name), name,
            "HeroiconName value has no slug mapping. Add an entry to HeroiconNameExtensions.ToSlug under policy-gated review."),
    };

    /// <summary>
    /// Returns the canonical lowercase Heroicons-variant slug (<c>outline</c>,
    /// <c>solid</c>, <c>mini</c>) for the given <paramref name="variant"/>.
    /// Used as the <c>heroicon-&lt;variant&gt;</c> CSS class suffix.
    /// </summary>
    public static string ToSlug(this HeroiconVariant variant) => variant switch
    {
        HeroiconVariant.Solid => "solid",
        HeroiconVariant.Mini => "mini",
        _ => "outline"
    };
}
