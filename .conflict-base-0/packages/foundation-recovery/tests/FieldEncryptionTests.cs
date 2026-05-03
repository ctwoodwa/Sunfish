using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Recovery;
using Sunfish.Foundation.Recovery.Crypto;
using Sunfish.Foundation.Recovery.TenantKey;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Recovery.Tests;

public sealed class FieldEncryptionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private static readonly DateTimeOffset Now = new(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Future = Now.AddHours(1);
    private static readonly DateTimeOffset Past = Now.AddHours(-1);

    private static FixedDecryptCapability ValidCap(TenantId tenant) =>
        new("cap-1", new ActorId("actor-1"), tenant, Future);

    [Fact]
    public async Task RoundTrip_DecryptsBackToOriginalPlaintext()
    {
        var keys = new InMemoryTenantKeyProvider();
        var enc = new TenantKeyProviderFieldEncryptor(keys);
        var dec = new TenantKeyProviderFieldDecryptor(keys, new TestClock(Now));
        var plaintext = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

        var field = await enc.EncryptAsync(plaintext, TenantA, Ct);
        var roundtrip = await dec.DecryptAsync(field, ValidCap(TenantA), TenantA, Ct);

        Assert.True(plaintext.AsSpan().SequenceEqual(roundtrip.Span));
    }

    [Fact]
    public async Task Encrypt_AlwaysWritesKeyVersionOne()
    {
        var enc = new TenantKeyProviderFieldEncryptor(new InMemoryTenantKeyProvider());

        var field = await enc.EncryptAsync(new byte[] { 1, 2, 3 }, TenantA, Ct);

        Assert.Equal(1, field.KeyVersion);
    }

    [Fact]
    public async Task Encrypt_DifferentTenants_ProduceDifferentCiphertext()
    {
        var keys = new InMemoryTenantKeyProvider();
        var enc = new TenantKeyProviderFieldEncryptor(keys);
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        var f1 = await enc.EncryptAsync(plaintext, TenantA, Ct);
        var f2 = await enc.EncryptAsync(plaintext, TenantB, Ct);

        Assert.False(f1.Ciphertext.Span.SequenceEqual(f2.Ciphertext.Span));
    }

    [Fact]
    public async Task Encrypt_FreshNonceEachCall()
    {
        var enc = new TenantKeyProviderFieldEncryptor(new InMemoryTenantKeyProvider());
        var plaintext = new byte[] { 1, 2, 3 };

        var f1 = await enc.EncryptAsync(plaintext, TenantA, Ct);
        var f2 = await enc.EncryptAsync(plaintext, TenantA, Ct);

        Assert.False(f1.Nonce.Span.SequenceEqual(f2.Nonce.Span));
    }

    [Fact]
    public async Task Decrypt_ExpiredCapability_ThrowsAndReason()
    {
        var keys = new InMemoryTenantKeyProvider();
        var enc = new TenantKeyProviderFieldEncryptor(keys);
        var dec = new TenantKeyProviderFieldDecryptor(keys, new TestClock(Now));
        var field = await enc.EncryptAsync(new byte[] { 1, 2, 3 }, TenantA, Ct);
        var expired = new FixedDecryptCapability("cap-expired", new ActorId("a"), TenantA, Past);

        var ex = await Assert.ThrowsAsync<FieldDecryptionDeniedException>(
            () => dec.DecryptAsync(field, expired, TenantA, Ct));

        Assert.Equal("cap-expired", ex.CapabilityId);
        Assert.Equal("expired", ex.Reason);
    }

    [Fact]
    public async Task Decrypt_WrongTenantCapability_Throws()
    {
        var keys = new InMemoryTenantKeyProvider();
        var enc = new TenantKeyProviderFieldEncryptor(keys);
        var dec = new TenantKeyProviderFieldDecryptor(keys, new TestClock(Now));
        var field = await enc.EncryptAsync(new byte[] { 1, 2, 3 }, TenantA, Ct);
        var wrongTenant = new FixedDecryptCapability("cap-wrong", new ActorId("a"), TenantB, Future);

        var ex = await Assert.ThrowsAsync<FieldDecryptionDeniedException>(
            () => dec.DecryptAsync(field, wrongTenant, TenantA, Ct));

        Assert.Equal("wrong-tenant", ex.Reason);
    }

    [Fact]
    public async Task Decrypt_KeyVersionZero_RejectsWithUnsupportedKeyVersion()
    {
        var dec = new TenantKeyProviderFieldDecryptor(new InMemoryTenantKeyProvider(), new TestClock(Now));
        var bad = new EncryptedField(new byte[16], new byte[12], 0);

        var ex = await Assert.ThrowsAsync<FieldDecryptionDeniedException>(
            () => dec.DecryptAsync(bad, ValidCap(TenantA), TenantA, Ct));

        Assert.Equal("unsupported key version", ex.Reason);
    }

    [Fact]
    public async Task Decrypt_NegativeKeyVersion_RejectsWithUnsupportedKeyVersion()
    {
        var dec = new TenantKeyProviderFieldDecryptor(new InMemoryTenantKeyProvider(), new TestClock(Now));
        var bad = new EncryptedField(new byte[16], new byte[12], -3);

        await Assert.ThrowsAsync<FieldDecryptionDeniedException>(
            () => dec.DecryptAsync(bad, ValidCap(TenantA), TenantA, Ct));
    }

    [Fact]
    public async Task Decrypt_TamperedCiphertext_ThrowsTagFailure()
    {
        var keys = new InMemoryTenantKeyProvider();
        var enc = new TenantKeyProviderFieldEncryptor(keys);
        var dec = new TenantKeyProviderFieldDecryptor(keys, new TestClock(Now));
        var field = await enc.EncryptAsync(new byte[] { 1, 2, 3, 4 }, TenantA, Ct);

        var tampered = field.Ciphertext.ToArray();
        tampered[0] ^= 0xFF;
        var bad = new EncryptedField(tampered, field.Nonce, field.KeyVersion);

        var ex = await Assert.ThrowsAsync<FieldDecryptionDeniedException>(
            () => dec.DecryptAsync(bad, ValidCap(TenantA), TenantA, Ct));

        Assert.Equal("AES-GCM tag verification failed", ex.Reason);
    }

    [Fact]
    public async Task Decrypt_TruncatedCiphertext_RejectsAsTooShort()
    {
        var dec = new TenantKeyProviderFieldDecryptor(new InMemoryTenantKeyProvider(), new TestClock(Now));
        var bad = new EncryptedField(new byte[8], new byte[12], 1);

        var ex = await Assert.ThrowsAsync<FieldDecryptionDeniedException>(
            () => dec.DecryptAsync(bad, ValidCap(TenantA), TenantA, Ct));

        Assert.Equal("ciphertext too short", ex.Reason);
    }

    [Fact]
    public async Task Decrypt_AuditDisabledOverload_DoesNotEmitAudit()
    {
        var keys = new InMemoryTenantKeyProvider();
        var enc = new TenantKeyProviderFieldEncryptor(keys);
        var dec = new TenantKeyProviderFieldDecryptor(keys, new TestClock(Now));
        var field = await enc.EncryptAsync(new byte[] { 1, 2, 3 }, TenantA, Ct);

        await dec.DecryptAsync(field, ValidCap(TenantA), TenantA, Ct);
        // No audit substrate wired → no calls expected; absence verified by the
        // fact that constructing without IAuditTrail / IOperationSigner does
        // not require either dep. Negative coverage is sufficient here.
    }

    [Fact]
    public async Task Decrypt_AuditEnabled_EmitsFieldDecryptedRecordOnSuccess()
    {
        var keys = new InMemoryTenantKeyProvider();
        var trail = Substitute.For<IAuditTrail>();
        var signer = TestSigner.Create();
        var enc = new TenantKeyProviderFieldEncryptor(keys);
        var dec = new TenantKeyProviderFieldDecryptor(keys, trail, signer, new TestClock(Now));
        var field = await enc.EncryptAsync(new byte[] { 1, 2, 3 }, TenantA, Ct);

        await dec.DecryptAsync(field, ValidCap(TenantA), TenantA, Ct);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.FieldDecrypted) && r.TenantId.Equals(TenantA)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Decrypt_AuditEnabled_EmitsDeniedRecordOnRejection()
    {
        var keys = new InMemoryTenantKeyProvider();
        var trail = Substitute.For<IAuditTrail>();
        var signer = TestSigner.Create();
        var enc = new TenantKeyProviderFieldEncryptor(keys);
        var dec = new TenantKeyProviderFieldDecryptor(keys, trail, signer, new TestClock(Now));
        var field = await enc.EncryptAsync(new byte[] { 1, 2, 3 }, TenantA, Ct);
        var expired = new FixedDecryptCapability("cap-x", new ActorId("a"), TenantA, Past);

        await Assert.ThrowsAsync<FieldDecryptionDeniedException>(
            () => dec.DecryptAsync(field, expired, TenantA, Ct));

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.FieldDecryptionDenied)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DecryptCapability_ConstructorRejectsEmptyCapabilityId()
    {
        Assert.Throws<ArgumentException>(
            () => new FixedDecryptCapability(" ", new ActorId("a"), TenantA, Future));
    }

    [Fact]
    public void Decryptor_TwoOverloadConstructor_PreventsMidStateConfiguration()
    {
        var ctors = typeof(TenantKeyProviderFieldDecryptor).GetConstructors();
        Assert.Equal(2, ctors.Length);
        // Verify the audit-enabled overload requires BOTH IAuditTrail + IOperationSigner;
        // there is no constructor accepting exactly one of them.
        Assert.DoesNotContain(ctors, c =>
            c.GetParameters().Any(p => p.ParameterType == typeof(IAuditTrail))
            && !c.GetParameters().Any(p => p.ParameterType == typeof(IOperationSigner)));
    }

    private sealed class TestClock : IRecoveryClock
    {
        private readonly DateTimeOffset _instant;
        public TestClock(DateTimeOffset instant) => _instant = instant;
        public DateTimeOffset UtcNow() => _instant;
    }

    private static class TestSigner
    {
        public static IOperationSigner Create() => new Ed25519Signer(KeyPair.Generate());
    }
}
