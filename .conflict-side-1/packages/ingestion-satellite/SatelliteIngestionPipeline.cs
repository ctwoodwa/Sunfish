using Sunfish.Foundation.Blobs;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Satellite.Providers;

namespace Sunfish.Ingestion.Satellite;

/// <summary>
/// Ingestion pipeline for satellite scenes. Given a <see cref="SatelliteAcquisition"/> handle it
/// downloads the raw bytes via the configured <see cref="ISatelliteImageryProvider"/>, archives
/// them to <see cref="IBlobStore"/>, copies the provider's metadata onto the body, and emits
/// a single <c>satellite.ingested</c> event on the minted entity. See Sunfish Platform spec §7.6.
/// </summary>
public sealed class SatelliteIngestionPipeline(
    IBlobStore blobs,
    ISatelliteImageryProvider provider)
    : IIngestionPipeline<SatelliteAcquisition>
{
    /// <inheritdoc/>
    public async ValueTask<IngestionResult<IngestedEntity>> IngestAsync(
        SatelliteAcquisition input,
        IngestionContext context,
        CancellationToken ct = default)
    {
        // 1. Download from provider. NotSupportedException maps to ProviderUnavailable (no provider
        //    registered); any other exception maps to ProviderFailed.
        Stream content;
        try
        {
            content = await provider.DownloadAsync(input, ct);
        }
        catch (NotSupportedException ex)
        {
            return IngestionResult<IngestedEntity>.Fail(
                IngestOutcome.ProviderUnavailable, ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return IngestionResult<IngestedEntity>.Fail(
                IngestOutcome.ProviderFailed, ex.Message);
        }

        using (content)
        {
            byte[] bytes;
            try
            {
                using var ms = new MemoryStream();
                await content.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return IngestionResult<IngestedEntity>.Fail(
                    IngestOutcome.ProviderFailed, $"Failed to read satellite stream: {ex.Message}");
            }

            if (bytes.Length == 0)
            {
                return IngestionResult<IngestedEntity>.Fail(
                    IngestOutcome.ProviderFailed, "Provider returned empty stream.");
            }

            // 2. Archive raw bytes (content-addressed dedup).
            var cid = await blobs.PutAsync(bytes, ct);

            // 3. Pull provider-side metadata. Failures here are treated as ProviderFailed rather
            //    than swallowed — the scene has been downloaded, so we know the provider is up;
            //    a metadata call that then fails is likely a real provider bug.
            IReadOnlyDictionary<string, object?> providerMetadata;
            try
            {
                providerMetadata = await provider.GetMetadataAsync(input, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return IngestionResult<IngestedEntity>.Fail(
                    IngestOutcome.ProviderFailed, $"Failed to fetch provider metadata: {ex.Message}");
            }

            var body = new Dictionary<string, object?>
            {
                ["providerId"] = input.ProviderId,
                ["acquisitionId"] = input.AcquisitionId,
                ["acquiredUtc"] = input.AcquiredUtc,
                ["bboxMinLat"] = input.Bbox.MinLat,
                ["bboxMinLong"] = input.Bbox.MinLong,
                ["bboxMaxLat"] = input.Bbox.MaxLat,
                ["bboxMaxLong"] = input.Bbox.MaxLong,
                ["cloudCoverPct"] = input.CloudCoverPct,
                ["imageBlobCid"] = cid.Value,
                ["providerMetadata"] = providerMetadata,
            };

            var events = new[]
            {
                new IngestedEvent("satellite.ingested", body, DateTime.UtcNow),
            };

            return IngestionResult<IngestedEntity>.Success(new IngestedEntity(
                EntityId: Guid.NewGuid().ToString("n"),
                SchemaId: input.SchemaId,
                Body: body,
                Events: events,
                BlobCids: new[] { cid }));
        }
    }
}
