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

    [Fact]
    public async Task Ingest_100MbStream_StreamsWithoutMemoryExplosion()
    {
        // Spec §7.5 streaming blob upload path. FileSystemBlobStore.PutStreamingAsync streams
        // directly to disk — the pipeline should not buffer the full 100 MiB payload in managed
        // memory. The managed-allocation delta for the current thread is used as a soft guard.
        const int payloadBytes = 100 * 1024 * 1024; // 100 MiB
        const long memoryBudgetBytes = 8 * 1024 * 1024; // 8 MiB delta (generous: metadata + framing)

        var payload = new byte[payloadBytes];
        new Random(9876).NextBytes(payload);

        var extractor = new StubExtractor(ImageryMetadata.Empty);
        var pipeline = new ImageryIngestionPipeline(_blobs, extractor);

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();

        var upload = new ImageUpload(
            Content: new MemoryStream(payload, writable: false),
            Filename: "large.jpg",
            ContentType: "image/jpeg",
            SchemaId: "sunfish.imagery/1");

        var result = await pipeline.IngestAsync(upload, Ctx());

        long allocAfter = GC.GetAllocatedBytesForCurrentThread();
        long delta = allocAfter - allocBefore;

        Assert.True(result.IsSuccess, $"Ingest failed: {result.Failure?.Message}");
        Assert.True(
            delta < memoryBudgetBytes,
            $"Managed-memory delta was {delta:N0} bytes (budget: {memoryBudgetBytes:N0}, " +
            $"payload: {payloadBytes:N0}). The pipeline is likely still buffering the full payload.");
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
