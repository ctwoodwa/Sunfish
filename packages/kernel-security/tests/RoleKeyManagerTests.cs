using System.Security.Cryptography;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Kernel.Security.Tests;

public sealed class RoleKeyManagerTests
{
    private readonly X25519KeyAgreement _kem = new();

    private RoleKeyManager NewManager(IKeystore? keystore = null)
        => new(_kem, keystore ?? new InMemoryKeystore());

    [Fact]
    public void GenerateRoleKey_returns_32_bytes()
    {
        var mgr = NewManager();
        var key = mgr.GenerateRoleKey();
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void GenerateRoleKey_produces_unique_keys()
    {
        var mgr = NewManager();
        var a = mgr.GenerateRoleKey().ToArray();
        var b = mgr.GenerateRoleKey().ToArray();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Wrap_and_Unwrap_roundtrip()
    {
        var mgr = NewManager();
        var (adminPub, adminPriv) = _kem.GenerateKeyPair();
        var (memberPub, memberPriv) = _kem.GenerateKeyPair();

        var original = mgr.GenerateRoleKey();
        var bundle = mgr.WrapRoleKey(original, "financial_role", memberPub, adminPriv, adminPub);

        Assert.Equal("financial_role", bundle.Role);
        Assert.Equal(memberPub, bundle.MemberPublicKey);
        Assert.Equal(24, bundle.Nonce.Length);
        Assert.Equal(32 + 16, bundle.WrappedKey.Length);

        var unwrapped = mgr.UnwrapRoleKey(bundle, memberPriv, adminPub);
        Assert.Equal(original.ToArray(), unwrapped.ToArray());
    }

    [Fact]
    public void Unwrap_throws_when_wrong_member_private_key()
    {
        var mgr = NewManager();
        var (adminPub, adminPriv) = _kem.GenerateKeyPair();
        var (memberPub, _) = _kem.GenerateKeyPair();
        var (_, attackerPriv) = _kem.GenerateKeyPair();

        var roleKey = mgr.GenerateRoleKey();
        var bundle = mgr.WrapRoleKey(roleKey, "r", memberPub, adminPriv, adminPub);

        Assert.Throws<CryptographicException>(() => mgr.UnwrapRoleKey(bundle, attackerPriv, adminPub));
    }

    [Fact]
    public void Unwrap_throws_when_wrong_admin_public_key()
    {
        var mgr = NewManager();
        var (adminPub, adminPriv) = _kem.GenerateKeyPair();
        var (memberPub, memberPriv) = _kem.GenerateKeyPair();
        var (otherAdminPub, _) = _kem.GenerateKeyPair();

        var roleKey = mgr.GenerateRoleKey();
        var bundle = mgr.WrapRoleKey(roleKey, "r", memberPub, adminPriv, adminPub);

        // Unwrap expects otherAdminPub → not the signer → MAC fails.
        Assert.Throws<CryptographicException>(() => mgr.UnwrapRoleKey(bundle, memberPriv, otherAdminPub));
    }

    [Fact]
    public async Task Store_and_Get_roundtrip_via_keystore()
    {
        var keystore = new InMemoryKeystore();
        var mgr = NewManager(keystore);

        var roleKey = mgr.GenerateRoleKey();
        await mgr.StoreRoleKeyAsync("team_member", roleKey, default);

        var fetched = await mgr.GetRoleKeyAsync("team_member", default);
        Assert.NotNull(fetched);
        Assert.Equal(roleKey.ToArray(), fetched.Value.ToArray());
    }

    [Fact]
    public async Task Get_returns_null_when_role_absent()
    {
        var mgr = NewManager();
        var fetched = await mgr.GetRoleKeyAsync("missing", default);
        Assert.Null(fetched);
    }

    [Fact]
    public void KeyRotation_revoked_member_cannot_decrypt_new_bundle()
    {
        // Scenario: admin rotates the role key. The revoked member gets no new bundle.
        // Even if they see the new bundle (e.g. because it is published to all), they
        // still can't decrypt because its recipient is a different member.
        var mgr = NewManager();
        var (adminPub, adminPriv) = _kem.GenerateKeyPair();
        var (revokedPub, revokedPriv) = _kem.GenerateKeyPair();
        var (activePub, _) = _kem.GenerateKeyPair();

        // Round 1: both members wrapped.
        var oldKey = mgr.GenerateRoleKey();
        var oldRevokedBundle = mgr.WrapRoleKey(oldKey, "r", revokedPub, adminPriv, adminPub);
        Assert.Equal(oldKey.ToArray(),
            mgr.UnwrapRoleKey(oldRevokedBundle, revokedPriv, adminPub).ToArray());

        // Round 2: admin rotates, only wraps for the still-active member.
        var newKey = mgr.GenerateRoleKey();
        var newActiveBundle = mgr.WrapRoleKey(newKey, "r", activePub, adminPriv, adminPub);

        // The revoked member cannot decrypt the active member's bundle.
        Assert.Throws<CryptographicException>(
            () => mgr.UnwrapRoleKey(newActiveBundle, revokedPriv, adminPub));
    }

}
