namespace Sunfish.Ingestion.Voice;

/// <summary>
/// A transcription produced by an <see cref="Transcribers.IVoiceTranscriber"/>.
/// </summary>
/// <param name="Transcript">The full transcribed text.</param>
/// <param name="Segments">Time-aligned segments, or empty when the provider does not return them.</param>
/// <param name="LanguageCode">Detected or assumed language code (e.g. <c>en</c>).</param>
public sealed record TranscriptionResult(
    string Transcript,
    IReadOnlyList<TranscriptSegment> Segments,
    string LanguageCode);

/// <summary>
/// A single time-aligned fragment of a transcript.
/// </summary>
/// <param name="StartSeconds">Start offset from the beginning of the audio, in seconds.</param>
/// <param name="EndSeconds">End offset from the beginning of the audio, in seconds.</param>
/// <param name="Text">Transcribed text for this segment.</param>
public sealed record TranscriptSegment(
    double StartSeconds,
    double EndSeconds,
    string Text);
