using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Sunfish.Ingestion.Core;

namespace Sunfish.Ingestion.Voice.Transcribers;

/// <summary>
/// <see cref="IVoiceTranscriber"/> that posts audio to the OpenAI Whisper HTTP endpoint
/// (<c>/v1/audio/transcriptions</c>) using multipart form data and the <c>verbose_json</c>
/// response format. Maps HTTP 429 to <see cref="IngestOutcome.ProviderUnavailable"/> and all
/// other non-2xx to <see cref="IngestOutcome.ProviderFailed"/>.
/// </summary>
public sealed class OpenAiWhisperTranscriber(HttpClient http, string apiKey) : IVoiceTranscriber
{
    /// <inheritdoc/>
    public async ValueTask<IngestionResult<TranscriptionResult>> TranscribeAsync(AudioBlob audio, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(audio.Content);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(audio.ContentType);
        content.Add(streamContent, "file", audio.Filename);
        content.Add(new StringContent("whisper-1"), "model");
        content.Add(new StringContent("verbose_json"), "response_format");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

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
                return IngestionResult<TranscriptionResult>.Fail(IngestOutcome.ProviderUnavailable, "Whisper rate limit exceeded.");
            }
            if (!response.IsSuccessStatusCode)
            {
                return IngestionResult<TranscriptionResult>.Fail(IngestOutcome.ProviderFailed, $"Whisper HTTP {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var transcript = root.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                var language = root.TryGetProperty("language", out var l) ? l.GetString() ?? "" : "";
                var segments = new List<TranscriptSegment>();
                if (root.TryGetProperty("segments", out var segs) && segs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in segs.EnumerateArray())
                    {
                        segments.Add(new TranscriptSegment(
                            StartSeconds: s.GetProperty("start").GetDouble(),
                            EndSeconds: s.GetProperty("end").GetDouble(),
                            Text: s.GetProperty("text").GetString() ?? ""));
                    }
                }
                return IngestionResult<TranscriptionResult>.Success(new TranscriptionResult(transcript, segments, language));
            }
            catch (JsonException ex)
            {
                return IngestionResult<TranscriptionResult>.Fail(IngestOutcome.ProviderFailed, $"Whisper response parse error: {ex.Message}");
            }
        }
    }
}
