using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Sunfish.Analyzers.LocComments
{
    /// <summary>
    /// Sunfish.I18n.001 — emits a warning for every .resx data entry missing a
    /// translator-context comment (or with an empty comment).
    /// </summary>
    /// <remarks>
    /// Reads .resx files via the AdditionalFiles compilation input. Consumers add
    /// every .resx as an &lt;AdditionalFiles Include="..."&gt; in their .csproj (or use the
    /// Sunfish loc-cascade .props that does this automatically — Plan 2 Task 4.3 follow-up).
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ResxCommentAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SUNFISH_I18N_001";

        internal static readonly DiagnosticDescriptor Rule = new(
            id: DiagnosticId,
            title: "Resource entry missing translator comment",
            messageFormat: "RESX entry '{0}' has no <comment> — translators need context to localize correctly",
            category: "Sunfish.I18n",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description:
                "Sunfish localization quality requires every .resx <data> entry to carry a <comment> that gives translators the context they need (audience, domain, surrounding UI). Missing or empty comments are the leading cause of mistranslation. See spec §8 + ADR 0034.",
            helpLinkUri: "https://github.com/ctwoodwa/Sunfish/blob/main/docs/diagnostic-codes.md#sunfish_i18n_001",
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationAction(AnalyzeCompilation);
        }

        private static void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            foreach (var additional in context.Options.AdditionalFiles)
            {
                if (!IsResxPath(additional.Path)) continue;

                var text = additional.GetText(context.CancellationToken);
                if (text is null) continue;

                IEnumerable<(string name, int approxLine)> entries;
                try
                {
                    entries = ParseResxEntriesMissingComment(text);
                }
                catch
                {
                    // Malformed RESX — skip silently. Build will surface separate XML errors.
                    continue;
                }

                foreach (var (name, line) in entries)
                {
                    var lineSpan = text.Lines.Count > line
                        ? text.Lines[line].Span
                        : new TextSpan(0, 0);
                    var location = Location.Create(additional.Path, lineSpan,
                        new LinePositionSpan(
                            new LinePosition(line, 0),
                            new LinePosition(line, lineSpan.Length)));
                    context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
                }
            }
        }

        private static bool IsResxPath(string path) =>
            !string.IsNullOrEmpty(path) &&
            path.EndsWith(".resx", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Parse a RESX text and return (name, approxLineNumber) for every &lt;data&gt; element
        /// missing a non-empty &lt;comment&gt; child. Lines are 0-indexed for Roslyn LinePosition.
        /// </summary>
        internal static IEnumerable<(string name, int approxLine)> ParseResxEntriesMissingComment(SourceText text)
        {
            var raw = text.ToString();
            using var reader = new StringReader(raw);
            var doc = XDocument.Load(reader, LoadOptions.SetLineInfo);
            if (doc.Root is null) yield break;

            foreach (var data in doc.Root.Elements("data"))
            {
                var name = data.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(name)) continue;

                // Skip non-string entries (binary blobs, ResXFileRef, typed values).
                var typeAttr = data.Attribute("type")?.Value;
                if (!string.IsNullOrEmpty(typeAttr)) continue;

                var comment = data.Element("comment")?.Value;
                if (!string.IsNullOrWhiteSpace(comment)) continue;

                var line = (data as IXmlLineInfo)?.LineNumber - 1 ?? 0;
                yield return (name!, line);
            }
        }
    }
}
