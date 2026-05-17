using Xunit;

namespace Sunfish.Foundation.Events.Tests;

/// <summary>
/// W#60 P4 — coverage for the inline ULID generator at
/// <see cref="EventId"/>.
/// </summary>
public sealed class EventIdTests
{
    [Fact]
    public void New_ReturnsLengthExactly26()
    {
        var id = EventId.New();
        Assert.Equal(26, id.Length);
    }

    [Fact]
    public void New_OnlyContainsCrockfordBase32Alphabet()
    {
        var id = EventId.New();
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        Assert.All(id, c => Assert.Contains(c, alphabet));
    }

    [Fact]
    public void New_DistinctAcrossManyCalls()
    {
        var ids = Enumerable.Range(0, 1000).Select(_ => EventId.New()).ToList();
        Assert.Equal(1000, ids.Distinct().Count());
    }

    [Fact]
    public void New_TimestampPrefixSortsInChronologicalOrder()
    {
        var earlier = EventId.New(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var later   = EventId.New(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        // ULIDs sort lexicographically by mint-time.
        Assert.True(string.Compare(earlier, later, StringComparison.Ordinal) < 0);
    }

    [Fact]
    public void New_PreUnixEpoch_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EventId.New(new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }
}
