using System.Collections.Generic;
using System.Text.Json;

namespace Sunfish.Tooling.DesignTokensCodegen;

/// <summary>
/// Parsed representation of a W3C Design Tokens <c>tokens.json</c> file.
/// Only the Sunfish token groups used by the codegen pipeline are modelled.
/// </summary>
public sealed class TokenCatalog
{
    public required IReadOnlyList<ColorEntry> Colors { get; init; }
    public required IReadOnlyList<FlatEntry> NonColorProps { get; init; }
    public required IReadOnlyList<ColorEntry> RoleBandColors { get; init; }
    public required IReadOnlyList<TextSurfacePair> ContrastPairs { get; init; }
}

public sealed class ColorEntry
{
    public required string CssVar { get; init; }   // e.g. --sf-color-surface-primary
    public required string Light { get; init; }
    public required string Dark { get; init; }
    public bool IsNormalText { get; init; }         // true → ≥4.5:1 required; false → ≥3:1
}

public sealed class FlatEntry
{
    public required string CssVar { get; init; }
    public required string Value { get; init; }
    public string? Group { get; init; }
}

public sealed class TextSurfacePair
{
    public required ColorEntry Text { get; init; }
    public required ColorEntry Surface { get; init; }
    public bool RequireEnhanced { get; init; }     // ≥4.5:1 vs ≥3:1
}

/// <summary>
/// Reads <c>tokens.json</c> in W3C Design Tokens format and produces a <see cref="TokenCatalog"/>.
/// </summary>
public static class TokensReader
{
    public static TokenCatalog Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var sf = doc.RootElement.GetProperty("sf");
        var color = sf.GetProperty("color");

        var surface = ReadColorGroup(color.GetProperty("surface"), "surface", true);
        var text = ReadColorGroup(color.GetProperty("text"), "text", true);
        var state = ReadColorGroup(color.GetProperty("state"), "state", false);
        var roleBand = ReadColorGroup(color.GetProperty("role-band"), "role-band", false);

        var allColors = new List<ColorEntry>();
        allColors.AddRange(surface);
        allColors.AddRange(text);
        allColors.AddRange(state);
        allColors.AddRange(roleBand);

        var nonColor = new List<FlatEntry>();
        nonColor.AddRange(ReadTypography(sf.GetProperty("typography")));
        nonColor.AddRange(ReadScale(sf.GetProperty("space"), "sf.space", "--sf-space-"));
        nonColor.AddRange(ReadScale(sf.GetProperty("radius"), "sf.radius", "--sf-radius-"));
        nonColor.AddRange(ReadElevation(sf.GetProperty("elevation")));
        nonColor.AddRange(ReadMotion(sf.GetProperty("motion")));
        nonColor.AddRange(ReadTargetSize(sf.GetProperty("target-size")));

        // Cross-product of text × surface for contrast checking.
        // text.primary and text.secondary → require enhanced (≥4.5:1 normal text).
        // text.tertiary → large text / non-text threshold (≥3:1).
        var pairs = new List<TextSurfacePair>();
        foreach (var t in text)
        {
            bool isNormal = !t.CssVar.EndsWith("tertiary");
            foreach (var s in surface)
            {
                pairs.Add(new TextSurfacePair
                {
                    Text = t,
                    Surface = s,
                    RequireEnhanced = isNormal,
                });
            }
        }

        return new TokenCatalog
        {
            Colors = allColors,
            NonColorProps = nonColor,
            RoleBandColors = roleBand,
            ContrastPairs = pairs,
        };
    }

    private static IReadOnlyList<ColorEntry> ReadColorGroup(
        JsonElement group, string segment, bool isNormalText)
    {
        var entries = new List<ColorEntry>();
        foreach (var prop in group.EnumerateObject())
        {
            if (prop.Name.StartsWith("$")) continue;
            var val = prop.Value.GetProperty("$value");
            entries.Add(new ColorEntry
            {
                CssVar = $"--sf-color-{segment}-{prop.Name}",
                Light = val.GetProperty("light").GetString()!,
                Dark = val.GetProperty("dark").GetString()!,
                IsNormalText = isNormalText,
            });
        }
        return entries;
    }

    private static IReadOnlyList<FlatEntry> ReadTypography(JsonElement typo)
    {
        var list = new List<FlatEntry>();
        // family
        foreach (var f in typo.GetProperty("family").EnumerateObject())
        {
            if (f.Name.StartsWith("$")) continue;
            list.Add(new FlatEntry
            {
                CssVar = $"--sf-typography-family-{f.Name}",
                Value = f.Value.GetProperty("$value").GetString()!,
                Group = "typography",
            });
        }
        // size
        foreach (var f in typo.GetProperty("size").EnumerateObject())
        {
            if (f.Name.StartsWith("$")) continue;
            list.Add(new FlatEntry
            {
                CssVar = $"--sf-typography-size-{f.Name}",
                Value = f.Value.GetProperty("$value").GetString()!,
                Group = "typography",
            });
        }
        // weight
        foreach (var f in typo.GetProperty("weight").EnumerateObject())
        {
            if (f.Name.StartsWith("$")) continue;
            list.Add(new FlatEntry
            {
                CssVar = $"--sf-typography-weight-{f.Name}",
                Value = f.Value.GetProperty("$value").GetRawText()!,
                Group = "typography",
            });
        }
        // line-height
        foreach (var f in typo.GetProperty("line-height").EnumerateObject())
        {
            if (f.Name.StartsWith("$")) continue;
            list.Add(new FlatEntry
            {
                CssVar = $"--sf-typography-line-height-{f.Name}",
                Value = f.Value.GetProperty("$value").GetRawText()!,
                Group = "typography",
            });
        }
        return list;
    }

    private static IReadOnlyList<FlatEntry> ReadScale(JsonElement group, string segment, string prefix)
    {
        var list = new List<FlatEntry>();
        foreach (var f in group.EnumerateObject())
        {
            if (f.Name.StartsWith("$")) continue;
            list.Add(new FlatEntry
            {
                CssVar = $"{prefix}{f.Name}",
                Value = f.Value.GetProperty("$value").GetString()!,
                Group = segment,
            });
        }
        return list;
    }

    private static IReadOnlyList<FlatEntry> ReadElevation(JsonElement group)
    {
        var list = new List<FlatEntry>();
        foreach (var f in group.EnumerateObject())
        {
            if (f.Name.StartsWith("$")) continue;
            list.Add(new FlatEntry
            {
                CssVar = $"--sf-elevation-{f.Name}",
                Value = f.Value.GetProperty("$value").GetString()!,
                Group = "elevation",
            });
        }
        return list;
    }

    private static IReadOnlyList<FlatEntry> ReadMotion(JsonElement group)
    {
        var list = new List<FlatEntry>();
        foreach (var f in group.GetProperty("duration").EnumerateObject())
        {
            if (f.Name.StartsWith("$")) continue;
            list.Add(new FlatEntry
            {
                CssVar = $"--sf-motion-duration-{f.Name}",
                Value = f.Value.GetProperty("$value").GetString()!,
                Group = "motion",
            });
        }
        foreach (var f in group.GetProperty("easing").EnumerateObject())
        {
            if (f.Name.StartsWith("$")) continue;
            var raw = f.Value.GetProperty("$value");
            // cubicBezier values are stored as [p1x,p1y,p2x,p2y] arrays.
            if (raw.ValueKind == JsonValueKind.Array)
            {
                double p1 = raw[0].GetDouble();
                double p2 = raw[1].GetDouble();
                double p3 = raw[2].GetDouble();
                double p4 = raw[3].GetDouble();
                list.Add(new FlatEntry
                {
                    CssVar = $"--sf-motion-easing-{f.Name}",
                    Value = $"cubic-bezier({p1}, {p2}, {p3}, {p4})",
                    Group = "motion",
                });
            }
        }
        return list;
    }

    private static IReadOnlyList<FlatEntry> ReadTargetSize(JsonElement group)
    {
        var list = new List<FlatEntry>();
        foreach (var f in group.EnumerateObject())
        {
            if (f.Name.StartsWith("$")) continue;
            list.Add(new FlatEntry
            {
                CssVar = $"--sf-target-size-{f.Name}",
                Value = f.Value.GetProperty("$value").GetString()!,
                Group = "target-size",
            });
        }
        return list;
    }
}
