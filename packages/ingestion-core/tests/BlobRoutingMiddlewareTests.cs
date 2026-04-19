using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Core.Middleware;
using Xunit;

namespace Sunfish.Ingestion.Core.Tests;

/// <summary>
/// Tests for G20 — <see cref="BlobRoutingMiddleware{TInput}"/> and
/// <see cref="BlobRoutingContext"/>. Covers:
/// <list type="bullet">
///   <item>Global default (64 KiB) is used when no per-schema override is given.</item>
///   <item>Per-schema override replaces the global default when present.</item>
///   <item>Payloads at the threshold boundary are NOT promoted (strictly greater-than).</item>
///   <item>Payloads above the threshold ARE promoted.</item>
///   <item>The middleware always calls next regardless of the routing decision.</item>
///   <item>BlobRoutingContext can be cleared after consumption.</item>
/// </list>
/// </summary>
public class BlobRoutingMiddlewareTests
{
    private static IngestedEntity StubEntity() =>
        new("e1", "schema/v1",
            new Dictionary<string, object?>(),
            Array.Empty<IngestedEvent>(),
            Array.Empty<Sunfish.Foundation.Blobs.Cid>());

    private static IngestionContext Ctx() =>
        IngestionContext.NewCorrelation("tenant-a", "actor-a");

    private static IngestionDelegate<long> AlwaysSuccess(IngestedEntity? entity = null) =>
        (_, _, _) => new ValueTask<IngestionResult<IngestedEntity>>(
            IngestionResult<IngestedEntity>.Success(entity ?? StubEntity()));

    // Size selector for long-typed inputs: the input IS the byte count.
    private static Func<long, long> Identity => input => input;

    // ------------------------------------------------------------------ //
    //  Global default constant                                             //
    // ------------------------------------------------------------------ //

    [Fact]
    public void DefaultBlobBoundaryBytes_Is64KiB()
    {
        Assert.Equal(65_536, BlobRoutingMiddleware<long>.DefaultBlobBoundaryBytes);
    }

    // ------------------------------------------------------------------ //
    //  Threshold selection: override vs global default                    //
    // ------------------------------------------------------------------ //

    [Fact]
    public void EffectiveThreshold_WithNoOverride_IsGlobalDefault()
    {
        var mw = new BlobRoutingMiddleware<long>(Identity);

        Assert.Equal(BlobRoutingMiddleware<long>.DefaultBlobBoundaryBytes, mw.EffectiveThreshold);
    }

    [Fact]
    public void EffectiveThreshold_WithOverride_IsOverrideValue()
    {
        const int customThreshold = 128 * 1024;
        var mw = new BlobRoutingMiddleware<long>(Identity, thresholdOverride: customThreshold);

        Assert.Equal(customThreshold, mw.EffectiveThreshold);
    }

    // ------------------------------------------------------------------ //
    //  Blob promotion decisions                                            //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task InvokeAsync_PayloadBelowGlobalDefault_DoesNotPromote()
    {
        var ctx = Ctx();
        var mw = new BlobRoutingMiddleware<long>(Identity); // global default = 64 KiB
        long payloadBytes = BlobRoutingMiddleware<long>.DefaultBlobBoundaryBytes - 1;

        await mw.InvokeAsync(payloadBytes, ctx, AlwaysSuccess(), CancellationToken.None);

        Assert.False(BlobRoutingContext.ShouldPromote(ctx));
        BlobRoutingContext.Clear(ctx);
    }

    [Fact]
    public async Task InvokeAsync_PayloadAtExactlyGlobalDefault_DoesNotPromote()
    {
        // Strictly greater-than semantics: at == threshold is NOT promoted.
        var ctx = Ctx();
        var mw = new BlobRoutingMiddleware<long>(Identity);
        long payloadBytes = BlobRoutingMiddleware<long>.DefaultBlobBoundaryBytes;

        await mw.InvokeAsync(payloadBytes, ctx, AlwaysSuccess(), CancellationToken.None);

        Assert.False(BlobRoutingContext.ShouldPromote(ctx));
        BlobRoutingContext.Clear(ctx);
    }

    [Fact]
    public async Task InvokeAsync_PayloadAboveGlobalDefault_Promotes()
    {
        var ctx = Ctx();
        var mw = new BlobRoutingMiddleware<long>(Identity);
        long payloadBytes = BlobRoutingMiddleware<long>.DefaultBlobBoundaryBytes + 1;

        await mw.InvokeAsync(payloadBytes, ctx, AlwaysSuccess(), CancellationToken.None);

        Assert.True(BlobRoutingContext.ShouldPromote(ctx));
        BlobRoutingContext.Clear(ctx);
    }

    [Fact]
    public async Task InvokeAsync_PayloadAboveOverride_PromotesEvenIfBelowGlobalDefault()
    {
        // Override is 32 KiB; payload is 40 KiB — above override, below global default.
        // The override must win.
        var ctx = Ctx();
        const int customThreshold = 32 * 1024;
        var mw = new BlobRoutingMiddleware<long>(Identity, thresholdOverride: customThreshold);
        long payloadBytes = 40 * 1024;

        await mw.InvokeAsync(payloadBytes, ctx, AlwaysSuccess(), CancellationToken.None);

        Assert.True(BlobRoutingContext.ShouldPromote(ctx));
        BlobRoutingContext.Clear(ctx);
    }

    [Fact]
    public async Task InvokeAsync_PayloadBelowOverride_DoesNotPromoteEvenIfAboveGlobalDefault()
    {
        // Override is 128 KiB; payload is 100 KiB — above global default, below override.
        // The override must win.
        var ctx = Ctx();
        const int customThreshold = 128 * 1024;
        var mw = new BlobRoutingMiddleware<long>(Identity, thresholdOverride: customThreshold);
        long payloadBytes = 100 * 1024; // 100 KiB

        await mw.InvokeAsync(payloadBytes, ctx, AlwaysSuccess(), CancellationToken.None);

        Assert.False(BlobRoutingContext.ShouldPromote(ctx));
        BlobRoutingContext.Clear(ctx);
    }

    // ------------------------------------------------------------------ //
    //  Middleware always forwards to next                                  //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext_Regardless()
    {
        var nextCalled = 0;
        IngestionDelegate<long> next = (_, _, _) =>
        {
            nextCalled++;
            return new ValueTask<IngestionResult<IngestedEntity>>(
                IngestionResult<IngestedEntity>.Success(StubEntity()));
        };

        var ctx = Ctx();
        var mw = new BlobRoutingMiddleware<long>(Identity);

        // Below threshold
        await mw.InvokeAsync(1L, ctx, next, CancellationToken.None);
        // Above threshold
        await mw.InvokeAsync(long.MaxValue, ctx, next, CancellationToken.None);

        Assert.Equal(2, nextCalled);
        BlobRoutingContext.Clear(ctx);
    }

    // ------------------------------------------------------------------ //
    //  BlobRoutingContext isolation and cleanup                           //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task BlobRoutingContext_Clear_RemovesFlag()
    {
        var ctx = Ctx();
        var mw = new BlobRoutingMiddleware<long>(Identity);
        long payloadBytes = BlobRoutingMiddleware<long>.DefaultBlobBoundaryBytes + 1;

        await mw.InvokeAsync(payloadBytes, ctx, AlwaysSuccess(), CancellationToken.None);
        Assert.True(BlobRoutingContext.ShouldPromote(ctx));

        BlobRoutingContext.Clear(ctx);

        Assert.False(BlobRoutingContext.ShouldPromote(ctx));
    }

    [Fact]
    public async Task BlobRoutingContext_ShouldPromote_ReturnsFalse_WhenMiddlewareNotInChain()
    {
        // When BlobRoutingMiddleware was never run, ShouldPromote must be false
        // (not throw, not return stale state).
        var ctx = Ctx();

        Assert.False(BlobRoutingContext.ShouldPromote(ctx));
    }

    [Fact]
    public async Task BlobRoutingContext_TwoConcurrentCorrelations_AreIsolated()
    {
        var ctxA = Ctx();
        var ctxB = Ctx(); // fresh Guid each time

        var mwAbove = new BlobRoutingMiddleware<long>(Identity);
        var mwBelow = new BlobRoutingMiddleware<long>(Identity);

        long aboveThreshold = BlobRoutingMiddleware<long>.DefaultBlobBoundaryBytes + 1;
        long belowThreshold = 1L;

        await mwAbove.InvokeAsync(aboveThreshold, ctxA, AlwaysSuccess(), CancellationToken.None);
        await mwBelow.InvokeAsync(belowThreshold, ctxB, AlwaysSuccess(), CancellationToken.None);

        Assert.True(BlobRoutingContext.ShouldPromote(ctxA));
        Assert.False(BlobRoutingContext.ShouldPromote(ctxB));

        BlobRoutingContext.Clear(ctxA);
        BlobRoutingContext.Clear(ctxB);
    }
}
