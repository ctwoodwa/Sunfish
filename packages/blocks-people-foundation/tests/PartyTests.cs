using System.Text.Json;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Validation;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.People.Foundation.Tests;

public class PartyTests
{
    private static TenantId Tenant() => new("acme");
    private static PartyId Actor() => PartyId.NewId();

    [Fact]
    public void Create_PersonWithGivenName_Succeeds()
    {
        var p = Party.Create(Tenant(), PartyKind.Person, "Jane Doe", Actor()) with
        {
            GivenName = "Jane",
            FamilyName = "Doe",
        };

        var r = PartyValidator.Validate(p);
        Assert.True(r.IsValid, string.Join("; ", r.Errors));
    }

    [Fact]
    public void Create_OrganizationWithLegalName_Succeeds()
    {
        var p = Party.Create(Tenant(), PartyKind.Organization, "Acme Holdings LLC", Actor()) with
        {
            LegalName = "Acme Holdings, LLC",
            LegalEntityType = "LLC",
        };

        var r = PartyValidator.Validate(p);
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Validate_PersonWithoutName_Fails()
    {
        // DisplayName is required by the record; bypass via reflection-free shape:
        // construct a Person with whitespace DisplayName and no GivenName.
        var p = Party.Create(Tenant(), PartyKind.Person, "   ", Actor());
        var r = PartyValidator.Validate(p);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("Person", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_OrganizationWithoutName_Fails()
    {
        var p = Party.Create(Tenant(), PartyKind.Organization, "   ", Actor());
        var r = PartyValidator.Validate(p);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("Organization", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_PersonWithParentOrgId_Fails()
    {
        var p = Party.Create(Tenant(), PartyKind.Person, "Jane Doe", Actor()) with
        {
            ParentOrgId = PartyId.NewId(),
        };
        var r = PartyValidator.Validate(p);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("ParentOrgId", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_OrganizationWithParentOrgId_Succeeds()
    {
        var p = Party.Create(Tenant(), PartyKind.Organization, "Acme Subsidiary LLC", Actor()) with
        {
            LegalName = "Acme Subsidiary, LLC",
            ParentOrgId = PartyId.NewId(),
        };
        var r = PartyValidator.Validate(p);
        Assert.True(r.IsValid);
    }

    [Fact]
    public void PartyKind_JsonRoundtrip_LowercasePersonOrganization()
    {
        var personJson = JsonSerializer.Serialize(PartyKind.Person);
        var orgJson = JsonSerializer.Serialize(PartyKind.Organization);
        Assert.Equal("\"person\"", personJson);
        Assert.Equal("\"organization\"", orgJson);

        Assert.Equal(PartyKind.Person, JsonSerializer.Deserialize<PartyKind>(personJson));
        Assert.Equal(PartyKind.Organization, JsonSerializer.Deserialize<PartyKind>(orgJson));
    }

    [Fact]
    public void PartyKind_JsonUnknownValue_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PartyKind>("\"alien\""));
    }

    [Fact]
    public void Create_PopulatesCrdtEnvelope()
    {
        var actor = Actor();
        var p = Party.Create(Tenant(), PartyKind.Person, "Jane Doe", actor) with
        {
            GivenName = "Jane",
        };

        Assert.Equal(1, p.Version);
        Assert.Equal(actor, p.CreatedBy);
        Assert.Equal(p.CreatedAt, p.UpdatedAt); // baseline equality on creation
        Assert.Empty(p.RevisionVector);
        Assert.Null(p.DeletedAt);
    }

    [Fact]
    public void PartyId_JsonRoundtrip_PreservesValue()
    {
        var id = PartyId.NewId();
        var json = JsonSerializer.Serialize(id);
        var roundTripped = JsonSerializer.Deserialize<PartyId>(json);
        Assert.Equal(id, roundTripped);
        Assert.StartsWith("\"", json);
        Assert.EndsWith("\"", json);
    }
}
