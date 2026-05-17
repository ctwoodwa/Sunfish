using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Reports;

/// <summary>
/// Source of opaque snapshot markers used by
/// <see cref="IReportRunner"/> to bind a cartridge run to a coherent
/// upstream-state slice. The marker is opaque to cartridges — they
/// pass it verbatim into upstream cluster read APIs.
/// </summary>
/// <remarks>
/// The Phase 1 default (<see cref="InMemorySnapshotMarkerSource"/>)
/// returns a monotonically increasing counter; upstream read APIs
/// currently ignore the marker argument so the counter is
/// informational. When the per-cluster marker honor lands in a
/// follow-on hand-off (Stage 02 §6.1 step 3 — wal-position + Loro
/// version-vector), cartridges automatically get coherent snapshots
/// without any code change at this layer.
/// </remarks>
public interface ISnapshotMarkerSource
{
    /// <summary>Capture the current snapshot marker for the given tenant.</summary>
    System.Threading.Tasks.Task<string> CaptureAsync(
        TenantId tenantId,
        System.Threading.CancellationToken ct = default);
}
