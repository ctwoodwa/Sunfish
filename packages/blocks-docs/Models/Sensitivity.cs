using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Docs.Models;

/// <summary>
/// Sensitivity classification for an <see cref="Attachment"/>. Drives
/// downstream sharing / export policy (e.g., PII-containing scans should
/// not be auto-shared to a viewer policy that allows external links).
/// </summary>
[JsonConverter(typeof(SensitivityJsonConverter))]
public enum Sensitivity
{
    /// <summary>Default. Safe to share within the tenant.</summary>
    Internal,

    /// <summary>Contains personally-identifiable information — handle per the org's PII policy.</summary>
    Pii,

    /// <summary>Contains financial detail not for general sharing (tax returns, account statements).</summary>
    Financial,

    /// <summary>Confidential — minimum-access; usually requires per-recipient grant.</summary>
    Confidential,
}

internal sealed class SensitivityJsonConverter : JsonConverter<Sensitivity>
{
    public override Sensitivity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "internal"      => Sensitivity.Internal,
            "pii"           => Sensitivity.Pii,
            "financial"     => Sensitivity.Financial,
            "confidential"  => Sensitivity.Confidential,
            var other       => throw new JsonException($"Unknown Sensitivity '{other}'."),
        };

    public override void Write(Utf8JsonWriter writer, Sensitivity value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            Sensitivity.Internal      => "internal",
            Sensitivity.Pii           => "pii",
            Sensitivity.Financial     => "financial",
            Sensitivity.Confidential  => "confidential",
            _ => throw new JsonException($"Unknown Sensitivity '{value}'."),
        });
    }
}
