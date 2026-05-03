using Sunfish.Kernel.Security.Attestation;
using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Kernel.Security.Tests;

public sealed class RoleAttestationTests
{
    private readonly Ed25519Signer _signer = new();

    private static byte[] NewTeamId() => Guid.NewGuid().ToByteArray();

    private (IAttestationIssuer issuer, IAttestationVerifier verifier, TimeProvider clock)
        MakeIssuerAndVerifier(DateTimeOffset now)
    {
        var clock = new StubTimeProvider(now);
        return (new AttestationIssuer(_signer, clock), new AttestationVerifier(_signer), clock);
    }

    [Fact]
    public void Issue_and_Verify_roundtrip()
    {
        var (adminPub, adminPriv) = _signer.GenerateKeyPair();
        var (subjectPub, _) = _signer.GenerateKeyPair();
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var (issuer, verifier, _) = MakeIssuerAndVerifier(now);

        var att = issuer.Issue(NewTeamId(), subjectPub, "team_member", TimeSpan.FromDays(7), adminPriv);

        Assert.Equal(adminPub, att.IssuerPublicKey);
        Assert.Equal(subjectPub, att.SubjectPublicKey);
        Assert.Equal("team_member", att.Role);
        Assert.Equal(64, att.Signature.Length);
        Assert.True(verifier.Verify(att, adminPub, now.AddMinutes(1)));
    }

    [Fact]
    public void Verify_rejects_expired_attestation()
    {
        var (adminPub, adminPriv) = _signer.GenerateKeyPair();
        var (subjectPub, _) = _signer.GenerateKeyPair();
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var (issuer, verifier, _) = MakeIssuerAndVerifier(now);

        var att = issuer.Issue(NewTeamId(), subjectPub, "team_member", TimeSpan.FromHours(1), adminPriv);

        Assert.False(verifier.Verify(att, adminPub, now.AddHours(2)));
    }

    [Fact]
    public void Verify_rejects_not_yet_valid_attestation()
    {
        var (adminPub, adminPriv) = _signer.GenerateKeyPair();
        var (subjectPub, _) = _signer.GenerateKeyPair();
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var (issuer, verifier, _) = MakeIssuerAndVerifier(now);

        var att = issuer.Issue(NewTeamId(), subjectPub, "team_member", TimeSpan.FromHours(1), adminPriv);

        Assert.False(verifier.Verify(att, adminPub, now.AddSeconds(-1)));
    }

    [Fact]
    public void Verify_rejects_signature_by_wrong_issuer()
    {
        var (_, adminPriv) = _signer.GenerateKeyPair();
        var (otherAdminPub, _) = _signer.GenerateKeyPair();
        var (subjectPub, _) = _signer.GenerateKeyPair();
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var (issuer, verifier, _) = MakeIssuerAndVerifier(now);

        var att = issuer.Issue(NewTeamId(), subjectPub, "admin", TimeSpan.FromHours(1), adminPriv);

        // Even if signature is valid, if the caller expects a different issuer we reject.
        Assert.False(verifier.Verify(att, otherAdminPub, now.AddMinutes(1)));
    }

    [Fact]
    public void Verify_rejects_tampered_role_field()
    {
        var (adminPub, adminPriv) = _signer.GenerateKeyPair();
        var (subjectPub, _) = _signer.GenerateKeyPair();
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var (issuer, verifier, _) = MakeIssuerAndVerifier(now);

        var att = issuer.Issue(NewTeamId(), subjectPub, "team_member", TimeSpan.FromHours(1), adminPriv);
        var tampered = att with { Role = "admin" };

        Assert.False(verifier.Verify(tampered, adminPub, now.AddMinutes(1)));
    }

    [Fact]
    public void Attestation_serializes_and_deserializes_via_Cbor()
    {
        var (_, adminPriv) = _signer.GenerateKeyPair();
        var (subjectPub, _) = _signer.GenerateKeyPair();
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var (issuer, _, _) = MakeIssuerAndVerifier(now);

        var original = issuer.Issue(NewTeamId(), subjectPub, "financial_role", TimeSpan.FromDays(30), adminPriv);
        var bytes = original.ToCbor();
        var restored = RoleAttestation.FromCbor(bytes);

        Assert.Equal(original.TeamId, restored.TeamId);
        Assert.Equal(original.SubjectPublicKey, restored.SubjectPublicKey);
        Assert.Equal(original.Role, restored.Role);
        // Second-level precision (we serialize Unix seconds), so compare via ToUnixTimeSeconds.
        Assert.Equal(original.IssuedAt.ToUnixTimeSeconds(), restored.IssuedAt.ToUnixTimeSeconds());
        Assert.Equal(original.ExpiresAt.ToUnixTimeSeconds(), restored.ExpiresAt.ToUnixTimeSeconds());
        Assert.Equal(original.IssuerPublicKey, restored.IssuerPublicKey);
        Assert.Equal(original.Signature, restored.Signature);
    }

    [Fact]
    public void Canonical_signable_encoding_is_deterministic()
    {
        var teamId = NewTeamId();
        var subject = new byte[32];
        var issuer = new byte[32];
        var issuedAt = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var expiresAt = issuedAt.AddHours(1);

        var a = new RoleAttestation(teamId, subject, "r", issuedAt, expiresAt, issuer, new byte[64]);
        var b = new RoleAttestation(teamId, subject, "r", issuedAt, expiresAt, issuer, new byte[64]);

        Assert.Equal(a.ToSignable(), b.ToSignable());
    }

    [Fact]
    public void Bundle_serializes_and_deserializes_multiple_roles()
    {
        var (_, adminPriv) = _signer.GenerateKeyPair();
        var (subjectPub, _) = _signer.GenerateKeyPair();
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var (issuer, _, _) = MakeIssuerAndVerifier(now);

        var teamId = NewTeamId();
        var a1 = issuer.Issue(teamId, subjectPub, "team_member", TimeSpan.FromDays(7), adminPriv);
        var a2 = issuer.Issue(teamId, subjectPub, "financial_role", TimeSpan.FromDays(7), adminPriv);

        var bundle = new AttestationBundle(new[] { a1, a2 });
        var restored = AttestationBundle.FromCbor(bundle.ToCbor());

        Assert.Equal(2, restored.Attestations.Count);
        Assert.Equal("team_member", restored.Attestations[0].Role);
        Assert.Equal("financial_role", restored.Attestations[1].Role);
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
