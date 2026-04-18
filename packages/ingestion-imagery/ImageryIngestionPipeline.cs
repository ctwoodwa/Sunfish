using Sunfish.Foundation.Blobs;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Imagery.Metadata;

namespace Sunfish.Ingestion.Imagery;

/// <summary>
/// Ingestion pipeline for drone/robot imagery uploads. Buffers the image into <see cref="IBlobStore"/>
/// first (content-addressed), then runs <see cref="IImageryMetadataExtractor"/> against a fresh
/// stream view over the same bytes, and finally emits a single <c>imagery.ingested</c> event on
/// the minted entity. Metadata extraction failures are non-fatal — the entity is still produced
/// with all-null metadata fields. See Sunfish Platform spec §7.5.
/// </summary>
public sealed class ImageryIngestionPipeline(
    IBlobStore blobs,
    IImageryMetadataExtractor extractor)
    : IIngestionPipeline<ImageUpload>
{
    /// <inheritdoc/>
    public async ValueTask<IngestionResult<IngestedEntity>> IngestAsync(
        ImageUpload input,
        IngestionContext context,
        CancellationToken ct = default)
    {
        // 1. Buffer the raw bytes into blob store (image archival, content-addressed dedup).
        using var ms = new MemoryStream();
        await input.Content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (bytes.Length == 0)
        {
            return IngestionResult<IngestedEntity>.Fail(
                IngestOutcome.ValidationFailed, "Image stream is empty.");
        }

        var cid = await blobs.PutAsync(bytes, ct);

        // 2. Metadata extraction uses a fresh MemoryStream view. Never fatal.
        ImageryMetadata metadata;
        try
        {
            using var metaStream = new MemoryStream(bytes, writable: false);
            metadata = extractor.Extract(metaStream);
        }
        catch
        {
            // Belt-and-suspenders: even a non-MetadataExtractor exception should not fail ingest.
            metadata = ImageryMetadata.Empty;
        }

        var body = new Dictionary<string, object?>
        {
            ["imageBlobCid"] = cid.Value,
            ["filename"] = input.Filename,
            ["contentType"] = input.ContentType,
            ["gpsLatitude"] = metadata.GpsLatitude,
            ["gpsLongitude"] = metadata.GpsLongitude,
            ["capturedUtc"] = metadata.CapturedUtc,
            ["cameraMake"] = metadata.CameraMake,
            ["cameraModel"] = metadata.CameraModel,
            ["focalLengthMm"] = metadata.FocalLengthMm,
            ["widthPx"] = metadata.WidthPx,
            ["heightPx"] = metadata.HeightPx,
        };

        var events = new[]
        {
            new IngestedEvent("imagery.ingested", body, DateTime.UtcNow),
        };

        return IngestionResult<IngestedEntity>.Success(new IngestedEntity(
            EntityId: Guid.NewGuid().ToString("n"),
            SchemaId: input.SchemaId,
            Body: body,
            Events: events,
            BlobCids: new[] { cid }));
    }
}
