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

    /// Phase 3.5 — `JsonCanonical` ships; previous trip-wire lifted.
    /// Verifies the envelope round-trips through the canonical encoder
    /// with sorted keys + no whitespace + UTF-8 (matching .NET
    /// `Sunfish.Foundation.Crypto.CanonicalJson.Serialize`).
    func testEnvelope_RoundTripsThroughJsonCanonical() throws {
        let envelope = newEnvelope(blobRef: "abc123")
        let bytes = try JsonCanonical.serialize(envelope)
        let json = String(data: bytes, encoding: .utf8) ?? ""

        // Canonical form: sorted keys + no whitespace. blobRef sorts
        // alphabetically before capturedAt, so the first key is blobRef.
        XCTAssertTrue(json.hasPrefix("{\"blobRef\":"))
        XCTAssertFalse(json.contains(" "))
        XCTAssertFalse(json.contains("\n"))
        // Decoded envelope round-trips equal.
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        let decoded = try decoder.decode(EventEnvelope.self, from: bytes)
        XCTAssertEqual(envelope, decoded)
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
