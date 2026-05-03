using Sunfish.Foundation.Base;
using Sunfish.UICore.Contracts;
using Sunfish.Foundation.Services;
using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Base;

/// <summary>
/// Base class for all Sunfish components, providing shared infrastructure such as
/// CSS class/style composition, RTL awareness, ARIA attribute helpers, and disposal.
/// </summary>
public abstract class SunfishComponentBase : ComponentBase, IDisposable
{
    /// <summary>Gets the active CSS provider, injected by the DI container.</summary>
    [Inject] protected ISunfishCssProvider CssProvider { get; set; } = default!;

    /// <summary>Gets the active icon provider, injected by the DI container.</summary>
    [Inject] protected ISunfishIconProvider IconProvider { get; set; } = default!;

    /// <summary>Gets the active theme service, injected by the DI container.</summary>
    [Inject] protected ISunfishThemeService ThemeService { get; set; } = default!;

    /// <summary>
    /// Additional CSS class names to append to the component's root element.
    /// </summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>
    /// Inline style string to append to the component's root element.
    /// </summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>
    /// Captures any unmatched HTML attributes and renders them on the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// Reusable builder for composing CSS class strings from multiple sources.
    /// </summary>
    protected CssClassBuilder ClassBuilder { get; } = new();

    /// <summary>
    /// Reusable builder for composing inline style strings from multiple sources.
    /// </summary>
    protected StyleBuilder StyleBuilder { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the current theme uses right-to-left layout direction.
    /// </summary>
    protected bool IsRtl => ThemeService.IsRtl;

    /// <summary>
    /// Combines a provider-supplied base CSS class with any consumer-supplied <see cref="Class"/> value.
    /// </summary>
    /// <param name="baseClass">The CSS class returned by the active <see cref="ISunfishCssProvider"/>.</param>
    /// <returns>A single space-separated class string.</returns>
    protected string CombineClasses(string baseClass)
    {
        return ClassBuilder.Clear()
            .AddClass(baseClass)
            .AddClass(Class)
            .Build();
    }

    /// <summary>
    /// Combines multiple provider-supplied base CSS classes with any consumer-supplied <see cref="Class"/> value.
    /// Null/empty entries are skipped.
    /// </summary>
    /// <param name="baseClasses">The CSS classes to combine, typically returned by the active <see cref="ISunfishCssProvider"/>.</param>
    /// <returns>A single space-separated class string.</returns>
    protected string CombineClasses(params string?[] baseClasses)
    {
        ClassBuilder.Clear();
        foreach (var c in baseClasses)
        {
            ClassBuilder.AddClass(c);
        }
        return ClassBuilder.AddClass(Class).Build();
    }

    /// <summary>
    /// Combines an optional provider-supplied base style with any consumer-supplied <see cref="Style"/> value.
    /// </summary>
    /// <param name="baseStyle">An optional inline style string to prepend.</param>
    /// <returns>A semicolon-separated style string.</returns>
    protected string CombineStyles(string? baseStyle = null)
    {
        return StyleBuilder.Clear()
            .AddStyle(baseStyle)
            .AddStyle(Style)
            .Build();
    }

    /// <summary>
    /// Combines multiple provider-supplied base styles with any consumer-supplied <see cref="Style"/> value.
    /// Null/empty entries are skipped.
    /// </summary>
    /// <param name="baseStyles">The inline style strings to combine.</param>
    /// <returns>A semicolon-separated style string.</returns>
    protected string CombineStyles(params string?[] baseStyles)
    {
        StyleBuilder.Clear();
        foreach (var s in baseStyles)
        {
            StyleBuilder.AddStyle(s);
        }
        return StyleBuilder.AddStyle(Style).Build();
    }

    /// <summary>
    /// Sets an <c>aria-*</c> attribute on the component's root element via <see cref="AdditionalAttributes"/>.
    /// </summary>
    /// <param name="key">The ARIA attribute name without the <c>aria-</c> prefix.</param>
    /// <param name="value">The attribute value. If <c>null</c>, the attribute is not set.</param>
    protected void SetAria(string key, object? value)
    {
        if (value != null)
        {
            AdditionalAttributes ??= new Dictionary<string, object>();
            AdditionalAttributes[$"aria-{key}"] = value;
        }
    }

    private bool _disposed;

    /// <summary>
    /// Releases all resources used by the component. Implements the dispose pattern.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Dispose(disposing: true);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override in derived classes to release managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>; <c>false</c> if called from a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
