using System;

namespace Sunfish.Compat.BootstrapIcons;

/// <summary>
/// Translation helpers for <see cref="IconName"/> → the canonical kebab-case Bootstrap
/// Icons slug used as the <c>bi-*</c> CSS class on the rendered element.
/// </summary>
public static class IconNameExtensions
{
    /// <summary>
    /// Returns the canonical kebab-case Bootstrap Icons slug for this enum value.
    /// </summary>
    /// <remarks>
    /// Bootstrap Icons names are kebab-case (<c>arrow-up</c>, <c>info-circle</c>,
    /// <c>x-circle</c>). This map is hand-authored rather than derived from
    /// <c>ToString()</c> because a few canonical names (e.g. <c>play-fill</c>,
    /// <c>exclamation-triangle</c>) would not survive a naive PascalCase→kebab transform.
    /// </remarks>
    public static string ToSlug(this IconName name) => name switch
    {
        // Core UI / navigation chrome
        IconName.House => "house",
        IconName.Search => "search",
        IconName.Gear => "gear",
        IconName.Person => "person",
        IconName.List => "list",
        IconName.X => "x",

        // Checkmarks / direction
        IconName.Check => "check",
        IconName.ArrowLeft => "arrow-left",
        IconName.ArrowRight => "arrow-right",
        IconName.ArrowUp => "arrow-up",
        IconName.ArrowDown => "arrow-down",
        IconName.ChevronUp => "chevron-up",
        IconName.ChevronDown => "chevron-down",
        IconName.ChevronLeft => "chevron-left",
        IconName.ChevronRight => "chevron-right",

        // Communication
        IconName.Envelope => "envelope",
        IconName.Telephone => "telephone",
        IconName.Calendar => "calendar",
        IconName.Clock => "clock",
        IconName.Chat => "chat",

        // Files / media
        IconName.Folder => "folder",
        IconName.FileText => "file-text",
        IconName.Image => "image",
        IconName.CameraVideo => "camera-video",
        IconName.MusicNote => "music-note",

        // Editing actions
        IconName.Save => "save",
        IconName.Pencil => "pencil",
        IconName.Trash => "trash",
        IconName.Plus => "plus",
        IconName.Dash => "dash",

        // Social / sharing
        IconName.Heart => "heart",
        IconName.Bookmark => "bookmark",
        IconName.Share => "share",
        IconName.Clipboard => "clipboard",
        IconName.Printer => "printer",

        // Transfer
        IconName.Download => "download",
        IconName.Upload => "upload",

        // Status / alerts
        IconName.InfoCircle => "info-circle",
        IconName.ExclamationTriangle => "exclamation-triangle",
        IconName.XCircle => "x-circle",
        IconName.CheckCircle => "check-circle",

        // Data / layout
        IconName.Grid => "grid",
        IconName.BarChart => "bar-chart",
        IconName.Filter => "filter",
        IconName.SortAlphaDown => "sort-alpha-down",

        // Media control
        IconName.PlayFill => "play-fill",
        IconName.PauseFill => "pause-fill",
        IconName.StopFill => "stop-fill",
        IconName.VolumeUp => "volume-up",
        IconName.Eye => "eye",

        _ => throw new ArgumentOutOfRangeException(
            nameof(name), name,
            "IconName value has no slug mapping. Add an entry to IconNameExtensions.ToSlug under policy-gated review."),
    };
}
