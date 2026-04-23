using Sunfish.Kernel.SchemaRegistry.Compaction;
using Sunfish.Kernel.SchemaRegistry.Upcasters;

namespace Sunfish.Kernel.SchemaRegistry.Tests;

/// <summary>
/// Contract coverage for <see cref="UpcasterRetirement"/> and its integration with
/// <see cref="UpcasterChain"/>: retirement is idempotent, visible via
/// <see cref="IUpcasterRetirement.IsRetired"/>, and causes <see cref="UpcasterChain.ApplyChain"/>
/// to skip retired edges.
/// </summary>
public class UpcasterRetirementTests
{
    private const string EventType = "record.updated";

    private sealed class MarkerUpcaster : IUpcaster
    {
        public string EventType { get; }
        public string FromVersion { get; }
        public string ToVersion { get; }

        public MarkerUpcaster(string eventType, string from, string to)
        {
            EventType = eventType;
            FromVersion = from;
            ToVersion = to;
        }

        public object Upcast(object olderEvent)
        {
            var map = (Dictionary<string, string>)olderEvent;
            var copy = new Dictionary<string, string>(map);
            copy[$"up:{FromVersion}->{ToVersion}"] = "1";
            return copy;
        }
    }

    [Fact]
    public void Retire_MarksUpcasterRetired()
    {
        var retirement = new UpcasterRetirement();
        retirement.Retire(EventType, "v1", "v2");

        Assert.True(retirement.IsRetired(EventType, "v1", "v2"));
        Assert.Single(retirement.Retirements);
        var record = retirement.Retirements[0];
        Assert.Equal(EventType, record.EventType);
        Assert.Equal("v1", record.FromVersion);
        Assert.Equal("v2", record.ToVersion);
    }

    [Fact]
    public void IsRetired_FalseForUnretired()
    {
        var retirement = new UpcasterRetirement();
        retirement.Retire(EventType, "v1", "v2");

        Assert.False(retirement.IsRetired(EventType, "v2", "v3"));
        Assert.False(retirement.IsRetired("other.type", "v1", "v2"));
    }

    [Fact]
    public void Retire_IsIdempotent()
    {
        var retirement = new UpcasterRetirement();
        retirement.Retire(EventType, "v1", "v2");
        retirement.Retire(EventType, "v1", "v2");
        retirement.Retire(EventType, "v1", "v2");

        Assert.True(retirement.IsRetired(EventType, "v1", "v2"));
        Assert.Single(retirement.Retirements);
    }

    [Fact]
    public void ApplyChain_SkipsRetiredUpcaster()
    {
        var retirement = new UpcasterRetirement();
        var chain = new UpcasterChain(retirement);
        chain.AddUpcaster(new MarkerUpcaster(EventType, "v1", "v2"));
        chain.AddUpcaster(new MarkerUpcaster(EventType, "v2", "v3"));

        // Before retirement: full chain works.
        var before = chain.ApplyChain(EventType, new Dictionary<string, string>(), "v1", "v3");
        Assert.NotNull(before);

        // Retire the first hop.
        retirement.Retire(EventType, "v1", "v2");

        var after = chain.ApplyChain(EventType, new Dictionary<string, string>(), "v1", "v3");
        Assert.Null(after);
    }

    [Fact]
    public void Retirements_IsImmutableSnapshot()
    {
        var retirement = new UpcasterRetirement();
        retirement.Retire(EventType, "v1", "v2");

        var snapshot = retirement.Retirements;

        retirement.Retire(EventType, "v2", "v3");

        // Prior snapshot must not reflect the subsequent mutation.
        Assert.Single(snapshot);
        Assert.Equal(2, retirement.Retirements.Count);
    }
}
