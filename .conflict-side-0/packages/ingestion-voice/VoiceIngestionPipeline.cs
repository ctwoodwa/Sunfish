using Sunfish.Foundation.Blobs;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Voice.Transcribers;

namespace Sunfish.Ingestion.Voice;

/// <summary>
/// Ingestion pipeline for audio payloads. Persists the raw audio bytes to the injected
/// <see cref="IBlobStore"/>, invokes the configured <see cref="IVoiceTranscriber"/>, and mints
/// a single <see cref="IngestedEntity"/> carrying the blob CID, transcript, and a
/// <c>voice.ingested</c> event.
/// </summary>
public sealed class VoiceIngestionPipeline(
    IBlobStore blobs, IVoiceTranscriber transcriber)
    : IIngestionPipeline<AudioBlob>
{
    /// <inheritdoc/>
    public async ValueTask<IngestionResult<IngestedEntity>> IngestAsync(
        AudioBlob input, IngestionContext context, CancellationToken ct = default)
    {
        // 1. Read all audio bytes (streaming to blob store, buffering for re-use by transcriber).
        using var ms = new MemoryStream();
        await input.Content.CopyToAsync(ms, ct);
        var audioBytes = ms.ToArray();

        if (audioBytes.Length == 0)
        {
            return IngestionResult<IngestedEntity>.Fail(IngestOutcome.ValidationFailed, "Audio stream is empty.");
        }

        // 2. Persist to blob store (D-BLOB-BOUNDARY: audio always > 64 KiB in real use;
        //    tests may pass smaller samples — pipeline always routes via blob store).
        var cid = await blobs.PutAsync(audioBytes, ct);

        // 3. Transcribe using a fresh stream view of the buffered bytes.
        var audioForTranscriber = new AudioBlob(
            Content: new MemoryStream(audioBytes, writable: false),
            Filename: input.Filename,
            ContentType: input.ContentType,
            SchemaId: input.SchemaId);

        var transcription = await transcriber.TranscribeAsync(audioForTranscriber, ct);
        if (!transcription.IsSuccess)
        {
            return IngestionResult<IngestedEntity>.Fail(
                transcription.Outcome,
                transcription.Failure!.Message,
                transcription.Failure.Details);
        }

        var result = transcription.Value!;

        var body = new Dictionary<string, object?>
        {
            ["audioBlobCid"] = cid.Value,
            ["transcript"] = result.Transcript,
            ["languageCode"] = result.LanguageCode,
            ["segmentCount"] = result.Segments.Count,
            ["capturedAtUtc"] = DateTime.UtcNow,
        };

        var events = new[]
        {
            new IngestedEvent("voice.ingested", body, DateTime.UtcNow),
        };

        var entity = new IngestedEntity(
            EntityId: Guid.NewGuid().ToString("n"),
            SchemaId: input.SchemaId,
            Body: body,
            Events: events,
            BlobCids: new[] { cid });

        return IngestionResult<IngestedEntity>.Success(entity);
    }
}
