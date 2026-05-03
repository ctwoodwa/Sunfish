using Sunfish.Foundation.PolicyEvaluator;
using Xunit;

namespace Sunfish.Foundation.Tests.PolicyEvaluator;

public class PolicyModelBuilderTests
{
    [Fact]
    public void Build_PreservesTypeOrder()
    {
        var model = PolicyModel.Create()
            .Type("property")
            .Type("inspection_firm")
            .Type("inspection")
            .Type("lease")
            .Build();

        var typeNames = model.Types.Keys.ToArray();
        Assert.Equal(new[] { "property", "inspection_firm", "inspection", "lease" }, typeNames);
    }

    [Fact]
    public void Build_AllowsUnionRelation()
    {
        var union = new RelationRewrite.Union(new RelationRewrite[]
        {
            new RelationRewrite.DirectUsers(new[] { "user" }),
            new RelationRewrite.ComputedUserset("owner"),
        });

        var model = PolicyModel.Create()
            .Type("document", t => t.Relation("can_read", union))
            .Build();

        var rewrite = model.Types["document"].Relations["can_read"];
        Assert.Equal(union, rewrite);
    }

    [Fact]
    public void Build_RejectsDuplicateTypeName()
    {
        var builder = PolicyModel.Create().Type("thing");
        var ex = Assert.Throws<ArgumentException>(() => builder.Type("thing"));
        Assert.Contains("Duplicate type 'thing'", ex.Message);
    }
}
