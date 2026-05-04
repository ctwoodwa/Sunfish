import XCTest
import Foundation
@testable import SunfishField

final class BlobStoreTests: XCTestCase {
    private var rootDirectory: URL!
    private var store: BlobStore!

    override func setUpWithError() throws {
        rootDirectory = FileManager.default.temporaryDirectory
            .appendingPathComponent("SunfishField-BlobStoreTests-\(UUID().uuidString)")
        store = try BlobStore(rootDirectory: rootDirectory)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: rootDirectory)
    }

    func testPut_Get_RoundTripsThroughContentAddress() throws {
        let bytes = Data("the quick brown fox".utf8)
        let address = try store.put(bytes)

        XCTAssertEqual(address, BlobStore.contentAddress(of: bytes))
        let fetched = try store.get(address: address)
        XCTAssertEqual(fetched, bytes)
    }

    func testContentAddress_IsLowercaseHexSha256_Deterministic() {
        let bytes = Data("hello".utf8)
        let address = BlobStore.contentAddress(of: bytes)

        // SHA-256("hello") known constant.
        XCTAssertEqual(address, "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824")
    }

    func testPut_SameBytesTwice_ProducesSameAddressAndIsIdempotent() throws {
        let bytes = Data("idempotent".utf8)
        let a = try store.put(bytes)
        let b = try store.put(bytes)
        XCTAssertEqual(a, b)
        XCTAssertEqual(try store.get(address: a), bytes)
    }

    func testRemove_DeletesTheBlob() throws {
        let bytes = Data("ephemeral".utf8)
        let address = try store.put(bytes)
        XCTAssertTrue(store.contains(address: address))

        let removed = try store.remove(address: address)
        XCTAssertTrue(removed)
        XCTAssertFalse(store.contains(address: address))
    }

    func testTotalBytes_AggregatesAcrossBlobs() throws {
        try store.put(Data(repeating: 0xab, count: 100))
        try store.put(Data(repeating: 0xcd, count: 250))
        let total = try store.totalBytes()
        XCTAssertEqual(total, 350)
    }
}
