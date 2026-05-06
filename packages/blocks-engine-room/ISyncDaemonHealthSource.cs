using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.EngineRoom;

namespace Sunfish.Blocks.EngineRoom;

/// <summary>
/// Optional host-side hook providing live sync-daemon telemetry to
/// <see cref="DefaultEngineRoomDataProvider"/>. Hosts that run a real
/// sync daemon implement this and register it in DI; hosts that don't
/// (kitchen-sink demo, unit tests) leave the registration unfilled and
/// the data provider returns a sensible default
/// (<see cref="SyncDaemonStatus.Unavailable"/> + zeros) per ADR 0079 §1
/// + W#50 Phase 2 fallback contract.
/// </summary>
public interface ISyncDaemonHealthSource
{
    /// <summary>
    /// Returns the current sync-daemon health snapshot. The data
    /// provider calls this once per
    /// <c>GetSyncDaemonHealthAsync</c> invocation; implementations
    /// SHOULD surface cached telemetry rather than driving a fresh probe
    /// per call.
    /// </summary>
    ValueTask<SyncDaemonHealth> GetCurrentAsync(
        TenantId tenantId,
        CancellationToken ct = default);
}
