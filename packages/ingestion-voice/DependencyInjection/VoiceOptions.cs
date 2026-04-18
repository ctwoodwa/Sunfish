namespace Sunfish.Ingestion.Voice.DependencyInjection;

/// <summary>
/// Options bag for <see cref="VoiceServiceCollectionExtensions.WithVoice"/>. Selects the active
/// <see cref="Transcribers.TranscriberKind"/> and carries provider-specific credentials.
/// </summary>
public sealed class VoiceOptions
{
    /// <summary>Which transcriber implementation to wire up.</summary>
    public Transcribers.TranscriberKind Kind { get; set; } = Transcribers.TranscriberKind.NoOp;

    /// <summary>API key or subscription key (required for <c>OpenAiWhisper</c> and <c>AzureSpeech</c>).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Azure region identifier (required for <c>AzureSpeech</c>, e.g. <c>eastus</c>).</summary>
    public string? AzureRegion { get; set; }

    /// <summary>Pre-supplied transcript for the <c>NoOp</c> transcriber.</summary>
    public string? NoOpTranscript { get; set; }

    /// <summary>Language code the <c>NoOp</c> transcriber reports (default: <c>en</c>).</summary>
    public string NoOpLanguage { get; set; } = "en";
}
