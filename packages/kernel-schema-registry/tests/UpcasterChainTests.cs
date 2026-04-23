using Sunfish.Kernel.SchemaRegistry.Upcasters;

namespace Sunfish.Kernel.SchemaRegistry.Tests;

/// <summary>
/// Contract coverage for <see cref="UpcasterChain"/> — forward-only chained application,
/// identity, missing edges, and cycle safety.
/// </summary>
public class UpcasterChainTests
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
    public void ApplyChain_SingleUpcaster_Applies()
    {
        var chain = new UpcasterChain();
        chain.AddUpcaster(new MarkerUpcaster(EventType, "v1", "v2"));

        var result = chain.ApplyChain(EventType, new Dictionary<string, string>(), "v1", "v2");

        var map = Assert.IsType<Dictionary<string, string>>(result);
        Assert.True(map.ContainsKey("up:v1->v2"));
    }

    [Fact]
    public void ApplyChain_Multistep_AppliesInOrder()
    {
        var chain = new UpcasterChain();
        chain.AddUpcaster(new MarkerUpcaster(EventType, "v1", "v2"));
        chain.AddUpcaster(new MarkerUpcaster(EventType, "v2", "v3"));

        var result = chain.ApplyChain(EventType, new Dictionary<string, string>(), "v1", "v3");

        var map = Assert.IsType<Dictionary<string, string>>(result);
        Assert.True(map.ContainsKey("up:v1->v2"));
        Assert.True(map.ContainsKey("up:v2->v3"));
    }

    [Fact]
    public void ApplyChain_NoPath_ReturnsNull()
    {
        var chain = new UpcasterChain();
        chain.AddUpcaster(new MarkerUpcaster(EventType, "v1", "v2"));

        var result = chain.ApplyChain(EventType, new Dictionary<string, string>(), "v1", "v9");

        Assert.Null(result);
    }

    [Fact]
    public void ApplyChain_IdentityVersion_ReturnsUnchanged()
    {
        var chain = new UpcasterChain();
        var input = new Dictionary<string, string> { ["k"] = "v" };

        var result = chain.ApplyChain(EventType, input, "v1", "v1");

        Assert.Same(input, result);
    }

    [Fact]
    public void ApplyChain_Cycle_ReturnsNullWithoutInfiniteLoop()
    {
        var chain = new UpcasterChain();
        // Last write wins per (EventType, FromVersion). Adding v1->v2 and then
        // v2->v1 means v1->v2->v1 cycles indefinitely without termination; the
        // visited-set guard must bail.
        chain.AddUpcaster(new MarkerUpcaster(EventType, "v1", "v2"));
        chain.AddUpcaster(new MarkerUpcaster(EventType, "v2", "v1"));

        var result = chain.ApplyChain(EventType, new Dictionary<string, string>(), "v1", "v9");

        Assert.Null(result);
    }

    [Fact]
    public void ApplyChain_WrongEventType_ReturnsNull()
    {
        var chain = new UpcasterChain();
        chain.AddUpcaster(new MarkerUpcaster("other.type", "v1", "v2"));

        var result = chain.ApplyChain(EventType, new Dictionary<string, string>(), "v1", "v2");

        Assert.Null(result);
    }

    [Fact]
    public void AddUpcaster_Null_Throws()
    {
        var chain = new UpcasterChain();
        Assert.Throws<ArgumentNullException>(() => chain.AddUpcaster(null!));
    }
}
