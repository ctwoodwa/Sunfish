using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Regulatory.Tests;

public sealed class SanctionsScreenerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ScreenAsync_EmptySource_NoHits()
    {
        var screener = new DefaultSanctionsScreener(new EmptySanctionsListSource(), time: new FakeTime(Now));
        var result = await screener.ScreenAsync("subject-1");
        Assert.Empty(result.Hits);
        Assert.Equal("subject-1", result.SubjectId);
        Assert.Equal(ScreeningPolicy.Default, result.Policy);
        Assert.Equal(Now, result.ScreenedAt);
    }

    [Fact]
    public async Task ScreenAsync_WithHits_ReturnsThemAllRegardlessOfPolicy()
    {
        var entries = new[]
        {
            new SanctionsListEntry
            {
                ListSource = "OFAC-SDN",
                MatchedName = "John Doe",
                MatchScore = 0.95,
                ListVersion = "2026-05-01",
            },
        };
        var source = new ListSource(entries);

        var defaultScreener = new DefaultSanctionsScreener(source, ScreeningPolicy.Default, new FakeTime(Now));
        var defaultResult = await defaultScreener.ScreenAsync("john-doe");
        Assert.Single(defaultResult.Hits);
        Assert.Equal(ScreeningPolicy.Default, defaultResult.Policy);

        // AdvisoryOnly does NOT suppress the hit content per A1.3 — substrate
        // returns the same shape; the host decides what to do.
        var advisoryScreener = new DefaultSanctionsScreener(source, ScreeningPolicy.AdvisoryOnly, new FakeTime(Now));
        var advisoryResult = await advisoryScreener.ScreenAsync("john-doe");
        Assert.Single(advisoryResult.Hits);
        Assert.Equal(ScreeningPolicy.AdvisoryOnly, advisoryResult.Policy);
    }

    [Fact]
    public async Task ScreenAsync_HonorsCancellation()
    {
        var screener = new DefaultSanctionsScreener(new EmptySanctionsListSource());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            screener.ScreenAsync("subject-1", cts.Token).AsTask());
    }

    [Fact]
    public async Task ScreenAsync_NullOrEmptySubject_Throws()
    {
        var screener = new DefaultSanctionsScreener(new EmptySanctionsListSource());
        await Assert.ThrowsAsync<ArgumentException>(() => screener.ScreenAsync("").AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => screener.ScreenAsync(null!).AsTask());
    }

    [Fact]
    public void Constructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultSanctionsScreener(null!));
    }

    [Fact]
    public void Policy_ReflectsConstructorChoice()
    {
        var s1 = new DefaultSanctionsScreener(new EmptySanctionsListSource());
        Assert.Equal(ScreeningPolicy.Default, s1.Policy);

        var s2 = new DefaultSanctionsScreener(new EmptySanctionsListSource(), ScreeningPolicy.AdvisoryOnly);
        Assert.Equal(ScreeningPolicy.AdvisoryOnly, s2.Policy);
    }

    private sealed class ListSource : ISanctionsListSource
    {
        private readonly IReadOnlyList<SanctionsListEntry> _entries;
        public ListSource(IReadOnlyList<SanctionsListEntry> entries) => _entries = entries;
        public IReadOnlyList<SanctionsListEntry> MatchesFor(string subjectId) => _entries;
    }

    private sealed class FakeTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
