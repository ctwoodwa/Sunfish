using Sunfish.Foundation.Blobs;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Imagery;
using Sunfish.Ingestion.Imagery.Metadata;
using Xunit;

namespace Sunfish.Ingestion.Imagery.Tests;

public class ImageryIngestionPipelineTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileSystemBlobStore _blobs;

    public ImageryIngestionPipelineTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "sunfish-imagery-" + Path.GetRandomFileName());
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

    private static IngestionContext Ctx() =>
        IngestionContext.NewCorrelation("tenant-1", "actor-1");

    [Fact]
    public async Task Ingest_HappyPath_StubExtractor_EntityBodyContainsCidAndMetadata()
    {
        var captured = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var extractor = new StubExtractor(new ImageryMetadata(
            GpsLatitude: 37.7749,
            GpsLongitude: -122.4194,
            CapturedUtc: captured,
            CameraMake: "Canon",
            CameraModel: "R5",
            FocalLengthMm: 35.0,
            WidthPx: 4000,
            HeightPx: 3000));

        var upload = new ImageUpload(
            Content: new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }),
            Filename: "photo.jpg",
            ContentType: "image/jpeg",
            SchemaId: "sunfish.imagery/1");

        var pipeline = new ImageryIngestionPipeline(_blobs, extractor);

        var result = await pipeline.IngestAsync(upload, Ctx());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("sunfish.imagery/1", result.Value!.SchemaId);
        Assert.Single(result.Value.BlobCids);
        Assert.Single(result.Value.Events);
        Assert.Equal("imagery.ingested", result.Value.Events[0].Kind);
        Assert.Equal("photo.jpg", result.Value.Body["filename"]);
        Assert.Equal("image/jpeg", result.Value.Body["contentType"]);
        Assert.Equal(37.7749, result.Value.Body["gpsLatitude"]);
        Assert.Equal(-122.4194, result.Value.Body["gpsLongitude"]);
        Assert.Equal(captured, result.Value.Body["capturedUtc"]);
        Assert.Equal("Canon", result.Value.Body["cameraMake"]);
        Assert.Equal("R5", result.Value.Body["cameraModel"]);
        Assert.Equal(35.0, result.Value.Body["focalLengthMm"]);
        Assert.Equal(4000, result.Value.Body["widthPx"]);
        Assert.Equal(3000, result.Value.Body["heightPx"]);
        Assert.NotNull(result.Value.Body["imageBlobCid"]);
    }

    [Fact]
    public async Task Ingest_SameImage_ProducesSameCid_Idempotent()
    {
        var extractor = new StubExtractor(ImageryMetadata.Empty);
        var bytes = new byte[] { 0xFF, 0xD8, 0x01, 0x02, 0x03, 0xFF, 0xD9 };

        var upload1 = new ImageUpload(new MemoryStream(bytes), "a.jpg", "image/jpeg", "sunfish.imagery/1");
        var upload2 = new ImageUpload(new MemoryStream(bytes), "b.jpg", "image/jpeg", "sunfish.imagery/1");

        var pipeline = new ImageryIngestionPipeline(_blobs, extractor);

        var r1 = await pipeline.IngestAsync(upload1, Ctx());
        var r2 = await pipeline.IngestAsync(upload2, Ctx());

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);
        Assert.Equal(r1.Value!.BlobCids[0], r2.Value!.BlobCids[0]);
        // Body metadata cid string must also match — content-addressed storage.
        Assert.Equal(r1.Value.Body["imageBlobCid"], r2.Value.Body["imageBlobCid"]);
    }

    [Fact]
    public async Task Ingest_ExtractorThrows_ReturnsEntityWithEmptyMetadata()
    {
        var extractor = new ThrowingExtractor();
        var upload = new ImageUpload(
            Content: new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }),
            Filename: "broken.jpg",
            ContentType: "image/jpeg",
            SchemaId: "sunfish.imagery/1");

        var pipeline = new ImageryIngestionPipeline(_blobs, extractor);

        var result = await pipeline.IngestAsync(upload, Ctx());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Null(result.Value!.Body["gpsLatitude"]);
        Assert.Null(result.Value.Body["gpsLongitude"]);
        Assert.Null(result.Value.Body["capturedUtc"]);
        Assert.Null(result.Value.Body["cameraMake"]);
        Assert.Null(result.Value.Body["cameraModel"]);
        // Entity still minted with blob CID despite extractor failure.
        Assert.NotNull(result.Value.Body["imageBlobCid"]);
    }

    [Fact(Skip = "Parking lot — streaming blob path deferred (currently buffers to memory)")]
    public async Task Ingest_100MbStream_StreamsWithoutMemoryExplosion()
    {
        // Will exercise the spec §7.5 "streaming blob upload" path once IBlobStore grows a
        // Stream-accepting overload. Currently the pipeline copies the stream into a MemoryStream
        // before calling PutAsync, so a 100 MB upload peaks at 100 MB of managed memory.
        await Task.CompletedTask;
    }

    private sealed class StubExtractor(ImageryMetadata result) : IImageryMetadataExtractor
    {
        public ImageryMetadata Extract(Stream imageContent) => result;
    }

    private sealed class ThrowingExtractor : IImageryMetadataExtractor
    {
        public ImageryMetadata Extract(Stream imageContent)
            => throw new InvalidOperationException("synthetic extractor failure");
    }
}
