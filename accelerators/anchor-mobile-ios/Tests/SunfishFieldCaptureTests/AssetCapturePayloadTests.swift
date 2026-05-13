import Testing
import Foundation
@testable import SunfishField

/// Contract tests for `AssetCapturePayload` serialization.
///
/// Pins the canonical-JSON encoding shape so Bridge deserialization and
/// iOS encoding remain byte-compatible. W#23.2 P1.
@Suite("AssetCapturePayload")
struct AssetCapturePayloadTests {

    // MARK: Round-trip

    @Test("encode + decode preserves all fields")
    func encodeDecodeRoundTrip() throws {
        let original = AssetCapturePayload(
            equipmentId: "equip-abc-123",
            photoKind: .primary,
            notes: "East boiler room")
        let data = try JsonCanonical.serialize(original)
        let decoded = try JSONDecoder().decode(AssetCapturePayload.self, from: data)
        #expect(decoded == original)
    }

    @Test("encode + decode with nil notes preserves nil")
    func encodeDecodeNilNotes() throws {
        let original = AssetCapturePayload(equipmentId: "equip-xyz", photoKind: .primary, notes: nil)
        let data = try JsonCanonical.serialize(original)
        let decoded = try JSONDecoder().decode(AssetCapturePayload.self, from: data)
        #expect(decoded.notes == nil)
    }

    // MARK: Canonical JSON key order

    @Test("canonical JSON keys appear in alphabetical order (equipmentId, notes, photoKind)")
    func canonicalJsonKeyOrder() throws {
        let payload = AssetCapturePayload(equipmentId: "e1", photoKind: .primary, notes: "note")
        let json = try JsonCanonical.serialize(payload)
        let text = String(decoding: json, as: UTF8.self)
        // Alphabetical: equipmentId < notes < photoKind
        let eIdx = text.range(of: "\"equipmentId\"")!.lowerBound
        let nIdx = text.range(of: "\"notes\"")!.lowerBound
        let pIdx = text.range(of: "\"photoKind\"")!.lowerBound
        #expect(eIdx < nIdx)
        #expect(nIdx < pIdx)
    }

    // MARK: PhotoKind raw value

    @Test("PhotoKind.primary raw value is \"primary\"")
    func photoKindPrimaryRawValue() {
        #expect(AssetCapturePayload.PhotoKind.primary.rawValue == "primary")
    }
}
