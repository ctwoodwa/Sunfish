namespace Sunfish.Ingestion.Satellite.Providers;

/// <summary>
/// Safe-by-default <see cref="ISatelliteImageryProvider"/> that returns empty results from
/// <see cref="ListAcquisitionsAsync"/> / <see cref="GetMetadataAsync"/> and throws
/// <see cref="NotSupportedException"/> from <see cref="DownloadAsync"/> with an actionable
/// message directing consumers to register a real provider.
/// </summary>
public sealed class NoOpSatelliteImageryProvider : ISatelliteImageryProvider
{
    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SatelliteAcquisition>> ListAcquisitionsAsync(
        BoundingBox bbox, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        => ValueTask.FromResult<IReadOnlyList<SatelliteAcquisition>>(Array.Empty<SatelliteAcquisition>());

    /// <inheritdoc/>
    public ValueTask<Stream> DownloadAsync(SatelliteAcquisition acquisition, CancellationToken ct)
        => throw new NotSupportedException(
            "NoOp satellite provider — register a real ISatelliteImageryProvider (e.g., Planet Labs, Maxar, Sentinel Hub) before calling DownloadAsync.");

    /// <inheritdoc/>
    public ValueTask<IReadOnlyDictionary<string, object?>> GetMetadataAsync(
        SatelliteAcquisition acquisition, CancellationToken ct)
        => ValueTask.FromResult<IReadOnlyDictionary<string, object?>>(
            new Dictionary<string, object?>());
}
