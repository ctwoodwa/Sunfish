using Sunfish.Ingestion.Core;

namespace Sunfish.Ingestion.Voice.Transcribers;

/// <summary>
/// Abstraction over a speech-to-text provider. Implementations translate an
/// <see cref="AudioBlob"/> into a <see cref="TranscriptionResult"/> or a structured
/// <see cref="IngestionResult{T}"/> failure.
/// </summary>
public interface IVoiceTranscriber
{
    /// <summary>
    /// Transcribes the given audio. Implementations must map transient provider issues to
    /// <see cref="IngestOutcome.ProviderUnavailable"/> and non-recoverable errors to
    /// <see cref="IngestOutcome.ProviderFailed"/>.
    /// </summary>
    ValueTask<IngestionResult<TranscriptionResult>> TranscribeAsync(
        AudioBlob audio, CancellationToken ct);
}
