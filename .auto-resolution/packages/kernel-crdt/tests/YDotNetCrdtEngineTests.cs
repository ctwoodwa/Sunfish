using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

using Microsoft.Extensions.DependencyInjection;

using Sunfish.Kernel.Crdt.DependencyInjection;

namespace Sunfish.Kernel.Crdt.Tests;

/// <summary>
/// Backend-parity suite for <see cref="YDotNetCrdtEngine"/>. Runs the same
/// convergence + idempotence properties that <see cref="ConvergencePropertyTests"/>
/// runs against the stub, but against the real Yjs/yrs backend — so we cover
/// actual concurrent-edit merge semantics (character-level RGA, map LWW keyed
/// on logical timestamps, array fractional-position order) that the stub's
/// total-order-replay cannot exercise.
///
/// If the YDotNet native binaries cannot load on the current platform, all
/// tests in this class will fail with a DllNotFoundException — that's the
/// desired signal ("the fallback backend is unavailable here") rather than a
/// silent pass.
/// </summary>
public class YDotNetCrdtEngineTests
{
    private static ICrdtEngine Engine() => new YDotNetCrdtEngine();

    [Fact]
    public void EngineMetadata_IsPopulated()
    {
        ICrdtEngine engine = Engine();
        Assert.Equal("ydotnet", engine.EngineName);
        Assert.False(string.IsNullOrWhiteSpace(engine.EngineVersion));
    }

    [Fact]
    public async Task CreateDocument_AssignsRequestedId()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("my-doc-id");
        Assert.Equal("my-doc-id", doc.DocumentId);
    }

    [Fact]
    public void CreateDocument_RejectsEmptyId()
    {
        var engine = Engine();
        Assert.Throws<ArgumentException>(() => engine.CreateDocument(string.Empty));
    }

    [Fact]
    public async Task OpenDocument_WithEmptySnapshot_ReturnsEmptyDoc()
    {
        var engine = Engine();
        await using var doc = engine.OpenDocument("doc-1", ReadOnlyMemory<byte>.Empty);
        Assert.Equal(0, doc.GetText("body").Length);
        Assert.Equal(0, doc.GetMap("meta").Count);
        Assert.Equal(0, doc.GetList("items").Count);
    }

    [Fact]
    public async Task TwoPeers_ConcurrentTextEdits_Converge_RealCrdtMerge()
    {
        // This is the property that the stub cannot honestly test: both peers
        // insert at index 0 concurrently. The stub's total-order replay would
        // produce either "abcxyz" or "xyzabc" deterministically, but a real
        // CRDT must converge to the same string on both sides via a logical-
        // timestamp tiebreak (not just positional replay).
        var engine = Engine();
        await using var alice = engine.CreateDocument("concurrent-text");
        await using var bob = engine.CreateDocument("concurrent-text");

        // Cross-seed the docs so they share the same root text container.
        alice.GetText("t");
        bob.GetText("t");

        // Before any concurrent edit, sync the empty state so the two
        // documents share a causal anchor.
        var aliceSv = alice.VectorClock;
        var bobSv = bob.VectorClock;
        bob.ApplyDelta(alice.EncodeDelta(bobSv));
        alice.ApplyDelta(bob.EncodeDelta(aliceSv));

        alice.GetText("t").Insert(0, "abc");
        bob.GetText("t").Insert(0, "xyz");

        // Full bidirectional exchange.
        aliceSv = alice.VectorClock;
        bobSv = bob.VectorClock;
        bob.ApplyDelta(alice.EncodeDelta(bobSv));
        alice.ApplyDelta(bob.EncodeDelta(aliceSv));

        var a = alice.GetText("t").Value;
        var b = bob.GetText("t").Value;
        Assert.Equal(a, b);
        Assert.Equal(6, a.Length);
        Assert.True(
            a == "abcxyz" || a == "xyzabc" || a == "axbycz" || a == "xaybzc"
            || a.Contains('a') && a.Contains('x'),
            $"Expected a merge of 'abc' and 'xyz'; got '{a}'.");
    }

    [Fact]
    public async Task TwoPeers_ConcurrentMapSetSameKey_ConvergeToSameWinner()
    {
        // LWW on the same key from two peers — both sides MUST see the same
        // winner after bidirectional exchange.
        var engine = Engine();
        await using var alice = engine.CreateDocument("concurrent-map");
        await using var bob = engine.CreateDocument("concurrent-map");

        // Ensure root containers exist on both sides with same type.
        alice.GetMap("m");
        bob.GetMap("m");

        alice.GetMap("m").Set("k", "alice-wins");
        bob.GetMap("m").Set("k", "bob-wins");

        var aliceSv = alice.VectorClock;
        var bobSv = bob.VectorClock;
        bob.ApplyDelta(alice.EncodeDelta(bobSv));
        alice.ApplyDelta(bob.EncodeDelta(aliceSv));

        var a = alice.GetMap("m").Get<string>("k");
        var b = bob.GetMap("m").Get<string>("k");
        Assert.Equal(a, b);
        Assert.Contains(a, new[] { "alice-wins", "bob-wins" });
    }

    [Fact]
    public async Task Snapshot_RoundTrips_PreservesState()
    {
        var engine = Engine();
        await using var alice = engine.CreateDocument("snap-doc");
        alice.GetText("t").Insert(0, "Hello, world!");
        alice.GetMap("m").Set("flag", true);
        alice.GetMap("m").Set("n", 42);
        alice.GetList("l").Push("one");
        alice.GetList("l").Push("two");

        var snapshot = alice.ToSnapshot();
        Assert.False(snapshot.IsEmpty);

        await using var bob = engine.OpenDocument("snap-doc", snapshot);
        Assert.Equal("Hello, world!", bob.GetText("t").Value);
        Assert.True(bob.GetMap("m").Get<bool>("flag"));
        Assert.Equal(42, bob.GetMap("m").Get<int>("n"));
        Assert.Equal(2, bob.GetList("l").Count);
        Assert.Equal("one", bob.GetList("l").Get<string>(0));
        Assert.Equal("two", bob.GetList("l").Get<string>(1));
    }

    [Fact]
    public async Task ApplyDelta_IsIdempotent()
    {
        var engine = Engine();
        await using var alice = engine.CreateDocument("idem-doc");
        await using var bob = engine.CreateDocument("idem-doc");

        alice.GetText("t").Insert(0, "hello");
        alice.GetMap("m").Set("k", "v");

        var delta = alice.EncodeDelta(bob.VectorClock);
        bob.ApplyDelta(delta);
        var firstText = bob.GetText("t").Value;
        var firstVal = bob.GetMap("m").Get<string>("k");
        var firstSv = bob.VectorClock.ToArray();

        bob.ApplyDelta(delta); // Applying the same delta again must be a no-op.
        var secondText = bob.GetText("t").Value;
        var secondVal = bob.GetMap("m").Get<string>("k");
        var secondSv = bob.VectorClock.ToArray();

        Assert.Equal(firstText, secondText);
        Assert.Equal(firstVal, secondVal);
        Assert.Equal(firstSv, secondSv);
    }

    [Fact]
    public void AddSunfishCrdtEngine_DefaultsToYDotNet()
    {
        var services = new ServiceCollection();
        services.AddSunfishCrdtEngine();
        using var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<ICrdtEngine>();
        Assert.Equal("ydotnet", engine.EngineName);
    }

    [Fact]
    public void AddSunfishCrdtEngineStub_OverridesDefaultBeforeRegistration()
    {
        var services = new ServiceCollection();
        services.AddSunfishCrdtEngineStub();
        services.AddSunfishCrdtEngine(); // Should no-op — stub already registered.
        using var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<ICrdtEngine>();
        Assert.Equal("stub", engine.EngineName);
    }

    // --- Property-based harness — same shape as ConvergencePropertyTests. ---

    public sealed record Op(bool Actor, OpKind Kind, int RawIndex, string? Payload);

    public enum OpKind { TextInsert, TextDelete, MapSet, MapDelete, ListPush, ListRemove }

    public static Arbitrary<Op[]> OpSequences()
    {
        var opGen =
            from actor in ArbMap.Default.GeneratorFor<bool>()
            from kind in Gen.Elements(
                OpKind.TextInsert, OpKind.TextInsert, OpKind.TextInsert,
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

    [Property(Arbitrary = new[] { typeof(YDotNetCrdtEngineTests) }, MaxTest = 40)]
    public async Task Convergence_AnyOpSequence_ExchangesConverge(Op[] ops)
    {
        var engine = Engine();
        await using var alice = engine.CreateDocument("prop-doc");
        alice.GetText("t").Insert(0, "base");
        alice.GetMap("m").Set("seed", 0);
        alice.GetList("l").Push("seed");
        var baseSnapshot = alice.ToSnapshot();
        await using var bob = engine.OpenDocument("prop-doc", baseSnapshot);

        foreach (var op in ops)
        {
            var target = op.Actor ? alice : bob;
            TryApplyOp(target, op);
        }

        // Compute BOTH deltas before applying either — this is the correct
        // bidirectional-sync protocol (each side encodes the ops the other is
        // missing according to pre-exchange clocks). Applying in the other
        // order lets Alice's own ops round-trip through Bob back to Alice,
        // which is supposed to be an idempotent no-op but stresses edge cases
        // in any CRDT's integration logic.
        var aliceClock = alice.VectorClock;
        var bobClock = bob.VectorClock;
        var aliceToBob = alice.EncodeDelta(bobClock);
        var bobToAlice = bob.EncodeDelta(aliceClock);
        bob.ApplyDelta(aliceToBob);
        alice.ApplyDelta(bobToAlice);

        Assert.Equal(alice.GetText("t").Value, bob.GetText("t").Value);
        AssertMapEqual(alice.GetMap("m"), bob.GetMap("m"));
        AssertListEqual(alice.GetList("l"), bob.GetList("l"));
    }

    [Property(Arbitrary = new[] { typeof(YDotNetCrdtEngineTests) }, MaxTest = 20)]
    public async Task Idempotence_ApplyingSameDeltaTwice_NoOp(Op[] ops)
    {
        var engine = Engine();
        await using var alice = engine.CreateDocument("idem-doc");
        await using var bob = engine.CreateDocument("idem-doc");

        // Ensure shared root containers.
        alice.GetText("t"); alice.GetMap("m"); alice.GetList("l");
        bob.GetText("t"); bob.GetMap("m"); bob.GetList("l");

        foreach (var op in ops) TryApplyOp(alice, op);

        var delta = alice.EncodeDelta(ReadOnlyMemory<byte>.Empty);
        bob.ApplyDelta(delta);
        var before = Serialize(bob);
        bob.ApplyDelta(delta);
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
                    doc.GetMap("m").Set($"k{op.RawIndex % 4}", op.Payload ?? "v");
                    break;
                case OpKind.MapDelete:
                    doc.GetMap("m").Remove($"k{op.RawIndex % 4}");
                    break;
                case OpKind.ListPush:
                    doc.GetList("l").Push(op.Payload ?? "x");
                    break;
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
            // Random op may target out-of-range indices; swallow and let the
            // convergence property speak for the ops that landed.
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
        var m = doc.GetMap("m");
        var mapKeys = string.Join(",", m.Keys.OrderBy(k => k, StringComparer.Ordinal)
            .Select(k => $"{k}={m.Get<string>(k)}"));
        var l = doc.GetList("l");
        var listItems = string.Join(",",
            Enumerable.Range(0, l.Count).Select(i => l.Get<string>(i)));
        return $"T:{text}|M:{mapKeys}|L:{listItems}";
    }
}
