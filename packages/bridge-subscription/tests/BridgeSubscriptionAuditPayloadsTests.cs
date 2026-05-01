using Sunfish.Bridge.Subscription.Audit;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class BridgeSubscriptionAuditPayloadsTests
{
    private const string TenantA = "tenant-abc";
    private const string EventId = "7f9d2a00-0000-0000-0000-000000000000";

    [Fact]
    public void Event_ShapeIsAlphabetized()
    {
        var p = BridgeSubscriptionAuditPayloads.Event(TenantA, BridgeSubscriptionEventType.SubscriptionTierUpgraded, EventId, 3);
        Assert.Equal(3, p.Body["delivery_attempt"]);
        Assert.Equal(EventId, p.Body["event_id"]);
        Assert.Equal("SubscriptionTierUpgraded", p.Body["event_type"]);
        Assert.Equal(TenantA, p.Body["tenant_id"]);
        Assert.Equal(4, p.Body.Count);
    }

    [Fact]
    public void SignatureFailed_IncludesSourceIp()
    {
        var p = BridgeSubscriptionAuditPayloads.SignatureFailed(TenantA, EventId, "203.0.113.42");
        Assert.Equal(EventId, p.Body["event_id"]);
        Assert.Equal("203.0.113.42", p.Body["source_ip"]);
        Assert.Equal(TenantA, p.Body["tenant_id"]);
        Assert.Equal(3, p.Body.Count);
    }

    [Fact]
    public void Stale_IncludesClockSkewSeconds()
    {
        var p = BridgeSubscriptionAuditPayloads.Stale(TenantA, BridgeSubscriptionEventType.SubscriptionDunning, EventId, 360.5);
        Assert.Equal(360.5, p.Body["clock_skew_seconds"]);
        Assert.Equal(EventId, p.Body["event_id"]);
        Assert.Equal("SubscriptionDunning", p.Body["event_type"]);
        Assert.Equal(TenantA, p.Body["tenant_id"]);
        Assert.Equal(4, p.Body.Count);
    }

    [Fact]
    public void WebhookRegistered_IncludesCallbackAndMode()
    {
        var p = BridgeSubscriptionAuditPayloads.WebhookRegistered(TenantA, "https://anchor.example/x", DeliveryMode.Sse);
        Assert.Equal("https://anchor.example/x", p.Body["callback_url"]);
        Assert.Equal("Sse", p.Body["delivery_mode"]);
        Assert.Equal(TenantA, p.Body["tenant_id"]);
        Assert.Equal(3, p.Body.Count);
    }

    [Fact]
    public void WebhookRotationStaged_IncludesFingerprintsAndGrace()
    {
        var p = BridgeSubscriptionAuditPayloads.WebhookRotationStaged(TenantA, "fpA", "fpB", 24);
        Assert.Equal(24, p.Body["grace_window_hours"]);
        Assert.Equal("fpB", p.Body["new_secret_fingerprint"]);
        Assert.Equal("fpA", p.Body["previous_secret_fingerprint"]);
        Assert.Equal(TenantA, p.Body["tenant_id"]);
        Assert.Equal(4, p.Body.Count);
    }

    [Fact]
    public void SelfSignedCertsConfigured_IncludesAllowedFlag()
    {
        var p = BridgeSubscriptionAuditPayloads.SelfSignedCertsConfigured(TenantA, allowed: true);
        Assert.Equal(true, p.Body["allowed"]);
        Assert.Equal(TenantA, p.Body["tenant_id"]);
        Assert.Equal(2, p.Body.Count);
    }
}
