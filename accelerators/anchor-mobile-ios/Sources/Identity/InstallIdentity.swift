import Foundation
import Crypto

/// Per-install Ed25519 root keypair per W#23 Phase 0 + ADR 0028-A2.8.
///
/// Generated locally on first app launch and persisted to the iOS Keychain
/// with `kSecAttrAccessibleAfterFirstUnlock` (the device must have been
/// unlocked at least once since boot for the key to be readable). The
/// public key derives the stable `DeviceId` that pairing tokens bind to.
///
/// This struct is value-typed; persistence is the consumer's responsibility
/// via the Keychain helpers in `InstallIdentity+Keychain.swift`.
public struct InstallIdentity: Sendable {
    /// Raw 32-byte Ed25519 public key.
    public let publicKey: Data

    /// Raw 32-byte Ed25519 private key seed (suitable for re-deriving the
    /// signing key via `Curve25519.Signing.PrivateKey(rawRepresentation:)`).
    public let privateKeySeed: Data

    /// Stable `DeviceId` derived from `publicKey`.
    public var deviceId: DeviceId {
        DeviceId.derive(fromPublicKey: publicKey)
    }

    /// Generate a fresh install identity. The caller is responsible for
    /// persisting it via `InstallIdentity+Keychain.persist(_:account:)`.
    public static func generate() -> InstallIdentity {
        let signingKey = Curve25519.Signing.PrivateKey()
        let publicKey = signingKey.publicKey.rawRepresentation
        let privateKeySeed = signingKey.rawRepresentation
        return InstallIdentity(publicKey: publicKey, privateKeySeed: privateKeySeed)
    }

    /// Reconstruct an identity from raw key bytes (e.g., loaded from Keychain).
    public init(publicKey: Data, privateKeySeed: Data) {
        self.publicKey = publicKey
        self.privateKeySeed = privateKeySeed
    }

    /// Re-hydrate the signing key from the private-key seed.
    public func signingKey() throws -> Curve25519.Signing.PrivateKey {
        try Curve25519.Signing.PrivateKey(rawRepresentation: privateKeySeed)
    }
}
