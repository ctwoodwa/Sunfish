using System;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class TrustChainResolverTests
{
    [Fact]
    public void ResolveFor_DefaultsToPubliclyRootedCa()
    {
        var resolver = new DefaultTrustChainResolver();
        var cfg = resolver.ResolveFor("tenant-a");
        Assert.Equal(WebhookTrustMode.PubliclyRootedCa, cfg.Mode);
        Assert.Null(cfg.PinnedCertPem);
    }

    [Fact]
    public void ConfigurePinnedCertificate_SetsPinAndPem()
    {
        var resolver = new DefaultTrustChainResolver();
        resolver.ConfigurePinnedCertificate("tenant-a", "-----BEGIN CERTIFICATE-----\nMIIB...\n-----END CERTIFICATE-----");
        var cfg = resolver.ResolveFor("tenant-a");
        Assert.Equal(WebhookTrustMode.PinnedCertificate, cfg.Mode);
        Assert.Contains("BEGIN CERTIFICATE", cfg.PinnedCertPem!);
    }

    [Fact]
    public void AllowSelfSigned_SwitchesMode_ButDoesNotEmitAudit()
    {
        // The audit-emission boundary is the host's admin handler per
        // A1.12.3; the resolver only records configuration.
        var resolver = new DefaultTrustChainResolver();
        resolver.AllowSelfSigned("tenant-a");
        var cfg = resolver.ResolveFor("tenant-a");
        Assert.Equal(WebhookTrustMode.AllowSelfSigned, cfg.Mode);
        Assert.Null(cfg.PinnedCertPem);
    }

    [Fact]
    public void ResetToDefault_RemovesOverride()
    {
        var resolver = new DefaultTrustChainResolver();
        resolver.AllowSelfSigned("tenant-a");
        Assert.Equal(WebhookTrustMode.AllowSelfSigned, resolver.ResolveFor("tenant-a").Mode);

        resolver.ResetToDefault("tenant-a");
        Assert.Equal(WebhookTrustMode.PubliclyRootedCa, resolver.ResolveFor("tenant-a").Mode);
    }

    [Fact]
    public void ResolveFor_DifferentTenants_AreIndependent()
    {
        var resolver = new DefaultTrustChainResolver();
        resolver.AllowSelfSigned("tenant-a");
        resolver.ConfigurePinnedCertificate("tenant-b", "pem");

        Assert.Equal(WebhookTrustMode.AllowSelfSigned, resolver.ResolveFor("tenant-a").Mode);
        Assert.Equal(WebhookTrustMode.PinnedCertificate, resolver.ResolveFor("tenant-b").Mode);
        Assert.Equal(WebhookTrustMode.PubliclyRootedCa, resolver.ResolveFor("tenant-c").Mode); // never touched
    }

    [Fact]
    public void ResolveFor_NullOrEmptyTenantId_Throws()
    {
        var resolver = new DefaultTrustChainResolver();
        Assert.Throws<ArgumentException>(() => resolver.ResolveFor(string.Empty));
    }

    [Fact]
    public void ConfigurePinnedCertificate_NullPem_Throws()
    {
        var resolver = new DefaultTrustChainResolver();
        Assert.Throws<ArgumentException>(() =>
            resolver.ConfigurePinnedCertificate("t", string.Empty));
    }
}
