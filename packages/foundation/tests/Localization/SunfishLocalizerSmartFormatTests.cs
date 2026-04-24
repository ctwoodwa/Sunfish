using System.Globalization;
using Sunfish.Foundation.Localization;
using Xunit;

namespace Sunfish.Foundation.Tests.Localization;

/// <summary>
/// Week 1 Section 3A smoke tests for the SunfishLocalizer SmartFormat.NET wrapper.
/// Three scenarios validate the CLDR plural-rule pivot from ICU4N to SmartFormat:
///   1. English simple string — pattern passed through unchanged (baseline)
///   2. Arabic six-form plural — zero, one, two, few, many, other all resolve correctly
///   3. Japanese single-form — every count maps to the same "other" form (CLDR `other` only)
/// Passing all three proves SmartFormat's PluralLocalizationFormatter handles the locales
/// the ICU4N pivot was motivated by (Arabic plural refinements post-CLDR-32).
/// </summary>
public class SunfishLocalizerSmartFormatTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Patterns =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en-US"] = new Dictionary<string, string>
            {
                ["greeting"] = "Hello, world",
                ["inbox.unread"] = "{count:plural:1 message|{} messages}",
            },
            ["ar-SA"] = new Dictionary<string, string>
            {
                ["greeting"] = "مرحبا بالعالم",
                ["inbox.unread"] =
                    "{count:plural:" +
                    "لا رسائل|" +
                    "رسالة واحدة|" +
                    "رسالتان|" +
                    "{} رسائل|" +
                    "{} رسالة|" +
                    "{} رسالة}",
            },
            ["ja"] = new Dictionary<string, string>
            {
                ["greeting"] = "こんにちは世界",
                ["inbox.unread"] = "{count:plural:{} 件のメッセージ}",
            },
        };

    private static SunfishLocalizer<TestResource> CreateLocalizer(string culture)
    {
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        var inner = new InMemoryStringLocalizer<TestResource>(Patterns);
        return new SunfishLocalizer<TestResource>(inner);
    }

    [Fact]
    public void English_SimpleString_ReturnsSourceValue()
    {
        var loc = CreateLocalizer("en-US");
        Assert.Equal("Hello, world", loc.Get("greeting"));
    }

    [Fact]
    public void Arabic_PluralSix_ReturnsCorrectForms()
    {
        var loc = CreateLocalizer("ar-SA");
        // CLDR Arabic plural rules:
        //   n = 0                  → zero
        //   n = 1                  → one
        //   n = 2                  → two
        //   n % 100 = 3..10        → few
        //   n % 100 = 11..99       → many
        //   otherwise              → other
        Assert.Equal("لا رسائل", loc.Plural("inbox.unread", 0));
        Assert.Equal("رسالة واحدة", loc.Plural("inbox.unread", 1));
        Assert.Equal("رسالتان", loc.Plural("inbox.unread", 2));
        Assert.Equal("3 رسائل", loc.Plural("inbox.unread", 3));
        Assert.Equal("11 رسالة", loc.Plural("inbox.unread", 11));
        Assert.Equal("100 رسالة", loc.Plural("inbox.unread", 100));
    }

    [Fact]
    public void Japanese_SingleForm_ReturnsSameFormForAllCounts()
    {
        var loc = CreateLocalizer("ja");
        // CLDR Japanese plural rules: only `other` — every count uses the same form.
        Assert.Equal("0 件のメッセージ", loc.Plural("inbox.unread", 0));
        Assert.Equal("1 件のメッセージ", loc.Plural("inbox.unread", 1));
        Assert.Equal("5 件のメッセージ", loc.Plural("inbox.unread", 5));
    }
}
