namespace Sunfish.Ingestion.Voice.Transcribers;

/// <summary>
/// Discriminator selecting which <see cref="IVoiceTranscriber"/> implementation DI should wire up.
/// </summary>
public enum TranscriberKind
{
    /// <summary>Deterministic in-process stub; returns a pre-supplied transcript.</summary>
    NoOp,

    /// <summary>OpenAI Whisper HTTPS adapter.</summary>
    OpenAiWhisper,

    /// <summary>Azure Cognitive Services Speech adapter.</summary>
    AzureSpeech,
}
