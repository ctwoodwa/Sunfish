using System.Text;
using System.Text.Json;
using Sunfish.Ingestion.Sensors;
using Sunfish.Ingestion.Sensors.Decoders;
using Xunit;

namespace Sunfish.Ingestion.Sensors.Tests;

public class JsonSensorBatchDecoderTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public async Task Decode_JsonArray_YieldsFiveReadings()
    {
        await using var fs = File.OpenRead(FixturePath("batch-small.json"));
        var decoder = new JsonSensorBatchDecoder(SensorBatchFormat.Json);

        var readings = new List<SensorReading>();
        await foreach (var r in decoder.DecodeAsync(fs, CancellationToken.None))
        {
            readings.Add(r);
        }

        Assert.Equal(5, readings.Count);
        Assert.Equal("s1", readings[0].SensorId);
        Assert.Equal("temperature", readings[0].Kind);
        Assert.Equal(21.3, readings[0].Value);
        Assert.Equal("celsius", readings[0].Unit);
    }

    [Fact]
    public async Task Decode_Ndjson_YieldsFiveReadings()
    {
        await using var fs = File.OpenRead(FixturePath("batch-ndjson.jsonl"));
        var decoder = new JsonSensorBatchDecoder(SensorBatchFormat.JsonNdjson);

        var readings = new List<SensorReading>();
        await foreach (var r in decoder.DecodeAsync(fs, CancellationToken.None))
        {
            readings.Add(r);
        }

        Assert.Equal(5, readings.Count);
        Assert.Equal("water_leak", readings[4].Kind);
        Assert.Equal(1, readings[4].Value);
    }

    [Fact]
    public async Task Decode_EmptyJsonArray_YieldsNothing()
    {
        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes("[]"));
        var decoder = new JsonSensorBatchDecoder(SensorBatchFormat.Json);

        var count = 0;
        await foreach (var _ in decoder.DecodeAsync(ms, CancellationToken.None))
        {
            count++;
        }

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Decode_MalformedJson_Throws()
    {
        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes("not json"));
        var decoder = new JsonSensorBatchDecoder(SensorBatchFormat.Json);

        await Assert.ThrowsAnyAsync<JsonException>(async () =>
        {
            await foreach (var _ in decoder.DecodeAsync(ms, CancellationToken.None))
            {
                // drain
            }
        });
    }

    [Fact]
    public async Task Decode_JsonArray_YieldsReadingsIncrementally()
    {
        await using var fs = File.OpenRead(FixturePath("batch-small.json"));
        var decoder = new JsonSensorBatchDecoder(SensorBatchFormat.Json);

        await using var enumerator = decoder.DecodeAsync(fs, CancellationToken.None).GetAsyncEnumerator();
        var steps = 0;
        while (await enumerator.MoveNextAsync())
        {
            // simulate incremental per-reading handling
            Assert.NotNull(enumerator.Current);
            steps++;
        }

        Assert.Equal(5, steps);
    }
}
