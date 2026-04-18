using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.PolicyEvaluator;
using Xunit;

namespace Sunfish.Foundation.Tests.PolicyEvaluator;

public class ReBACEvaluatorTests
{
    private static ContextEnvelope Now() => new(DateTimeOffset.UtcNow, Purpose: null);

    private static Subject MakeSubject()
    {
        using var kp = KeyPair.Generate();
        return new Subject(kp.PrincipalId);
    }

    [Fact]
    public async Task Evaluate_DirectRelation_PermitsWhenTupleExists()
    {
        var alice = MakeSubject();
        var doc = new PolicyResource("document", "doc-1");

        var model = PolicyModel.Create()
            .Type("document", t => t
                .Relation("owner", new RelationRewrite.DirectUsers(new[] { "user" }))
                .Relation("can_read", new RelationRewrite.ComputedUserset("owner")))
            .Build();

        var tuples = new InMemoryRelationTupleStore();
        await tuples.AddSubjectAsync(alice, "owner", doc);

        var evaluator = new ReBACPolicyEvaluator(model, tuples);
        var decision = await evaluator.EvaluateAsync(alice, new ActionType("read"), doc, Now());

        Assert.Equal(DecisionKind.Permit, decision.Kind);
        Assert.Single(decision.MatchedPolicies);
        Assert.Equal("document#can_read", decision.MatchedPolicies[0]);
    }

    [Fact]
    public async Task Evaluate_DirectRelation_DeniesWhenTupleMissing()
    {
        var alice = MakeSubject();
        var doc = new PolicyResource("document", "doc-1");

        var model = PolicyModel.Create()
            .Type("document", t => t
                .Relation("owner", new RelationRewrite.DirectUsers(new[] { "user" }))
                .Relation("can_read", new RelationRewrite.ComputedUserset("owner")))
            .Build();

        var tuples = new InMemoryRelationTupleStore();

        var evaluator = new ReBACPolicyEvaluator(model, tuples);
        var decision = await evaluator.EvaluateAsync(alice, new ActionType("read"), doc, Now());

        Assert.Equal(DecisionKind.Deny, decision.Kind);
    }

    [Fact]
    public async Task Evaluate_UnionRelation_ShortCircuitsOnFirstPermit()
    {
        var alice = MakeSubject();
        var doc = new PolicyResource("document", "doc-1");

        var model = PolicyModel.Create()
            .Type("document", t => t
                .Relation("viewer", new RelationRewrite.DirectUsers(new[] { "user" }))
                .Relation("editor", new RelationRewrite.DirectUsers(new[] { "user" }))
                .Relation("can_read", new RelationRewrite.Union(new RelationRewrite[]
                {
                    new RelationRewrite.ComputedUserset("viewer"),
                    new RelationRewrite.ComputedUserset("editor"),
                })))
            .Build();

        var tuples = new InMemoryRelationTupleStore();
        // Only editor tuple exists — the union must still permit via the second branch.
        await tuples.AddSubjectAsync(alice, "editor", doc);

        var evaluator = new ReBACPolicyEvaluator(model, tuples);
        var decision = await evaluator.EvaluateAsync(alice, new ActionType("read"), doc, Now());

        Assert.Equal(DecisionKind.Permit, decision.Kind);
    }

    [Fact]
    public async Task Evaluate_TupleToUserset_FollowsIndirectRelation()
    {
        // Model: folder#viewer, document#parent (tuple-to-userset), document#can_read = parent#viewer
        var model = PolicyModel.Create()
            .Type("folder", t => t
                .Relation("viewer", new RelationRewrite.DirectUsers(new[] { "user" })))
            .Type("document", t => t
                .Relation("parent", new RelationRewrite.DirectUsers(new[] { "folder" }))
                .Relation("can_read", new RelationRewrite.TupleToUserset("parent", "viewer")))
            .Build();

        var alice = MakeSubject();
        var folder = new PolicyResource("folder", "fold-1");
        var doc = new PolicyResource("document", "doc-1");

        var tuples = new InMemoryRelationTupleStore();
        // doc has parent -> folder (expressed as SelfRef to the folder resource)
        await tuples.AddAsync(new UsersetRef.SelfRef(folder), "parent", doc);
        // alice is a viewer of folder
        await tuples.AddSubjectAsync(alice, "viewer", folder);

        var evaluator = new ReBACPolicyEvaluator(model, tuples);
        var decision = await evaluator.EvaluateAsync(alice, new ActionType("read"), doc, Now());

        Assert.Equal(DecisionKind.Permit, decision.Kind);
    }

    [Fact]
    public async Task Evaluate_MissingTypeInModel_ReturnsIndeterminate()
    {
        var alice = MakeSubject();
        var model = PolicyModel.Create().Build(); // empty model
        var tuples = new InMemoryRelationTupleStore();
        var evaluator = new ReBACPolicyEvaluator(model, tuples);

        var decision = await evaluator.EvaluateAsync(
            alice,
            new ActionType("read"),
            new PolicyResource("nonexistent_type", "x"),
            Now());

        Assert.Equal(DecisionKind.Indeterminate, decision.Kind);
        Assert.Contains("Unknown type", decision.Reason);
    }

    [Fact]
    public async Task Evaluate_UnknownRelationOnType_ReturnsDeny()
    {
        var alice = MakeSubject();
        // Type exists but has no 'can_read' relation.
        var model = PolicyModel.Create()
            .Type("document", t => t.Relation("owner", new RelationRewrite.DirectUsers(new[] { "user" })))
            .Build();
        var tuples = new InMemoryRelationTupleStore();
        var evaluator = new ReBACPolicyEvaluator(model, tuples);

        var decision = await evaluator.EvaluateAsync(
            alice,
            new ActionType("read"),
            new PolicyResource("document", "doc-1"),
            Now());

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Contains("No relation", decision.Reason);
    }
}
