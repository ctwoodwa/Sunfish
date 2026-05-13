using System;
using System.IO;
using Sunfish.UIAdapters.Blazor.A11y;
using Xunit;

namespace Sunfish.Anchor.Tests.A11y;

/// <summary>
/// W#47 Phase 4 — WCAG 2.2 AA + EN 301 549 v3.2.1 structural a11y assertions for
/// <c>SystemRequirementsInlinePanel.razor</c> (PostInstallInlineExplanation mode).
///
/// Contract declared per ADR 0034 (per-adapter a11y harness):
/// <code>
/// new SunfishA11yContract
/// {
///     Name    = "System requirements summary",
///     Role    = "region",
///     KeyboardMap  = [{ keys: ["Enter", "Space"], action: "toggle-details" }],
///     Focus   = { Initial = "none", Trap = false },
///     LiveRegion   = LiveRegionPoliteness.Off,   // no live region in inline mode
///     ReducedMotion = "respects",
///     RtlIconMirror = "non-directional",
///     Wcag22Conformant = ["1.1.1","1.3.1","2.1.1","2.4.7","2.5.8","4.1.2"],
/// }
/// </code>
///
/// Verified via source-file structural inspection per the Phase 3 precedent.
/// </summary>
public sealed class SystemRequirementsInlinePanelA11yTests
{
    // ── Test 1: fail-badge non-text character has text alternative (SC 1.1.1) ──

    [Fact]
    public void InlinePanel_FailBadgeHasAccessibleLabel()
    {
        var source = LocateRazorSourceOrFail("SystemRequirementsInlinePanel.razor");
        var markup = File.ReadAllText(source);

        // Verify the key appears within an aria-label attribute context (not merely in a comment).
        // Razor attribute values can contain nested quotes: aria-label="@L["key"]", so we match
        // aria-label followed (on the same or adjacent line) by the localization key.
        // [^>]* stops at the element boundary; Singleline allows cross-line match within element.
        Assert.True(
            System.Text.RegularExpressions.Regex.IsMatch(
                markup,
                @"aria-label[^>]*sysreq\.inline\.requirement_not_met",
                System.Text.RegularExpressions.RegexOptions.Singleline),
            $"SystemRequirementsInlinePanel.razor must declare aria-label=\"@L[\"sysreq.inline." +
            $"requirement_not_met\"]\" on the fail badge per WCAG 2.2 SC 1.1.1 " +
            $"(non-text characters need a text alternative — key must appear in attribute " +
            $"context, not only in a comment). Source: {source}");
    }

    // ── Test 2: panel has no assertive live region or role="alert" (liveRegion: Off) ──

    [Fact]
    public void InlinePanel_HasNoAssertiveLiveRegion()
    {
        var source = LocateRazorSourceOrFail("SystemRequirementsInlinePanel.razor");
        var markup = File.ReadAllText(source);

        // role="alert" carries implicit aria-live="assertive" per ARIA 1.2 §5.3.2,
        // so both the explicit attribute AND the implicit role must be absent.
        Assert.False(
            markup.Contains("aria-live=\"assertive\""),
            $"SystemRequirementsInlinePanel.razor must NOT declare aria-live=\"assertive\": " +
            $"inline explanation is not an interruption-worthy announcement per ADR 0063-A1.1 " +
            $"PostInstallInlineExplanation contract (liveRegion: Off). Source: {source}");

        Assert.False(
            markup.Contains("role=\"alert\""),
            $"SystemRequirementsInlinePanel.razor must NOT use role=\"alert\" — that role " +
            $"carries implicit aria-live=\"assertive\" per ARIA 1.2 §5.3.2, which violates the " +
            $"liveRegion: Off contract for the inline explanation mode. Source: {source}");
    }

    // ── Test 3: fallback detail strings present for Pass + Fail outcomes (SC 4.1.2) ─

    [Fact]
    public void InlinePanel_FallbackDetailStringsPresent()
    {
        var source = LocateRazorSourceOrFail("SystemRequirementsInlinePanel.razor");
        var markup = File.ReadAllText(source);

        Assert.True(
            markup.Contains("sysreq.inline.detail_fail_default") &&
            markup.Contains("sysreq.inline.detail_pass_default"),
            $"SystemRequirementsInlinePanel.razor must reference both sysreq.inline.detail_fail_default " +
            $"and sysreq.inline.detail_pass_default fallback strings per SC 4.1.2 (Name, Role, Value) — " +
            $"every dimension row must convey its state in text when DimensionEvaluation.Detail is null. " +
            $"Source: {source}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string LocateRazorSourceOrFail(string fileName)
    {
        const int MaxDepth = 12;
        var visited = new System.Collections.Generic.List<string>();
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (int depth = 0; depth < MaxDepth && current is not null; depth++)
        {
            var candidate = Path.Combine(
                current.FullName, "accelerators", "anchor", "Components", fileName);
            if (File.Exists(candidate)) return candidate;
            visited.Add(current.FullName);
            current = current.Parent;
        }
        throw new InvalidOperationException(
            $"Could not locate {fileName} after walking {MaxDepth} levels up from " +
            $"'{AppContext.BaseDirectory}'. Visited: {string.Join(", ", visited)}");
    }
}
