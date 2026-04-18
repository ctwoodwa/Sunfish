using System.Collections.Concurrent;

namespace Sunfish.Ingestion.Core.Middleware;

/// <summary>
/// Middleware that short-circuits repeat ingestions of the same input within a sliding time
/// window. The caller supplies a <see cref="Func{TInput, String}"/> to derive a dedup key from
/// each input — typically a content hash or a CID computed from canonical bytes.
/// </summary>
/// <remarks>
/// This is a simple in-memory implementation suitable for single-process adapters. Expired
/// entries are evicted lazily on each call. For distributed or persistent dedup, implement a
/// custom middleware backed by the appropriate store.
/// </remarks>
/// <typeparam name="TInput">The modality-specific input type.</typeparam>
public sealed class DeduplicationMiddleware<TInput>(Func<TInput, string> keyFactory, TimeSpan window) : IIngestionMiddleware<TInput>
{
    private readonly ConcurrentDictionary<string, DateTime> _seen = new();

    /// <inheritdoc />
    public async ValueTask<IngestionResult<IngestedEntity>> InvokeAsync(
        TInput input, IngestionContext context, IngestionDelegate<TInput> next, CancellationToken ct)
    {
        var key = keyFactory(input);
        var now = DateTime.UtcNow;

        // Evict expired entries lazily. ConcurrentDictionary is safe to enumerate during
        // modification; TryRemove is a no-op if the key is already gone.
        foreach (var kv in _seen)
        {
            if (now - kv.Value > window)
                _seen.TryRemove(kv.Key, out _);
        }

        if (_seen.ContainsKey(key))
            return IngestionResult<IngestedEntity>.Fail(IngestOutcome.Duplicate, $"Duplicate input (key={key}) within {window.TotalMinutes:F1}m window.");

        _seen[key] = now;
        return await next(input, context, ct);
    }
}
