using Sunfish.Foundation.Blobs;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Imagery.Metadata;

namespace Sunfish.Ingestion.Imagery;

/// <summary>
/// Ingestion pipeline for drone/robot imagery uploads. Streams the image into <see cref="IBlobStore"/>
/// via <see cref="IBlobStore.PutStreamingAsync"/> (content-addressed, no full in-memory buffering when
/// the backend supports streaming), then runs <see cref="IImageryMetadataExtractor"/> against a seeked
/// or buffered view over the same bytes, and finally emits a single <c>imagery.ingested</c> event on
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
        // 1. Stream the raw bytes into blob store (spec §7.5 streaming upload path).
        //    PutStreamingAsync consumes the stream once; backends that can stream natively
        //    (e.g. FileSystemBlobStore) avoid buffering the full payload in managed memory.
        //    The stream is then re-read from the beginning for metadata extraction below.
        if (!input.Content.CanSeek)
        {
            // Non-seekable source: buffer once so metadata extraction can re-read.
            using var ms = new MemoryStream();
            await input.Content.CopyToAsync(ms, ct);

            if (ms.Length == 0)
            {
                return IngestionResult<IngestedEntity>.Fail(
                    IngestOutcome.ValidationFailed, "Image stream is empty.");
            }

            ms.Position = 0;
            var cidNs = await blobs.PutStreamingAsync(ms, ct);
            ms.Position = 0;
            return await FinishIngestAsync(input, cidNs, ms, ct);
        }

        if (input.Content.Length == 0)
        {
            return IngestionResult<IngestedEntity>.Fail(
                IngestOutcome.ValidationFailed, "Image stream is empty.");
        }

        var cid = await blobs.PutStreamingAsync(input.Content, ct);

        // Seek back so metadata extraction can re-read from the start.
        input.Content.Position = 0;
        return await FinishIngestAsync(input, cid, input.Content, ct);
    }

    private ValueTask<IngestionResult<IngestedEntity>> FinishIngestAsync(
        ImageUpload input,
        Cid cid,
        Stream contentForMetadata,
        CancellationToken ct)
    {
        _ = ct; // reserved for future async metadata extraction

        // 2. Metadata extraction. Never fatal.
        ImageryMetadata metadata;
        try
        {
            metadata = extractor.Extract(contentForMetadata);
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

        return ValueTask.FromResult(IngestionResult<IngestedEntity>.Success(new IngestedEntity(
            EntityId: Guid.NewGuid().ToString("n"),
            SchemaId: input.SchemaId,
            Body: body,
            Events: events,
            BlobCids: new[] { cid })));
    }
}
