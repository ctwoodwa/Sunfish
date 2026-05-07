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

        var t1 = EncryptionHandshake.ComputeTranscriptHash(
            ephemA, idA, ephemB, idB, tenant,
            inviteCapabilities: 0x07, negotiatedCapability: (byte)ChannelCapability.Text,
            presenceCapsInitiator: 0x07, presenceCapsResponder: 0x03);
        var t2 = EncryptionHandshake.ComputeTranscriptHash(
            ephemA, idA, ephemB, idB, tenant,
            inviteCapabilities: 0x07, negotiatedCapability: (byte)ChannelCapability.Text,
            presenceCapsInitiator: 0x07, presenceCapsResponder: 0x03);

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

        var t1 = EncryptionHandshake.ComputeTranscriptHash(
            ephemA, idA, ephemB, idB, tenant,
            inviteCapabilities: 0x07, negotiatedCapability: (byte)ChannelCapability.Text,
            presenceCapsInitiator: 0x07, presenceCapsResponder: 0x07);
        var t2 = EncryptionHandshake.ComputeTranscriptHash(
            ephemA, idA, ephemB, idB, tenant,
            inviteCapabilities: 0x07, negotiatedCapability: (byte)ChannelCapability.Audio,
            presenceCapsInitiator: 0x07, presenceCapsResponder: 0x07);

        Assert.NotEqual(t1, t2);
        Assert.False(EncryptionHandshake.TranscriptsMatch(t1, t2));

        // Tampered transcript — flip one byte — also rejects.
        var tampered = (byte[])t1.Clone();
        tampered[0] ^= 0xFF;
        Assert.False(EncryptionHandshake.TranscriptsMatch(t1, tampered));
    }

    [Fact]
    public void TranscriptHash_DivergesOnInviteCapabilities()
    {
        // ADR 0076-A2: a relay-MITM that downgrades INVITE.capabilities (0x07
        // → 0x01) without touching ACCEPT.capability would otherwise produce
        // matching transcripts on both peers. Binding inviteCapabilities into
        // the hash means the responder's transcript (computed over the bytes
        // it actually saw on the wire) diverges from the initiator's
        // transcript (computed over the bytes it sent), surfacing as
        // ChannelTerminationReason.TranscriptMismatch on CONFIRM exchange.
        var ephemA = NewEphemeralPublicKey();
        var ephemB = NewEphemeralPublicKey();
        var idA = NewIdentityPublicKey();
        var idB = NewIdentityPublicKey();
        var tenant = EncryptionHandshake.TenantBytes(Tenant);

        var initiatorView = EncryptionHandshake.ComputeTranscriptHash(
            ephemA, idA, ephemB, idB, tenant,
            inviteCapabilities: 0x07, negotiatedCapability: 0x01,
            presenceCapsInitiator: 0x07, presenceCapsResponder: 0x07);
        var responderViewAfterMitm = EncryptionHandshake.ComputeTranscriptHash(
            ephemA, idA, ephemB, idB, tenant,
            inviteCapabilities: 0x01, negotiatedCapability: 0x01,
            presenceCapsInitiator: 0x07, presenceCapsResponder: 0x07);

        Assert.False(EncryptionHandshake.TranscriptsMatch(initiatorView, responderViewAfterMitm));
    }

    [Fact]
    public void TranscriptHash_DivergesOnPresenceCaps()
    {
        // ADR 0076-A1: a relay-MITM that rewrites HELLO.presence.caps in
        // either direction must surface as a transcript mismatch — same
        // mechanic as the INVITE.capabilities downgrade, applied to the
        // presence broadcast bytes both peers commit to during HELLO.
        var ephemA = NewEphemeralPublicKey();
        var ephemB = NewEphemeralPublicKey();
        var idA = NewIdentityPublicKey();
        var idB = NewIdentityPublicKey();
        var tenant = EncryptionHandshake.TenantBytes(Tenant);

        var initiatorView = EncryptionHandshake.ComputeTranscriptHash(
            ephemA, idA, ephemB, idB, tenant,
            inviteCapabilities: 0x07, negotiatedCapability: 0x01,
            presenceCapsInitiator: 0x07, presenceCapsResponder: 0x07);
        var responderViewAfterPresenceTamper = EncryptionHandshake.ComputeTranscriptHash(
            ephemA, idA, ephemB, idB, tenant,
            inviteCapabilities: 0x07, negotiatedCapability: 0x01,
            presenceCapsInitiator: 0x07, presenceCapsResponder: 0x01);

        Assert.False(EncryptionHandshake.TranscriptsMatch(initiatorView, responderViewAfterPresenceTamper));
    }

    [Theory]
    [InlineData(
        // V7: tenant 'tenant-001-acme'; inviteCaps=0x07; negotiatedCap=0x01;
        // presenceCapsA=0x07; presenceCapsB=0x03. Expected hash from the
        // canonical ADR 0076-A1+A2 generator at tools/icm/generate-channel-vectors.py.
        "005f111c8869fa005c1df5c8c775eb95a6a7dca4393e5df3ad152e017d78b23e",
        "4dba7077e2cbb3f4b66e1fb8e07911c9110d918326e707f60b8494974e85db35",
        "0238bb7243d92826e653ae2b9f98b2fe93661fe19e5e53ca40d8f1552389fb3c",
        "01f61a817230f3abf2e2b88665cbd05d44aa6d8dfcdc6a58cfa24df20b6cd50a",
        "tenant-001-acme",
        (byte)0x07, (byte)0x01, (byte)0x07, (byte)0x03,
        "5c38292a921ea0bff9a3b20b49e255d8e8eb06579e1aaa44aa11ad539f03a8fb")]
    [InlineData(
        // V8: zero-length tenant edge case (uint32BE(0) length-prefix with no
        // tenant bytes); all caps=0x01.
        "005f111c8869fa005c1df5c8c775eb95a6a7dca4393e5df3ad152e017d78b23e",
        "4dba7077e2cbb3f4b66e1fb8e07911c9110d918326e707f60b8494974e85db35",
        "0238bb7243d92826e653ae2b9f98b2fe93661fe19e5e53ca40d8f1552389fb3c",
        "01f61a817230f3abf2e2b88665cbd05d44aa6d8dfcdc6a58cfa24df20b6cd50a",
        "",
        (byte)0x01, (byte)0x01, (byte)0x01, (byte)0x01,
        "de32c848e5a0c63e7919201a2d984a2d7fc0efef0ba928a4a3845ba02ee4d039")]
    [InlineData(
        // V9: UTF-8 multi-byte tenant; inviteCaps=0x07 → negotiatedCap=0x02
        // (audio); presenceCapsA=0x07; presenceCapsB=0x06.
        "005f111c8869fa005c1df5c8c775eb95a6a7dca4393e5df3ad152e017d78b23e",
        "4dba7077e2cbb3f4b66e1fb8e07911c9110d918326e707f60b8494974e85db35",
        "0238bb7243d92826e653ae2b9f98b2fe93661fe19e5e53ca40d8f1552389fb3c",
        "01f61a817230f3abf2e2b88665cbd05d44aa6d8dfcdc6a58cfa24df20b6cd50a",
        "tenant-é-ünïcödë",
        (byte)0x07, (byte)0x02, (byte)0x07, (byte)0x06,
        "852c278135eccb596882767608598806eeefca4c5b546e22984497102d906cb6")]
    public void TranscriptHash_KnownAnswerVectors_MatchAdr0076A1A2Generator(
        string initEphemHex, string initIdHex,
        string respEphemHex, string respIdHex,
        string tenantValue,
        byte inviteCaps, byte negotiatedCap,
        byte presenceCapsA, byte presenceCapsB,
        string expectedSha256Hex)
    {
        var actual = EncryptionHandshake.ComputeTranscriptHash(
            HexToBytes(initEphemHex), HexToBytes(initIdHex),
            HexToBytes(respEphemHex), HexToBytes(respIdHex),
            System.Text.Encoding.UTF8.GetBytes(tenantValue),
            inviteCapabilities: inviteCaps,
            negotiatedCapability: negotiatedCap,
            presenceCapsInitiator: presenceCapsA,
            presenceCapsResponder: presenceCapsB);

        Assert.Equal(expectedSha256Hex, Convert.ToHexString(actual).ToLowerInvariant());
    }

    private static byte[] HexToBytes(string hex)
    {
        if (hex.Length == 0) return Array.Empty<byte>();
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
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
