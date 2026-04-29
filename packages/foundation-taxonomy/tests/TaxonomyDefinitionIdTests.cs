namespace Sunfish.Foundation.Taxonomy.Tests;

public sealed class TaxonomyDefinitionIdTests
{
    [Fact]
    public void Parse_RoundTrips()
    {
        var id = TaxonomyDefinitionId.Parse("Sunfish.Signature.Scopes");
        Assert.Equal("Sunfish", id.Vendor);
        Assert.Equal("Signature", id.Domain);
        Assert.Equal("Scopes", id.TaxonomyName);
        Assert.Equal("Sunfish.Signature.Scopes", id.ToString());
    }

    [Fact]
    public void Parse_RejectsTooFewSegments()
    {
        Assert.Throws<FormatException>(() => TaxonomyDefinitionId.Parse("Sunfish.Signature"));
    }

    [Fact]
    public void Parse_RejectsTooManySegments()
    {
        Assert.Throws<FormatException>(() => TaxonomyDefinitionId.Parse("a.b.c.d"));
    }

    [Fact]
    public void Parse_RejectsLeadingDigit()
    {
        Assert.Throws<FormatException>(() => TaxonomyDefinitionId.Parse("Sunfish.1Signature.Scopes"));
    }

    [Fact]
    public void Parse_RejectsNonAlphanumericChar()
    {
        Assert.Throws<FormatException>(() => TaxonomyDefinitionId.Parse("Sunfish.Sig-nature.Scopes"));
    }

    [Fact]
    public void Validate_PassesOnValidTokens()
    {
        var id = new TaxonomyDefinitionId("Sunfish", "Signature", "Scopes");
        id.Validate();
    }

    [Fact]
    public void Validate_RejectsInvalidVendor()
    {
        var id = new TaxonomyDefinitionId("9bad", "Signature", "Scopes");
        Assert.Throws<FormatException>(() => id.Validate());
    }
}
