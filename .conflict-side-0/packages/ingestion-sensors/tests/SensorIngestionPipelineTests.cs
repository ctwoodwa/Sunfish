using System.Text;
using Sunfish.Foundation.Blobs;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Sensors;
using Xunit;

namespace Sunfish.Ingestion.Sensors.Tests;

public class SensorIngestionPipelineTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileSystemBlobStore _blobs;

    public SensorIngestionPipelineTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "sunfish-sensors-" + Path.GetRandomFileName());
        _blobs = new FileSystemBlobStore(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static IngestionContext Ctx() =>
        IngestionContext.NewCorrelation("tenant-1", "actor-1");

    [Fact]
    public async Task Ingest_Json_HappyPath_FiveReadings_OneBlobCid_FiveEvents()
    {
        var bytes = await File.ReadAllBytesAsync(FixturePath("batch-small.json"));
        var batch = new SensorBatch(
            Content: new MemoryStream(bytes),
            Format: SensorBatchFormat.Json,
            ProducerId: "gateway-1",
            SchemaId: "sunfish.sensors.batch/1");

        var pipeline = new SensorIngestionPipeline(_blobs);

        var result = await pipeline.IngestAsync(batch, Ctx());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(5, result.Value!.Events.Count);
        Assert.Single(result.Value.BlobCids);
        Assert.All(result.Value.Events, e => Assert.Equal("sensor.reading", e.Kind));
        Assert.Equal(5, result.Value.Body["readingCount"]);
        Assert.Equal("gateway-1", result.Value.Body["producerId"]);
    }

    [Fact]
    public async Task Ingest_EmptyJsonArray_ReturnsSuccess_ZeroEvents()
    {
        var batch = new SensorBatch(
            Content: new MemoryStream(Encoding.UTF8.GetBytes("[]")),
            Format: SensorBatchFormat.Json,
            ProducerId: "gateway-1",
            SchemaId: "sunfish.sensors.batch/1");

        var pipeline = new SensorIngestionPipeline(_blobs);

        var result = await pipeline.IngestAsync(batch, Ctx());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Events);
        Assert.Equal(0, result.Value.Body["readingCount"]);
        Assert.Null(result.Value.Body["windowStartUtc"]);
        Assert.Null(result.Value.Body["windowEndUtc"]);
    }

    [Fact]
    public async Task Ingest_MessagePack_ReturnsUnsupportedFormat()
    {
        var batch = new SensorBatch(
            Content: new MemoryStream(new byte[] { 0x81, 0x01 }),
            Format: SensorBatchFormat.MessagePack,
            ProducerId: "gateway-1",
            SchemaId: "sunfish.sensors.batch/1");

        var pipeline = new SensorIngestionPipeline(_blobs);

        var result = await pipeline.IngestAsync(batch, Ctx());

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestOutcome.UnsupportedFormat, result.Outcome);
        Assert.Contains("MessagePack", result.Failure!.Message);
    }

    [Fact]
    public async Task Ingest_BlobArchival_ContentRoundTrips()
    {
        var bytes = await File.ReadAllBytesAsync(FixturePath("batch-small.json"));
        var batch = new SensorBatch(
            Content: new MemoryStream(bytes),
            Format: SensorBatchFormat.Json,
            ProducerId: "gateway-1",
            SchemaId: "sunfish.sensors.batch/1");

        var pipeline = new SensorIngestionPipeline(_blobs);
        var result = await pipeline.IngestAsync(batch, Ctx());

        Assert.True(result.IsSuccess);
        var cid = result.Value!.BlobCids[0];
        var stored = await _blobs.GetAsync(cid);
        Assert.NotNull(stored);
        Assert.Equal(bytes, stored!.Value.ToArray());
    }
}
