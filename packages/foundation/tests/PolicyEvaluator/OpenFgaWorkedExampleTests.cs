using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.PolicyEvaluator;
using Xunit;

namespace Sunfish.Foundation.Tests.PolicyEvaluator;

/// <summary>
/// Exercises the spec §3.5 worked example:
///   - Jim is an employee of acmeFirm.
///   - acmeFirm is the pm_firm of property:42.
///   - Therefore Jim can inspect an inspection tied to property:42.
///
/// Also pins down a negative case: a prospective buyer with no tuple relationship to a lease
/// cannot read it.
/// </summary>
public class OpenFgaWorkedExampleTests
{
    private static ContextEnvelope TodayAt(DateTimeOffset when) => new(when, Purpose: "inspection");

    private static Subject MakeSubject()
    {
        using var kp = KeyPair.Generate();
        return new Subject(kp.PrincipalId);
    }

    /// <summary>Builds the spec §3.5 policy model.</summary>
    private static PolicyModel BuildPolicyModel()
    {
        return PolicyModel.Create()
            .Type("inspection_firm", t => t
                .Relation("employee", new RelationRewrite.DirectUsers(new[] { "user" })))
            .Type("property", t => t
                .Relation("landlord", new RelationRewrite.DirectUsers(new[] { "user" }))
                .Relation("pm_firm", new RelationRewrite.DirectUsers(new[] { "inspection_firm" }))
                // can_inspect = landlord  OR  employees-of-pm_firm
                .Relation("can_inspect", new RelationRewrite.Union(new RelationRewrite[]
                {
                    new RelationRewrite.ComputedUserset("landlord"),
                    new RelationRewrite.TupleToUserset("pm_firm", "employee"),
                })))
            .Type("inspection", t => t
                .Relation("property", new RelationRewrite.DirectUsers(new[] { "property" }))
                // can_read = anyone who can_inspect the underlying property
                .Relation("can_read", new RelationRewrite.TupleToUserset("property", "can_inspect")))
            .Type("lease", t => t
                .Relation("tenant", new RelationRewrite.DirectUsers(new[] { "user" }))
                .Relation("landlord", new RelationRewrite.DirectUsers(new[] { "user" }))
                .Relation("can_read", new RelationRewrite.Union(new RelationRewrite[]
                {
                    new RelationRewrite.ComputedUserset("tenant"),
                    new RelationRewrite.ComputedUserset("landlord"),
                })))
            .Build();
    }

    [Fact]
    public async Task JimCanInspect_BecauseJimIsInAcmeWhichIsPmFirmOfProperty42()
    {
        var model = BuildPolicyModel();
        var tuples = new InMemoryRelationTupleStore();

        var jim = MakeSubject();
        var landlord = MakeSubject();

        var property42 = new PolicyResource("property", "42");
        var acmeFirm = new PolicyResource("inspection_firm", "acme");
        var today = new PolicyResource("inspection", "2026-04-17");

        // Seed: landlord is landlord of property 42
        await tuples.AddSubjectAsync(landlord, "landlord", property42);
        // Seed: acmeFirm (as a resource) is the pm_firm of property 42
        await tuples.AddAsync(new UsersetRef.SelfRef(acmeFirm), "pm_firm", property42);
        // Seed: jim is an employee of acmeFirm
        await tuples.AddSubjectAsync(jim, "employee", acmeFirm);
        // Seed: today's inspection is tied to property 42
        await tuples.AddAsync(new UsersetRef.SelfRef(property42), "property", today);

        var evaluator = new ReBACPolicyEvaluator(model, tuples);
        var decision = await evaluator.EvaluateAsync(
            jim,
            new ActionType("read"),
            today,
            TodayAt(DateTimeOffset.Parse("2026-04-17T10:00:00Z")));

        Assert.Equal(DecisionKind.Permit, decision.Kind);
        Assert.Single(decision.MatchedPolicies);
        Assert.Equal("inspection#can_read", decision.MatchedPolicies[0]);
    }

    [Fact]
    public async Task ProspectiveBuyerCannotReadLease()
    {
        var model = BuildPolicyModel();
        var tuples = new InMemoryRelationTupleStore();

        var tenant = MakeSubject();
        var landlord = MakeSubject();
        var buyer = MakeSubject();   // not tenant, not landlord

        var lease = new PolicyResource("lease", "lease-42");

        await tuples.AddSubjectAsync(tenant, "tenant", lease);
        await tuples.AddSubjectAsync(landlord, "landlord", lease);

        var evaluator = new ReBACPolicyEvaluator(model, tuples);

        // Sanity: tenant and landlord can read.
        var tenantDecision = await evaluator.EvaluateAsync(tenant, new ActionType("read"), lease, new ContextEnvelope(DateTimeOffset.UtcNow, null));
        var landlordDecision = await evaluator.EvaluateAsync(landlord, new ActionType("read"), lease, new ContextEnvelope(DateTimeOffset.UtcNow, null));
        Assert.Equal(DecisionKind.Permit, tenantDecision.Kind);
        Assert.Equal(DecisionKind.Permit, landlordDecision.Kind);

        // The buyer, with no tuple, cannot.
        var buyerDecision = await evaluator.EvaluateAsync(buyer, new ActionType("read"), lease, new ContextEnvelope(DateTimeOffset.UtcNow, null));
        Assert.Equal(DecisionKind.Deny, buyerDecision.Kind);
    }
}
