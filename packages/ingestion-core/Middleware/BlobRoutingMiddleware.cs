namespace Sunfish.Ingestion.Core.Middleware;

/// <summary>
/// Middleware that classifies an ingestion input as inline or blob-promoted based on a
/// byte-size threshold. When the serialized byte count of the payload exceeds
/// <see cref="BlobRoutingMiddleware{TInput}.EffectiveThreshold"/>, the middleware sets
/// a promotion flag via <see cref="BlobRoutingContext.SetShouldPromote"/> before
/// forwarding to the next stage.
/// </summary>
/// <remarks>
/// <para>
/// The global default is <see cref="DefaultBlobBoundaryBytes"/> (64 KiB), the
/// <c>D-BLOB-BOUNDARY</c> constant from spec Appendix D. Callers may supply a
/// per-schema override by reading <c>Schema.BlobThreshold</c> from the kernel
/// schema registry (G20 — spec §3.4) and passing it as <paramref name="thresholdOverride"/>
/// at pipeline construction time. When <paramref name="thresholdOverride"/> is
/// <see langword="null"/>, the global default applies — preserving existing behaviour
/// for schemas that carry no override.
/// </para>
/// <para>
/// Byte sizing is delegated to the caller-supplied <paramref name="sizeSelector"/> func
/// so that the middleware stays input-type-agnostic. Modality adapters typically pass
/// the serialized byte count of the primary payload field.
/// </para>
/// </remarks>
/// <typeparam name="TInput">The modality-specific input type.</typeparam>
/// <param name="sizeSelector">
/// Returns the byte count of the relevant payload portion for a given input. Must return
/// a non-negative value; negative values are treated as zero.
/// </param>
/// <param name="thresholdOverride">
/// Per-schema threshold in bytes (from <c>Schema.BlobThreshold</c>). When
/// <see langword="null"/> <see cref="DefaultBlobBoundaryBytes"/> applies.
/// </param>
public sealed class BlobRoutingMiddleware<TInput>(
    Func<TInput, long> sizeSelector,
    int? thresholdOverride = null) : IIngestionMiddleware<TInput>
{
    /// <summary>
    /// The global blob-routing boundary from spec Appendix D (<c>D-BLOB-BOUNDARY</c>):
    /// 64 KiB (65 536 bytes). Used when no per-schema override is registered.
    /// </summary>
    public const int DefaultBlobBoundaryBytes = 64 * 1024; // 65 536

    /// <summary>
    /// The threshold actually applied by this middleware instance — either the
    /// override supplied at construction, or
    /// <see cref="DefaultBlobBoundaryBytes"/> when no override was given.
    /// </summary>
    public int EffectiveThreshold { get; } = thresholdOverride ?? DefaultBlobBoundaryBytes;

    /// <inheritdoc />
    public async ValueTask<IngestionResult<IngestedEntity>> InvokeAsync(
        TInput input,
        IngestionContext context,
        IngestionDelegate<TInput> next,
        CancellationToken ct)
    {
        var byteCount = Math.Max(0L, sizeSelector(input));
        BlobRoutingContext.SetShouldPromote(context, byteCount > EffectiveThreshold);
        return await next(input, context, ct);
    }
}

/// <summary>
/// Side-channel state written by <see cref="BlobRoutingMiddleware{TInput}"/> and read by
/// downstream terminal delegates that route large payloads to the blob store.
/// Stored as a static ambient state keyed by correlation id — suitable for
/// single-threaded pipeline execution (the ingestion chain is sequential per call).
/// </summary>
public static class BlobRoutingContext
{
    // Keyed by CorrelationId so concurrent ingestion calls don't share state.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool>
        _promoteFlags = new();

    /// <summary>
    /// Records whether the payload associated with <paramref name="context"/> should be
    /// promoted to the blob store rather than inlined. Called by
    /// <see cref="BlobRoutingMiddleware{TInput}"/>.
    /// </summary>
    public static void SetShouldPromote(IngestionContext context, bool value)
        => _promoteFlags[context.CorrelationId] = value;

    /// <summary>
    /// Returns <see langword="true"/> when a prior
    /// <see cref="BlobRoutingMiddleware{TInput}"/> in the chain decided the payload
    /// should be promoted. Returns <see langword="false"/> when the middleware was not
    /// registered or the payload is below the threshold.
    /// </summary>
    public static bool ShouldPromote(IngestionContext context)
        => _promoteFlags.TryGetValue(context.CorrelationId, out var v) && v;

    /// <summary>
    /// Removes the promotion flag for <paramref name="context"/> from the ambient state.
    /// Terminal delegates should call this after consuming the flag to avoid unbounded
    /// growth in long-lived processes.
    /// </summary>
    public static void Clear(IngestionContext context)
        => _promoteFlags.TryRemove(context.CorrelationId, out _);
}
