using Sunfish.Kernel.Buckets;

namespace Sunfish.Kernel.Buckets.Tests;

public sealed class SimpleBucketFilterEvaluatorTests
{
    private readonly IBucketFilterEvaluator _eval = new SimpleBucketFilterEvaluator();

    [Fact]
    public void Equality_operator_matches()
    {
        var ctx = new Dictionary<string, object?>
        {
            ["record.team_id"] = "T1",
            ["peer.team_id"] = "T1",
        };

        Assert.True(_eval.Evaluate("record.team_id = peer.team_id", ctx));
    }

    [Fact]
    public void Inequality_operator_matches()
    {
        var ctx = new Dictionary<string, object?>
        {
            ["record.team_id"] = "T1",
            ["peer.team_id"] = "T2",
        };

        Assert.True(_eval.Evaluate("record.team_id != peer.team_id", ctx));
        Assert.False(_eval.Evaluate("record.team_id = peer.team_id", ctx));
    }

    [Fact]
    public void And_chain_requires_all_clauses_true()
    {
        var ctx = new Dictionary<string, object?>
        {
            ["record.team_id"] = "T1",
            ["peer.team_id"] = "T1",
            ["project.archived"] = true,
        };

        Assert.True(_eval.Evaluate("record.team_id = peer.team_id AND project.archived = true", ctx));
        Assert.False(_eval.Evaluate("record.team_id = peer.team_id AND project.archived = false", ctx));
    }

    [Fact]
    public void Dotted_property_access_resolves_from_context()
    {
        var ctx = new Dictionary<string, object?>
        {
            ["project.archived"] = true,
        };

        Assert.True(_eval.Evaluate("project.archived = true", ctx));
    }

    [Fact]
    public void Undefined_field_returns_false_on_equality()
    {
        var ctx = new Dictionary<string, object?>();

        Assert.False(_eval.Evaluate("record.nonexistent = 'anything'", ctx));
    }

    [Fact]
    public void Throws_on_unknown_operator()
    {
        var ctx = new Dictionary<string, object?> { ["a"] = 1 };

        Assert.Throws<BucketFilterSyntaxException>(() => _eval.Evaluate("a < 5", ctx));
    }

    [Fact]
    public void Null_filter_is_treated_as_always_true()
    {
        var ctx = new Dictionary<string, object?>();
        Assert.True(_eval.Evaluate(null, ctx));
        Assert.True(_eval.Evaluate("", ctx));
        Assert.True(_eval.Evaluate("   ", ctx));
    }
}
