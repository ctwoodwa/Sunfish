import Foundation
import Crypto

/// Stable per-install device identifier derived from the install's Ed25519
/// public key. Per W#23 Phase 0 hand-off: first 16 hex chars of
/// `SHA-256(publicKeyBytes)`.
///
/// The `DeviceId` is deterministic given the same public key — pairing tokens
/// bind to it; rotating the install identity rotates the device id.
public struct DeviceId: Equatable, Hashable, Sendable, CustomStringConvertible {
    public let value: String

    public init(value: String) {
        self.value = value
    }

    /// Derive a `DeviceId` from raw Ed25519 public-key bytes (32 bytes).
    public static func derive(fromPublicKey publicKeyBytes: Data) -> DeviceId {
        let digest = SHA256.hash(data: publicKeyBytes)
        let hex = digest.map { String(format: "%02x", $0) }.joined()
        // First 16 hex chars (= 8 bytes of entropy from the 32-byte digest).
        // Per the hand-off — sufficient collision resistance for the operator's
        // device fleet at the per-install scale.
        return DeviceId(value: String(hex.prefix(16)))
    }

    public var description: String { value }
}
