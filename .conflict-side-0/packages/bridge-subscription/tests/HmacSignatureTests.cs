using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class HmacSignatureTests
{
    private const string Secret = "shared-secret-base64";
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    private static BridgeSubscriptionEvent NewUnsigned() => new()
    {
        TenantId = "tenant-abc",
        EventType = BridgeSubscriptionEventType.SubscriptionTierUpgraded,
        EditionBefore = "anchor-self-host",
        EditionAfter = "bridge-pro",
        EffectiveAt = Now,
        EventId = Guid.Parse("7f9d2a00-0000-0000-0000-000000000000"),
        DeliveryAttempt = 1,
        Signature = string.Empty,
    };

    [Fact]
    public async Task SignAsync_ProducesNonEmptySignatureWithExpectedPrefix()
    {
        var signer = new HmacSha256EventSigner();
        var signed = await signer.SignAsync(NewUnsigned(), Secret);
        Assert.StartsWith(HmacSha256EventSigner.SignaturePrefix, signed.Signature);
        Assert.True(signed.Signature.Length > HmacSha256EventSigner.SignaturePrefix.Length);
        Assert.Equal(SignatureAlgorithm.HmacSha256, signed.Algorithm);
    }

    [Fact]
    public async Task VerifyAsync_RoundTrip_Succeeds()
    {
        var signer = new HmacSha256EventSigner();
        var signed = await signer.SignAsync(NewUnsigned(), Secret);
        Assert.True(await signer.VerifyAsync(signed, Secret));
    }

    [Fact]
    public async Task VerifyAsync_TamperedPayload_Fails()
    {
        var signer = new HmacSha256EventSigner();
        var signed = await signer.SignAsync(NewUnsigned(), Secret);
        var tampered = signed with { EditionAfter = "tampered-pro" };
        Assert.False(await signer.VerifyAsync(tampered, Secret));
    }

    [Fact]
    public async Task VerifyAsync_TamperedSignature_Fails()
    {
        var signer = new HmacSha256EventSigner();
        var signed = await signer.SignAsync(NewUnsigned(), Secret);
        var tampered = signed with { Signature = HmacSha256EventSigner.SignaturePrefix + "xxxxxxxxxxxxxxxxxxxxxx" };
        Assert.False(await signer.VerifyAsync(tampered, Secret));
    }

    [Fact]
    public async Task VerifyAsync_WrongSecret_Fails()
    {
        var signer = new HmacSha256EventSigner();
        var signed = await signer.SignAsync(NewUnsigned(), Secret);
        Assert.False(await signer.VerifyAsync(signed, "wrong-secret"));
    }

    [Fact]
    public async Task VerifyAsync_Algorithm_Ed25519_Fails()
    {
        var signer = new HmacSha256EventSigner();
        var signed = await signer.SignAsync(NewUnsigned(), Secret);
        var withWrongAlgo = signed with { Algorithm = SignatureAlgorithm.Ed25519 };
        Assert.False(await signer.VerifyAsync(withWrongAlgo, Secret));
    }

    [Fact]
    public async Task VerifyAsync_MissingPrefix_Fails()
    {
        var signer = new HmacSha256EventSigner();
        var signed = await signer.SignAsync(NewUnsigned(), Secret);
        var noPrefix = signed.Signature[HmacSha256EventSigner.SignaturePrefix.Length..];
        Assert.False(await signer.VerifyAsync(signed with { Signature = noPrefix }, Secret));
    }

    [Fact]
    public async Task SignAsync_ZeroesSignatureField_BeforeHashing()
    {
        // Bridge-side signer should produce the same output regardless
        // of whatever was in Signature on the unsigned input — proves
        // the signature field is stripped from the signing surface.
        var signer = new HmacSha256EventSigner();
        var withGarbage = NewUnsigned() with { Signature = "garbage-input-signature" };
        var withEmpty = NewUnsigned() with { Signature = string.Empty };

        var s1 = await signer.SignAsync(withGarbage, Secret);
        var s2 = await signer.SignAsync(withEmpty, Secret);
        Assert.Equal(s1.Signature, s2.Signature);
    }

    [Fact]
    public async Task SignAsync_NullArguments_Throw()
    {
        var signer = new HmacSha256EventSigner();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            signer.SignAsync(null!, Secret).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            signer.SignAsync(NewUnsigned(), string.Empty).AsTask());
    }

    [Fact]
    public void Algorithm_IsHmacSha256()
    {
        Assert.Equal(SignatureAlgorithm.HmacSha256, new HmacSha256EventSigner().Algorithm);
    }
}
