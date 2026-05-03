using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Core.Middleware;
using Xunit;

namespace Sunfish.Ingestion.Core.Tests;

public class MiddlewareChainTests
{
    private static IngestedEntity StubEntity() =>
        new("e1", "schema/v1",
            new Dictionary<string, object?>(),
            Array.Empty<IngestedEvent>(),
            Array.Empty<Sunfish.Foundation.Blobs.Cid>());

    private static IngestionContext Ctx() =>
        IngestionContext.NewCorrelation("tenant-a", "actor-a");

    /// <summary>
    /// Middleware that appends its name to a shared list before and after calling next —
    /// lets tests assert ordering around the terminal.
    /// </summary>
    private sealed class RecordingMiddleware(string name, List<string> log) : IIngestionMiddleware<string>
    {
        public async ValueTask<IngestionResult<IngestedEntity>> InvokeAsync(
            string input, IngestionContext context, IngestionDelegate<string> next, CancellationToken ct)
        {
            log.Add($"{name}:before");
            var r = await next(input, context, ct);
            log.Add($"{name}:after");
            return r;
        }
    }

    /// <summary>Middleware that returns a Fail without ever calling next.</summary>
    private sealed class ShortCircuitMiddleware : IIngestionMiddleware<string>
    {
        public ValueTask<IngestionResult<IngestedEntity>> InvokeAsync(
            string input, IngestionContext context, IngestionDelegate<string> next, CancellationToken ct) =>
            new(IngestionResult<IngestedEntity>.Fail(IngestOutcome.Quarantined, "blocked"));
    }

    private sealed class ThrowingMiddleware : IIngestionMiddleware<string>
    {
        public ValueTask<IngestionResult<IngestedEntity>> InvokeAsync(
            string input, IngestionContext context, IngestionDelegate<string> next, CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task Build_WithNoMiddleware_InvokesTerminalDirectly()
    {
        var terminalCalls = 0;
        IngestionDelegate<string> terminal = (_, _, _) =>
        {
            terminalCalls++;
            return new ValueTask<IngestionResult<IngestedEntity>>(
                IngestionResult<IngestedEntity>.Success(StubEntity()));
        };

        var pipeline = new IngestionPipelineBuilder<string>().Build(terminal);
        var r = await pipeline("hello", Ctx(), CancellationToken.None);

        Assert.Equal(1, terminalCalls);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task Build_WithTwoMiddlewares_InvokesInOrder()
    {
        var log = new List<string>();
        IngestionDelegate<string> terminal = (_, _, _) =>
        {
            log.Add("terminal");
            return new ValueTask<IngestionResult<IngestedEntity>>(
                IngestionResult<IngestedEntity>.Success(StubEntity()));
        };

        var pipeline = new IngestionPipelineBuilder<string>()
            .Use(new RecordingMiddleware("a", log))
            .Use(new RecordingMiddleware("b", log))
            .Build(terminal);

        await pipeline("hello", Ctx(), CancellationToken.None);

        Assert.Equal(
            new[] { "a:before", "b:before", "terminal", "b:after", "a:after" },
            log);
    }

    [Fact]
    public async Task Build_MiddlewareCanShortCircuit()
    {
        var terminalCalls = 0;
        IngestionDelegate<string> terminal = (_, _, _) =>
        {
            terminalCalls++;
            return new ValueTask<IngestionResult<IngestedEntity>>(
                IngestionResult<IngestedEntity>.Success(StubEntity()));
        };

        var pipeline = new IngestionPipelineBuilder<string>()
            .Use(new ShortCircuitMiddleware())
            .Build(terminal);

        var r = await pipeline("hello", Ctx(), CancellationToken.None);

        Assert.Equal(0, terminalCalls);
        Assert.False(r.IsSuccess);
        Assert.Equal(IngestOutcome.Quarantined, r.Outcome);
    }

    [Fact]
    public async Task Build_MiddlewareOrderPreservedAcrossBuilds()
    {
        var log1 = new List<string>();
        var log2 = new List<string>();

        IngestionDelegate<string> terminal1 = (_, _, _) =>
        {
            log1.Add("terminal");
            return new ValueTask<IngestionResult<IngestedEntity>>(
                IngestionResult<IngestedEntity>.Success(StubEntity()));
        };
        IngestionDelegate<string> terminal2 = (_, _, _) =>
        {
            log2.Add("terminal");
            return new ValueTask<IngestionResult<IngestedEntity>>(
                IngestionResult<IngestedEntity>.Success(StubEntity()));
        };

        var builder = new IngestionPipelineBuilder<string>()
            .Use(new RecordingMiddleware("a", log1))
            .Use(new RecordingMiddleware("b", log1));
        var pipeline1 = builder.Build(terminal1);

        // Rebuild with a different logger list — the two delegates must be independent.
        var builder2 = new IngestionPipelineBuilder<string>()
            .Use(new RecordingMiddleware("a", log2))
            .Use(new RecordingMiddleware("b", log2));
        var pipeline2 = builder2.Build(terminal2);

        await pipeline1("x", Ctx(), CancellationToken.None);
        await pipeline2("y", Ctx(), CancellationToken.None);

        var expected = new[] { "a:before", "b:before", "terminal", "b:after", "a:after" };
        Assert.Equal(expected, log1);
        Assert.Equal(expected, log2);
    }

    [Fact]
    public async Task Build_ExceptionInMiddleware_Propagates()
    {
        IngestionDelegate<string> terminal = (_, _, _) =>
            new ValueTask<IngestionResult<IngestedEntity>>(
                IngestionResult<IngestedEntity>.Success(StubEntity()));

        var pipeline = new IngestionPipelineBuilder<string>()
            .Use(new ThrowingMiddleware())
            .Build(terminal);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await pipeline("hello", Ctx(), CancellationToken.None));
    }
}
