using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Sunfish.Tooling.LocalizationXliff
{
    /// <summary>
    /// MSBuild task that converts Sunfish <c>.resx</c> source files to XLIFF 2.0 bilingual
    /// exchange format for the <see cref="Xliff20File"/> pipeline.
    /// </summary>
    /// <remarks>
    /// One XLIFF file is produced per (source <c>.resx</c>, target locale) pair. The source
    /// language is taken from <see cref="SourceLanguage"/> (default <c>en-US</c>); target
    /// language is inferred from the <c>.resx</c> filename suffix (e.g., <c>Resources.ar-SA.resx</c>).
    /// For the neutral source file (<c>Resources.resx</c>), one XLIFF per enumerated target locale
    /// is emitted — the caller drives the enumeration via MSBuild metadata <c>%(TargetLocale)</c>
    /// on the input item if supplied, otherwise no targets are emitted for neutral files.
    ///
    /// When <see cref="PreserveApprovedTargets"/> is true (default), existing XLIFF files at the
    /// output path have their <c>translated</c> / <c>final</c> segments preserved; only entries
    /// whose source text has changed or whose keys are new are updated. This is the idempotency
    /// guarantee that makes the task safe to run on every build.
    /// </remarks>
    public sealed class SunfishResxToXliffTask : Microsoft.Build.Utilities.Task
    {
        /// <summary>Input <c>.resx</c> files. Filename suffix (e.g. <c>.ar-SA.resx</c>) drives the target locale.</summary>
        [Required]
        public ITaskItem[] SourceResxFiles { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>Where XLIFF files are written (created if missing).</summary>
        [Required]
        public string OutputDirectory { get; set; } = string.Empty;

        /// <summary>BCP-47 tag for the source language. Default <c>en-US</c>.</summary>
        public string SourceLanguage { get; set; } = "en-US";

        /// <summary>Preserve translator-approved segments on re-export. Default true.</summary>
        public bool PreserveApprovedTargets { get; set; } = true;

        /// <summary>Emitted XLIFF files (for downstream targets).</summary>
        [Output]
        public ITaskItem[] GeneratedXliffFiles { get; private set; } = Array.Empty<ITaskItem>();

        /// <summary>Regex for <c>Resources.&lt;bcp47&gt;.resx</c> → captures the locale tag.</summary>
        private static readonly Regex LocaleSuffixPattern =
            new Regex(@"\.(?<locale>[a-zA-Z]{2,3}(-[A-Za-z0-9]+)*)\.resx$", RegexOptions.Compiled);

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(OutputDirectory))
            {
                Log.LogError("OutputDirectory is required.");
                return false;
            }

            Directory.CreateDirectory(OutputDirectory);
            var produced = new List<ITaskItem>();

            foreach (var item in SourceResxFiles)
            {
                var inputPath = item.ItemSpec;
                if (!File.Exists(inputPath))
                {
                    Log.LogWarning("Source .resx not found: {0}", inputPath);
                    continue;
                }

                var targetLocale = InferTargetLocale(inputPath, item);
                if (string.IsNullOrEmpty(targetLocale))
                {
                    Log.LogMessage(MessageImportance.Low,
                        "Skipping neutral .resx (no target-locale metadata): {0}", inputPath);
                    continue;
                }

                try
                {
                    var outputPath = BuildOutputPath(inputPath, targetLocale!);
                    var resx = ResxFile.Load(inputPath);
                    var xliff = ConvertToXliff(resx, inputPath, targetLocale!, outputPath);
                    xliff.Save(outputPath);

                    var producedItem = new TaskItem(outputPath);
                    producedItem.SetMetadata("SourceResx", inputPath);
                    producedItem.SetMetadata("TargetLocale", targetLocale);
                    produced.Add(producedItem);

                    Log.LogMessage(MessageImportance.Normal,
                        "Exported {0} → {1} ({2} units)", inputPath, outputPath, xliff.Units.Count);
                }
                catch (Exception ex)
                {
                    Log.LogError("Failed to export {0} → XLIFF: {1}", inputPath, ex.Message);
                    return false;
                }
            }

            GeneratedXliffFiles = produced.ToArray();
            return true;
        }

        private string? InferTargetLocale(string path, ITaskItem item)
        {
            var metadataLocale = item.GetMetadata("TargetLocale");
            if (!string.IsNullOrEmpty(metadataLocale)) return metadataLocale;

            var match = LocaleSuffixPattern.Match(path);
            return match.Success ? match.Groups["locale"].Value : null;
        }

        private string BuildOutputPath(string inputPath, string targetLocale)
        {
            var baseName = Path.GetFileNameWithoutExtension(inputPath);
            // Strip the locale suffix if present: "Resources.ar-SA" → "Resources"
            var stripMatch = Regex.Match(baseName, @"^(?<base>.+?)\.[a-zA-Z]{2,3}(-[A-Za-z0-9]+)*$");
            if (stripMatch.Success) baseName = stripMatch.Groups["base"].Value;
            return Path.Combine(OutputDirectory, $"{baseName}.{targetLocale}.xlf");
        }

        private Xliff20File ConvertToXliff(ResxFile resx, string sourcePath, string targetLocale, string outputPath)
        {
            Xliff20File existing = PreserveApprovedTargets && File.Exists(outputPath)
                ? SafeLoadExisting(outputPath)
                : new Xliff20File { SourceLanguage = SourceLanguage, TargetLanguage = targetLocale };

            var existingByKey = existing.Units.ToDictionary(u => u.Id, StringComparer.Ordinal);
            var xliff = new Xliff20File
            {
                SourceLanguage = SourceLanguage,
                TargetLanguage = targetLocale,
                OriginalFile = Path.GetFileName(sourcePath),
            };

            foreach (var entry in resx.Entries)
            {
                var unit = new XliffUnit
                {
                    Id = entry.Name,
                    Source = entry.Value,
                    TranslatorNote = entry.Comment,
                    LocationNote = Path.GetFileName(sourcePath),
                };

                if (existingByKey.TryGetValue(entry.Name, out var prior))
                {
                    var priorSourceMatches = string.Equals(prior.Source, entry.Value, StringComparison.Ordinal);
                    if (priorSourceMatches &&
                        (prior.State == "translated" || prior.State == "reviewed" || prior.State == "final"))
                    {
                        unit.Target = prior.Target;
                        unit.State = prior.State;
                    }
                    else if (priorSourceMatches)
                    {
                        unit.Target = prior.Target;
                        unit.State = prior.State;
                    }
                    else
                    {
                        unit.Target = prior.Target;
                        unit.State = "initial";
                    }
                }

                xliff.Units.Add(unit);
            }

            return xliff;
        }

        private Xliff20File SafeLoadExisting(string path)
        {
            try { return Xliff20File.Load(path); }
            catch (Exception ex)
            {
                Log.LogWarning("Existing XLIFF at {0} could not be loaded ({1}); starting fresh.", path, ex.Message);
                return new Xliff20File { SourceLanguage = SourceLanguage };
            }
        }
    }
}
