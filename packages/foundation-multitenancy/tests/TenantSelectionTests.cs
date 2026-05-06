using System;
using System.Collections.Generic;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Foundation.MultiTenancy.Tests;

/// <summary>
/// W#1 WS-A — coverage for <see cref="TenantSelection"/> per ADR 0084.
/// </summary>
public class TenantSelectionTests
{
    private static readonly TenantId TenantA = new("alpha");
    private static readonly TenantId TenantB = new("bravo");

    [Fact]
    public void Reserved_PrefixGuard_RejectsExternalDoubleUnderscore()
    {
        Assert.Throws<ArgumentException>(() => new TenantId("__custom__"));
    }

    [Fact]
    public void Regular_TenantId_Constructs()
    {
        var t = new TenantId("regular-tenant");
        Assert.Equal("regular-tenant", t.Value);
    }

    [Fact]
    public void System_Sentinel_HasReservedValue()
    {
        Assert.Equal("__system__", TenantId.System.Value);
    }

    [Fact]
    public void Of_Single_ReturnsForSingle()
    {
        var sel = TenantSelection.Of(TenantA);
        var single = Assert.IsType<TenantSelection.ForSingle>(sel);
        Assert.Equal(TenantA, single.TenantId);
    }

    [Fact]
    public void Of_TwoTenants_ReturnsForMultiple()
    {
        var sel = TenantSelection.Of(TenantA, TenantB);
        var multi = Assert.IsType<TenantSelection.ForMultiple>(sel);
        Assert.Equal(2, multi.TenantIds.Length);
    }

    [Fact]
    public void Of_DuplicateTenants_DoesNotDeduplicate()
    {
        var sel = TenantSelection.Of(TenantA, TenantA);
        var multi = Assert.IsType<TenantSelection.ForMultiple>(sel);
        Assert.Equal(2, multi.TenantIds.Length);
    }

    [Fact]
    public void ForMultiple_Equality_IsSequenceEqual_AndHashCodeMatches()
    {
        var a = new TenantSelection.ForMultiple(new[] { TenantA, TenantB });
        var b = new TenantSelection.ForMultiple(new[] { TenantA, TenantB });
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        var reversed = new TenantSelection.ForMultiple(new[] { TenantB, TenantA });
        Assert.NotEqual(a, reversed);
    }

    [Fact]
    public void ForMultiple_EmptyEnumerable_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TenantSelection.ForMultiple(new List<TenantId>()));
    }

    [Fact]
    public void Of_EmptyParams_Throws()
    {
        Assert.Throws<ArgumentException>(() => TenantSelection.Of());
    }

    [Fact]
    public void ImplicitCast_FromTenantId_ProducesForSingle()
    {
        TenantSelection sel = TenantA;
        var single = Assert.IsType<TenantSelection.ForSingle>(sel);
        Assert.Equal(TenantA, single.TenantId);
    }

    [Fact]
    public void Matches_ForSingle_OnlyMatchesItsTenant()
    {
        var sel = TenantSelection.Of(TenantA);
        Assert.True(sel.Matches(TenantA));
        Assert.False(sel.Matches(TenantB));
    }

    [Fact]
    public void Matches_ForMultiple_MatchesEachMember()
    {
        var sel = TenantSelection.Of(TenantA, TenantB);
        Assert.True(sel.Matches(TenantA));
        Assert.True(sel.Matches(TenantB));
        Assert.False(sel.Matches(new TenantId("charlie")));
    }

    [Fact]
    public void Matches_AllAccessible_AlwaysTrue()
    {
        var sel = new TenantSelection.AllAccessible();
        Assert.True(sel.Matches(TenantA));
        Assert.True(sel.Matches(TenantB));
        Assert.True(sel.Matches(TenantId.System));
    }

    [Fact]
    public void NullAuditContextProvider_GetTenant_ReturnsSystem()
    {
        var prov = Sunfish.Foundation.Assets.Audit.NullAuditContextProvider.Instance;
        Assert.Equal(TenantId.System, prov.GetTenant());
    }
}
