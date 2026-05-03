using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Append-only vendor-performance event log per ADR 0058. Phase 3 ships
/// the contract + InMemory implementation. Production storage is
/// persistence-backed; the log is event-sourced (every entry is
/// immutable).
/// </summary>
public interface IVendorPerformanceLog
{
    /// <summary>Appends an event to the log. Always returns the persisted record (with the id supplied or generated).</summary>
    Task<VendorPerformanceRecord> AppendAsync(VendorPerformanceRecord record, CancellationToken ct);

    /// <summary>
    /// Lists every entry for the supplied vendor in append order
    /// (oldest first). Caller paginates via <paramref name="skip"/> +
    /// <paramref name="take"/>; both default to "no limit" when null.
    /// </summary>
    IAsyncEnumerable<VendorPerformanceRecord> ListByVendorAsync(
        VendorId vendor,
        int? skip,
        int? take,
        CancellationToken ct);

    /// <summary>
    /// Convenience projector — converts a work-order lifecycle event
    /// into a <see cref="VendorPerformanceRecord"/> appended to the log.
    /// Wires up by the W#19 audit-emission pipeline (W#18 Phase 7 will
    /// hook the projection into the work-order Completed / Cancelled
    /// transitions).
    /// </summary>
    Task<VendorPerformanceRecord> ProjectFromWorkOrderAsync(
        VendorId vendor,
        WorkOrderId workOrder,
        VendorPerformanceEvent eventType,
        ActorId recordedBy,
        DateTimeOffset occurredAt,
        string? notes,
        CancellationToken ct);
}
