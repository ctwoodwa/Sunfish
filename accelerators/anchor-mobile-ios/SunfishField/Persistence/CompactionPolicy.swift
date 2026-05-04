import Foundation
import GRDB
import os

/// Bounds the local persistence layer per ADR 0028-A2.7:
/// 5000-event hard cap, 500 MB blob cap, 30-day soft warning, 90-day forced
/// foreground sync, VACUUM after batches >= 100.
///
/// The policy is consulted by capture entry points (block new captures when
/// the queue reaches the hard cap) and by the post-sync ACK handler (delete
/// acked rows + their blobs; VACUUM when enough rows were collected).
public struct CompactionPolicy: Sendable {
    public static let maxQueueDepth = 5000
    public static let maxBlobBytes: Int64 = 500 * 1024 * 1024
    public static let warnAtPercent = 0.80
    public static let blockAtPercent = 1.0
    public static let softWarningAge: TimeInterval = 30 * 24 * 60 * 60
    public static let forcedForegroundSyncAge: TimeInterval = 90 * 24 * 60 * 60
    public static let vacuumBatchThreshold = 100

    /// Reasons a new capture can be blocked. Surfaced to the UX via the
    /// queue-status home screen (Phase 6).
    public enum CaptureBlockReason: Equatable, Sendable {
        case queueFull(events: Int)
        case blobStorageExceeded(bytes: Int64)
    }

    /// Inspect the current queue / blob-store sizes and return a block
    /// reason when the hard cap is reached. `nil` indicates the device may
    /// accept a new capture.
    public static func captureBlocker(
        queueDepth: Int,
        blobBytes: Int64
    ) -> CaptureBlockReason? {
        if queueDepth >= maxQueueDepth {
            return .queueFull(events: queueDepth)
        }
        if blobBytes >= maxBlobBytes {
            return .blobStorageExceeded(bytes: blobBytes)
        }
        return nil
    }

    /// Returns true when the queue / blob-store is at or above the
    /// `warnAtPercent` threshold; used by the queue-status row to switch to
    /// the yellow warning state per ADR 0028-A2.7.
    public static func shouldWarn(queueDepth: Int, blobBytes: Int64) -> Bool {
        let queueRatio = Double(queueDepth) / Double(maxQueueDepth)
        let blobRatio = Double(blobBytes) / Double(maxBlobBytes)
        return queueRatio >= warnAtPercent || blobRatio >= warnAtPercent
    }

    /// Sweep acked rows + their blobs. Caller passes a `BlobStore` for
    /// the secondary blob-file deletion. Returns the number of rows
    /// removed; the caller VACUUMs when this crosses
    /// `vacuumBatchThreshold`.
    @discardableResult
    public static func sweepAcked(
        in db: Database,
        blobStore: BlobStore
    ) throws -> Int {
        // Caller serializes via DatabaseQueue.write { db in sweepAcked(...) }
        // which already wraps in a SQLite transaction; nesting another
        // BEGIN here would error. The SELECT-then-DELETE is therefore
        // atomic with respect to any concurrent caller (the queue
        // serializes writes per its `Configuration.label`), and
        // `db.changesCount` after the DELETE is the canonical count.
        let ackedBlobRefs: [String?] = try String?.fetchAll(
            db,
            sql: "SELECT blob_ref FROM event_queue WHERE queue_status = ?",
            arguments: [QueueStatus.acked.rawValue])
        try db.execute(
            sql: "DELETE FROM event_queue WHERE queue_status = ?",
            arguments: [QueueStatus.acked.rawValue])
        let ackedRowsRemoved = db.changesCount

        // Blobs are reference-counted by the event_queue.blob_ref column; a
        // hash that still appears for a non-acked row must be preserved.
        for case let blobRef? in ackedBlobRefs {
            let stillReferenced: Bool = try Bool.fetchOne(
                db,
                sql: "SELECT EXISTS(SELECT 1 FROM event_queue WHERE blob_ref = ?)",
                arguments: [blobRef]) ?? false
            if !stillReferenced {
                do {
                    try blobStore.remove(address: blobRef)
                } catch {
                    // Surface I/O errors via the system log so a stuck
                    // blob (permissions / missing-file race / disk full)
                    // doesn't disappear silently.
                    Self.compactionLogger.error(
                        "BlobStore.remove failed for orphaned blob \(blobRef, privacy: .public): \(String(describing: error), privacy: .public)")
                }
            }
        }
        return ackedRowsRemoved
    }

    private static let compactionLogger = Logger(
        subsystem: "dev.sunfish.field",
        category: "CompactionPolicy")
}
