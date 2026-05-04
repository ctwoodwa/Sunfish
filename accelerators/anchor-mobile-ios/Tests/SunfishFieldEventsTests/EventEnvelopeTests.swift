import XCTest
import Foundation
@testable import SunfishField

final class EventEnvelopeTests: XCTestCase {
    private static let captured = Date(timeIntervalSince1970: 1_780_000_000)

    private func newEnvelope(
        seq: UInt64 = 42,
        eventType: EventType = .Inspection,
        payload: Data = Data("hello".utf8),
        blobRef: String? = nil
    ) -> EventEnvelope {
        EventEnvelope(
            deviceLocalSeq: seq,
            capturedAt: Self.captured,
            deviceId: "ipad-abcdef0123456789",
            eventType: eventType,
            payload: payload,
            blobRef: blobRef,
            capturedUnderKernel: "1.3.0",
            capturedUnderSchemaEpoch: 7)
    }

    func testEnvelope_RoundTripsThroughCodable() throws {
        let envelope = newEnvelope()
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        let bytes = try encoder.encode(envelope)
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        let roundtripped = try decoder.decode(EventEnvelope.self, from: bytes)

        XCTAssertEqual(envelope, roundtripped)
    }

    func testEnvelope_UsesCamelCasePropertyNamesPerAdr0028A78() throws {
        let envelope = newEnvelope()
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        let json = try encoder.encode(envelope)
        let str = String(data: json, encoding: .utf8) ?? ""

        XCTAssertTrue(str.contains("\"deviceLocalSeq\""))
        XCTAssertTrue(str.contains("\"capturedAt\""))
        XCTAssertTrue(str.contains("\"deviceId\""))
        XCTAssertTrue(str.contains("\"eventType\""))
        XCTAssertTrue(str.contains("\"capturedUnderKernel\""))
        XCTAssertTrue(str.contains("\"capturedUnderSchemaEpoch\""))
    }

    func testEnvelope_PostA9FieldsCarryThrough() throws {
        let envelope = newEnvelope()
        XCTAssertEqual(envelope.capturedUnderKernel, "1.3.0")
        XCTAssertEqual(envelope.capturedUnderSchemaEpoch, 7)
    }

    func testEnvelope_BlobRefOptional() throws {
        let withRef = newEnvelope(blobRef: "abc123")
        let withoutRef = newEnvelope(blobRef: nil)
        XCTAssertEqual(withRef.blobRef, "abc123")
        XCTAssertNil(withoutRef.blobRef)
    }

    /// Trip-wire for the deferred RFC 8785 canonicalizer (Phase 3.5).
    /// Substrate v1 ships `EventEnvelope` + `EventQueueService` against
    /// Swift's standard `JSONEncoder`, which is NOT byte-stable across
    /// replicas. Phase 3.5 ships the full RFC 8785 Swift canonicalizer
    /// + the 10-fixture cross-language byte-for-byte test against
    /// `Sunfish.Foundation.Crypto.CanonicalJson.Serialize`. Until then
    /// this test stays explicitly skipped — its presence in the suite
    /// is the machine-readable trip-wire that a follow-up PR is owed.
    func testEnvelope_RFC8785_CrossLanguageByteParity_PendingPhase3Point5() throws {
        try XCTSkipIf(true,
            "Pending W#23 Phase 3.5: RFC 8785 Swift canonicalizer + 10-fixture cross-language test against Sunfish.Foundation.Crypto.CanonicalJson.Serialize")
    }

    func testEventType_AllCasesEncodeAsTheirRawString() throws {
        let encoder = JSONEncoder()
        for eventType in EventType.allCases {
            let json = try encoder.encode(eventType)
            let str = String(data: json, encoding: .utf8) ?? ""
            XCTAssertEqual(str, "\"\(eventType.rawValue)\"")
        }
    }
}
