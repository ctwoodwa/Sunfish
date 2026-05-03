using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Sunfish.Ingestion.Core;

namespace Sunfish.Ingestion.Voice.Transcribers;

/// <summary>
/// <see cref="IVoiceTranscriber"/> that posts audio to the Azure Speech short-audio REST
/// endpoint (<c>/speech/recognition/conversation/cognitiveservices/v1</c>). Maps HTTP 429 to
/// <see cref="IngestOutcome.ProviderUnavailable"/> and all other non-2xx to
/// <see cref="IngestOutcome.ProviderFailed"/>.
/// </summary>
public sealed class AzureSpeechTranscriber(HttpClient http, string region, string subscriptionKey) : IVoiceTranscriber
{
    /// <inheritdoc/>
    public async ValueTask<IngestionResult<TranscriptionResult>> TranscribeAsync(AudioBlob audio, CancellationToken ct)
    {
        var url = $"https://{region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language=en-US";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var streamContent = new StreamContent(audio.Content);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(audio.ContentType);
        request.Content = streamContent;
        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", subscriptionKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            return IngestionResult<TranscriptionResult>.Fail(IngestOutcome.ProviderUnavailable, ex.Message);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return IngestionResult<TranscriptionResult>.Fail(IngestOutcome.ProviderUnavailable, "Azure Speech rate limit exceeded.");
            }
            if (!response.IsSuccessStatusCode)
            {
                return IngestionResult<TranscriptionResult>.Fail(IngestOutcome.ProviderFailed, $"Azure Speech HTTP {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var status = root.TryGetProperty("RecognitionStatus", out var s) ? s.GetString() ?? "" : "";
                if (!string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase))
                {
                    return IngestionResult<TranscriptionResult>.Fail(IngestOutcome.ProviderFailed, $"Azure Speech recognition status: {status}.");
                }

                var transcript = root.TryGetProperty("DisplayText", out var d) ? d.GetString() ?? "" : "";
                // Duration is in 100-nanosecond units (ticks); 10_000_000 ticks = 1 second.
                double durationSeconds = 0;
                if (root.TryGetProperty("Duration", out var dur) && dur.TryGetInt64(out var ticks))
                {
                    durationSeconds = ticks / 10_000_000d;
                }

                var segments = new List<TranscriptSegment>
                {
                    new(StartSeconds: 0, EndSeconds: durationSeconds, Text: transcript),
                };

                return IngestionResult<TranscriptionResult>.Success(new TranscriptionResult(transcript, segments, "en-US"));
            }
            catch (JsonException ex)
            {
                return IngestionResult<TranscriptionResult>.Fail(IngestOutcome.ProviderFailed, $"Azure Speech response parse error: {ex.Message}");
            }
        }
    }
}
