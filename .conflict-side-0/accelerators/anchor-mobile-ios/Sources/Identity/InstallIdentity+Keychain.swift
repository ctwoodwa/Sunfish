import Foundation
import Crypto

#if canImport(Security)
import Security

/// Keychain persistence for `InstallIdentity` per W#23 Phase 0 + ADR 0028-A2.8.
///
/// Stores the Ed25519 private-key seed under `kSecAttrAccessibleAfterFirstUnlock`
/// (key readable any time after first device unlock since boot). The public
/// key is derived on demand from the seed, so only the seed persists.
///
/// `kSecAttrSynchronizable=false` per the hand-off — we deliberately do NOT
/// sync the install identity through iCloud Keychain; pairing semantics
/// require a per-device root keypair.
public enum InstallIdentityKeychain {
    /// Default service identifier — namespaced under the field-capture bundle id.
    public static let defaultService = "dev.sunfish.field.identity"

    /// Default account name — single per-install identity.
    public static let defaultAccount = "install-root-keypair"

    /// Persist the supplied `identity` under `(service, account)`. Replaces
    /// any prior keychain entry under the same coordinates.
    public static func persist(
        _ identity: InstallIdentity,
        service: String = defaultService,
        account: String = defaultAccount
    ) throws {
        // Delete any prior entry first; a clean replace simplifies the
        // first-launch / rotated-identity story.
        let deleteQuery: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
        ]
        SecItemDelete(deleteQuery as CFDictionary)

        let attributes: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlock,
            kSecAttrSynchronizable as String: kCFBooleanFalse as Any,
            kSecValueData as String: identity.privateKeySeed,
        ]

        let status = SecItemAdd(attributes as CFDictionary, nil)
        guard status == errSecSuccess else {
            throw InstallIdentityKeychainError.osStatus(status)
        }
    }

    /// Load the persisted identity, or `nil` if no entry exists yet.
    public static func load(
        service: String = defaultService,
        account: String = defaultAccount
    ) throws -> InstallIdentity? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne,
        ]

        var item: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &item)
        if status == errSecItemNotFound {
            return nil
        }
        guard status == errSecSuccess, let data = item as? Data else {
            throw InstallIdentityKeychainError.osStatus(status)
        }

        let signing = try Curve25519.Signing.PrivateKey(rawRepresentation: data)
        return InstallIdentity(
            publicKey: signing.publicKey.rawRepresentation,
            privateKeySeed: data
        )
    }

    /// Delete the persisted identity. No-op if no entry exists.
    public static func delete(
        service: String = defaultService,
        account: String = defaultAccount
    ) {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
        ]
        SecItemDelete(query as CFDictionary)
    }
}

public enum InstallIdentityKeychainError: Error {
    case osStatus(OSStatus)
}
#endif
