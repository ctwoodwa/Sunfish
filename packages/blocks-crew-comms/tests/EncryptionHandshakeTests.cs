using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NSec.Cryptography;
using Sunfish.Blocks.CrewComms.Crypto;
using Sunfish.Blocks.CrewComms.Protocol;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Blocks.CrewComms.Tests;

public class EncryptionHandshakeTests
{
    private static readonly TenantId Tenant = new("acme");

    [Fact]
    public async Task HelloSignature_VerifiedByCounterparty()
    {
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        var roster = new TestRoster(new[] { keyA, keyB });

        using var hsB = new EncryptionHandshake(keyB, roster, Tenant);

        var ephemA = NewEphemeralPublicKey();
        using var hsA = new EncryptionHandshake(keyA, roster, Tenant);
        var helloA = hsA.BuildHello(ephemA, ChannelCapability.Text, DateTimeOffset.UtcNow);

        var verifiedPeer = await hsB.VerifyHelloAsync(helloA, CancellationToken.None);
        Assert.Equal(PeerId.From(keyA.PrincipalId), verifiedPeer);
    }

    [Fact]
    public async Task HelloSignature_TamperedIdentityKey_Rejected()
    {
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        using var keyImposter = KeyPair.Generate();
        var roster = new TestRoster(new[] { keyA, keyB, keyImposter });

        using var hsA = new EncryptionHandshake(keyA, roster, Tenant);
        using var hsB = new EncryptionHandshake(keyB, roster, Tenant);
        var hello = hsA.BuildHello(NewEphemeralPublicKey(), ChannelCapability.Text, DateTimeOffset.UtcNow);

        // Replace the identity key with the imposter's, leaving keyA's signature.
        var tampered = hello with { IdentityPublicKey = keyImposter.PrincipalId.AsSpan().ToArray() };

        await Assert.ThrowsAsync<CryptographicException>(
            () => hsB.VerifyHelloAsync(tampered, CancellationToken.None));
    }

    [Fact]
    public async Task TenantRoster_PeerNotInRoster_Rejected()
    {
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        // Roster knows only keyB locally. keyA is not registered.
        var roster = new TestRoster(new[] { keyB });

        using var hsA = new EncryptionHandshake(keyA, roster, Tenant);
        using var hsB = new EncryptionHandshake(keyB, roster, Tenant);
        var hello = hsA.BuildHello(NewEphemeralPublicKey(), ChannelCapability.Text, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<ArgumentException>(
            () => hsB.VerifyHelloAsync(hello, CancellationToken.None));
    }

    [Fact]
    public async Task TenantBinding_DifferentTenant_Rejected()
    {
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        var roster = new TestRoster(new[] { keyA, keyB });

        using var hsA = new EncryptionHandshake(keyA, roster, new TenantId("acme"));
        using var hsB = new EncryptionHandshake(keyB, roster, new TenantId("globex"));
        var hello = hsA.BuildHello(NewEphemeralPublicKey(), ChannelCapability.Text, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<ArgumentException>(
            () => hsB.VerifyHelloAsync(hello, CancellationToken.None));
    }

    [Fact]
    public void SharedSecret_BothPeersAgreeOnSessionKeyBytes()
    {
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        var roster = new TestRoster(new[] { keyA, keyB });

        using var ephemA = Key.Create(KeyAgreementAlgorithm.X25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        using var ephemB = Key.Create(KeyAgreementAlgorithm.X25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        var ephemAPub = ephemA.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        var ephemBPub = ephemB.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        var peerA = PeerId.From(keyA.PrincipalId);
        var peerB = PeerId.From(keyB.PrincipalId);

        using var hsA = new EncryptionHandshake(keyA, roster, Tenant);
        using var hsB = new EncryptionHandshake(keyB, roster, Tenant);

        // Both peers agree initiator=A, responder=B (by glare resolution).
        hsA.DeriveSessionKey(ephemA, ephemBPub, peerA, peerB);
        hsB.DeriveSessionKey(ephemB, ephemAPub, peerA, peerB);

        Assert.NotNull(hsA.SessionKey);
        Assert.NotNull(hsB.SessionKey);

        var keyABytes = hsA.SessionKey!.Export(KeyBlobFormat.RawSymmetricKey);
        var keyBBytes = hsB.SessionKey!.Export(KeyBlobFormat.RawSymmetricKey);
        Assert.Equal(keyABytes, keyBBytes);
    }

    [Fact]
    public void TranscriptHash_BothPeersComputeIdenticalBytes()
    {
        var ephemA = NewEphemeralPublicKey();
        var ephemB = NewEphemeralPublicKey();
        var idA = NewIdentityPublicKey();
        var idB = NewIdentityPublicKey();
        var tenant = EncryptionHandshake.TenantBytes(Tenant);

        var t1 = EncryptionHandshake.ComputeTranscriptHash(ephemA, idA, ephemB, idB, tenant, (byte)ChannelCapability.Text);
        var t2 = EncryptionHandshake.ComputeTranscriptHash(ephemA, idA, ephemB, idB, tenant, (byte)ChannelCapability.Text);

        Assert.Equal(t1, t2);
        Assert.True(EncryptionHandshake.TranscriptsMatch(t1, t2));
    }

    [Fact]
    public void EncryptionHandshake_ConfirmMismatchRejects()
    {
        // Both peers compute the transcript over the SAME shared inputs except
        // the negotiated capability. A divergence at any input MUST cause
        // TranscriptsMatch to return false — i.e., the rejection path that
        // P3's NativeChannelSession will surface as ChannelTerminationReason.TranscriptMismatch.
        var ephemA = NewEphemeralPublicKey();
        var ephemB = NewEphemeralPublicKey();
        var idA = NewIdentityPublicKey();
        var idB = NewIdentityPublicKey();
        var tenant = EncryptionHandshake.TenantBytes(Tenant);

        var t1 = EncryptionHandshake.ComputeTranscriptHash(ephemA, idA, ephemB, idB, tenant, (byte)ChannelCapability.Text);
        var t2 = EncryptionHandshake.ComputeTranscriptHash(ephemA, idA, ephemB, idB, tenant, (byte)ChannelCapability.Audio);

        Assert.NotEqual(t1, t2);
        Assert.False(EncryptionHandshake.TranscriptsMatch(t1, t2));

        // Tampered transcript — flip one byte — also rejects.
        var tampered = (byte[])t1.Clone();
        tampered[0] ^= 0xFF;
        Assert.False(EncryptionHandshake.TranscriptsMatch(t1, tampered));
    }

    [Fact]
    public void SessionKey_RoundTripsChaCha20Poly1305()
    {
        // Closes the "derived key actually works for AEAD" loop before P3
        // depends on it — derive the same session key from both peers and
        // verify that ChaCha20-Poly1305 ciphertext from peer A decrypts on
        // peer B's key.
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        var roster = new TestRoster(new[] { keyA, keyB });

        using var ephemA = Key.Create(KeyAgreementAlgorithm.X25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        using var ephemB = Key.Create(KeyAgreementAlgorithm.X25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        var ephemAPub = ephemA.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        var ephemBPub = ephemB.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        var peerA = PeerId.From(keyA.PrincipalId);
        var peerB = PeerId.From(keyB.PrincipalId);

        using var hsA = new EncryptionHandshake(keyA, roster, Tenant);
        using var hsB = new EncryptionHandshake(keyB, roster, Tenant);
        hsA.DeriveSessionKey(ephemA, ephemBPub, peerA, peerB);
        hsB.DeriveSessionKey(ephemB, ephemAPub, peerA, peerB);

        var aead = AeadAlgorithm.ChaCha20Poly1305;
        var nonce = new byte[aead.NonceSize]; // 12 zero bytes — single-shot test, never reused in production.
        var plaintext = System.Text.Encoding.UTF8.GetBytes("ahoy from peer A");
        var ciphertext = aead.Encrypt(hsA.SessionKey!, nonce, ReadOnlySpan<byte>.Empty, plaintext);
        var roundTripped = aead.Decrypt(hsB.SessionKey!, nonce, ReadOnlySpan<byte>.Empty, ciphertext)
            ?? throw new InvalidOperationException("AEAD decrypt returned null — peer B failed to authenticate ciphertext.");
        Assert.Equal(plaintext, roundTripped);
    }

    [Fact]
    public void HeartbeatSignature_RoundTrip_VerifiesAgainstIdentityKey()
    {
        using var keyA = KeyPair.Generate();
        var roster = new TestRoster(new[] { keyA });
        using var hs = new EncryptionHandshake(keyA, roster, Tenant);
        var hello = hs.BuildHello(NewEphemeralPublicKey(), ChannelCapability.Text, DateTimeOffset.UtcNow);

        Assert.True(EncryptionHandshake.VerifyHeartbeat(
            hello.Presence, keyA.PrincipalId.AsSpan()));

        // Tampered timestamp invalidates the heartbeat signature.
        var tampered = hello.Presence with { Timestamp = hello.Presence.Timestamp + 1 };
        Assert.False(EncryptionHandshake.VerifyHeartbeat(
            tampered, keyA.PrincipalId.AsSpan()));
    }

    private static byte[] NewEphemeralPublicKey()
    {
        using var k = Key.Create(KeyAgreementAlgorithm.X25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        return k.PublicKey.Export(KeyBlobFormat.RawPublicKey);
    }

    private static byte[] NewIdentityPublicKey()
    {
        using var k = KeyPair.Generate();
        return k.PrincipalId.AsSpan().ToArray();
    }

    private sealed class TestRoster : ICrewRoster
    {
        private readonly IReadOnlyList<CrewMember> _members;
        public TestRoster(IEnumerable<KeyPair> keys)
        {
            var list = new List<CrewMember>();
            var i = 0;
            foreach (var k in keys)
                list.Add(new CrewMember { Peer = PeerId.From(k.PrincipalId), DisplayName = $"member-{i++}" });
            _members = list;
        }

        public Task<IReadOnlyList<CrewMember>> GetCrewAsync(TenantId tenant, CancellationToken ct)
            => Task.FromResult(_members);
    }
}
