using System.Text;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Kernel.Security.Tests;

public sealed class TeamSubkeyDerivationTests
{
    private readonly Ed25519Signer _signer = new();
    private readonly TeamSubkeyDerivation _sut;

    public TeamSubkeyDerivationTests()
    {
        _sut = new TeamSubkeyDerivation(_signer);
    }

    [Fact]
    public void DeriveSubkey_is_deterministic_for_same_root_and_teamId()
    {
        var (_, root) = _signer.GenerateKeyPair();
        var team = Guid.NewGuid().ToString();

        var a = _sut.DeriveSubkey(root, team);
        var b = _sut.DeriveSubkey(root, team);

        Assert.Equal(a, b);
        Assert.Equal(TeamSubkeyDerivation.SubkeyLength, a.Length);
    }

    [Fact]
    public void DeriveSubkey_varies_across_team_ids_with_same_root()
    {
        var (_, root) = _signer.GenerateKeyPair();
        var t1 = Guid.NewGuid().ToString();
        var t2 = Guid.NewGuid().ToString();
        var t3 = Guid.NewGuid().ToString();

        var a = _sut.DeriveSubkey(root, t1);
        var b = _sut.DeriveSubkey(root, t2);
        var c = _sut.DeriveSubkey(root, t3);

        Assert.NotEqual(a, b);
        Assert.NotEqual(b, c);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void DeriveSubkey_varies_across_roots_with_same_team_id()
    {
        var (_, root1) = _signer.GenerateKeyPair();
        var (_, root2) = _signer.GenerateKeyPair();
        var team = Guid.NewGuid().ToString();

        var a = _sut.DeriveSubkey(root1, team);
        var b = _sut.DeriveSubkey(root2, team);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void DeriveSubkey_rejects_wrong_root_length()
    {
        var badRoot = new byte[16];
        Assert.Throws<ArgumentException>(() => _sut.DeriveSubkey(badRoot, "team-a"));
    }

    [Fact]
    public void DeriveSubkey_rejects_empty_team_id()
    {
        var (_, root) = _signer.GenerateKeyPair();
        Assert.Throws<ArgumentException>(() => _sut.DeriveSubkey(root, ""));
    }

    [Fact]
    public void DeriveTeamKeypair_produces_valid_Ed25519_keypair()
    {
        var (_, root) = _signer.GenerateKeyPair();
        var team = Guid.NewGuid().ToString();

        var (pub, priv) = _sut.DeriveTeamKeypair(root, team);

        Assert.Equal(32, pub.Length);
        Assert.Equal(32, priv.Length);

        // Sign a test message and verify with the derived public key.
        var msg = Encoding.UTF8.GetBytes("team-scoped HELLO payload");
        var sig = _signer.Sign(msg, priv);
        Assert.True(_signer.Verify(msg, sig, pub),
            "Derived subkey must produce signatures verifiable under its own derived public key.");
    }

    [Fact]
    public void DeriveTeamKeypair_is_deterministic()
    {
        var (_, root) = _signer.GenerateKeyPair();
        var team = Guid.NewGuid().ToString();

        var (pub1, priv1) = _sut.DeriveTeamKeypair(root, team);
        var (pub2, priv2) = _sut.DeriveTeamKeypair(root, team);

        Assert.Equal(pub1, pub2);
        Assert.Equal(priv1, priv2);
    }

    [Fact]
    public void Cross_team_public_keys_are_pairwise_distinct_for_1000_teams()
    {
        // ADR 0032 §Device identity: "Operators of different teams see different
        // public keys — they cannot correlate the same user across teams." This
        // test asserts that operational property at scale: 1000 distinct teams
        // MUST yield 1000 distinct public keys (and 1000 distinct subkey seeds).
        var (_, root) = _signer.GenerateKeyPair();

        var publicKeys = new HashSet<string>(1000);
        var seeds = new HashSet<string>(1000);

        for (var i = 0; i < 1000; i++)
        {
            var teamId = Guid.NewGuid().ToString();
            var (pub, priv) = _sut.DeriveTeamKeypair(root, teamId);
            publicKeys.Add(Convert.ToHexString(pub));
            seeds.Add(Convert.ToHexString(priv));
        }

        Assert.Equal(1000, publicKeys.Count);
        Assert.Equal(1000, seeds.Count);
    }

    [Fact]
    public void Subkey_first_32_bytes_form_the_seed_used_by_DeriveTeamKeypair()
    {
        // Cross-check: the keypair must match what you'd get by manually passing
        // the first 32 bytes of the subkey into GenerateFromSeed.
        var (_, root) = _signer.GenerateKeyPair();
        var team = "team-alpha";

        var subkey = _sut.DeriveSubkey(root, team);
        var manualSeed = subkey.AsSpan(0, 32);
        var (manualPub, manualPriv) = _signer.GenerateFromSeed(manualSeed);

        var (pub, priv) = _sut.DeriveTeamKeypair(root, team);

        Assert.Equal(manualPub, pub);
        Assert.Equal(manualPriv, priv);
    }

    [Fact]
    public void TeamScopedNodeIdentity_Derive_preserves_NodeId_and_changes_keys()
    {
        var (rootPub, rootPriv) = _signer.GenerateKeyPair();
        const string nodeId = "aabbccdd11223344aabbccdd11223344";
        var root = new Sunfish.Kernel.Sync.Identity.NodeIdentity(nodeId, rootPub, rootPriv);

        var team = Guid.NewGuid().ToString();
        var scoped = Sunfish.Kernel.Sync.Identity.TeamScopedNodeIdentity.Derive(root, team, _sut);

        Assert.Equal(nodeId, scoped.NodeId);
        Assert.NotEqual(root.PublicKey, scoped.PublicKey);
        Assert.NotEqual(root.PrivateKey, scoped.PrivateKey);

        // Round-trip: signing with the team-scoped private key must verify under
        // its team-scoped public key — end-to-end identity flow.
        var msg = Encoding.UTF8.GetBytes("HELLO from scoped identity");
        var sig = _signer.Sign(msg, scoped.PrivateKey);
        Assert.True(_signer.Verify(msg, sig, scoped.PublicKey));
    }

    [Fact]
    public void TeamScopedNodeIdentity_Derive_is_deterministic_per_root_and_team()
    {
        var (rootPub, rootPriv) = _signer.GenerateKeyPair();
        var root = new Sunfish.Kernel.Sync.Identity.NodeIdentity(
            "aabbccdd11223344aabbccdd11223344", rootPub, rootPriv);
        var team = Guid.NewGuid().ToString();

        var a = Sunfish.Kernel.Sync.Identity.TeamScopedNodeIdentity.Derive(root, team, _sut);
        var b = Sunfish.Kernel.Sync.Identity.TeamScopedNodeIdentity.Derive(root, team, _sut);

        Assert.Equal(a.PublicKey, b.PublicKey);
        Assert.Equal(a.PrivateKey, b.PrivateKey);
    }
}
