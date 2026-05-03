using System;
using System.Collections.Generic;
using System.Text.Json;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class BridgeSubscriptionEventTests
{
    private static BridgeSubscriptionEvent NewUpgradeEvent() => new()
    {
        TenantId = "tenant-abc",
        EventType = BridgeSubscriptionEventType.SubscriptionTierUpgraded,
        EditionBefore = "anchor-self-host",
        EditionAfter = "bridge-pro",
        EffectiveAt = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
        EventId = Guid.Parse("7f9d2a00-0000-0000-0000-000000000000"),
        DeliveryAttempt = 1,
        Signature = "hmac-sha256:placeholder",
    };

    [Fact]
    public void BridgeSubscriptionEvent_RoundTripsThroughCanonicalJson()
    {
        var ev = NewUpgradeEvent();
        var bytes = CanonicalJson.Serialize(ev);
        var roundtripped = JsonSerializer.Deserialize<BridgeSubscriptionEvent>(System.Text.Encoding.UTF8.GetString(bytes));

        Assert.NotNull(roundtripped);
        var rebytes = CanonicalJson.Serialize(roundtripped);
        Assert.Equal(bytes, rebytes);
    }

    [Fact]
    public void BridgeSubscriptionEvent_UsesCamelCasePropertyNames()
    {
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(NewUpgradeEvent()));
        Assert.Contains("\"tenantId\"", json);
        Assert.Contains("\"eventType\"", json);
        Assert.Contains("\"editionBefore\"", json);
        Assert.Contains("\"editionAfter\"", json);
        Assert.Contains("\"effectiveAt\"", json);
        Assert.Contains("\"eventId\"", json);
        Assert.Contains("\"deliveryAttempt\"", json);
        Assert.Contains("\"signature\"", json);
        Assert.Contains("\"algorithm\"", json);
        // PascalCase regression guard.
        Assert.DoesNotContain("\"TenantId\"", json);
    }

    [Fact]
    public void BridgeSubscriptionEvent_SerializesEnumValues_AsPascalCaseLiterals()
    {
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(NewUpgradeEvent()));
        Assert.Contains("\"SubscriptionTierUpgraded\"", json);
        Assert.Contains("\"HmacSha256\"", json);
        // Ordinal-regression guard.
        Assert.DoesNotContain("\"eventType\":3", json);
    }

    [Theory]
    [InlineData(BridgeSubscriptionEventType.SubscriptionStarted)]
    [InlineData(BridgeSubscriptionEventType.SubscriptionRenewed)]
    [InlineData(BridgeSubscriptionEventType.SubscriptionCancelled)]
    [InlineData(BridgeSubscriptionEventType.SubscriptionTierUpgraded)]
    [InlineData(BridgeSubscriptionEventType.SubscriptionTierDowngraded)]
    [InlineData(BridgeSubscriptionEventType.SubscriptionDunning)]
    [InlineData(BridgeSubscriptionEventType.SubscriptionExpired)]
    public void BridgeSubscriptionEventType_AllSevenValuesRoundTrip(BridgeSubscriptionEventType kind)
    {
        var ev = NewUpgradeEvent() with { EventType = kind };
        var bytes = CanonicalJson.Serialize(ev);
        var roundtripped = JsonSerializer.Deserialize<BridgeSubscriptionEvent>(System.Text.Encoding.UTF8.GetString(bytes));
        Assert.NotNull(roundtripped);
        Assert.Equal(kind, roundtripped!.EventType);
        Assert.Contains($"\"{kind}\"", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void BridgeSubscriptionEvent_SubscriptionStarted_HasNullEditionBefore()
    {
        var started = NewUpgradeEvent() with
        {
            EventType = BridgeSubscriptionEventType.SubscriptionStarted,
            EditionBefore = null,
            EditionAfter = "bridge-pro",
        };
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(started));
        var roundtripped = JsonSerializer.Deserialize<BridgeSubscriptionEvent>(json);
        Assert.NotNull(roundtripped);
        Assert.Null(roundtripped!.EditionBefore);
        Assert.Equal("bridge-pro", roundtripped.EditionAfter);
    }

    [Fact]
    public void BridgeSubscriptionEvent_SubscriptionExpired_HasNullEditionAfter()
    {
        var expired = NewUpgradeEvent() with
        {
            EventType = BridgeSubscriptionEventType.SubscriptionExpired,
            EditionBefore = "bridge-pro",
            EditionAfter = null,
        };
        var roundtripped = JsonSerializer.Deserialize<BridgeSubscriptionEvent>(System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(expired)));
        Assert.NotNull(roundtripped);
        Assert.Null(roundtripped!.EditionAfter);
        Assert.Equal("bridge-pro", roundtripped.EditionBefore);
    }

    [Fact]
    public void Algorithm_DefaultsToHmacSha256()
    {
        // Only TenantId / EventType / EffectiveAt / EventId / DeliveryAttempt / Signature
        // are required; Algorithm should default to HmacSha256 per A1.12.2.
        var ev = new BridgeSubscriptionEvent
        {
            TenantId = "t",
            EventType = BridgeSubscriptionEventType.SubscriptionRenewed,
            EditionBefore = "x",
            EditionAfter = "x",
            EffectiveAt = DateTimeOffset.UtcNow,
            EventId = Guid.NewGuid(),
            DeliveryAttempt = 1,
            Signature = "hmac-sha256:abc",
        };
        Assert.Equal(SignatureAlgorithm.HmacSha256, ev.Algorithm);
    }

    [Fact]
    public void Algorithm_Ed25519_RoundTripsButReservedForPhase2Plus()
    {
        // Phase 1 substrate accepts the surface but does NOT implement Ed25519
        // signing. The enum value exists for forward-compat per A1.12.2.
        var ev = NewUpgradeEvent() with { Algorithm = SignatureAlgorithm.Ed25519 };
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(ev));
        Assert.Contains("\"Ed25519\"", json);
        var roundtripped = JsonSerializer.Deserialize<BridgeSubscriptionEvent>(json);
        Assert.Equal(SignatureAlgorithm.Ed25519, roundtripped!.Algorithm);
    }
}

public sealed class WebhookRegistrationTests
{
    [Fact]
    public void WebhookRegistration_RoundTripsThroughCanonicalJson()
    {
        var reg = new WebhookRegistration
        {
            CallbackUrl = new Uri("https://anchor.example.com/sunfish/bridge-events"),
            DeliveryMode = DeliveryMode.Webhook,
            SubscribedEvents = new[]
            {
                BridgeSubscriptionEventType.SubscriptionStarted,
                BridgeSubscriptionEventType.SubscriptionTierUpgraded,
                BridgeSubscriptionEventType.SubscriptionTierDowngraded,
            },
            SharedSecret = "base64-token-...",
        };
        var bytes = CanonicalJson.Serialize(reg);
        var roundtripped = JsonSerializer.Deserialize<WebhookRegistration>(System.Text.Encoding.UTF8.GetString(bytes));
        Assert.NotNull(roundtripped);
        Assert.Equal(bytes, CanonicalJson.Serialize(roundtripped));
    }

    [Fact]
    public void WebhookRegistration_DeliveryModeAsLiteral_AndSubscribedEventsAsArray()
    {
        var reg = new WebhookRegistration
        {
            CallbackUrl = new Uri("https://anchor.example.com/x"),
            DeliveryMode = DeliveryMode.Sse,
            SubscribedEvents = null,
            SharedSecret = "k",
        };
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(reg));
        Assert.Contains("\"Sse\"", json);
    }
}

public sealed class SubscribedEventFilterTests
{
    [Fact]
    public void Includes_NullList_AcceptsEverything()
    {
        var filter = new SubscribedEventFilter { Events = null };
        foreach (var k in Enum.GetValues<BridgeSubscriptionEventType>())
        {
            Assert.True(filter.Includes(k));
        }
    }

    [Fact]
    public void Includes_EmptyList_AcceptsEverything()
    {
        var filter = new SubscribedEventFilter { Events = Array.Empty<BridgeSubscriptionEventType>() };
        foreach (var k in Enum.GetValues<BridgeSubscriptionEventType>())
        {
            Assert.True(filter.Includes(k));
        }
    }

    [Fact]
    public void Includes_SubsetList_AcceptsOnlyListed()
    {
        var filter = new SubscribedEventFilter
        {
            Events = new[]
            {
                BridgeSubscriptionEventType.SubscriptionTierUpgraded,
                BridgeSubscriptionEventType.SubscriptionTierDowngraded,
            },
        };
        Assert.True(filter.Includes(BridgeSubscriptionEventType.SubscriptionTierUpgraded));
        Assert.True(filter.Includes(BridgeSubscriptionEventType.SubscriptionTierDowngraded));
        Assert.False(filter.Includes(BridgeSubscriptionEventType.SubscriptionStarted));
        Assert.False(filter.Includes(BridgeSubscriptionEventType.SubscriptionDunning));
    }
}
