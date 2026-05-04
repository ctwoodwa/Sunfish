import XCTest
import Foundation
import GRDB
@testable import SunfishField

final class CompactionPolicyTests: XCTestCase {
    func testCaptureBlocker_QueueAtCap_ReturnsQueueFull() {
        let blocker = CompactionPolicy.captureBlocker(
            queueDepth: CompactionPolicy.maxQueueDepth,
            blobBytes: 0)
        XCTAssertEqual(blocker, .queueFull(events: CompactionPolicy.maxQueueDepth))
    }

    func testCaptureBlocker_BlobBytesAtCap_ReturnsBlobStorageExceeded() {
        let blocker = CompactionPolicy.captureBlocker(
            queueDepth: 0,
            blobBytes: CompactionPolicy.maxBlobBytes)
        XCTAssertEqual(blocker, .blobStorageExceeded(bytes: CompactionPolicy.maxBlobBytes))
    }

    func testCaptureBlocker_BothBelowCap_ReturnsNil() {
        XCTAssertNil(CompactionPolicy.captureBlocker(queueDepth: 100, blobBytes: 1024))
    }

    func testShouldWarn_AboveEightyPercent_ReturnsTrue() {
        XCTAssertTrue(CompactionPolicy.shouldWarn(
            queueDepth: Int(Double(CompactionPolicy.maxQueueDepth) * 0.85),
            blobBytes: 0))
        XCTAssertTrue(CompactionPolicy.shouldWarn(
            queueDepth: 0,
            blobBytes: Int64(Double(CompactionPolicy.maxBlobBytes) * 0.85)))
    }

    func testShouldWarn_BelowEightyPercent_ReturnsFalse() {
        XCTAssertFalse(CompactionPolicy.shouldWarn(
            queueDepth: Int(Double(CompactionPolicy.maxQueueDepth) * 0.50),
            blobBytes: Int64(Double(CompactionPolicy.maxBlobBytes) * 0.50)))
    }

    func testSweepAcked_RemovesAckedRowsAndOrphanedBlobs() throws {
        let workingDirectory = FileManager.default.temporaryDirectory
            .appendingPathComponent("SunfishField-CompactionTests-\(UUID().uuidString)")
        defer { try? FileManager.default.removeItem(at: workingDirectory) }

        let database = try AppDatabase.open(at: workingDirectory)
        let blobStore = try BlobStore(rootDirectory: workingDirectory.appendingPathComponent("blobs"))
        let blobAddress = try blobStore.put(Data([0x42]))

        try database.queue.write { db in
            var acked = EventQueueRecord(deviceLocalSeq: 1, capturedAt: Date(), eventType: "Receipt",
                payload: Data(), blobRef: blobAddress, queueStatus: .acked)
            try acked.insert(db)
            var pending = EventQueueRecord(deviceLocalSeq: 2, capturedAt: Date(), eventType: "Receipt",
                payload: Data(), blobRef: nil, queueStatus: .pending)
            try pending.insert(db)
        }

        try database.queue.write { db in
            let removed = try CompactionPolicy.sweepAcked(in: db, blobStore: blobStore)
            XCTAssertEqual(removed, 1)
        }

        let remaining = try database.queue.read { db in
            try EventQueueRecord.fetchCount(db)
        }
        XCTAssertEqual(remaining, 1)
        XCTAssertFalse(blobStore.contains(address: blobAddress))
    }
}
