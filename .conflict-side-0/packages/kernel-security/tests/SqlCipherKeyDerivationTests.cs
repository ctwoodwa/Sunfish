using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Kernel.Security.Tests;

/// <summary>
/// Wave 6.3.B stop-work resolution tests — verifies that the second HKDF-Expand
/// pass over the root seed yields a deterministic, per-team, and cryptographically
/// distinct key from what <see cref="TeamSubkeyDerivation"/> produces for the
/// same inputs.
/// </summary>
public sealed class SqlCipherKeyDerivationTests
{
    private readonly Ed25519Signer _signer = new();
    private readonly SqlCipherKeyDerivation _sut = new();

    [Fact]
    public void DeriveSqlCipherKey_is_deterministic_for_same_root_and_teamId()
    {
        var (_, root) = _signer.GenerateKeyPair();
        var teamId = Guid.NewGuid().ToString("D");

        var a = _sut.DeriveSqlCipherKey(root, teamId);
        var b = _sut.DeriveSqlCipherKey(root, teamId);

        Assert.Equal(a, b);
        Assert.Equal(SqlCipherKeyDerivation.SqlCipherKeyLength, a.Length);
    }

    [Fact]
    public void DeriveSqlCipherKey_varies_across_team_ids_with_same_root()
    {
        var (_, root) = _signer.GenerateKeyPair();
        var t1 = Guid.NewGuid().ToString("D");
        var t2 = Guid.NewGuid().ToString("D");
        var t3 = Guid.NewGuid().ToString("D");

        var a = _sut.DeriveSqlCipherKey(root, t1);
        var b = _sut.DeriveSqlCipherKey(root, t2);
        var c = _sut.DeriveSqlCipherKey(root, t3);

        Assert.NotEqual(a, b);
        Assert.NotEqual(b, c);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void DeriveSqlCipherKey_is_domain_separated_from_TeamSubkeyDerivation()
    {
        // The whole point of the second-HKDF-call design: for the same root
        // + team id, the SQLCipher key MUST NOT equal any prefix of the
        // signing-subkey derivation. If this test ever fails, the info-label
        // separation has been broken and a reviewer should not ship the change.
        var (_, root) = _signer.GenerateKeyPair();
        var teamId = Guid.NewGuid().ToString("D");

        var sqlCipherKey = _sut.DeriveSqlCipherKey(root, teamId);

        var subkeyDeriv = new TeamSubkeyDerivation(_signer);
        var signingSubkey = subkeyDeriv.DeriveSubkey(root, teamId);

        // signingSubkey is 64 bytes (seed || reserved) — assert the SQLCipher
        // key is neither the first 32 bytes nor the second 32 bytes.
        Assert.NotEqual(sqlCipherKey, signingSubkey.AsSpan(0, 32).ToArray());
        Assert.NotEqual(sqlCipherKey, signingSubkey.AsSpan(32, 32).ToArray());
    }

    [Fact]
    public void DeriveSqlCipherKey_rejects_wrong_root_length()
    {
        var badRoot = new byte[16];
        Assert.Throws<ArgumentException>(() => _sut.DeriveSqlCipherKey(badRoot, "team-a"));
    }

    [Fact]
    public void DeriveSqlCipherKey_rejects_null_or_empty_team_id()
    {
        var (_, root) = _signer.GenerateKeyPair();
        // ArgumentException.ThrowIfNullOrEmpty throws ArgumentException for empty
        // and ArgumentNullException (a subclass) for null; assert the expected
        // exact exception type for each branch.
        Assert.Throws<ArgumentException>(() => _sut.DeriveSqlCipherKey(root, ""));
        Assert.Throws<ArgumentNullException>(() => _sut.DeriveSqlCipherKey(root, null!));
    }
}
