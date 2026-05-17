using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialAp.Models;

/// <summary>
/// AP aging buckets — same five-bucket convention as
/// <c>Sunfish.Blocks.FinancialAr.Models.AgingBucket</c>. The
/// expected use is symmetric (rent-collection dashboards, vendor-aging
/// reports, treasury cash-flow projections); having the same names +
/// JSON codes on both sides lets UIs render both reports through a
/// single template.
/// </summary>
[JsonConverter(typeof(AgingBucketJsonConverter))]
public enum AgingBucket
{
    /// <summary>Open bills not yet past <c>DueDate</c>.</summary>
    Current,

    /// <summary>1–30 days past due.</summary>
    Days0To30,

    /// <summary>31–60 days past due.</summary>
    Days31To60,

    /// <summary>61–90 days past due.</summary>
    Days61To90,

    /// <summary>91+ days past due.</summary>
    Days90Plus,
}

internal sealed class AgingBucketJsonConverter : JsonConverter<AgingBucket>
{
    public override AgingBucket Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "current"     => AgingBucket.Current,
            "0-30"        => AgingBucket.Days0To30,
            "31-60"       => AgingBucket.Days31To60,
            "61-90"       => AgingBucket.Days61To90,
            "90+"         => AgingBucket.Days90Plus,
            var other     => throw new JsonException($"Unknown AgingBucket '{other}'."),
        };

    public override void Write(Utf8JsonWriter writer, AgingBucket value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            AgingBucket.Current     => "current",
            AgingBucket.Days0To30   => "0-30",
            AgingBucket.Days31To60  => "31-60",
            AgingBucket.Days61To90  => "61-90",
            AgingBucket.Days90Plus  => "90+",
            _ => throw new JsonException($"Unknown AgingBucket '{value}'."),
        });
    }
}
