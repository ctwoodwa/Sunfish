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

    /// <summary>
    /// W#1 WS-A security follow-up MF-5 — the positional-record ctor
    /// pre-MF-5 silently accepted an empty <see cref="ImmutableArray{T}"/>
    /// (including <c>default</c>) and produced an unsafe selection that
    /// matched zero tenants. Post-MF-5, <see cref="TenantSelection.ForMultiple"/>
    /// is a manual record body whose ctor rejects both empty and default
    /// arrays via <see cref="ImmutableArray{T}.IsDefaultOrEmpty"/>.
    /// </summary>
    [Fact]
    public void ForMultiple_EmptyImmutableArray_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TenantSelection.ForMultiple(System.Collections.Immutable.ImmutableArray<TenantId>.Empty));
    }

    /// <summary>
    /// W#1 WS-A security follow-up MF-5 — default
    /// <see cref="ImmutableArray{T}"/> (uninitialized) must also be
    /// rejected so the invariant "TenantIds.Length ≥ 1" holds for every
    /// constructed instance.
    /// </summary>
    [Fact]
    public void ForMultiple_DefaultImmutableArray_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TenantSelection.ForMultiple(default(System.Collections.Immutable.ImmutableArray<TenantId>)));
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

    /// <summary>
    /// W#1 WS-A security follow-up MF-1 — <see cref="TenantSelection.AllAccessible"/>
    /// matches every real tenant but EXCLUDES Sunfish system sentinels
    /// (<c>__system__</c> and any future <c>__</c>-prefixed sentinel).
    /// Otherwise platform-admin queries would surface system records as
    /// if they were tenant data.
    /// </summary>
    [Fact]
    public void Matches_AllAccessible_MatchesRealTenantsAndExcludesSystemSentinels()
    {
        var sel = new TenantSelection.AllAccessible();
        Assert.True(sel.Matches(TenantA));
        Assert.True(sel.Matches(TenantB));
        Assert.False(sel.Matches(TenantId.System));
        // Default (uninitialized) TenantId — Value is null — also a sentinel
        // by the IsSystemSentinel fail-closed semantics.
        Assert.False(sel.Matches(default(TenantId)));
    }

    /// <summary>
    /// W#1 WS-A security follow-up MF-6 — <see cref="TenantSelection.All"/>
    /// is a singleton convenience over a fresh <see cref="TenantSelection.AllAccessible"/>;
    /// repeated reads return the same instance to avoid hot-path allocation.
    /// </summary>
    [Fact]
    public void All_SingletonReference_IsStable()
    {
        var first = TenantSelection.All;
        var second = TenantSelection.All;
        Assert.Same(first, second);
        Assert.IsType<TenantSelection.AllAccessible>(first);
    }

    [Fact]
    public void NullAuditContextProvider_GetTenant_ReturnsSystem()
    {
        var prov = Sunfish.Foundation.Assets.Audit.NullAuditContextProvider.Instance;
        Assert.Equal(TenantId.System, prov.GetTenant());
    }
}
