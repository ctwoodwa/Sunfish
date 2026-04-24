namespace Sunfish.Foundation.Localization;

/// <summary>
/// CLDR-aware localizer wrapping <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/>.
/// Supports CLDR plural rules (Arabic six-form, Japanese/Chinese zero-form), gender variants,
/// and locale-aware number/date formatting via SmartFormat.NET + .NET System.Globalization.
/// </summary>
/// <remarks>
/// Implementation backs onto SmartFormat.NET's PluralLocalizationFormatter for plural/select
/// logic per the ICU4N pivot recorded in waves/global-ux/decisions.md (2026-04-25). The public
/// contract is independent of the formatter choice so a later swap back to ICU MessageFormat
/// implementation does not ripple into callers.
/// </remarks>
public interface ISunfishLocalizer<T>
{
    /// <summary>Simple key lookup. Returns the pattern unmodified.</summary>
    string Get(string key);

    /// <summary>
    /// Formatted key lookup. The stored pattern is evaluated by SmartFormat under the
    /// current UI culture; <paramref name="args"/> supplies named placeholders.
    /// </summary>
    /// <param name="key">Resource key.</param>
    /// <param name="args">Object whose public properties become named placeholders, or a
    /// <see cref="System.Collections.Generic.IDictionary{TKey,TValue}"/> of string to object.</param>
    string Format(string key, object args);

    /// <summary>
    /// Plural-form key lookup. Shortcut for the common `{count:plural:...}` pattern.
    /// The <paramref name="count"/> value is bound to the `count` named placeholder.
    /// Additional named placeholders may be supplied via <paramref name="additionalArgs"/>.
    /// </summary>
    string Plural(string key, long count, object? additionalArgs = null);
}
