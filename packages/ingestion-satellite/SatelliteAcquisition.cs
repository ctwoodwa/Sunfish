namespace Sunfish.Ingestion.Satellite;

/// <summary>
/// Axis-aligned WGS-84 bounding box used to scope satellite acquisition queries.
/// </summary>
/// <param name="MinLat">Minimum latitude in decimal degrees (southern edge).</param>
/// <param name="MinLong">Minimum longitude in decimal degrees (western edge).</param>
/// <param name="MaxLat">Maximum latitude in decimal degrees (northern edge).</param>
/// <param name="MaxLong">Maximum longitude in decimal degrees (eastern edge).</param>
public sealed record BoundingBox(double MinLat, double MinLong, double MaxLat, double MaxLong);

/// <summary>
/// A provider-specific handle to a single satellite imagery acquisition. The platform does not
/// prescribe which constellation/vendor/resolution — only the minimum metadata required to
/// ingest one scene. See Sunfish Platform spec §7.6.
/// </summary>
/// <param name="ProviderId">The provider namespace (e.g. <c>planet</c>, <c>maxar</c>, <c>sentinel-hub</c>).</param>
/// <param name="AcquisitionId">The provider's opaque id for the scene.</param>
/// <param name="AcquiredUtc">UTC capture timestamp.</param>
/// <param name="Bbox">The scene's spatial footprint.</param>
/// <param name="CloudCoverPct">Estimated cloud cover (0.0–100.0).</param>
/// <param name="SchemaId">Schema id to stamp on the resulting entity.</param>
public sealed record SatelliteAcquisition(
    string ProviderId,
    string AcquisitionId,
    DateTime AcquiredUtc,
    BoundingBox Bbox,
    double CloudCoverPct,
    string SchemaId);
