using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Sunfish.Tooling.LocalizationXliff
{
    /// <summary>
    /// MSBuild task that converts translator-owned XLIFF 2.0 files back to <c>.resx</c> satellite
    /// form for .NET satellite-assembly compilation.
    /// </summary>
    /// <remarks>
    /// Only segments with state <c>translated</c>, <c>reviewed</c>, or <c>final</c> are materialised
    /// into the output <c>.resx</c>. Units in state <c>initial</c> are emitted with the source
    /// value as the resource value — matching .NET's ResourceManager fallback-to-neutral behaviour
    /// with explicit translator notes preserved.
    /// </remarks>
    public sealed class SunfishXliffToResxTask : Microsoft.Build.Utilities.Task
    {
        /// <summary>Input XLIFF 2.0 files.</summary>
        [Required]
        public ITaskItem[] SourceXliffFiles { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>Where locale-specific <c>.resx</c> files are written.</summary>
        [Required]
        public string OutputDirectory { get; set; } = string.Empty;

        /// <summary>Emitted <c>.resx</c> files (for downstream targets).</summary>
        [Output]
        public ITaskItem[] GeneratedResxFiles { get; private set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(OutputDirectory))
            {
                Log.LogError("OutputDirectory is required.");
                return false;
            }

            Directory.CreateDirectory(OutputDirectory);
            var produced = new List<ITaskItem>();

            foreach (var item in SourceXliffFiles)
            {
                var inputPath = item.ItemSpec;
                if (!File.Exists(inputPath))
                {
                    Log.LogWarning("XLIFF file not found: {0}", inputPath);
                    continue;
                }

                try
                {
                    var xliff = Xliff20File.Load(inputPath);
                    var resx = ConvertToResx(xliff);
                    var outputPath = BuildOutputPath(xliff);
                    resx.Save(outputPath);

                    var producedItem = new TaskItem(outputPath);
                    producedItem.SetMetadata("SourceXliff", inputPath);
                    producedItem.SetMetadata("TargetLocale", xliff.TargetLanguage);
                    produced.Add(producedItem);

                    Log.LogMessage(MessageImportance.Normal,
                        "Imported {0} → {1} ({2} entries)", inputPath, outputPath, resx.Entries.Count);
                }
                catch (Exception ex)
                {
                    Log.LogError("Failed to import {0} → .resx: {1}", inputPath, ex.Message);
                    return false;
                }
            }

            GeneratedResxFiles = produced.ToArray();
            return true;
        }

        private static ResxFile ConvertToResx(Xliff20File xliff)
        {
            var resx = new ResxFile();
            foreach (var unit in xliff.Units)
            {
                var value = (unit.State == "translated" || unit.State == "reviewed" || unit.State == "final")
                    ? unit.Target ?? unit.Source
                    : unit.Source;

                resx.Entries.Add(new ResxEntry
                {
                    Name = unit.Id,
                    Value = value,
                    Comment = unit.TranslatorNote,
                });
            }
            return resx;
        }

        private string BuildOutputPath(Xliff20File xliff)
        {
            var basename = Path.GetFileNameWithoutExtension(xliff.OriginalFile);
            // Strip .resx extension if the original file preserved it
            if (basename.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
            {
                basename = basename.Substring(0, basename.Length - 5);
            }
            var fileName = string.IsNullOrEmpty(xliff.TargetLanguage)
                ? $"{basename}.resx"
                : $"{basename}.{xliff.TargetLanguage}.resx";
            return Path.Combine(OutputDirectory, fileName);
        }
    }
}
