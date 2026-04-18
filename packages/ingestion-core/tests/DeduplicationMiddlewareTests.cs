using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Core.Middleware;
using Xunit;

namespace Sunfish.Ingestion.Core.Tests;

public class DeduplicationMiddlewareTests
{
    private static IngestedEntity StubEntity() =>
        new("e1", "schema/v1",
            new Dictionary<string, object?>(),
            Array.Empty<IngestedEvent>(),
            Array.Empty<Sunfish.Foundation.Blobs.Cid>());

    private static IngestionContext Ctx() =>
        IngestionContext.NewCorrelation("tenant-a", "actor-a");

    private sealed class RecordingTerminal
    {
        public int Calls { get; private set; }

        public IngestionDelegate<string> AsDelegate() => (_, _, _) =>
        {
            Calls++;
            return new ValueTask<IngestionResult<IngestedEntity>>(
                IngestionResult<IngestedEntity>.Success(StubEntity()));
        };
    }

    [Fact]
    public async Task InvokeAsync_FirstCall_PassesThrough()
    {
        var terminal = new RecordingTerminal();
        var mw = new DeduplicationMiddleware<string>(s => s, TimeSpan.FromMinutes(5));

        var r = await mw.InvokeAsync("k1", Ctx(), terminal.AsDelegate(), CancellationToken.None);

        Assert.True(r.IsSuccess);
        Assert.Equal(1, terminal.Calls);
    }

    [Fact]
    public async Task InvokeAsync_SecondCallWithSameKey_ReturnsDuplicateOutcome()
    {
        var terminal = new RecordingTerminal();
        var mw = new DeduplicationMiddleware<string>(s => s, TimeSpan.FromMinutes(5));

        var r1 = await mw.InvokeAsync("k1", Ctx(), terminal.AsDelegate(), CancellationToken.None);
        var r2 = await mw.InvokeAsync("k1", Ctx(), terminal.AsDelegate(), CancellationToken.None);

        Assert.True(r1.IsSuccess);
        Assert.False(r2.IsSuccess);
        Assert.Equal(IngestOutcome.Duplicate, r2.Outcome);
        Assert.Equal(1, terminal.Calls); // next was only called once, for the first input
    }

    [Fact]
    public async Task InvokeAsync_SecondCallAfterWindowExpiry_PassesThroughAgain()
    {
        // Precision note: this test is coarse-grained by design — the middleware uses wall-clock
        // DateTime.UtcNow internally. A ~3x ratio between delay and window is typically stable on
        // CI. If flakiness appears, switch to an injectable TimeProvider.
        var terminal = new RecordingTerminal();
        var window = TimeSpan.FromMilliseconds(50);
        var mw = new DeduplicationMiddleware<string>(s => s, window);

        var r1 = await mw.InvokeAsync("k1", Ctx(), terminal.AsDelegate(), CancellationToken.None);
        await Task.Delay(150);
        var r2 = await mw.InvokeAsync("k1", Ctx(), terminal.AsDelegate(), CancellationToken.None);

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);
        Assert.Equal(2, terminal.Calls);
    }
}
