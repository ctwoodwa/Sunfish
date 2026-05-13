using System;
using System.IO;
using Sunfish.UIAdapters.Blazor.A11y;
using Xunit;

namespace Sunfish.Anchor.Tests.A11y;

/// <summary>
/// W#47 Phase 4 — WCAG 2.2 AA + EN 301 549 v3.2.1 structural a11y assertions for
/// <c>SystemRequirements.razor</c> (PreInstallFullPage mode).
///
/// Contract declared per ADR 0034 (per-adapter a11y harness):
/// <code>
/// new SunfishA11yContract
/// {
///     Name    = "System requirements",
///     Role    = "main",
///     KeyboardMap = [{ keys: ["Tab"], action: "navigate-rows" }],
///     Focus   = { Initial = "none", Trap = false },
///     LiveRegion   = LiveRegionPoliteness.Polite,   // single polite verdict region (B5)
///     ReducedMotion = "respects",
///     RtlIconMirror = "non-directional",            // ✓ / ⚠ / ✗ are non-directional glyphs
///     Wcag22Conformant = ["1.3.1","2.1.1","2.4.3","2.4.7","2.5.8","4.1.3"],
/// }
/// </code>
///
/// Verified via source-file structural inspection per the Phase 3 precedent.
/// Full browser axe-core run deferred to CI Playwright gate (ADR 0034 §Phase-3 integration).
/// </summary>
public sealed class SystemRequirementsPreInstallFullPageA11yTests
{
    // ── Test 1: page landmark + labelling (SC 1.3.1 / 2.4.6) ───────────────

    [Fact]
    public void PreInstallPage_HasMainLandmarkWithAriaLabelledBy()
    {
        var source = LocateRazorSourceOrFail("SystemRequirements.razor");
        var markup = File.ReadAllText(source);

        Assert.True(
            markup.Contains("role=\"main\""),
            $"SystemRequirements.razor must declare role=\"main\" per SC 1.3.6 landmark " +
            $"(ensures AT users can navigate directly to the page body). Source: {source}");

        Assert.True(
            markup.Contains("aria-labelledby="),
            $"SystemRequirements.razor role=\"main\" landmark must have aria-labelledby " +
            $"pointing at the h1 heading per SC 2.4.6. Source: {source}");
    }

    // ── Test 2: single polite live region covers loading→verdict (SC 4.1.3 / B5) ─

    [Fact]
    public void PreInstallPage_HasSinglePoliteStatusRegion()
    {
        var source = LocateRazorSourceOrFail("SystemRequirements.razor");
        var markup = File.ReadAllText(source);

        Assert.True(
            markup.Contains("role=\"status\""),
            $"SystemRequirements.razor must declare role=\"status\" on the verdict live region " +
            $"per WCAG 2.2 SC 4.1.3 (status messages must be programmatically determinable). " +
            $"Source: {source}");

        Assert.True(
            markup.Contains("aria-live=\"polite\""),
            $"The verdict live region must use aria-live=\"polite\" (not assertive) per WCAG " +
            $"council B5+B6 fix — assertive on the verdict region would interrupt at-task " +
            $"announcements. Source: {source}");

        Assert.True(
            markup.Contains("aria-atomic=\"true\""),
            $"The polite verdict region must declare aria-atomic=\"true\" so the full verdict " +
            $"string is re-announced on transitions (SC 4.1.3). Source: {source}");
    }

    // ── Test 3: dimension list accessible label (SC 1.3.1 / B9 amendment) ──

    [Fact]
    public void PreInstallPage_DimensionListHasAriaLabel()
    {
        var source = LocateRazorSourceOrFail("SystemRequirements.razor");
        var markup = File.ReadAllText(source);

        Assert.True(
            markup.Contains("sysreq.dim_list.label"),
            $"SystemRequirements.razor must apply a localized aria-label on the dimension " +
            $"<ul> via the sysreq.dim_list.label key per WCAG council B9 fix (SC 1.3.1 " +
            $"— list must have a programmatic label). Source: {source}");
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
                current.FullName, "accelerators", "anchor", "Components", "Pages", fileName);
            if (File.Exists(candidate)) return candidate;
            visited.Add(current.FullName);
            current = current.Parent;
        }
        throw new InvalidOperationException(
            $"Could not locate {fileName} after walking {MaxDepth} levels up from " +
            $"'{AppContext.BaseDirectory}'. Visited: {string.Join(", ", visited)}");
    }
}
