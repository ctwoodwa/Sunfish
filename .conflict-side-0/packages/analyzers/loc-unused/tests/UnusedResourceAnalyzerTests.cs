using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Sunfish.Analyzers.LocUnused;
using Xunit;

namespace Sunfish.Analyzers.LocUnused.Tests;

public class UnusedResourceAnalyzerTests
{
    private const string TwoKeyResx = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="Greeting" xml:space="preserve">
            <value>Hello</value>
            <comment>Dashboard greeting.</comment>
          </data>
          <data name="Farewell" xml:space="preserve">
            <value>Goodbye</value>
            <comment>Logout banner.</comment>
          </data>
        </root>
        """;

    [Fact]
    public async Task EmitsDiagnostic_WhenResourceKeyHasNoReference()
    {
        // Source references neither key — both should be flagged.
        var source = """
            using System;
            class Demo
            {
                public void M() { Console.WriteLine("nothing localized"); }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source, TwoKeyResx, "Resources.resx");

        Assert.Equal(2, diagnostics.Count);
        var messages = diagnostics.Select(d => d.GetMessage()).ToList();
        Assert.Contains(messages, m => m.Contains("'Greeting'"));
        Assert.Contains(messages, m => m.Contains("'Farewell'"));
        Assert.All(diagnostics, d => Assert.Equal(UnusedResourceAnalyzer.DiagnosticId, d.Id));
    }

    [Fact]
    public async Task NoDiagnostic_WhenKeyReferencedViaIndexer()
    {
        // Both keys referenced via the localizer["Key"] indexer pattern.
        var source = """
            using Microsoft.Extensions.Localization;
            class Demo
            {
                private readonly IStringLocalizer<Demo> localizer;
                public Demo(IStringLocalizer<Demo> loc) { localizer = loc; }
                public string A() => localizer["Greeting"];
                public string B() => localizer["Farewell"];
            }
            namespace Microsoft.Extensions.Localization
            {
                public interface IStringLocalizer<T>
                {
                    string this[string name] { get; }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source, TwoKeyResx, "Resources.resx");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenKeyReferencedViaGetString()
    {
        // Both keys referenced via the .GetString("Key") method-call pattern.
        var source = """
            using Microsoft.Extensions.Localization;
            class Demo
            {
                private readonly IStringLocalizer<Demo> localizer;
                public Demo(IStringLocalizer<Demo> loc) { localizer = loc; }
                public string A() => localizer.GetString("Greeting");
                public string B() => localizer.GetString("Farewell", "arg1");
            }
            namespace Microsoft.Extensions.Localization
            {
                public interface IStringLocalizer<T>
                {
                    string GetString(string name, params object[] args);
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source, TwoKeyResx, "Resources.resx");
        Assert.Empty(diagnostics);
    }

    private static async Task<List<Diagnostic>> RunAnalyzerAsync(string source, string resxContent, string resxFileName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var additional = new InMemoryAdditionalText(resxFileName, resxContent);

        var analyzer = new UnusedResourceAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(additional)));

        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        return diagnostics
            .Where(d => d.Id == UnusedResourceAnalyzer.DiagnosticId)
            .ToList();
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly string _content;
        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _content = content;
        }
        public override string Path { get; }
        public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default) =>
            SourceText.From(_content);
    }
}
