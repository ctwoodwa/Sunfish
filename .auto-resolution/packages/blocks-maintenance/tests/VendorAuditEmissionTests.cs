using Sunfish.Blocks.Maintenance.Audit;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.Maintenance.Tests;

/// <summary>
/// W#18 Phase 7 — schema snapshot tests for the 7 vendor-onboarding
/// AuditEventType + VendorAuditPayloadFactory body shapes per ADR 0058
/// + ADR 0049. Verifies the TIN-PII-discipline invariant (no TIN bytes
/// surface in any audit body).
/// </summary>
public sealed class VendorAuditEmissionTests
{
    private static readonly VendorId TestVendor = VendorId.NewId();
    private static readonly ActorId Operator = new("operator");

    // ─────────── AuditEventType constants ───────────

    [Fact]
    public void AuditEventType_VendorOnboarding_AllSevenDeclared()
    {
        // Each event must be a distinct value with a stable string.
        var values = new[]
        {
            AuditEventType.VendorCreated.Value,
            AuditEventType.VendorMagicLinkIssued.Value,
            AuditEventType.VendorMagicLinkConsumed.Value,
            AuditEventType.VendorOnboardingStateChanged.Value,
            AuditEventType.W9DocumentReceived.Value,
            AuditEventType.W9DocumentVerified.Value,
            AuditEventType.VendorActivated.Value,
        };
        Assert.Equal(7, values.Length);
        Assert.Equal(values.Length, values.Distinct().Count());
    }

    // ─────────── Body factory snapshots ───────────

    [Fact]
    public void VendorCreated_BodyHasExpectedKeys()
    {
        var vendor = MakeVendor();
        var body = VendorAuditPayloadFactory.VendorCreated(vendor).Body;

        Assert.Equal(vendor.Id.Value, body["vendor_id"]);
        Assert.Equal(vendor.DisplayName, body["display_name"]);
        Assert.Equal("Pending", body["onboarding_state"]);
        Assert.Equal(0, body["specialty_count"]);
        Assert.Equal("Active", body["status"]);
    }

    [Fact]
    public void VendorMagicLinkIssued_BodyHasExpectedKeys()
    {
        var linkId = new VendorMagicLinkId(Guid.NewGuid());
        var expires = new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero);
        var body = VendorAuditPayloadFactory
            .VendorMagicLinkIssued(TestVendor, linkId, Operator, expires)
            .Body;

        Assert.Equal(TestVendor.Value, body["vendor_id"]);
        Assert.Equal(linkId.Value, body["magic_link_id"]);
        Assert.Equal(Operator.Value, body["issued_by"]);
        Assert.Equal(expires.ToString("O"), body["expires_at"]);
    }

    [Fact]
    public void VendorMagicLinkConsumed_BodyCapturesFingerprint()
    {
        var linkId = new VendorMagicLinkId(Guid.NewGuid());
        var body = VendorAuditPayloadFactory
            .VendorMagicLinkConsumed(TestVendor, linkId, "198.51.100.42", "Mozilla/5.0")
            .Body;

        Assert.Equal("198.51.100.42", body["consumer_ip"]);
        Assert.Equal("Mozilla/5.0", body["user_agent"]);
    }

    [Fact]
    public void VendorOnboardingStateChanged_BodyCarriesTransition()
    {
        var body = VendorAuditPayloadFactory
            .VendorOnboardingStateChanged(TestVendor, VendorOnboardingState.Pending, VendorOnboardingState.W9Requested, Operator)
            .Body;

        Assert.Equal("Pending", body["previous_state"]);
        Assert.Equal("W9Requested", body["new_state"]);
        Assert.Equal(Operator.Value, body["actor"]);
    }

    [Fact]
    public void W9DocumentReceived_BodyCarriesIdAndTimestamp_NotTin()
    {
        var docId = new W9DocumentId(Guid.NewGuid());
        var received = DateTimeOffset.UtcNow;
        var body = VendorAuditPayloadFactory
            .W9DocumentReceived(TestVendor, docId, received)
            .Body;

        Assert.Equal(docId.Value, body["w9_document_id"]);
        Assert.Equal(received.ToString("O"), body["received_at"]);
        // TIN-PII discipline: no key reads as a TIN field name.
        Assert.False(body.ContainsKey("tin"));
        Assert.False(body.ContainsKey("ssn"));
        Assert.False(body.ContainsKey("ein"));
    }

    [Fact]
    public void W9DocumentVerified_BodyCarriesVerifier()
    {
        var docId = new W9DocumentId(Guid.NewGuid());
        var body = VendorAuditPayloadFactory
            .W9DocumentVerified(TestVendor, docId, Operator)
            .Body;

        Assert.Equal(Operator.Value, body["verified_by"]);
    }

    [Fact]
    public void VendorActivated_BodyMinimalShape()
    {
        var body = VendorAuditPayloadFactory.VendorActivated(TestVendor, Operator).Body;
        Assert.Equal(TestVendor.Value, body["vendor_id"]);
        Assert.Equal(Operator.Value, body["actor"]);
    }

    // ─────────── TIN PII discipline invariant ───────────

    [Fact]
    public void NoFactoryBodyKey_LooksLikeATinField()
    {
        // Run every factory; collect every key from every body; assert
        // none read as TIN field names. The seeded test vendor /
        // sentinel values do not contain "ssn"/"tin"/"ein" so any such
        // key would be a definite leak.
        var vendor = MakeVendor();
        var docId = new W9DocumentId(Guid.NewGuid());
        var linkId = new VendorMagicLinkId(Guid.NewGuid());

        var bodies = new[]
        {
            VendorAuditPayloadFactory.VendorCreated(vendor).Body,
            VendorAuditPayloadFactory.VendorMagicLinkIssued(TestVendor, linkId, Operator, DateTimeOffset.UtcNow).Body,
            VendorAuditPayloadFactory.VendorMagicLinkConsumed(TestVendor, linkId, "ip", "ua").Body,
            VendorAuditPayloadFactory.VendorOnboardingStateChanged(TestVendor, VendorOnboardingState.Pending, VendorOnboardingState.Active, Operator).Body,
            VendorAuditPayloadFactory.W9DocumentReceived(TestVendor, docId, DateTimeOffset.UtcNow).Body,
            VendorAuditPayloadFactory.W9DocumentVerified(TestVendor, docId, Operator).Body,
            VendorAuditPayloadFactory.VendorActivated(TestVendor, Operator).Body,
        };
        var forbiddenKeys = new[] { "tin", "ssn", "ein", "tax_id" };
        foreach (var body in bodies)
        {
            foreach (var key in body.Keys)
            {
                Assert.DoesNotContain(key, forbiddenKeys);
            }
        }
    }

    private static Vendor MakeVendor() => new()
    {
        Id = TestVendor,
        DisplayName = "Acme Plumbing",
        Status = VendorStatus.Active,
        OnboardingState = VendorOnboardingState.Pending,
    };
}
