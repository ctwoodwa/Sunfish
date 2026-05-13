using System;
using System.IO;
using System.Text.RegularExpressions;
using Sunfish.UIAdapters.Blazor.A11y;
using Xunit;

namespace Sunfish.Anchor.Tests.A11y;

/// <summary>
/// W#47 Phase 4 — WCAG 2.2 AA + EN 301 549 v3.2.1 structural a11y assertions for
/// <c>SystemRequirementsRegressionBanner.razor</c> (PostInstallRegressionBanner mode).
///
/// Contract declared per ADR 0034 (per-adapter a11y harness):
/// <code>
/// new SunfishA11yContract
/// {
///     Name    = "System requirement regression detected",
///     Role    = "alert",                              // implicit aria-live="assertive" (C3)
///     KeyboardMap  = [],
///     Focus   = { Initial = "none", Trap = false },   // per C5: no FocusAsync (SC 3.2.1)
///     LiveRegion   = LiveRegionPoliteness.Assertive,  // via role="alert"
///     ReducedMotion = "respects",                     // no animations per ADR 0034
///     RtlIconMirror = "non-directional",              // ⚠ is a non-directional Unicode glyph
///     Wcag22Conformant = ["1.3.1","1.3.2","3.2.1","4.1.3"],
/// }
/// </code>
///
/// Verified via source-file structural inspection per the Phase 3 precedent.
/// </summary>
public sealed class SystemRequirementsRegressionBannerA11yTests
{
    // ── Test 1: role="alert" (implicit assertive) per SC 4.1.3 / C3 amendment ──

    [Fact]
    public void RegressionBanner_HasAlertRoleForAssertiveAnnouncement()
    {
        var source = LocateBannerSourceOrFail();
        var markup = File.ReadAllText(source);

        // C3: role="alert" carries implicit aria-live="assertive" per ARIA 1.2.
        // Explicit aria-live="assertive" was dropped to prevent NVDA+Firefox double-announcement.
        Assert.True(
            markup.Contains("role=\"alert\""),
            $"SystemRequirementsRegressionBanner.razor must declare role=\"alert\" " +
            $"per WCAG 2.2 SC 4.1.3 (assertive live-region for regression announcements). " +
            $"Source: {source}");
    }

    // ── Test 2: no explicit aria-live="assertive" in non-comment markup (C3) ──

    [Fact]
    public void RegressionBanner_NoRedundantAriaLiveAssertive()
    {
        var source = LocateBannerSourceOrFail();
        var raw = File.ReadAllText(source);
        // Build the complement of all @* ... *@ Razor comment blocks.
        // Razor does NOT support nested block comments, so splitting by @* / *@
        // boundary pairs covers 100% of the comment surface for this file type.
        var sb = new System.Text.StringBuilder();
        int pos = 0;
        foreach (System.Text.RegularExpressions.Match m in
            Regex.Matches(raw, @"@\*.*?\*@", RegexOptions.Singleline))
        {
            sb.Append(raw, pos, m.Index - pos);
            pos = m.Index + m.Length;
        }
        sb.Append(raw, pos, raw.Length - pos);
        var nonCommentMarkup = sb.ToString();

        Assert.False(
            nonCommentMarkup.Contains("aria-live=\"assertive\""),
            $"SystemRequirementsRegressionBanner.razor must NOT declare explicit " +
            $"aria-live=\"assertive\" in template markup — this causes double-announcement " +
            $"on NVDA+Firefox per WCAG council C3 amendment (role=\"alert\" already carries " +
            $"the implicit assertive behaviour per ARIA 1.2 §5.3.2). Source: {source}");
    }

    // ── Test 3: polite additions-only list region for incremental items (C4) ──

    [Fact]
    public void RegressionBanner_HasPoliteAdditionsOnlyListRegion()
    {
        var source = LocateBannerSourceOrFail();
        var markup = File.ReadAllText(source);

        Assert.True(
            markup.Contains("aria-live=\"polite\"") &&
            markup.Contains("aria-relevant=\"additions\""),
            $"SystemRequirementsRegressionBanner.razor must declare a secondary " +
            $"aria-live=\"polite\" + aria-relevant=\"additions\" region for the regression " +
            $"list per WCAG council C4 amendment — subsequent regressions are announced " +
            $"incrementally rather than re-reading the full list (ARIA 1.2 §aria-atomic). " +
            $"Source: {source}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string LocateBannerSourceOrFail()
    {
        const int MaxDepth = 12;
        var visited = new System.Collections.Generic.List<string>();
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (int depth = 0; depth < MaxDepth && current is not null; depth++)
        {
            var candidate = Path.Combine(
                current.FullName,
                "accelerators", "anchor", "Components",
                "SystemRequirementsRegressionBanner.razor");
            if (File.Exists(candidate)) return candidate;
            visited.Add(current.FullName);
            current = current.Parent;
        }
        throw new InvalidOperationException(
            $"Could not locate SystemRequirementsRegressionBanner.razor after walking {MaxDepth} " +
            $"levels up from '{AppContext.BaseDirectory}'. Visited: {string.Join(", ", visited)}");
    }
}
