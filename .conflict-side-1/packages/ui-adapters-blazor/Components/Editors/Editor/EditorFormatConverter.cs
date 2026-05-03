using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.UIAdapters.Blazor.Components.Editors;

/// <summary>
/// Converts editor content between HTML and another format. Register
/// implementations via DI; the editor resolves them at runtime by format name.
/// </summary>
public interface IEditorFormatConverter
{
    /// <summary>The format this converter handles (e.g., "markdown", "plaintext").</summary>
    string Format { get; }

    /// <summary>Convert from this format to HTML.</summary>
    string ToHtml(string content);

    /// <summary>Convert from HTML to this format.</summary>
    string FromHtml(string html);
}

/// <summary>
/// Markdown ↔ HTML converter backed by Markdig (MIT). Markdown → HTML is
/// full-fidelity via Markdig's advanced extensions pipeline. HTML → Markdown
/// is a best-effort conversion via basic tag stripping — full round-trip
/// fidelity is not guaranteed (this is the "bounded adapter" boundary).
/// </summary>
internal sealed class MarkdownFormatConverter : IEditorFormatConverter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string Format => "markdown";

    public string ToHtml(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        return Markdown.ToHtml(content, Pipeline);
    }

    public string FromHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        return BasicHtmlToMarkdown.Convert(html);
    }
}

/// <summary>
/// Plaintext ↔ HTML converter. Import wraps lines in &lt;p&gt; tags;
/// export strips all HTML tags. No external dependencies.
/// </summary>
internal sealed class PlainTextFormatConverter : IEditorFormatConverter
{
    public string Format => "plaintext";

    public string ToHtml(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        var lines = content.Split('\n');
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');
            sb.Append("<p>").Append(System.Net.WebUtility.HtmlEncode(trimmed)).Append("</p>");
        }
        return sb.ToString();
    }

    public string FromHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        // Replace block-level closing tags with newlines, then strip all tags
        var text = Regex.Replace(html, @"</(?:p|div|br|h[1-6]|li|tr)>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text);
        return text.Trim();
    }
}

/// <summary>
/// Basic HTML → Markdown converter. Handles common block and inline elements.
/// Not a full-fidelity round-trip — complex HTML (tables, nested lists) may
/// lose formatting. This is the "bounded adapter" boundary documented in the
/// human decision (2026-04-09).
/// </summary>
internal static class BasicHtmlToMarkdown
{
    public static string Convert(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        var text = html;

        // Block elements → Markdown equivalents
        text = Regex.Replace(text, @"<h1[^>]*>(.*?)</h1>", "# $1\n\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<h2[^>]*>(.*?)</h2>", "## $1\n\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<h3[^>]*>(.*?)</h3>", "### $1\n\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<h4[^>]*>(.*?)</h4>", "#### $1\n\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<h5[^>]*>(.*?)</h5>", "##### $1\n\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<h6[^>]*>(.*?)</h6>", "###### $1\n\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Inline elements
        text = Regex.Replace(text, @"<strong[^>]*>(.*?)</strong>", "**$1**", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<b[^>]*>(.*?)</b>", "**$1**", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<em[^>]*>(.*?)</em>", "*$1*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<i[^>]*>(.*?)</i>", "*$1*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<code[^>]*>(.*?)</code>", "`$1`", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Links and images
        text = Regex.Replace(text, @"<a[^>]*href=""([^""]*?)""[^>]*>(.*?)</a>", "[$2]($1)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<img[^>]*src=""([^""]*?)""[^>]*alt=""([^""]*?)""[^>]*/?>", "![$2]($1)", RegexOptions.IgnoreCase);

        // Line breaks and paragraphs
        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</p>\s*", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<p[^>]*>", "", RegexOptions.IgnoreCase);

        // List items (basic — unordered only)
        text = Regex.Replace(text, @"<li[^>]*>(.*?)</li>", "- $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Strip remaining tags
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);

        // Decode entities
        text = System.Net.WebUtility.HtmlDecode(text);

        // Normalize whitespace
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }
}

/// <summary>
/// DI registration extensions for SunfishEditor format converters.
/// </summary>
public static class SunfishEditorServiceExtensions
{
    /// <summary>
    /// Registers the Markdown format converter for SunfishEditor import/export.
    /// Uses Markdig (MIT) for Markdown → HTML conversion. HTML → Markdown uses
    /// a basic built-in converter (best-effort, not full round-trip fidelity).
    /// </summary>
    public static IServiceCollection AddSunfishEditorMarkdownSupport(
        this IServiceCollection services)
    {
        services.AddSingleton<IEditorFormatConverter, MarkdownFormatConverter>();
        return services;
    }

    /// <summary>
    /// Registers the plaintext format converter for SunfishEditor import/export.
    /// Converts between plain text and HTML via simple tag wrapping/stripping.
    /// No external dependencies.
    /// </summary>
    public static IServiceCollection AddSunfishEditorPlainTextSupport(
        this IServiceCollection services)
    {
        services.AddSingleton<IEditorFormatConverter, PlainTextFormatConverter>();
        return services;
    }
}
