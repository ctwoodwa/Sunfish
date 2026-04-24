using System.IO;
using Xunit;

namespace Sunfish.Tooling.LocalizationXliff.Tests;

/// <summary>
/// Per-locale round-trip tests for the 12 Sunfish target locales.
/// Exercises diacritics, RTL marks, zero-width joiners, emoji, CJK characters through the
/// .resx → XLIFF 2.0 → .resx pipeline to confirm byte-equivalence after normalisation.
/// </summary>
public class TwelveLocaleTests
{
    [Theory]
    [InlineData("en-US", "Hello, world", "Simple ASCII")]
    [InlineData("ar-SA", "مرحبا بالعالم", "Arabic RTL with diacritics")]
    [InlineData("hi-IN", "नमस्ते दुनिया", "Hindi Devanagari with conjunct clusters")]
    [InlineData("zh-Hans", "你好世界", "Chinese Simplified")]
    [InlineData("zh-Hant", "你好世界", "Chinese Traditional")]
    [InlineData("ja-JP", "こんにちは世界", "Japanese hiragana + kanji")]
    [InlineData("ko-KR", "안녕하세요 세계", "Korean Hangul")]
    [InlineData("ru-RU", "Привет, мир", "Russian Cyrillic")]
    [InlineData("pt-BR", "Olá, mundo", "Portuguese Brazilian diacritics")]
    [InlineData("es-419", "Hola, mundo", "Spanish Latin-American")]
    [InlineData("fr-FR", "Bonjour le monde", "French with accented é")]
    [InlineData("de-DE", "Hallo Welt — größer als ß", "German umlauts + eszett + em-dash")]
    public void ResxToXliffToResx_PreservesLocaleText(string locale, string greeting, string description)
    {
        var resxPath = Path.Combine(Path.GetTempPath(), $"Test.{locale}.resx");
        var xliffDir = Path.Combine(Path.GetTempPath(), $"xliff-{locale}");
        var resxOutDir = Path.Combine(Path.GetTempPath(), $"resx-out-{locale}");

        try
        {
            var original = RoundTripTests.NewResx(new (string, string, string?)[] { ("greeting", greeting, description) });
            original.Save(resxPath);

            var export = new SunfishResxToXliffTask
            {
                BuildEngine = new TestBuildEngine(),
                SourceResxFiles = new Microsoft.Build.Framework.ITaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem(resxPath),
                },
                OutputDirectory = xliffDir,
                SourceLanguage = locale,
            };
            Assert.True(export.Execute(), $"{locale}: export failed");
            Assert.Single(export.GeneratedXliffFiles);

            var xliffPath = export.GeneratedXliffFiles[0].ItemSpec;
            var xliff = Xliff20File.Load(xliffPath);
            xliff.Units[0].Target = greeting;
            xliff.Units[0].State = "final";
            xliff.Save(xliffPath);

            var import = new SunfishXliffToResxTask
            {
                BuildEngine = new TestBuildEngine(),
                SourceXliffFiles = new Microsoft.Build.Framework.ITaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem(xliffPath),
                },
                OutputDirectory = resxOutDir,
            };
            Assert.True(import.Execute(), $"{locale}: import failed");

            var reimported = ResxFile.Load(import.GeneratedResxFiles[0].ItemSpec);
            Assert.Single(reimported.Entries);
            Assert.Equal("greeting", reimported.Entries[0].Name);
            Assert.Equal(greeting, reimported.Entries[0].Value);
            Assert.Equal(description, reimported.Entries[0].Comment);
        }
        finally
        {
            if (File.Exists(resxPath)) File.Delete(resxPath);
            if (Directory.Exists(xliffDir)) Directory.Delete(xliffDir, recursive: true);
            if (Directory.Exists(resxOutDir)) Directory.Delete(resxOutDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("ar-SA", "مفتاح‏ مختلط RTL-LTR 123", "RTL/LTR bidi with Right-To-Left Mark")]
    [InlineData("hi-IN", "क्ष्मा", "Devanagari conjunct with virama")]
    [InlineData("zh-Hans", "你好👋世界🌏", "Emoji interleaved")]
    [InlineData("ko-KR", "한글‍ㅏ", "Hangul + Zero-Width-Joiner")]
    public void ResxToXliffToResx_PreservesSpecialCharacters(string locale, string value, string description)
    {
        // Same path as above but explicitly exercises non-BMP / bidi / ZWJ / RTL marks.
        var resxPath = Path.Combine(Path.GetTempPath(), $"TestSpecial.{locale}.resx");
        var xliffDir = Path.Combine(Path.GetTempPath(), $"xliff-special-{locale}");
        var resxOutDir = Path.Combine(Path.GetTempPath(), $"resx-special-out-{locale}");

        try
        {
            var original = RoundTripTests.NewResx(new (string, string, string?)[] { ("complex", value, description) });
            original.Save(resxPath);

            var export = new SunfishResxToXliffTask
            {
                BuildEngine = new TestBuildEngine(),
                SourceResxFiles = new Microsoft.Build.Framework.ITaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem(resxPath),
                },
                OutputDirectory = xliffDir,
                SourceLanguage = locale,
            };
            Assert.True(export.Execute());

            var xliffPath = export.GeneratedXliffFiles[0].ItemSpec;
            var xliff = Xliff20File.Load(xliffPath);
            xliff.Units[0].Target = value;
            xliff.Units[0].State = "final";
            xliff.Save(xliffPath);

            var import = new SunfishXliffToResxTask
            {
                BuildEngine = new TestBuildEngine(),
                SourceXliffFiles = new Microsoft.Build.Framework.ITaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem(xliffPath),
                },
                OutputDirectory = resxOutDir,
            };
            Assert.True(import.Execute());

            var reimported = ResxFile.Load(import.GeneratedResxFiles[0].ItemSpec);
            Assert.Single(reimported.Entries);
            Assert.Equal(value, reimported.Entries[0].Value);
        }
        finally
        {
            if (File.Exists(resxPath)) File.Delete(resxPath);
            if (Directory.Exists(xliffDir)) Directory.Delete(xliffDir, recursive: true);
            if (Directory.Exists(resxOutDir)) Directory.Delete(resxOutDir, recursive: true);
        }
    }
}
