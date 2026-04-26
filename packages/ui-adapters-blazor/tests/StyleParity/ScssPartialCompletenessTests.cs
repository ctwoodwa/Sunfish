// =============================================================================
// SYNTHESIS Theme 2 — Phase 3 prevention test
// =============================================================================
// Codifies the fix strategy from icm/07_review/output/style-audits/SYNTHESIS.md
// Theme 2 ("Empty/stub SCSS partials under one or more providers"):
//
//   "Add a build-time check that no `*.scss` partial contains only a TODO comment."
//
// Background: the Material `_dialog.scss` and `_date-picker.scss` partials
// shipped as 5-line TODO stubs at audit time — the SunfishDialog and
// SunfishCalendar rendered as unstyled browser primitives under the Material
// skin in production. Phase 1B authored both partials end-to-end
// (~298 + ~446 lines respectively). This test guards against that exact
// regression on the GuardedPartials list, plus anything we add to it as
// future Material/Fluent components are authored. We deliberately do NOT
// run the check across BS5 partials wholesale, because BS5 commonly leans
// on Bootstrap's own primitives for whole component families and ships
// intentionally-thin bridge partials documenting that delegation — those
// are documentation comments, not the regression Theme 2 targets.
// =============================================================================

using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.StyleParity;

public class ScssPartialCompletenessTests
{
    // Provider-relative partials whose component cannot rely on a host-framework
    // primitive (BS5 leans heavily on Bootstrap; M3 / Fluent v9 must author
    // their own surface). Each entry is "<Provider>/<partial-filename>" and
    // MUST contain at least one CSS rule body — never a TODO stub.
    //
    // To extend: add the partial here when its corresponding component is
    // authored under a skin that doesn't have a host-framework fallback.
    public static IEnumerable<object[]> GuardedPartials()
    {
        // Theme 2 root cause cases — both were 5-line TODO stubs at audit time.
        yield return new object[] { "Material/_dialog.scss" };
        yield return new object[] { "Material/_date-picker.scss" };
        // Material Calendar surrounds the date-picker; it ships under date-picker.scss.
        // The SunfishCalendar component compiles into the date-picker partial path.
    }

    [Theory]
    [MemberData(nameof(GuardedPartials))]
    public void GuardedPartial_HasCssRulesAndIsNotATodoStub(string providerRelativePath)
    {
        var path = ResolvePartialPath(providerRelativePath);
        Assert.True(File.Exists(path), $"Guarded SCSS partial not found at {path}.");

        var raw = File.ReadAllText(path);
        var stripped = StripCommentsAndWhitespace(raw);

        Assert.True(
            HasCssRule(stripped),
            $"SYNTHESIS Theme 2: '{providerRelativePath}' contains no CSS rule blocks. "
            + "This partial covers a Sunfish component that has no host-framework primitive to fall back on; "
            + "shipping it without rules causes the component to render unstyled.");

        Assert.False(
            LooksLikeTodoStub(raw),
            $"SYNTHESIS Theme 2: '{providerRelativePath}' looks like a TODO/stub. "
            + "Author the partial end-to-end before shipping (see SYNTHESIS.md Theme 2 for the spec).");
    }

    private static bool LooksLikeTodoStub(string raw)
    {
        // A "TODO stub" is a partial that ALL of:
        //   1. Has zero CSS rule blocks (no `{ ... }` syntax in code).
        //   2. Carries a forward-looking marker token — TODO, FIXME, HACK,
        //      Phase/Batch markers, or "placeholder ... yet"-style language.
        //
        // We deliberately do NOT treat a `// Placeholder — no Bootstrap bridge
        // styles defined yet` documentation comment as a stub when paired with
        // an explanation that the framework handles the surface natively. The
        // marker has to be a *promise* of work, not documentation of
        // intentional delegation. Heuristic: require an actual "TODO" /
        // "FIXME" token on its own (case-insensitive) AND no rule blocks.
        var stripped = StripCommentsAndWhitespace(raw);
        if (HasCssRule(stripped)) return false;

        // Match TODO / FIXME / HACK as standalone tokens (word boundaries)
        // anywhere in the file. Doc-comment "Placeholder" alone is allowed.
        return Regex.IsMatch(raw, @"\b(TODO|FIXME|HACK)\b", RegexOptions.IgnoreCase);
    }

    private static bool HasCssRule(string stripped)
    {
        // A rule block requires at least one `{` followed (eventually) by a `}`
        // around a non-empty body. We accept any opening brace as evidence, since
        // SCSS nested rules and at-rules (@media, @supports, @keyframes) all use
        // `{ ... }`.
        return stripped.IndexOf('{') >= 0;
    }

    private static string StripCommentsAndWhitespace(string raw)
    {
        // Remove `// line comments` and `/* block comments */`, then trim.
        var noBlock = Regex.Replace(raw, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        var noLine = Regex.Replace(noBlock, @"//[^\n]*", string.Empty);
        return Regex.Replace(noLine, @"\s+", string.Empty);
    }

    private static string ResolvePartialPath(string providerRelativePath)
    {
        var dir = AppContext.BaseDirectory;
        var info = new DirectoryInfo(dir);
        while (info != null && !Directory.Exists(Path.Combine(info.FullName, "packages", "ui-adapters-blazor")))
        {
            info = info.Parent;
        }
        Assert.NotNull(info);

        var parts = providerRelativePath.Split('/', '\\');
        Assert.Equal(2, parts.Length);
        var provider = parts[0];
        var fileName = parts[1];

        return Path.Combine(
            info!.FullName,
            "packages", "ui-adapters-blazor", "Providers", provider,
            "Styles", "components", fileName);
    }
}
