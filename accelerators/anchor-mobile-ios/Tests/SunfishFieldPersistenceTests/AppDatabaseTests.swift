import XCTest
import Foundation
import GRDB
@testable import SunfishField

final class AppDatabaseTests: XCTestCase {
    private var workingDirectory: URL!

    override func setUpWithError() throws {
        workingDirectory = FileManager.default.temporaryDirectory
            .appendingPathComponent("SunfishField-AppDatabaseTests-\(UUID().uuidString)")
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: workingDirectory)
    }

    func testOpen_AppliesV1Migration_AndCreatesEventQueueTable() throws {
        let database = try AppDatabase.open(at: workingDirectory)

        try database.queue.read { db in
            XCTAssertTrue(try db.tableExists("event_queue"))
            XCTAssertTrue(try db.tableExists("audit_local"))
        }
    }

    func testOpen_IsIdempotent_ReopeningTheSameDirectoryDoesNotFail() throws {
        let database1 = try AppDatabase.open(at: workingDirectory)
        try database1.queue.write { db in
            var rec = EventQueueRecord(
                deviceLocalSeq: 1,
                capturedAt: Date(),
                eventType: "Receipt",
                payload: Data([0x01, 0x02]))
            try rec.insert(db)
        }

        let database2 = try AppDatabase.open(at: workingDirectory)
        let count = try database2.queue.read { db in
            try EventQueueRecord.fetchCount(db)
        }
        XCTAssertEqual(count, 1)
    }

    func testEventQueueRecord_RoundTripsThroughInsertAndFetch() throws {
        let database = try AppDatabase.open(at: workingDirectory)
        let captured = Date(timeIntervalSince1970: 1_780_000_000)
        var record = EventQueueRecord(
            deviceLocalSeq: 42,
            capturedAt: captured,
            eventType: "Inspection",
            payload: Data("hello".utf8),
            blobRef: "deadbeef",
            queueStatus: .pending,
            attemptCount: 0)

        try database.queue.write { db in
            try record.insert(db)
        }
        XCTAssertNotNil(record.rowid)

        let fetched = try database.queue.read { db in
            try EventQueueRecord.fetchOne(db, key: record.rowid!)
        }
        XCTAssertNotNil(fetched)
        XCTAssertEqual(fetched?.deviceLocalSeq, 42)
        XCTAssertEqual(fetched?.eventType, "Inspection")
        XCTAssertEqual(fetched?.payload, Data("hello".utf8))
        XCTAssertEqual(fetched?.blobRef, "deadbeef")
        XCTAssertEqual(fetched?.queueStatus, .pending)
        XCTAssertEqual(fetched?.attemptCount, 0)
    }

    func testEventQueue_DeviceLocalSeqUniqueness_RejectsDuplicate() throws {
        let database = try AppDatabase.open(at: workingDirectory)
        try database.queue.write { db in
            var first = EventQueueRecord(deviceLocalSeq: 1, capturedAt: Date(), eventType: "Receipt", payload: Data())
            try first.insert(db)
        }

        XCTAssertThrowsError(try database.queue.write { db in
            var dup = EventQueueRecord(deviceLocalSeq: 1, capturedAt: Date(), eventType: "Receipt", payload: Data())
            try dup.insert(db)
        })
    }
}
