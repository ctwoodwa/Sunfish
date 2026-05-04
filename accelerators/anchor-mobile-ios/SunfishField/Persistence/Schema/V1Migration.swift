import Foundation
import GRDB

/// Initial schema migration. Per W#23 P2 hand-off + ADR 0028-A2.1
/// per-event-type LWW + forward-only-status guards.
public enum V1Migration {
    public static let identifier = "v1"

    public static func register(in migrator: inout DatabaseMigrator) {
        migrator.registerMigration(identifier) { db in
            // Outbound event queue. One row per captured field event awaiting
            // sync to Anchor via the Bridge route family (Phase 4).
            try db.create(table: "event_queue") { t in
                t.autoIncrementedPrimaryKey("rowid")
                t.column("device_local_seq", .integer).notNull().unique()
                t.column("captured_at", .text).notNull()
                t.column("event_type", .text).notNull()
                t.column("payload", .blob).notNull()
                // Optional content-address into the BlobStore for binary
                // attachments (photos, signed documents, etc.).
                t.column("blob_ref", .text)
                t.column("queue_status", .text).notNull().defaults(to: "pending")
                t.column("last_attempt_at", .text)
                t.column("attempt_count", .integer).notNull().defaults(to: 0)
            }
            try db.create(index: "idx_event_queue_status", on: "event_queue", columns: ["queue_status"])
            try db.create(index: "idx_event_queue_captured_at", on: "event_queue", columns: ["captured_at"])

            // Local audit log — mirrored to Anchor on next sync. Used to
            // reconstruct the field device's local actions when the user
            // disputes a sync result.
            try db.create(table: "audit_local") { t in
                t.autoIncrementedPrimaryKey("rowid")
                t.column("occurred_at", .text).notNull()
                t.column("event_type", .text).notNull()
                t.column("payload", .blob).notNull()
            }
        }
    }
}
