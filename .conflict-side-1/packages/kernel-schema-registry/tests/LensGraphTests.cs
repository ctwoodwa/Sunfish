using Sunfish.Kernel.SchemaRegistry.Lenses;

namespace Sunfish.Kernel.SchemaRegistry.Tests;

/// <summary>
/// Contract coverage for <see cref="LensGraph"/> — BFS traversal, forward/backward
/// transforms, cycle safety, and shortest-path preference.
/// </summary>
public class LensGraphTests
{
    private const string EventType = "record.updated";

    // A tiny lens whose payload is a Dictionary<string, string>. It adds or removes
    // a marker key per version so we can assert which lenses got applied.
    private sealed class MarkerLens : ISchemaLens
    {
        public string EventType { get; }
        public string FromVersion { get; }
        public string ToVersion { get; }

        public MarkerLens(string eventType, string from, string to)
        {
            EventType = eventType;
            FromVersion = from;
            ToVersion = to;
        }

        public object ForwardTransform(object olderEvent)
        {
            var map = (Dictionary<string, string>)olderEvent;
            var copy = new Dictionary<string, string>(map);
            copy[$"fwd:{FromVersion}->{ToVersion}"] = "1";
            return copy;
        }

        public object BackwardTransform(object newerEvent)
        {
            var map = (Dictionary<string, string>)newerEvent;
            var copy = new Dictionary<string, string>(map);
            copy[$"bwd:{ToVersion}->{FromVersion}"] = "1";
            return copy;
        }
    }

    [Fact]
    public void Transform_SingleLens_AppliesForward()
    {
        var graph = new LensGraph();
        graph.AddLens(new MarkerLens(EventType, "v1", "v2"));

        var result = graph.Transform(EventType, new Dictionary<string, string>(), "v1", "v2");

        var map = Assert.IsType<Dictionary<string, string>>(result);
        Assert.True(map.ContainsKey("fwd:v1->v2"));
    }

    [Fact]
    public void Transform_SingleLens_AppliesBackward()
    {
        var graph = new LensGraph();
        graph.AddLens(new MarkerLens(EventType, "v1", "v2"));

        var result = graph.Transform(EventType, new Dictionary<string, string>(), "v2", "v1");

        var map = Assert.IsType<Dictionary<string, string>>(result);
        Assert.True(map.ContainsKey("bwd:v2->v1"));
    }

    [Fact]
    public void Transform_TwoHopChain_AppliesBothForward()
    {
        var graph = new LensGraph();
        graph.AddLens(new MarkerLens(EventType, "v1", "v2"));
        graph.AddLens(new MarkerLens(EventType, "v2", "v3"));

        var result = graph.Transform(EventType, new Dictionary<string, string>(), "v1", "v3");

        var map = Assert.IsType<Dictionary<string, string>>(result);
        Assert.True(map.ContainsKey("fwd:v1->v2"));
        Assert.True(map.ContainsKey("fwd:v2->v3"));
    }

    [Fact]
    public void Transform_NoPath_ReturnsNull()
    {
        var graph = new LensGraph();
        graph.AddLens(new MarkerLens(EventType, "v1", "v2"));

        var result = graph.Transform(EventType, new Dictionary<string, string>(), "v1", "v9");

        Assert.Null(result);
    }

    [Fact]
    public void Transform_SameVersion_ReturnsEventUnchanged()
    {
        var graph = new LensGraph();
        var input = new Dictionary<string, string> { ["payload"] = "original" };

        var result = graph.Transform(EventType, input, "v1", "v1");

        Assert.Same(input, result);
    }

    [Fact]
    public void Transform_DifferentEventType_NoCrossOver()
    {
        var graph = new LensGraph();
        graph.AddLens(new MarkerLens("other.type", "v1", "v2"));

        var result = graph.Transform(EventType, new Dictionary<string, string>(), "v1", "v2");

        Assert.Null(result);
    }

    [Fact]
    public void Transform_Cycle_DoesNotInfiniteLoop()
    {
        var graph = new LensGraph();
        // v1 -> v2 and v2 -> v1 form a cycle. Target v9 is unreachable; BFS must bail.
        graph.AddLens(new MarkerLens(EventType, "v1", "v2"));
        graph.AddLens(new MarkerLens(EventType, "v2", "v1"));

        var result = graph.Transform(EventType, new Dictionary<string, string>(), "v1", "v9");

        Assert.Null(result);
    }

    [Fact]
    public void Transform_DirectLensPreferredOverTwoHopChain()
    {
        var graph = new LensGraph();
        // Two paths from v1 to v3: direct lens + the v1->v2->v3 chain.
        graph.AddLens(new MarkerLens(EventType, "v1", "v2"));
        graph.AddLens(new MarkerLens(EventType, "v2", "v3"));
        graph.AddLens(new MarkerLens(EventType, "v1", "v3"));

        var result = graph.Transform(EventType, new Dictionary<string, string>(), "v1", "v3");

        var map = Assert.IsType<Dictionary<string, string>>(result);
        // BFS returns the shortest path — the direct lens — so the v2 intermediate key is absent.
        Assert.True(map.ContainsKey("fwd:v1->v3"));
        Assert.False(map.ContainsKey("fwd:v1->v2"));
        Assert.False(map.ContainsKey("fwd:v2->v3"));
    }

    [Fact]
    public void HasPath_ReportsReachability()
    {
        var graph = new LensGraph();
        graph.AddLens(new MarkerLens(EventType, "v1", "v2"));
        graph.AddLens(new MarkerLens(EventType, "v2", "v3"));

        Assert.True(graph.HasPath(EventType, "v1", "v3"));
        Assert.True(graph.HasPath(EventType, "v3", "v1")); // backward via the two lenses
        Assert.False(graph.HasPath(EventType, "v1", "v9"));
        Assert.True(graph.HasPath(EventType, "v1", "v1")); // identity
    }

    [Fact]
    public void ShortestPath_ReturnsVersionSequence()
    {
        var graph = new LensGraph();
        graph.AddLens(new MarkerLens(EventType, "v1", "v2"));
        graph.AddLens(new MarkerLens(EventType, "v2", "v3"));
        graph.AddLens(new MarkerLens(EventType, "v3", "v4"));

        var path = graph.ShortestPath(EventType, "v1", "v4");

        Assert.Equal(new[] { "v1", "v2", "v3", "v4" }, path);
    }

    [Fact]
    public void ShortestPath_NoPath_ReturnsEmpty()
    {
        var graph = new LensGraph();
        graph.AddLens(new MarkerLens(EventType, "v1", "v2"));

        var path = graph.ShortestPath(EventType, "v1", "v9");

        Assert.Empty(path);
    }

    [Fact]
    public void AddLens_Null_Throws()
    {
        var graph = new LensGraph();
        Assert.Throws<ArgumentNullException>(() => graph.AddLens(null!));
    }
}
