using Sunfish.Foundation.Blobs;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Voice;
using Sunfish.Ingestion.Voice.Transcribers;
using Xunit;

namespace Sunfish.Ingestion.Voice.Tests;

public class VoiceIngestionPipelineTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileSystemBlobStore _blobs;

    public VoiceIngestionPipelineTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "sunfish-voice-" + Path.GetRandomFileName());
        _blobs = new FileSystemBlobStore(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static IngestionContext Ctx() =>
        IngestionContext.NewCorrelation("tenant-1", "actor-1");

    private static AudioBlob Audio(byte[] bytes) =>
        new(new MemoryStream(bytes), "clip.wav", "audio/wav", "sunfish.voice.clip/1");

    private sealed class StubTranscriber(IngestionResult<TranscriptionResult> canned) : IVoiceTranscriber
    {
        public ValueTask<IngestionResult<TranscriptionResult>> TranscribeAsync(AudioBlob audio, CancellationToken ct)
            => ValueTask.FromResult(canned);
    }

    [Fact]
    public async Task IngestAsync_HappyPath_EntityBodyContainsCidAndTranscript()
    {
        var transcription = new TranscriptionResult("hello world", Array.Empty<TranscriptSegment>(), "en");
        var transcriber = new StubTranscriber(IngestionResult<TranscriptionResult>.Success(transcription));
        var pipeline = new VoiceIngestionPipeline(_blobs, transcriber);

        var result = await pipeline.IngestAsync(Audio(new byte[] { 1, 2, 3, 4 }), Ctx());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value!.Body["audioBlobCid"]);
        Assert.False(string.IsNullOrWhiteSpace((string)result.Value.Body["audioBlobCid"]!));
        Assert.Equal("hello world", result.Value.Body["transcript"]);
        Assert.Equal("en", result.Value.Body["languageCode"]);
        Assert.Single(result.Value.Events);
        Assert.Equal("voice.ingested", result.Value.Events[0].Kind);
        Assert.Single(result.Value.BlobCids);
    }

    [Fact]
    public async Task IngestAsync_TranscriberFailure_PropagatesOutcome()
    {
        var failure = IngestionResult<TranscriptionResult>.Fail(IngestOutcome.ProviderFailed, "oops");
        var transcriber = new StubTranscriber(failure);
        var pipeline = new VoiceIngestionPipeline(_blobs, transcriber);

        var result = await pipeline.IngestAsync(Audio(new byte[] { 1, 2, 3 }), Ctx());

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestOutcome.ProviderFailed, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Equal("oops", result.Failure!.Message);
    }

    [Fact]
    public async Task IngestAsync_AudioBytesPersisted_WithCorrectCid()
    {
        var bytes = new byte[100];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i & 0xff);

        var transcription = new TranscriptionResult("x", Array.Empty<TranscriptSegment>(), "en");
        var transcriber = new StubTranscriber(IngestionResult<TranscriptionResult>.Success(transcription));
        var pipeline = new VoiceIngestionPipeline(_blobs, transcriber);

        var result = await pipeline.IngestAsync(Audio(bytes), Ctx());

        Assert.True(result.IsSuccess);
        var cid = result.Value!.BlobCids[0];
        var stored = await _blobs.GetAsync(cid);
        Assert.NotNull(stored);
        Assert.Equal(bytes, stored!.Value.ToArray());
    }

    [Fact]
    public async Task IngestAsync_SameAudio_ProducesSameCid_Idempotent()
    {
        var bytes = new byte[] { 9, 8, 7, 6, 5 };
        var transcription = new TranscriptionResult("x", Array.Empty<TranscriptSegment>(), "en");
        var transcriber = new StubTranscriber(IngestionResult<TranscriptionResult>.Success(transcription));
        var pipeline = new VoiceIngestionPipeline(_blobs, transcriber);

        var result1 = await pipeline.IngestAsync(Audio(bytes), Ctx());
        var result2 = await pipeline.IngestAsync(Audio(bytes), Ctx());

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(result1.Value!.BlobCids[0], result2.Value!.BlobCids[0]);
    }

    [Fact]
    public async Task IngestAsync_EmptyAudio_ReturnsValidationFailed()
    {
        var transcription = new TranscriptionResult("x", Array.Empty<TranscriptSegment>(), "en");
        var transcriber = new StubTranscriber(IngestionResult<TranscriptionResult>.Success(transcription));
        var pipeline = new VoiceIngestionPipeline(_blobs, transcriber);

        var result = await pipeline.IngestAsync(Audio(Array.Empty<byte>()), Ctx());

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestOutcome.ValidationFailed, result.Outcome);
    }
}
