using Microsoft.Extensions.DependencyInjection;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Core.DependencyInjection;
using Sunfish.Ingestion.Voice.Transcribers;

namespace Sunfish.Ingestion.Voice.DependencyInjection;

/// <summary>
/// DI extensions for registering the voice ingestion pipeline and its selected transcriber.
/// </summary>
public static class VoiceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IVoiceTranscriber"/> (per <see cref="VoiceOptions.Kind"/>) and the
    /// <see cref="VoiceIngestionPipeline"/> on the given <paramref name="builder"/>.
    /// </summary>
    public static IngestionBuilder WithVoice(
        this IngestionBuilder builder,
        Action<VoiceOptions> configure)
    {
        var options = new VoiceOptions();
        configure(options);
        builder.Services.AddSingleton(options);

        builder.Services.AddSingleton<IVoiceTranscriber>(sp =>
        {
            var o = sp.GetRequiredService<VoiceOptions>();
            return o.Kind switch
            {
                TranscriberKind.OpenAiWhisper => new OpenAiWhisperTranscriber(
                    sp.GetRequiredService<HttpClient>(),
                    o.ApiKey ?? throw new InvalidOperationException("VoiceOptions.ApiKey required for OpenAiWhisper.")),
                TranscriberKind.AzureSpeech => new AzureSpeechTranscriber(
                    sp.GetRequiredService<HttpClient>(),
                    o.AzureRegion ?? throw new InvalidOperationException("VoiceOptions.AzureRegion required for AzureSpeech."),
                    o.ApiKey ?? throw new InvalidOperationException("VoiceOptions.ApiKey required for AzureSpeech.")),
                TranscriberKind.NoOp or _ => new NoOpTranscriber(o.NoOpTranscript ?? "", o.NoOpLanguage),
            };
        });

        builder.Services.AddSingleton<IIngestionPipeline<AudioBlob>, VoiceIngestionPipeline>();
        return builder;
    }
}
