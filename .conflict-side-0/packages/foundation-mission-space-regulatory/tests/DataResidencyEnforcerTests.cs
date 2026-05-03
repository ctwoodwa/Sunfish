using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Regulatory.Tests;

public sealed class DataResidencyEnforcerTests
{
    [Fact]
    public async Task EnforceAsync_NoConstraintForRecordClass_Permitted()
    {
        var enforcer = new DefaultDataResidencyEnforcer(new EmptyDataResidencyConstraintSource());
        var verdict = await enforcer.EnforceAsync("lease", "US-UT");
        Assert.True(verdict.IsPermitted);
        Assert.Null(verdict.ViolatedConstraintId);
    }

    [Fact]
    public async Task EnforceAsync_ProhibitedJurisdiction_Blocked()
    {
        var source = new DictSource(new()
        {
            ["lease"] = new DataResidencyConstraint
            {
                RecordClass = "lease",
                ProhibitedJurisdictions = new[] { "RU" },
            },
        });
        var enforcer = new DefaultDataResidencyEnforcer(source);
        var verdict = await enforcer.EnforceAsync("lease", "RU");
        Assert.False(verdict.IsPermitted);
        Assert.Equal("lease", verdict.ViolatedConstraintId);
        Assert.Contains("prohibited", verdict.Detail!);
    }

    [Fact]
    public async Task EnforceAsync_AllowedListNonEmpty_AndJurisdictionInIt_Permitted()
    {
        var source = new DictSource(new()
        {
            ["lease"] = new DataResidencyConstraint
            {
                RecordClass = "lease",
                AllowedJurisdictions = new[] { "US-UT", "US-WA" },
            },
        });
        var enforcer = new DefaultDataResidencyEnforcer(source);
        var verdict = await enforcer.EnforceAsync("lease", "US-UT");
        Assert.True(verdict.IsPermitted);
    }

    [Fact]
    public async Task EnforceAsync_AllowedListNonEmpty_AndJurisdictionNotInIt_Blocked()
    {
        var source = new DictSource(new()
        {
            ["lease"] = new DataResidencyConstraint
            {
                RecordClass = "lease",
                AllowedJurisdictions = new[] { "US-UT", "US-WA" },
            },
        });
        var enforcer = new DefaultDataResidencyEnforcer(source);
        var verdict = await enforcer.EnforceAsync("lease", "EU-DE");
        Assert.False(verdict.IsPermitted);
        Assert.Equal("lease", verdict.ViolatedConstraintId);
        Assert.Contains("not in the allowed list", verdict.Detail!);
    }

    [Fact]
    public async Task EnforceAsync_ProhibitedTakesPrecedenceOverAllowed()
    {
        var source = new DictSource(new()
        {
            ["lease"] = new DataResidencyConstraint
            {
                RecordClass = "lease",
                AllowedJurisdictions = new[] { "RU", "US-UT" }, // RU is in allowed
                ProhibitedJurisdictions = new[] { "RU" },        // …but also prohibited
            },
        });
        var enforcer = new DefaultDataResidencyEnforcer(source);
        var verdict = await enforcer.EnforceAsync("lease", "RU");
        Assert.False(verdict.IsPermitted);
        Assert.Contains("prohibited", verdict.Detail!);
    }

    [Fact]
    public async Task EnforceAsync_AllowedListEmpty_AnyJurisdictionPermittedUnlessProhibited()
    {
        var source = new DictSource(new()
        {
            ["lease"] = new DataResidencyConstraint
            {
                RecordClass = "lease",
                ProhibitedJurisdictions = new[] { "RU" },
            },
        });
        var enforcer = new DefaultDataResidencyEnforcer(source);

        var v1 = await enforcer.EnforceAsync("lease", "EU-DE");
        Assert.True(v1.IsPermitted);

        var v2 = await enforcer.EnforceAsync("lease", "RU");
        Assert.False(v2.IsPermitted);
    }

    [Fact]
    public async Task EnforceAsync_NullArgs_Throws()
    {
        var enforcer = new DefaultDataResidencyEnforcer(new EmptyDataResidencyConstraintSource());
        await Assert.ThrowsAsync<ArgumentException>(() => enforcer.EnforceAsync("", "US-UT").AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => enforcer.EnforceAsync("lease", "").AsTask());
    }

    [Fact]
    public void Constructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultDataResidencyEnforcer(null!));
    }

    [Fact]
    public async Task EnforceAsync_HonorsCancellation()
    {
        var enforcer = new DefaultDataResidencyEnforcer(new EmptyDataResidencyConstraintSource());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            enforcer.EnforceAsync("lease", "US-UT", cts.Token).AsTask());
    }

    private sealed class DictSource : IDataResidencyConstraintSource
    {
        private readonly Dictionary<string, DataResidencyConstraint> _by;
        public DictSource(Dictionary<string, DataResidencyConstraint> by) => _by = by;
        public DataResidencyConstraint? GetConstraint(string recordClass) =>
            _by.TryGetValue(recordClass, out var c) ? c : null;
    }
}
