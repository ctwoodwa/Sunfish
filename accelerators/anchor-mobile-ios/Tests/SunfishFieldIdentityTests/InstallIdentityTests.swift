import XCTest
import Crypto
@testable import SunfishFieldIdentity

final class InstallIdentityTests: XCTestCase {
    func testGenerate_ProducesValidEd25519Keypair() {
        let identity = InstallIdentity.generate()

        XCTAssertEqual(identity.publicKey.count, 32)
        XCTAssertEqual(identity.privateKeySeed.count, 32)
    }

    func testGenerate_PublicKeyDerivedFromPrivateKeySeed() throws {
        let identity = InstallIdentity.generate()

        let signing = try Curve25519.Signing.PrivateKey(rawRepresentation: identity.privateKeySeed)
        XCTAssertEqual(signing.publicKey.rawRepresentation, identity.publicKey)
    }

    func testGenerate_ProducesUniqueKeypairsAcrossCalls() {
        let a = InstallIdentity.generate()
        let b = InstallIdentity.generate()

        XCTAssertNotEqual(a.publicKey, b.publicKey)
        XCTAssertNotEqual(a.privateKeySeed, b.privateKeySeed)
        XCTAssertNotEqual(a.deviceId, b.deviceId)
    }

    func testDeviceId_ConsistentlyDerivedFromPublicKey() {
        let identity = InstallIdentity.generate()

        let derivedOnce = identity.deviceId
        let derivedAgain = identity.deviceId

        XCTAssertEqual(derivedOnce, derivedAgain)
        XCTAssertEqual(derivedOnce, DeviceId.derive(fromPublicKey: identity.publicKey))
    }

    func testSigningKey_RoundTripsViaSeed() throws {
        let identity = InstallIdentity.generate()

        let signing = try identity.signingKey()
        let payload = "field-pairing-test".data(using: .utf8)!
        let signature = try signing.signature(for: payload)

        XCTAssertTrue(signing.publicKey.isValidSignature(signature, for: payload))
    }

    func testInit_WithExplicitBytes_PreservesFields() {
        let pk = Data(repeating: 0xAA, count: 32)
        let seed = Data(repeating: 0xBB, count: 32)

        let identity = InstallIdentity(publicKey: pk, privateKeySeed: seed)

        XCTAssertEqual(identity.publicKey, pk)
        XCTAssertEqual(identity.privateKeySeed, seed)
    }
}
