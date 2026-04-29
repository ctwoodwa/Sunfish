namespace Sunfish.Foundation.Taxonomy.Tests;

public sealed class TaxonomyVersionTests
{
    [Fact]
    public void V1_0_0_Constant_Equals_Triple()
    {
        Assert.Equal(new TaxonomyVersion(1, 0, 0), TaxonomyVersion.V1_0_0);
    }

    [Fact]
    public void Parse_RoundTrips()
    {
        var v = TaxonomyVersion.Parse("2.3.5");
        Assert.Equal(new TaxonomyVersion(2, 3, 5), v);
        Assert.Equal("2.3.5", v.ToString());
    }

    [Fact]
    public void Parse_RejectsTooFewSegments()
    {
        Assert.Throws<FormatException>(() => TaxonomyVersion.Parse("1.0"));
    }

    [Fact]
    public void Parse_RejectsNegative()
    {
        Assert.Throws<FormatException>(() => TaxonomyVersion.Parse("-1.0.0"));
    }

    [Fact]
    public void Parse_RejectsNonInteger()
    {
        Assert.Throws<FormatException>(() => TaxonomyVersion.Parse("a.b.c"));
    }
}
