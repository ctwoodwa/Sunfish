using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Sunfish.UIAdapters.Blazor.A11y;

/// <summary>
/// Optional axe.run() configuration. Maps directly to the axe-core 4.x `RunOptions`
/// JS object: <see href="https://github.com/dequelabs/axe-core/blob/develop/doc/API.md#options-parameter"/>.
/// </summary>
public sealed class AxeOptions
{
    /// <summary>WCAG / best-practice tags to scope the rule set.
    /// Default: <c>wcag2a / wcag2aa / wcag21a / wcag21aa / wcag22aa / best-practice</c>.</summary>
    public IReadOnlyList<string> Tags { get; init; } = new[]
    {
        "wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa", "best-practice",
    };

    /// <summary>Optional rule overrides (rule id → enabled).</summary>
    public IReadOnlyDictionary<string, bool> RuleOverrides { get; init; } = new Dictionary<string, bool>();

    /// <summary>Theme stylesheet URL or inline CSS to inject before running axe.
    /// Required for color-contrast rules. Pass <c>null</c> to skip theme injection.</summary>
    public string? ThemeCss { get; init; }
}

/// <summary>
/// Runs axe-core against a markup fragment by wrapping it in a full HTML5 document,
/// hosting it in a Playwright page, injecting axe-core, and deserialising the result.
/// Returned <see cref="AxeResult"/> can be filtered by <see cref="AxeImpact"/> for
/// moderate+ violations. Decoupled from bUnit so any markup source can drive it
/// (callers typically pass <c>renderedComponent.Markup</c>).
/// </summary>
public static class AxeRunner
{
    /// <summary>
    /// Resolves to the axe-core JS bundle path. Set via environment variable
    /// <c>SUNFISH_AXE_CORE_PATH</c> in CI, or auto-discovered from pnpm's nested
    /// node_modules during local dev.
    /// </summary>
    public static string AxeCorePath { get; set; } = ResolveAxeCorePath();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Run axe-core against the markup fragment. Wraps <paramref name="markup"/> in a
    /// full HTML doc, loads it via <see cref="IPage.SetContentAsync"/>, injects axe-core,
    /// runs the rule set, and returns the typed result.
    /// </summary>
    public static async Task<AxeResult> RunAxeAsync(
        string markup,
        IPage page,
        AxeOptions? options = null)
    {
        if (markup is null) throw new ArgumentNullException(nameof(markup));
        if (page is null) throw new ArgumentNullException(nameof(page));

        options ??= new AxeOptions();

        var fullHtml = WrapInHtmlDocument(markup, options.ThemeCss);
        await page.SetContentAsync(fullHtml).ConfigureAwait(false);

        if (!File.Exists(AxeCorePath))
        {
            throw new InvalidOperationException(
                $"axe-core JS bundle not found at '{AxeCorePath}'. " +
                "Set SUNFISH_AXE_CORE_PATH environment variable or place axe.min.js " +
                "where AxeRunner.AxeCorePath points.");
        }

        await page.AddScriptTagAsync(new PageAddScriptTagOptions { Path = AxeCorePath }).ConfigureAwait(false);

        var optionsJson = SerializeAxeOptions(options);
        var resultJson = await page.EvaluateAsync<JsonElement>(
            $"async () => await axe.run(document, {optionsJson})").ConfigureAwait(false);

        return JsonSerializer.Deserialize<AxeResult>(resultJson.GetRawText(), JsonOptions)
            ?? new AxeResult();
    }

    /// <summary>
    /// Wrap a fragment of HTML markup in a minimal full HTML5 document with optional
    /// theme CSS so axe's color-contrast rule has a real document to evaluate.
    /// </summary>
    /// <remarks>
    /// The wrapper is a11y-clean by construction: it sets <c>lang</c>, includes a
    /// <c>&lt;main&gt;</c> landmark to satisfy <c>landmark-one-main</c> + <c>region</c>
    /// rules, and gives the document a non-empty <c>&lt;title&gt;</c>. Callers can pass a
    /// non-default <paramref name="lang"/> when testing locale-specific scenarios; the
    /// fragment under test sits inside the <c>&lt;main&gt;</c>.
    /// </remarks>
    public static string WrapInHtmlDocument(string fragmentMarkup, string? themeCss = null, string lang = "en")
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine($"<html lang=\"{lang}\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<title>Sunfish a11y harness fragment</title>");
        if (!string.IsNullOrEmpty(themeCss))
        {
            // Caller may pass either inline CSS or a URL. Heuristic: starts with http(s):// → URL.
            if (themeCss!.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                themeCss.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"<link rel=\"stylesheet\" href=\"{themeCss}\">");
            }
            else
            {
                sb.AppendLine($"<style>{themeCss}</style>");
            }
        }
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        // <main> landmark satisfies axe's landmark-one-main + region rules so the
        // wrapper itself never contributes violations to the fragment under test.
        sb.AppendLine("<main>");
        sb.AppendLine(fragmentMarkup);
        sb.AppendLine("</main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string SerializeAxeOptions(AxeOptions options)
    {
        var dict = new Dictionary<string, object>
        {
            ["runOnly"] = new Dictionary<string, object>
            {
                ["type"] = "tag",
                ["values"] = options.Tags.ToArray(),
            },
        };
        if (options.RuleOverrides.Count > 0)
        {
            dict["rules"] = options.RuleOverrides.ToDictionary(
                kv => kv.Key,
                kv => (object)new Dictionary<string, bool> { ["enabled"] = kv.Value });
        }
        return JsonSerializer.Serialize(dict);
    }

    private static string ResolveAxeCorePath()
    {
        // 1. Environment variable wins.
        var envPath = Environment.GetEnvironmentVariable("SUNFISH_AXE_CORE_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath)) return envPath;

        // 2. Walk up from the current working directory looking for pnpm's nested store.
        var current = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10 && !string.IsNullOrEmpty(current); i++)
        {
            var pnpmDir = Path.Combine(current, "node_modules", ".pnpm");
            if (Directory.Exists(pnpmDir))
            {
                var match = Directory.EnumerateDirectories(pnpmDir, "axe-core@*").FirstOrDefault();
                if (match is not null)
                {
                    var path = Path.Combine(match, "node_modules", "axe-core", "axe.min.js");
                    if (File.Exists(path)) return path;
                }
            }
            current = Path.GetDirectoryName(current);
        }

        // 3. Sentinel — caller will see a clear error if axe-core is missing at runtime.
        return Path.Combine("axe.min.js");
    }
}
