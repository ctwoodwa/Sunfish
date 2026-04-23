using System.Security.Cryptography;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Kernel.Security.Keys;

/// <summary>
/// Default <see cref="IRoleKeyManager"/>. Role keys are 32 bytes (matches the key
/// size of ChaCha20-Poly1305 and AES-256-GCM — the expected Layer 2 field-level
/// AEADs from paper §11.2). Keys are generated with <see cref="RandomNumberGenerator"/>
/// and wrapped with X25519 sealed-box (<see cref="IX25519KeyAgreement"/>). Unwrapped
/// keys are cached in the OS keystore via <see cref="IKeystore"/>.
/// </summary>
public sealed class RoleKeyManager : IRoleKeyManager
{
    /// <summary>Role-key length in bytes.</summary>
    public const int RoleKeyLength = 32;

    /// <summary>Keystore name prefix for stored role keys.</summary>
    public const string KeystoreNamespace = "sunfish:role-key:";

    private readonly IX25519KeyAgreement _kem;
    private readonly IKeystore _keystore;

    /// <summary>Constructs a manager bound to the X25519 primitive and an OS keystore.</summary>
    public RoleKeyManager(IX25519KeyAgreement kem, IKeystore keystore)
    {
        _kem = kem ?? throw new ArgumentNullException(nameof(kem));
        _keystore = keystore ?? throw new ArgumentNullException(nameof(keystore));
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GenerateRoleKey()
    {
        var key = new byte[RoleKeyLength];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    /// <inheritdoc />
    public RoleKeyBundle WrapRoleKey(
        ReadOnlyMemory<byte> roleKey,
        string role,
        byte[] memberPublicKey,
        ReadOnlyMemory<byte> adminPrivateKey,
        byte[] adminPublicKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(role);
        ArgumentNullException.ThrowIfNull(memberPublicKey);
        ArgumentNullException.ThrowIfNull(adminPublicKey);
        if (roleKey.Length != RoleKeyLength)
        {
            throw new ArgumentException(
                $"Role key must be {RoleKeyLength} bytes (was {roleKey.Length}).", nameof(roleKey));
        }

        var (ciphertext, nonce) = _kem.Box(roleKey.Span, memberPublicKey, adminPrivateKey.Span);
        return new RoleKeyBundle(
            Role: role,
            MemberPublicKey: memberPublicKey,
            WrappedKey: ciphertext,
            Nonce: nonce);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> UnwrapRoleKey(
        RoleKeyBundle bundle,
        ReadOnlyMemory<byte> memberPrivateKey,
        byte[] adminPublicKey)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(adminPublicKey);

        var plaintext = _kem.OpenBox(
            bundle.WrappedKey,
            bundle.Nonce,
            adminPublicKey,
            memberPrivateKey.Span);

        if (plaintext is null)
        {
            throw new CryptographicException(
                "Role key bundle authentication failed — bundle was not produced by the expected admin " +
                "for this recipient, or it was tampered with in transit.");
        }
        if (plaintext.Length != RoleKeyLength)
        {
            throw new CryptographicException(
                $"Unwrapped role key has unexpected length {plaintext.Length} (expected {RoleKeyLength}).");
        }
        return plaintext;
    }

    /// <inheritdoc />
    public Task StoreRoleKeyAsync(string role, ReadOnlyMemory<byte> roleKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(role);
        return _keystore.SetKeyAsync(KeystoreName(role), roleKey, ct);
    }

    /// <inheritdoc />
    public Task<ReadOnlyMemory<byte>?> GetRoleKeyAsync(string role, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(role);
        return _keystore.GetKeyAsync(KeystoreName(role), ct);
    }

    private static string KeystoreName(string role) => KeystoreNamespace + role;
}
