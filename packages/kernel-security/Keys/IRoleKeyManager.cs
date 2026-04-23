namespace Sunfish.Kernel.Security.Keys;

/// <summary>
/// Per-role symmetric-key lifecycle manager. Implements paper §11.3:
/// <list type="number">
///   <item>admin generates a new per-role key (<see cref="GenerateRoleKey"/>);</item>
///   <item>wraps it for each qualifying member using X25519 sealed-box (<see cref="WrapRoleKey"/>);</item>
///   <item>publishes the wrapped bundles as administrative events in the log;</item>
///   <item>each member unwraps its bundle with its private key (<see cref="UnwrapRoleKey"/>);</item>
///   <item>the member caches the key in the OS keystore (<see cref="StoreRoleKeyAsync"/>).</item>
/// </list>
/// Rotation is achieved by re-running the issuance flow and omitting revoked members.
/// </summary>
public interface IRoleKeyManager
{
    /// <summary>Admin side: generate a new 32-byte symmetric key for a role.</summary>
    ReadOnlyMemory<byte> GenerateRoleKey();

    /// <summary>
    /// Admin side: wrap a role key for a specific member using the admin's
    /// X25519 private key and the member's X25519 public key.
    /// </summary>
    RoleKeyBundle WrapRoleKey(
        ReadOnlyMemory<byte> roleKey,
        string role,
        byte[] memberPublicKey,
        ReadOnlyMemory<byte> adminPrivateKey,
        byte[] adminPublicKey);

    /// <summary>
    /// Member side: unwrap a received role-key bundle using this node's X25519
    /// private key. Throws on authentication failure — the caller must handle it.
    /// </summary>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The bundle was not produced for this recipient, was tampered with, or was
    /// not issued by <paramref name="adminPublicKey"/>.
    /// </exception>
    ReadOnlyMemory<byte> UnwrapRoleKey(
        RoleKeyBundle bundle,
        ReadOnlyMemory<byte> memberPrivateKey,
        byte[] adminPublicKey);

    /// <summary>
    /// Store an unwrapped role key in the OS keystore under a namespaced key-name.
    /// Naming convention: <c>sunfish:role-key:{role}</c>.
    /// </summary>
    Task StoreRoleKeyAsync(string role, ReadOnlyMemory<byte> roleKey, CancellationToken ct);

    /// <summary>Retrieve a previously-stored role key, or <c>null</c> if absent.</summary>
    Task<ReadOnlyMemory<byte>?> GetRoleKeyAsync(string role, CancellationToken ct);
}

/// <summary>
/// Wire-format role-key bundle — published as an administrative event in the log.
/// </summary>
/// <param name="Role">Role this key grants access to.</param>
/// <param name="MemberPublicKey">32-byte X25519 public key of the intended recipient.</param>
/// <param name="WrappedKey">Ciphertext: X25519-sealed-box ciphertext of the role key (48 bytes = 32-byte key + 16-byte tag).</param>
/// <param name="Nonce">24-byte nonce generated at wrap time.</param>
public sealed record RoleKeyBundle(
    string Role,
    byte[] MemberPublicKey,
    byte[] WrappedKey,
    byte[] Nonce);
