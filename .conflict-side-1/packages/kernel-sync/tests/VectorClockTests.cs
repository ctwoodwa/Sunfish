namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Coverage for <see cref="VectorClock"/> — the algebra (dominates /
/// dominated / concurrent / equal), merge idempotence, and CBOR roundtrip.
/// </summary>
public class VectorClockTests
{
    [Fact]
    public void Empty_Clock_Returns_Zero_For_Unknown_Node()
    {
        var clock = new VectorClock();
        Assert.Equal(0ul, clock.Get("unknownNode"));
    }

    [Fact]
    public void Set_And_Get_Roundtrip_For_Single_Node()
    {
        var clock = new VectorClock();
        clock.Set("nodeA", 5);
        Assert.Equal(5ul, clock.Get("nodeA"));

        clock.Set("nodeA", 9);
        Assert.Equal(9ul, clock.Get("nodeA"));
    }

    [Fact]
    public void Compare_Equal_When_Clocks_Have_Same_Entries()
    {
        var a = new VectorClock();
        a.Set("x", 1);
        a.Set("y", 2);
        var b = new VectorClock();
        b.Set("x", 1);
        b.Set("y", 2);

        Assert.Equal(VectorClockRelationship.Equal, VectorClock.Compare(a, b));
    }

    [Fact]
    public void Compare_Dominates_When_One_Clock_Is_Strictly_Ahead()
    {
        var ahead = new VectorClock();
        ahead.Set("x", 3);
        ahead.Set("y", 2);
        var behind = new VectorClock();
        behind.Set("x", 2);
        behind.Set("y", 2);

        Assert.Equal(VectorClockRelationship.Dominates, VectorClock.Compare(ahead, behind));
        Assert.Equal(VectorClockRelationship.Dominated, VectorClock.Compare(behind, ahead));
        Assert.True(ahead.Dominates(behind));
        Assert.False(behind.Dominates(ahead));
    }

    [Fact]
    public void Compare_Concurrent_When_Each_Clock_Has_Disjoint_Advance()
    {
        var a = new VectorClock();
        a.Set("x", 5);
        a.Set("y", 1);
        var b = new VectorClock();
        b.Set("x", 1);
        b.Set("y", 5);

        Assert.Equal(VectorClockRelationship.Concurrent, VectorClock.Compare(a, b));
    }

    [Fact]
    public void Merge_Is_Pointwise_Max_And_Idempotent()
    {
        var a = new VectorClock();
        a.Set("x", 5);
        a.Set("y", 1);
        var b = new VectorClock();
        b.Set("x", 2);
        b.Set("y", 7);
        b.Set("z", 4);

        a.Merge(b);

        Assert.Equal(5ul, a.Get("x"));
        Assert.Equal(7ul, a.Get("y"));
        Assert.Equal(4ul, a.Get("z"));

        // Merging the same thing again must not change anything — idempotence.
        a.Merge(b);
        Assert.Equal(5ul, a.Get("x"));
        Assert.Equal(7ul, a.Get("y"));
        Assert.Equal(4ul, a.Get("z"));
    }

    [Fact]
    public void Merge_Makes_Result_Dominate_Both_Inputs()
    {
        var a = new VectorClock();
        a.Set("x", 3);
        a.Set("y", 1);
        var b = new VectorClock();
        b.Set("x", 2);
        b.Set("y", 4);
        var expected = new Dictionary<string, ulong> { ["x"] = 3, ["y"] = 4 };

        a.Merge(b);
        var merged = new VectorClock(a.Snapshot());

        Assert.Equal(expected["x"], merged.Get("x"));
        Assert.Equal(expected["y"], merged.Get("y"));
    }

    [Fact]
    public void Cbor_Roundtrip_Preserves_All_Entries()
    {
        var clock = new VectorClock();
        clock.Set("alpha", 100);
        clock.Set("beta", 200);
        clock.Set("gamma", 300);

        var bytes = clock.ToCbor();
        var decoded = VectorClock.FromCbor(bytes);

        Assert.Equal(100ul, decoded.Get("alpha"));
        Assert.Equal(200ul, decoded.Get("beta"));
        Assert.Equal(300ul, decoded.Get("gamma"));
    }
}
