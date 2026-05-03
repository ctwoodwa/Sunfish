using Sunfish.Ingestion.Satellite;
using Sunfish.Ingestion.Satellite.Providers;
using Xunit;

namespace Sunfish.Ingestion.Satellite.Tests;

public class NoOpSatelliteImageryProviderTests
{
    private static readonly BoundingBox Bbox = new(-10, -10, 10, 10);
    private static readonly DateTime From = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc);

    private static SatelliteAcquisition SampleAcquisition() => new(
        ProviderId: "noop",
        AcquisitionId: "acq-1",
        AcquiredUtc: new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
        Bbox: Bbox,
        CloudCoverPct: 5.0,
        SchemaId: "sunfish.satellite/1");

    [Fact]
    public async Task ListAcquisitionsAsync_ReturnsEmpty()
    {
        var sut = new NoOpSatelliteImageryProvider();

        var result = await sut.ListAcquisitionsAsync(Bbox, From, To, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task DownloadAsync_ThrowsNotSupportedException_WithActionableMessage()
    {
        var sut = new NoOpSatelliteImageryProvider();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            async () => await sut.DownloadAsync(SampleAcquisition(), CancellationToken.None));

        Assert.Contains("register a real", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMetadataAsync_ReturnsEmptyDictionary()
    {
        var sut = new NoOpSatelliteImageryProvider();

        var result = await sut.GetMetadataAsync(SampleAcquisition(), CancellationToken.None);

        Assert.Empty(result);
    }
}
