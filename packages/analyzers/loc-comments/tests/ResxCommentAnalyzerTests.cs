using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Sunfish.Analyzers.LocComments;
using Xunit;

namespace Sunfish.Analyzers.LocComments.Tests;

public class ResxCommentAnalyzerTests
{
    [Fact]
    public async Task NoDiagnostic_WhenAllEntriesHaveComments()
    {
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Greeting" xml:space="preserve">
                <value>Hello</value>
                <comment>Greeting on the dashboard.</comment>
              </data>
              <data name="Farewell" xml:space="preserve">
                <value>Goodbye</value>
                <comment>Logout banner.</comment>
              </data>
            </root>
            """;

        var diagnostics = await RunAnalyzerAsync(resx, "Resources.resx");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task EmitsDiagnostic_WhenCommentMissing()
    {
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Greeting" xml:space="preserve">
                <value>Hello</value>
              </data>
            </root>
            """;

        var diagnostics = await RunAnalyzerAsync(resx, "Resources.resx");
        Assert.Single(diagnostics);
        Assert.Equal(ResxCommentAnalyzer.DiagnosticId, diagnostics[0].Id);
        Assert.Contains("Greeting", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task EmitsDiagnostic_WhenCommentEmpty()
    {
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Greeting" xml:space="preserve">
                <value>Hello</value>
                <comment>   </comment>
              </data>
            </root>
            """;

        var diagnostics = await RunAnalyzerAsync(resx, "Resources.resx");
        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task EmitsMultipleDiagnostics_OneEntryPerMissingComment()
    {
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="A" xml:space="preserve"><value>a</value></data>
              <data name="B" xml:space="preserve"><value>b</value><comment>has it</comment></data>
              <data name="C" xml:space="preserve"><value>c</value></data>
              <data name="D" xml:space="preserve"><value>d</value></data>
            </root>
            """;

        var diagnostics = await RunAnalyzerAsync(resx, "Resources.resx");
        Assert.Equal(3, diagnostics.Count);
        var names = diagnostics.Select(d => d.GetMessage()).ToList();
        Assert.Contains(names, m => m.Contains("'A'"));
        Assert.Contains(names, m => m.Contains("'C'"));
        Assert.Contains(names, m => m.Contains("'D'"));
        Assert.DoesNotContain(names, m => m.Contains("'B'"));
    }

    [Fact]
    public async Task IgnoresTypedDataEntries_BinaryBlobsAndFileRefs()
    {
        // Binary / ResXFileRef entries have a 'type' attribute and aren't translator-facing strings.
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Logo" type="System.Byte[], mscorlib" mimetype="application/x-microsoft.net.object.bytearray.base64">
                <value>AAAA</value>
              </data>
              <data name="Greeting" xml:space="preserve">
                <value>Hello</value>
              </data>
            </root>
            """;

        var diagnostics = await RunAnalyzerAsync(resx, "Resources.resx");
        Assert.Single(diagnostics);
        Assert.Contains("Greeting", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task IgnoresNonResxAdditionalFiles()
    {
        var json = """{ "key": "value" }""";

        var diagnostics = await RunAnalyzerAsync(json, "config.json");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task SilentlySkips_WhenResxIsMalformedXml()
    {
        var resx = "<root><data name='unclosed' ></root>";

        var diagnostics = await RunAnalyzerAsync(resx, "Broken.resx");
        Assert.Empty(diagnostics);
    }

    private static async Task<List<Diagnostic>> RunAnalyzerAsync(string fileContent, string fileName)
    {
        // Construct a minimal compilation with the .resx as an AdditionalFile.
        var syntaxTree = CSharpSyntaxTree.ParseText("class _ {}");
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var additional = new InMemoryAdditionalText(fileName, fileContent);

        var analyzer = new ResxCommentAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(additional)));

        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        return diagnostics
            .Where(d => d.Id == ResxCommentAnalyzer.DiagnosticId)
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
