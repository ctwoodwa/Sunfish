using Sunfish.Anchor.Services;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Security.Attestation;
using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Anchor.Tests;

public sealed class QrOnboardingServiceTests
{
    private readonly Ed25519Signer _signer = new();
    private readonly IEncryptedStore _store = new NoopEncryptedStore();
    private readonly StubTimeProvider _clock = new(new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero));

    private QrOnboardingService MakeService(TimeProvider? clock = null)
    {
        var effectiveClock = clock ?? _clock;
        var issuer = new AttestationIssuer(_signer, effectiveClock);
        var verifier = new AttestationVerifier(_signer);
        return new QrOnboardingService(_signer, _store, verifier, issuer, effectiveClock);
    }

    [Fact]
    public async Task Encode_then_Decode_roundtrip_preserves_bundle_and_snapshot()
    {
        var svc = MakeService();
        var founder = await svc.GenerateFounderBundleAsync("Acme", CancellationToken.None);
        var snapshot = new byte[] { 1, 2, 3, 4, 5 };

        var payload = await svc.EncodePayloadAsync(founder.Bundle, snapshot, CancellationToken.None);
        var decoded = await svc.DecodePayloadAsync(payload, CancellationToken.None);

        Assert.Equal(founder.Bundle.Attestations.Count, decoded.Bundle.Attestations.Count);
        Assert.Equal(founder.Bundle.Attestations[0].TeamId, decoded.Bundle.Attestations[0].TeamId);
        Assert.Equal(founder.Bundle.Attestations[0].SubjectPublicKey, decoded.Bundle.Attestations[0].SubjectPublicKey);
        Assert.Equal(snapshot, decoded.InitialSnapshot.ToArray());
    }

    [Fact]
    public async Task GenerateFounderBundle_produces_self_signed_attestation()
    {
        var svc = MakeService();

        var result = await svc.GenerateFounderBundleAsync("My Team", CancellationToken.None);

        var attestation = Assert.Single(result.Bundle.Attestations);
        // Founder: issuer == subject.
        Assert.Equal(attestation.SubjectPublicKey, attestation.IssuerPublicKey);
        Assert.Equal("admin", attestation.Role);
        // Device public key on the result matches the attestation's subject.
        Assert.Equal(result.DevicePublicKey, attestation.SubjectPublicKey);
        Assert.Equal(32, result.DevicePrivateKey.Length);
    }

    [Fact]
    public async Task GenerateFounderBundle_produces_verifiable_attestation()
    {
        var svc = MakeService();
        var verifier = new AttestationVerifier(_signer);

        var result = await svc.GenerateFounderBundleAsync("My Team", CancellationToken.None);
        var attestation = result.Bundle.Attestations[0];

        // Self-signed: verify against the attestation's own issuer key at clock-now.
        Assert.True(verifier.Verify(attestation, attestation.IssuerPublicKey, _clock.GetUtcNow()));
    }

    [Fact]
    public async Task DecodePayload_rejects_payload_shorter_than_length_prefixes()
    {
        var svc = MakeService();
        var tiny = new byte[] { 1, 2, 3 };

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.DecodePayloadAsync(tiny, CancellationToken.None));
    }

    [Fact]
    public async Task DecodePayload_rejects_truncated_bundle_bytes()
    {
        var svc = MakeService();
        var founder = await svc.GenerateFounderBundleAsync("Acme", CancellationToken.None);
        var full = await svc.EncodePayloadAsync(founder.Bundle, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        // Drop the tail half — the bundle-length prefix now over-runs the buffer.
        var truncated = full.ToArray().AsMemory(0, Math.Max(5, full.Length / 2));

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.DecodePayloadAsync(truncated, CancellationToken.None));
    }

    [Fact]
    public async Task DecodePayload_preserves_snapshot_bytes_exactly()
    {
        var svc = MakeService();
        var founder = await svc.GenerateFounderBundleAsync("Acme", CancellationToken.None);
        var snapshot = new byte[4096];
        System.Security.Cryptography.RandomNumberGenerator.Fill(snapshot);

        var payload = await svc.EncodePayloadAsync(founder.Bundle, snapshot, CancellationToken.None);
        var decoded = await svc.DecodePayloadAsync(payload, CancellationToken.None);

        Assert.Equal(snapshot, decoded.InitialSnapshot.ToArray());
    }

    [Fact]
    public async Task DecodePayload_rejects_expired_attestation()
    {
        // Founder issued at t=12:00, valid for 365 days. Verifier clock set to
        // t+400 days should trip the ExpiresAt check.
        var issueClock = new StubTimeProvider(new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero));
        var issuerSvc = MakeService(issueClock);
        var founder = await issuerSvc.GenerateFounderBundleAsync("Acme", CancellationToken.None);
        var payload = await issuerSvc.EncodePayloadAsync(founder.Bundle, ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        // Decode with a clock set well past ExpiresAt.
        var futureClock = new StubTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var decoderSvc = MakeService(futureClock);

        await Assert.ThrowsAsync<InvalidOnboardingPayloadException>(
            () => decoderSvc.DecodePayloadAsync(payload, CancellationToken.None));
    }

    [Fact]
    public async Task DecodePayload_rejects_tampered_signature()
    {
        var svc = MakeService();
        var founder = await svc.GenerateFounderBundleAsync("Acme", CancellationToken.None);

        // Flip a bit in the signature; re-wrap in a new bundle/payload.
        var att = founder.Bundle.Attestations[0];
        var badSig = (byte[])att.Signature.Clone();
        badSig[0] ^= 0xFF;
        var tampered = att with { Signature = badSig };
        var tamperedBundle = new AttestationBundle(new[] { tampered });
        var payload = await svc.EncodePayloadAsync(tamperedBundle, ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOnboardingPayloadException>(
            () => svc.DecodePayloadAsync(payload, CancellationToken.None));
    }

    [Fact]
    public async Task IssueJoinerAttestation_is_verifiable_with_founder_public_key()
    {
        var svc = MakeService();
        var verifier = new AttestationVerifier(_signer);
        var founder = await svc.GenerateFounderBundleAsync("Acme", CancellationToken.None);
        var founderAtt = founder.Bundle.Attestations[0];

        var (joinerPub, _) = _signer.GenerateKeyPair();
        var joinerBundle = await svc.IssueJoinerAttestationAsync(
            teamId: founderAtt.TeamId,
            joinerPublicKey: joinerPub,
            founderPrivateKey: founder.DevicePrivateKey,
            ct: CancellationToken.None);

        var joinerAtt = Assert.Single(joinerBundle.Attestations);
        Assert.Equal("team_member", joinerAtt.Role);
        Assert.Equal(joinerPub, joinerAtt.SubjectPublicKey);
        Assert.Equal(founder.DevicePublicKey, joinerAtt.IssuerPublicKey);
        Assert.True(verifier.Verify(joinerAtt, founder.DevicePublicKey, _clock.GetUtcNow()));
    }

    private sealed class StubTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public StubTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>
    /// No-op <see cref="IEncryptedStore"/> — QrOnboardingService keeps a field
    /// reference for future wiring but doesn't exercise the store in tests.
    /// </summary>
    private sealed class NoopEncryptedStore : IEncryptedStore
    {
        public Task OpenAsync(string databasePath, ReadOnlyMemory<byte> key, CancellationToken ct) => Task.CompletedTask;
        public Task<byte[]?> GetAsync(string key, CancellationToken ct) => Task.FromResult<byte[]?>(null);
        public Task SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task CloseAsync() => Task.CompletedTask;
    }
}
