using System;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

public sealed class ReportExecutionContextTests
{
    [Fact]
    public void Context_Equality_IsValueBased()
    {
        var tenant = new TenantId("t1");
        var principal = PrincipalId.FromBytes(new byte[32]);
        var asOf = new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero);
        var a = new ReportExecutionContext(tenant, "marker:1", asOf, principal);
        var b = new ReportExecutionContext(tenant, "marker:1", asOf, principal);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Context_DifferentMarker_ProducesInequality()
    {
        var tenant = new TenantId("t1");
        var principal = PrincipalId.FromBytes(new byte[32]);
        var asOf = new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero);
        var a = new ReportExecutionContext(tenant, "marker:1", asOf, principal);
        var b = new ReportExecutionContext(tenant, "marker:2", asOf, principal);
        Assert.NotEqual(a, b);
    }
}
