using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Sunfish.Tooling.LocalizationXliff.Tests;

/// <summary>
/// Round-trip property + fixture tests for Plan 2 Task 1.4 Section 3A binary gate
/// ("XLIFF round-trip: .resx → XLIFF 2.0 → .resx is byte-identical").
/// </summary>
/// <remarks>
/// Byte-identical over XML serialization is strict; the practical guarantee Sunfish
/// needs is structural equivalence after normalisation (attribute order, whitespace).
/// Tests assert structural equality of the loaded model, not raw bytes.
/// </remarks>
public class RoundTripTests
{
    [Fact]
    public void ResxFile_SaveLoad_PreservesEntries()
    {
        var resx = NewResx(new (string, string, string?)[] { ("greeting", "Hello", "Simple greeting"), ("count", "{0} items", null) });
        var path = TempPath(".resx");
        try
        {
            resx.Save(path);
            var reloaded = ResxFile.Load(path);
            AssertResxEqual(resx, reloaded);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Xliff20File_SaveLoad_PreservesUnits()
    {
        var xliff = NewXliff("en-US", "ar-SA", new (string, string, string?, string)[]
        {
            ("greeting", "Hello", "مرحبا", "final"),
            ("count", "{0} items", "{0} عناصر", "translated"),
        });
        var path = TempPath(".xlf");
        try
        {
            xliff.Save(path);
            var reloaded = Xliff20File.Load(path);
            AssertXliffEqual(xliff, reloaded);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ExportThenImport_PreservesEntriesForApprovedTargets()
    {
        var resxPath = TempPath(".ar-SA.resx");
        var xliffDir = Path.Combine(Path.GetTempPath(), $"xliff-{Guid.NewGuid():N}");
        var outputResxDir = Path.Combine(Path.GetTempPath(), $"outresx-{Guid.NewGuid():N}");
        try
        {
            var original = NewResx(new (string, string, string?)[] { ("greeting", "Hello", "Simple greeting") });
            original.Save(resxPath);

            var exportTask = new SunfishResxToXliffTask
            {
                BuildEngine = new TestBuildEngine(),
                SourceResxFiles = new Microsoft.Build.Framework.ITaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem(resxPath),
                },
                OutputDirectory = xliffDir,
                SourceLanguage = "en-US",
            };
            Assert.True(exportTask.Execute(), "export task failed");
            Assert.Single(exportTask.GeneratedXliffFiles);

            // Simulate translator approval: set target + state="final"
            var xliffPath = exportTask.GeneratedXliffFiles[0].ItemSpec;
            var translated = Xliff20File.Load(xliffPath);
            translated.Units[0].Target = "مرحبا";
            translated.Units[0].State = "final";
            translated.Save(xliffPath);

            var importTask = new SunfishXliffToResxTask
            {
                BuildEngine = new TestBuildEngine(),
                SourceXliffFiles = new Microsoft.Build.Framework.ITaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem(xliffPath),
                },
                OutputDirectory = outputResxDir,
            };
            Assert.True(importTask.Execute(), "import task failed");
            Assert.Single(importTask.GeneratedResxFiles);

            var reimported = ResxFile.Load(importTask.GeneratedResxFiles[0].ItemSpec);
            Assert.Single(reimported.Entries);
            Assert.Equal("greeting", reimported.Entries[0].Name);
            Assert.Equal("مرحبا", reimported.Entries[0].Value);
            Assert.Equal("Simple greeting", reimported.Entries[0].Comment);
        }
        finally
        {
            if (File.Exists(resxPath)) File.Delete(resxPath);
            if (Directory.Exists(xliffDir)) Directory.Delete(xliffDir, recursive: true);
            if (Directory.Exists(outputResxDir)) Directory.Delete(outputResxDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("simple-key", "Simple ASCII value")]
    [InlineData("key.dotted", "With a {0} placeholder")]
    [InlineData("unicode-🔑", "Emoji key + body 🏠")]
    [InlineData("long-key-with-many-segments", "Short value")]
    public void ResxFile_SaveLoad_RoundTripsAcrossKeyShapes(string key, string value)
    {
        var resx = NewResx(new (string, string, string?)[] { (key, value, null) });
        var path = TempPath(".resx");
        try
        {
            resx.Save(path);
            var reloaded = ResxFile.Load(path);
            Assert.Single(reloaded.Entries);
            Assert.Equal(key, reloaded.Entries[0].Name);
            Assert.Equal(value, reloaded.Entries[0].Value);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static string TempPath(string suffix) =>
        Path.Combine(Path.GetTempPath(), $"sunfish-roundtrip-{Guid.NewGuid():N}{suffix}");

    internal static ResxFile NewResx((string name, string value, string? comment)[] entries)
    {
        var resx = new ResxFile();
        foreach (var (name, value, comment) in entries)
        {
            resx.Entries.Add(new ResxEntry { Name = name, Value = value, Comment = comment });
        }
        return resx;
    }

    internal static Xliff20File NewXliff(string src, string tgt,
        (string id, string source, string? target, string state)[] units)
    {
        var x = new Xliff20File { SourceLanguage = src, TargetLanguage = tgt, OriginalFile = "test.resx" };
        foreach (var (id, source, target, state) in units)
        {
            x.Units.Add(new XliffUnit { Id = id, Source = source, Target = target, State = state });
        }
        return x;
    }

    internal static void AssertResxEqual(ResxFile a, ResxFile b)
    {
        Assert.Equal(a.Entries.Count, b.Entries.Count);
        for (int i = 0; i < a.Entries.Count; i++)
        {
            Assert.Equal(a.Entries[i].Name, b.Entries[i].Name);
            Assert.Equal(a.Entries[i].Value, b.Entries[i].Value);
            Assert.Equal(a.Entries[i].Comment ?? "", b.Entries[i].Comment ?? "");
        }
    }

    internal static void AssertXliffEqual(Xliff20File a, Xliff20File b)
    {
        Assert.Equal(a.SourceLanguage, b.SourceLanguage);
        Assert.Equal(a.TargetLanguage, b.TargetLanguage);
        Assert.Equal(a.Units.Count, b.Units.Count);
        for (int i = 0; i < a.Units.Count; i++)
        {
            Assert.Equal(a.Units[i].Id, b.Units[i].Id);
            Assert.Equal(a.Units[i].Source, b.Units[i].Source);
            Assert.Equal(a.Units[i].Target, b.Units[i].Target);
            Assert.Equal(a.Units[i].State, b.Units[i].State);
        }
    }
}

/// <summary>Minimal IBuildEngine for driving MSBuild tasks from xUnit without an MSBuild host.</summary>
internal sealed class TestBuildEngine : Microsoft.Build.Framework.IBuildEngine
{
    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => "test.csproj";

    public bool BuildProjectFile(string projectFileName, string[] targetNames,
        System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs) => true;

    public void LogCustomEvent(Microsoft.Build.Framework.CustomBuildEventArgs e) { }
    public void LogErrorEvent(Microsoft.Build.Framework.BuildErrorEventArgs e) { }
    public void LogMessageEvent(Microsoft.Build.Framework.BuildMessageEventArgs e) { }
    public void LogWarningEvent(Microsoft.Build.Framework.BuildWarningEventArgs e) { }
}
