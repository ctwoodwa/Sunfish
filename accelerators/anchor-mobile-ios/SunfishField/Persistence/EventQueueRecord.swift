import Foundation
import GRDB

/// Persistable representation of a row in `event_queue`. Per W#23 P2 hand-off.
///
/// `device_local_seq` is the monotonically-increasing per-install sequence
/// number assigned at capture time; it uniquely identifies the event within
/// the device. `payload` holds the canonical-JSON-encoded event envelope
/// (Phase 3 ships the canonicalizer + parity test against the .NET side).
public struct EventQueueRecord: Codable, FetchableRecord, MutablePersistableRecord, Sendable, Equatable {
    public static let databaseTableName = "event_queue"

    public var rowid: Int64?
    public var deviceLocalSeq: Int64
    public var capturedAt: Date
    public var eventType: String
    public var payload: Data
    public var blobRef: String?
    public var queueStatus: QueueStatus
    public var lastAttemptAt: Date?
    public var attemptCount: Int

    public init(
        rowid: Int64? = nil,
        deviceLocalSeq: Int64,
        capturedAt: Date,
        eventType: String,
        payload: Data,
        blobRef: String? = nil,
        queueStatus: QueueStatus = .pending,
        lastAttemptAt: Date? = nil,
        attemptCount: Int = 0
    ) {
        self.rowid = rowid
        self.deviceLocalSeq = deviceLocalSeq
        self.capturedAt = capturedAt
        self.eventType = eventType
        self.payload = payload
        self.blobRef = blobRef
        self.queueStatus = queueStatus
        self.lastAttemptAt = lastAttemptAt
        self.attemptCount = attemptCount
    }

    public mutating func didInsert(_ inserted: InsertionSuccess) {
        rowid = inserted.rowID
    }

    enum CodingKeys: String, CodingKey {
        case rowid
        case deviceLocalSeq = "device_local_seq"
        case capturedAt = "captured_at"
        case eventType = "event_type"
        case payload
        case blobRef = "blob_ref"
        case queueStatus = "queue_status"
        case lastAttemptAt = "last_attempt_at"
        case attemptCount = "attempt_count"
    }
}

/// Lifecycle state for a row in `event_queue`. Aligned with the
/// `queue_status` text values in `V1Migration`.
public enum QueueStatus: String, Codable, DatabaseValueConvertible, Sendable, CaseIterable {
    /// Captured locally; not yet attempted upload.
    case pending
    /// Currently being uploaded by the background URLSession.
    case uploading
    /// Bridge acknowledged the event; safe to remove on next compaction.
    case acked
    /// Upload failed permanently (after retry exhaustion); user-visible.
    case failedPermanent = "failed-permanent"
}
