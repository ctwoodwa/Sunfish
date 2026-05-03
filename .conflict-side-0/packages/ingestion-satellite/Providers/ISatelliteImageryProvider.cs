namespace Sunfish.Ingestion.Satellite.Providers;

/// <summary>
/// Contract between the satellite ingestion pipeline and an external imagery source (Planet Labs,
/// Maxar, Sentinel Hub, USGS, etc.). Implementations are vendor-specific and live in downstream
/// adapter packages; the Sunfish default is <see cref="NoOpSatelliteImageryProvider"/>.
/// See Sunfish Platform spec §7.6.
/// </summary>
public interface ISatelliteImageryProvider
{
    /// <summary>
    /// Lists acquisitions intersecting <paramref name="bbox"/> and falling within the UTC
    /// time window <paramref name="fromUtc"/>..<paramref name="toUtc"/>. Implementations may
    /// cap result size; callers should assume results are not paged beyond the returned list.
    /// </summary>
    ValueTask<IReadOnlyList<SatelliteAcquisition>> ListAcquisitionsAsync(
        BoundingBox bbox,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct);

    /// <summary>
    /// Downloads the raw bytes for one acquisition. Returned stream is caller-owned — the
    /// pipeline disposes it after the bytes are archived to <c>IBlobStore</c>.
    /// </summary>
    ValueTask<Stream> DownloadAsync(
        SatelliteAcquisition acquisition,
        CancellationToken ct);

    /// <summary>
    /// Fetches provider-specific metadata for the acquisition (sensor id, bands, processing
    /// level, etc.). The dictionary shape is intentionally unconstrained — it is stamped onto
    /// the entity body as <c>providerMetadata</c> without interpretation.
    /// </summary>
    ValueTask<IReadOnlyDictionary<string, object?>> GetMetadataAsync(
        SatelliteAcquisition acquisition,
        CancellationToken ct);
}
