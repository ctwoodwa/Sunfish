using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Sunfish.Tooling.LocalizationXliff
{
    /// <summary>
    /// Reader / writer for .NET resource XML (.resx) files used by the
    /// <c>SunfishResxToXliffTask</c> and <c>SunfishXliffToResxTask</c> MSBuild tasks.
    /// </summary>
    /// <remarks>
    /// Only the Sunfish-localized subset is handled: the standard &lt;data&gt; string
    /// entries with optional &lt;comment&gt; elements. ResXFileRef binary references,
    /// custom type metadata, and the RESX header blocks are preserved verbatim on
    /// round-trip so hand-maintained metadata survives export/import cycles.
    /// </remarks>
    public sealed class ResxFile
    {
        /// <summary>Ordered entries in document order; order preservation is part of the round-trip contract.</summary>
        public List<ResxEntry> Entries { get; } = new List<ResxEntry>();

        /// <summary>Non-data elements (schema, resheader, resmimetype) kept verbatim.</summary>
        public List<XElement> PreservedHeader { get; } = new List<XElement>();

        /// <summary>Load a RESX file from disk.</summary>
        public static ResxFile Load(string path)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            return FromXml(doc);
        }

        /// <summary>Parse RESX content from an XDocument.</summary>
        public static ResxFile FromXml(XDocument doc)
        {
            if (doc?.Root is null) throw new InvalidDataException("RESX document has no root.");
            if (doc.Root.Name.LocalName != "root")
                throw new InvalidDataException($"RESX root element must be 'root', got '{doc.Root.Name.LocalName}'.");

            var file = new ResxFile();
            foreach (var child in doc.Root.Elements())
            {
                if (child.Name.LocalName == "data" && child.Attribute("name") is not null
                    && (child.Attribute("type") is null || string.IsNullOrEmpty(child.Attribute("type")!.Value)))
                {
                    file.Entries.Add(new ResxEntry
                    {
                        Name = child.Attribute("name")!.Value,
                        Value = child.Element("value")?.Value ?? string.Empty,
                        Comment = child.Element("comment")?.Value,
                    });
                }
                else
                {
                    file.PreservedHeader.Add(new XElement(child));
                }
            }
            return file;
        }

        /// <summary>Write this RESX to disk. Document structure preserves ordering + headers.</summary>
        public void Save(string path)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir!);
            ToXml().Save(path);
        }

        /// <summary>Render to an XDocument for in-memory use / tests.</summary>
        public XDocument ToXml()
        {
            var root = new XElement("root");
            foreach (var header in PreservedHeader) root.Add(new XElement(header));
            foreach (var entry in Entries) root.Add(entry.ToXElement());
            return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        }
    }

    /// <summary>A single &lt;data&gt; string entry in a RESX file.</summary>
    public sealed class ResxEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Comment { get; set; }

        internal XElement ToXElement()
        {
            var el = new XElement("data",
                new XAttribute("name", Name),
                new XAttribute(XNamespace.Xml + "space", "preserve"),
                new XElement("value", Value));
            if (!string.IsNullOrEmpty(Comment)) el.Add(new XElement("comment", Comment));
            return el;
        }
    }
}
