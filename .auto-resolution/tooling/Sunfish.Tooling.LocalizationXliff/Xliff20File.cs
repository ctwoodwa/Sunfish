using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Sunfish.Tooling.LocalizationXliff
{
    /// <summary>
    /// Reader / writer for XLIFF 2.0 (OASIS 2014) bilingual files used in the
    /// Sunfish <c>.resx</c> &lt;-&gt; XLIFF round-trip pipeline.
    /// </summary>
    /// <remarks>
    /// Bilingual mode only — one source+target language pair per file — matching
    /// how <c>.resx</c> satellite assemblies are structured and matching Weblate
    /// 5.17.1's documented XLIFF 2.0 support (bilingual). Units are keyed by
    /// the RESX <c>name</c> attribute so round-trip is lossless without heuristic
    /// matching. Translator-approved targets (state <c>final</c> or
    /// <c>translated</c>) are preserved on export — the task layer is responsible
    /// for honouring the <c>PreserveApprovedTargets</c> flag.
    /// </remarks>
    public sealed class Xliff20File
    {
        public const string Xliff20Namespace = "urn:oasis:names:tc:xliff:document:2.0";
        public const string Version = "2.0";

        public string SourceLanguage { get; set; } = "en-US";
        public string TargetLanguage { get; set; } = "";
        public string OriginalFile { get; set; } = "";
        public List<XliffUnit> Units { get; } = new List<XliffUnit>();

        public static Xliff20File Load(string path)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            return FromXml(doc);
        }

        public static Xliff20File FromXml(XDocument doc)
        {
            if (doc?.Root is null) throw new InvalidDataException("XLIFF document has no root.");
            XNamespace ns = Xliff20Namespace;
            if (doc.Root.Name.Namespace != ns)
                throw new InvalidDataException($"XLIFF root must be in {Xliff20Namespace}; got {doc.Root.Name.Namespace}.");

            var version = doc.Root.Attribute("version")?.Value ?? string.Empty;
            if (version != Version)
                throw new InvalidDataException($"XLIFF version must be {Version}; got '{version}'.");

            var file = new Xliff20File
            {
                SourceLanguage = doc.Root.Attribute("srcLang")?.Value ?? "en-US",
                TargetLanguage = doc.Root.Attribute("trgLang")?.Value ?? string.Empty,
            };

            var fileElement = doc.Root.Element(ns + "file");
            if (fileElement is null)
                throw new InvalidDataException("XLIFF document missing required <file> element.");

            file.OriginalFile = fileElement.Attribute("original")?.Value ?? string.Empty;

            foreach (var unit in fileElement.Elements(ns + "unit"))
            {
                var id = unit.Attribute("id")?.Value ?? throw new InvalidDataException("<unit> missing required id attribute.");
                var notes = unit.Element(ns + "notes");
                var locationNote = notes?.Elements(ns + "note")
                    .FirstOrDefault(n => n.Attribute("category")?.Value == "location")?.Value;
                var translatorNote = notes?.Elements(ns + "note")
                    .FirstOrDefault(n => n.Attribute("category")?.Value == "translator")?.Value;

                var segment = unit.Element(ns + "segment");
                if (segment is null)
                    throw new InvalidDataException($"<unit id=\"{id}\"> missing required <segment>.");

                file.Units.Add(new XliffUnit
                {
                    Id = id,
                    Source = segment.Element(ns + "source")?.Value ?? string.Empty,
                    Target = segment.Element(ns + "target")?.Value,
                    State = segment.Attribute("state")?.Value ?? "initial",
                    LocationNote = locationNote,
                    TranslatorNote = translatorNote,
                });
            }

            return file;
        }

        public void Save(string path)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir!);
            ToXml().Save(path);
        }

        public XDocument ToXml()
        {
            XNamespace ns = Xliff20Namespace;
            var fileEl = new XElement(ns + "file",
                new XAttribute("id", "f1"),
                new XAttribute("original", OriginalFile));

            foreach (var unit in Units) fileEl.Add(unit.ToXElement(ns));

            var root = new XElement(ns + "xliff",
                new XAttribute("version", Version),
                new XAttribute("srcLang", SourceLanguage),
                new XAttribute("trgLang", TargetLanguage),
                fileEl);

            return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        }
    }

    /// <summary>A single translation unit inside an XLIFF file.</summary>
    public sealed class XliffUnit
    {
        /// <summary>Stable identifier — maps 1:1 to the RESX entry <c>name</c> attribute.</summary>
        public string Id { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        /// <summary>Null until a translator has authored a target. Empty string on new units.</summary>
        public string? Target { get; set; }

        /// <summary>
        /// XLIFF 2.0 segment state: <c>initial</c> / <c>translated</c> / <c>reviewed</c> / <c>final</c>.
        /// Export-direction tasks must preserve <c>translated</c> / <c>final</c> state when the
        /// round-trip runs against an existing XLIFF file.
        /// </summary>
        public string State { get; set; } = "initial";

        /// <summary>Origin RESX location note (informational only).</summary>
        public string? LocationNote { get; set; }

        /// <summary>Translator-facing context; mirrored from RESX &lt;comment&gt;.</summary>
        public string? TranslatorNote { get; set; }

        internal XElement ToXElement(XNamespace ns)
        {
            var notes = new List<XElement>();
            if (!string.IsNullOrEmpty(LocationNote))
            {
                notes.Add(new XElement(ns + "note",
                    new XAttribute("category", "location"),
                    LocationNote));
            }
            if (!string.IsNullOrEmpty(TranslatorNote))
            {
                notes.Add(new XElement(ns + "note",
                    new XAttribute("category", "translator"),
                    TranslatorNote));
            }

            var segment = new XElement(ns + "segment",
                new XAttribute("state", State),
                new XElement(ns + "source", Source));

            if (Target is not null)
            {
                segment.Add(new XElement(ns + "target", Target));
            }

            var unit = new XElement(ns + "unit",
                new XAttribute("id", Id));

            if (notes.Count > 0)
            {
                unit.Add(new XElement(ns + "notes", notes));
            }

            unit.Add(segment);
            return unit;
        }
    }
}
