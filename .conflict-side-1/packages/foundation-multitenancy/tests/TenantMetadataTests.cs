using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.MultiTenancy.Tests;

public class TenantMetadataTests
{
    [Fact]
    public void TenantMetadata_defaults_to_active_status_and_empty_properties()
    {
        var tenant = new TenantMetadata
        {
            Id = new TenantId("acme"),
            Name = "acme",
        };

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Empty(tenant.Properties);
        Assert.Null(tenant.DisplayName);
        Assert.Null(tenant.Locale);
        Assert.Null(tenant.CreatedAt);
    }

    [Fact]
    public void ITenantContext_IsResolved_is_false_when_tenant_is_null()
    {
        ITenantContext ctx = new EmptyContext();

        Assert.False(ctx.IsResolved);
    }

    [Fact]
    public void ITenantContext_IsResolved_is_true_when_tenant_present()
    {
        ITenantContext ctx = new FixedContext(new TenantMetadata
        {
            Id = new TenantId("acme"),
            Name = "acme",
        });

        Assert.True(ctx.IsResolved);
    }

    private sealed class EmptyContext : ITenantContext
    {
        public TenantMetadata? Tenant => null;
    }

    private sealed class FixedContext(TenantMetadata tenant) : ITenantContext
    {
        public TenantMetadata? Tenant { get; } = tenant;
    }
}
