using Sunfish.Ingestion.Voice;
using Sunfish.Ingestion.Voice.Transcribers;
using Xunit;

namespace Sunfish.Ingestion.Voice.Tests;

public class NoOpTranscriberTests
{
    private static AudioBlob Audio() =>
        new(new MemoryStream(new byte[] { 1, 2, 3 }), "clip.wav", "audio/wav", "sunfish.voice.clip/1");

    [Fact]
    public async Task TranscribeAsync_ReturnsPreSuppliedTranscript_Verbatim()
    {
        var transcriber = new NoOpTranscriber("the quick brown fox", "en");

        var result = await transcriber.TranscribeAsync(Audio(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("the quick brown fox", result.Value!.Transcript);
        Assert.Equal("en", result.Value.LanguageCode);
        Assert.Empty(result.Value.Segments);
    }

    [Fact]
    public async Task TranscribeAsync_WithEmptyTranscript_ReturnsSuccessWithEmptyString()
    {
        var transcriber = new NoOpTranscriber("");

        var result = await transcriber.TranscribeAsync(Audio(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("", result.Value!.Transcript);
        Assert.Equal("en", result.Value.LanguageCode);
    }
}
