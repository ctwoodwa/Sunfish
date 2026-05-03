using System;

namespace Sunfish.Compat.Octicons;

/// <summary>
/// Translation helpers for <see cref="OcticonName"/> → the canonical kebab-case
/// Octicons slug used as the <c>octicon-*</c> CSS class on the rendered element.
/// </summary>
public static class OcticonNameExtensions
{
    /// <summary>
    /// Returns the canonical kebab-case Octicons slug for this enum value.
    /// </summary>
    /// <remarks>
    /// Octicons slugs are kebab-case (<c>mark-github</c>, <c>issue-opened</c>,
    /// <c>star-fill</c>, <c>plus-circle</c>). This map is hand-authored rather than
    /// derived from <c>ToString()</c> so that every upstream slug is explicitly
    /// verified against the Primer Octicons catalog and any future additions require
    /// a policy-gated arm rather than silently deriving from a naive transform.
    /// </remarks>
    public static string ToSlug(this OcticonName name) => name switch
    {
        // GitHub-branded core
        OcticonName.MarkGithub => "mark-github",
        OcticonName.Repo => "repo",
        OcticonName.GitBranch => "git-branch",
        OcticonName.GitCommit => "git-commit",
        OcticonName.GitMerge => "git-merge",
        OcticonName.GitPullRequest => "git-pull-request",
        OcticonName.IssueOpened => "issue-opened",
        OcticonName.IssueClosed => "issue-closed",

        // Checkmarks / direction
        OcticonName.Check => "check",
        OcticonName.X => "x",
        OcticonName.ChevronUp => "chevron-up",
        OcticonName.ChevronDown => "chevron-down",
        OcticonName.ChevronLeft => "chevron-left",
        OcticonName.ChevronRight => "chevron-right",
        OcticonName.ArrowUp => "arrow-up",
        OcticonName.ArrowDown => "arrow-down",
        OcticonName.ArrowLeft => "arrow-left",
        OcticonName.ArrowRight => "arrow-right",

        // Core UI / navigation chrome
        OcticonName.Home => "home",
        OcticonName.Gear => "gear",
        OcticonName.Person => "person",
        OcticonName.People => "people",
        OcticonName.Organization => "organization",

        // Security / access
        OcticonName.Key => "key",
        OcticonName.Lock => "lock",
        OcticonName.Unlock => "unlock",
        OcticonName.Eye => "eye",
        OcticonName.EyeClosed => "eye-closed",

        // Social / bookmarking
        OcticonName.Heart => "heart",
        OcticonName.Star => "star",
        OcticonName.StarFill => "star-fill",
        OcticonName.Bookmark => "bookmark",
        OcticonName.BookmarkFill => "bookmark-fill",

        // Data / layout
        OcticonName.Search => "search",
        OcticonName.Filter => "filter",
        OcticonName.Sort => "sort",

        // Transfer
        OcticonName.Download => "download",
        OcticonName.Upload => "upload",

        // Editing actions
        OcticonName.Pencil => "pencil",
        OcticonName.Trash => "trash",
        OcticonName.Plus => "plus",
        OcticonName.PlusCircle => "plus-circle",
        OcticonName.Dash => "dash",

        // Communication / notification
        OcticonName.Comment => "comment",
        OcticonName.Mail => "mail",
        OcticonName.Bell => "bell",

        // Status / alerts
        OcticonName.Info => "info",
        OcticonName.Alert => "alert",
        OcticonName.Stop => "stop",
        OcticonName.CheckCircle => "check-circle",

        _ => throw new ArgumentOutOfRangeException(
            nameof(name), name,
            "OcticonName value has no slug mapping. Add an entry to OcticonNameExtensions.ToSlug under policy-gated review."),
    };
}
