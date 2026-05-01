import XCTest
import Crypto
@testable import SunfishFieldIdentity

final class DeviceIdTests: XCTestCase {
    func testDerive_ProducesStable16HexCharsFromKnownPublicKey() {
        // Fixed 32-byte public key for deterministic vectoring.
        let publicKey = Data((0..<32).map { UInt8($0) })

        let id = DeviceId.derive(fromPublicKey: publicKey)

        // 16 hex chars (= 8 bytes of the 32-byte SHA-256 digest).
        XCTAssertEqual(id.value.count, 16)

        // Verify the value matches the manual SHA-256 of the public key.
        let digest = SHA256.hash(data: publicKey)
        let fullHex = digest.map { String(format: "%02x", $0) }.joined()
        XCTAssertEqual(id.value, String(fullHex.prefix(16)))
    }

    func testDerive_DeterministicForSamePublicKey() {
        let publicKey = Data((0..<32).map { _ in UInt8.random(in: 0...255) })

        let id1 = DeviceId.derive(fromPublicKey: publicKey)
        let id2 = DeviceId.derive(fromPublicKey: publicKey)

        XCTAssertEqual(id1, id2)
    }

    func testDerive_DifferentPublicKeysProduceDifferentIds() {
        let pkA = Data((0..<32).map { UInt8($0) })
        let pkB = Data((0..<32).map { UInt8($0 + 1) })

        let idA = DeviceId.derive(fromPublicKey: pkA)
        let idB = DeviceId.derive(fromPublicKey: pkB)

        XCTAssertNotEqual(idA, idB)
    }

    func testDescription_ReturnsValue() {
        let id = DeviceId(value: "abcdef0123456789")
        XCTAssertEqual(id.description, "abcdef0123456789")
    }

    func testEquatable_ByValue() {
        XCTAssertEqual(DeviceId(value: "abcd"), DeviceId(value: "abcd"))
        XCTAssertNotEqual(DeviceId(value: "abcd"), DeviceId(value: "efgh"))
    }
}
