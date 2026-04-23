using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Sunfish.Kernel.Crdt.Tests;

/// <summary>
/// Paper §15 Level 1 property-based harness: for any two peers starting from
/// identical state and applying any sequence of operations, after bidirectional
/// delta exchange they converge to the same state.
/// </summary>
public class ConvergencePropertyTests
{
    private static ICrdtEngine Engine() => new StubCrdtEngine();

    /// <summary>
    /// Simplified op DTO used to drive the harness. FsCheck generates random sequences;
    /// each op is dispatched to Alice or Bob depending on <see cref="Actor"/>.
    /// </summary>
    public sealed record Op(bool Actor, OpKind Kind, int RawIndex, string? Payload);

    public enum OpKind { TextInsert, TextDelete, MapSet, MapDelete, ListPush, ListRemove }

    public static Arbitrary<Op[]> OpSequences()
    {
        var opGen =
            from actor in ArbMap.Default.GeneratorFor<bool>()
            from kind in Gen.Elements(
                OpKind.TextInsert, OpKind.TextInsert, OpKind.TextInsert,   // bias toward inserts
                OpKind.TextDelete,
                OpKind.MapSet, OpKind.MapSet,
                OpKind.MapDelete,
                OpKind.ListPush, OpKind.ListPush,
                OpKind.ListRemove)
            from idx in Gen.Choose(0, 12)
            from text in Gen.Elements("a", "b", "c", "hello", "xy", "1", "end")
            select new Op(actor, kind, idx, text);
        return Gen.ArrayOf(opGen).ToArbitrary();
    }

    [Property(Arbitrary = new[] { typeof(ConvergencePropertyTests) }, MaxTest = 80)]
    public async Task Convergence_AnyOpSequence_ExchangesConverge(Op[] ops)
    {
        // Arrange — two peers sharing an initial snapshot.
        var engine = Engine();
        await using var alice = engine.CreateDocument("prop-doc");
        alice.GetText("t").Insert(0, "base");
        alice.GetMap("m").Set("seed", 0);
        alice.GetList("l").Push("seed");
        var baseSnapshot = alice.ToSnapshot();
        await using var bob = engine.OpenDocument("prop-doc", baseSnapshot);

        // Act — apply each op to the selected peer.
        foreach (var op in ops)
        {
            var target = op.Actor ? alice : bob;
            TryApplyOp(target, op);
        }

        // Exchange deltas both ways.
        var aliceClock = alice.VectorClock;
        var bobClock = bob.VectorClock;
        bob.ApplyDelta(alice.EncodeDelta(bobClock));
        alice.ApplyDelta(bob.EncodeDelta(aliceClock));

        // Assert — convergence across all containers.
        Assert.Equal(alice.GetText("t").Value, bob.GetText("t").Value);
        AssertMapEqual(alice.GetMap("m"), bob.GetMap("m"));
        AssertListEqual(alice.GetList("l"), bob.GetList("l"));
    }

    [Property(Arbitrary = new[] { typeof(ConvergencePropertyTests) }, MaxTest = 40)]
    public async Task Idempotence_ApplyingSameDeltaTwice_NoOp(Op[] ops)
    {
        var engine = Engine();
        await using var alice = engine.CreateDocument("idem-doc");
        await using var bob = engine.CreateDocument("idem-doc");

        foreach (var op in ops) TryApplyOp(alice, op);

        var delta = alice.EncodeDelta(ReadOnlyMemory<byte>.Empty);
        bob.ApplyDelta(delta);
        var before = Serialize(bob);
        bob.ApplyDelta(delta); // second application must not change observable state.
        var after = Serialize(bob);

        Assert.Equal(before, after);
    }

    private static void TryApplyOp(ICrdtDocument doc, Op op)
    {
        try
        {
            switch (op.Kind)
            {
                case OpKind.TextInsert:
                {
                    var t = doc.GetText("t");
                    var idx = Math.Clamp(op.RawIndex, 0, t.Length);
                    t.Insert(idx, op.Payload ?? "x");
                    break;
                }
                case OpKind.TextDelete:
                {
                    var t = doc.GetText("t");
                    if (t.Length == 0) break;
                    var idx = Math.Clamp(op.RawIndex, 0, t.Length - 1);
                    t.Delete(idx, 1);
                    break;
                }
                case OpKind.MapSet:
                {
                    doc.GetMap("m").Set($"k{op.RawIndex % 4}", op.Payload ?? "v");
                    break;
                }
                case OpKind.MapDelete:
                {
                    doc.GetMap("m").Remove($"k{op.RawIndex % 4}");
                    break;
                }
                case OpKind.ListPush:
                {
                    doc.GetList("l").Push(op.Payload ?? "x");
                    break;
                }
                case OpKind.ListRemove:
                {
                    var l = doc.GetList("l");
                    if (l.Count == 0) break;
                    var idx = Math.Clamp(op.RawIndex, 0, l.Count - 1);
                    l.RemoveAt(idx);
                    break;
                }
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            // Swallow — random op can target out-of-range indices; contract says
            // it throws, and the convergence property is still meaningful for the
            // ops that did land.
        }
    }

    private static void AssertMapEqual(ICrdtMap a, ICrdtMap b)
    {
        Assert.Equal(a.Count, b.Count);
        var aKeys = a.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        var bKeys = b.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(aKeys, bKeys);
        foreach (var key in aKeys)
        {
            Assert.Equal(a.Get<string>(key), b.Get<string>(key));
        }
    }

    private static void AssertListEqual(ICrdtList a, ICrdtList b)
    {
        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a.Get<string>(i), b.Get<string>(i));
        }
    }

    private static string Serialize(ICrdtDocument doc)
    {
        var text = doc.GetText("t").Value;
        var mapKeys = string.Join(",", doc.GetMap("m").Keys.OrderBy(k => k, StringComparer.Ordinal)
            .Select(k => $"{k}={doc.GetMap("m").Get<string>(k)}"));
        var listItems = string.Join(",",
            Enumerable.Range(0, doc.GetList("l").Count).Select(i => doc.GetList("l").Get<string>(i)));
        return $"T:{text}|M:{mapKeys}|L:{listItems}";
    }
}
