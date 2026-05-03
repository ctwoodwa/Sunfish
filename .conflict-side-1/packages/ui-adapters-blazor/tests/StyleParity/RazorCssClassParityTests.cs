// =============================================================================
// SYNTHESIS Theme 1 — Phase 3 prevention test
// =============================================================================
// Codifies the fix strategy from icm/07_review/output/style-audits/SYNTHESIS.md
// Theme 1 ("Razor emits one class set, CSS styles another"):
//
//   "(b) add a parity test that renders each component under each provider and
//    asserts no emitted class is unmatched by a CSS rule"
//
// Approach: rather than rendering each component (which requires the entire
// bUnit + provider DI pipeline), this test takes the curated list of
// historically-broken Sunfish-namespaced (`sf-*`) classes that Theme 1 called
// out as "dead CSS" — i.e. emitted by Razor but never matched by a CSS rule —
// and asserts each one IS present as a selector in every provider's CSS bundle.
// This guards against re-introducing the dead-CSS cascade on the four components
// audited (SunfishCalendar, SunfishDataGrid, SunfishDialog, SunfishButton)
// across the three first-party skins (Bootstrap 5, Fluent UI v9, Material 3).
//
// Allowlist: when a class is intentionally only styled by some skins, list it
// in `OptionalPerProvider` so the test still catches the universal-failure
// pattern but tolerates skin-specific opt-outs (recorded with rationale).
// =============================================================================

using System.IO;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.StyleParity;

public class RazorCssClassParityTests
{
    // Classes from SYNTHESIS Theme 1 that were dead-CSS at audit time. Each
    // MUST now appear as a selector in every first-party provider's CSS bundle.
    public static IEnumerable<object[]> RequiredClasses()
    {
        // Calendar — BS5 + Fluent dead-CSS cascade was the largest cluster.
        // Phase 1A added these to all three provider CSS bundles via BEM rewrite.
        yield return new object[] { "sf-calendar__cell--today" };
        yield return new object[] { "sf-calendar__cell--selected" };
        yield return new object[] { "sf-calendar__cell--other-month" };
        yield return new object[] { "sf-calendar__cell--disabled" };
        yield return new object[] { "sf-calendar__cell--in-range" };
        yield return new object[] { "sf-calendar__cell--range-start" };
        yield return new object[] { "sf-calendar__cell--range-end" };
        yield return new object[] { "sf-calendar__cell--range-edge" };
        yield return new object[] { "sf-calendar__cell--focused" };

        // Dialog — slot vocabulary per ADR 0023. Razor emits provider-routed
        // classes via DialogContentClass()/HeaderClass()/etc., but the shared
        // BEM hooks on the close button and root container must exist.
        yield return new object[] { "sf-dialog__close" };
        yield return new object[] { "sf-dialog--draggable" };

        // DataGrid — historically Telerik/MAR-prefixed selectors had no rules.
        // The emit names have since been re-aligned to BEM `sf-datagrid__*`.
        yield return new object[] { "sf-datagrid__cmd-btn" };
        yield return new object[] { "sf-datagrid__col--locked-end" };

        // Button — icon slot, emitted by SunfishButton/SunfishToggleButton/
        // SunfishSplitButton when an Icon parameter is supplied.
        yield return new object[] { "sf-button__icon" };
    }

    // Per-provider opt-outs for classes that a specific skin intentionally does
    // not style (e.g., relies on a parent rule, or maps to a framework primitive).
    // Entry shape: (className, providerKey).
    private static readonly HashSet<(string Class, string Provider)> OptionalPerProvider = new()
    {
        // Bootstrap delegates draggability to .modal CSS chrome plus Sunfish's
        // bridge SCSS in _bridge-overlays / _dialog. The compiled bundle does
        // include `.sf-dialog--draggable` via _dialog.scss; the entry here is
        // the documented escape-hatch shape, currently empty.
    };

    [Theory]
    [MemberData(nameof(RequiredClasses))]
    public void RequiredClass_IsPresentInBootstrapCss(string className) =>
        AssertClassPresent(className, ProviderKey: "Bootstrap");

    [Theory]
    [MemberData(nameof(RequiredClasses))]
    public void RequiredClass_IsPresentInFluentUiCss(string className) =>
        AssertClassPresent(className, ProviderKey: "FluentUI");

    [Theory]
    [MemberData(nameof(RequiredClasses))]
    public void RequiredClass_IsPresentInMaterialCss(string className) =>
        AssertClassPresent(className, ProviderKey: "Material");

    private static void AssertClassPresent(string className, string ProviderKey)
    {
        if (OptionalPerProvider.Contains((className, ProviderKey))) return;

        var cssPath = ResolveProviderCssPath(ProviderKey);
        Assert.True(File.Exists(cssPath), $"Provider CSS bundle not found at {cssPath}.");

        var css = File.ReadAllText(cssPath);

        // We look for any selector containing `.<className>` with a non-class
        // boundary on either side — this avoids matching `sf-foo` inside
        // `sf-foo-bar`. The dot prefix anchors to a CSS class selector.
        var pattern = $".{className}";
        var matchPositions = FindClassSelectorOccurrences(css, pattern);

        Assert.True(
            matchPositions > 0,
            $"Provider '{ProviderKey}' CSS bundle does not contain selector .{className}. "
            + "SYNTHESIS Theme 1: this class is emitted by a SunfishComponent's Razor and was historically "
            + "left unstyled. Either add a rule that targets it, or add (className, provider) to OptionalPerProvider.");
    }

    private static int FindClassSelectorOccurrences(string css, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = css.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            // Ensure the next char after the match is not a class-name continuation
            // (i.e. not a letter, digit, hyphen, or underscore). Selectors can be
            // followed by `,` ` ` `:` `{` `>` `+` `~` `[` `.`.
            var endIdx = index + needle.Length;
            if (endIdx >= css.Length)
            {
                count++; break;
            }
            var next = css[endIdx];
            var isClassNameContinuation = char.IsLetterOrDigit(next) || next == '-' || next == '_';
            if (!isClassNameContinuation)
            {
                count++;
            }
            index = endIdx;
        }
        return count;
    }

    private static string ResolveProviderCssPath(string providerKey)
    {
        // Test working directory is the test bin output; walk up to the repo
        // root and resolve the provider's wwwroot/css folder.
        var dir = AppContext.BaseDirectory;
        var info = new DirectoryInfo(dir);
        while (info != null && !Directory.Exists(Path.Combine(info.FullName, "packages", "ui-adapters-blazor")))
        {
            info = info.Parent;
        }
        Assert.NotNull(info);

        var bundleName = providerKey switch
        {
            "Bootstrap" => "sunfish-bootstrap.css",
            "FluentUI" => "sunfish-fluentui.css",
            "Material" => "sunfish-material.css",
            _ => throw new ArgumentOutOfRangeException(nameof(providerKey)),
        };

        return Path.Combine(
            info!.FullName,
            "packages", "ui-adapters-blazor", "Providers", providerKey,
            "wwwroot", "css", bundleName);
    }
}
