using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SecurityPolicy.Models;
using Sunfish.Foundation.SecurityPolicy.Retention;
using Sunfish.Kernel.Audit.Retention;
using Xunit;
using KernelPolicy = Sunfish.Kernel.Audit.Retention.AuditRetentionPolicy;
using SecPolicy = Sunfish.Foundation.SecurityPolicy.Models.AuditRetentionPolicy;

namespace Sunfish.Foundation.SecurityPolicy.Tests;

public sealed class RetentionTests
{
    private static readonly TenantId Tenant = new("tenant-retention");
    private static readonly DateTimeOffset RecordCreatedAt = new(2026, 5, 17, 12, 0, 0, TimeSpan.Zero);

    private static TenantSecurityPolicy PolicyWith(SecPolicy retention)
        => TenantSecurityPolicy.DefaultFor(Tenant, RecordCreatedAt) with { AuditRetention = retention };

    private static SecPolicy MakeRetention(
        TimeSpan defaultMin,
        TimeSpan defaultMax,
        RetentionJurisdictionPreset preset,
        IReadOnlyDictionary<AuditEventClass, (TimeSpan Min, TimeSpan Max)>? overrides = null)
        => new(
            DefaultMinimumRetentionWindow: defaultMin,
            DefaultMaximumRetentionWindow: defaultMax,
            PerClassOverrides: overrides ?? new ReadOnlyDictionary<AuditEventClass, (TimeSpan Min, TimeSpan Max)>(
                new Dictionary<AuditEventClass, (TimeSpan Min, TimeSpan Max)>()),
            JurisdictionPreset: preset);

    private static DefaultRetentionPolicyResolver ResolverFor(SecPolicy retention)
        => new DefaultRetentionPolicyResolver((_, _) => ValueTask.FromResult(PolicyWith(retention)));

    // ---- JurisdictionFloorHelper unit checks ----

    [Fact]
    public void JurisdictionFloor_Hipaa_FloorsIdentityAt6YearsLeapAware()
    {
        var floor = JurisdictionFloorHelper.GetFloor(RetentionJurisdictionPreset.HipaaInformedDefault, AuditEventClass.Identity);
        Assert.NotNull(floor);
        Assert.Equal(TimeSpan.FromDays(365 * 6 + 2), floor!.Value);
    }

    [Fact]
    public void JurisdictionFloor_Hipaa_FloorsSecurity_AndConfiguration_AndNotFinancial()
    {
        Assert.NotNull(JurisdictionFloorHelper.GetFloor(RetentionJurisdictionPreset.HipaaInformedDefault, AuditEventClass.Security));
        Assert.NotNull(JurisdictionFloorHelper.GetFloor(RetentionJurisdictionPreset.HipaaInformedDefault, AuditEventClass.Configuration));
        Assert.Null(JurisdictionFloorHelper.GetFloor(RetentionJurisdictionPreset.HipaaInformedDefault, AuditEventClass.Financial));
    }

    [Fact]
    public void JurisdictionFloor_PciDss_FloorsFinancialAndSecurityAt12MonthsLeapSafe()
    {
        var floor = JurisdictionFloorHelper.GetFloor(RetentionJurisdictionPreset.PciDssInformedDefault, AuditEventClass.Financial);
        Assert.Equal(TimeSpan.FromDays(366), floor);
        Assert.Equal(TimeSpan.FromDays(366), JurisdictionFloorHelper.GetFloor(RetentionJurisdictionPreset.PciDssInformedDefault, AuditEventClass.Security));
        Assert.Null(JurisdictionFloorHelper.GetFloor(RetentionJurisdictionPreset.PciDssInformedDefault, AuditEventClass.Identity));
    }

    [Fact]
    public void JurisdictionFloor_Soc2_Gdpr_EuAiAct_Custom_NoFloor()
    {
        foreach (AuditEventClass ec in Enum.GetValues<AuditEventClass>())
        {
            Assert.Null(JurisdictionFloorHelper.GetFloor(RetentionJurisdictionPreset.Soc2InformedDefault, ec));
            Assert.Null(JurisdictionFloorHelper.GetFloor(RetentionJurisdictionPreset.GdprInformedDefault, ec));
            Assert.Null(JurisdictionFloorHelper.GetFloor(RetentionJurisdictionPreset.EuAiActInformedDefault, ec));
            Assert.Null(JurisdictionFloorHelper.GetFloor(RetentionJurisdictionPreset.Custom, ec));
        }
    }

    // ---- DefaultRetentionPolicyResolver ----

    [Fact]
    public async Task Resolver_GetActive_ReturnsTenantsRetention()
    {
        var ret = MakeRetention(TimeSpan.FromDays(30), TimeSpan.FromDays(365), RetentionJurisdictionPreset.Custom);
        var resolver = ResolverFor(ret);
        var active = await resolver.GetActiveAsync(Tenant);
        Assert.Same(ret, active);
    }

    [Fact]
    public async Task Resolver_DefaultCustom_UsesDefaultsForClassWithoutOverride()
    {
        var ret = MakeRetention(TimeSpan.FromDays(30), TimeSpan.FromDays(365), RetentionJurisdictionPreset.Custom);
        var resolver = ResolverFor(ret);
        var verdict = await resolver.ResolveAsync(Tenant, AuditEventClass.System, RecordCreatedAt);
        Assert.Equal(RecordCreatedAt + TimeSpan.FromDays(30), verdict.MinimumHoldUntil);
        Assert.Equal(RecordCreatedAt + TimeSpan.FromDays(365), verdict.MaximumHoldUntil);
        Assert.False(verdict.IsJurisdictionFloor);
    }

    [Fact]
    public async Task Resolver_PerClassOverride_WinsAgainstDefaults_NoFloor()
    {
        var overrides = new Dictionary<AuditEventClass, (TimeSpan Min, TimeSpan Max)>
        {
            [AuditEventClass.Financial] = (TimeSpan.FromDays(2 * 365), TimeSpan.FromDays(10 * 365)),
        };
        var ret = MakeRetention(TimeSpan.FromDays(30), TimeSpan.FromDays(365), RetentionJurisdictionPreset.Custom, overrides);
        var resolver = ResolverFor(ret);
        var verdict = await resolver.ResolveAsync(Tenant, AuditEventClass.Financial, RecordCreatedAt);
        Assert.Equal(RecordCreatedAt + TimeSpan.FromDays(2 * 365), verdict.MinimumHoldUntil);
    }

    [Fact]
    public async Task Resolver_HipaaPreset_FloorsIdentityAt6YearsEvenWith1YearOverride()
    {
        var overrides = new Dictionary<AuditEventClass, (TimeSpan Min, TimeSpan Max)>
        {
            [AuditEventClass.Identity] = (TimeSpan.FromDays(365), TimeSpan.FromDays(365 * 10)),
        };
        var ret = MakeRetention(TimeSpan.FromDays(30), TimeSpan.FromDays(365 * 10), RetentionJurisdictionPreset.HipaaInformedDefault, overrides);
        var resolver = ResolverFor(ret);
        var verdict = await resolver.ResolveAsync(Tenant, AuditEventClass.Identity, RecordCreatedAt);
        Assert.Equal(RecordCreatedAt + JurisdictionFloorHelper.HipaaSixYears, verdict.MinimumHoldUntil);
        Assert.True(verdict.IsJurisdictionFloor);
    }

    [Fact]
    public async Task Resolver_HipaaPreset_OverrideGreaterThanFloor_OverrideWins_NoFloorFlag()
    {
        var overrides = new Dictionary<AuditEventClass, (TimeSpan Min, TimeSpan Max)>
        {
            [AuditEventClass.Identity] = (TimeSpan.FromDays(365 * 10), TimeSpan.FromDays(365 * 20)),
        };
        var ret = MakeRetention(TimeSpan.FromDays(30), TimeSpan.FromDays(365 * 20), RetentionJurisdictionPreset.HipaaInformedDefault, overrides);
        var resolver = ResolverFor(ret);
        var verdict = await resolver.ResolveAsync(Tenant, AuditEventClass.Identity, RecordCreatedAt);
        Assert.Equal(RecordCreatedAt + TimeSpan.FromDays(365 * 10), verdict.MinimumHoldUntil);
        Assert.False(verdict.IsJurisdictionFloor);
    }

    [Fact]
    public async Task Resolver_HipaaPreset_DoesNotFloorFinancial()
    {
        var ret = MakeRetention(TimeSpan.FromDays(30), TimeSpan.FromDays(365), RetentionJurisdictionPreset.HipaaInformedDefault);
        var resolver = ResolverFor(ret);
        var verdict = await resolver.ResolveAsync(Tenant, AuditEventClass.Financial, RecordCreatedAt);
        Assert.False(verdict.IsJurisdictionFloor);
        Assert.Equal(RecordCreatedAt + TimeSpan.FromDays(30), verdict.MinimumHoldUntil);
    }

    [Fact]
    public async Task Resolver_PciDssPreset_FloorsFinancialAt12Months()
    {
        var ret = MakeRetention(TimeSpan.FromDays(30), TimeSpan.FromDays(365 * 10), RetentionJurisdictionPreset.PciDssInformedDefault);
        var resolver = ResolverFor(ret);
        var verdict = await resolver.ResolveAsync(Tenant, AuditEventClass.Financial, RecordCreatedAt);
        Assert.Equal(RecordCreatedAt + JurisdictionFloorHelper.PciDssTwelveMonths, verdict.MinimumHoldUntil);
        Assert.True(verdict.IsJurisdictionFloor);
    }

    [Fact]
    public async Task Resolver_Soc2_GdprPreset_NoAutoFloor()
    {
        var ret1 = MakeRetention(TimeSpan.FromDays(30), TimeSpan.FromDays(365), RetentionJurisdictionPreset.Soc2InformedDefault);
        var v1 = await ResolverFor(ret1).ResolveAsync(Tenant, AuditEventClass.Security, RecordCreatedAt);
        Assert.False(v1.IsJurisdictionFloor);

        var ret2 = MakeRetention(TimeSpan.FromDays(30), TimeSpan.FromDays(365), RetentionJurisdictionPreset.GdprInformedDefault);
        var v2 = await ResolverFor(ret2).ResolveAsync(Tenant, AuditEventClass.Identity, RecordCreatedAt);
        Assert.False(v2.IsJurisdictionFloor);
    }

    [Fact]
    public async Task Resolver_VerdictEventClass_MatchesRequested()
    {
        var ret = MakeRetention(TimeSpan.FromDays(30), TimeSpan.FromDays(365), RetentionJurisdictionPreset.Custom);
        var resolver = ResolverFor(ret);
        var verdict = await resolver.ResolveAsync(Tenant, AuditEventClass.Configuration, RecordCreatedAt);
        Assert.Equal(AuditEventClass.Configuration, verdict.EventClass);
    }

    // ---- DefaultAuditRetentionEnforcer ----

    [Fact]
    public async Task Enforcer_ApplyAsync_WalksAllEventClasses_MarksPolicyMatched()
    {
        var ret = MakeRetention(TimeSpan.FromDays(30), TimeSpan.FromDays(365), RetentionJurisdictionPreset.Custom);
        var resolver = ResolverFor(ret);
        var enforcer = new DefaultAuditRetentionEnforcer(resolver, TimeProvider.System);
        var policy = new KernelPolicy(MinDays: 30, MaxDays: 365, LegalHoldOverride: false, EnforcementMode: AuditRetentionEnforcementMode.DryRun);

        var result = await enforcer.ApplyAsync(Tenant, policy);

        Assert.True(result.PolicyMatched);
        // Phase 1 no purge surface — counts are all zero.
        Assert.Equal(0, result.EntriesEvaluated);
        Assert.Equal(0, result.EntriesPurged);
        Assert.Equal(0, result.EntriesSkippedDueToHold);
    }

    [Fact]
    public async Task Enforcer_ApplyAsync_ResolverThrows_ReturnsPolicyMatchedFalse_NoException()
    {
        var throwingResolver = new ThrowingResolver();
        var enforcer = new DefaultAuditRetentionEnforcer(throwingResolver, TimeProvider.System);
        var policy = new KernelPolicy(30, 365, false, AuditRetentionEnforcementMode.DryRun);

        var result = await enforcer.ApplyAsync(Tenant, policy);

        Assert.False(result.PolicyMatched);
    }

    [Fact]
    public async Task Enforcer_ApplyAsync_CancellationPropagates()
    {
        var ret = MakeRetention(TimeSpan.FromDays(30), TimeSpan.FromDays(365), RetentionJurisdictionPreset.Custom);
        var resolver = ResolverFor(ret);
        var enforcer = new DefaultAuditRetentionEnforcer(resolver, TimeProvider.System);
        var policy = new KernelPolicy(30, 365, false, AuditRetentionEnforcementMode.DryRun);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            enforcer.ApplyAsync(Tenant, policy, cts.Token));
    }

    private sealed class ThrowingResolver : IRetentionPolicyResolver
    {
        public ValueTask<SecPolicy> GetActiveAsync(TenantId tenant, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
        public ValueTask<RetentionVerdict> ResolveAsync(TenantId tenant, AuditEventClass eventClass, DateTimeOffset recordCreatedAt, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
    }
}
