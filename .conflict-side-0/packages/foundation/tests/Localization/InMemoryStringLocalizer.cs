using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Sunfish.Foundation.Tests.Localization;

/// <summary>
/// Minimal <see cref="IStringLocalizer{T}"/> backed by an in-memory culture→key→pattern map.
/// Picks patterns by <see cref="CultureInfo.CurrentUICulture"/>, falling back to neutral
/// language (e.g., `ar-SA` → `ar` → invariant) then to the key name if unresolved. Used by
/// <see cref="SunfishLocalizerSmartFormatTests"/> so the tests exercise SmartFormat pattern
/// evaluation, not ASP.NET Core's RESX path-resolution machinery.
/// </summary>
public sealed class InMemoryStringLocalizer<T> : IStringLocalizer<T>
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _patternsByCulture;

    public InMemoryStringLocalizer(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> patternsByCulture)
    {
        _patternsByCulture = patternsByCulture ?? throw new ArgumentNullException(nameof(patternsByCulture));
    }

    public LocalizedString this[string name]
    {
        get
        {
            var pattern = Resolve(name);
            return pattern is null
                ? new LocalizedString(name, name, resourceNotFound: true)
                : new LocalizedString(name, pattern);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var pattern = Resolve(name);
            return pattern is null
                ? new LocalizedString(name, name, resourceNotFound: true)
                : new LocalizedString(name, string.Format(CultureInfo.CurrentUICulture, pattern, arguments));
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var cultureName = CultureInfo.CurrentUICulture.Name;
        if (_patternsByCulture.TryGetValue(cultureName, out var cultureMap))
        {
            foreach (var kvp in cultureMap)
            {
                yield return new LocalizedString(kvp.Key, kvp.Value);
            }
        }
    }

    private string? Resolve(string key)
    {
        var culture = CultureInfo.CurrentUICulture;
        while (culture is not null && !string.IsNullOrEmpty(culture.Name))
        {
            if (_patternsByCulture.TryGetValue(culture.Name, out var map) && map.TryGetValue(key, out var pattern))
            {
                return pattern;
            }
            culture = culture.Parent;
        }
        if (_patternsByCulture.TryGetValue(string.Empty, out var invariant) && invariant.TryGetValue(key, out var invariantPattern))
        {
            return invariantPattern;
        }
        return null;
    }
}
