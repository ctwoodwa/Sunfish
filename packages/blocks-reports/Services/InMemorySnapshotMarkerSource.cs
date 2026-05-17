using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Reports;

/// <summary>
/// Phase 1 default <see cref="ISnapshotMarkerSource"/>. Emits
/// monotonically increasing markers of the form
/// <c>inmem:{tenantId}:{counter}</c>. Safe for concurrent use across
/// tenants — the counter is single-process atomic via
/// <see cref="System.Threading.Interlocked.Increment(ref long)"/>.
/// </summary>
public sealed class InMemorySnapshotMarkerSource : ISnapshotMarkerSource
{
    private long _counter = 0;

    /// <inheritdoc />
    public System.Threading.Tasks.Task<string> CaptureAsync(
        TenantId tenantId,
        System.Threading.CancellationToken ct = default)
    {
        var c = System.Threading.Interlocked.Increment(ref _counter);
        return System.Threading.Tasks.Task.FromResult($"inmem:{tenantId.Value}:{c}");
    }
}
