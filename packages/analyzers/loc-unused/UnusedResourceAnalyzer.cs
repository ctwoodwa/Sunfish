using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Sunfish.Analyzers.LocUnused
{
    /// <summary>
    /// Sunfish.I18n.002 — emits a warning for every .resx data entry whose key has no
    /// consuming reference in the same package's .cs / .razor source files.
    /// </summary>
    /// <remarks>
    /// Reads .resx files via the AdditionalFiles compilation input. For each <c>&lt;data
    /// name="X"&gt;</c> entry, scans every C# syntax tree in the compilation for references
    /// of the form <c>localizer["X"]</c> (indexer) or <c>localizer.GetString("X")</c>. A
    /// resource key with zero matches is reported as unused.
    /// <para>
    /// The "same package" boundary is implicit: each csproj has its own compilation, so
    /// the analyzer's view of source code is naturally scoped to the project that owns
    /// the .resx. Razor (.razor) source compiles to .cs syntax trees in the same compilation
    /// (Razor source generator), so razor-side references are picked up via the compiled
    /// tree without a separate file scan.
    /// </para>
    /// <para>
    /// Severity is Error. Previously Warning, which only failed the build because every
    /// Sunfish project sets TreatWarningsAsErrors=true in Directory.Build.props — making
    /// the gate implicit on a build-wide flag. Promoted here to match Plan 5 §"CI gates"
    /// and mirror the SUNFISH_I18N_001 cascade pattern (PR #75): the diagnostic now blocks
    /// builds independently of warnings-as-errors policy.
    /// </para>
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnusedResourceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SUNFISH_I18N_002";

        internal static readonly DiagnosticDescriptor Rule = new(
            id: DiagnosticId,
            title: "Unused localized resource",
            messageFormat: "Resource '{0}' in {1} is not referenced from any .cs or .razor file in this package — consider removing or wiring up",
            category: "Localization",
            // Plan 5 promotion: Error severity. Previously Warning, which only
            // failed the build because every Sunfish project sets
            // TreatWarningsAsErrors=true in Directory.Build.props — making the
            // gate implicit on a build-wide flag. Promoting here mirrors the
            // SUNFISH_I18N_001 cascade (PR #75) so the diagnostic is Error
            // regardless of warnings-as-errors policy.
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description:
                "Sunfish localization quality requires every .resx <data> entry to have at least one consuming reference. Orphaned entries waste translator effort and inflate locale bundles. The analyzer detects keys with zero matches against IStringLocalizer indexer / GetString call patterns in the compilation's C# source. See Plan 5 §analyzer-package and ADR 0034.",
            helpLinkUri: "https://github.com/ctwoodwa/Sunfish/blob/main/packages/analyzers/loc-unused/README.md",
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
            // Step 1: collect every (resourceKey, sourceFile, line) from .resx AdditionalFiles.
            var resxEntries = new List<(string key, string path, int line)>();
            foreach (var additional in context.Options.AdditionalFiles)
            {
                if (!IsResxPath(additional.Path)) continue;

                var text = additional.GetText(context.CancellationToken);
                if (text is null) continue;

                IEnumerable<(string name, int approxLine)> entries;
                try
                {
                    entries = ParseResxEntries(text);
                }
                catch
                {
                    // Malformed RESX — skip silently. Build will surface separate XML errors.
                    continue;
                }

                foreach (var (name, line) in entries)
                {
                    resxEntries.Add((name, additional.Path, line));
                }
            }

            if (resxEntries.Count == 0) return;

            // Step 2: aggregate every C# source text in the compilation into a single haystack.
            // For the package boundary: the compilation IS the package's own csproj output, so
            // including all syntax-tree text limits the search to the same package automatically.
            var haystack = string.Concat(context.Compilation.SyntaxTrees
                .Select(t => t.GetText(context.CancellationToken).ToString()));

            // Step 3: for each resx key, check if any reference pattern matches.
            foreach (var (key, path, line) in resxEntries)
            {
                if (IsKeyReferenced(haystack, key)) continue;

                var sourceText = context.Options.AdditionalFiles
                    .First(f => f.Path == path)
                    .GetText(context.CancellationToken);

                var lineSpan = sourceText is not null && sourceText.Lines.Count > line
                    ? sourceText.Lines[line].Span
                    : new TextSpan(0, 0);

                var location = Location.Create(path, lineSpan,
                    new LinePositionSpan(
                        new LinePosition(line, 0),
                        new LinePosition(line, lineSpan.Length)));

                var fileName = Path.GetFileName(path);
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, key, fileName));
            }
        }

        private static bool IsResxPath(string path) =>
            !string.IsNullOrEmpty(path) &&
            path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Detect a usage of <paramref name="key"/> via either of the two canonical
        /// IStringLocalizer access patterns:
        /// <list type="bullet">
        ///   <item><c>localizer["KeyName"]</c> — indexer access</item>
        ///   <item><c>localizer.GetString("KeyName"[, args])</c> — method call</item>
        /// </list>
        /// Both patterns require a quoted string literal containing exactly the key. Patterns
        /// like <c>nameof(Resources.KeyName)</c> are not matched (rare in practice for
        /// localizer call sites; if a real consumer adopts that pattern, add a third regex).
        /// </summary>
        internal static bool IsKeyReferenced(string haystack, string key)
        {
            var escapedKey = Regex.Escape(key);

            // Pattern A: indexer — [...]"key"...]
            // We look for: ["key"]  or  ["key" ,  …]  optionally with whitespace.
            // Quoted with " (interpolation `$"…"` is matched fine since we just need the literal substring).
            // We DO NOT require the open-bracket to be glued to a known identifier — that would
            // be expensive and brittle across formatting variants. The combination of
            // open-bracket + quoted key + close-bracket is specific enough to avoid false
            // positives in non-localizer code.
            var indexerPattern = $@"\[\s*""{escapedKey}""\s*\]";
            if (Regex.IsMatch(haystack, indexerPattern)) return true;

            // Pattern B: method — .GetString("key" [, args])
            var getStringPattern = $@"\.GetString\s*\(\s*""{escapedKey}""\s*[,)]";
            if (Regex.IsMatch(haystack, getStringPattern)) return true;

            return false;
        }

        /// <summary>
        /// Parse a RESX text and return (name, approxLineNumber) for every translator-facing
        /// &lt;data&gt; element (i.e., string entries — typed/binary entries are skipped).
        /// Lines are 0-indexed for Roslyn LinePosition.
        /// </summary>
        internal static IEnumerable<(string name, int approxLine)> ParseResxEntries(SourceText text)
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

                var line = (data as IXmlLineInfo)?.LineNumber - 1 ?? 0;
                yield return (name!, line);
            }
        }
    }
}
