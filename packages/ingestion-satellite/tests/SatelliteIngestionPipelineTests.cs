using Sunfish.Foundation.Blobs;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Satellite;
using Sunfish.Ingestion.Satellite.Providers;
using Xunit;

namespace Sunfish.Ingestion.Satellite.Tests;

public class SatelliteIngestionPipelineTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileSystemBlobStore _blobs;

    public SatelliteIngestionPipelineTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "sunfish-satellite-" + Path.GetRandomFileName());
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

    private static SatelliteAcquisition SampleAcquisition() => new(
        ProviderId: "stub",
        AcquisitionId: "scene-42",
        AcquiredUtc: new DateTime(2024, 3, 10, 11, 15, 0, DateTimeKind.Utc),
        Bbox: new BoundingBox(37.0, -122.5, 37.8, -121.9),
        CloudCoverPct: 12.5,
        SchemaId: "sunfish.satellite/1");

    [Fact]
    public async Task Ingest_HappyPath_ProducesEntityWithCidAndBbox()
    {
        var provider = new StubProvider
        {
            Metadata = new Dictionary<string, object?> { ["cloudCover"] = 12.5 },
        };
        var pipeline = new SatelliteIngestionPipeline(_blobs, provider);
        var acquisition = SampleAcquisition();

        var result = await pipeline.IngestAsync(acquisition, Ctx());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("sunfish.satellite/1", result.Value!.SchemaId);
        Assert.Single(result.Value.BlobCids);
        Assert.Single(result.Value.Events);
        Assert.Equal("satellite.ingested", result.Value.Events[0].Kind);
        Assert.Equal("stub", result.Value.Body["providerId"]);
        Assert.Equal("scene-42", result.Value.Body["acquisitionId"]);
        Assert.Equal(37.0, result.Value.Body["bboxMinLat"]);
        Assert.Equal(-122.5, result.Value.Body["bboxMinLong"]);
        Assert.Equal(37.8, result.Value.Body["bboxMaxLat"]);
        Assert.Equal(-121.9, result.Value.Body["bboxMaxLong"]);
        Assert.Equal(12.5, result.Value.Body["cloudCoverPct"]);
        Assert.NotNull(result.Value.Body["imageBlobCid"]);

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.Value.Body["providerMetadata"]);
        Assert.Equal(12.5, metadata["cloudCover"]);
    }

    [Fact]
    public async Task Ingest_WithNoOpProvider_ReturnsProviderUnavailable()
    {
        var provider = new NoOpSatelliteImageryProvider();
        var pipeline = new SatelliteIngestionPipeline(_blobs, provider);

        var result = await pipeline.IngestAsync(SampleAcquisition(), Ctx());

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestOutcome.ProviderUnavailable, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Contains("NoOp", result.Failure!.Message);
    }

    [Fact]
    public async Task Ingest_ProviderThrows_ReturnsProviderFailed()
    {
        var provider = new StubProvider { ThrowOnDownload = true };
        var pipeline = new SatelliteIngestionPipeline(_blobs, provider);

        var result = await pipeline.IngestAsync(SampleAcquisition(), Ctx());

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestOutcome.ProviderFailed, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Contains("fake provider failure", result.Failure!.Message);
    }

    internal sealed class StubProvider : ISatelliteImageryProvider
    {
        public Func<Stream>? StreamFactory { get; init; }
        public bool ThrowOnDownload { get; init; }
        public IReadOnlyDictionary<string, object?> Metadata { get; init; } =
            new Dictionary<string, object?>();

        public ValueTask<IReadOnlyList<SatelliteAcquisition>> ListAcquisitionsAsync(
            BoundingBox bbox, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
            => ValueTask.FromResult<IReadOnlyList<SatelliteAcquisition>>(Array.Empty<SatelliteAcquisition>());

        public ValueTask<Stream> DownloadAsync(SatelliteAcquisition acquisition, CancellationToken ct)
        {
            if (ThrowOnDownload)
            {
                throw new InvalidOperationException("fake provider failure");
            }
            return ValueTask.FromResult(
                StreamFactory?.Invoke() ?? new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }));
        }

        public ValueTask<IReadOnlyDictionary<string, object?>> GetMetadataAsync(
            SatelliteAcquisition acquisition, CancellationToken ct)
            => ValueTask.FromResult(Metadata);
    }
}
