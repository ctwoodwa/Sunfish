using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Validation;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.People.Foundation.Tests;

public class PartyRoleTests
{
    private static TenantId Tenant() => new("acme");
    private static PartyId Actor() => PartyId.NewId();

    private static PartyRole NewRole(string roleName, string roleRecordId = "consumer-record-123") =>
        PartyRole.Create(Tenant(), PartyId.NewId(), roleName, roleRecordId, Actor());

    [Theory]
    [InlineData(PartyRoleName.Customer)]
    [InlineData(PartyRoleName.Tenant)]
    [InlineData(PartyRoleName.Vendor)]
    [InlineData(PartyRoleName.Contractor)]
    [InlineData(PartyRoleName.Employee)]
    public void Create_WithKnownRoleName_Passes(string code)
    {
        var r = PartyRoleValidator.Validate(NewRole(code));
        Assert.True(r.IsValid, string.Join("; ", r.Errors));
    }

    [Theory]
    [InlineData("landlord")]     // future-canonical code, not yet in registry
    [InlineData("loan-officer")] // multi-word kebab-case
    [InlineData("agent-2")]      // digits allowed
    public void Create_WithUnknownButShapeValidRoleName_Passes(string code)
    {
        var r = PartyRoleValidator.Validate(NewRole(code));
        Assert.True(r.IsValid, $"Unknown shape-valid codes are accepted per CRDT §5. Got: {string.Join("; ", r.Errors)}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyRoleName_Fails(string code)
    {
        var r = PartyRoleValidator.Validate(NewRole(code));
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Customer")]      // uppercase letter
    [InlineData("VENDOR")]
    [InlineData("Tenant-Of-Record")]
    public void Validate_RoleNameWithUppercase_Fails(string code)
    {
        var r = PartyRoleValidator.Validate(NewRole(code));
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("kebab-case", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RoleNameWithSpaces_Fails()
    {
        var r = PartyRoleValidator.Validate(NewRole("property owner"));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Validate_RoleNameWithUnderscores_Fails()
    {
        var r = PartyRoleValidator.Validate(NewRole("loan_officer"));
        Assert.False(r.IsValid);
    }

    [Theory]
    [InlineData("-customer")]
    [InlineData("customer-")]
    [InlineData("--customer")]
    public void Validate_RoleNameWithDashAtEdgeOrDouble_Fails(string code)
    {
        var r = PartyRoleValidator.Validate(NewRole(code));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Validate_RoleNameTooLong_Fails()
    {
        var sixtyFive = new string('a', 65);
        var r = PartyRoleValidator.Validate(NewRole(sixtyFive));
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("64", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EmptyRoleRecordId_Fails()
    {
        var r = PartyRoleValidator.Validate(NewRole(PartyRoleName.Customer, roleRecordId: ""));
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("RoleRecordId", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EndedBeforeStarted_Fails()
    {
        var started = new Instant(DateTimeOffset.Parse("2026-06-01T00:00:00Z"));
        var ended = new Instant(DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        var role = PartyRole.Create(Tenant(), PartyId.NewId(), PartyRoleName.Tenant, "lease-1", Actor(), startedAt: started)
            with { EndedAt = ended };
        var r = PartyRoleValidator.Validate(role);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("EndedAt", StringComparison.Ordinal));
    }

    [Fact]
    public void End_ReturnsNewRecordWithBumpedVersionAndUpdatedAt()
    {
        var role = NewRole(PartyRoleName.Tenant);
        Assert.True(role.IsActive);

        var endedAt = new Instant(role.StartedAt.Value.AddDays(180));
        var ender = Actor();
        var ended = role.End(endedAt, "lease term complete", ender);

        Assert.False(ended.IsActive);
        Assert.Equal(endedAt, ended.EndedAt);
        Assert.Equal("lease term complete", ended.EndedReason);
        Assert.Equal(ender, ended.UpdatedBy);
        Assert.Equal(endedAt, ended.UpdatedAt);
        Assert.Equal(role.Version + 1, ended.Version);
        // Original record untouched (record-with semantics).
        Assert.True(role.IsActive);
        Assert.Null(role.EndedAt);
    }

    [Fact]
    public void IsKnown_KnownCodes_All5ReturnTrue()
    {
        Assert.True(PartyRoleName.IsKnown(PartyRoleName.Customer));
        Assert.True(PartyRoleName.IsKnown(PartyRoleName.Tenant));
        Assert.True(PartyRoleName.IsKnown(PartyRoleName.Vendor));
        Assert.True(PartyRoleName.IsKnown(PartyRoleName.Contractor));
        Assert.True(PartyRoleName.IsKnown(PartyRoleName.Employee));
    }

    [Theory]
    [InlineData("landlord")]
    [InlineData("lead")]
    [InlineData("applicant")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Customer")]  // case-sensitive lookup
    public void IsKnown_UnknownOrInvalidCode_ReturnsFalse(string? code)
    {
        Assert.False(PartyRoleName.IsKnown(code));
    }

    [Fact]
    public void PartyRoleName_AllContainsExactly5Codes()
    {
        Assert.Equal(5, PartyRoleName.All.Count);
        Assert.Contains(PartyRoleName.Customer, PartyRoleName.All);
        Assert.Contains(PartyRoleName.Tenant, PartyRoleName.All);
        Assert.Contains(PartyRoleName.Vendor, PartyRoleName.All);
        Assert.Contains(PartyRoleName.Contractor, PartyRoleName.All);
        Assert.Contains(PartyRoleName.Employee, PartyRoleName.All);
    }
}
