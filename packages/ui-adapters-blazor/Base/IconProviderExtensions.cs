using Microsoft.AspNetCore.Components;
using Sunfish.Foundation.Enums;
using Sunfish.UICore.Contracts;

namespace Sunfish.UIAdapters.Blazor.Base;

/// <summary>
/// Blazor-side extensions over <see cref="ISunfishIconProvider"/>.
/// </summary>
/// <remarks>
/// <see cref="ISunfishIconProvider.GetIcon"/> returns a raw HTML string because the
/// contract is framework-agnostic (React, Angular, Vue, and plain-JS consumers all
/// need a raw string they can pass through their own safe-render primitive). In
/// Razor, however, <c>@stringExpression</c> HTML-encodes by default — rendering
/// the raw SVG as visible text instead of as DOM. This extension returns a
/// <see cref="MarkupString"/> so Razor skips encoding and the SVG renders as real
/// markup. Use <c>@IconProvider.GetIconMarkup(name, size)</c> from any Razor file;
/// never <c>@IconProvider.GetIcon(name, size)</c>, which is the bug.
/// </remarks>
public static class IconProviderExtensions
{
    /// <summary>
    /// Returns the icon markup pre-wrapped as a <see cref="MarkupString"/> so Razor
    /// renders it as HTML rather than HTML-encoding it.
    /// </summary>
    /// <param name="provider">The icon provider resolved from DI.</param>
    /// <param name="name">The logical icon name (e.g., "home", "settings").</param>
    /// <param name="size">The desired icon size. Defaults to <see cref="IconSize.Medium"/>.</param>
    /// <returns>A <see cref="MarkupString"/> containing the icon's SVG markup.</returns>
    public static MarkupString GetIconMarkup(
        this ISunfishIconProvider provider,
        string name,
        IconSize size = IconSize.Medium)
        => new(provider.GetIcon(name, size));
}
