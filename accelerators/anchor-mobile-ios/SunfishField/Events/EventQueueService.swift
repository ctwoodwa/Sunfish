import Foundation
import GRDB

/// Outbound event-queue service surface. Per W#23 hand-off Phase 3.
///
/// Phase 3 ships the contract + a GRDB-backed default implementation
/// over the `event_queue` table from Phase 2. The full sync engine
/// (Phase 4) consumes this service.
public protocol EventQueueServicing: Sendable {
    /// Persist an envelope into the local outbound queue. Idempotent on
    /// `deviceLocalSeq` — re-appending the same sequence number is a
    /// no-op (per Phase 2 V1Migration UNIQUE constraint).
    func appendAsync(envelope: EventEnvelope) async throws

    /// Return the next batch of pending events, up to `limit`. Pending
    /// means `queue_status = 'pending'`. Order: ascending by
    /// `device_local_seq`.
    func nextPendingBatch(limit: Int) async throws -> [EventQueueRecord]

    /// Mark a previously-uploaded event as ack'd. Caller has the row's
    /// `device_local_seq` from the upload-task tracking map.
    func markAcked(deviceLocalSeq: Int64) async throws

    /// Mark a previously-uploaded event as permanently failed. Carries
    /// the `reason` for surfacing to the queue-status home screen
    /// (Phase 6).
    func markFailed(deviceLocalSeq: Int64, reason: String) async throws
}

/// Default GRDB-backed implementation. Per W#23 hand-off Phase 3.
public final class EventQueueService: EventQueueServicing, @unchecked Sendable {
    private let database: AppDatabase

    public init(database: AppDatabase) {
        self.database = database
    }

    public func appendAsync(envelope: EventEnvelope) async throws {
        // Phase 3 substrate: persist via Codable round-trip into the
        // event_queue row's payload BLOB. Phase 3.5's RFC 8785
        // canonicalizer replaces the JSONEncoder used here so the
        // bytes are byte-stable across replicas. Phase 4 (sync engine)
        // re-encodes the envelope at upload time, so single-replica
        // byte-instability is acceptable for substrate v1.
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        let payload = try encoder.encode(envelope)

        try await Task {
            try database.queue.write { db in
                var record = EventQueueRecord(
                    deviceLocalSeq: Int64(envelope.deviceLocalSeq),
                    capturedAt: envelope.capturedAt,
                    eventType: envelope.eventType.rawValue,
                    payload: payload,
                    blobRef: envelope.blobRef,
                    queueStatus: .pending,
                    attemptCount: 0)
                try record.insert(db)
            }
        }.value
    }

    public func nextPendingBatch(limit: Int) async throws -> [EventQueueRecord] {
        try await Task {
            try database.queue.read { db in
                try EventQueueRecord
                    .filter(Column("queue_status") == QueueStatus.pending.rawValue)
                    .order(Column("device_local_seq"))
                    .limit(limit)
                    .fetchAll(db)
            }
        }.value
    }

    public func markAcked(deviceLocalSeq: Int64) async throws {
        try await Task {
            try database.queue.write { db in
                try db.execute(
                    sql: "UPDATE event_queue SET queue_status = ? WHERE device_local_seq = ?",
                    arguments: [QueueStatus.acked.rawValue, deviceLocalSeq])
            }
        }.value
    }

    public func markFailed(deviceLocalSeq: Int64, reason: String) async throws {
        // Phase 3 substrate: mark the row failed-permanent. The reason
        // string is logged to the local audit_local table (per Phase 2
        // V1Migration); the Phase 6 queue-status home screen surfaces
        // it from there.
        //
        // payload column shape: UTF-8 reason string (substrate v1).
        // Phase 6 may upgrade to a {reason, retry_count, last_error}
        // JSON envelope when the queue-status row needs richer surface.
        let nowIso = ISO8601DateFormatter().string(from: Date())
        try await Task {
            try database.queue.write { db in
                try db.execute(
                    sql: "UPDATE event_queue SET queue_status = ? WHERE device_local_seq = ?",
                    arguments: [QueueStatus.failedPermanent.rawValue, deviceLocalSeq])
                try db.execute(
                    sql: "INSERT INTO audit_local (occurred_at, event_type, payload) VALUES (?, ?, ?)",
                    arguments: [nowIso, "EventQueueMarkedFailed", Data(reason.utf8)])
            }
        }.value
    }
}
