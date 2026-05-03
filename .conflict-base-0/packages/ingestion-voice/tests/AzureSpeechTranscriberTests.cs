using System.Net;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Voice;
using Sunfish.Ingestion.Voice.Transcribers;
using Xunit;

namespace Sunfish.Ingestion.Voice.Tests;

public class AzureSpeechTranscriberTests
{
    private static AudioBlob Audio() =>
        new(new MemoryStream(new byte[] { 1, 2, 3 }), "clip.wav", "audio/wav", "sunfish.voice.clip/1");

    private static HttpResponseMessage JsonResponse(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task TranscribeAsync_Happy200_ParsesResponse()
    {
        // Duration is in 100-nanosecond units: 1_000_000 = 0.1 seconds.
        const string payload = """{"RecognitionStatus":"Success","DisplayText":"hi","Duration":1000000}""";
        using var http = new HttpClient(new StubHandler(_ => JsonResponse(HttpStatusCode.OK, payload)));
        var transcriber = new AzureSpeechTranscriber(http, region: "eastus", subscriptionKey: "key-test");

        var result = await transcriber.TranscribeAsync(Audio(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("hi", result.Value!.Transcript);
        Assert.Single(result.Value.Segments);
        Assert.Equal("hi", result.Value.Segments[0].Text);
    }

    [Fact]
    public async Task TranscribeAsync_401_ReturnsProviderFailed()
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var transcriber = new AzureSpeechTranscriber(http, "eastus", "key-test");

        var result = await transcriber.TranscribeAsync(Audio(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestOutcome.ProviderFailed, result.Outcome);
        Assert.Contains("401", result.Failure!.Message);
    }

    [Fact]
    public async Task TranscribeAsync_429_ReturnsProviderUnavailable()
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        var transcriber = new AzureSpeechTranscriber(http, "eastus", "key-test");

        var result = await transcriber.TranscribeAsync(Audio(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestOutcome.ProviderUnavailable, result.Outcome);
    }

    [Fact]
    public async Task TranscribeAsync_MalformedJson_ReturnsProviderFailed()
    {
        using var http = new HttpClient(new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "not json")));
        var transcriber = new AzureSpeechTranscriber(http, "eastus", "key-test");

        var result = await transcriber.TranscribeAsync(Audio(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestOutcome.ProviderFailed, result.Outcome);
        Assert.Contains("parse", result.Failure!.Message, StringComparison.OrdinalIgnoreCase);
    }
}
