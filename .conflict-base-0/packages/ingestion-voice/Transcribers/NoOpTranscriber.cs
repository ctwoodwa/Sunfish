using Sunfish.Ingestion.Core;

namespace Sunfish.Ingestion.Voice.Transcribers;

/// <summary>
/// Deterministic in-process <see cref="IVoiceTranscriber"/> that returns a pre-supplied
/// transcript without inspecting the audio stream. Useful for tests and offline demos.
/// </summary>
public sealed class NoOpTranscriber(string preSuppliedTranscript, string language = "en") : IVoiceTranscriber
{
    /// <inheritdoc/>
    public ValueTask<IngestionResult<TranscriptionResult>> TranscribeAsync(AudioBlob audio, CancellationToken ct)
    {
        var result = new TranscriptionResult(
            Transcript: preSuppliedTranscript,
            Segments: Array.Empty<TranscriptSegment>(),
            LanguageCode: language);
        return ValueTask.FromResult(IngestionResult<TranscriptionResult>.Success(result));
    }
}
